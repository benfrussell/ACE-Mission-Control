using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Pbdrone;
using Windows.ApplicationModel.Core;

namespace ACE_Mission_Control.Core.Models
{
    public class Drone : INotifyPropertyChanged
    {
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

        private bool _newMission;
        public bool NewMission
        {
            get { return _newMission; }
            set
            {
                if (_newMission == value)
                    return;
                _newMission = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("MissionCanBeModified");
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

        private MissionStatus.Types.Stage _missionStage;
        public MissionStatus.Types.Stage MissionStage
        {
            get { return _missionStage; }
            set
            {
                if (_missionStage == value)
                    return;
                _missionStage = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("MissionCanBeReset");
                NotifyPropertyChanged("MissionCanBeModified");
                NotifyPropertyChanged("MissionCanToggleActivation");
            }
        }

        private bool _missionIsActivated;
        public bool MissionIsActivated 
        { 
            get { return _missionIsActivated; }
            set
            {
                if (_missionIsActivated == value)
                    return;
                _missionIsActivated = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("OBCCanBeTested");
                NotifyPropertyChanged("MissionCanBeReset");
                NotifyPropertyChanged("MissionCanBeModified");
                NotifyPropertyChanged("MissionCanToggleActivation");
            }
        }

        private bool _missionHasProgess;
        public bool MissionHasProgress
        {
            get { return _missionHasProgess; }
            set
            {
                if (_missionHasProgess == value)
                    return;
                _missionHasProgess = value;
                NotifyPropertyChanged();
                NotifyPropertyChanged("MissionCanBeReset");
            }
        }   

        public bool OBCCanBeTested
        {
            get { return !MissionIsActivated && OBCClient.IsConnected; }
        }

        public bool MissionCanBeReset
        {
            get { return MissionHasProgress && 
                    !MissionIsActivated && 
                    OBCClient.IsConnected && 
                    MissionStage != MissionStatus.Types.Stage.NoMission; }
        }

        public bool MissionCanBeModified
        {
            get { return OBCClient.IsConnected && ((!MissionIsActivated && 
                    MissionStage != MissionStatus.Types.Stage.NoMission) ||
                    NewMission);  }
        }

        public bool MissionCanToggleActivation
        {
            get
            {
                if (!OBCClient.IsConnected)
                    return false;
                if (MissionIsActivated)
                    return true;
                else
                    return InterfaceState == InterfaceStatus.Types.State.Online && 
                        MissionStage != MissionStatus.Types.Stage.NoMission;
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
                NotifyPropertyChanged();
            }
        }

        private int _treatmentDuration;
        public int TreatmentDuration
        {
            get { return _treatmentDuration; }
            set
            {
                if (_treatmentDuration == value)
                    return;
                _treatmentDuration = value;
                NotifyPropertyChanged();
            }
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
                NotifyPropertyChanged();
            }
        }

        private List<string> _availablePayloads;
        public List<string> AvailablePayloads
        {
            get { return _availablePayloads; }
            set
            {
                if (_availablePayloads == value)
                    return;
                _availablePayloads = value;
                NotifyPropertyChanged();
            }
        }

        private MissionData _missionData;
        public MissionData MissionData
        {
            get { return _missionData; }
            set
            {
                if (_missionData == value)
                    return;
                _missionData = value;
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

        public int ID;
        
        public OnboardComputerClient OBCClient;
        public ObservableCollection<AlertEntry> AlertLog;
        private Queue<string> commandQueue;
        private bool checkCommandsSent;

        public Drone(int id, string name, string clientHostname, string clientUsername)
        {
            commandQueue = new Queue<string>();

            ID = id;
            Name = name;
            AlertLog = new ObservableCollection<AlertEntry>();
            MissionData = new MissionData();
            NewMission = false;

            OBCClient = new OnboardComputerClient(this, clientHostname);
            OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            OBCClient.PrimaryMonitorClient.MessageReceivedEvent += PrimaryMonitorClient_MessageReceivedEvent;
            OBCClient.PrimaryCommanderClient.PropertyChanged += PrimaryCommanderClient_PropertyChanged;
        }

        private void PrimaryCommanderClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ReadyForCommand" && OBCClient.PrimaryCommanderClient.ReadyForCommand)
            {
                if (OBCClient.IsConnected)
                {
                    if (checkCommandsSent && commandQueue.Count > 0)
                    {
                        SendCommand(commandQueue.Dequeue());
                    }
                    else if (!checkCommandsSent)
                    {
                        SendCheckCommands();
                    }
                }
            }
            else if (e.PropertyName == "Connected")
            {
                checkCommandsSent = false;
            }
                
        }

        private void SendCheckCommands()
        {
            SendCommand("check_interface");
            SendCommand("check_mission_status");
            SendCommand("check_mission_config");
            checkCommandsSent = true;
        }

        public void SendCommand(string command)
        {
            if (!OBCClient.PrimaryCommanderClient.Connected)
            {
                var alert = new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.NoConnection);
                AddAlert(alert);
                return;
            }
            else if (!OBCClient.PrimaryCommanderClient.ReadyForCommand)
            {
                commandQueue.Enqueue(command);
                return;
            }

            if (!OBCClient.PrimaryCommanderClient.SendCommand(command))
            {
                commandQueue.Enqueue(command);
            }
        }

        public void UploadMission()
        {
            string uploadCmd = string.Format("set_mission -data {0} -duration {1} -entry {2} -name {3} -radians", 
                MissionData.AreaScanRoutes[0].GetVerticesString(),
                TreatmentDuration,
                MissionData.AreaScanRoutes[0].GetEntryVetexString(),
                MissionData.AreaScanRoutes[0].Name);
            SendCommand(uploadCmd);

            if (MissionData.AreaScanRoutes.Count > 1)
                for (int i = 1; i < MissionData.AreaScanRoutes.Count; i++)
                    SendCommand(string.Format("add_area -data {0} -name {1} -radians", 
                        MissionData.AreaScanRoutes[i].GetVerticesString(),
                        MissionData.AreaScanRoutes[i].Name));
        }

        private void PrimaryMonitorClient_MessageReceivedEvent(object sender, MessageReceivedEventArgs e)
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
                    MissionStage = missionStatus.MissionStage;
                    MissionIsActivated = missionStatus.Activated;
                    MissionHasProgress = missionStatus.InProgress;
                    break;
                case ACEEnums.MessageType.MissionConfig:
                    var missionConfig = (MissionConfig)e.Message;
                    FlyThroughMode = missionConfig.FlyThroughMode;
                    TreatmentDuration = missionConfig.TreatmentDuration;
                    AvailablePayloads = missionConfig.AvailablePayloads.ToList();
                    SelectedPayload = missionConfig.SelectedPayload;
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
            if (e.PropertyName == "IsConnected")
            {
                NotifyPropertyChanged("OBCCanBeTested");
                NotifyPropertyChanged("MissionCanBeReset");
                NotifyPropertyChanged("MissionCanBeModified");
                NotifyPropertyChanged("MissionCanToggleActivation");

                if (OBCClient.IsConnected && !checkCommandsSent && OBCClient.PrimaryCommanderClient.ReadyForCommand)
                {
                    SendCheckCommands();
                }
            }
        }

        public async void AddAlert(AlertEntry entry, bool blockDuplicates = false)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (blockDuplicates && entry.Type == LastAlertType)
                    return;
                AlertLog.Add(entry);
            });
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
