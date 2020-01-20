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
    public sealed partial class MissionPage : Page
    { 
        private static int curDroneID = 0;
        private static List<int> dronesRegistered = new List<int>();
        private int droneID;
        private bool diagCanClose = false;
        private bool isInit = false;

        private MissionViewModel ViewModel
        {
            get { return (MissionViewModel)ViewModelLocator.Current.GetViewModel<MissionViewModel>(droneID); }
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (e.NavigationMode == NavigationMode.Back || isInit)
                return;

            if (e.Parameter.GetType() == typeof(int))
                droneID = (int)e.Parameter;
            else
                droneID = 0;

            curDroneID = droneID;

            Messenger.Default.Register<ShowPassphraseDialogMessage>(this, droneID, showDiag);

            Messenger.Default.Register<HidePassphraseDialogMessage>(this, droneID, (msg) =>
            {
                if (curDroneID != droneID)
                    return;
                diagCanClose = true;
                PassphraseDialog.Hide();
            });

            System.Diagnostics.Debug.WriteLine("Registered");

            dronesRegistered.Add(droneID);

            isInit = true;
        }

        private async void showDiag(object msg)
        {
            System.Diagnostics.Debug.WriteLine("I am called.");
            if (curDroneID != droneID)
                return;
            await PassphraseDialog.ShowAsync();
        }

        protected override void OnNavigatingFrom(NavigatingCancelEventArgs e)
        {
            base.OnNavigatingFrom(e);

            Messenger.Default.Unregister(this);
        }

        public MissionPage()
        {
            this.InitializeComponent();
            NavigationCacheMode = NavigationCacheMode.Enabled;
            System.Diagnostics.Debug.WriteLine("Initialized");
        }

        // TODO: Re-enable this when the button works
        private void PassphraseDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            // If we're closing because of the primary button, check if it's been allowed first
            if (args.Result == ContentDialogResult.Primary)
            {
                if (diagCanClose)
                    PassphraseDialogInput.Password = "";

                args.Cancel = !diagCanClose;
                diagCanClose = false;
            }
            else
            {
                PassphraseDialogInput.Password = "";
            }
        }

        //private void PassphraseDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        //{
        //    if (diagCanClose)
        //        PassphraseDialogInput.Password = "";

        //    args.Cancel = !diagCanClose;
        //    diagCanClose = false;
        //}
    }
}
