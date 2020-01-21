using System;
using System.Collections.Generic;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.ViewModels;

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace ACE_Mission_Control.Views
{
    public sealed partial class MainPage : DroneBasePage
    { 
        private MainViewModel ViewModel
        {
            get { return ViewModelLocator.Current.MainViewModel; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            MissionFrame.Navigate(typeof(MissionPage), droneID);
            ConfigFrame.Navigate(typeof(ConfigPage), droneID);

            if (e.NavigationMode == NavigationMode.Back || isInit)
                return;

            isInit = true;
        }
        public MainPage() : base()
        {
            
        }
    }
}
