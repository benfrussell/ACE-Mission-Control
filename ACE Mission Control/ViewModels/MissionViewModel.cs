using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.UI.Xaml.Controls;
using GalaSoft.MvvmLight;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight.Command;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight.Messaging;
using System.ComponentModel;
using Windows.ApplicationModel.Core;
using static ACE_Mission_Control.Core.Models.ACETypes;

namespace ACE_Mission_Control.ViewModels
{
    public class ShowPassphraseDialogMessage : MessageBase { }
    public class HidePassphraseDialogMessage : MessageBase { }
    public class MissionViewModel : DroneViewModelBase
    {
        private Symbol _obcAlertSymbol;
        public Symbol OBCAlertSymbol
        {
            set
            {
                if (_obcAlertSymbol == value)
                    return;
                _obcAlertSymbol = value;
                RaisePropertyChanged("OBCStatusSymbol");
            }
            get
            {
                return _obcAlertSymbol;
            }
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

        private Symbol _lockButtonSymbol;
        public Symbol LockButtonSymbol
        {
            get { return _lockButtonSymbol; }
            set
            {
                if (value == _lockButtonSymbol)
                    return;
                _lockButtonSymbol = value;
                RaisePropertyChanged("LockButtonSymbol");
            }
        }

        private bool _lockButtonEnabled;
        public bool LockButtonEnabled
        {
            get { return _lockButtonEnabled; }
            set
            {
                if (value == _lockButtonEnabled)
                    return;
                _lockButtonEnabled = value;
                RaisePropertyChanged("LockButtonEnabled");
            }
        }

        private string _passDialogErrorText;
        public string PassDialogErrorText
        {
            get { return _passDialogErrorText; }
            set
            {
                if (value == _passDialogErrorText)
                    return;
                _passDialogErrorText = value;
                RaisePropertyChanged("PassDialogErrorText");
            }
        }

        private string _passDialogInputText;
        public string PassDialogInputText
        {
            get { return _passDialogInputText; }
            set
            {
                if (_passDialogInputText == value)
                    return;
                _passDialogInputText = value;
                RaisePropertyChanged("PassDialogInputText");
            }
        }

        private bool _passDialogLoading;
        public bool PassDialogLoading
        {
            get { return _passDialogLoading; }
            set
            {
                if (value == _passDialogLoading)
                    return;
                _passDialogLoading = value;
                RaisePropertyChanged("PassDialogLoading");
            }
        }

        private static bool passDiagShown = false;

        public RelayCommand LockButtonCommand => new RelayCommand(() => lockButtonClicked());

        public RelayCommand PassDialogEnterCommand => new RelayCommand(() => {
            PassDialogErrorText = "";
            PassDialogLoading = true;
            passDialogEntered(); }
        );

        public MissionViewModel()
        {
            System.Diagnostics.Debug.WriteLine("New instance.");
            OBCAlertSymbol = Symbol.Message;
            UGCSMissionRetrieveText = "Never!";
        }

        protected override void DroneAttached(bool firstTime)
        {
            LockButtonSymbol = OnboardComputerController.KeyOpen ? Symbol.Accept : Symbol.Permissions;
            LockButtonEnabled = !OnboardComputerController.KeyOpen;
            OBCStatusText = AttachedDrone.OBCClient.Status.ToString();

            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.OBCClient.Alerts.CollectionChanged += Alerts_CollectionChanged;
            OnboardComputerController.StaticPropertyChanged += OnboardComputerClient_StaticPropertyChanged;
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.OBCClient.Alerts.CollectionChanged -= Alerts_CollectionChanged;
            OnboardComputerController.StaticPropertyChanged -= OnboardComputerClient_StaticPropertyChanged;
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
            if (alert.Level == AlertLevel.Info)
                OBCAlertSymbol = Symbol.Message;
            else if (alert.Level == AlertLevel.Medium)
                OBCAlertSymbol = Symbol.Flag;
            else if (alert.Level == AlertLevel.High)
                OBCAlertSymbol = Symbol.Important;

            OBCAlert = alert;
        }

        private async void OnboardComputerClient_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "KeyOpen")
                {
                    LockButtonSymbol = OnboardComputerController.KeyOpen ? Symbol.Accept : Symbol.Permissions;
                    LockButtonEnabled = !OnboardComputerController.KeyOpen;
                }
            });
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var client = sender as OnboardComputerClient;

                if (client.AttachedDrone.ID != DroneID)
                    return;

                if (e.PropertyName == "Status")
                    OBCStatusText = client.Status.ToString();
            });
        }

        private void lockButtonClicked()
        {
            System.Diagnostics.Debug.WriteLine("Executing from " + DroneID);
            Messenger.Default.Send(new ShowPassphraseDialogMessage());
        }

        private async void passDialogEntered()
        {
            string response = await OnboardComputerController.OpenPrivateKeyAsync(PassDialogInputText);
            PassDialogLoading = false;
            if (response != null)
                PassDialogErrorText = response;
            else
                Messenger.Default.Send(new HidePassphraseDialogMessage());
        }
    }
}
