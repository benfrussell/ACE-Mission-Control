using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using static ACE_Mission_Control.Core.Models.ACEEnums;

namespace ACE_Mission_Control.Core.Models
{
    public class ResponseReceivedEventArgs : EventArgs
    {
        public string Line { get; set; }
    }

    public class CommanderClient : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<ResponseReceivedEventArgs> ResponseReceivedEvent;

        private bool _initialized;
        public bool Initialized
        {
            get { return _initialized; }
            private set
            {
                if (_initialized == value)
                    return;
                _initialized = value;
                NotifyPropertyChanged();
            }
        }

        private bool _readyForCommand;
        public bool ReadyForCommand
        {
            get { return _readyForCommand; }
            private set
            {
                if (_readyForCommand == value)
                    return;
                _readyForCommand = value;
                NotifyPropertyChanged();
            }
        }

        private bool _started;
        public bool Started
        {
            get { return _started; }
            private set
            {
                if (_started == value)
                    return;
                _started = value;
                NotifyPropertyChanged();
            }
        }

        private SshClient client;
        public ShellStream Stream;

        public CommanderClient()
        {
            Initialized = false;
            ReadyForCommand = false;
        }

        public bool StartStream(out AlertEntry alert, ConnectionInfo connectInfo)
        {
            try
            {
                client = new SshClient(connectInfo);
                client.Connect();
            }
            catch (System.Net.Sockets.SocketException e)
            {
                alert = new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.CommanderSocketError, e.Message);
                return false;
            }
            catch (SshAuthenticationException e)
            {
                alert = new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.CommanderSSHError, e.Message);
                return false;
            }

            if (client == null || !client.IsConnected)
            {
                alert = new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.CommanderCouldNotConnect);
                return false;
            }

            Stream = client.CreateShellStream("Commander", 128, 64, 512, 256, 512);
            Stream.DataReceived += Stream_DataReceived;
            Stream.WriteLine("python3 ~/ACE-Onboard-Computer/build/bin/ace_commander.py");

            alert = new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.CommanderStarting);
            Started = true;
            return true;
        }

        public bool SendCommand(out AlertEntry.AlertType error, string command)
        {
            if (!Stream.CanWrite)
            {
                error = AlertEntry.AlertType.CommanderNotWriteable;
                return false;
            }

            Stream.WriteLine(command);
            error = AlertEntry.AlertType.None;
            return true;
        }

        public void Disconnect()
        {
            if (client == null || !client.IsConnected)
                return;

            client.Disconnect();
            ReadyForCommand = false;
            Initialized = false;
        }

        private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
        {
            string data_text = Encoding.UTF8.GetString(e.Data);
            if (data_text[0] == '>')
            {
                ReadyForCommand = true;
                if (!Initialized) 
                    Initialized = true;
            }
            else
            {
                ReadyForCommand = false;
                if (!Initialized)
                    return;
                ResponseReceivedEventArgs response_e = new ResponseReceivedEventArgs();
                response_e.Line = data_text;
                ResponseReceivedEvent(this, response_e);
            }
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
