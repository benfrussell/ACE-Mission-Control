using System;
using System.ComponentModel;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
using Windows.UI;
using System.Collections.ObjectModel;
using GalaSoft.MvvmLight.Messaging;
using GalaSoft.MvvmLight.Command;
using Microsoft.Toolkit.Uwp.UI.Controls;
using ACE_Mission_Control.Helpers;
using Windows.ApplicationModel.DataTransfer;
using Windows.UI.Xaml;

namespace ACE_Mission_Control.ViewModels
{
    public class ScrollAlertDataGridMessage : MessageBase { public AlertEntry newEntry { get; set; } }
    public class AlertDataGridSizeChangeMessage : MessageBase { }

    public class MainViewModel : DroneViewModelBase
    {
        public string DroneName
        {
            get { return AttachedDrone.Name; }
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

        private ObservableCollection<AlertEntry> _alerts;
        public ObservableCollection<AlertEntry> Alerts
        {
            get => _alerts;
            set
            {
                if (_alerts == value)
                    return;
                _alerts = value;
                RaisePropertyChanged();
            }
        }

        private GridLength frameHeight;
        public GridLength FrameHeight
        {
            get => frameHeight;
            set
            {
                if (value == frameHeight)
                    return;
                frameHeight = value;
                RaisePropertyChanged();
            }
        }

        private GridLength alertGridHeight;
        public GridLength AlertGridHeight
        {
            get => alertGridHeight;
            set
            {
                if (value == alertGridHeight)
                    return;
                alertGridHeight = value;
                RaisePropertyChanged();
            }
        }

        public MainViewModel()
        {
            _alerts = new ObservableCollection<AlertEntry>();
        }

        protected override void DroneAttached(bool firstTime)
        {
            _alerts = new ObservableCollection<AlertEntry>(AttachedDrone.AlertLog);

            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged += Mission_PropertyChanged;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged += AlertLog_CollectionChanged;

            SetDirectorConnectedText();
            SetChaperoneConnectedText();
            SetDroneConnectedText();
        }

        private async void AlertLog_CollectionChanged(object sender, System.Collections.Specialized.NotifyCollectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.Action == System.Collections.Specialized.NotifyCollectionChangedAction.Add)
                {
                    foreach (AlertEntry entry in e.NewItems)
                        Alerts.Add(entry);
                }

                var msg = new ScrollAlertDataGridMessage() { newEntry = Alerts[Alerts.Count - 1] };
                Messenger.Default.Send(msg);
            });
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
                if (e.PropertyName == "InterfaceState")
                    SetDroneConnectedText();
                else if (e.PropertyName == "Synchronized")
                    RaisePropertyChanged("Synchronized");
            });
        }

        private void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "Stage")
                RaisePropertyChanged("Stage");
            else if (e.PropertyName == "Activated")
                RaisePropertyChanged("Activated");
        }

        private void UGCSClient_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ConnectionMessage")
                RaisePropertyChanged("ConnectionMessage");
        }


        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged -= Mission_PropertyChanged;
            UGCSClient.StaticPropertyChanged -= UGCSClient_StaticPropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged -= AlertLog_CollectionChanged;
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

        public RelayCommand<DataGrid> AlertCopyCommand => new RelayCommand<DataGrid>((grid) => alertCopyCommand(grid));
        private void alertCopyCommand(DataGrid grid)
        {
            string copiedText = "";
            AlertToString alertConverter = new AlertToString();
            foreach (object item in grid.SelectedItems)
            {
                var entry = (AlertEntry)item;
                copiedText += entry.Timestamp.ToLongTimeString() + " " + alertConverter.Convert(entry, typeof(string), null, null) + "\n";
            }
            DataPackage dataPackage = new DataPackage();
            dataPackage.RequestedOperation = DataPackageOperation.Copy;
            dataPackage.SetText(copiedText);
            Clipboard.SetContent(dataPackage);
        }

        public RelayCommand<Pivot> PivotSelectionChangedCommand => new RelayCommand<Pivot>((pivot) => pivotSelectionChangedCommand(pivot));

        private void pivotSelectionChangedCommand(Pivot pivot)
        {
            var name = (pivot.SelectedItem as PivotItem).Name;
            var gridHeightChanged = false;

            if (name == "MissionItem")
            {
                FrameHeight = new GridLength(1, GridUnitType.Auto);
                if (AlertGridHeight.Value == 80)
                    gridHeightChanged = true;
                AlertGridHeight = new GridLength(1, GridUnitType.Star);
            }
            else if (name == "PlannerItem" || name == "ConfigItem" || name == "ConsoleItem")
            {
                FrameHeight = new GridLength(1, GridUnitType.Star);
                if (AlertGridHeight.Value != 80)
                    gridHeightChanged = true;
                AlertGridHeight = new GridLength(80);
            }

            if (gridHeightChanged)
                Messenger.Default.Send(new AlertDataGridSizeChangeMessage());

            if (Alerts.Count > 0)
            {
                var alertScrollMsg = new ScrollAlertDataGridMessage() { newEntry = Alerts[Alerts.Count - 1] };
                Messenger.Default.Send(alertScrollMsg);
            }
        }
    }
}
