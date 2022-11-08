using Pbdrone;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE_Mission_Control.Core.Models
{
    public enum ServiceStatus : int
    {
        NotRunning,
        Starting,
        RunningDatabaseNotReachable,
        RunningNoUgCSConnection,
        RunningDatabaseConnectionRefused,
        Running
    }

    public interface IDashboardServiceMonitor
    {
        ServiceStatus Status { get; }

        event PropertyChangedEventHandler PropertyChanged;

        Task<bool> StartAsync();
    }

    public class DashboardServiceMonitor : INotifyPropertyChanged, IDashboardServiceMonitor
    {
        private ServiceStatus status;
        public ServiceStatus Status
        {
            get { return status; }
            private set
            {
                if (status == value)
                    return;
                status = value;
                NotifyPropertyChanged();
            }
        }

        RequestClient requestClient;
        bool awaitingStatusUpdate;
        bool attemptConnection;
        System.Timers.Timer attemptConnectionTimer;
        System.Timers.Timer statusRequestTimer;

        public event PropertyChangedEventHandler PropertyChanged;

        public DashboardServiceMonitor()
        {
            requestClient = new RequestClient();
            requestClient.ResponseReceivedEvent += RequestClient_ResponseReceivedEvent;
            requestClient.PropertyChanged += RequestClient_PropertyChanged;

            statusRequestTimer = new System.Timers.Timer();
            statusRequestTimer.Elapsed += StatusRequestTimer_Elapsed;
            statusRequestTimer.Interval = 3000;
            statusRequestTimer.AutoReset = false;

            attemptConnectionTimer = new System.Timers.Timer();
            attemptConnectionTimer.Elapsed += AttemptConnectionTimer_Elapsed;
            attemptConnectionTimer.Interval = 3000;
            attemptConnectionTimer.AutoReset = false;

            Status = ServiceStatus.NotRunning;

            awaitingStatusUpdate = false;
            attemptConnection = false;
        }

        private void AttemptConnectionTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            _ = StartAsync();
        }

        private void StatusRequestTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            requestClient.SendCommand("status");
            awaitingStatusUpdate = true;
        }

        private void RequestClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Connected" && !requestClient.Connected)
            {
                if (statusRequestTimer.Enabled)
                    statusRequestTimer.Stop();
                Status = ServiceStatus.NotRunning;
                if (attemptConnection)
                    attemptConnectionTimer.Start();
            }
        }

        private void RequestClient_ResponseReceivedEvent(object sender, ResponseReceivedEventArgs e)
        {
            int parsedStatusInt;
            if (int.TryParse(e.Line, out parsedStatusInt))
                StatusUpdateReceived((ServiceStatus)parsedStatusInt);
        }

        private void StatusUpdateReceived(ServiceStatus newStatus)
        {
            if (awaitingStatusUpdate)
            {
                Status = newStatus;
                statusRequestTimer.Start();
                awaitingStatusUpdate = false;
            }
        }

        public Task<bool> StartAsync()
        {
            awaitingStatusUpdate = false;
            requestClient.TryConnection("localhost", "5538");
            return Task.Run(async () =>
            {
                while (requestClient.ConnectionInProgress)
                    await Task.Delay(10);
                if (requestClient.Connected)
                    statusRequestTimer.Start();
                else if (attemptConnection)
                    attemptConnectionTimer.Start();
                return requestClient.Connected;
            });
        }

        public void StartConnectionAttempts()
        {
            attemptConnection = true;
            _ = StartAsync();
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
