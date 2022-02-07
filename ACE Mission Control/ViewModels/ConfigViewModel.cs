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
        public bool ManualCommandsChecked
        {
            get { return AttachedDrone.ManualCommandsOnly; }
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


        public RelayCommand ManualCommandsCheckedCommand => new RelayCommand(() => manualCommandsCheckedCommand());
        public RelayCommand<DataGridCellEditEndedEventArgs> ConfigureOptionEdited => new RelayCommand<DataGridCellEditEndedEventArgs>((e) => configureOptionEdited(e));

        public ConfigViewModel()
        {
            
        }

        protected override void DroneAttached(bool firstTime)
        {
            RaisePropertyChanged("ManualCommandsChecked");
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            ConfigEntries = new ObservableCollection<ConfigEntry>(AttachedDrone.ConfigEntries.Select(i => i.Clone()));
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

        private void manualCommandsCheckedCommand()
        {
            AttachedDrone.ManualCommandsOnly = !AttachedDrone.ManualCommandsOnly;
        }

        private void configureOptionEdited(DataGridCellEditEndedEventArgs e)
        {
            ConfigEntry entry = (ConfigEntry)e.Row.DataContext;
            AttachedDrone.SendCommand($"set_config_entry -id {entry.Id} -value {entry.Value}", tag: entry);
        }
    }
}
