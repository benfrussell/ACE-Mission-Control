using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using Windows.ApplicationModel.Core;

namespace ACE_Mission_Control.ViewModels
{
    public class ConfigViewModel : DroneViewModelBase
    {
        private string _hostname_text;
        public string Hostname_Text
        {
            get { return _hostname_text; }
            set
            {
                if (_hostname_text != value)
                {
                    UnsavedChanges = (AttachedDrone.OBCClient.Hostname != Hostname_Text);
                    _hostname_text = value;
                    RaisePropertyChanged();
                }
            }
        }

        private bool _unsaved_changes = false;
        public bool UnsavedChanges
        {
            get { return _unsaved_changes; }
            set
            {
                if (_unsaved_changes != value)
                {
                    _unsaved_changes = value;
                    RaisePropertyChanged();
                }
            }
        }

        public bool ManualCommandsChecked
        {
            get { return AttachedDrone.ManualCommandsOnly; }
        }


        public RelayCommand ApplyChangesCommand => new RelayCommand(() => applyButtonClicked());
        public RelayCommand ResetChangesCommand => new RelayCommand(() => resetButtonClicked());
        public RelayCommand ManualCommandsCheckedCommand => new RelayCommand(() => manualCommandsCheckedCommand());

        public ConfigViewModel()
        {

        }

        protected override void DroneAttached(bool firstTime)
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
            RaisePropertyChanged("ManualCommandsChecked");
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
        }

        private async void AttachedDrone_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                if (e.PropertyName == "ManualCommandsOnly")
                    RaisePropertyChanged("ManualCommandsChecked");
            });
        }

        private void applyButtonClicked()
        {
            AttachedDrone.OBCClient.Configure(Hostname_Text);
            UnsavedChanges = false;
        }

        private void resetButtonClicked()
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
        }

        private void manualCommandsCheckedCommand()
        {
            AttachedDrone.ManualCommandsOnly = !AttachedDrone.ManualCommandsOnly;
        }
    }
}
