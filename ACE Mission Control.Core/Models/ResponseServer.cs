using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class ResponseServer
    {
        ResponseSocket socket;

        public ResponseServer(string connectString)
        {
            socket = new ResponseSocket(connectString);
            socket.ReceiveReady += Socket_ReceiveReady;
        }

        private void Socket_ReceiveReady(object sender, NetMQ.NetMQSocketEventArgs e)
        {
            
        }
    }
}
