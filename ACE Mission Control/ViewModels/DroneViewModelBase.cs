using ACE_Mission_Control.Core.Models;
using GalaSoft.MvvmLight;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

/* How to attach a Drone to a ViewModel
 * ShellPage.xaml           The MenuItems in the navigation bar in the shell page are the Drone objects.
 * 
 * ShellViewModel.cs:       If the invoked item is a Drone object, navigate to the MainView page with the drone ID as a parameter.
 *                          OnFrameNavigate parses the drone ID parameter to get the drone name for the header.
 *                          
 * DroneBasePage.xaml.cs:   OnNavigate parses the drone ID parameter or sets it 0. The drone ID is now available to all inheriting pages.
 *                          The drone ID is assigned to a property in the DroneViewModelBase, so all view models have the drone.
 *                          
 * DroneViewModelBase.cs:   After the DroneID is set, AttachedDrone or UnattachDrone is called for the inherting view models.
 */

namespace ACE_Mission_Control.ViewModels
{
    public abstract class DroneViewModelBase : ViewModelBase
    {
        private Drone attachedDrone;
        public Drone AttachedDrone { get => attachedDrone; set => attachedDrone = value; }

        protected int? DroneID;
        protected bool IsDroneAttached;
        private List<int> previouslyAttached = new List<int>();

        public void SetDroneID(int id)
        {
            if (DroneID != null)
            {
                // Don't attach the drone if the new drone is the same as the current one
                if (DroneID == id)
                    return;
                DroneUnattaching();
            }

            foreach (Drone d in DroneController.Drones)
            {
                if (d.ID == id)
                {
                    DroneID = id;
                    AttachedDrone = d;
                    IsDroneAttached = true;

                    RaisePropertyChanged();

                    if (!previouslyAttached.Contains(id))
                    {
                        DroneAttached(true);
                        previouslyAttached.Add(id);
                    }
                    else
                    {
                        DroneAttached(false);
                    }
                    break;
                }
            }
        }

        protected abstract void DroneAttached(bool firstTime);
        protected abstract void DroneUnattaching();
    }
}
