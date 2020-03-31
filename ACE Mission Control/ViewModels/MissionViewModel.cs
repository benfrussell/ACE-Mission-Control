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
using static ACE_Mission_Control.Core.Models.ACEEnums;
using System.Collections.ObjectModel;
using Windows.UI.Core;

namespace ACE_Mission_Control.ViewModels
{
    public class ShowPassphraseDialogMessage : MessageBase { }
    public class HidePassphraseDialogMessage : MessageBase { }
    public class MissionViewModel : DroneViewModelBase
    {
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

        public bool OBCConnected
        {
            get
            {
                return AttachedDrone.OBCClient.IsConnected;
            }
        }

        public bool OBCCanBeTested
        {
            get
            {
                return false;
            }
        }

        public bool MissionCanBeReset
        {
            get
            {
                return false;
            }
        }

        public bool MissionCanBeModified
        {
            get
            {
                return false;
            }
        }

        private string _missionActivatedText;
        public string MissionActivatedText
        {
            set
            {
                if (value == _missionActivatedText)
                    return;
                _missionActivatedText = value;
                RaisePropertyChanged();
            }
            get
            {
                return _missionActivatedText;
            }
        }

        public bool FlyThroughMode
        {
            get
            {
                return false;
            }
        }

        public string TreatmentDuration
        {
            get
            {
                return "90";
            }
        }

        public List<string> AvailablePayloads
        {
            get
            {
                return new List<string>();
            }
        }

        public int SelectedPayload
        {
            get
            {
                return 0;
            }
        }

        public bool UGCSConnected
        {
            get
            {
                return false;
            }
        }


        private ObservableCollection<AlertEntry> _alerts;
        public ObservableCollection<AlertEntry> Alerts
        {
            get {
                return _alerts;
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
            _alerts = new ObservableCollection<AlertEntry>();
        }

        protected override void DroneAttached(bool firstTime)
        {
            LockButtonSymbol = OnboardComputerController.KeyOpen ? Symbol.Accept : Symbol.Permissions;
            LockButtonEnabled = !OnboardComputerController.KeyOpen;

            OnboardComputerController.StaticPropertyChanged += OnboardComputerClient_StaticPropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged += AlertLog_CollectionChanged;

            _alerts = new ObservableCollection<AlertEntry>(AttachedDrone.AlertLog);
        }

        private async void AlertLog_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {

                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                    foreach (AlertEntry entry in e.NewItems)
                        _alerts.Add(entry);
            });
        }

        protected override void DroneUnattaching()
        {
            OnboardComputerController.StaticPropertyChanged -= OnboardComputerClient_StaticPropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged -= AlertLog_CollectionChanged;
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
            {
                PassDialogErrorText = response;
            }
            else
            {
                Messenger.Default.Send(new HidePassphraseDialogMessage());
                OnboardComputerController.StartTryingConnections();
            }
                
        }
    }
}
