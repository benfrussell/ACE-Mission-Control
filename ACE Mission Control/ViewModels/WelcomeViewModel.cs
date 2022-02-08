using System;
using System.ComponentModel;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;

namespace ACE_Mission_Control.ViewModels
{
    public class WelcomeViewModel : ViewModelBase
    {
        private string _ugcsConnectText;
        public string UGCSConnectText
        {
            get { return _ugcsConnectText; }
            set
            {
                if (value == _ugcsConnectText)
                    return;
                _ugcsConnectText = value;
                RaisePropertyChanged("UGCSConnectText");
            }
        }

        private string _welcomeTitle;
        public string WelcomeTitle
        {
            get { return _welcomeTitle; }
            set { Set(ref _welcomeTitle, value); }
        }

        public WelcomeViewModel()
        {
            UGCSClient.StaticPropertyChanged += UGCSClient_StaticPropertyChanged;
            UGCSConnectText = UGCSClient.ConnectionMessage;
            WelcomeTitle = "Shell_HomeItem".GetLocalized();
        }

        private async void UGCSClient_StaticPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "ConnectionMessage")
                    UGCSConnectText = UGCSClient.ConnectionMessage;
            });
        }
    }
}
