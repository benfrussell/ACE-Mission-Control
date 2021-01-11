using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text;
using System.Timers;
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

        private bool _connectionFailure;
        public bool ConnectionFailure
        {
            get { return _connectionFailure; }
            private set
            {
                if (_connectionFailure == value)
                    return;
                _connectionFailure = value;
                NotifyPropertyChanged();
            }
        }

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

        private string address;
        private RequestSocket socket;
        private NetMQPoller poller;
        private Timer failureTimer;
        private bool commandSent;

        public CommanderClient()
        {
            failureTimer = new Timer(5000);
            failureTimer.Elapsed += FailureTimer_Elapsed;
            failureTimer.AutoReset = true;

            ConnectionFailure = false;
            Connected = false;
            ReadyForCommand = false;
            socket = new RequestSocket();
            socket.SendReady += Socket_SendReady;
            socket.ReceiveReady += Socket_ReceiveReady;
            poller = new NetMQPoller();
            poller.Add(socket);
        }

        private void FailureTimer_Elapsed(object sender, ElapsedEventArgs e)
        {
            if (!Connected)
            {
                ConnectionFailure = true;
            }
            else if (commandSent)
            {
                ConnectionFailure = true;
                socket.Disconnect(address);
            }
        }

        public void StartStream(string ip)
        {
            ConnectionFailure = false;
            Connected = false;
            address = "tcp://" + ip + ":5536";
            failureTimer.Stop();
            failureTimer.Start();
            socket.Connect(address);
            if (!poller.IsRunning)
                poller.Run();
        }

        private void Socket_SendReady(object sender, NetMQSocketEventArgs e)
        {
            if (!Connected)
            {
                commandSent = true;
                ReadyForCommand = false;
                socket.TrySendFrame("ping");
            }
            else
            {
                ReadyForCommand = true;
            }
        }

        public bool SendCommand(string command)
        {
            if (!ReadyForCommand || command == null)
                return false;
            ReadyForCommand = false;
            failureTimer.Stop();
            failureTimer.Start();
            commandSent = true;
            try
            {
                socket.SendFrame(command);
            }
            catch (FiniteStateMachineException)
            {
                return false;
            }
            return true;
        }

        public void Disconnect()
        {
            if (!Connected)
                return;

            Connected = false;
            ReadyForCommand = false;
        }

        private void Socket_ReceiveReady(object sender, NetMQSocketEventArgs e)
        {
            string data_text = e.Socket.ReceiveFrameString();

            if (!Connected)
                Connected = true;

            commandSent = false;

            ResponseReceivedEventArgs response_e = new ResponseReceivedEventArgs();
            response_e.Line = data_text;
            ResponseReceivedEvent(this, response_e);
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
