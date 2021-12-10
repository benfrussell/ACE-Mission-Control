using System;
using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface IRequestClient : IACENetMQClient
    {
        bool ReadyForCommand { get; }

        event EventHandler<ResponseReceivedEventArgs> ResponseReceivedEvent;

        bool SendCommand(string command);
    }
}