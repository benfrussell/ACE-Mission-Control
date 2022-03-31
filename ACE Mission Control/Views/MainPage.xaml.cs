using System;
using System.Collections.Generic;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.ViewModels;
using GalaSoft.MvvmLight.Messaging;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;
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


        public MainPage() : base()
        {
            this.InitializeComponent();
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            var storyboard = Items.Resources["FadeInStoryboard"] as Storyboard;
            storyboard.Begin();

            var pageParams = (DronePageParams)e.Parameter;

            Items.SelectedIndex = pageParams.PivotItem;

            var missionVM = ViewModelLocator.Current.MissionViewModel;
            missionVM.ConnectionExpanded = pageParams.ConnectionOpen;
            missionVM.PlannerExpanded = pageParams.PlannerOpen;
            missionVM.ControlsExpanded = pageParams.ControlsOpen;

            // Suppress navigation transitions for any page that type other than the one that we just switched from
            if (Items.SelectedItem == null || (Items.SelectedItem as PivotItem).Name == "MissionItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), pageParams, e.NavigationTransitionInfo);
                ConfigFrame.Navigate(typeof(ConfigPage), pageParams, new SuppressNavigationTransitionInfo());
                ConsoleFrame.Navigate(typeof(ConsolePage), pageParams, new SuppressNavigationTransitionInfo());
            }
            else if ((Items.SelectedItem as PivotItem).Name == "ConfigItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), pageParams, new SuppressNavigationTransitionInfo());
                ConfigFrame.Navigate(typeof(ConfigPage), pageParams, e.NavigationTransitionInfo);
                ConsoleFrame.Navigate(typeof(ConsolePage), pageParams, new SuppressNavigationTransitionInfo());
            }
            else if ((Items.SelectedItem as PivotItem).Name == "ConsoleItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), pageParams, new SuppressNavigationTransitionInfo());
                ConfigFrame.Navigate(typeof(ConfigPage), pageParams, new SuppressNavigationTransitionInfo());
                ConsoleFrame.Navigate(typeof(ConsolePage), pageParams, e.NavigationTransitionInfo);
            }
        }
    }
}
