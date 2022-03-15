using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Pbdrone;

namespace ACE_Mission_Control.Core.Models
{
    public class Command
    {
        public enum TriggerType
        {
            Normal,
            Synchronize,
            Update
        }

        public string Input { get; private set; }
        public string Name { get; private set; }
        public string Parameters { get; private set; }
        // Is this command sent automatically
        public bool IsAutoCommand { get; private set; }
        // Is this command sent to sync the director and mission control
        public TriggerType Trigger { get; private set; }
        public object Tag { get; private set; }

        public Command(string input, bool autoCommand = false, TriggerType trigger = TriggerType.Normal, object tag = null)
        {
            Input = input;
            var splitInput = input.Split(' ');
            Name = splitInput[0];
            Parameters = input.Substring(Name.Length);
            IsAutoCommand = autoCommand;
            Trigger = trigger;
            Tag = tag;
        }
    }

    public class Drone : INotifyPropertyChanged, IDrone
    {
        public enum SyncState
        {
            // Failed if any sync or update command failed
            SynchronizeFailed = 0,
            // Initial state
            NotSynchronized = 1,
            // The initial process of retrieving the drone state, sending the updated state, and confirming receipt
            Synchronizing = 2,
            // The state has been synchronized previously and an update is being sent to maintain sync.
            SendingUpdate = 3,
            // Syncing and updating is paused. After leaving this state another synchronization will need to be done.
            Paused = 4,
            // The drone and mission control are synchronized.
            Synchronized = 5
        }

        public static List<string> ChaperoneCommandList = new List<string> { "get_error", "check_director", "start_director", "force_stop_payload" };
        public event PropertyChangedEventHandler PropertyChanged;

        private AlertEntry.AlertType LastAlertType
        {
            get
            {
                if (AlertLog.Count == 0)
                    return AlertEntry.AlertType.None;
                return AlertLog[AlertLog.Count - 1].Type;
            }
        }

        private InterfaceStatus.Types.State _interfaceState;
        public InterfaceStatus.Types.State InterfaceState
        {
            get { return _interfaceState; }
            set
            {
                if (_interfaceState == value)
                    return;
                _interfaceState = value;
                NotifyPropertyChanged();
                UpdateConnectionStage();
            }
        }

        private FlightStatus.Types.State _flightState;
        public FlightStatus.Types.State FlightState
        {
            get { return _flightState; }
            set
            {
                if (_flightState == value)
                    return;
                _flightState = value;
                NotifyPropertyChanged();
            }
        }

        public bool OBCCanBeTested
        {
            get { return !Mission.Locked && OBCClient.IsDirectorConnected; }
        }

        private IMission _mission;
        public IMission Mission
        {
            get { return _mission; }
            set
            {
                if (_mission == value)
                    return;
                _mission = value;
                NotifyPropertyChanged();
            }
        }

        private string _name;
        public string Name
        {
            get { return _name; }
            set
            {
                if (_name == value)
                    return;
                _name = value;
                NotifyPropertyChanged();
            }
        }

        private bool _manualCommandsOnly;
        public bool ManualCommandsOnly
        {
            get { return _manualCommandsOnly; }
            set
            {
                if (_manualCommandsOnly == value)
                    return;
                _manualCommandsOnly = value;
                NotifyPropertyChanged();
            }
        }

        private SyncState _synchronization;
        public SyncState Synchronization
        {
            get { return _synchronization; }
            set
            {
                if (_synchronization == value)
                    return;
                _synchronization = value;
                UpdateCanStartSynchronize();
                NotifyPropertyChanged();
            }
        }

        private bool _canSynchronize;
        public bool CanStartSynchronize
        {
            get { return _canSynchronize; }
            private set
            {
                if (_canSynchronize == value)
                    return;
                _canSynchronize = value;
                NotifyPropertyChanged();
            }
        }

        private void UpdateCanStartSynchronize()
        {
            CanStartSynchronize = OBCClient.IsDirectorConnected && Synchronization != SyncState.Paused && Synchronization != SyncState.Synchronizing;
        }

