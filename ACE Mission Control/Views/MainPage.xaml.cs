using System;
using System.Collections.Generic;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.ViewModels;

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media.Animation;
using Windows.UI.Xaml.Navigation;

namespace ACE_Mission_Control.Views
{
    public sealed partial class MainPage : Page
    {
        private int droneID;
        private bool isInit = false;

        private MainViewModel ViewModel
        {
            get { return ViewModelLocator.Current.MainViewModel; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter.GetType() == typeof(int))
                droneID = (int)e.Parameter;
            else
                droneID = 0;

            ViewModel.SetDroneID(droneID);

            MissionFrame.Navigate(typeof(MissionPage), droneID);
            ConfigFrame.Navigate(typeof(ConfigPage), droneID);

            if (e.NavigationMode == NavigationMode.Back || isInit)
                return;

            isInit = true;
        }
        public MainPage()
        {
            InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }
    }
}
