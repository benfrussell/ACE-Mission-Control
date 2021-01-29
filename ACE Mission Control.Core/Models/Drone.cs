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
        public string Input { get; set; }
        // Is this command sent automatically
        public bool AutoCommand { get; set; }
        // Is this command sent to sync the director and mission control
        public bool SyncCommand { get; set; }

        public Command(string input, bool autoCommand = false, bool syncCommand = false)
        {
            Input = input;
            AutoCommand = autoCommand;
            SyncCommand = syncCommand;
        }
    }

    public class Drone : INotifyPropertyChanged
    {
        public static List<string> ChaperoneCommandList = new List<string> { "get_error", "check_director", "start_director", "force_stop_payload" };
        public event PropertyChangedEventHandler PropertyChanged;

        public AlertEntry.AlertType LastAlertType
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

        private Mission _mission;
        public Mission Mission
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

        private bool _synchronized;
        public bool Synchronized
        {
            get { return _synchronized; }
            set
            {
                if (_synchronized == value)
                    return;
                _synchronized = value;
                NotifyPropertyChanged();
            }
        }

        private bool _canManuallySynchronize;
        public bool CanManuallySynchronize
        {
            get { return _canManuallySynchronize; }
            set
            {
                if (_canManuallySynchronize == value)
                    return;
                _canManuallySynchronize = value;
                NotifyPropertyChanged();
            }
        }

        public int ID;

        public OnboardComputerClient OBCClient;
        public ObservableCollection<AlertEntry> AlertLog;

        private Queue<Command> directorCommandQueue;
        private Queue<Command> chaperoneCommandQueue;
        private Command lastCommandSent;

        private int syncCommandsSent;
        private bool syncFailed;

        public Drone(int id, string name, string clientHostname)
        {
            directorCommandQueue = new Queue<Command>();
            chaperoneCommandQueue = new Queue<Command>();

            ID = id;
            Name = name;
            AlertLog = new ObservableCollection<AlertEntry>();
            ManualCommandsOnly = false;
            InterfaceState = InterfaceStatus.Types.State.Offline;
            Synchronized = false;
            CanManuallySynchronize = false;
            syncCommandsSent = 0;

            OBCClient = new OnboardComputerClient(this, clientHostname);
            OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            OBCClient.DirectorMonitorClient.MessageReceivedEvent += DirectorMonitorClient_MessageReceivedEvent;
            OBCClient.DirectorRequestClient.PropertyChanged += DirectorRequestClient_PropertyChanged;
            OBCClient.DirectorRequestClient.ResponseReceivedEvent += DirectorRequestClient_ResponseReceivedEvent;

            Mission = new Mission(this, OBCClient);
            Mission.PropertyChanged += Mission_PropertyChanged;
            Mission.StartParameters.StartParametersChangedEvent += StartParameters_StartParametersChangedEvent;
        }

        private void StartParameters_StartParametersChangedEvent(object sender, EventArgs e)
        {
            if (Synchronized && Mission.MissionSet)
            {
                // Send start mode commands right away if synchronized
                // If not synchronized, they will be sent during synchronization
                SendStartModeCommands(false);
            }
        }

        private void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "MissionSet" && Mission.MissionSet)
                SendStartModeCommands(false);
        }

        private void DirectorRequestClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ReadyForCommand" && OBCClient.DirectorRequestClient.ReadyForCommand)
            {
                if (OBCClient.IsDirectorConnected)
                    CommandsReadied();
            }
        }

        private void CommandsReadied()
        {
            // A sync is in progress is syncCommandsSent is greater than 0
            if ((Synchronized || syncCommandsSent > 0) && directorCommandQueue.Count > 0)
                SendCommand(directorCommandQueue.Dequeue());
            else if (!Synchronized && syncCommandsSent == 0)
                Synchronize();
        }

        private void SendStartModeCommands(bool manuallySent = false)
        {
            var entryCommand = $"set_entry -entry {Mission.GetStartCoordinateString()} -radians";
            var flyThroughCommand = Mission.StartParameters.StopAndTurn ? "set_fly_through -off" : "set_fly_through -on";

            // Only send these commands if the mission is set
            if (!Mission.MissionSet)
                return;

            var wasActivated = Mission.Activated;
            if (Mission.Activated)
            {
                syncCommandsSent++;
                SendCommand("deactivate_mission", true, true);
            }

            syncCommandsSent += 2;
            // If the command is sent right away then we call it a manual command
            SendCommand(entryCommand, !manuallySent, true);
            SendCommand(flyThroughCommand, !manuallySent, true);

            if (wasActivated)
            {
                syncCommandsSent++;
                SendCommand("activate_mission", true, true);
            }
        }

        // Check commands update the state of the Onboard Computer with Mission Control
        // They're sent everytime a connection to the director is re-established
        public void Synchronize(bool manualSyncronize = false)
        {
            syncFailed = false;
            // Manual syncs are allowed when a sync failure occurs until another sync attempt is made
            CanManuallySynchronize = false;
            syncCommandsSent += 3;
            SendCommand("check_interface", !manualSyncronize, true);
            SendCommand("check_mission_config", !manualSyncronize, true);
            SendCommand("check_mission_status", !manualSyncronize, true);
            // The mission status might trigger a start mode update anyway but better safe than sorry
            if (manualSyncronize)
                SendStartModeCommands(manualSyncronize);
        }

        public void SendCommand(string command, bool autoCommand = false, bool syncCommand = false)
        {
            SendCommand(new Command(command, autoCommand, syncCommand));
        }

        // TODO: Should probably handle this in OnboardComputerClient but keep this as an interface for the ViewModel?
        public void SendCommand(Command command)
        {
            string cmdNameOnly = command.Input.Split(' ')[0];
            
            if (command.AutoCommand && ManualCommandsOnly)
            {
                AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.CommandError, $": command '{command}' was not sent in manual mode because it was an automatic command."));
                return;
            }

            // Don't allow any more sync commands if the sync failed
            if (command.SyncCommand && syncFailed)
                return;

            // Commands are dumped if the client isn't connected, otherwise if the send fails the command is queued
            if (ChaperoneCommandList.Any(c => c == cmdNameOnly))
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
                    directorCommandQueue.Enqueue(command);
            }

        }

        // Returns success or failure
        private bool SendCommandWithClient(RequestClient client, Command command)
        {
            if (!client.ReadyForCommand)
                return false;

            bool sendSuccessful = client.SendCommand(command.Input);

            if (sendSuccessful)
            {
                if (command.SyncCommand)
                {
                    if (Synchronized)
                        Synchronized = false;
                }
                lastCommandSent = command;
                return true;
            }

            return false;
        }

        public void UploadMission()
        {
            bool firstCmd = true;
            var instructions = Mission.GetRemainingInstructions();
            foreach (TreatmentInstruction instruction in instructions)
            {
                if (instruction.Enabled)
                {
                    if (firstCmd)
                    {
                        string uploadCmd = string.Format("set_mission -data {0} -duration {1} -entry {2} -exit {3} -id {4} -radians",
                            instruction.GetTreatmentAreaString(),
                            Mission.TreatmentDuration,
                            Mission.GetStartCoordinateString(),
                            instruction.GetExitCoordinateString(),
                            instruction.TreatmentPolygon.Id);
                        SendCommand(uploadCmd);
                        firstCmd = false;
                        continue;
                    }

                    SendCommand(string.Format("add_area -data {0} -entry {1} -exit {2} -id {3} -radians",
                        instruction.GetTreatmentAreaString(),
                        instruction.GetEntryCoordianteString(),
                        instruction.GetExitCoordinateString(),
                        instruction.TreatmentPolygon.Id));
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
                    break;
                case ACEEnums.MessageType.FlightStatus:
                    var flightStatus = (FlightStatus)e.Message;
                    FlightState = flightStatus.FlightState;
                    break;
                case ACEEnums.MessageType.MissionStatus:
                    var missionStatus = (MissionStatus)e.Message;
                    Mission.UpdateMissionStatus(missionStatus);
                    break;
                case ACEEnums.MessageType.MissionConfig:
                    var missionConfig = (MissionConfig)e.Message;
                    Mission.UpdateMissionConfig(missionConfig);
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

        private void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsDirectorConnected")
            {
                NotifyPropertyChanged("OBCCanBeTested");

                if (!OBCClient.IsDirectorConnected)
                {
                    Synchronized = false;
                    directorCommandQueue.Clear();
                    InterfaceState = InterfaceStatus.Types.State.Offline;
                }
                else
                {
                    if (OBCClient.DirectorRequestClient.ReadyForCommand)
                        CommandsReadied();
                }
            }
        }

        private void DirectorRequestClient_ResponseReceivedEvent(object sender, ResponseReceivedEventArgs e)
        {
            // Ping commands aren't sent by the drone class, so last command sent will be null for these
            if (lastCommandSent == null)
                return;

            // Special handling for sync commands
            if (lastCommandSent.SyncCommand && e.Line.Contains("(FAILURE)"))
            {
                // If a sync command fails then clear the Queue of all sync commands
                directorCommandQueue = new Queue<Command>(directorCommandQueue.Where(c => c.SyncCommand == false));
                syncCommandsSent = 0;
                syncFailed = true;
                CanManuallySynchronize = true;
            }
            else if (lastCommandSent.SyncCommand && e.Line.Contains("(SUCCESS)"))
            {
                syncCommandsSent--;
                if (syncCommandsSent == 0)
                    Synchronized = true;
            }

            lastCommandSent = null;
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