        private ACEEnums.ConnectionSummary _connectionStage;
        public ACEEnums.ConnectionSummary ConnectionStage
        {
            get { return _connectionStage; }
            private set
            {
                if (_connectionStage == value)
                    return;
                _connectionStage = value;
                NotifyPropertyChanged();
            }
        }

        private bool _isNotConnected;
        public bool IsNotConnected
        {
            get { return _isNotConnected; }
            private set
            {
                if (_isNotConnected == value)
                    return;
                _isNotConnected = value;
                NotifyPropertyChanged();
            }
        }

        private void UpdateConnectionStage()
        {
            if (InterfaceState == InterfaceStatus.Types.State.Online && OBCClient.IsDirectorConnected && OBCClient.IsChaperoneConnected)
                ConnectionStage = ACEEnums.ConnectionSummary.ConnectedACEDrone;
            else if (InterfaceState == InterfaceStatus.Types.State.Online && OBCClient.IsDirectorConnected)
                ConnectionStage = ACEEnums.ConnectionSummary.ConnectedACEDroneLimited;
            else if (InterfaceState == InterfaceStatus.Types.State.Attempting && OBCClient.IsDirectorConnected)
                ConnectionStage = ACEEnums.ConnectionSummary.TryingDroneConnection;
            else if (OBCClient.IsDirectorConnected && OBCClient.IsChaperoneConnected)
                ConnectionStage = ACEEnums.ConnectionSummary.ConnectedACE;
            else if (OBCClient.IsDirectorConnected)
                ConnectionStage = ACEEnums.ConnectionSummary.ConnectedACELimited;
            else if (OBCClient.AutoTryingConnections)
                ConnectionStage = ACEEnums.ConnectionSummary.TryingACEConnection;
            else
                ConnectionStage = ACEEnums.ConnectionSummary.ConnectionDisabled;

            IsNotConnected = (int)ConnectionStage < 2;
        }

        private List<ConfigEntry> _configEntries;
        public List<ConfigEntry> ConfigEntries
        {
            get => _configEntries;
            set
            {
                if (_configEntries == value)
                    return;
                _configEntries = value;
                NotifyPropertyChanged();
            }
        }

        private bool AllSyncCommandsSent
        {
            get => missionStatusReceived && missionConfigReceived && interfaceStatusReceived && missionConfigHandled;
        }

        private bool CanUpdate
        {
            get => OBCClient.IsDirectorConnected && (Synchronization == SyncState.Synchronized || Synchronization == SyncState.SendingUpdate);
        }

        public int ID;

        public IOnboardComputerClient OBCClient;
        public ObservableCollection<AlertEntry> AlertLog;

        private Queue<Command> directorCommandQueue;
        private Queue<Command> chaperoneCommandQueue;
        private Command lastCommandSent;

        private bool configReceived;
        
        private bool missionStatusReceived;
        private bool missionConfigReceived;
        private bool interfaceStatusReceived;
        private bool missionConfigHandled;

        private int updateCommandsSent;
        private int syncCommandsSent;

        public Drone(int id, string name, IOnboardComputerClient onboardComputer, IMission mission)
        {
            directorCommandQueue = new Queue<Command>();
            chaperoneCommandQueue = new Queue<Command>();

            ID = id;
            Name = name;
            AlertLog = new ObservableCollection<AlertEntry>();
            ConfigEntries = new List<ConfigEntry>();
            ManualCommandsOnly = false;

            configReceived = false;

            ResetSyncProgressFlags();

            OBCClient = onboardComputer;
            OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            OBCClient.DirectorMonitorClient.MessageReceivedEvent += DirectorMonitorClient_MessageReceivedEvent;
            OBCClient.DirectorRequestClient.PropertyChanged += DirectorRequestClient_PropertyChanged;
            OBCClient.DirectorRequestClient.ResponseReceivedEvent += DirectorRequestClient_ResponseReceivedEvent;

            Mission = mission;
            Mission.PropertyChanged += Mission_PropertyChanged;
            Mission.InstructionSyncedPropertyUpdated += Mission_InstructionSyncedPropertyUpdated;
            Mission.InstructionAreasUpdated += Mission_InstructionAreasUpdated;
            Mission.ProgressReset += Mission_ProgressReset;

            InterfaceState = InterfaceStatus.Types.State.Offline;
            Synchronization = SyncState.NotSynchronized;

            UpdateConnectionStage();
        }

