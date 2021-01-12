using System;
using System.Collections.Generic;
using System.Text;
using UGCS.Sdk.Protocol;
using UGCS.Sdk.Protocol.Encoding;
using UGCS.Sdk.Tasks;
using ProtoBuf;
using System.Timers;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Threading;
using System.Linq;

namespace ACE_Mission_Control.Core.Models
{
    public class ReceivedVehicleListEventArgs : EventArgs
    {
        public List<Vehicle> Vehicles { get; set; }
    }

    public class ReceivedRecentRoutesEventArgs : EventArgs
    {
        public List<Route> Routes { get; set; }
    }

    public class UGCSClient : INotifyPropertyChanged
    {
        public struct ConnectionResult
        {
            public bool Success;
            public string Message;
        }

        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        public static event EventHandler<ReceivedVehicleListEventArgs> ReceivedVehicleListEvent;
        public static event EventHandler<ReceivedRecentRoutesEventArgs> ReceivedRecentRoutesEvent;

        private static System.Timers.Timer connectTimer;
        private static TcpClient tcpClient;
        private static MessageSender messageSender;
        private static MessageReceiver messageReceiver;
        private static MessageExecutor messageExecutor;
        private static int clientID;
        private static SynchronizationContext syncContext;

        private static bool _isConnected;
        public static bool IsConnected
        {
            get { return _isConnected; }
            private set
            {
                if (value == _isConnected)
                    return;
                _isConnected = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static bool _tryingConnections;
        public static bool TryingConnections
        {
            get { return _tryingConnections; }
            private set
            {
                if (value == _tryingConnections)
                    return;
                _tryingConnections = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static string _connectionMessage;
        public static string ConnectionMessage
        {
            get { return _connectionMessage; }
            private set
            {
                if (value == _connectionMessage)
                    return;
                _connectionMessage = value;
                NotifyStaticPropertyChanged();
            }
        }

        static UGCSClient()
        {
            IsConnected = false;
            TryingConnections = false;
            ConnectionMessage = "Not connected to UGCS";
            syncContext = SynchronizationContext.Current;
        }

        public static void StartTryingConnections()
        {
            if (TryingConnections)
                return;
            TryingConnections = true;

            // Prepare the reconnect timer but start one connection attempt right away
            connectTimer = new System.Timers.Timer(3000);
            connectTimer.Elapsed += ConnectAsync;
            connectTimer.AutoReset = false;
            ConnectAsync();
        }

        public static async void ConnectAsync(Object source = null, ElapsedEventArgs args = null)
        {
            if (IsConnected)
                return;

            ConnectionMessage = "Attempting connection to UGCS";

            ConnectionResult result = await Task.Run(Connect);
            IsConnected = result.Success;
            ConnectionMessage = result.Message;

            if (!IsConnected)
                connectTimer.Start();
            else
                TryingConnections = false;
        }

        public static ConnectionResult Connect()
        {
            tcpClient = new TcpClient();
            try
            {
                tcpClient.Connect("localhost", 3334);
            }
            catch (System.Net.Sockets.SocketException e)
            {
                return new ConnectionResult() { Success = false, Message = $"Couldn't connect to UGCS ({e.Message})" };
            }


            messageSender = new MessageSender(tcpClient.Session);
            messageReceiver = new MessageReceiver(tcpClient.Session);
            messageExecutor = new MessageExecutor(messageSender, messageReceiver, new InstantTaskScheduler());
            messageExecutor.Configuration.DefaultTimeout = 5000;

            AuthorizeHciRequest authorizeRequest = new AuthorizeHciRequest
            {
                ClientId = -1,
                Locale = "en-US"
            };

            var authorizeResponse = messageExecutor.Submit<AuthorizeHciResponse>(authorizeRequest);
            if (authorizeResponse.Exception != null)
            {
                return new ConnectionResult() { Success = false, Message = authorizeResponse.Exception.Message };
            }

            clientID = authorizeResponse.Value.ClientId;

            LoginRequest loginRequest = new LoginRequest();
            loginRequest.UserLogin = "admin";
            loginRequest.UserPassword = "admin";
            loginRequest.ClientId = clientID;

            LoginResponse loginResponse = messageExecutor.Submit<LoginResponse>(loginRequest).Value;
            if (loginResponse == null || loginResponse.User == null)
            {
                return new ConnectionResult() { Success = false, Message = "Incorrect UGCS username or password" };
            }
            return new ConnectionResult() { Success = true, Message = "Connected to UGCS" };
        }

        public static void RequestVehicleList()
        {
            RetrieveAndProcessObjectList("Vehicle", (objects) => ReceivedVehicleListEvent(
                    null,
                    new ReceivedVehicleListEventArgs() { Vehicles = new List<Vehicle>(from obj in objects select obj.Vehicle) }
                )
            );
        }

        public static void GetLogs()
        {
            System.Diagnostics.Debug.WriteLine("Requesting up to 10 logs from the past hour.");
            GetVehicleLogRequest logRequest = new GetVehicleLogRequest();
            logRequest.ClientId = clientID;
            logRequest.Limit = 10;
            logRequest.FromTime = new DateTimeOffset(DateTime.Now).ToUnixTimeSeconds() * 1000;
            logRequest.FromTimeSpecified = true;
            logRequest.ReverseOrder = true;
            var logResponse = RequestAndWait<GetVehicleLogResponse>(logRequest);
            System.Diagnostics.Debug.WriteLine($"Client ID: {logRequest.ClientId}");
            System.Diagnostics.Debug.WriteLine($"From time: {logRequest.FromTime}");
            System.Diagnostics.Debug.WriteLine($"Logs received: {logResponse.VehicleLogEntries.Count}");
        }

        public static void RequestRecentMissionRoutes()
        {
            RetrieveAndProcessObjectList(
                "Route",
                (objects) =>
                {
                    IEnumerable<Route> routes = from obj in objects select obj.Route;
                    Route mostRecentRoute = routes.Aggregate((r1, r2) =>
                        r1.LastModificationTimeSpecified &&
                        r2.LastModificationTimeSpecified &&
                        r1.LastModificationTime > r1.LastModificationTime ? r1 : r2);

                    RetrieveAndProcessObject(
                        "Mission",
                        mostRecentRoute.Mission.Id,
                        (missionObj) => ReceivedRecentRoutesEvent(
                            null,
                            new ReceivedRecentRoutesEventArgs() { Routes = missionObj.Mission.Routes }));
                });
        }

        private static async void RetrieveAndProcessObjectList(string objectType, Action<List<DomainObjectWrapper>> processCallback)
        {
            if (!IsConnected)
                throw new Exception($"Trying to retrieve {objectType} object list from UGCS but UGCS is not connected.");

            GetObjectListRequest request = new GetObjectListRequest
            {
                ClientId = clientID,
                ObjectType = objectType
            };

            var response = await Task.Run(() => RequestAndWait<GetObjectListResponse>(request));
            
            if (response != null)
                syncContext.Post(
                    new SendOrPostCallback((_) => processCallback(response.Objects)),
                    null
                );
        }

        private static async void RetrieveAndProcessObject(string objectType, int objectID, Action<DomainObjectWrapper> processCallback)
        {
            if (!IsConnected)
                throw new Exception($"Trying to retrieve {objectType} object from UGCS but UGCS is not connected.");

            GetObjectRequest request = new GetObjectRequest
            {
                ClientId = clientID,
                ObjectId = objectID,
                ObjectType = objectType
            };

            var response = await Task.Run(() => RequestAndWait<GetObjectResponse>(request));

            if (response != null)
                syncContext.Post(
                    new SendOrPostCallback((_) => processCallback(response.Object)),
                    null
                );
        }

        private static void HandleDisconnect()
        {
            IsConnected = false;
            StartTryingConnections();
        }

        private static T RequestAndWait<T>(IExtensible request) where T : IExtensible
        {
            var execution = messageExecutor.Submit<T>(request);
            execution.Wait();
            if (execution.Exception is SessionDisconnectedException)
                syncContext.Post(
                    new SendOrPostCallback((_) => HandleDisconnect()),
                    null
                );
            return execution.Value;
        }

        private static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = "")
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }
    }
}
