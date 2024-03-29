﻿using System;
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

        public MainViewModel()
        {
            _alerts = new ObservableCollection<AlertEntry>();
        }

        protected override void DroneAttached(bool firstTime)
        {
            _alerts = new ObservableCollection<AlertEntry>(AttachedDrone.AlertLog);

            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged += Mission_PropertyChanged;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged += AlertLog_CollectionChanged;
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

        private async void AttachedDrone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "Synchronized")
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
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged -= Mission_PropertyChanged;
            UGCSClient.StaticPropertyChanged -= UGCSClient_StaticPropertyChanged;
            AttachedDrone.AlertLog.CollectionChanged -= AlertLog_CollectionChanged;
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

    }
}
