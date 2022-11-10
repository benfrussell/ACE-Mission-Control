using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Windows.ApplicationModel.Core;

namespace ACE_Mission_Control.ViewModels
{
    public class ScrollToConsoleEndMessage : MessageBase { }
    public class ConsoleViewModel : DroneViewModelBase
    {
        private string _monitorText;
        public string MonitorText
        {
            get { return _monitorText; }
            set
            {
                if (_monitorText == value)
                    return;
                _monitorText = value;
                RaisePropertyChanged("MonitorText");
                Messenger.Default.Send(new ScrollToConsoleEndMessage());
            }
        }

        private string _cmdResponseText;
        public string CMDResponseText
        {
            get { return _cmdResponseText; }
            set
            {
                if (_cmdResponseText == value)
                    return;
                _cmdResponseText = value;
                RaisePropertyChanged("CMDResponseText");
            }
        }

        private bool _canWriteCommand;
        public bool CanWriteCommand
        {
            get { return _canWriteCommand; }
            set
            {
                if (_canWriteCommand == value)
                    return;
                _canWriteCommand = value;
                RaisePropertyChanged("CanWriteCommand");
            }
        }

        private string _commandText;
        public string CommandText
        {
            get { return _commandText; }
            set
            {
                if (_commandText == value)
                    return;
                _commandText = value;
                RaisePropertyChanged("CommandText");
            }
        }

        private bool _canOpenDebug;
        public bool CanOpenDebug
        {
            get { return _canOpenDebug; }
            set
            {
                if (_canOpenDebug == value)
                    return;
                _canOpenDebug = value;
                RaisePropertyChanged("CanOpenDebug");
            }
        }

        public RelayCommand ConsoleActivateCommand => new RelayCommand(() => activateDebugCommand());
        public RelayCommand ConsoleCommandEnteredCommand => new RelayCommand(() => {
            if (CommandText != "" && AttachedDrone.OBCClient.DirectorRequestClient.ReadyForCommand)
            {
                if (!AttachedDrone.OBCClient.DirectorRequestClient.SendCommand(CommandText))
                {
                    var converter = new AlertToString();
                    CMDResponseText = (string)converter.Convert(new AlertEntry(AlertEntry.AlertLevel.Medium, AlertEntry.AlertType.NoConnection), typeof(string), null, null);
                }
                else
                {
                    CommandText = "";
                    CMDResponseText = "";
                }
            }
        });

        public ConsoleViewModel()
        {

        }

        protected override void DroneAttached(bool firstTime)
        {
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            //AttachedDrone.OBCClient.DebugMonitorClient.LineReceivedEvent += DebugMonitorStream_LineReceivedEvent;
            //AttachedDrone.OBCClient.DebugMonitorClient.PropertyChanged += DebugMonitorClient_PropertyChanged;
            AttachedDrone.OBCClient.DirectorRequestClient.ResponseReceivedEvent += DebugCommanderStream_ResponseReceivedEvent;
            //MonitorText = AttachedDrone.OBCClient.DebugMonitorClient.AllReceived;
            //CanOpenDebug = AttachedDrone.OBCClient.IsConnected;
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            //AttachedDrone.OBCClient.DebugMonitorClient.LineReceivedEvent -= DebugMonitorStream_LineReceivedEvent;
            //AttachedDrone.OBCClient.DebugMonitorClient.PropertyChanged -= DebugMonitorClient_PropertyChanged;
            AttachedDrone.OBCClient.DirectorRequestClient.ResponseReceivedEvent -= DebugCommanderStream_ResponseReceivedEvent;
        }

        private async void DebugMonitorClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            //await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            //{
            //    if (e.PropertyName == "Connected" && AttachedDrone.OBCClient.DebugMonitorClient.Connected)
            //        CanWriteCommand = true;
            //});
        }

        private async void DebugCommanderStream_ResponseReceivedEvent(object sender, ResponseReceivedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                CMDResponseText += e.Response;
            });
        }

        private async void DebugMonitorStream_LineReceivedEvent(object sender, LineReceivedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                MonitorText = MonitorText + e.Line;
            });
        }

        private async void OBCClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var client = sender as OnboardComputerClient;

                if (client.AssociatedDroneID != DroneID)
                    return;

                //if (e.PropertyName == "IsConnected")
                //    CanOpenDebug = client.IsConnected;
            });
        }

        private void activateDebugCommand()
        {
            //AttachedDrone.OBCClient.OpenDebugConsole();
        }
    }
}
