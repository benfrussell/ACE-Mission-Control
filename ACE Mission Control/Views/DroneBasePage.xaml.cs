using ACE_Mission_Control.ViewModels;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
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
        public int DroneID;
        protected bool isInit;

        protected DroneViewModelBase BaseViewModel
        {
            get
            {
                // Gets the subclass ViewModel parameter
                MemberInfo member = GetType()
                    .GetMember("ViewModel", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static)
                    .FirstOrDefault();

                PropertyInfo property = member as PropertyInfo;

                return (DroneViewModelBase)property.GetValue(this, null);
            }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            DronePageParams pageParams = (DronePageParams)e.Parameter;
            DroneID = pageParams.DroneID;
            BaseViewModel.SetDroneID(DroneID);

            isInit = true;
        }

        public DroneBasePage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
        }
    }
}
