using System;
using System.Collections.Generic;
using Windows.UI.Xaml.Controls;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight.Command;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight.Messaging;
using System.ComponentModel;
using Windows.ApplicationModel.Core;
using System.Collections.ObjectModel;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;
using Windows.ApplicationModel.DataTransfer;
using Microsoft.Toolkit.Uwp.UI.Controls;

namespace ACE_Mission_Control.ViewModels
{
    // --- Messages

    public class ScrollAlertDataGridMessage : MessageBase { public AlertEntry newEntry { get; set; } }

    // --- Properties

    public class MissionViewModel : DroneViewModelBase
    {
        private bool _droneConnectionOn;
        public bool DroneConnectionOn
        {
            get { return _droneConnectionOn; }
            set
            {
                if (_droneConnectionOn == value)
                    return;
                _droneConnectionOn = value;
                RaisePropertyChanged();
            }
        }

        private ObservableCollection<AlertEntry> _alerts;
        public ObservableCollection<AlertEntry> Alerts
        {
            get { return _alerts; }
        }

        public MissionViewModel()
        {
            _alerts = new ObservableCollection<AlertEntry>();
        }

        protected override void DroneAttached(bool firstTime)
        {
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged += AlertLog_CollectionChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;

            DroneConnectionOn = AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting ||
                AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online;

            _alerts = new ObservableCollection<AlertEntry>(AttachedDrone.AlertLog);
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "IsDirectorConnected")
                {
                    RaisePropertyChanged("IsDirectorConnected");
                    connectDroneCommand();
                }
                else if (e.PropertyName == "IsChaperoneConnected")
                {
                    RaisePropertyChanged("OBCChaperoneConnected");
                }
            });
        }

        private async void AttachedDrone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (e.PropertyName)
                {
                    case "OBCCanBeTested":
                        RaisePropertyChanged("OBCCanBeTested");
                        break;
                }
            });
        }

        private async void AlertLog_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    foreach (AlertEntry entry in e.NewItems)
                        _alerts.Add(entry);
                var msg = new ScrollAlertDataGridMessage() { newEntry = _alerts[_alerts.Count - 1] };
                Messenger.Default.Send(msg);
            });
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged -= AlertLog_CollectionChanged;
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
        }

        // --- OBC Commands

        public RelayCommand ConnectOBCCommand => new RelayCommand(() => connectOBCCommand());
        private void connectOBCCommand()
        {
            if (AttachedDrone.OBCClient.AutoTryingConnections)
            {
                AttachedDrone.OBCClient.StopTryingConnections();
                AttachedDrone.OBCClient.Disconnect();
            }
            else
            {
                AttachedDrone.OBCClient.StartTryingConnections();
            }
                
        }

        public RelayCommand<ToggleSwitch> ConnectDroneCommand => new RelayCommand<ToggleSwitch>((toggle) => connectDroneCommand(toggle));
        private void connectDroneCommand(ToggleSwitch toggle = null)
        {
            bool connectDrone = toggle == null ? DroneConnectionOn : toggle.IsOn;
            if (connectDrone)
            {
                if (AttachedDrone.OBCClient.IsDirectorConnected && AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Offline)
                    AttachedDrone.SendCommand("start_interface");
            }
            else
            {
                if (AttachedDrone.OBCClient.IsDirectorConnected &&
                    (AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting ||
                    AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online))
                    AttachedDrone.SendCommand("stop_interface");
            }
        }

        public RelayCommand RestartOBCCommand => new RelayCommand(() => restartOBCCommand());
        private void restartOBCCommand()
        {
            AttachedDrone.SendCommand("start_director");
        }

        public RelayCommand TestPayloadCommand => new RelayCommand(() => testPayloadCommand());
        private void testPayloadCommand()
        {
            AttachedDrone.SendCommand("test_payload");
        }

        public RelayCommand TestInterfaceCommand => new RelayCommand(() => testInterfaceCommand());
        private void testInterfaceCommand()
        {
            AttachedDrone.SendCommand("test_interface");
        }

        public RelayCommand<DataGrid> AlertCopyCommand => new RelayCommand<DataGrid>((grid) => alertCopyCommand(grid));
        private void alertCopyCommand(DataGrid grid)
        {
            string copiedText = "";
            AlertToString alertConverter = new AlertToString();
            foreach (object item in grid.SelectedItems)
            {
                var entry = (AlertEntry)item;
                copiedText += entry.Timestamp.ToLongTimeString() + " " + alertConverter.Convert(entry, typeof(string), null, null) + "\n";
            }
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(copiedText);
            Clipboard.SetContent(dataPackage);
        }
    }
}
