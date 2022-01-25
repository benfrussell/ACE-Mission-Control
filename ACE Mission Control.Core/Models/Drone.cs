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
        public string Input { get; private set; }
        public string Name { get; private set; }
        public string Parameters { get; private set; }
        // Is this command sent automatically
        public bool IsAutoCommand { get; private set; }
        // Is this command sent to sync the director and mission control
        public bool IsSyncCommand { get; private set; }
        public object Tag { get; private set; }

        public Command(string input, bool autoCommand = false, bool syncCommand = false, object tag = null)
        {
            Input = input;
            var splitInput = input.Split(' ');
            Name = splitInput[0];
            Parameters = input.Substring(Name.Length);
            IsAutoCommand = autoCommand;
            IsSyncCommand = syncCommand;
            Tag = tag;
        }
    }

    public class Drone : INotifyPropertyChanged, IDrone
    {
        public enum SyncState
        {
            SynchronizeFailed = 0,
            NotSynchronized = 1,
            Synchronizing = 2,
            Paused = 3,
            Synchronized = 4
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
            get { return !Mission.Activated && OBCClient.IsDirectorConnected; }
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
                UpdateCanSynchronize();
                UpdateMissionLockState();
                NotifyPropertyChanged();
            }
        }

        private bool _awayOnMission;
        public bool AwayOnMission
        {
            get => _awayOnMission;
            private set
            {
                if (_awayOnMission == value)
                    return;
                _awayOnMission = value;
                NotifyPropertyChanged();
            }
        }

        private void UpdateAwayOnMission()
        {
            AwayOnMission =
                Mission.Stage == MissionStatus.Types.Stage.Enroute ||
                Mission.Stage == MissionStatus.Types.Stage.Executing ||
                (Mission.Stage == MissionStatus.Types.Stage.Ready && Mission.MissionSet && FlightState == FlightStatus.Types.State.InAir && OBCClient.AutoTryingConnections);
        }

        private bool _canSynchronize;
        public bool CanSynchronize
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

        private void UpdateCanSynchronize()
        {
            CanSynchronize = OBCClient.IsDirectorConnected && OBCClient.DirectorRequestClient.ReadyForCommand && Synchronization != SyncState.Paused && Synchronization != SyncState.Synchronizing;  
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

        public int ID;

        public IOnboardComputerClient OBCClient;
        public ObservableCollection<AlertEntry> AlertLog;

        private Queue<Command> directorCommandQueue;
        private Queue<Command> chaperoneCommandQueue;
        private Command lastCommandSent;

        private bool configReceived;
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

            syncCommandsSent = 0;
            configReceived = false;

            OBCClient = onboardComputer;
            OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            OBCClient.DirectorMonitorClient.MessageReceivedEvent += DirectorMonitorClient_MessageReceivedEvent;
            OBCClient.DirectorRequestClient.PropertyChanged += DirectorRequestClient_PropertyChanged;
            OBCClient.DirectorRequestClient.ResponseReceivedEvent += DirectorRequestClient_ResponseReceivedEvent;

            Mission = mission;
            Mission.PropertyChanged += Mission_PropertyChanged;
            Mission.StartStopPointsUpdated += StartParameters_StartParametersChangedEvent;
            Mission.InstructionRouteUpdated += Mission_InstructionRouteUpdated;
            Mission.ProgressReset += Mission_ProgressReset;

            InterfaceState = InterfaceStatus.Types.State.Offline;
            Synchronization = SyncState.NotSynchronized;
        }

        private void Mission_ProgressReset(object sender, EventArgs e)
        {
            if (Mission.MissionSet && OBCClient.IsDirectorConnected && Synchronization == SyncState.Synchronized)
                SendCommand("reset_mission", true);
        }

        private void StartParameters_StartParametersChangedEvent(object sender, EventArgs e)
        {
            if (Synchronization == SyncState.Synchronized && Mission.MissionSet)
            {
                // Send start mode commands right away if synchronized
                // If not synchronized, they will be sent during synchronization
                SendStartModeCommands(false);
            }
        }

        private void Mission_InstructionRouteUpdated(object sender, InstructionRouteUpdatedArgs e)
        {
            // If the mission isn't set yet, all the details will be sent when the areas are uploaded
            if (!Mission.MissionSet)
                return;

            // We only care about enabled instructions
            if (!e.Instruction.Enabled)
                return;

            // Send start mode commands right away if synchronized
            // If not synchronized, they will be sent during synchronization
            if (Synchronization == SyncState.Synchronized)
            {
                SendNewInstructionEntryCommand(e.Instruction);
            }
            else
            {
                unsentRouteChanges.RemoveAll(i => i.ID == e.Instruction.ID);
                unsentRouteChanges.Add(e.Instruction);
            }
        }

        private void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // If we discover the mission is already set while synchronizing, send the start mode commands so the drone knows where we want to start
            if (e.PropertyName == "MissionSet" && Mission.MissionSet && Synchronization == SyncState.Synchronizing)
                SendStartModeCommands(false);

            if (e.PropertyName == "Stage" || e.PropertyName == "MissionSet")
            {
                UpdateAwayOnMission();
                // When determined to be away on mission, pause sync so we don't try to send updates while it's working
                if (AwayOnMission)
                {
                    Synchronization = SyncState.Paused;
                }   
                else if (Synchronization == SyncState.Paused)
                {
                    // If we're no longer away on mission but syncing is still paused, try to resync
                    // This could happen if the OBC was connected for the entire flight (normally the reconnect triggers the unpause)
                    Synchronization = SyncState.NotSynchronized;
                    if (CanSynchronize)
                        Synchronize();
                }
            }
                
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
                UpdateCanSynchronize();
            }
        }

        private void CommandsReadied()
        {
            // Not Synchronized means it should be synchronized first if commands are ready to go
            if (Synchronization != SyncState.NotSynchronized && directorCommandQueue.Count > 0)
            {
                SendCommand(directorCommandQueue.Dequeue());
            }
            else if (Synchronization == SyncState.NotSynchronized)
            {
                syncCommandsSent = 0;
                Synchronize();
            }
        }

        private void SendStartModeCommands(bool manuallySent = false)
        {
            if (!Mission.MissionSet)
                AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.None, "Tried to send the entry point but no mission is set!"));

            // Only send these commands if the mission is set and syncing is not paused
            if (!Mission.MissionSet || Synchronization == SyncState.Paused)
                return;

            var command = $"set_entry -entry {Mission.GetStartCoordinateString()} -radians";

            if (!Mission.StopAndTurnStartMode)
                command += " -fly_through";

            SendCommand(command, !manuallySent, true);
        }

        private void SendNewInstructionEntryCommand(ITreatmentInstruction instruction, bool manuallySent = false)
        {
            // Only send these commands if the mission is set and syncing is not paused
            if (!Mission.MissionSet || Synchronization == SyncState.Paused)
                return;

            var command = $"set_entry -id {instruction.ID} -entry {instruction.GetEntryCoordianteString()} -exit {instruction.GetExitCoordinateString()} -radians";

            if (!instruction.FirstInstruction || !Mission.StopAndTurnStartMode)
                command += " -fly_through";

            SendCommand(command, !manuallySent, true);
        }

        // Check commands update the state of the Onboard Computer with Mission Control
        // They're sent everytime a connection to the director is re-established
        public void Synchronize(bool manualSyncronize = false)
        {
            // Mission status needs to be checked first because it tells us the most important information (activated, stage)
            // Those details inform whether we can set new areas to finish synchronizing later on (after check_mission_config received)
            SendCommand("check_mission_status", !manualSyncronize, true);
            SendCommand("check_mission_config", !manualSyncronize, true);
            SendCommand("check_interface", !manualSyncronize, true);

            if (!configReceived || manualSyncronize)
            {
                SendCommand("check_ace_config", !manualSyncronize, true);
            }
        }

        public void SendCommand(string command, bool autoCommand = false, bool syncCommand = false, object tag = null)
        {
            SendCommand(new Command(command, autoCommand, syncCommand, tag));
        }

        // TODO: Should probably handle this in OnboardComputerClient but keep this as an interface for the ViewModel?
        public void SendCommand(Command command)
        {
            if (command.IsAutoCommand && ManualCommandsOnly)
            {
                AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.CommandError, $": command '{command}' was not sent in manual mode because it was an automatic command."));
                return;
            }

            // Don't allow any more sync commands if the sync failed - another sync attempt has to be triggered first
            if (command.IsSyncCommand && Synchronization == SyncState.SynchronizeFailed)
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
                    if (command.IsSyncCommand)
                    {
                        if (Synchronization != SyncState.Synchronizing)
                            Synchronization = SyncState.Synchronizing;
                        syncCommandsSent++;
                    }
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

        public void UploadMission()
        {
            var instructions = Mission.GetRemainingInstructions();
            foreach (ITreatmentInstruction instruction in instructions)
            {
                if (instruction.Enabled)
                {
                    if (instruction.FirstInstruction)
                    {
                        string uploadCmd = string.Format("set_mission -data {0} -duration {1} -entry {2} -exit {3} -id {4} -radians",
                            instruction.GetTreatmentAreaString(),
                            Mission.TreatmentDuration,
                            Mission.GetStartCoordinateString(),
                            instruction.GetExitCoordinateString(),
                            instruction.ID);

                        if (!Mission.StopAndTurnStartMode)
                            uploadCmd += " -fly_through";

                        if (instruction.AreaStatus == AreaResult.Types.Status.InProgress)
                            uploadCmd += " -in_progress";

                        SendCommand(uploadCmd, tag: instruction.ID);
                        continue;
                    }

                    var areaCmd = string.Format("add_area -data {0} -entry {1} -exit {2} -id {3} -radians",
                        instruction.GetTreatmentAreaString(),
                        instruction.GetEntryCoordianteString(),
                        instruction.GetExitCoordinateString(),
                        instruction.ID);

                    if (!Mission.StopAndTurnStartMode)
                        areaCmd += " -fly_through";

                    if (instruction.AreaStatus == AreaResult.Types.Status.InProgress)
                        areaCmd += " -in_progress";

                    SendCommand(areaCmd, tag: instruction.ID);
                }
            }
        }

        private void DirectorMonitorClient_MessageReceivedEvent(object sender, MessageReceivedEventArgs e)
        {
            switch (e.MessageType)
            {
                case ACEEnums.MessageType.InterfaceStatus:
                    var interfaceStatus = (InterfaceStatus)e.Message;
                    InterfaceState = interfaceStatus.InterfaceState;
                    AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.SynchronizeUpdate, "Interface State"));
                    break;
                case ACEEnums.MessageType.FlightStatus:
                    var flightStatus = (FlightStatus)e.Message;
                    FlightState = flightStatus.FlightState;
                    break;
                case ACEEnums.MessageType.MissionStatus:
                    var missionStatus = (MissionStatus)e.Message;
                    Mission.UpdateMissionStatus(missionStatus);
                    AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.SynchronizeUpdate, "Mission Status"));
                    break;
                case ACEEnums.MessageType.MissionConfig:
                    var missionConfig = (MissionConfig)e.Message;
                    Mission.UpdateMissionConfig(missionConfig);
                    AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.SynchronizeUpdate, "Mission Config"));

                    // Unsent route changes can only be sent after we've been updated about the mission config (tells us if there's a mission sent)
                    if (Mission.MissionSet && HasUnsentChanges)
                    {
                        foreach (ITreatmentInstruction instruction in unsentRouteChanges)
                            SendNewInstructionEntryCommand(instruction, false);
                        unsentRouteChanges.Clear();
                    }

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
                case ACEEnums.MessageType.ACEError:
                    if (syncCommandsSent > 0)
                        HandleFailedSync();
                    break;
                default:
                    break;
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
                    directorCommandQueue.Clear();
                    syncCommandsSent = 0;
                    InterfaceState = InterfaceStatus.Types.State.Offline;
                }
                else
                {
                    // Reconnecting always puts the paused state to not synchronized.
                    // Paused depends on the mission stage which may have changed. We need to resync to get the current stage.
                    if (Synchronization == SyncState.Paused)
                        Synchronization = SyncState.NotSynchronized;
                    
                    if (OBCClient.DirectorRequestClient.ReadyForCommand)
                        CommandsReadied();
                }

                UpdateCanSynchronize();
                UpdateMissionLockState();
            }
            else if (e.PropertyName == "AutoTryingConnections")
            {
                UpdateAwayOnMission();
            }
        }

        private void DirectorRequestClient_ResponseReceivedEvent(object sender, ResponseReceivedEventArgs e)
        {
            // Ping commands aren't sent by the drone class, so last command sent will be null for these
            if (lastCommandSent == null)
                return;

            // Special handling for sync commands
            if (lastCommandSent.IsSyncCommand)
            {
                if (e.Line.Contains("(FAILURE)"))
                {
                    HandleFailedSync();
                }
                else if (e.Line.Contains("(SUCCESS)"))
                {
                    // Something isn't right if we have 0 sync commands sent, but we receive a successful sync command response (likely connection was interrupted)
                    // Restart the sync process
                    if (syncCommandsSent == 0)
                    {
                        if (CanSynchronize)
                            Synchronize();
                    }
                    else
                    {
                        bool syncCommandQueued = directorCommandQueue.Any(c => c.IsSyncCommand);

                        syncCommandsSent--;
                        // If no more sync commands are sent out and none are coming down the pipe, we're finished synchronizing
                        if (syncCommandsSent == 0 && !syncCommandQueued)
                        {
                            Synchronization = SyncState.Synchronized;
                        }
                    }
                }
            }
            else if (lastCommandSent.Name == "set_mission" || lastCommandSent.Name == "add_area")
            {
                if (e.Line.Contains("(SUCCESS)"))
                    Mission.SetInstructionUploaded((int)lastCommandSent.Tag);
            }
            else if (lastCommandSent.Name == "set_config_entry")
            {
                if (e.Line.Contains("(SUCCESS)"))
                {
                    var updated_entry = (ConfigEntry)lastCommandSent.Tag;
                    var entry_index = ConfigEntries.IndexOf(ConfigEntries.FirstOrDefault(c => c.Id == updated_entry.Id));

                    ConfigEntries[entry_index].Value = updated_entry.Value;
                }

                NotifyPropertyChanged("ConfigEntries");
            }

            lastCommandSent = null;
        }

        private void HandleFailedSync()
        {
            // If a sync command fails then clear the Queue of all sync commands
            directorCommandQueue = new Queue<Command>(directorCommandQueue.Where(c => c.IsSyncCommand == false));
            syncCommandsSent = 0;
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

        private void UpdateMissionLockState()
        {
            if (OBCClient.IsDirectorConnected && Synchronization == SyncState.Synchronized)
                Mission.Unlock();
            else
                Mission.Lock();
        }
    }
}
