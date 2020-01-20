using System;
using System.Collections.Generic;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.ViewModels;

using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

namespace ACE_Mission_Control.Views
{
    public sealed partial class MainPage : Page
    {
        private int droneID;

        private MainViewModel ViewModel
        {
            get { return (MainViewModel)ViewModelLocator.Current.GetViewModel<MainViewModel>(droneID); }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (e.Parameter.GetType() == typeof(int))
            {
                droneID = (int)e.Parameter;
            }
            else
            {
                droneID = 0;
            }

            MissionFrame.Navigate(typeof(MissionPage), droneID);
            ConfigFrame.Navigate(typeof(ConfigPage), droneID);
        }
        public MainPage()
        {
            InitializeComponent();
        }
    }
}
