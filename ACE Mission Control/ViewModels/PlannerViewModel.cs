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
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace ACE_Mission_Control.ViewModels
{
    public class UpdatePlannerMapAreas : MessageBase { }
    public class UpdatePlannerMapPoints : MessageBase { public TreatmentInstruction Instruction { get; set; } }
    public class MapPointSelected : MessageBase { public int index { get; set; } }

    public class PlannerViewModel : DroneViewModelBase
    {
        private ObservableCollection<TreatmentInstruction> treatmentInstructions;
        public ObservableCollection<TreatmentInstruction> TreatmentInstructions
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

        private string _missionActivatedText;
        public string MissionActivatedText
        {
            set
            {
                if (value == _missionActivatedText)
                    return;
                _missionActivatedText = value;
                RaisePropertyChanged();
            }
            get
            {
                return _missionActivatedText;
            }
        }

        private string _treatmentDuration;
        public string TreatmentDuration
        {
            get { return _treatmentDuration; }
            set
            {
                if (_treatmentDuration == value)
                    return;
                _treatmentDuration = value;
                if (isTreatmentDurationValid(_treatmentDuration))
                {
                    TreatmentDurationBorderColour = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemBaseMediumHighColor"]);
                    TreatmentDurationValidText = "";
                }
                else
                {
                    TreatmentDurationBorderColour = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemErrorTextColor"]);
                    TreatmentDurationValidText = "Mission_InvalidInteger".GetLocalized();
                }
                RaisePropertyChanged("TreatmentDuration");
            }
        }

        private SolidColorBrush _treatmentDurationBorderColour;
        public SolidColorBrush TreatmentDurationBorderColour
        {
            get { return _treatmentDurationBorderColour; }
            set
            {
                if (_treatmentDurationBorderColour == value)
                    return;
                _treatmentDurationBorderColour = value;
                RaisePropertyChanged("TreatmentDurationBorderColour");
            }
        }

        private string _treatmentDurationValidText;
        public string TreatmentDurationValidText
        {
            get { return _treatmentDurationValidText; }
            set
            {
                if (_treatmentDurationValidText == value)
                    return;
                _treatmentDurationValidText = value;
                RaisePropertyChanged("TreatmentDurationValidText");
            }
        }

        public List<string> AvailablePayloads
        {
            get { return AttachedDrone.Mission.AvailablePayloads; }
        }

        private int _selectedPayload;
        public int SelectedPayload
        {
            get { return _selectedPayload; }
            set
            {
                if (_selectedPayload == value)
                    return;
                _selectedPayload = value;
                RaisePropertyChanged();
            }
        }

        private bool suppressPayloadCommand;

        public PlannerViewModel()
        {
            Messenger.Default.Register<MapPointSelected>(this, (msg) => { });

            TreatmentDurationBorderColour = new SolidColorBrush((Windows.UI.Color)Application.Current.Resources["SystemBaseMediumHighColor"]);
            suppressPayloadCommand = false;
        }

        protected override void DroneAttached(bool firstTime)
        {
            TreatmentInstructions = AttachedDrone.Mission.TreatmentInstructions;
            Messenger.Default.Send(new UpdatePlannerMapAreas());
            AttachedDrone.Mission.PropertyChanged += Mission_PropertyChanged;
            AttachedDrone.Mission.InstructionUpdated += Mission_InstructionUpdated;

            if (AttachedDrone.Mission.Activated)
                MissionActivatedText = "Mission_DeactivateButton".GetLocalized();
            else
                MissionActivatedText = "Mission_ActivateButton".GetLocalized();
        }

        private async void Mission_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                switch (e.PropertyName)
                {
                    case "CanBeReset":
                        RaisePropertyChanged("CanBeReset");
                        break;
                    case "CanBeModified":
                        RaisePropertyChanged("CanBeModified");
                        break;
                    case "CanToggleActivation":
                        RaisePropertyChanged("CanToggleActivation");
                        break;
                    case "Activated":
                        if (AttachedDrone.Mission.Activated)
                            MissionActivatedText = "Mission_DeactivateButton".GetLocalized();
                        else
                            MissionActivatedText = "Mission_ActivateButton".GetLocalized();
                        break;
                    case "TreatmentDuration":
                        TreatmentDuration = AttachedDrone.Mission.TreatmentDuration.ToString();
                        break;
                    case "SelectedPayload":
                        suppressPayloadCommand = true;
                        SelectedPayload = AttachedDrone.Mission.SelectedPayload;
                        break;
                    case "AvailablePayloads":
                        RaisePropertyChanged("AvailablePayloads");
                        break;
                }
            });
        }

        private void Mission_InstructionUpdated(object sender, InstructionsUpdatedEventArgs e)
        {
            // Force the binding to update by removing and re-adding the instruction
            // This is dumb but the alternative seems to be making my own ObservableCollection class which is also dumb
            foreach (TreatmentInstruction instruction in e.Instructions)
            {
                var indexOf = TreatmentInstructions.IndexOf(instruction);
                TreatmentInstructions.RemoveAt(indexOf);
                TreatmentInstructions.Insert(indexOf, instruction);

                // Update the map points to remove them for this instruction if it not longer has a treatment route
                if (instruction.TreatmentRoute == null)
                    Messenger.Default.Send(new UpdatePlannerMapPoints() { Instruction = instruction });
            }
            Messenger.Default.Send(new UpdatePlannerMapAreas());
        }

        public RelayCommand<ComboBox> WaypointRouteComboBox_SelectionChangedCommand => new RelayCommand<ComboBox>(args => WaypointRouteComboBox_SelectionChanged(args));

        // Not called when the selection changes to null
        private void WaypointRouteComboBox_SelectionChanged(ComboBox args)
        {
            if (args.DataContext != null)
            {
                Messenger.Default.Send(new UpdatePlannerMapPoints() { Instruction = args.DataContext as TreatmentInstruction });
            }
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.Mission.InstructionUpdated -= Mission_InstructionUpdated;
            AttachedDrone.Mission.PropertyChanged -= Mission_PropertyChanged;
        }

        private bool isTreatmentDurationValid(string durationString)
        {
            int parseOut;
            return durationString.Length == 0 || int.TryParse(durationString, out parseOut);
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

        public RelayCommand DurationChangedCommand => new RelayCommand(() => durationChangedCommand());
        private void durationChangedCommand()
        {
            if (isTreatmentDurationValid(TreatmentDuration))
                AttachedDrone.SendCommand("set_duration -duration " + TreatmentDuration.ToString());
        }

        public RelayCommand UploadCommand => new RelayCommand(() => uploadCommand());
        private void uploadCommand()
        {
            AttachedDrone.UploadMission();
        }

        public RelayCommand ActivateCommand => new RelayCommand(() => activateCommand());
        private void activateCommand()
        {
            if (AttachedDrone.Mission.Activated)
                AttachedDrone.SendCommand("deactivate_mission");
            else
                AttachedDrone.SendCommand("activate_mission");
        }

        public RelayCommand ResetCommand => new RelayCommand(() => resetCommand());
        private void resetCommand()
        {
            AttachedDrone.SendCommand("reset_mission");
        }

        public RelayCommand<SelectionChangedEventArgs> StartModeSelectionCommand => new RelayCommand<SelectionChangedEventArgs>((args) => startModeSelectionCommand(args));
        private void startModeSelectionCommand(SelectionChangedEventArgs args)
        {
            
        }
    }
}
