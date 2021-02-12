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

        private bool canManuallySynchronize;
        public bool CanManuallySynchronize
        {
            get { return canManuallySynchronize; }
            set
            {
                if (canManuallySynchronize == value)
                    return;
                canManuallySynchronize = value;
                RaisePropertyChanged();
            }
        }
        private void UpdateCanManuallySynchronize()
        {
            CanManuallySynchronize =
                AttachedDrone.OBCClient.IsDirectorConnected &&
                (AttachedDrone.Synchronization == Drone.SyncState.SynchronizeFailed ||
                AttachedDrone.ManualCommandsOnly);
        }

        public MissionViewModel()
        {
            
        }

        protected override void DroneAttached(bool firstTime)
        {
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            UpdateCanManuallySynchronize();
            DroneConnectionOn = AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting ||
                AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online;
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "IsDirectorConnected")
                {
                    RaisePropertyChanged("IsDirectorConnected");
                    connectDroneCommand();
                    UpdateCanManuallySynchronize();
                }
                else if (e.PropertyName == "IsChaperoneConnected")
                {
                    RaisePropertyChanged("IsChaperoneConnected");
                }
            });
        }

        private async void AttachedDrone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (e.PropertyName)
                {
                    case "InterfaceState":
                        DroneConnectionOn = AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting || AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online;
                        break;
                    case "Synchronization":
                        UpdateCanManuallySynchronize();
                        break;
                }
            });
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
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
                AttachedDrone.Mission.ResetStatus();
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

        public RelayCommand StopPayloadCommand => new RelayCommand(() => stopPayloadCommand());
        private void stopPayloadCommand()
        {
            AttachedDrone.SendCommand("force_stop_payload");
        }

        public RelayCommand SynchronizeCommand => new RelayCommand(() => synchronizeCommand());
        private void synchronizeCommand()
        {
            AttachedDrone.Synchronize(true);
        }
    }
}
