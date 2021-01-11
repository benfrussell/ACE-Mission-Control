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
        public ObservableCollection<TreatmentInstruction> TreatmentInstructions
        {
            get => IsDroneAttached ? AttachedDrone.MissionData.TreatmentInstructions : null;
        }

        public PlannerViewModel()
        {
            Messenger.Default.Register<MapPointSelected>(this, (msg) => { });
        }

        protected override void DroneAttached(bool firstTime)
        {
            RaisePropertyChanged("TreatmentInstructions");

            AttachedDrone.PropertyChanged += AttachedDrone_PropertyChanged;
            AttachedDrone.MissionData.TreatmentInstructions.CollectionChanged += TreatmentInstructions_CollectionChanged;
        }

        private async void TreatmentInstructions_CollectionChanged(object sender, NotifyCollectionChangedEventArgs e)
        {
            await CoreApplication.MainView.CoreWindow.Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                RaisePropertyChanged("TreatmentInstructions");
                if (e.OldItems != null)
                    foreach (TreatmentInstruction oldItem in e.OldItems)
                        oldItem.PropertyChanged -= TreatmentInstruction_PropertyChanged;

                if (e.NewItems != null)
                    foreach (TreatmentInstruction newItem in e.NewItems)
                        newItem.PropertyChanged += TreatmentInstruction_PropertyChanged;
            });
        }

        private void TreatmentInstruction_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            RaisePropertyChanged("TreatmentInstructions");
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
        }
    }
}
