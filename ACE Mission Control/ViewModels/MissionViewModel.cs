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
using System.Globalization;
using Windows.UI.Notifications;
using Windows.UI.Xaml.Controls.Maps;
using Windows.Devices.Geolocation;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Linq;
using Windows.Storage.Streams;
using Windows.UI;

namespace ACE_Mission_Control.ViewModels
{
    public class RemakeMapMessage : MessageBase { }

    public class SetMapPointsMessage : MessageBase
    {
        public IEnumerable<ITreatmentInstruction> Instructions;
        public SetMapPointsMessage(IEnumerable<ITreatmentInstruction> instructions) { Instructions = instructions; }
        public SetMapPointsMessage(ITreatmentInstruction instruction) { Instructions = new List<ITreatmentInstruction> { instruction }; }
    }

    public class SetMapPolygonsMessage : MessageBase
    {
        public IEnumerable<ITreatmentInstruction> Instructions;
        public SetMapPolygonsMessage(IEnumerable<ITreatmentInstruction> instructions) { Instructions = instructions; }
        public SetMapPolygonsMessage(ITreatmentInstruction instruction) { Instructions = new List<ITreatmentInstruction> { instruction }; }
    }

    public class MissionViewModel : DroneViewModelBase
    {
        public enum ConnectStatus
        {
            NotConnected,
            Attempting,
            Connected
        }
        // --- Connection properties

        private ConnectStatus _chaperoneStatus;
        public ConnectStatus ChaperoneStatus
        {
            get { return _chaperoneStatus; }
            set
            {
                if (_chaperoneStatus == value)
                    return;
                _chaperoneStatus = value;
                RaisePropertyChanged();
            }
        }

        private ConnectStatus _directorStatus;
        public ConnectStatus DirectorStatus
        {
            get { return _directorStatus; }
            set
            {
                if (_directorStatus == value)
                    return;
                _directorStatus = value;
                RaisePropertyChanged();
            }
        }

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

        private bool _droneConnectionOn;
        public bool DroneConnectEnabled
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

        private string _droneConnectText;
        public string DroneConnectText
        {
            get { return _droneConnectText; }
            set
            {
                if (_droneConnectText == value)
                    return;
                _droneConnectText = value;
                RaisePropertyChanged();
            }
        }

        // --- Planner properties

        private string _plannerStatus;
        public string PlannerStatus
        {
            get { return _plannerStatus; }
            set
            {
                if (_plannerStatus == value)
                    return;
                _plannerStatus = value;
                RaisePropertyChanged();
            }
        }

        private SolidColorBrush _plannerStatusColour;
        public SolidColorBrush PlannerStatusColour
        {
            get { return _plannerStatusColour; }
            set
            {
                if (_plannerStatusColour == value)
                    return;
                _plannerStatusColour = value;
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
                    TreatmentDurationError = false;
                else
                    TreatmentDurationError = true;
                RaisePropertyChanged("TreatmentDuration");
            }
        }

        private bool _treatmentDurationError;
        public bool TreatmentDurationError
        {
            get { return _treatmentDurationError; }
            set
            {
                if (_treatmentDurationError == value)
                    return;
                _treatmentDurationError = value;
                RaisePropertyChanged();
            }
        }

        private int _selectedStartMode;
        public int SelectedStartMode
        {
            get { return _selectedStartMode; }
            set
            {
                if (_selectedStartMode == value)
                    return;
                _selectedStartMode = value;
                RaisePropertyChanged();
            }
        }

        private bool startModeError;
        public bool StartModeError
        {
            get { return startModeError; }
            set
            {
                if (startModeError == value)
                    return;
                startModeError = value;
                RaisePropertyChanged();
            }
        }

        private string _lockButtonText;
        public string LockButtonText
        {
            get { return _lockButtonText; }
            set
            {
                if (_lockButtonText == value)
                    return;
                _lockButtonText = value;
                RaisePropertyChanged();
            }
        }

        private bool startModeErrorNotificationSent;

