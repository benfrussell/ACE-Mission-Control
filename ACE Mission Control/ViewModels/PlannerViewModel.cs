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

namespace ACE_Mission_Control.ViewModels
{
    public class AddMapAreasMessage : MessageBase { public List<AreaScanPolygon> areas { get; set; } }
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
            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            TreatmentInstructions = AttachedDrone.MissionData.TreatmentInstructions;
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
            }
        }

        private async void AttachedDrone_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                //switch (e.PropertyName)
                //{
                //    case "MissionData":
                //        // New mission data received in the model
                //        RaisePropertyChanged("TreatmentInstructions");
                //        if (AttachedDrone.MissionData.TreatmentInstructions.Count > 0)
                //        {
                //            var treatmentPolygons = (from i in AttachedDrone.MissionData.TreatmentInstructions
                //                                     select i.TreatmentPolygon).ToList();
                //            Messenger.Default.Send(new AddMapAreasMessage() { areas = treatmentPolygons });
                //        }

                //        break;
                //}
            });
        }

        protected override void DroneUnattaching()
        {
            AttachedDrone.PropertyChanged -= AttachedDrone_PropertyChanged;
            AttachedDrone.MissionData.InstructionUpdated -= MissionData_InstructionUpdated;
        }
    }
}
