using Renci.SshNet;
using Renci.SshNet.Common;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using static ACE_Mission_Control.Core.Models.ACEEnums;
using Google.Protobuf;
using Pbdrone;

namespace ACE_Mission_Control.Core.Models
{
    public class LineReceivedEventArgs : EventArgs
    {
        public string Line { get; set; }
    }

    public class MessageReceivedEventArgs : EventArgs
    {
        public MessageType MessageType;
        public IMessage Message;
    }

    public class MonitorClient : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;
        public event EventHandler<LineReceivedEventArgs> LineReceivedEvent;
        public event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;

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

        public bool Connected = false;

        private SshClient client;
        private ShellStream stream;
        private bool byte_mode;

        public MonitorClient()
        {
            Receiving = false;
            AllReceived = "";
        }

        public bool StartStream(out AlertEntry alert, ConnectionInfo connectionInfo, int debug_level = 0, bool heartbeat = true)
        {
            try
            {
                client = new SshClient(connectionInfo);
                client.Connect();
            }
            catch (System.Net.Sockets.SocketException e)
            {
                if (e.SocketErrorCode == System.Net.Sockets.SocketError.TimedOut)
                    alert = new AlertEntry(AlertLevel.Info, AlertType.MonitorConnecting);
                else
                    alert = new AlertEntry(AlertLevel.Medium, AlertType.MonitorSocketError, e.Message);
                return false;
            }
            catch (SshAuthenticationException e)
            {
                alert = new AlertEntry(AlertLevel.Medium, AlertType.MonitorSSHError, e.Message);
                return false;
            }

            if (client == null || !client.IsConnected)
            {
                alert = new AlertEntry(AlertLevel.Medium, AlertType.MonitorCouldNotConnect);
                return false;
            }

            AllReceived = "";
            byte_mode = debug_level == 0;

            string debug_arg = "-d " + debug_level.ToString();
            string heartbeat_arg = heartbeat ? " -h" : "";

            stream = client.CreateShellStream("Monitor", 128, 64, 512, 256, 512);
            stream.DataReceived += Stream_DataReceived;
            stream.WriteLine("python3 ~/ACE-Onboard-Computer/build/bin/ace_monitor.py " + debug_arg + heartbeat_arg);

            Connected = true;
            alert = new AlertEntry(AlertLevel.Info, AlertType.MonitorStarting);
            return true;
        }

        public void Disconnect()
        {
            if (client == null || !client.IsConnected)
                return;

            client.Disconnect();
            Receiving = false;
            Connected = false;
        }

        private void Stream_DataReceived(object sender, Renci.SshNet.Common.ShellDataEventArgs e)
        {
            string data_text = Encoding.UTF8.GetString(e.Data);

            if (!byte_mode)
            {
                AllReceived += data_text;

                LineReceivedEventArgs line_e = new LineReceivedEventArgs();
                line_e.Line = data_text;

                LineReceivedEvent(this, line_e);
            }
            else if (Receiving)
            {
                string[] data_split = data_text.Trim().Split(':');
                if (data_split.Length != 2)
                    return;

                int message_type_id;
                if (!int.TryParse(data_split[0], out message_type_id))
                    return;

                byte[] message_data = StringToByteArray(data_split[1]);
                IMessage message = null;

                switch ((MessageType)message_type_id)
                {
                    case MessageType.Heartbeat:
                        message = Heartbeat.Parser.ParseFrom(message_data);
                        break;
                    case MessageType.InterfaceStatus:
                        message = InterfaceStatus.Parser.ParseFrom(message_data);
                        break;
                    case MessageType.FlightStatus:
                        message = FlightStatus.Parser.ParseFrom(message_data);
                        break;
                    case MessageType.ControlDevice:
                        message = ControlDevice.Parser.ParseFrom(message_data);
                        break;
                    case MessageType.Position:
                        message = Position.Parser.ParseFrom(message_data);
                        break;
                    case MessageType.FlightAnomaly:
                        message = FlightAnomaly.Parser.ParseFrom(message_data);
                        break;
                    case MessageType.ACEError:
                        message = ACEError.Parser.ParseFrom(message_data);
                        break;
                    default:
                        System.Diagnostics.Debug.WriteLine("Received unknown message type: " + message_type_id);
                        break;
                }

                if (message != null)
                {
                    MessageReceivedEventArgs messageEventArgs = new MessageReceivedEventArgs();
                    messageEventArgs.MessageType = (MessageType)message_type_id;
                    messageEventArgs.Message = message;
                    MessageReceivedEvent(this, messageEventArgs);
                }
            }

            if (!Receiving && data_text == "Ready to receive updates.")
                Receiving = true;
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        private static byte[] StringToByteArray(string hex)
        {
            if (hex.Length % 2 == 1)
                throw new Exception("The binary key cannot have an odd number of digits");

            byte[] arr = new byte[hex.Length >> 1];

            for (int i = 0; i < hex.Length >> 1; ++i)
            {
                arr[i] = (byte)((GetHexVal(hex[i << 1]) << 4) + (GetHexVal(hex[(i << 1) + 1])));
            }

            return arr;
        }

        private static int GetHexVal(char hex)
        {
            int val = (int)hex;
            //For uppercase A-F letters:
            //return val - (val < 58 ? 48 : 55);
            //For lowercase a-f letters:
            return val - (val < 58 ? 48 : 87);
            //Or the two combined, but a bit slower:
            //return val - (val < 58 ? 48 : (val < 97 ? 55 : 87));
        }
    }
}
