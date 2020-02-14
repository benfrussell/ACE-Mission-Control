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

            var storyboard = ContentArea.Resources["FadeInStoryboard"] as Storyboard;
            storyboard.Begin();

            // Suppress navigation transitions for any page that type other than the one that we just switched from
            if (Items.SelectedItem == null || (Items.SelectedItem as PivotItem).Name == "MissionItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), droneID, e.NavigationTransitionInfo);
                ConfigFrame.Navigate(typeof(ConfigPage), droneID, new SuppressNavigationTransitionInfo());
                ConsoleFrame.Navigate(typeof(ConsolePage), droneID, new SuppressNavigationTransitionInfo());
            }
            else if ((Items.SelectedItem as PivotItem).Name == "ConfigItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), droneID, new SuppressNavigationTransitionInfo());
                ConfigFrame.Navigate(typeof(ConfigPage), droneID, e.NavigationTransitionInfo);
                ConsoleFrame.Navigate(typeof(ConsolePage), droneID, new SuppressNavigationTransitionInfo());
            }
            else if ((Items.SelectedItem as PivotItem).Name == "ConsoleItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), droneID, new SuppressNavigationTransitionInfo());
                ConfigFrame.Navigate(typeof(ConfigPage), droneID, new SuppressNavigationTransitionInfo());
                ConsoleFrame.Navigate(typeof(ConsolePage), droneID, e.NavigationTransitionInfo);
            }

        }
        public MainPage() : base()
        {
            this.InitializeComponent();
        }
    }
}
