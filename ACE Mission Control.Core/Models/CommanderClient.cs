using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;

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

        public bool Started = false;

        private SshClient client;
        private ShellStream stream;

        public CommanderClient()
        {
            Initialized = false;
            ReadyForCommand = false;
        }

        public bool StartStream(out string result, ConnectionInfo connectInfo)
        {
            try
            {
                client = new SshClient(connectInfo);
                client.Connect();
            }
            catch (System.Net.Sockets.SocketException e)
            {
                result = e.Message;
                return false;
            }
            catch (SshAuthenticationException e)
            {
                result = e.Message;
                return false;
            }

            if (client == null || !client.IsConnected)
            {
                result = "Client did not connect.";
                return false;
            }

            stream = client.CreateShellStream("Commander", 128, 64, 512, 256, 512);
            stream.DataReceived += Stream_DataReceived;
            stream.WriteLine("python3 ~/Drone/build/bin/ace_commander.py");

            result = "Successful";
            Started = true;
            return true;
        }

        public bool SendCommand(out string error, string command)
        {
            if (!stream.CanWrite)
            {
                error = "The command stream is not currently writeable.";
                return false;
            }

            stream.WriteLine(command);
            error = "";
            return true;
        }

        private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
        {
            string data_text = Encoding.UTF8.GetString(e.Data);
            System.Diagnostics.Debug.WriteLine("RECEIVED: " + data_text);
            if (data_text[0] == '>')
            {
                ReadyForCommand = true;
                if (!Initialized) { Initialized = true; }
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
