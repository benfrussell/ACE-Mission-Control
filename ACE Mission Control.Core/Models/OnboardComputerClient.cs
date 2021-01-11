using System;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;
using System.IO;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static ACE_Mission_Control.Core.Models.ACEEnums;
using System.Timers;
using Pbdrone;
using System.Security;
using System.Runtime.InteropServices;

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
        public SecureString Password;

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
                if (Hostname.Length != 0)
                {
                    IsConfigured = true;
                }
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

        public OnboardComputerClient(Drone parentDrone, string hostname)
        {
            AttachedDrone = parentDrone;
            Hostname = hostname;
            AutomationDisabled = false;
            AutoConnectDisabled = false;
            IsConnected = false;
            AttemptingConnection = false;
            Password = new SecureString();

            PrimaryMonitorClient = new MonitorClient();
            PrimaryMonitorClient.PropertyChanged += PrimaryMonitorClient_PropertyChanged;
            PrimaryMonitorClient.MessageReceivedEvent += PrimaryMonitorClient_MessageReceivedEvent;

            DebugMonitorClient = new MonitorClient();

            PrimaryCommanderClient = new CommanderClient();
            PrimaryCommanderClient.PropertyChanged += PrimaryCommanderClient_PropertyChanged;

            if (hostname.Length == 0)
                IsConfigured = false;
            else
                IsConfigured = true;
        }

        // Non-static methods

        public void TryConnect()
        {
            if (PrimaryMonitorClient.Connected)
                return;

            if (AttemptingConnection)
                return;

            if (!IsConfigured)
            {
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.NoConnectionNotConfigured), true);
                return;
            }

            IsConnected = false;
            AttemptingConnection = true;

            IntPtr valuePtr = IntPtr.Zero;
            try
            {
                valuePtr = Marshal.SecureStringToGlobalAllocUnicode(Password);
                string decPassword = Marshal.PtrToStringUni(valuePtr);

                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionSearching));

                PrimaryCommanderClient.StartStream(Hostname);
            }
            finally
            {
                Marshal.ZeroFreeGlobalAllocUnicode(valuePtr);
            }
        }

        private void PrimaryCommanderClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Connected" && PrimaryCommanderClient.Connected)
            {
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionStarting));
                PrimaryMonitorClient.StartStream(Hostname);

            }
            else if (e.PropertyName == "ConnectionFailure" && PrimaryCommanderClient.ConnectionFailure)
            {
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.ConnectionNoResponse));
                Disconnect();
            }
        }

        private void PrimaryMonitorClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Start the primary command stream if the monitor is receiving successfully
            if (e.PropertyName == "Connected" && PrimaryMonitorClient.Connected)
            {
                IsConnected = true;
                AttemptingConnection = false;
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionReady));
            }
            else if (e.PropertyName == "Timedout" && PrimaryMonitorClient.Timedout)
            {
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionTimedOut));
                Disconnect();
            }
        }

        private void PrimaryMonitorClient_MessageReceivedEvent(object sender, MessageReceivedEventArgs e)
        {
            if (!IsConnected)
                return;

            if (e.MessageType == MessageType.Heartbeat)
            {
                Heartbeat heartbeat = (Heartbeat)e.Message;
                if (heartbeat.Arrhythmia > 0)
                    AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.OBCSlow, heartbeat.Arrhythmia.ToString()));
            }
            else if (e.MessageType == MessageType.ACEError)
            {
                ACEError error = (ACEError)e.Message;
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.High, AlertEntry.AlertType.OBCError, error.Timestamp + ": " + error.Error));
            }
        }

        public void OpenDebugConsole()
        {
            DebugMonitorClient.StartStream(Hostname, 2, false);
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

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
