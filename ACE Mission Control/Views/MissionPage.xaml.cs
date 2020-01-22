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
                base.OnNavigatedTo(e);
            }
            else
            {
                Messenger.Default.Register<ShowPassphraseDialogMessage>(this, async (msg) => await PassphraseDialog.ShowAsync());

                Messenger.Default.Register<HidePassphraseDialogMessage>(this, (msg) =>
                {
                    diagCanClose = true;
                    PassphraseDialog.Hide();
                });
                base.OnNavigatedTo(e);
            }



            base.OnNavigatedTo(e);
        }

        public MissionPage() : base()
        {
            this.InitializeComponent();

            Loaded += MissionPage_Loaded;
        }

        private void MissionPage_Loaded(object sender, RoutedEventArgs e)
        {
        }

        private void PassphraseDialog_Closing(ContentDialog sender, ContentDialogClosingEventArgs args)
        {
            // Only allow the dialog to close if the diagCanClose is true.
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
    }
}
