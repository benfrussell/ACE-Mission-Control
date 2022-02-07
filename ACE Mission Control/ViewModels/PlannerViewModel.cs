using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE_Mission_Control.Core.Models;
using ACE_Mission_Control.Helpers;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Windows.ApplicationModel.Core;
using Windows.Devices.Geolocation;
using Windows.Storage.Streams;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Maps;
using Windows.UI.Xaml.Media;
using Windows.UI.Notifications;
using Microsoft.Toolkit.Uwp.Notifications;
using System.Globalization;

namespace ACE_Mission_Control.ViewModels
{
    public class PlannerViewModel : DroneViewModelBase
    {
        private ObservableCollection<ITreatmentInstruction> treatmentInstructions;
        public ObservableCollection<ITreatmentInstruction> TreatmentInstructions
        {
            get => treatmentInstructions;
            set
            {
                if (treatmentInstructions == value)
                    return;
                treatmentInstructions = value;
                RaisePropertyChanged();
            }
        }

        private string _missionLockText;
        public string MissionLockText
        {
            set
            {
                if (value == _missionLockText)
                    return;
                _missionLockText = value;
                RaisePropertyChanged();
            }
            get
            {
                return _missionLockText;
            }
        }

        private bool suppressPayloadCommand;

        public PlannerViewModel()
        {
            suppressPayloadCommand = false;
        }

        protected override void DroneAttached(bool firstTime)
        {
            TreatmentInstructions = AttachedDrone.Mission.TreatmentInstructions;

            

            

            if (AttachedDrone.Mission.Locked)
                MissionLockText = "Planner_LockButton".GetLocalized();
            else
                MissionLockText = "Planner_UnlockButton".GetLocalized();
        }

        

        protected override void DroneUnattaching()
        {
            
        }

        // --- Mission Commands

        public RelayCommand<ComboBox> PayloadSelectionCommand => new RelayCommand<ComboBox>((box) => payloadSelectionCommand(box));
        private void payloadSelectionCommand(ComboBox box)
        {
            if (!suppressPayloadCommand)
            {
                //AttachedDrone.SendCommand("set_payload -index " + box.SelectedIndex.ToString());
                suppressPayloadCommand = false;
            }
        }

        public RelayCommand LockCommand => new RelayCommand(() => lockCommand());
        private void lockCommand()
        {
            if (!AttachedDrone.Mission.Locked)
                AttachedDrone.SendCommand("lock_mission");
            else
                AttachedDrone.SendCommand("unlock_mission");
        }

        
    }
}
