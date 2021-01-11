using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using Google.Protobuf;
using static ACE_Mission_Control.Core.Models.ACEEnums;
using Pbdrone;
using NetMQ.Sockets;
using NetMQ;
using System.Collections.Generic;
using System.Timers;

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

        private bool _connected;
        public bool Connected
        {
            get { return _connected; }
            private set
            {
                if (_connected == value)
                    return;
                _connected = value;
                NotifyPropertyChanged();
            }
        }

        private bool _timedout;
        public bool Timedout
        {
            get { return _timedout; }
            private set
            {
                if (_timedout == value)
                    return;
                _timedout = value;
                NotifyPropertyChanged();
            }
        }

        private SubscriberSocket socket;
        private NetMQPoller poller;
        private bool byteMode;
        private string address;
        private Timer failureTimer;

        public MonitorClient()
        {
            failureTimer = new Timer(5000);
            failureTimer.Elapsed += FailureTimer_Elapsed;
            failureTimer.AutoReset = true;

            Connected = false;
            AllReceived = "";
            socket = new SubscriberSocket();
            socket.ReceiveReady += Socket_ReceiveReady;

            poller = new NetMQPoller();
            poller.Add(socket);
        }

        private void FailureTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (Connected)
                socket.Disconnect(address);
            Connected = false;
            Timedout = true;
        }

        public void StartStream(string ip, int debug_level = 0, bool heartbeat = true)
        {
            Connected = false;
            Timedout = false;
            address = "tcp://" + ip + ":5535";
            socket.Connect(address);
            socket.SubscribeToAnyTopic();

            if (!poller.IsRunning)
                poller.RunAsync();

            failureTimer.Start();

            AllReceived = "";
            byteMode = debug_level == 0;

            string debug_arg = "-d " + debug_level.ToString();
            string heartbeat_arg = heartbeat ? " -h" : "";
        }

        public void Disconnect()
        {
            if (!Connected)
                return;

            Connected = false;
        }

        private void Socket_ReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            if (!Connected)
                Connected = true;
            List<byte[]> data = e.Socket.ReceiveMultipartBytes(2);

            //if (!byteMode)
            //{
            //    AllReceived += data_text;

            //    LineReceivedEventArgs line_e = new LineReceivedEventArgs();
            //    line_e.Line = data_text;

            //    LineReceivedEvent(this, line_e);
            //}

            if (data.Count != 2)
                return;

            int message_type_id = (byte)data[0][0];

            byte[] message_data = data[1];
            IMessage message = null;

            switch ((MessageType)message_type_id)
            {
                case MessageType.Heartbeat:
                    failureTimer.Stop();
                    failureTimer.Start();
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
                case MessageType.Telemetry:
                    message = Telemetry.Parser.ParseFrom(message_data);
                    break;
                case MessageType.FlightAnomaly:
                    message = FlightAnomaly.Parser.ParseFrom(message_data);
                    break;
                case MessageType.ACEError:
                    message = ACEError.Parser.ParseFrom(message_data);
                    break;
                case MessageType.MissionStatus:
                    message = MissionStatus.Parser.ParseFrom(message_data);
                    break;
                case MessageType.MissionConfig:
                    message = MissionConfig.Parser.ParseFrom(message_data);
                    break;
                case MessageType.CommandResponse:
                    message = CommandResponse.Parser.ParseFrom(message_data);
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
