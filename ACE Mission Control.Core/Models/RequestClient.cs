using NetMQ;
using NetMQ.Sockets;
using System;
using System.Threading;

namespace ACE_Mission_Control.Core.Models
{
    public class ResponseReceivedEventArgs : EventArgs
    {
        public string Line { get; set; }
    }

    public class RequestClient : ACENetMQClient<RequestSocket>
    {
        public event EventHandler<ResponseReceivedEventArgs> ResponseReceivedEvent;

        private readonly object readyForCommandLock = new object();

        private bool _readyForCommand;
        public bool ReadyForCommand
        {
            get
            {
                lock (readyForCommandLock)
                {
                    return _readyForCommand;
                }
            }
            private set
            {
                lock (readyForCommandLock)
                {
                    if (_readyForCommand == value)
                        return;
                    _readyForCommand = value;
                    NotifyPropertyChanged();
                }
            }
        }

        public RequestClient() : base(new RequestSocket())
        {
            ReadyForCommand = false;
            Socket.SendReady += Socket_SendReady;
        }

        protected override void OnTryConnection()
        {
            ReadyForCommand = false;
            FailureTimer.Start();
        }

        private void Socket_SendReady(object sender, NetMQSocketEventArgs e)
        {
            // TODO: Pinging twice for some reason but only with the director port (5536)
            if (!Connected)
                Socket.TrySendFrame("ping");
        }

        public bool SendCommand(string command)
        {
            if (!ReadyForCommand || command == null)
                return false;

            try
            {
                Socket.SendFrame(command);
            }
            catch (FiniteStateMachineException)
            {
                return false;
            }

            ReadyForCommand = false;
            FailureTimer.Start();

            return true;
        }

        protected override object OnReceiveReady_SocketThread(NetMQSocketEventArgs e)
        {
            return e.Socket.ReceiveFrameString();
        }

        protected override void OnReceiveReady_MainThread(object socketThreadResult)
        {
            FailureTimer.Stop();

            if (!Connected)
            {
                ConnectionInProgress = false;
                Connected = true;
            }

            ResponseReceivedEventArgs response_e = new ResponseReceivedEventArgs();
            response_e.Line = (string)socketThreadResult;
            ResponseReceivedEvent?.Invoke(this, response_e);
            ReadyForCommand = true;
        }

        protected override void OnDisconnect()
        {
            FailureTimer.Stop();
            ReadyForCommand = false;
        }
    }
}
