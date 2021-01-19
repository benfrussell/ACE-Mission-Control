using System;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using static ACE_Mission_Control.Core.Models.ACEEnums;
using System.Timers;
using Pbdrone;

namespace ACE_Mission_Control.Core.Models
{
    public class OnboardComputerClient : INotifyPropertyChanged
    {
        private string _hostname = "";
        public string Hostname
        {
            get { return _hostname; }
            private set
            {
                if (_hostname == value)
                    return;
                _hostname = value;
                NotifyPropertyChanged();
            }
        }

        public bool IsConfigured
        {
            get { return Hostname != null && Hostname.Length > 0; }
        }

        public bool IsDirectorConnected
        {
            get { return DirectorRequestClient.Connected && DirectorMonitorClient.Connected; }
        }

        public bool IsChaperoneConnected
        {
            get { return ChaperoneRequestClient.Connected; }
        }

        private bool _autoTryingConnections;
        public bool AutoTryingConnections
        {
            get { return _autoTryingConnections; }
            private set
            {
                if (value == _autoTryingConnections)
                    return;
                _autoTryingConnections = value;
                NotifyPropertyChanged();
            }
        }

        public bool ConnectionInProgress
        {
            get { return ChaperoneRequestClient.ConnectionInProgress ||
                    DirectorRequestClient.ConnectionInProgress ||
                    DirectorMonitorClient.ConnectionInProgress; }
        }

        // The reattempt timer is only started if AutoTryConnections is on
        // If AutoTryConnections is on, the timer is triggered by a connection failure when no other attempts are currently in progress
        // It will also be started after a manual disconnect
        private Timer reattemptTimer;

        public event PropertyChangedEventHandler PropertyChanged;
        public Drone AttachedDrone;

        public SubscriberClient DirectorMonitorClient;
        public RequestClient DirectorRequestClient;
        public RequestClient ChaperoneRequestClient;

        public OnboardComputerClient(Drone parentDrone, string hostname)
        {
            AttachedDrone = parentDrone;
            Hostname = hostname;
            AutoTryingConnections = false;

            reattemptTimer = new Timer(3000);
            reattemptTimer.Elapsed += Connect;
            reattemptTimer.AutoReset = false;

            DirectorMonitorClient = new SubscriberClient();
            DirectorMonitorClient.PropertyChanged += DirectorMonitorClient_PropertyChanged;
            DirectorMonitorClient.MessageReceivedEvent += DirectorMonitorClient_MessageReceivedEvent;

            DirectorRequestClient = new RequestClient();
            DirectorRequestClient.PropertyChanged += DirectorRequestClient_PropertyChanged;

            ChaperoneRequestClient = new RequestClient();
            ChaperoneRequestClient.PropertyChanged += ChaperoneRequestClient_PropertyChanged;
        }

        public void Configure(string hostname)
        {
            Hostname = hostname;
            NotifyPropertyChanged("Hostname");
            NotifyPropertyChanged("IsConfigured");
            if (IsDirectorConnected || IsChaperoneConnected)
                Disconnect();
            else if (AutoTryingConnections)
                Connect();
        }

        public void StartTryingConnections()
        {
            if (AutoTryingConnections)
                return;
            AutoTryingConnections = true;

            Connect();
        }

        public void StopTryingConnections()
        {
            if (!AutoTryingConnections)
                return;
            AutoTryingConnections = false;

            reattemptTimer.Stop();
        }

        public void Connect(Object source = null, ElapsedEventArgs args = null)
        {
            if (DirectorMonitorClient.Connected)
            {
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionReady));
                return;
            }

            if (!IsConfigured)
            {
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.NoConnectionNotConfigured), true);
                return;
            }

            if (!ChaperoneRequestClient.Connected && !ChaperoneRequestClient.ConnectionInProgress)
                ChaperoneRequestClient.TryConnection(Hostname, "5537");

            if (!DirectorRequestClient.Connected && !DirectorRequestClient.ConnectionInProgress)
                DirectorRequestClient.TryConnection(Hostname, "5536");

            // Only try a monitor connection if the request client isn't already making an attempt
            // Both clients communicate with the same program, so we only need to try with one to start
            // If the request client succeeds it will move on to connect the monitor client 
            if (!DirectorRequestClient.ConnectionInProgress && !DirectorMonitorClient.Connected && !DirectorMonitorClient.ConnectionInProgress)
                DirectorMonitorClient.TryConnection(Hostname, "5535");

            if (ConnectionInProgress)
                NotifyPropertyChanged("ConnectionInProgress");
        }

        private void ChaperoneRequestClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Connected" && ChaperoneRequestClient.Connected)
            {
                NotifyPropertyChanged("IsChaperoneConnected");
            }
            else if (e.PropertyName == "ConnectionFailure" && ChaperoneRequestClient.ConnectionFailure)
            {
                ClientConnectionFailed();
            }
        }

        private void DirectorRequestClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Connected")
            {
                NotifyPropertyChanged("IsDirectorConnected");
                if (DirectorRequestClient.Connected)
                {
                    if (!DirectorMonitorClient.Connected)
                    {
                        AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionStarting));
                        DirectorMonitorClient.TryConnection(Hostname, "5535");
                    }
                    else
                    {
                        AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionReady));
                    }
                        
                }
            }
            else if (e.PropertyName == "ConnectionFailure" && DirectorRequestClient.ConnectionFailure)
            {
                ClientConnectionFailed();
            }
        }

        private void DirectorMonitorClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Connected")
            {
                NotifyPropertyChanged("IsDirectorConnected");
                if (DirectorMonitorClient.Connected)
                    AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionReady));
            }
            else if (e.PropertyName == "ConnectionFailure" && DirectorMonitorClient.ConnectionFailure)
            {
                ClientConnectionFailed();
            }
        }

        private void ClientConnectionFailed()
        {
            // Only perform failure functions if no other clients are still trying to connect
            if (!ConnectionInProgress)
            {
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.ConnectionTimedOut));
                NotifyPropertyChanged("ConnectionInProgress");
                if (AutoTryingConnections)
                    reattemptTimer.Start();
            }
        }

        private void DirectorMonitorClient_MessageReceivedEvent(object sender, MessageReceivedEventArgs e)
        {
            if (e.MessageType == MessageType.Heartbeat)
            {
                Heartbeat heartbeat = (Heartbeat)e.Message;
                if (heartbeat.Arrhythmia > 0)
                    AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.OBCSlow, heartbeat.Arrhythmia.ToString()));
            }
            else if (e.MessageType == MessageType.ACEError)
            {
                ACEError error = (ACEError)e.Message;
                AttachedDrone.AddAlert(new AlertEntry(AlertEntry.AlertLevel.High, AlertEntry.AlertType.OBCError, error.Timestamp + ": " + error.Error));
            }
        }

        public void Disconnect()
        {
            DirectorMonitorClient.Disconnect();
            DirectorRequestClient.Disconnect();
            ChaperoneRequestClient.Disconnect();
            if (AutoTryingConnections)
                reattemptTimer.Start();
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
