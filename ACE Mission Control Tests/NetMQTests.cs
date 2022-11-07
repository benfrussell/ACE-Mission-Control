using ACE_Mission_Control.Core.Models;
using NetMQ;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Xunit;

namespace ACE_Mission_Control_Tests
{
    public class NetMQTests
    {
        [Fact]
        public async void ResponseServer_Running_With_Valid_Host()
        {
            ResponseServer server = new ResponseServer();
            await server.StartAsync("tcp://localhost:5540", (request) => { return ""; });
            Assert.Equal(ServerStatus.Running, server.Status);
        }

        [Fact]
        public async void ResponseServer_Does_Not_Call_Failure_Handler_With_Valid_Host()
        {
            ResponseServer server = new ResponseServer();
            bool failureHandlerCalled = false;
            Exception ex;

            await server.StartAsync("tcp://localhost:5541", (request) => { return ""; }, (e) => { failureHandlerCalled = true; ex = e; });

            Assert.False(failureHandlerCalled);
        }

        [Fact]
        public async void ResponseServer_Failed_With_Invalid_Host()
        {
            ResponseServer server = new ResponseServer();
            await server.StartAsync("tcp://notarealhost:0", (request) => { return ""; });
            Assert.Equal(ServerStatus.Failed, server.Status);
        }

        [Fact]
        public async void ResponseServer_Calls_Failure_Handler_With_Invalid_Host()
        {
            ResponseServer server = new ResponseServer();
            Exception failedException = null;

            await server.StartAsync("tcp://notarealhost:0", (request) => { return ""; }, (e) => { failedException = e; });

            Assert.NotNull(failedException);
            Assert.Equal(typeof(SocketException), failedException.GetType());
        }

        [Fact]
        public async void ResponseServer_Failed_With_Busy_Address()
        {
            ResponseServer server1 = new ResponseServer();
            await server1.StartAsync("tcp://localhost:5542", (request) => { return ""; });

            ResponseServer server2 = new ResponseServer();
            await server2.StartAsync("tcp://localhost:5542", (request) => { return ""; });

            Assert.Equal(ServerStatus.Failed, server2.Status);
        }

        [Fact]
        public async void ResponseServer_Calls_Failure_Handler_With_Busy_Address()
        {
            Exception failedException = null;

            ResponseServer server1 = new ResponseServer();
            await server1.StartAsync("tcp://localhost:5543", (request) => { return ""; });

            ResponseServer server2 = new ResponseServer();
            await server2.StartAsync("tcp://localhost:5543", (request) => { return ""; }, (e) => { failedException = e; });

            Assert.NotNull(failedException);
            Assert.Equal(typeof(AddressAlreadyInUseException), failedException.GetType());
        }

        [Fact]
        public async void RequestClient_Connected_With_Valid_Host()
        {
            ResponseServer server = new ResponseServer();
            await server.StartAsync("tcp://localhost:5544", (request) => { return ""; });

            RequestClient client = new RequestClient();
            client.TryConnection("localhost", "5544");
            await Task.Run(async () => { while (client.ConnectionInProgress) await Task.Delay(10); });
            Assert.True(client.Connected);
        }

        [Fact]
        public async void RequestClient_Not_Connected_With_Invalid_Host()
        {
            RequestClient client = new RequestClient();
            client.TryConnection("notarealhost", "0");
            await Task.Run(async () => { while (client.ConnectionInProgress) await Task.Delay(10); });
            Assert.False(client.Connected);
        }

        [Fact]
        public async void ResponseServer_Sends_Response()
        {
            ResponseServer server = new ResponseServer();
            await server.StartAsync("tcp://localhost:5545", (request) => { return "I'm up!"; });

            bool responseReceived = false;

            RequestClient client = new RequestClient();
            client.ResponseReceivedEvent += (sender, e) => { responseReceived = true; };
            client.TryConnection("localhost", "5545");
            await Task.Run(async () => { while (client.ConnectionInProgress) await Task.Delay(10); });

            client.SendCommand("You up?");

            var waitForResponseTask = Task.Run(async () => { while (!responseReceived) await Task.Delay(10); });
            await Task.WhenAny(waitForResponseTask, Task.Delay(3000));

            Assert.True(responseReceived);
        }
    }
}
