using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ACE_Mission_Control.Core.Models
{
    public class IPLookup : INotifyPropertyChanged
    {
        public static event PropertyChangedEventHandler StaticPropertyChanged;
        public event PropertyChangedEventHandler PropertyChanged;

        private static ObservableCollection<Tuple<string, string>> entriesFound;
        public static ObservableCollection<Tuple<string, string>> EntriesFound
        {
            get { return entriesFound; }
            private set
            {
                if (value == entriesFound)
                    return;
                entriesFound = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static bool searching;
        public static bool Searching
        {
            get { return searching; }
            private set
            {
                if (value == searching)
                    return;
                searching = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static int progress;
        public static int Progress
        {
            get { return progress; }
            private set
            {
                if (value == progress)
                    return;
                progress = value;
                NotifyStaticPropertyChanged();
            }
        }

        private static ManualResetEventSlim connectFinished = new ManualResetEventSlim();
        private static Socket testSocket;
        private static SocketAsyncEventArgs socketEvent;

        static IPLookup()
        {
            Searching = false;
            EntriesFound = new ObservableCollection<Tuple<string, string>>();
            Progress = 0;
        }

        public static async void LookupIPs(string matchingHostname)
        {
            Searching = true;
            EntriesFound.Clear();
            connectFinished.Reset();

            var interfaces = NetworkInterface
                .GetAllNetworkInterfaces()
                .Where(n => n.OperationalStatus == OperationalStatus.Up)
                .Where(n => n.NetworkInterfaceType != NetworkInterfaceType.Loopback)
                .Where(n => n.Supports(NetworkInterfaceComponent.IPv4)).ToList();

            var totalIPs = interfaces.Count * 255f;
            var ipCount = 0f;

            foreach (NetworkInterface i in interfaces)
            {
                var gateway_ip = i.GetIPProperties().GatewayAddresses.FirstOrDefault(g => g.Address.AddressFamily == AddressFamily.InterNetwork);
                if (gateway_ip == null)
                    continue;

                var gateway_ip_bytes = gateway_ip.Address.GetAddressBytes();
                byte b = 1;

                while (true)
                {
                    var ip = new IPAddress(new byte[] { gateway_ip_bytes[0], gateway_ip_bytes[1], gateway_ip_bytes[2], b });

                    testSocket = new Socket(SocketType.Stream, ProtocolType.Tcp);

                    socketEvent = new SocketAsyncEventArgs();
                    socketEvent.Completed += SocketEvent_Completed;
                    socketEvent.RemoteEndPoint = new IPEndPoint(ip, 22);
                    
                    testSocket.ConnectAsync(socketEvent);

                    await Task.WhenAny(Task.Run(() => connectFinished.Wait()), Task.Delay(75));

                    if (!connectFinished.IsSet)
                        Socket.CancelConnectAsync(socketEvent);

                    if (testSocket.Connected)
                    {
                        try
                        {
                            IPHostEntry entry = Dns.GetHostEntry(ip);
                            if (entry != null && (bool)entry.HostName?.Contains(matchingHostname))
                                EntriesFound.Add(new Tuple<string, string>(entry.HostName, ip.ToString()));
                        }
                        catch (SocketException e)
                        {
                            EntriesFound.Add(new Tuple<string, string>("Unknown name", ip.ToString()));
                        }
                        testSocket.Disconnect(false);
                    }

                    connectFinished.Reset();

                    ipCount++;
                    Progress = (int)((ipCount / totalIPs) * 100);

                    if (b == 255)
                        break;
                    else
                        b++;

                }
            }

            Searching = false;
        }

        private static void SocketEvent_Completed(object sender, SocketAsyncEventArgs e)
        {
            connectFinished.Set();
        }

        private static void NotifyStaticPropertyChanged([CallerMemberName] string propertyName = "")
        {
            StaticPropertyChanged?.Invoke(null, new PropertyChangedEventArgs(propertyName));
        }
    }
}
