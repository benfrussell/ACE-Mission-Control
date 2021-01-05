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

namespace ACE_Mission_Control.Core.Models
{
    public class VehicleModificationEventArgs : EventArgs
    {
        public ModificationType Modification { get; set; }
        public Vehicle Vehicle { get; set; }
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

        public static event EventHandler<VehicleModificationEventArgs> VehicleModificationEvent;

        private static System.Timers.Timer connectTimer;
        private static TcpClient tcpClient;
        private static MessageSender messageSender;
        private static MessageReceiver messageReceiver;
        private static MessageExecutor messageExecutor;
        private static int clientID;
        private static UGCSVehicleListener vehicleListener;
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
            connectTimer.AutoReset = true;
            ConnectAsync();
        }

        public static async void ConnectAsync(Object source = null, ElapsedEventArgs args = null)
        {
            if (IsConnected)
                return;

            connectTimer.Stop();
            ConnectionMessage = "Attempting connection to UGCS";

            try
            {
                ConnectionResult result = await Task.Run(Connect);
                IsConnected = result.Success;
                ConnectionMessage = result.Message;
            }
            catch (Exception e)
            {
                IsConnected = false;
                ConnectionMessage = e.Message;
                System.Diagnostics.Debug.WriteLine(e);
            }

            if (!IsConnected)
                connectTimer.Start();
            else
                TryingConnections = false;
        }

        public static ConnectionResult Connect()
        {
            tcpClient = new TcpClient();
            tcpClient.Connect("localhost", 3334);

            messageSender = new MessageSender(tcpClient.Session);
            messageReceiver = new MessageReceiver(tcpClient.Session);
            messageExecutor = new MessageExecutor(messageSender, messageReceiver, new InstantTaskScheduler());
            messageExecutor.Configuration.DefaultTimeout = 10000;
            //var notificationListener = new NotificationListener();
            //messageReceiver.AddListener(-1, notificationListener);

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
            GetObjectListRequest vehiclesRequest = new GetObjectListRequest();
            vehiclesRequest.ClientId = clientID;
            vehiclesRequest.ObjectType = "Vehicle";
            var vehiclesResponse = RequestAndWait<GetObjectListResponse>(vehiclesRequest);

            foreach (var v in vehiclesResponse.Objects)
            {
                syncContext.Post(new SendOrPostCallback((_) => 
                    VehicleModificationEvent(null, new VehicleModificationEventArgs()
                        {
                            Modification = ModificationType.MT_CREATE,
                            Vehicle = v.Vehicle
                        }
                    )),
                    null
                );
            }
        }

        private static void StartReceivingVehicleUpdates()
        {
            GetObjectListRequest vehiclesRequest = new GetObjectListRequest();
            vehiclesRequest.ClientId = clientID;
            vehiclesRequest.ObjectType = "Vehicle";
            var vehiclesResponse = RequestAndWait<GetObjectListResponse>(vehiclesRequest);

            vehicleListener = new UGCSVehicleListener(new EventSubscriptionWrapper(), clientID, messageExecutor, new NotificationListener());

            foreach (var v in vehiclesResponse.Objects)
            {
                VehicleModificationEvent(null, new VehicleModificationEventArgs()
                {
                    Modification = ModificationType.MT_CREATE,
                    Vehicle = v.Vehicle
                }
                );
                var objModSubs = new ObjectModificationSubscription();
                objModSubs.ObjectId = v.Vehicle.Id;
                vehicleListener.SubscribeVehicle(objModSubs, vehicleCallback);
            }
        }

        private static void vehicleCallback(ModificationType modification, Vehicle vehicle)
        {
            System.Diagnostics.Debug.WriteLine("Vehicle change!");
        }

        // Make a request expecting a response of type T
        private static T RequestAndWait<T>(IExtensible request) where T : IExtensible
        {
            var execution = messageExecutor.Submit<T>(request);
            execution.Wait();
            if (execution.Exception != null)
                throw execution.Exception;
            return execution.Value;
        }

        private static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = "")
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }
    }
}
