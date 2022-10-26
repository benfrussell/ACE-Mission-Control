using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Windows.UI.Xaml.Data;
using ACE_Mission_Control.Core.Models;
using System.Collections.ObjectModel;
using Windows.Storage.Pickers;
using Windows.Storage;
using Windows.Storage.Search;
using System.IO;
using Windows.UI.Xaml.Controls;

namespace ACE_Mission_Control.ViewModels
{
    public class FlightTimeViewModel : ViewModelBase
    {
        private string _flightTimeTitle;
        public string FlightTimeTitle
        {
            get { return _flightTimeTitle; }
            set { Set(ref _flightTimeTitle, value); }
        }

        private CollectionViewSource flightTimeSource;
        private LogReader logReader;

        private bool pilotColumnsVisible = false;
        public bool PilotColumnsVisible
        {
            get { return pilotColumnsVisible; }
            set
            {
                if (pilotColumnsVisible != value)
                {
                    pilotColumnsVisible = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool machineColumnsVisible = true;
        public bool MachineColumnsVisible
        {
            get { return machineColumnsVisible; }
            set
            {
                if (machineColumnsVisible != value)
                {
                    machineColumnsVisible = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool showProgressRing = false;
        public bool ShowProgressRing
        {
            get { return showProgressRing; }
            set
            {
                if (showProgressRing != value)
                {
                    showProgressRing = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int progress = 0;
        public int Progress
        {
            get { return progress; }
            set
            {
                if (progress != value)
                {
                    progress = value;
                    RaisePropertyChanged();
                }
            }
        }

        private int progressMax = 0;
        public int ProgressMax
        {
            get { return progressMax; }
            set
            {
                if (progressMax != value)
                {
                    progressMax = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string progressText = "0/0";
        public string ProgressText
        {
            get { return progressText; }
            set
            {
                if (progressText != value)
                {
                    progressText = value;
                    RaisePropertyChanged();
                }
            }
        }

        public ICollectionView FlightTimeCollection { get => flightTimeSource.View; }

        public FlightTimeViewModel()
        {
            FlightTimeTitle = "Shell_FlightTimeItem".GetLocalized();
            flightTimeSource = new CollectionViewSource();
            flightTimeSource.IsSourceGrouped = true;
            logReader = new LogReader();
        }

        public RelayCommand MachineViewButtonClickedCommand => new RelayCommand(() => GroupFlightTimeByMachine());

        private void GroupFlightTimeByMachine()
        {
            PilotColumnsVisible = false;
            MachineColumnsVisible = true;
            var query =
                from e in logReader.Entries
                group e by e.Machine into m
                orderby m.Key
                select m;
            flightTimeSource.Source = query;
            RaisePropertyChanged("FlightTimeCollection");
        }

        public RelayCommand PilotViewButtonClickedCommand => new RelayCommand(() => GroupFlightTimeByPilot());

        private void GroupFlightTimeByPilot()
        {
            PilotColumnsVisible = true;
            MachineColumnsVisible = false;
            var query =
                from e in logReader.Entries
                group e by e.Pilot into m
                orderby m.Key
                select m;
            flightTimeSource.Source = query;
            RaisePropertyChanged("FlightTimeCollection");
        }

        public RelayCommand LoadLogsButtonClickedCommand => new RelayCommand(() => loadLogsButtonClicked());

        private async void loadLogsButtonClicked()
        {
            // Test broad file access
            try
            {
                StorageFolder testFolder = await StorageFolder.GetFolderFromPathAsync(@"C:\");
            }
            catch (UnauthorizedAccessException)
            {
                showNoAccessDialog();
                return;
            }

            logReader.ClearEntries();

            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");
            var folder = await folderPicker.PickSingleFolderAsync();

            if (folder != null)
                getLogsFromDirectory(folder);
        }

        public RelayCommand ExportLogsButtonClickedCommand => new RelayCommand(() => exportLogsButtonClicked());

        private async void exportLogsButtonClicked()
        {
            var savePicker = new FileSavePicker();
            savePicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            savePicker.FileTypeChoices.Add("Comma Separated Values", new List<string>() { ".csv" });
            savePicker.SuggestedFileName = $"{DateTime.Today.ToShortDateString()} Flight Log Export";
            var file = await savePicker.PickSaveFileAsync();

            if (file != null)
                logReader.ExportEntries(new StreamWriter(await file.OpenStreamForWriteAsync()));
        }

        private async void showNoAccessDialog()
        {
            ContentDialog dialog = new ContentDialog
            {
                Title = "Time_NoAccessTitle".GetLocalized(),
                Content = "Time_NoAccessContent".GetLocalized(),
                CloseButtonText = "OK"
            };

            ContentDialogResult result = await dialog.ShowAsync();

            await Windows.System.Launcher.LaunchUriAsync(new Uri("ms-settings:privacy-broadfilesystemaccess"));
        }

        private async void getLogsFromDirectory(StorageFolder directory)
        {
            var allFilesQuery = directory.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, new List<string>() { ".csv" }));
            var allFilesCount = (await allFilesQuery.GetFilesAsync()).Count;

            ShowProgressRing = true;
            Progress = 0;
            ProgressMax = allFilesCount;
            ProgressText = $"{Progress}/{allFilesCount}";

            var csvShallowQuery = directory.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, new List<string>() { ".csv" }));
            var csvShallowQueryResult = await csvShallowQuery.GetFilesAsync();
            foreach (StorageFile csvResult in csvShallowQueryResult)
            {
                await logReader.ReadAsync(
                    LogReader.GetDateFromFilename(csvResult.DisplayName),
                    "Unnamed",
                    LogReader.GetMachineNameFromFilename(csvResult.DisplayName),
                    new StreamReader(await csvResult.OpenStreamForReadAsync()));
                Progress += 1;
                ProgressText = $"{Progress}/{allFilesCount}";
            }
                

            var subdirectories = await directory.GetFoldersAsync();

            foreach (StorageFolder subdirectory in subdirectories)
            {
                var csvDeepQuery = subdirectory.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByName, new List<string>() { ".csv" }));
                var csvDeepQueryResult = await csvDeepQuery.GetFilesAsync();

                var resultList = csvDeepQueryResult.ToList();
                string pilotName = subdirectory.Name;

                foreach (StorageFile csvResult in csvDeepQueryResult)
                {
                    await logReader.ReadAsync(
                        LogReader.GetDateFromFilename(csvResult.DisplayName),
                        pilotName,
                        LogReader.GetMachineNameFromFilename(csvResult.DisplayName),
                        new StreamReader(await csvResult.OpenStreamForReadAsync()));
                    Progress += 1;
                    ProgressText = $"{Progress}/{allFilesCount}";
                }
                    
            }

            logReader.SortAndRecalculateEntries();

            GroupFlightTimeByMachine();

            ShowProgressRing = false;
        }
    }
}
