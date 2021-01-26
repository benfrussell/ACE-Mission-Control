using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Pbdrone;

namespace ACE_Mission_Control.Core.Models
{
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
                NotifyPropertyChanged("MissionCanToggleActivation");
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

        public int ID;

        public OnboardComputerClient OBCClient;
        public ObservableCollection<AlertEntry> AlertLog;
        private Queue<string> directorCommandQueue;
        private Queue<string> chaperoneCommandQueue;
        private bool checkCommandsSent;

        public Drone(int id, string name, string clientHostname)
        {
            directorCommandQueue = new Queue<string>();
            chaperoneCommandQueue = new Queue<string>();

            ID = id;
            Name = name;
            AlertLog = new ObservableCollection<AlertEntry>();
            ManualCommandsOnly = false;
            InterfaceState = InterfaceStatus.Types.State.Offline;

            OBCClient = new OnboardComputerClient(this, clientHostname);
            OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            OBCClient.DirectorMonitorClient.MessageReceivedEvent += DirectorMonitorClient_MessageReceivedEvent;
            OBCClient.DirectorRequestClient.PropertyChanged += DirectorRequestClient_PropertyChanged;

            Mission = new Mission(this, OBCClient);
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
            if (checkCommandsSent && directorCommandQueue.Count > 0)
                SendCommand(directorCommandQueue.Dequeue());
            else if (!checkCommandsSent)
                SendCheckCommands();
        }

        // Check commands update the state of the Onboard Computer with Mission Control
        // They're sent everytime a connection to the director is re-established
        private void SendCheckCommands()
        {
            if (ManualCommandsOnly)
                return;
            SendCommand("check_interface");
            SendCommand("check_mission_status");
            SendCommand("check_mission_config");
            checkCommandsSent = true;
        }

        // TODO: Should probably handle this in OnboardComputerClient but keep this as an interface for the ViewModel?
        public void SendCommand(string command)
        {
            string commandOnly = command.Split(' ')[0];
            
            // Commands are dumped if the client isn't connected, otherwise if the send fails the command is queued

            if (ChaperoneCommandList.Any(c => c == commandOnly))
            {
                if (!OBCClient.IsChaperoneConnected)
                {
                    AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.NoConnection));
                    return;
                }
                    
                if (!SendCommandWithClient(OBCClient.ChaperoneRequestClient, command) && !ManualCommandsOnly)
                    chaperoneCommandQueue.Enqueue(command);
            }
            else
            {
                if (!OBCClient.IsDirectorConnected)
                {
                    AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.NoConnection));
                    return;
                }

                if (!SendCommandWithClient(OBCClient.DirectorRequestClient, command) && !ManualCommandsOnly)
                    directorCommandQueue.Enqueue(command);
            }

        }

        // Returns success or failure
        private bool SendCommandWithClient(RequestClient client, string command)
        {
            if (!client.ReadyForCommand)
                return false;
            if (!client.SendCommand(command))
                return false;
            return true;
        }

        public void UploadMission()
        {
            bool firstCmd = true;
            foreach (TreatmentInstruction instruction in Mission.TreatmentInstructions)
            {
                if (instruction.Enabled)
                {
                    if (firstCmd)
                    {
                        string uploadCmd = string.Format("set_mission -data {0} -duration {1} -entry {2} -name {3} -radians",
                            Mission.TreatmentInstructions[0].GetTreatmentAreaString(),
                            Mission.TreatmentDuration,
                            Mission.GetStartCoordianteString(),
                            Mission.TreatmentInstructions[0].Name);
                        SendCommand(uploadCmd);
                        firstCmd = false;
                        continue;
                    }

                    SendCommand(string.Format("add_area -data {0} -name {1} -radians",
                        instruction.GetTreatmentAreaString(),
                        instruction.Name));
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
                    break;
                case ACEEnums.MessageType.MissionConfig:
                    var missionConfig = (MissionConfig)e.Message;
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
                NotifyPropertyChanged("MissionCanBeReset");
                NotifyPropertyChanged("MissionCanBeModified");
                NotifyPropertyChanged("MissionCanToggleActivation");

                if (!OBCClient.IsDirectorConnected)
                {
                    checkCommandsSent = false;
                    InterfaceState = InterfaceStatus.Types.State.Offline;
                }
                else
                {
                    if (OBCClient.DirectorRequestClient.ReadyForCommand)
                        CommandsReadied();
                }
            }
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
