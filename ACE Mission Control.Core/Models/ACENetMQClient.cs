using NetMQ;
using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading;
using System.Timers;

namespace ACE_Mission_Control.Core.Models
{
    public abstract class ACENetMQClient<T> : INotifyPropertyChanged where T : NetMQSocket, IReceivingSocket
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private bool _connected;
        public bool Connected
        {
            get { return _connected; }
            protected set
            {
                if (_connected == value)
                    return;
                _connected = value;
                NotifyPropertyChanged();
            }
        }

        private bool _connectionInProgress;
        public bool ConnectionInProgress
        {
            get { return _connectionInProgress; }
            protected set
            {
                if (_connectionInProgress == value)
                    return;
                _connectionInProgress = value;
                NotifyPropertyChanged();
            }
        }

        private bool _connectionFailure;
        public bool ConnectionFailure
        {
            get { return _connectionFailure; }
            protected set
            {
                if (_connectionFailure == value)
                    return;
                _connectionFailure = value;
                NotifyPropertyChanged();
            }
        }

        protected T Socket;
        protected NetMQPoller Poller;
        protected string Address;
        protected System.Timers.Timer FailureTimer;

        private SynchronizationContext syncContext;

        public ACENetMQClient(T socket, int timeoutMsec = 5000)
        {
            Socket = socket;
            Socket.ReceiveReady += Socket_ReceiveReady;

            FailureTimer = new System.Timers.Timer(timeoutMsec);
            FailureTimer.Elapsed += FailureTimer_Elapsed;
            FailureTimer.AutoReset = false;

            ConnectionFailure = false;
            Connected = false;

            syncContext = SynchronizationContext.Current;
        }

        private void FailureTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            syncContext.Post(
                new SendOrPostCallback(
                    (_) =>
                    {
                        Disconnect();
                        ConnectionFailure = true;
                    }),
                null
            );
        }

        public void TryConnection(string ip, string port)
        {
            if (ConnectionInProgress)
                return;

            if (Connected)
                Disconnect();

            ConnectionInProgress = true;
            ConnectionFailure = false;
            Address = "tcp://" + ip + ":" + port;
            Socket.Connect(Address);

            // Need to recreate the poller each time for some reason
            Poller = new NetMQPoller();
            Poller.Add(Socket);
            Poller.RunAsync();

            OnTryConnection();
        }

        protected abstract void OnTryConnection();

        private void Socket_ReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            object socketThreadResult = OnReceiveReady_SocketThread(e);
            syncContext.Post(new SendOrPostCallback((_) => OnReceiveReady_MainThread(socketThreadResult)), null);
        }

        protected abstract object OnReceiveReady_SocketThread(NetMQSocketEventArgs e);

        protected abstract void OnReceiveReady_MainThread(object socketThreadResult);

        public void Disconnect()
        {
            if (!Connected && !ConnectionInProgress)
                return;

            Socket.Disconnect(Address);
            if (Poller.IsRunning)
                Poller.StopAsync();

            ConnectionInProgress = false;
            Connected = false;

            OnDisconnect();
        }

        protected abstract void OnDisconnect();

        protected void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
