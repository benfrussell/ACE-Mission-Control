using System;
using Google.Protobuf;
using static ACE_Mission_Control.Core.Models.ACEEnums;
using Pbdrone;
using NetMQ.Sockets;
using NetMQ;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Threading;

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

    public class SubscriberClient : ACENetMQClient<SubscriberSocket>
    {
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

        public SubscriberClient() : base(new SubscriberSocket())
        {
            AllReceived = "";
        }

        protected override void OnDisconnect()
        {
            FailureTimer.Stop();
        }

        protected override async void ClientRuntimeAsync(CancellationToken cancellationToken)
        {
            AllReceived = "";
            Socket.SubscribeToAnyTopic();
            FailureTimer.Start();

            while (true)
            {
                List<byte[]> data = new List<byte[]>();

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!Socket.TryReceiveMultipartBytes(ref data, 2))
                    {
                        try
                        {
                            await Task.Delay(100, cancellationToken);
                        }
                        catch (OperationCanceledException)
                        {
                            break;
                        }
                    }
                    else
                    {
                        break;
                    }
                }

                if (cancellationToken.IsCancellationRequested)
                    break;

                if (data.Count != 2)
                    continue;

                int message_type_id = (byte)data[0][0];

                byte[] message_data = data[1];
                IMessage message = null;

                switch ((MessageType)message_type_id)
                {
                    case MessageType.Heartbeat:
                        FailureTimer.Stop();
                        FailureTimer.Start();
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
                    if (!Connected)
                    {
                        FailureTimer.Stop();
                        ConnectionInProgress = false;
                        Connected = true;
                    }

                    var messageArgs = new MessageReceivedEventArgs()
                    {
                        Message = message,
                        MessageType = (MessageType)message_type_id
                    };
                    MessageReceivedEvent(this, messageArgs);
                }
            }
        }
    }
}
