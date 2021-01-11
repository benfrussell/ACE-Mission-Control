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
                    UnsavedChanges = (AttachedDrone.OBCClient.Hostname != Hostname_Text) | (AttachedDrone.OBCClient.Username != value);
                    _hostname_text = value;
                    RaisePropertyChanged();
                }
            }
        }

        private string _username_text;
        public string Username_Text
        {
            get { return _username_text; }
            set
            {
                if (_username_text != value)
                {
                    UnsavedChanges = (AttachedDrone.OBCClient.Hostname != Hostname_Text) | (AttachedDrone.OBCClient.Username != value);
                    _username_text = value;
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

        private bool _automation_checked;
        public bool AutomationChecked
        {
            get { return _automation_checked; }
            set
            {
                if (_automation_checked == value)
                    return;
                _automation_checked = value;
                RaisePropertyChanged("AutomationChecked");
            }
        }

        public RelayCommand ApplyChangesCommand => new RelayCommand(() => applyButtonClicked());
        public RelayCommand ResetChangesCommand => new RelayCommand(() => resetButtonClicked());
        public RelayCommand AutomationCheckCommand => new RelayCommand(() => automationChecked());

        public ConfigViewModel()
        {
            
        }

        protected override void DroneAttached(bool firstTime)
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
            Username_Text = AttachedDrone.OBCClient.Username;
            AutomationChecked = AttachedDrone.OBCClient.AutomationDisabled;
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
        }

        protected override void DroneUnattaching()
        {
            
        }

        private async void OBCClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var client = sender as OnboardComputerClient;

                if (client.AttachedDrone.ID != DroneID)
                    return;
            });
        }

        private void applyButtonClicked()
        {
            AttachedDrone.OBCClient.Username = Username_Text;
            AttachedDrone.OBCClient.Hostname = Hostname_Text;
            UnsavedChanges = false;
        }

        private void resetButtonClicked()
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
            Username_Text = AttachedDrone.OBCClient.Username;
        }

        private void automationChecked()
        {
            AttachedDrone.OBCClient.AutomationDisabled = AutomationChecked;
        }
    }
}
