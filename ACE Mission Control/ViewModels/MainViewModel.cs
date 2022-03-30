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

        private PivotItem selectedItem;
        public PivotItem SelectedItem
        {
            get => selectedItem;
            set
            {
                if (selectedItem == value)
                    return;
                selectedItem = value;
                RaisePropertyChanged();
            }
        }

        public MainViewModel()
        {
            _alerts = new ObservableCollection<AlertEntry>();
        }

        protected override void DroneAttached(bool firstTime)
        {
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            AttachedDrone.Mission.PropertyChanged += Mission_PropertyChanged;
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
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
        }
    }
}
