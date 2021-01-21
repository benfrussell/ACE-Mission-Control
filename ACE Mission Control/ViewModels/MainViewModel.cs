using System;
using System.ComponentModel;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI;

namespace ACE_Mission_Control.ViewModels
{
    public class MainViewModel : DroneViewModelBase
    {
        public string DroneName
        {
            get { return AttachedDrone.Name; }
        }

        private string _obcStatusText;
        public string OBCStatusText
        {
            get => _obcStatusText;
            set
            {
                if (value == _obcStatusText)
                    return;
                _obcStatusText = value;
                RaisePropertyChanged("OBCStatusText");
            }
        }

        private string _obcDirectorConnectedText;
        public string OBCDirectorConnectedText
        {
            get => _obcDirectorConnectedText;
            set
            {
                if (value == _obcDirectorConnectedText)
                    return;
                _obcDirectorConnectedText = value;
                RaisePropertyChanged("OBCDirectorConnectedText");
            }
        }

        private SolidColorBrush _obcDirectorConnectedColour;
        public SolidColorBrush OBCDirectorConnectedColour
        {
            get => _obcDirectorConnectedColour;
            set
            {
                if (value == _obcDirectorConnectedColour)
                    return;
                _obcDirectorConnectedColour = value;
                RaisePropertyChanged("OBCDirectorConnectedColour");
            }
        }

        private string _obcChaperoneConnectedText;
        public string OBCChaperoneConnectedText
        {
            get => _obcChaperoneConnectedText;
            set
            {
                if (value == _obcChaperoneConnectedText)
                    return;
                _obcChaperoneConnectedText = value;
                RaisePropertyChanged("OBCChaperoneConnectedText");
            }
        }

        private SolidColorBrush _obcChaperoneConnectedColour;
        public SolidColorBrush OBCChaperoneConnectedColour
        {
            get => _obcChaperoneConnectedColour;
            set
            {
                if (value == _obcChaperoneConnectedColour)
                    return;
                _obcChaperoneConnectedColour = value;
                RaisePropertyChanged("OBCChaperoneConnectedColour");
            }
        }

        private string _obcDroneConnectedText;
        public string OBCDroneConnectedText
        {
            get => _obcDroneConnectedText;
            set
            {
                if (value == _obcDroneConnectedText)
                    return;
                _obcDroneConnectedText = value;
                RaisePropertyChanged("OBCDroneConnectedText");
            }
        }

        private SolidColorBrush _obcDroneConnectedColour;
        public SolidColorBrush OBCDroneConnectedColour
        {
            get => _obcDroneConnectedColour;
            set
            {
                if (value == _obcDroneConnectedColour)
                    return;
                _obcDroneConnectedColour = value;
                RaisePropertyChanged("OBCDroneConnectedColour");
            }
        }

        private string _ugcsMissionRetrieveText;
        public string UGCSMissionRetrieveText
        {
            set { _ugcsMissionRetrieveText = value; }
            get { return _ugcsMissionRetrieveText; }
        }

        public MainViewModel()
        {
        }

        protected override void DroneAttached(bool firstTime)
        {
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            OBCStatusText = AttachedDrone.MissionStage.ToString();
            SetDirectorConnectedText();
            SetChaperoneConnectedText();
            SetDroneConnectedText();
        }

        private void SetDirectorConnectedText()
        {
            if (AttachedDrone.OBCClient.IsDirectorConnected)
            {
                OBCDirectorConnectedText = "Connected";
                OBCDirectorConnectedColour = new SolidColorBrush(Colors.ForestGreen);
            }
            else if (AttachedDrone.OBCClient.ConnectionInProgress)
            {
                OBCDirectorConnectedText = "Attempting Connection";
                OBCDirectorConnectedColour = new SolidColorBrush(Colors.Yellow);
            }
            else
            {
                OBCDirectorConnectedText = "Not Connected";
                OBCDirectorConnectedColour = new SolidColorBrush(Colors.OrangeRed);
            }
        }

        private void SetChaperoneConnectedText()
        {
            if (AttachedDrone.OBCClient.IsChaperoneConnected)
            {
                OBCChaperoneConnectedText = "Connected";
                OBCChaperoneConnectedColour = new SolidColorBrush(Colors.ForestGreen);
            }
            else if (AttachedDrone.OBCClient.ConnectionInProgress)
            {
                OBCChaperoneConnectedText = "Attempting Connection";
                OBCChaperoneConnectedColour = new SolidColorBrush(Colors.Yellow);
            }
            else
            {
                OBCChaperoneConnectedText = "Not Connected";
                OBCChaperoneConnectedColour = new SolidColorBrush(Colors.OrangeRed);
            }
        }

        private void SetDroneConnectedText()
        {
            if (AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Online)
            {
                OBCDroneConnectedText = "Connected";
                OBCDroneConnectedColour = new SolidColorBrush(Colors.ForestGreen);
            }
            else if (AttachedDrone.InterfaceState == Pbdrone.InterfaceStatus.Types.State.Attempting)
            {
                OBCDroneConnectedText = "Attempting Connection";
                OBCDroneConnectedColour = new SolidColorBrush(Colors.Yellow);
            }
            else
            {
                OBCDroneConnectedText = "Not Connected";
                OBCDroneConnectedColour = new SolidColorBrush(Colors.OrangeRed);
            }
        }

        private async void AttachedDrone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "MissionStage")
                    OBCStatusText = AttachedDrone.MissionStage.ToString();
                else if (e.PropertyName == "InterfaceState")
                    SetDroneConnectedText();
            });
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
        }

        private async void OBCClient_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "ConnectionInProgress")
                {
                    SetChaperoneConnectedText();
                    SetDirectorConnectedText();
                }
                else if (e.PropertyName == "IsDirectorConnected")
                {
                    SetDirectorConnectedText();
                }
                else if (e.PropertyName == "IsChaperoneConnected")
                {
                    SetChaperoneConnectedText();
                }
            });
        }
    }
}
