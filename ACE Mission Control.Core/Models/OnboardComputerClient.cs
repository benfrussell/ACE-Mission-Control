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

namespace ACE_Mission_Control.Core.Models
{
    public class OnboardComputerClient : INotifyPropertyChanged
    {
        public enum StatusEnum
        {
            PrivateKeyClosed,
            NotConfigured,
            SearchingPreMission,
            TryingPreMission,
            ConnectedPreMission
        }

        // Non-static variables

        public event PropertyChangedEventHandler PropertyChanged;
        public Drone AttachedDrone;
        public MonitorClient PrimaryMonitorClient;
        public MonitorClient DebugMonitorClient;
        public CommanderClient PrimaryCommanderClient;

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

        private bool _commanderErrorFlag;
        public bool CommanderErrorFlag
        {
            get { return _commanderErrorFlag; }
            private set
            {
                if (_commanderErrorFlag == value)
                    return;
                _commanderErrorFlag = value;
                NotifyPropertyChanged();
            }
        }

        private string _commanderErrorText;
        public string CommanderErrorText
        {
            get { return _commanderErrorText; }
            private set
            {
                if (_commanderErrorText == value)
                    return;
                _commanderErrorText = value;
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

        public bool TryConnect(out string result)
        {
            if (PrimaryMonitorClient.Receiving && PrimaryCommanderClient.Initialized)
            {
                result = "Already connected.";
                return true;
            }

            if (PrimaryMonitorClient.Started)
            {
                result = "Already starting.";
                return true;
            }

            IsConnected = false;

            if (!OnboardComputerController.KeyOpen)
            {
                result = "The private key has not been opened.";
                return false;
            }

            if (!IsConfigured)
            {
                result = "This client isn't configured.";
                return false;
            }

            connectionInfo = new ConnectionInfo(Hostname, Username, new PrivateKeyAuthenticationMethod(Username, 
                OnboardComputerController.PrivateKey));

            CommanderErrorFlag = false;
            CommanderErrorText = "";

            bool successful = PrimaryMonitorClient.StartStream(out result, connectionInfo);

            UpdateStatus();

            return successful;
        }

        private void PrimaryMonitorClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Start the debug command stream if the monitor is receiving successfully
            if (e.PropertyName == "Receiving" && PrimaryMonitorClient.Receiving)
            {
                string result;
                if (!PrimaryCommanderClient.StartStream(out result, connectionInfo))
                {
                    CommanderErrorFlag = true;
                    CommanderErrorText = result;
                }
                UpdateStatus();
            }
        }

        private void PrimaryCommanderClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Initialized" && PrimaryCommanderClient.Initialized)
            {
                IsConnected = true;
            }
            UpdateStatus();
        }

        public bool OpenDebugConsole(out string error)
        {
            string stream_error;
            if (!DebugMonitorClient.StartStream(out stream_error, connectionInfo, 2, true))
            {
                error = stream_error;
                return false;
            }
            error = "";
            return true;
        }

        public void UpdateStatus()
        {
            if (!OnboardComputerController.KeyOpen)
                Status = StatusEnum.PrivateKeyClosed;
            else if (!IsConfigured)
                Status = StatusEnum.NotConfigured;
            else if (PrimaryMonitorClient.Started == false)
                Status = StatusEnum.SearchingPreMission;
            else if (IsConnected)
                Status = StatusEnum.ConnectedPreMission;
            else
                Status = StatusEnum.TryingPreMission;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