        public MissionViewModel()
        {
            IPLookup.StaticPropertyChanged += IPLookup_StaticPropertyChanged;
            EntriesFound = IPLookup.EntriesFound;
            Searching = IPLookup.Searching;
            Progress = IPLookup.Progress;
            TreatmentDurationError = false;
            StartModeError = false;
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

        protected override void DroneAttached(bool firstTime)
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;

            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged += Mission_PropertyChanged;

            AttachedDrone.Mission.InstructionAreasUpdated += Mission_InstructionAreasUpdated;
            AttachedDrone.Mission.InstructionRouteUpdated += Mission_InstructionRouteUpdated;
            AttachedDrone.Mission.InstructionSyncedPropertyUpdated += Mission_InstructionSyncedPropertyUpdated;

            Messenger.Default.Send(new SetMapPolygonsMessage(AttachedDrone.Mission.TreatmentInstructions));
            Messenger.Default.Send(new SetMapPointsMessage(AttachedDrone.Mission.TreatmentInstructions));

            startModeErrorNotificationSent = false;
            CheckStartModeError();

            UpdateConnectionStatuses();
            UpdatePlannerStatus();
            UpdateLockButton();
            UpdateDroneConnectButton();

            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            if (settings.Values.Keys.Contains(AttachedDrone.Name + "_IP"))
            {
                Hostname_Text = (string)settings.Values[AttachedDrone.Name + "_IP"];
                applyButtonClicked();
            }

            SelectedStartMode = (int)AttachedDrone.Mission.StartMode;
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "IsDirectorConnected" || e.PropertyName == "IsChaperoneConnected" || e.PropertyName == "ConnectionInProgress")
                    UpdateConnectionStatuses();
            });
        }

        private async void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                RaisePropertyChanged(e.PropertyName);

                switch (e.PropertyName)
                {
                    case "Locked":
                        UpdateLockButton();
                        break;
                    case "TreatmentDuration":
                        TreatmentDuration = AttachedDrone.Mission.TreatmentDuration.ToString();
                        break;
                    case "StartMode":
                        SelectedStartMode = (int)AttachedDrone.Mission.StartMode;
                        CheckStartModeError();
                        break;
                    case "MissionSet":
                        UpdatePlannerStatus();
                        break;
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
                        UpdateDroneConnectButton();
                        break;
                    case "Synchronization":
                        UpdateDroneConnectButton();
                        break;
                }
            });
        }

        private void Mission_InstructionRouteUpdated(object sender, InstructionRouteUpdatedArgs e)
        {
            Messenger.Default.Send(new SetMapPolygonsMessage(e.Instruction));
            Messenger.Default.Send(new SetMapPointsMessage(e.Instruction));
            if (e.Instruction.FirstInstruction)
                CheckStartModeError();
        }

        private async void Mission_InstructionSyncedPropertyUpdated(object sender, InstructionSyncedPropertyUpdatedArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var instruction = AttachedDrone.Mission.GetInstructionByID(e.InstructionID);
                if (instruction == null)
                    return;

                if (e.UpdatedParameters.Contains("AreaEntryExitCoordinates"))
                {
                    Messenger.Default.Send(new SetMapPointsMessage(instruction));
                    CheckStartModeError();
                }
                else if (e.UpdatedParameters.Contains("Enabled"))
                {
                    Messenger.Default.Send(new SetMapPolygonsMessage(instruction));
                    Messenger.Default.Send(new SetMapPointsMessage(instruction));
                    // If there's a new first instruction after this change, which WASN'T this instruction, we need to draw it's route on the map now
                    if (!instruction.FirstInstruction)
                    {
                        var nextInstruction = AttachedDrone.Mission.GetNextInstruction();
                        if (nextInstruction == null)
                            return;

                        Messenger.Default.Send(new SetMapPolygonsMessage(nextInstruction));
                        Messenger.Default.Send(new SetMapPointsMessage(nextInstruction));
                    }

                    // Same deal for the last instruction, we draw it's points differently
                    if (!instruction.LastInstruction)
                    {
                        var lastInstruction = AttachedDrone.Mission.GetLastInstruction();
                        if (lastInstruction != null)
                            Messenger.Default.Send(new SetMapPointsMessage(lastInstruction));
                    }
                }
            });
        }

        private void Mission_InstructionAreasUpdated(object sender, InstructionAreasUpdatedArgs e)
        {
            Messenger.Default.Send(new SetMapPolygonsMessage(e.Instructions));
            Messenger.Default.Send(new SetMapPointsMessage(e.Instructions));
            CheckStartModeError();
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged -= Mission_PropertyChanged;
            AttachedDrone.Mission.InstructionAreasUpdated -= Mission_InstructionAreasUpdated;
            AttachedDrone.Mission.InstructionRouteUpdated -= Mission_InstructionRouteUpdated;
            AttachedDrone.Mission.InstructionSyncedPropertyUpdated -= Mission_InstructionSyncedPropertyUpdated;
        }

        private void UpdateLockButton()
        {
            if (AttachedDrone.Mission.Locked)
                LockButtonText = "Planner_UnlockButton".GetLocalized();
            else
                LockButtonText = "Planner_LockButton".GetLocalized();
        }

        private void UpdateDroneConnectButton()
        {
            if (AttachedDrone.Synchronization != Drone.SyncState.Synchronized)
                DroneConnectEnabled = false;
            else
                DroneConnectEnabled = AttachedDrone.InterfaceState != Pbdrone.InterfaceStatus.Types.State.Attempting;

            if (AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online)
                DroneConnectText = "Mission_DisconnectDrone".GetLocalized();
            else
                DroneConnectText = "Mission_ConnectDrone".GetLocalized();
        }

        private void UpdateConnectionStatuses()
        {
            if (AttachedDrone.OBCClient.IsDirectorConnected)
                DirectorStatus = ConnectStatus.Connected;
            else if (AttachedDrone.OBCClient.ConnectionInProgress)
                DirectorStatus = ConnectStatus.Attempting;
            else
                DirectorStatus = ConnectStatus.NotConnected;

            if (AttachedDrone.OBCClient.IsChaperoneConnected)
                ChaperoneStatus = ConnectStatus.Connected;
            else if (AttachedDrone.OBCClient.ConnectionInProgress)
                ChaperoneStatus = ConnectStatus.Attempting;
            else
                ChaperoneStatus = ConnectStatus.NotConnected;
        }

        private void UpdatePlannerStatus()
        {
            if (AttachedDrone.Mission.MissionSet)
            {
                PlannerStatus = "Mission_PlannerMissionSet".GetLocalized();
                PlannerStatusColour = new SolidColorBrush(Colors.ForestGreen);
            }
            else
            {
                PlannerStatus = "Mission_PlannerMissionNotSet".GetLocalized();
                PlannerStatusColour = new SolidColorBrush(Colors.Yellow);
            }
        }

        private void CheckStartModeError()
        {
            var startPosition = AttachedDrone.Mission.GetStartCoordinate();

            if (AttachedDrone.Mission.StartMode == StartTreatmentParameters.Mode.Flythrough && startPosition != null && !double.IsNaN(startPosition.X) && !double.IsNaN(startPosition.Y))
            {
                var nextInstruction = AttachedDrone.Mission.GetNextInstruction();

                // 7.5 metres is the hardcoded buffer for triggering entry in the drone 
                if (nextInstruction != null && nextInstruction.HasValidTreatmentRoute() &&
                    !nextInstruction.TreatmentRoute.DoesRoutePassCoordinate(startPosition, 7.5f))
                {
                    StartModeError = true;

                    if (!startModeErrorNotificationSent & !Window.Current.Visible)
                    {
                        // Construct the content
                        var content = new ToastContentBuilder()
                            .AddText("Planner_FlythroughErrorNotification_Title".GetLocalized())
                            .AddText(string.Format("Planner_FlythroughErrorNotification_Content".GetLocalized(), AttachedDrone.Name))
                            .GetToastContent();

                        // Create the notification
                        var notif = new ToastNotification(content.GetXml());

                        // And show it!
                        ToastNotificationManager.CreateToastNotifier().Show(notif);
                        startModeErrorNotificationSent = true;
                    }

                    return;
                }
            }

            StartModeError = false;
            startModeErrorNotificationSent = false;
        }

        // --- Connection commands

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

        public RelayCommand ConnectDroneCommand => new RelayCommand(() => connectDroneCommand());
        private void connectDroneCommand()
        {
            if (AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online)
            {
                AttachedDrone.SendCommand("stop_interface");                    
            }
            else
            {
                AttachedDrone.SendCommand("start_interface");
            }
        }

        public RelayCommand ApplyChangesCommand => new RelayCommand(() => applyButtonClicked());
        private void applyButtonClicked()
        {
            AttachedDrone.OBCClient.Configure(Hostname_Text);
            var settings = Windows.Storage.ApplicationData.Current.LocalSettings;
            settings.Values[AttachedDrone.Name + "_IP"] = Hostname_Text;
            UnsavedChanges = false;
        }

        public RelayCommand ResetChangesCommand => new RelayCommand(() => resetButtonClicked());
        private void resetButtonClicked()
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
        }

        public RelayCommand StartSearch => new RelayCommand(() => IPLookup.LookupIPs("gdg-pi"));

        public RelayCommand<ListView> SearchResultClickedCommand => new RelayCommand<ListView>((v) => searchResultClicked(v));
        private void searchResultClicked(ListView list)
        {
            if (list.SelectedItem == null)
                return;
            var item = list.SelectedItem as Tuple<string, string>;
            Hostname_Text = item.Item2;
            applyButtonClicked();
        }

        // --- Planner commands

        public RelayCommand<TreatmentInstruction> ReorderInstructionUpCommand => new RelayCommand<TreatmentInstruction>((args) => reorderInstructionCommand(args, -1));
        public RelayCommand<TreatmentInstruction> ReorderInstructionDownCommand => new RelayCommand<TreatmentInstruction>((args) => reorderInstructionCommand(args, 1));
        private void reorderInstructionCommand(TreatmentInstruction instruction, int change)
        {
            AttachedDrone.Mission.ReorderInstruction(instruction, AttachedDrone.Mission.TreatmentInstructions.IndexOf(instruction) + change);
        }

        public RelayCommand<TreatmentInstruction> WaypointRouteChangedCommand => new RelayCommand<TreatmentInstruction>((args) => waypointRouteChangedCommand(args));
        private void waypointRouteChangedCommand(TreatmentInstruction args)
        {
            if (args == null)
                return;

            // When moving items in the ObservableCollection of TreatmentInstructions that is bound to the ListView that holds these Comboboxes....
            // ... it seems UWP internally adds/readds the TreatmentInstruction item
            // When doing this readding, it sets the ComboBox's SelectedItem to null for some reason (the SelectionChanged event shows that the selected item gets removed by an internal trigger)
            // So we need to ask the TreatmentInstruction to resend a NotifyPropertyChanged for it's TreatmentRoute property, thus setting the SelectedItem back
            // This is all very stupid!
            if (!args.Renotifying)
                args.RenotifyTreatmentRoute();
        }

        public RelayCommand DurationChangedCommand => new RelayCommand(() => durationChangedCommand());
        private void durationChangedCommand()
        {
            if (isTreatmentDurationValid(TreatmentDuration))
                AttachedDrone.SendCommand("set_duration -duration " + TreatmentDuration.ToString());
        }

        private bool isTreatmentDurationValid(string durationString)
        {
            int parseOut;
            return durationString.Length == 0 || int.TryParse(durationString, NumberStyles.Integer, CultureInfo.InvariantCulture, out parseOut);
        }

        public RelayCommand<ComboBoxItem> StartModeSelectionCommand => new RelayCommand<ComboBoxItem>((args) => startModeSelectionCommand(args));
        private void startModeSelectionCommand(ComboBoxItem item)
        {
            var selectedMode = (StartTreatmentParameters.Mode)item.Tag;
            AttachedDrone.Mission.StartMode = selectedMode;
        }

        public RelayCommand ResetCommand => new RelayCommand(() => resetCommand());
        private void resetCommand()
        {
            AttachedDrone.Mission.ResetProgress();
        }

        public RelayCommand LockCommand => new RelayCommand(() => lockCommand());
        private void lockCommand()
        {
            AttachedDrone.ToggleLock();
        }

        public RelayCommand RemakeMapCommand => new RelayCommand(() => remakeMapCommand());
        private void remakeMapCommand()
        {
            Messenger.Default.Send(new RemakeMapMessage());
        }

        // --- Control commands

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
