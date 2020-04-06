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
using System.Collections.ObjectModel;
using Windows.UI.Core;
using Windows.Storage.Pickers;
using Windows.Storage;

namespace ACE_Mission_Control.ViewModels
{
    // --- Messages

    public class ShowPassphraseDialogMessage : MessageBase { }
    public class HidePassphraseDialogMessage : MessageBase { }
    public class ShowSetupMissionDialogMessage : MessageBase { }
    public class HideSetupMissionDialogMessage : MessageBase { }
    public class ScrollAlertDataGridMessage : MessageBase { public AlertEntry newEntry { get; set; } }

    // --- Properties

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
            get { return AttachedDrone.OBCClient.IsConnected; }
        }

        public bool OBCCanBeTested
        {
            get { return AttachedDrone.OBCCanBeTested; }
        }

        public bool MissionCanBeReset
        {
            get { return AttachedDrone.MissionCanBeReset; }
        }

        public bool MissionCanBeModified
        {
            get { return AttachedDrone.MissionCanBeModified; }
        }

        public bool MissionCanToggleActivation
        {
            get { return AttachedDrone.MissionCanToggleActivation; }
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

        private bool _flyThroughMode;
        public bool FlyThroughMode
        {
            get { return _flyThroughMode; }
            set
            {
                if (_flyThroughMode == value)
                    return;
                _flyThroughMode = value;
                RaisePropertyChanged();
            }
        }

        private string _treatmentDuration;
        public string TreatmentDuration
        {
            get { return _treatmentDuration; }
            set
            {
                if (_treatmentDuration == value)
                    return;
                _treatmentDuration = value;
                RaisePropertyChanged();
            }
        }

        public List<string> AvailablePayloads
        {
            get { return AttachedDrone.AvailablePayloads; }
        }

        private int _selectedPayload;
        public int SelectedPayload
        {
            get { return _selectedPayload; }
            set
            {
                if (_selectedPayload == value)
                    return;
                _selectedPayload = value;
                RaisePropertyChanged();
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
            get { return _alerts; }
        }

        private ObservableCollection<AreaScanRoute> _areaScanRoutes;
        public ObservableCollection<AreaScanRoute> AreaScanRoutes
        {
            get { return _areaScanRoutes; }
            set
            {
                if (_areaScanRoutes == value)
                    return;
                _areaScanRoutes = value;
                RaisePropertyChanged("AreaScanRoutes");
            }
        }

        private List<int> _firstAreaScanVertices;
        public List<int> FirstAreaScanVertices
        {
            get { return _firstAreaScanVertices; }
            set
            {
                if (_firstAreaScanVertices == value)
                    return;
                _firstAreaScanVertices = value;
                RaisePropertyChanged("FirstAreaScanVertices");
            }
        }

        private int _selectedAreaScanEntry;
        public int SelectedAreaScanEntry
        {
            get { return _selectedAreaScanEntry; }
            set
            {
                if (_selectedAreaScanEntry == value)
                    return;
                _selectedAreaScanEntry = value;
                AttachedDrone.MissionData.AreaScanRoutes[0].EntryVertex = value;
                RaisePropertyChanged("SelectedAreaScanEntry");
            }
        }

        public MissionViewModel()
        {
            _alerts = new ObservableCollection<AlertEntry>();
            _areaScanRoutes = new ObservableCollection<AreaScanRoute>();
            _selectedAreaScanEntry = 0;
            _firstAreaScanVertices = new List<int>();
        }

        protected override void DroneAttached(bool firstTime)
        {
            LockButtonSymbol = OnboardComputerController.KeyOpen ? Symbol.Accept : Symbol.Permissions;
            LockButtonEnabled = !OnboardComputerController.KeyOpen;

            OnboardComputerController.StaticPropertyChanged += OnboardComputerClient_StaticPropertyChanged;
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged += AlertLog_CollectionChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;

            _alerts = new ObservableCollection<AlertEntry>(AttachedDrone.AlertLog);
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "IsConnected")
                    RaisePropertyChanged("OBCConnected");
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
                    case "MissionCanBeReset":
                        RaisePropertyChanged("MissionCanBeReset");
                        break;
                    case "MissionCanBeModified":
                        RaisePropertyChanged("MissionCanBeModified");
                        break;
                    case "MissionCanToggleActivation":
                        RaisePropertyChanged("MissionCanToggleActivation");
                        break;
                    case "MissionIsActivated":
                        if (AttachedDrone.MissionIsActivated)
                            MissionActivatedText = "Mission_DeactivateButton.Content".GetLocalized();
                        else
                            MissionActivatedText = "Mission_ActivateButton.Content".GetLocalized();
                        break;
                    case "FlyThroughMode":
                        FlyThroughMode = AttachedDrone.FlyThroughMode;
                        break;
                    case "TreatmentDuration":
                        TreatmentDuration = AttachedDrone.TreatmentDuration.ToString();
                        break;
                    case "SelectedPayload":
                        SelectedPayload = AttachedDrone.SelectedPayload;
                        break;
                    case "AvailablePayloads":
                        RaisePropertyChanged("AvailablePayloads");
                        break;
                    case "MissionData":
                        AreaScanRoutes = AttachedDrone.MissionData.AreaScanRoutes;
                        Messenger.Default.Send(new ShowSetupMissionDialogMessage());
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
            OnboardComputerController.StaticPropertyChanged -= OnboardComputerClient_StaticPropertyChanged;
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged -= AlertLog_CollectionChanged;
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
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

        // --- Commands

        public RelayCommand ImportMissionCommand => new RelayCommand(() => importMissionDialog());

        private async void importMissionDialog()
        {
            var picker = new FileOpenPicker();
            picker.FileTypeFilter.Add(".json");
            picker.ViewMode = PickerViewMode.Thumbnail;
            picker.SuggestedStartLocation = PickerLocationId.DocumentsLibrary;

            var result = await picker.PickSingleFileAsync();
            if (result != null)
            {
                AttachedDrone.MissionData = await MissionData.CreateMissionDataFromFile(result);
            }
        }

        public RelayCommand LockButtonCommand => new RelayCommand(() => lockButtonClicked());

        private void lockButtonClicked()
        {
            Messenger.Default.Send(new ShowPassphraseDialogMessage());
        }

        public RelayCommand PassDialogEnterCommand => new RelayCommand(() => {
            PassDialogErrorText = "";
            PassDialogLoading = true;
            passDialogEntered();
        });

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

        public RelayCommand SetupMissionDialogEnterCommand => new RelayCommand(() => setupMissionDialogEntered());

        private void setupMissionDialogEntered()
        {
            AttachedDrone.MissionData.AreaScanRoutes = AreaScanRoutes;
            SelectedAreaScanEntry = 0;
            FirstAreaScanVertices.Clear();
            foreach (int v in AttachedDrone.MissionData.AreaScanRoutes[0].Vertices)
            {
                FirstAreaScanVertices.Add(v);
            }
            AttachedDrone.NewMission = true;
            Messenger.Default.Send(new HideSetupMissionDialogMessage());
        }
    }
}
