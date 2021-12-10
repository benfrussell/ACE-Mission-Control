using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface IOnboardComputerClient
    {
        bool AutoTryingConnections { get; }
        bool ConnectionInProgress { get; }
        string Hostname { get; }
        bool IsChaperoneConnected { get; }
        bool IsConfigured { get; }
        bool IsDirectorConnected { get; }
        ISubscriberClient DirectorMonitorClient { get; }
        IRequestClient DirectorRequestClient { get; }
        IRequestClient ChaperoneRequestClient { get; }

        event PropertyChangedEventHandler PropertyChanged;

        void Configure(string hostname);
        void Connect();
        void Disconnect();
        void StartTryingConnections();
        void StopTryingConnections();
    }
}