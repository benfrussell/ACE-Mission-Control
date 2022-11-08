using NetMQ;
using NetMQ.Sockets;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Runtime.InteropServices.ComTypes;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE_Mission_Control.Core.Models
{
    public enum ServerStatus
    {
        Stopped,
        Starting,
        Running,
        Failed
    }

    public interface IResponseServer
    {
        ServerStatus Status { get; }

        Task StartAsync(string connectionString, Func<string, string> requestHandler, Action<Exception> startFailureHandler);
        void Stop();
    }

    public class ResponseServer : IResponseServer
    {
        public ServerStatus Status { get; private set; }

        Func<string, string> requestHandler;
        Action<Exception> startFailureHandler;
        SynchronizationContext syncContext;
        NetMQPoller poller;

        public ResponseServer()
        {
            syncContext = new SynchronizationContext();
            Status = ServerStatus.Stopped;
        }

        public Task StartAsync(string connectionString, Func<string, string> requestHandler, Action<Exception> startFailureHandler = null)
        {
            if (Status == ServerStatus.Running)
                return Task.CompletedTask;

            this.requestHandler = requestHandler;
            this.startFailureHandler = startFailureHandler;
            Status = ServerStatus.Starting;

            _ = Task.Run(() => RunSocket(connectionString));

            return Task.Run(async () => { while (Status == ServerStatus.Starting) await Task.Delay(10); });
        }

        public void Stop()
        {
            if (poller != null)
                poller.Stop();
        }

        private void RunSocket(string connectionString)
        {
            var repSocket = new ResponseSocket();
            try
            {
                repSocket.Bind(connectionString);
            }
            catch (Exception e) when (e is SocketException || e is AddressAlreadyInUseException)
            {
                repSocket.Dispose();
                syncContext.Post(new SendOrPostCallback((_) => 
                {
                    if (startFailureHandler != null)
                        startFailureHandler(e);
                    Status = ServerStatus.Failed;
                }), null);
                return;
            }

            using (repSocket)
            using (poller = new NetMQPoller { repSocket })
            {
                repSocket.ReceiveReady += (sender, args) =>
                {
                    string request = args.Socket.ReceiveFrameString();
                    string response = "";
                    syncContext.Send(new SendOrPostCallback((_) => { response = requestHandler(request); }), null);
                    args.Socket.SendFrame(response);
                };
                syncContext.Post(new SendOrPostCallback((_) => { Status = ServerStatus.Running; }), null);
                poller.Run();
            }
        }
    }
}
