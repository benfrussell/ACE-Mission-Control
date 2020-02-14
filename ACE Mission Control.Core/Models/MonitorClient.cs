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
    public class LineReceivedEventArgs : EventArgs
    {
        public string Line { get; set; }
    }

    public class MonitorClient : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<LineReceivedEventArgs> LineReceivedEvent;

        private bool _receiving;
        public bool Receiving
        {
            get { return _receiving; }
            private set
            {
                if (_receiving == value)
                    return;
                _receiving = value;
                NotifyPropertyChanged();
            }
        }

        private string _allReceived;
        public string AllReceived
        {
            get { return _allReceived; }
            set
            {
                if (_allReceived != null && value.Length == _allReceived.Length)
                    return;
                _allReceived = value;
                NotifyPropertyChanged();
            }
        }

        public bool Started = false;

        private SshClient client;
        private ShellStream stream;
        private bool byte_mode;

        public MonitorClient()
        {
            Receiving = false;
            AllReceived = "";
        }

        public bool StartStream(out string result, ConnectionInfo connectionInfo, int debug_level = 0, bool heartbeat = true)
        {
            try
            {
                client = new SshClient(connectionInfo);
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

            AllReceived = "";
            byte_mode = debug_level == 0;

            string debug_arg = "-d " + debug_level.ToString();
            string heartbeat_arg = heartbeat ? " -h" : "";

            stream = client.CreateShellStream("Monitor", 128, 64, 512, 256, 512);
            stream.DataReceived += Stream_DataReceived;
            stream.WriteLine("python3 ~/Drone/build/bin/ace_monitor.py " + debug_arg + heartbeat_arg);

            result = "Success.";
            Started = true;
            return true;
        }

        private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
        {
            string data_text = Encoding.UTF8.GetString(e.Data);
            AllReceived += data_text;

            LineReceivedEventArgs line_e = new LineReceivedEventArgs();
            line_e.Line = data_text;

            if (!Receiving && data_text == "Ready to receive updates.")
            {
                Receiving = true;
            }

            LineReceivedEvent(this, line_e);
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
