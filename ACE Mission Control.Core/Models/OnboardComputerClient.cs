using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;
using Renci.SshNet;
using System.Security.Principal;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using Renci.SshNet.Common;
using System.Collections.ObjectModel;
using static ACE_Mission_Control.Core.Models.ACETypes;

namespace ACE_Mission_Control.Core.Models
{
    public class OnboardComputerClient : INotifyPropertyChanged
    {
        // Non-static variables

        public event PropertyChangedEventHandler PropertyChanged;
        public Drone AttachedDrone;
        public MonitorClient PrimaryMonitorClient;
        public MonitorClient DebugMonitorClient;
        public CommanderClient PrimaryCommanderClient;
        public ObservableCollection<AlertEntry> Alerts;

        private ConnectionInfo connectionInfo;

        private string _hostname = "";
        public string Hostname
        {
            get { return _hostname; }
            set
            {
                if (_hostname == value)
                    return;
                _hostname = value;
                NotifyPropertyChanged();
                if (Hostname.Length != 0 && Username.Length != 0)
                {
                    IsConfigured = true;
                    UpdateStatus();
                }
            }
        }

        private string _username = "";
        public string Username
        {
            get { return _username; }
            set
            {
                if (_username == value)
                    return;
                _username = value;
                NotifyPropertyChanged();
                if (Hostname.Length != 0 && Username.Length != 0)
                {
                    IsConfigured = true;
                    UpdateStatus();
                }
            }
        }

        private StatusEnum _status;
        public StatusEnum Status
        {
            get
            {
                return _status;
            }
            set
            {
                if (value == _status)
                    return;
                _status = value;
                NotifyPropertyChanged();
            }
        }

        private bool _isConfigured;
        public bool IsConfigured
        {
            get { return _isConfigured; }
            private set
            {
                if (_isConfigured == value)
                    return;
                _isConfigured = value;
                NotifyPropertyChanged();
            }
        }

        private bool _automationDisabled;
        public bool AutomationDisabled
        {
            get { return _automationDisabled; }
            set
            {
                if (_automationDisabled == value)
                    return;
                _automationDisabled = value;
                NotifyPropertyChanged();
            }
        }

        private bool _isConnected;
        public bool IsConnected
        {
            get { return _isConnected; }
            private set
            {
                if (_isConnected == value)
                    return;
                _isConnected = value;
                NotifyPropertyChanged();
            }
        }

        public OnboardComputerClient(Drone parentDrone, string hostname, string username)
        {
            AttachedDrone = parentDrone;
            Username = username;
            Hostname = hostname;
            AutomationDisabled = false;
            IsConnected = false;
            Alerts = new ObservableCollection<AlertEntry>();

            PrimaryMonitorClient = new MonitorClient();
            PrimaryMonitorClient.PropertyChanged += PrimaryMonitorClient_PropertyChanged;

            DebugMonitorClient = new MonitorClient();

            PrimaryCommanderClient = new CommanderClient();
            PrimaryCommanderClient.PropertyChanged += PrimaryCommanderClient_PropertyChanged;

            if (hostname.Length == 0 || username.Length == 0)
                IsConfigured = false;
            else
                IsConfigured = true;

            UpdateStatus();
        }

        // Non-static methods

        public void TryConnect()
        {
            if (PrimaryMonitorClient.Receiving && PrimaryCommanderClient.Initialized)
                return;

            if (PrimaryMonitorClient.Connected)
                return;

            IsConnected = false;

            if (!OnboardComputerController.KeyOpen)
            {
                Alerts.Add(MakeAlertEntry(AlertLevel.Medium, AlertType.NoConnectionKeyClosed));
                return;
            }

            if (!IsConfigured)
            {
                Alerts.Add(MakeAlertEntry(AlertLevel.Medium, AlertType.NoConnectionNotConfigured));
                return;
            }

            connectionInfo = new ConnectionInfo(Hostname, Username, new PrivateKeyAuthenticationMethod(Username, 
                OnboardComputerController.PrivateKey));

            Alerts.Add(MakeAlertEntry(AlertLevel.Info, AlertType.MonitorConnecting));

            AlertEntry monitorAlert;
            PrimaryMonitorClient.StartStream(out monitorAlert, connectionInfo);

            UpdateStatus();

            Alerts.Add(monitorAlert);
        }

        private void PrimaryMonitorClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Start the primary command stream if the monitor is receiving successfully
            if (e.PropertyName == "Receiving" && PrimaryMonitorClient.Receiving)
            {
                Alerts.Add(MakeAlertEntry(AlertLevel.Info, AlertType.CommanderConnecting));
                AlertEntry commanderAlert;
                PrimaryCommanderClient.StartStream(out commanderAlert, connectionInfo);
                Alerts.Add(commanderAlert);
                UpdateStatus();
            }
        }

        private void PrimaryCommanderClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Initialized" && PrimaryCommanderClient.Initialized)
            {
                Alerts.Add(MakeAlertEntry(AlertLevel.Info, AlertType.ConnectionReady));
                IsConnected = true;
            }
            UpdateStatus();
        }

        public bool OpenDebugConsole(out AlertEntry alertResult)
        {
            bool successful;
            successful = DebugMonitorClient.StartStream(out alertResult, connectionInfo, 2, true);
            return successful;
        }

        public void UpdateStatus()
        {
            if (!OnboardComputerController.KeyOpen)
                Status = StatusEnum.PrivateKeyClosed;
            else if (!IsConfigured)
                Status = StatusEnum.NotConfigured;
            else if (PrimaryMonitorClient.Connected == false)
                Status = StatusEnum.SearchingPreMission;
            else if (IsConnected)
                Status = StatusEnum.ConnectedPreMission;
            else
                Status = StatusEnum.ConnectingPreMission;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
