using ACE_Mission_Control.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public partial class DroneBasePage : Page
    {
        protected int droneID;
        protected bool isInit;

        protected virtual DroneViewModelBase BaseViewModel { get; }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.Parameter.GetType() == typeof(int))
                droneID = (int)e.Parameter;
            else
                droneID = 0;

            BaseViewModel.SetDroneID(droneID);

            if (e.NavigationMode == NavigationMode.Back || isInit)
                return;

            // Use this to limit execution of subsequent code to only execute on the first navigation
            //if (e.NavigationMode == NavigationMode.Back || isInit)
            //    return;

            isInit = true;
        }

        public DroneBasePage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }
    }
}
