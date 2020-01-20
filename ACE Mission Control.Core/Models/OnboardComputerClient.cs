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

namespace ACE_Mission_Control.Core.Models
{
    public class OnboardComputerClient : INotifyPropertyChanged
    {
        public enum StatusEnum
        {
            PrivateKeyClosed,
            NotConfigured,
            WaitingPreMission,
            TryingPreMission,
            ConnectedPreMission
        }

        // Non-static variables

        public event PropertyChangedEventHandler PropertyChanged;
        public Drone AttachedDrone;
        private SshClient client;
        private ShellStream sshStream;

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
                    _isConfigured = true;
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
                    _isConfigured = true;
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
        }

        public OnboardComputerClient(Drone parentDrone, string hostname, string username)
        {
            AttachedDrone = parentDrone;
            Username = username;
            Hostname = hostname;
            client = null;

            if (hostname.Length == 0 || username.Length == 0)
                _isConfigured = false;
            else
                _isConfigured = true;

            UpdateStatus();
        }

        // Non-static methods

        public bool TryConnect(out string result)
        {
            if (client != null && client.IsConnected)
            {
                result = "Already connected.";
                return true;
            }

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

            var connectInfo = new ConnectionInfo(Hostname, Username, new PrivateKeyAuthenticationMethod(Username, 
                OnboardComputerController.PrivateKey));

            client = new SshClient(connectInfo);

            client.Connect();
            UpdateStatus();

            if (client.IsConnected)
            {
                sshStream = client.CreateShellStream("ACE Terminal", 128, 64, 512, 256, 512);
                sshStream.DataReceived += SshStream_DataReceived;
                sshStream.WriteLine("python ~/py-scripts/blink.py");
            }

            result = "Connected maybe!";
            return true;
        }

        private void SshStream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
        {
            //System.Diagnostics.Debug.WriteLine("SSH DATA RECEIVED");
            System.Diagnostics.Debug.Write(Encoding.Default.GetString(e.Data));
        }

        public void UpdateStatus()
        {
            if (!OnboardComputerController.KeyOpen)
                Status = StatusEnum.PrivateKeyClosed;
            else if (!IsConfigured)
                Status = StatusEnum.NotConfigured;
            else if (client == null)
                Status = StatusEnum.WaitingPreMission;
            else if (client.IsConnected)
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
