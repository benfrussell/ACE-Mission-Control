﻿using System;
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
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml;
using System.Security;
using Microsoft.Toolkit.Uwp.UI.Controls;
using Windows.ApplicationModel.DataTransfer;
using Windows.Devices.Geolocation;

namespace ACE_Mission_Control.ViewModels
{
    // --- Messages

    public class ShowPassphraseDialogMessage : MessageBase { }
    public class HidePassphraseDialogMessage : MessageBase { }
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
                if (isTreatmentDurationValid(_treatmentDuration))
                {
                    TreatmentDurationBorderColour = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemBaseMediumHighColor"]);
                    TreatmentDurationValidText = "";
                }
                else
                {
                    TreatmentDurationBorderColour = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemErrorTextColor"]);
                    TreatmentDurationValidText = "Mission_InvalidInteger".GetLocalized();
                }
                RaisePropertyChanged("TreatmentDuration");
            }
        }

        private SolidColorBrush _treatmentDurationBorderColour;
        public SolidColorBrush TreatmentDurationBorderColour
        {
            get { return _treatmentDurationBorderColour; }
            set
            {
                if (_treatmentDurationBorderColour == value)
                    return;
                _treatmentDurationBorderColour = value;
                RaisePropertyChanged("TreatmentDurationBorderColour");
            }
        }

        private string _treatmentDurationValidText;
        public string TreatmentDurationValidText
        {
            get { return _treatmentDurationValidText; }
            set
            {
                if (_treatmentDurationValidText == value)
                    return;
                _treatmentDurationValidText = value;
                RaisePropertyChanged("TreatmentDurationValidText");
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

        private bool setupMissionDialogOpen;
        private bool suppressPayloadCommand;
        private UGCSClient ugcsclient;

        public MissionViewModel()
        {
            _alerts = new ObservableCollection<AlertEntry>();
            TreatmentDurationBorderColour = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemBaseMediumHighColor"]);

            setupMissionDialogOpen = false;
            suppressPayloadCommand = false;
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
                            MissionActivatedText = "Mission_DeactivateButton".GetLocalized();
                        else
                            MissionActivatedText = "Mission_ActivateButton".GetLocalized();
                        break;
                    case "FlyThroughMode":
                        FlyThroughMode = AttachedDrone.FlyThroughMode;
                        break;
                    case "TreatmentDuration":
                        TreatmentDuration = AttachedDrone.TreatmentDuration.ToString();
                        break;
                    case "SelectedPayload":
                        suppressPayloadCommand = true;
                        SelectedPayload = AttachedDrone.SelectedPayload;
                        break;
                    case "AvailablePayloads":
                        RaisePropertyChanged("AvailablePayloads");
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

        private bool isTreatmentDurationValid(string durationString)
        {
            int parseOut;
            return durationString.Length == 0 || int.TryParse(durationString, out parseOut);
        }

        // --- OBC Commands

        public RelayCommand LockButtonCommand => new RelayCommand(() => lockButtonClicked());
        private void lockButtonClicked()
        {
            Messenger.Default.Send(new ShowPassphraseDialogMessage());
        }

        public RelayCommand ConnectDroneCommand => new RelayCommand(() => connectDroneCommand());
        private void connectDroneCommand()
        {
            AttachedDrone.SendCommand("start_interface");
        }

        public RelayCommand RestartOBCCommand => new RelayCommand(() => restartOBCCommand());
        private void restartOBCCommand()
        {
            AttachedDrone.SendCommand("stop_director");
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

        // --- Mission Commands

        public RelayCommand<ComboBox> PayloadSelectionCommand => new RelayCommand<ComboBox>((box) => payloadSelectionCommand(box));
        private void payloadSelectionCommand(ComboBox box)
        {
            if (!suppressPayloadCommand)
            {
                //AttachedDrone.SendCommand("set_payload -index " + box.SelectedIndex.ToString());
                suppressPayloadCommand = false;
            }       
        }

        public RelayCommand DurationChangedCommand => new RelayCommand(() => durationChangedCommand());
        private void durationChangedCommand()
        {
            if (isTreatmentDurationValid(TreatmentDuration))
                AttachedDrone.SendCommand("set_duration -duration " + TreatmentDuration.ToString());
        }

        public RelayCommand FlyThroughChangedCommand => new RelayCommand(() => flyThroughChangedCommand());
        private void flyThroughChangedCommand()
        {
            if (FlyThroughMode)
                AttachedDrone.SendCommand("set_fly_through -on");
            else
                AttachedDrone.SendCommand("set_fly_through -off");
        }

        public RelayCommand UploadCommand => new RelayCommand(() => uploadCommand());
        private void uploadCommand()
        {
            AttachedDrone.UploadMission();
        }

        public RelayCommand ActivateCommand => new RelayCommand(() => activateCommand());
        private void activateCommand()
        {
            if (AttachedDrone.MissionIsActivated)
                AttachedDrone.SendCommand("deactivate_mission");
            else
                AttachedDrone.SendCommand("activate_mission");
        }

        public RelayCommand ResetCommand => new RelayCommand(() => resetCommand());
        private void resetCommand()
        {
            AttachedDrone.SendCommand("reset_mission");
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

        // --- Dialog Commands

        public RelayCommand PassDialogEnterCommand => new RelayCommand(() => {
            PassDialogErrorText = "";
            PassDialogLoading = true;
            passDialogEntered();
        });

        private void passDialogEntered()
        {
            Messenger.Default.Send(new HidePassphraseDialogMessage());
            OnboardComputerController.StartTryingConnections();

        }

        public RelayCommand<PasswordBox> PassChangedCommand => new RelayCommand<PasswordBox>(pass => passChanged(pass));
        private void passChanged(PasswordBox pass)
        {
            if (pass.Password.Length > 0)
                AttachedDrone.OBCClient.Password.Clear();
            foreach (char c in pass.Password.ToCharArray())
                AttachedDrone.OBCClient.Password.AppendChar(c);
        }
    }
}
