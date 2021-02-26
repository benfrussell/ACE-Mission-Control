using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Pbdrone;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;

namespace ACE_Mission_Control.ViewModels
{
    public class ConfigViewModel : DroneViewModelBase
    {
        private string _hostname_text;
        public string Hostname_Text
        {
            get { return _hostname_text; }
            set
            {
                if (_hostname_text != value)
                {
                    UnsavedChanges = (AttachedDrone.OBCClient.Hostname != Hostname_Text);
                    _hostname_text = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _unsaved_changes = false;
        public bool UnsavedChanges
        {
            get { return _unsaved_changes; }
            set
            {
                if (_unsaved_changes != value)
                {
                    _unsaved_changes = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ManualCommandsChecked
        {
            get { return AttachedDrone.ManualCommandsOnly; }
        }

        private ObservableCollection<Tuple<string, string>> entriesFound;
        public ObservableCollection<Tuple<string, string>> EntriesFound
        {
            get { return entriesFound; }
            private set
            {
                if (value == entriesFound)
                    return;
                entriesFound = value;
                RaisePropertyChanged();
            }
        }

        private bool searching;
        public bool Searching
        {
            get { return searching; }
            private set
            {
                if (value == searching)
                    return;
                searching = value;
                RaisePropertyChanged();
            }
        }

        private int progress;
        public int Progress
        {
            get { return progress; }
            private set
            {
                if (value == progress)
                    return;
                progress = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<ConfigEntry> _configEntries;
        public ObservableCollection<ConfigEntry> ConfigEntries
        {
            get => _configEntries;
            set
            {
                if (_configEntries == value)
                    return;
                _configEntries = value;
                RaisePropertyChanged();
            }
        }


        public RelayCommand ApplyChangesCommand => new RelayCommand(() => applyButtonClicked());
        public RelayCommand ResetChangesCommand => new RelayCommand(() => resetButtonClicked());
        public RelayCommand ManualCommandsCheckedCommand => new RelayCommand(() => manualCommandsCheckedCommand());
        public RelayCommand StartSearch => new RelayCommand(() => IPLookup.LookupIPs("gdg-pi"));
        public RelayCommand<ListView> SearchResultClickedCommand => new RelayCommand<ListView>((v) => searchResultClicked(v));
        public RelayCommand<DataGridCellEditEndedEventArgs> ConfigureOptionEdited => new RelayCommand<DataGridCellEditEndedEventArgs>((e) => configureOptionEdited(e));

        public ConfigViewModel()
        {
            IPLookup.StaticPropertyChanged += IPLookup_StaticPropertyChanged;
            EntriesFound = IPLookup.EntriesFound;
            Searching = IPLookup.Searching;
            Progress = IPLookup.Progress;
        }

        protected override void DroneAttached(bool firstTime)
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
            RaisePropertyChanged("ManualCommandsChecked");
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            ConfigEntries = new ObservableCollection<ConfigEntry>(AttachedDrone.ConfigEntries.Select(i => i.Clone()));
        }

        private void IPLookup_StaticPropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            switch (e.PropertyName)
            {
                case "Searching":
                    Searching = IPLookup.Searching;
                    break;
                case "Progress":
                    Progress = IPLookup.Progress;
                    break; 
            }
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
        }

        private async void AttachedDrone_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "ManualCommandsOnly")
                    RaisePropertyChanged("ManualCommandsChecked");
                else if (e.PropertyName == "ConfigEntries")
                    ConfigEntries = new ObservableCollection<ConfigEntry>(AttachedDrone.ConfigEntries.Select(i => i.Clone()));
            });
        }

        private void applyButtonClicked()
        {
            AttachedDrone.OBCClient.Configure(Hostname_Text);
            UnsavedChanges = false;
        }

        private void resetButtonClicked()
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
        }

        private void manualCommandsCheckedCommand()
        {
            AttachedDrone.ManualCommandsOnly = !AttachedDrone.ManualCommandsOnly;
        }

        private void searchResultClicked(ListView list)
        {
            if (list.SelectedItem == null)
                return;
            var item = list.SelectedItem as Tuple<string, string>;
            Hostname_Text = item.Item2;
            applyButtonClicked();
        }

        private void configureOptionEdited(DataGridCellEditEndedEventArgs e)
        {
            ConfigEntry entry = (ConfigEntry)e.Row.DataContext;
            AttachedDrone.SendCommand($"set_config_entry -id {entry.Id} -value {entry.Value}", tag: entry);
        }
    }
}
