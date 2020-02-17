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
using System.Timers;

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
        private Timer connectionTimeout;

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

        private bool _attemptingConnection;
        public bool AttemptingConnection
        {
            get { return _attemptingConnection; }
            private set
            {
                if (_attemptingConnection == value)
                    return;
                _attemptingConnection = value;
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
            AttemptingConnection = false;
            Alerts = new ObservableCollection<AlertEntry>();

            connectionTimeout = new Timer(15000);
            connectionTimeout.Elapsed += ConnectionTimeout_Elapsed;

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
            if (PrimaryMonitorClient.Connected)
                return;

            if (AttemptingConnection)
                return;

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

            IsConnected = false;
            AttemptingConnection = true;
            System.Diagnostics.Debug.WriteLine("Starting attempt...");

            connectionInfo = new ConnectionInfo(Hostname, Username, new PrivateKeyAuthenticationMethod(Username, 
                OnboardComputerController.PrivateKey));

            Alerts.Add(MakeAlertEntry(AlertLevel.Info, AlertType.MonitorConnecting));

            AlertEntry monitorAlert;

            bool successful = PrimaryMonitorClient.StartStream(out monitorAlert, connectionInfo);
            if (!successful)
                AttemptingConnection = false;
            else
                connectionTimeout.Start();

            if (!successful)
                System.Diagnostics.Debug.WriteLine("Finishing attempt...");

            UpdateStatus();

            Alerts.Add(monitorAlert);
        }

        private void PrimaryMonitorClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Start the primary command stream if the monitor is receiving successfully
            if (e.PropertyName == "Receiving" && PrimaryMonitorClient.Receiving)
            {
                // Reset the timeout to 0
                connectionTimeout.Stop();
                connectionTimeout.Start();

                Alerts.Add(MakeAlertEntry(AlertLevel.Info, AlertType.CommanderConnecting));
                AlertEntry commanderAlert;

                bool successful = PrimaryCommanderClient.StartStream(out commanderAlert, connectionInfo);
                connectionTimeout.Stop();
                if (!successful)
                    AttemptingConnection = false;
                else
                    connectionTimeout.Start();

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
                AttemptingConnection = false;
                connectionTimeout.Stop();
            }
            UpdateStatus();
        }

        private void ConnectionTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (IsConnected)
                return;

            PrimaryMonitorClient.Disconnect();
            PrimaryCommanderClient.Disconnect();

            IsConnected = false;
            AttemptingConnection = false;
            Alerts.Add(MakeAlertEntry(AlertLevel.Medium, AlertType.ConnectionTimedOut, Alerts[0].Type.ToString()));
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
