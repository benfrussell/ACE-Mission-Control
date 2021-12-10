using NetMQ;
using NetMQ.Sockets;
using System;
using System.Threading;
using System.Threading.Tasks;

namespace ACE_Mission_Control.Core.Models
{
    public class ResponseReceivedEventArgs : EventArgs
    {
        public string Line { get; set; }
    }

    public class RequestClient : ACENetMQClient<RequestSocket>, IRequestClient
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

        private TaskCompletionSource<string> nextCommand;

        public RequestClient() : base(new RequestSocket())
        {
            ReadyForCommand = false;
        }

        protected override async void ClientRuntimeAsync(CancellationToken cancellationToken)
        {
            cancellationToken.Register(() => nextCommand?.TrySetCanceled());

            while (true)
            {
                if (!Connected)
                {
                    Socket.SendFrame("ping");
                }
                else
                {
                    nextCommand = new TaskCompletionSource<string>();
                    ReadyForCommand = true;
                    try
                    {
                        string command = await nextCommand.Task;
                        Socket.SendFrame(command);
                    }
                    catch (OperationCanceledException)
                    {
                        break;
                    }
                }

                ReadyForCommand = false;
                FailureTimer.Start();

                string response = "";

                while (!cancellationToken.IsCancellationRequested)
                {
                    if (!Socket.TryReceiveFrameString(out response))
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

                FailureTimer.Stop();

                if (!Connected)
                {
                    ConnectionInProgress = false;
                    Connected = true;
                }

                ResponseReceivedEventArgs response_e = new ResponseReceivedEventArgs();
                response_e.Line = response;
                ResponseReceivedEvent?.Invoke(this, response_e);
            }
        }

        public bool SendCommand(string command)
        {
            if (!ReadyForCommand || command == null)
                return false;

            try
            {
                nextCommand.SetResult(command);
            }
            catch (FiniteStateMachineException)
            {
                return false;
            }

            ReadyForCommand = false;
            FailureTimer.Start();

            return true;
        }

        protected override void OnDisconnect()
        {
            FailureTimer.Stop();
            Socket = new RequestSocket();
            ReadyForCommand = false;
        }
    }
}
