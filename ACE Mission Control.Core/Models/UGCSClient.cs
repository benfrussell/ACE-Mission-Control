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

namespace ACE_Mission_Control.Core.Models
{
    public class UGCSClient : INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private static Timer connectTimer;
        private static TcpClient tcpClient;
        private static MessageSender messageSender;
        private static MessageReceiver messageReceiver;
        private static MessageExecutor messageExecutor;
        private static int clientID;

        private static bool _isConnected;
        public static bool IsConnected
        {
            get { return _isConnected; }
            set
            {
                if (value == _isConnected)
                    return;
                _isConnected = value;
                NotifyStaticPropertyChanged();
            }
        }

        public static void StartTryingConnections()
        {
            connectTimer = new Timer(5000);
            // Hook up the Elapsed event for the timer.
            connectTimer.Elapsed += Connect;
            connectTimer.AutoReset = true;
            connectTimer.Enabled = true;
        }

        public static void Connect(Object source, ElapsedEventArgs e)
        {
            if (IsConnected)
                return;

            tcpClient = new TcpClient();
            tcpClient.Connect("localhost", 3334);
            messageSender = new MessageSender(tcpClient.Session);
            messageReceiver = new MessageReceiver(tcpClient.Session);
            messageExecutor = new MessageExecutor(messageSender, messageReceiver, new InstantTaskScheduler());
            messageExecutor.Configuration.DefaultTimeout = 10000;

            var notificationListener = new NotificationListener();
            messageReceiver.AddListener(-1, notificationListener);

            AuthorizeHciRequest authorizeRequest = new AuthorizeHciRequest();
            authorizeRequest.ClientId = -1;
            authorizeRequest.Locale = "en-US";

            var authorizeResponse = RequestAndWait<AuthorizeHciResponse>(authorizeRequest);
            clientID = authorizeResponse.ClientId;
            System.Diagnostics.Debug.WriteLine("UGCS ID: " + clientID.ToString());

            LoginRequest loginRequest = new LoginRequest();
            loginRequest.UserLogin = "admin";
            loginRequest.UserPassword = "admin";
            loginRequest.ClientId = clientID;

            LoginResponse loginResponse = RequestAndWait<LoginResponse>(loginRequest);

            IsConnected = true;
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
