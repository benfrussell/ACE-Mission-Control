using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Text;
using System.Numerics;
using System.Collections.ObjectModel;
using System.Runtime.CompilerServices;
using Pbdrone;

namespace ACE_Mission_Control.Core.Models
{
    public class Drone : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        public int ID;
        public string Name;
        public OnboardComputerClient OBCClient;
        public ObservableCollection<AlertEntry> AlertLog;

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
            get { return (!MissionIsActivated && 
                    MissionStage != MissionStatus.Types.Stage.NoMission) ||
                    NewMission;  }
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
                _missionData = value;
                NotifyPropertyChanged();
            }
        }

        public Drone(int id, string name, string clientHostname, string clientUsername)
        {
            ID = id;
            Name = name;

            OBCClient = new OnboardComputerClient(this, clientHostname, clientUsername);
            OBCClient.PropertyChanged += OBCClient_PropertyChanged;

            AlertLog = new ObservableCollection<AlertEntry>();
            MissionData = new MissionData();
            NewMission = false;
        }

        private void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "IsConnected")
            {
                NotifyPropertyChanged("OBCCanBeTested");
                NotifyPropertyChanged("MissionCanBeReset");
                NotifyPropertyChanged("MissionCanToggleActivation");
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
