using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;

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

        public RelayCommand ApplyChangesCommand => new RelayCommand(() => applyButtonClicked());
        public RelayCommand ResetChangesCommand => new RelayCommand(() => resetButtonClicked());

        public ConfigViewModel()
        {
            
        }

        protected override void DroneAttached(bool firstTime)
        {
            Hostname_Text = AttachedDrone.OBCClient.Hostname;
            Username_Text = AttachedDrone.OBCClient.Username;
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
    }
}
