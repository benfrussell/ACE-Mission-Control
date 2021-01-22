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
using ACE_Mission_Control.ViewModels;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight.Messaging;
using System.Threading.Tasks;

// The Blank Page item template is documented at https://go.microsoft.com/fwlink/?LinkId=234238

namespace ACE_Mission_Control.Views
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MissionPage : DroneBasePage
    {
        private bool diagCanClose = false;

        private MissionViewModel ViewModel
        {
            get { return ViewModelLocator.Current.MissionViewModel; }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            if (isInit)
            {
                // Things to do on every navigation
            }
            else
            {
                Messenger.Default.Register<ScrollAlertDataGridMessage>(this, (msg) => AlertGridScrollToBottom(msg.newEntry));
            }
            base.OnNavigatedTo(e);
        }

        public MissionPage() : base()
        {
            this.InitializeComponent();

            Loaded += MissionPage_Loaded;
        }

        private void AlertGridScrollToBottom(object newItem)
        {
            AlertDataGrid.ScrollIntoView(newItem, AlertDataGrid.Columns[0]);
        }

        private void MissionPage_Loaded(object sender, RoutedEventArgs e)
        {
        }
    }
}
