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

        public ICollectionView FlightTimeCollection { get => flightTimeSource.View; }

        public FlightTimeViewModel()
        {
            FlightTimeTitle = "Shell_FlightTimeItem".GetLocalized();
            flightTimeSource = new CollectionViewSource();
            flightTimeSource.IsSourceGrouped = true;
            logReader = new LogReader();
            logReader.LogsDirectoryLoaded += LogReader_LogsDirectoryLoaded;
        }

        private void LogReader_LogsDirectoryLoaded(object sender, EventArgs e)
        {
            GroupFlightTimeByMachine();
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
            var folderPicker = new FolderPicker();
            folderPicker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;
            folderPicker.FileTypeFilter.Add("*");

            var folder = await folderPicker.PickSingleFolderAsync();
            getLogsFromDirectory(folder);
        }

        private async void getLogsFromDirectory(StorageFolder directory)
        {
            var csvShallowQuery = directory.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.DefaultQuery, new List<string>() { ".csv" }));
            var csvShallowQueryResult = await csvShallowQuery.GetFilesAsync();
            foreach (StorageFile csvResult in csvShallowQueryResult)
                logReader.Read(
                    LogReader.GetDateFromFilename(csvResult.DisplayName),
                    "Unnamed",
                    LogReader.GetMachineNameFromFilename(csvResult.DisplayName),
                    new StreamReader(await csvResult.OpenStreamForReadAsync()));

            var subdirectories = await directory.GetFoldersAsync();

            foreach (StorageFolder subdirectory in subdirectories)
            {
                var csvDeepQuery = subdirectory.CreateFileQueryWithOptions(new QueryOptions(CommonFileQuery.OrderByDate, new List<string>() { ".csv" }));
                var csvDeepQueryResult = await csvDeepQuery.GetFilesAsync();

                var resultList = csvDeepQueryResult.ToList();
                string pilotName = subdirectory.Name;

                foreach (StorageFile csvResult in csvDeepQueryResult)
                    logReader.Read(
                    LogReader.GetDateFromFilename(csvResult.DisplayName),
                    pilotName,
                    LogReader.GetMachineNameFromFilename(csvResult.DisplayName),
                    new StreamReader(await csvResult.OpenStreamForReadAsync()));
            }

            logReader.SortAndRecalculateEntries();

            GroupFlightTimeByMachine();
        }
    }
}
