﻿using System;
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
using Pbdrone;

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
        public double ConnectionTimeoutDelay;
        public double HeartbeatTimeoutDelay;

        private ConnectionInfo connectionInfo;
        private Timer connectionTimeout;
        private Timer heartbeatTimeout;

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

        private bool _autoConnectDisabled;
        public bool AutoConnectDisabled
        {
            get { return _autoConnectDisabled; }
            set
            {
                if (_autoConnectDisabled == value)
                    return;
                _autoConnectDisabled = value;
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
            AutoConnectDisabled = false;
            IsConnected = false;
            AttemptingConnection = false;
            Alerts = new ObservableCollection<AlertEntry>();

            ConnectionTimeoutDelay = 15000;
            connectionTimeout = new Timer(ConnectionTimeoutDelay);
            connectionTimeout.Elapsed += ConnectionTimeout_Elapsed;

            HeartbeatTimeoutDelay = 3000;
            heartbeatTimeout = new Timer(HeartbeatTimeoutDelay);
            heartbeatTimeout.Elapsed += HeartbeatTimeout_Elapsed;

            PrimaryMonitorClient = new MonitorClient();
            PrimaryMonitorClient.PropertyChanged += PrimaryMonitorClient_PropertyChanged;
            PrimaryMonitorClient.MessageReceivedEvent += PrimaryMonitorClient_MessageReceivedEvent;

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

            System.Diagnostics.Debug.WriteLine("TRY CONNECT");

            IsConnected = false;
            AttemptingConnection = true;

            connectionInfo = new ConnectionInfo(Hostname, Username, new PrivateKeyAuthenticationMethod(Username, 
                OnboardComputerController.PrivateKey));

            Alerts.Add(MakeAlertEntry(AlertLevel.Info, AlertType.MonitorConnecting));

            AlertEntry monitorAlert;

            bool successful = PrimaryMonitorClient.StartStream(out monitorAlert, connectionInfo);
            if (!successful)
                AttemptingConnection = false;
            else
                connectionTimeout.Start();

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
                heartbeatTimeout.Start();
            }
            UpdateStatus();
        }

        private void ConnectionTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (IsConnected)
                return;
            Disconnect();
            Alerts.Add(MakeAlertEntry(AlertLevel.Medium, AlertType.ConnectionTimedOut, Alerts[0].Type.ToString()));
        }

        private void PrimaryMonitorClient_MessageReceivedEvent(object sender, MessageReceivedEventArgs e)
        {
            if (!IsConnected)
                return;

            if (e.MessageType == MessageType.Heartbeat)
            {
                Heartbeat heartbeat = (Heartbeat)e.Message;
                if (heartbeat.Arrhythmia > 0)
                    Alerts.Add(MakeAlertEntry(AlertLevel.Medium, AlertType.OBCSlow, heartbeat.Arrhythmia.ToString()));
                heartbeatTimeout.Stop();
                heartbeatTimeout.Start();
            }
            else if (e.MessageType == MessageType.ACEError)
            {
                ACEError error = (ACEError)e.Message;
                Alerts.Add(MakeAlertEntry(AlertLevel.High, AlertType.OBCError, error.Timestamp + ": " + error.Message));
            }
        }

        private void HeartbeatTimeout_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!IsConnected)
                return;
            Disconnect();
            Alerts.Add(MakeAlertEntry(AlertLevel.Medium, AlertType.OBCStoppedResponding));
        }

        public bool OpenDebugConsole(out AlertEntry alertResult)
        {
            bool successful;
            successful = DebugMonitorClient.StartStream(out alertResult, connectionInfo, 2, false);
            return successful;
        }

        public void Disconnect()
        {
            PrimaryMonitorClient.Disconnect();
            PrimaryCommanderClient.Disconnect();
            if (DebugMonitorClient.Connected)
                DebugMonitorClient.Disconnect();
            IsConnected = false;
            AttemptingConnection = false;
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