        public void ToggleLock()
        {
            // If we're connected we should do this through the director. We'll be locked/unlocked on our side by the response.
            if (OBCClient.IsDirectorConnected)
            {
                if (Mission.Locked)
                    SendCommand("unlock_mission");
                else
                    SendCommand("lock_mission");
            }
            else
            {
                if (Mission.Locked)
                    Mission.Unlock();
                else
                    Mission.Lock();
            }
        }

        private void ResetSyncProgressFlags()
        {
            missionStatusReceived = false;
            missionConfigReceived = false;
            interfaceStatusReceived = false;
            missionConfigHandled = false;

            updateCommandsSent = 0;
            syncCommandsSent = 0;
        }

        private void Mission_InstructionAreasUpdated(object sender, InstructionAreasUpdatedArgs e)
        {
            if (!CanUpdate)
                return;

            foreach (ITreatmentInstruction instruction in e.Instructions)
            {
                if (!instruction.Enabled)
                    return;

                if (instruction.CurrentUploadStatus == TreatmentInstruction.UploadStatus.NotUploaded)
                    SendEntireInstruction(instruction, Command.TriggerType.Update);
                else
                    SendInstructionArea(instruction, Command.TriggerType.Update);
            }
        }

        private void Mission_InstructionSyncedPropertyUpdated(object sender, InstructionSyncedPropertyUpdatedArgs e)
        {
            if (!CanUpdate)
                return;

            var instruction = Mission.GetInstructionByID(e.InstructionID);

            if (instruction == null)
                return;

            // Only send updates for disabled commands if the update was the Enabled parameter
            if (!instruction.Enabled && !e.UpdatedParameters.Contains("Enabled"))
                return;

            if (instruction.CurrentUploadStatus == TreatmentInstruction.UploadStatus.NotUploaded)
            {
                SendEntireInstruction(instruction, Command.TriggerType.Update);
            }
            else
            {
                // If the change was that an already uploaded instruction was re-enabled, send the instruction area again
                // This covers us if there were any changes while it was disabled and not syncing
                if (instruction.CurrentUploadStatus == TreatmentInstruction.UploadStatus.Uploaded && instruction.Enabled && e.UpdatedParameters.Contains("Enabled"))
                    SendInstructionArea(instruction, Command.TriggerType.Update);          
                SendInstructionProperties(instruction, Command.TriggerType.Update, e.UpdatedParameters);
            }
                
        }

        private void SendEntireInstruction(ITreatmentInstruction instruction, Command.TriggerType trigger)
        {
            var command = string.Format("set_route -id {0} -order {1} -area_mod_time {2} -area {3} -entry {4} -exit {5} -property_mod_time {6} -status {7} -radians",
                instruction.ID,
                instruction.Order,
                Mission.GetLastAreaModificationTime(instruction.ID),
                instruction.GetTreatmentAreaString(),
                Mission.GetStartCoordinateString(instruction.ID),
                Mission.GetStopCoordinateString(instruction.ID),
                Mission.GetLastPropertyModificationTime(instruction.ID),
                (int)instruction.AreaStatus);

            if (instruction.Enabled)
                command = command + " -enabled";
            else
                command = command + " -disabled";

            if (Mission.GetStartingTurnType(instruction.ID) == Waypoint.TurnType.FlyThrough)
                command = command + " -fly_through";
            else
                command = command + " -stopandgo";

            if (instruction.FirstInstruction && Mission.LastPosition != null && Mission.LastPosition.X != 0 && Mission.LastPosition.Y != 0)
                command = command + $" -lastpos {Mission.LastPosition.Y},{Mission.LastPosition.X}";

            Mission.SetInstructionUploadStatus(instruction.ID, TreatmentInstruction.UploadStatus.Uploading);
            SendCommand(command, true, trigger, instruction.ID);
        }

