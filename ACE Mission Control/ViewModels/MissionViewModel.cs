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

namespace ACE_Mission_Control.ViewModels
{
    public class ShowPassphraseDialogMessage : MessageBase { }
    public class HidePassphraseDialogMessage : MessageBase { }
    public class MissionViewModel : DroneViewModelBase
    {
        private Symbol _obcStatusSymbol;
        public Symbol OBCStatusSymbol
        {
            set
            {
                _obcStatusSymbol = value;
            }
            get
            {
                return _obcStatusSymbol;
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
                    return "Loading page...";
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
            OBCStatusSymbol = Symbol.Find;
            UGCSMissionRetrieveText = "Never!";
        }

        protected override void DroneAttached(bool firstTime)
        {
            if (!firstTime)
                return;

            LockButtonSymbol = OnboardComputerController.KeyOpen ? Symbol.Accept : Symbol.Permissions;
            LockButtonEnabled = !OnboardComputerController.KeyOpen;

            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            OnboardComputerController.StaticPropertyChanged += OnboardComputerClient_StaticPropertyChanged;
            OBCStatusText = AttachedDrone.OBCClient.Status.ToString();
        }

        protected override void DroneUnattaching()
        {

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
