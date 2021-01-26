using ACE_Mission_Control.ViewModels;
using ACE_Mission_Control.Core.Models;
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
using GalaSoft.MvvmLight.Messaging;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI;
using Windows.Devices.Geolocation;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{

    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class PlannerPage : DroneBasePage
    {
        private PlannerViewModel ViewModel
        {
            get { return ViewModelLocator.Current.PlannerViewModel; }
        }

        public PlannerPage() : base()
        {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (isInit)
            {
                // Things to do on every navigation
            }
            else
            {
                EntryMapControl.MapServiceToken = "Av_Cfm7_8qnq4khKZCRO5ywWQD0h2NDiuRVYZ1l2ArUEmrOM3ttdXQv6R_Wck_Lj";
            }

            base.OnNavigatedTo(e);
        }
    }
}
