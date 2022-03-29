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

            var storyboard = ContentArea.Resources["FadeInStoryboard"] as Storyboard;
            storyboard.Begin();

            // Suppress navigation transitions for any page that type other than the one that we just switched from
            if (Items.SelectedItem == null || (Items.SelectedItem as PivotItem).Name == "MissionItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), DroneID, e.NavigationTransitionInfo);
                ConfigFrame.Navigate(typeof(ConfigPage), DroneID, new SuppressNavigationTransitionInfo());
                ConsoleFrame.Navigate(typeof(ConsolePage), DroneID, new SuppressNavigationTransitionInfo());
            }
            else if ((Items.SelectedItem as PivotItem).Name == "PlannerItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), DroneID, new SuppressNavigationTransitionInfo());
                ConfigFrame.Navigate(typeof(ConfigPage), DroneID, new SuppressNavigationTransitionInfo());
                ConsoleFrame.Navigate(typeof(ConsolePage), DroneID, new SuppressNavigationTransitionInfo());
            }
            else if ((Items.SelectedItem as PivotItem).Name == "ConfigItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), DroneID, new SuppressNavigationTransitionInfo());
                ConfigFrame.Navigate(typeof(ConfigPage), DroneID, e.NavigationTransitionInfo);
                ConsoleFrame.Navigate(typeof(ConsolePage), DroneID, new SuppressNavigationTransitionInfo());
            }
            else if ((Items.SelectedItem as PivotItem).Name == "ConsoleItem")
            {
                MissionFrame.Navigate(typeof(MissionPage), DroneID, new SuppressNavigationTransitionInfo());
                ConfigFrame.Navigate(typeof(ConfigPage), DroneID, new SuppressNavigationTransitionInfo());
                ConsoleFrame.Navigate(typeof(ConsolePage), DroneID, e.NavigationTransitionInfo);
            }
        }

        private ScrollViewer GetScrollViewer(DependencyObject element)
        {
            if (element is ScrollViewer)
            {
                return (ScrollViewer)element;
            }

            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(element); i++)
            {
                var child = VisualTreeHelper.GetChild(element, i);

                var result = GetScrollViewer(child);
                if (result == null)
                {
                    continue;
                }
                else
                {
                    return result;
                }
            }

            return null;
        }
    }
}