        private void SendInstructionArea(ITreatmentInstruction instruction, Command.TriggerType trigger)
        {
            var command = string.Format("set_route -id {0} -area_mod_time {1} -area {2} -radians",
                    instruction.ID,
                    instruction.TreatmentPolygon.LastModificationTime,
                    instruction.GetTreatmentAreaString());
            Mission.SetInstructionUploadStatus(instruction.ID, TreatmentInstruction.UploadStatus.Uploading);
            SendCommand(command, true, trigger, instruction.ID);
        }

        private void SendInstructionProperties(ITreatmentInstruction instruction, Command.TriggerType trigger, List<string> properties = null)
        {
            bool allProperties = properties == null;

            var command = $"set_route -id {instruction.ID} -property_mod_time {Mission.GetLastPropertyModificationTime(instruction.ID)}";

            if (allProperties || properties.Contains("Order"))
                command = command + $" -order {instruction.Order}";

            if (allProperties || properties.Contains("AreaEntryExitCoordinates"))
            {
                command = command + string.Format(" -entry {0} -exit {1} -radians",
                    Mission.GetStartCoordinateString(instruction.ID),
                    Mission.GetStopCoordinateString(instruction.ID));
            }

            if (allProperties || properties.Contains("StartingTurnType"))
            {
                if (Mission.GetStartingTurnType(instruction.ID) == Waypoint.TurnType.FlyThrough)
                    command = command + " -fly_through";
                else
                    command = command + " -stopandgo";
            }

            if (allProperties || properties.Contains("Enabled"))
            {
                if (instruction.Enabled)
                    command = command + " -enabled";
                else
                    command = command + " -disabled";
            }

            SendCommand(command, true, trigger, instruction.ID);
        }

        private void Mission_ProgressReset(object sender, EventArgs e)
        {
            if (Mission.MissionSet && OBCClient.IsDirectorConnected && Synchronization == SyncState.Synchronized)
                SendCommand("reset_mission", true);
        }

        private void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Locked")
            {
                if (Mission.Locked)
                    Synchronization = SyncState.Paused;
                else
                    CheckIfSyncShouldUnpause();

                NotifyPropertyChanged("OBCCanBeTested");
            }
                
        }

        private void CheckIfSyncShouldUnpause()
        {
            if (Synchronization != SyncState.Paused)
                return;

            if (!Mission.Locked)
                Synchronization = SyncState.NotSynchronized;

            if (CanStartSynchronize)
                Synchronize();
        }

