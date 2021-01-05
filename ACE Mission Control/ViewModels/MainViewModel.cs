using System;
using System.ComponentModel;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;

namespace ACE_Mission_Control.ViewModels
{
    public class MainViewModel : DroneViewModelBase
    {
        public string DroneName
        {
            get { return AttachedDrone.Name; }
        }

        private string _obcStatusText;
        public string OBCStatusText
        {
            set
            {
                if (value == _obcStatusText)
                    return;
                _obcStatusText = value;
                RaisePropertyChanged("OBCStatusText");
            }
            get
            {
                if (IsDroneAttached)
                    return _obcStatusText;
                else
                    return "Loading associated drone...";
            }
        }

        private string _obcConnectedText;
        public string OBCConnectedText
        {
            set
            {
                if (value == _obcConnectedText)
                    return;
                _obcConnectedText = value;
                RaisePropertyChanged("OBCConnectedText");
            }
            get
            {
                if (IsDroneAttached)
                    return _obcConnectedText;
                else
                    return "Loading associated drone...";
            }
        }

        private Symbol _obcAlertSymbol;
        public Symbol OBCAlertSymbol
        {
            set
            {
                if (_obcAlertSymbol == value)
                    return;
                _obcAlertSymbol = value;
                RaisePropertyChanged("OBCAlertSymbol");
            }
            get
            {
                return _obcAlertSymbol;
            }
        }

        private AlertEntry _obcAlert;
        public AlertEntry OBCAlert
        {
            set
            {
                _obcAlert = value;
                RaisePropertyChanged("OBCAlert");
            }
            get
            {
                return _obcAlert;
            }
        }

        private string _ugcsMissionRetrieveText;
        public string UGCSMissionRetrieveText
        {
            set { _ugcsMissionRetrieveText = value; }
            get { return _ugcsMissionRetrieveText; }
        }

        public MainViewModel()
        {
        }

        protected override void DroneAttached(bool firstTime)
        {
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged += Alerts_CollectionChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            OBCStatusText = AttachedDrone.MissionStage.ToString();
            OBCConnectedText = AttachedDrone.OBCClient.IsConnected ? "Connected" : "Not Connected";
        }

        private async void AttachedDrone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "MissionStage")
                    OBCStatusText = AttachedDrone.MissionStage.ToString();
            });
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged -= Alerts_CollectionChanged;
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var client = sender as OnboardComputerClient;

                if (client.AttachedDrone.ID != DroneID)
                    return;

                if (e.PropertyName == "IsConnected")
                    OBCConnectedText = AttachedDrone.OBCClient.IsConnected ? "Connected" : "Not Connected";
            });
        }

        private async void Alerts_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    ProcessAlertChanges(e.NewItems);
                }
            });
        }

        private void ProcessAlertChanges(System.Collections.IList newItems)
        {
            AlertEntry alert = (AlertEntry)newItems[0];
            if (alert.Level == AlertEntry.AlertLevel.Info)
                OBCAlertSymbol = Symbol.Message;
            else if (alert.Level == AlertEntry.AlertLevel.Medium)
                OBCAlertSymbol = Symbol.Flag;
            else if (alert.Level == AlertEntry.AlertLevel.High)
                OBCAlertSymbol = Symbol.Important;

            OBCAlert = alert;
        }
    }
}
