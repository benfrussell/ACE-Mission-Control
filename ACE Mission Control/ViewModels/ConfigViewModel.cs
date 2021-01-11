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

        private bool _automation_checked;
        public bool DisableAutomationChecked
        {
            get { return _automation_checked; }
            set
            {
                if (_automation_checked == value)
                    return;
                _automation_checked = value;
                RaisePropertyChanged("DisableAutomationChecked");
            }
        }

        private bool _disable_auto_connect_checked;
        public bool DisableAutoConnectChecked
        {
            get { return _disable_auto_connect_checked; }
            set
            {
                if (_disable_auto_connect_checked == value)
                    return;
                _disable_auto_connect_checked = value;
                RaisePropertyChanged("DisableAutoConnectChecked");
            }
        }

        private bool _connect_button_enabled;
        public bool ConnectButtonEnabled
        {
            get { return _connect_button_enabled; }
            set
            {
                if (value == _connect_button_enabled)
                    return;
                _connect_button_enabled = value;
                RaisePropertyChanged("ConnectButtonEnabled");
            }
        }

        public RelayCommand ApplyChangesCommand => new RelayCommand(() => applyButtonClicked());
        public RelayCommand ResetChangesCommand => new RelayCommand(() => resetButtonClicked());
        public RelayCommand DisableAutomationCheckCommand => new RelayCommand(() => disableAutomationChecked());
        public RelayCommand ConnectCommand => new RelayCommand(() => connectClicked());
        public RelayCommand DisableAutoConnectCheckCommand => new RelayCommand(() => disableAutoConnectChecked());
        public ConfigViewModel()
        {

        }

        protected override void DroneAttached(bool firstTime)
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
            DisableAutomationChecked = AttachedDrone.OBCClient.AutomationDisabled;
            DisableAutoConnectChecked = AttachedDrone.OBCClient.AutoConnectDisabled;
            ConnectButtonEnabled = !AttachedDrone.OBCClient.IsConnected &&
                !AttachedDrone.OBCClient.AttemptingConnection &&
                AttachedDrone.OBCClient.AutoConnectDisabled;
            AttachedDrone.OBCClient.PropertyChanged += OBCClient_PropertyChanged;
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.OBCClient.PropertyChanged -= OBCClient_PropertyChanged;
        }

        private async void OBCClient_PropertyChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            // Property changes might come in from the wrong thread. This dispatches it to the UI thread.
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                var client = sender as OnboardComputerClient;

                if (client.AttachedDrone.ID != DroneID)
                    return;

                if (e.PropertyName == "IsConnected" || e.PropertyName == "AttemptingConnection" || e.PropertyName == "AutoConnectDisabled")
                {
                    ConnectButtonEnabled = !AttachedDrone.OBCClient.IsConnected &&
                        !AttachedDrone.OBCClient.AttemptingConnection &&
                        AttachedDrone.OBCClient.AutoConnectDisabled;
                }

            });
        }

        private void applyButtonClicked()
        {
            AttachedDrone.OBCClient.Hostname = Hostname_Text;
            UnsavedChanges = false;
        }

        private void resetButtonClicked()
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
        }

        private void disableAutomationChecked()
        {
            AttachedDrone.OBCClient.AutomationDisabled = DisableAutomationChecked;
        }

        private void connectClicked()
        {
            AttachedDrone.OBCClient.TryConnect();
        }

        private void disableAutoConnectChecked()
        {
            AttachedDrone.OBCClient.AutoConnectDisabled = DisableAutoConnectChecked;
        }
    }
}
