using System;
using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface ISubscriberClient : IACENetMQClient
    {
        string AllReceived { get; set; }

        event EventHandler<LineReceivedEventArgs> LineReceivedEvent;
        event EventHandler<MessageReceivedEventArgs> MessageReceivedEvent;
    }
}