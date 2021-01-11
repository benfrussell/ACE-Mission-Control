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
 * MainPage.xaml.cs:        OnNavigate parses the drone ID parameter or set it 0.
 *                          Navigate the subviews of MainPage with the drone ID as a parameter to load them.
 *                          Link the ViewModel to the page by executing the ViewModel's SetDroneID method with the drone ID as a parameter.
 *                          
 * (SubView)Page.xaml.cs:   Same operations as MainPage.xaml.cs. ViewModel is linked with GetViewModel.
 *                          
 * DroneViewModelBase.cs:   SetDroneID sets the ID if it has not been set in this instance yet.
 *                          If it is set the DroneAttached abstract method is called.
 * 
 * (Page)ViewModel.cs:      The drone can be accessed at AttachedDrone as soon as DroneAttached() is called.
 */

namespace ACE_Mission_Control.ViewModels
{
    public abstract class DroneViewModelBase : ViewModelBase
    {
        protected int? DroneID;
        protected Drone AttachedDrone;
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
