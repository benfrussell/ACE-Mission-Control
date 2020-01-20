using System;
using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;

namespace ACE_Mission_Control.ViewModels
{
    public class MainViewModel : DroneViewModelBase
    {
        public string DroneName
        {
            get { return AttachedDrone.Name; }
        }
        public MainViewModel()
        {
            
        }

        protected override void DroneAttached(bool firstTime)
        {

        }
    }
}
