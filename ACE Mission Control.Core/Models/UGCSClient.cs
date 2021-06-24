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

    public class ReceivedRoutesEventArgs : EventArgs
    {
        public List<Route> Routes { get; set; }
    }

    public class ReceivedMissionsEventArgs : EventArgs
    {
        public List<UGCS.Sdk.Protocol.Encoding.Mission> Missions { get; set; }
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
        public static event EventHandler<ReceivedRoutesEventArgs> ReceivedRoutesEvent;
        public static event EventHandler<ReceivedMissionsEventArgs> ReceivedMissionsEvent;

        public delegate void ProcessObject(DomainObjectWrapper obj, out object result);

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

        private static bool _requestingRoutes;
        public static bool RequestingRoutes
        {
            get { return _requestingRoutes; }
            private set
            {
                if (value == _requestingRoutes)
                    return;
                _requestingRoutes = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static bool _requestingMissions;
        public static bool RequestingMissions
        {
            get { return _requestingMissions; }
            private set
            {
                if (value == _requestingMissions)
                    return;
                _requestingMissions = value;
                NotifyStaticPropertyChanged();
            }
        }

        static UGCSClient()
        {
            IsConnected = false;
            TryingConnections = false;
            RequestingRoutes = false;
            RequestingMissions = false;
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

            DroneController.AlertAllDrones(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.UGCSStatus, $"Connected with Client ID ({clientID})"));

            return new ConnectionResult() { Success = true, Message = "Connected to UGCS" };
        }

        public static async Task<Waypoint> InsertWaypointAlongRoute(int routeID, string preceedingSegmentUuid, double longitude, double latitude)
        {
            // First get the route & preceeding segment
            DomainObjectWrapper obj = await RetrieveObjectAsync("Route", routeID);
            if (obj.Route == null)
                return null;

            var route = obj.Route;

            SegmentDefinition preceedingSegment = route.Segments.FirstOrDefault(s => s.Uuid == preceedingSegmentUuid);
            if (preceedingSegment == null)
                return null;

            // Then add the waypoint
            SegmentDefinition newSegment = new SegmentDefinition
            {
                Uuid = Guid.NewGuid().ToString(),
                AlgorithmClassName = "com.ugcs.ucs.service.routing.impl.WaypointAlgorithm"
            };
            newSegment.ParameterValues.AddRange(preceedingSegment.ParameterValues);
            newSegment.Figure = new Figure { Type = FigureType.FT_POINT };

            FigurePoint newPoint = preceedingSegment.Figure.Points[0].Clone();
            newPoint.Longitude = longitude;
            newPoint.Latitude = latitude;

            newSegment.Figure.Points.Add(newPoint);

            route.Segments.Insert(route.Segments.IndexOf(preceedingSegment) + 1, newSegment);

            // Then send an update request
            CreateOrUpdateObjectRequest request = new CreateOrUpdateObjectRequest()
            {
                ClientId = clientID,
                Object = new DomainObjectWrapper().Put(route, "Route"),
                WithComposites = true,
                ObjectType = "Route",
                AcquireLock = true
            };
            var response = await Task.Run(() => RequestAndWait<CreateOrUpdateObjectResponse>(request));

            var turnType = newSegment.ParameterValues.FirstOrDefault(p => p.Name == "wpTurnType")?.Value;
            return new Waypoint(newSegment.Uuid, turnType, longitude, latitude);
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

        public static async void RequestMissions()
        {
            if (RequestingMissions)
                return;
            RequestingMissions = true;

            // Request missions one by one because it's more reliable than requesting all missions at once apparently
            var missionID = 1;
            var allMissions = new List<UGCS.Sdk.Protocol.Encoding.Mission>();

            while (true)
            {
                GetObjectRequest request = new GetObjectRequest
                {
                    ClientId = clientID,
                    ObjectId = missionID,
                    ObjectType = "Mission"
                };

                var requestTask = Task.Run(() => RequestAndWait<GetObjectResponse>(request));
                var completedTask = await Task.WhenAny(requestTask, Task.Delay(5000));

                if (completedTask == requestTask)
                {
                    if (requestTask.Result != null)
                    {
                        allMissions.Add(requestTask.Result.Object.Mission);
                        DroneController.AlertAllDrones(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.UGCSStatus, $"Retrieved mission {missionID}"));
                        missionID++;
                    }
                    else
                    {
                        break;
                    }
                }
                else
                {
                    DroneController.AlertAllDrones(new AlertEntry(AlertEntry.AlertLevel.Info, AlertEntry.AlertType.UGCSStatus, $"Timed out retrieving mission {missionID}, continuing..."));
                    missionID++;
                }

                
            }

            ReceivedMissionsEvent?.Invoke(
                        null,
                        new ReceivedMissionsEventArgs() { Missions = allMissions });
            RequestingMissions = false;
        }


        public static void RequestRoutes(int missionID)
        {
            if (RequestingRoutes)
                return;
            RequestingRoutes = true;

            RetrieveAndProcessObject(
                "Mission",
                missionID,
                (missionObj) =>
                {
                    ReceivedRoutesEvent(
                        null,
                        new ReceivedRoutesEventArgs() { Routes = missionObj.Mission.Routes });
                    RequestingRoutes = false;
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

        private static Task<DomainObjectWrapper> RetrieveObjectAsync(string objectType, int objectID)
        {
            if (!IsConnected)
                throw new Exception($"Trying to retrieve {objectType} object from UGCS but UGCS is not connected.");

            GetObjectRequest request = new GetObjectRequest
            {
                ClientId = clientID,
                ObjectId = objectID,
                ObjectType = objectType
            };

            return Task.Run(() => RequestAndWait<GetObjectResponse>(request).Object);
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