        private void DirectorRequestClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ReadyForCommand")
            {
                if (OBCClient.DirectorRequestClient.ReadyForCommand)
                {
                    if (OBCClient.IsDirectorConnected)
                        CommandsReadied();
                }
            }
        }

        private void CommandsReadied()
        {
            // Not Synchronized means it should be synchronized first if commands are ready to go
            if (Synchronization != SyncState.NotSynchronized && directorCommandQueue.Count > 0)
                SendCommand(directorCommandQueue.Dequeue());
            else if (Synchronization == SyncState.NotSynchronized)
                Synchronize();
        }

        // Check commands update the state of the Onboard Computer with Mission Control
        public void Synchronize(bool manualSyncronize = false)
        {
            ResetSyncProgressFlags();
            Synchronization = SyncState.Synchronizing;
            // Mission status needs to be checked first because it tells us the most important information (activated, stage)
            // Those details inform whether we can set new areas to finish synchronizing later on (after check_mission_config received)
            SendCommand("check_mission_status", !manualSyncronize, Command.TriggerType.Synchronize);
            SendCommand("check_mission_config", !manualSyncronize, Command.TriggerType.Synchronize);
            SendCommand("check_interface", !manualSyncronize, Command.TriggerType.Synchronize);

            if (!configReceived || manualSyncronize)
                SendCommand("check_ace_config", !manualSyncronize);
        }


        public void SendCommand(string command, bool autoCommand = false, Command.TriggerType trigger = Command.TriggerType.Normal, object tag = null)
        {
            SendCommand(new Command(command, autoCommand, trigger, tag));
        }

        // TODO: Should probably handle this in OnboardComputerClient but keep this as an interface for the ViewModel?
        public void SendCommand(Command command)
        {
            if (command.IsAutoCommand && ManualCommandsOnly)
            {
                AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.CommandError, $": command '{command}' was not sent in manual mode because it was an automatic command."));
                return;
            }

            if (command.Trigger == Command.TriggerType.Update && !CanUpdate)
                return;

            if (command.Trigger == Command.TriggerType.Synchronize && Synchronization == SyncState.SynchronizeFailed)
                return;

            // Check which connection the command should be sent on (Chaperone, if not assume Director)
            // Commands are dumped if the client isn't connected, otherwise if the send fails the command is queued
            if (ChaperoneCommandList.Any(c => c == command.Name))
            {
                if (!OBCClient.IsChaperoneConnected)
                {
                    AddAlert(new AlertEntry(AlertEntry.AlertLevel.High, AlertEntry.AlertType.CommandError, $": command '{command}' could not be sent because the chaperone isn't connected."));
                    return;
                }

                if (!SendCommandWithClient(OBCClient.ChaperoneRequestClient, command))
                    chaperoneCommandQueue.Enqueue(command);
            }
            else
            {
                if (!OBCClient.IsDirectorConnected)
                {
                    AddAlert(new AlertEntry(AlertEntry.AlertLevel.High, AlertEntry.AlertType.CommandError, $": command '{command}' could not be sent because the director isn't connected. Resend the command when connected!"));
                    return;
                }

                if (!SendCommandWithClient(OBCClient.DirectorRequestClient, command))
                {
                    directorCommandQueue.Enqueue(command);
                }
                else
                {
                    if (command.Trigger == Command.TriggerType.Update)
                        updateCommandsSent++;
                    else if (command.Trigger == Command.TriggerType.Synchronize)
                        syncCommandsSent++;
                }

            }

        }

        // Returns success or failure
        private bool SendCommandWithClient(IRequestClient client, Command command)
        {
            if (!client.ReadyForCommand)
                return false;

            bool sendSuccessful = client.SendCommand(command.Input);

            if (sendSuccessful)
            {
                lastCommandSent = command;
                return true;
            }

            return false;
        }

        private void DirectorMonitorClient_MessageReceivedEvent(object sender, MessageReceivedEventArgs e)
        {
            switch (e.MessageType)
            {
                case ACEEnums.MessageType.InterfaceStatus:
                    var interfaceStatus = (InterfaceStatus)e.Message;
                    interfaceStatusReceived = true;
                    InterfaceState = interfaceStatus.InterfaceState;
                    CheckIfSyncComplete();
                    break;
                case ACEEnums.MessageType.FlightStatus:
                    var flightStatus = (FlightStatus)e.Message;
                    FlightState = flightStatus.FlightState;
                    break;
                case ACEEnums.MessageType.MissionStatus:
                    var missionStatus = (MissionStatus)e.Message;
                    missionStatusReceived = true;
                    HandleDroneMissionStatus(missionStatus);
                    break;
                case ACEEnums.MessageType.MissionConfig:
                    var missionConfig = (MissionConfig)e.Message;
                    missionConfigReceived = true;
                    HandleDroneMissionConfig(missionConfig);
                    break;
                case ACEEnums.MessageType.Configuration:
                    var configuration = (Configuration)e.Message;
                    ConfigEntries = new List<ConfigEntry>(configuration.List);
                    configReceived = true;
                    break;
                case ACEEnums.MessageType.CommandResponse:
                    var commandResponse = (CommandResponse)e.Message;
                    if (commandResponse.Command == "ping")
                        break;
                    var responseLevel = commandResponse.Successful ? AlertEntry.AlertLevel.Info : AlertEntry.AlertLevel.Medium;
                    var alertType = commandResponse.Successful ? AlertEntry.AlertType.CommandResponse : AlertEntry.AlertType.CommandError;
                    string alertInfo = "'" + commandResponse.Command + "': " + commandResponse.Response;
                    var alert = new AlertEntry(responseLevel, alertType, alertInfo);
                    AddAlert(alert);
                    break;
                default:
                    break;
            }
        }

        private void HandleDroneMissionStatus(MissionStatus newStatus)
        {
            bool justReturned =
                Mission.Stage != MissionStatus.Types.Stage.Returning &&
                Mission.Stage != MissionStatus.Types.Stage.Override &&
                (newStatus.MissionStage == MissionStatus.Types.Stage.Returning ||
                newStatus.MissionStage == MissionStatus.Types.Stage.Override);

            bool firstPositionUpdate = Mission.LastPosition == null && newStatus.LastLongitude != 0 && newStatus.LastLatitude != 0;

            Mission.SetStage(newStatus.MissionStage);

            if (newStatus.Locked)
                Mission.Lock();
            else
                Mission.Unlock();

            if (newStatus.TreatmentTime > Mission.TreatmentTimeElapsed && (!justReturned || firstPositionUpdate))
                AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ExecutionTimeUpdated, newStatus.TreatmentTime.ToString() + "s"));
            else if (newStatus.TreatmentTime > 0 && justReturned)
                AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.FinishedExecution, newStatus.TreatmentTime.ToString() + "s"));

            Mission.TreatmentTimeElapsed = newStatus.TreatmentTime;

            if (justReturned || firstPositionUpdate)
                Mission.Returned(newStatus.LastLongitude, newStatus.LastLatitude);

            CheckIfSyncComplete();
        }

        private void HandleDroneMissionConfig(MissionConfig missionConfig)
        {
            if (Synchronization == SyncState.Paused)
            {
                missionConfigHandled = true;
                return;
            }

            Mission.TreatmentDuration = missionConfig.TreatmentDuration;

            UpdateMissionWithDroneRoutes(missionConfig.Routes);
            SendMissionStateDifferences(missionConfig.Routes);
            SendNotUploadedRoutes();

            missionConfigHandled = true;

            // If no sync commands were sent after handling the mission config, then the sync could be finished here
            CheckIfSyncComplete();
        }
                
        private void UpdateMissionWithDroneRoutes(IEnumerable<MissionRoute> routes)
        {
            foreach (MissionRoute route in routes)
            {
                if ((int)route.Status > (int)Mission.GetAreaStatus(route.ID))
                    Mission.SetInstructionAreaStatus(route.ID, route.Status);                      
            }

            Mission.SetUploadedInstructions(routes.Select(r => r.ID));
            Mission.ReorderInstructionsByID(routes.OrderBy(r => r.Order).Select(r => r.ID).ToList());
        }

        private void SendMissionStateDifferences(IEnumerable<MissionRoute> routes)
        {
            foreach (MissionRoute route in routes)
            {
                ITreatmentInstruction instruction = Mission.GetInstructionByID(route.ID);

                if (instruction == null)
                    continue;

                bool propertiesOutdated = Mission.GetLastPropertyModificationTime(route.ID) > route.LastPropertyModification;
                bool areaOutdated = Mission.GetLastAreaModificationTime(route.ID) > route.LastAreaModification;

                if (propertiesOutdated && areaOutdated && instruction.CurrentUploadStatus != TreatmentInstruction.UploadStatus.Uploading)
                    SendEntireInstruction(instruction, Command.TriggerType.Synchronize);
                else if (propertiesOutdated)
                    SendInstructionProperties(instruction, Command.TriggerType.Synchronize);
                else if (areaOutdated && instruction.CurrentUploadStatus != TreatmentInstruction.UploadStatus.Uploading)
                    SendInstructionArea(instruction, Command.TriggerType.Synchronize);
            }
        }

        private void SendNotUploadedRoutes()
        {
            foreach (ITreatmentInstruction instruction in Mission.GetRemainingInstructions())
            {
                if (instruction.CurrentUploadStatus == TreatmentInstruction.UploadStatus.NotUploaded)
                    SendEntireInstruction(instruction, Command.TriggerType.Synchronize);
            }
        }

        private void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDirectorConnected")
            {
                NotifyPropertyChanged("OBCCanBeTested");

                if (!OBCClient.IsDirectorConnected)
                {
                    if (Synchronization != SyncState.Paused)
                        Synchronization = SyncState.NotSynchronized;
                    if (Mission.Locked)
                        Mission.Unlock();
                    directorCommandQueue.Clear();
                    updateCommandsSent = 0;
                    InterfaceState = InterfaceStatus.Types.State.Offline;
                }
                else
                {
                    // Reconnecting should always puts the paused state to not synchronized.
                    // Paused depends on the mission stage which may have changed. We need to resync to get the current stage.
                    if (Synchronization == SyncState.Paused)
                        Synchronization = SyncState.NotSynchronized;
                    
                    if (OBCClient.DirectorRequestClient.ReadyForCommand)
                        CommandsReadied();
                }

                UpdateCanStartSynchronize();
                UpdateConnectionStage();
            }
            else if (e.PropertyName == "AutoTryingConnections")
            {
                UpdateConnectionStage();
            }
            else if (e.PropertyName == "IsChaperoneConnected")
            {
                UpdateConnectionStage();
            }
        }

        private void DirectorRequestClient_ResponseReceivedEvent(object sender, ResponseReceivedEventArgs e)
        {
            // Ping commands aren't sent by the drone class, so last command sent will be null for these
            if (lastCommandSent == null)
                return;

            switch (lastCommandSent.Trigger)
            {
                case Command.TriggerType.Synchronize:
                    syncCommandsSent--;
                    if (e.Line.Contains("(FAILURE)"))
                        HandleUpdateOrSyncFailure();
                    else if (e.Line.Contains("(SUCCESS)"))
                        CheckIfSyncComplete();

                    break;
                case Command.TriggerType.Update:
                    updateCommandsSent--;
                    if (e.Line.Contains("(FAILURE)"))
                        HandleUpdateOrSyncFailure();
                    else if (e.Line.Contains("(SUCCESS)"))
                        CheckIfUpdateComplete();

                    break;
                default:
                    break;
            }

            if (lastCommandSent.Name == "set_mission" || lastCommandSent.Name == "add_area" ||
               (lastCommandSent.Name == "set_route" && lastCommandSent.Parameters.Contains("area")))
            {
                if (e.Line.Contains("(SUCCESS)"))
                    Mission.SetInstructionUploadStatus((int)lastCommandSent.Tag, TreatmentInstruction.UploadStatus.Uploaded);
            }
            else if (lastCommandSent.Name == "set_config_entry")
            {
                if (e.Line.Contains("(SUCCESS)"))
                {
                    var updated_entry = (ConfigEntry)lastCommandSent.Tag;
                    var entry_index = ConfigEntries.IndexOf(ConfigEntries.FirstOrDefault(c => c.Id == updated_entry.Id));

                    ConfigEntries[entry_index].Value = updated_entry.Value;
                    NotifyPropertyChanged("ConfigEntries");
                }
            }

            lastCommandSent = null;
        }

        private void CheckIfSyncComplete()
        {
            // Something isn't right if we have 0 sync commands sent. Restart the sync process
            if (syncCommandsSent < 0)
            {
                if (CanStartSynchronize)
                    Synchronize();
            }
            else
            {
                // If no more sync commands are sent out and none are coming down the pipe, we're finished synchronizing
                if (syncCommandsSent == 0 && Synchronization == SyncState.Synchronizing && AllSyncCommandsSent)
                    Synchronization = SyncState.Synchronized;
            }
        }

        private void CheckIfUpdateComplete()
        {
            if (updateCommandsSent <= 0)
            {
                if (Synchronization == SyncState.SendingUpdate)
                    Synchronization = SyncState.Synchronized;
            }
        }

        private void HandleUpdateOrSyncFailure()
        {
            // If a sync command fails then clear the Queue of all commands
            directorCommandQueue.Clear();
            ResetSyncProgressFlags();
            Synchronization = SyncState.SynchronizeFailed;
        }

        public void AddAlert(AlertEntry entry, bool blockDuplicates = false)
        {
            if (blockDuplicates && entry.Type == LastAlertType)
                return;
            AlertLog.Add(entry);
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
