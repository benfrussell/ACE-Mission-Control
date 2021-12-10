using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface IACENetMQClient
    {
        bool Connected { get; }
        bool ConnectionFailure { get; }
        bool ConnectionInProgress { get; }

        event PropertyChangedEventHandler PropertyChanged;

        void Disconnect();
        void TryConnection(string ip, string port);
    }
}