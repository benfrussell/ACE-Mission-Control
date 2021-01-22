using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Collections.Specialized;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;
using GalaSoft.MvvmLight.Command;
using GalaSoft.MvvmLight.Messaging;
using Windows.ApplicationModel.Core;
using Windows.UI.Xaml.Controls;

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

        public PlannerViewModel()
        {
            Messenger.Default.Register<MapPointSelected>(this, (msg) => { });
        }

        protected override void DroneAttached(bool firstTime)
        {
            TreatmentInstructions = AttachedDrone.MissionData.TreatmentInstructions;
            Messenger.Default.Send(new UpdatePlannerMapAreas());
            AttachedDrone.MissionData.InstructionUpdated += MissionData_InstructionUpdated;
        }

        private void MissionData_InstructionUpdated(object sender, InstructionsUpdatedEventArgs e)
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
            AttachedDrone.MissionData.InstructionUpdated -= MissionData_InstructionUpdated;
        }
    }
}
