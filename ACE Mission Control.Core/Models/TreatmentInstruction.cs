using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text;

namespace ACE_Mission_Control.Core.Models
{
    public class TreatmentInstruction : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private AreaScanPolygon treatmentPolygon;
        public AreaScanPolygon TreatmentPolygon 
        { 
            get => treatmentPolygon;
            set
            {
                if (treatmentPolygon == value)
                    return;
                treatmentPolygon = value;
                RecalculateAutoLocks();
                NotifyPropertyChanged();
                NotifyPropertyChanged("Name");
                NotifyPropertyChanged("ID");
            } 
        }

        private WaypointRoute treatmentRoute;
        public WaypointRoute TreatmentRoute 
        { 
            get => treatmentRoute;
            set
            {
                if (treatmentRoute == value)
                    return;
                treatmentRoute = value;
                RecalculateAutoLocks();
                NotifyPropertyChanged();
            }
        }

        private List<int> validTreatmentRouteIDs;
        public List<int> ValidTreatmentRouteIDs
        {
            get => validTreatmentRouteIDs;
            private set
            {
                if (validTreatmentRouteIDs == value)
                    return;
                validTreatmentRouteIDs = value;
                NotifyPropertyChanged();
            }
        }

        private bool autoCalcUnlock = true;
        public bool AutoCalcUnlock 
        { 
            get => autoCalcUnlock;
            set
            {
                if (autoCalcUnlock == value)
                    return;
                autoCalcUnlock = value;
                RecalculateAutoLocks();
                NotifyPropertyChanged();
            }
        }

        private bool autoCalcLock = true;
        public bool AutoCalcLock 
        { 
            get => autoCalcLock; 
            set
            {
                if (autoCalcLock == value)
                    return;
                autoCalcLock = value;
                RecalculateAutoLocks();
                NotifyPropertyChanged();
            }
        }

        private Coordinate payloadUnlockCoordinate;
        public Coordinate PayloadUnlockCoordinate 
        { 
            get => payloadUnlockCoordinate;
            set
            {
                if (payloadUnlockCoordinate == value)
                    return;
                payloadUnlockCoordinate = value;
                NotifyPropertyChanged();
            }
        }

        private Coordinate payloadLockCoordinate;
        public Coordinate PayloadLockCoordinate 
        { 
            get => payloadLockCoordinate;
            set
            {
                if (payloadLockCoordinate == value)
                    return;
                payloadLockCoordinate = value;
                NotifyPropertyChanged();
            }
        }

        public string Name { get => TreatmentPolygon.Name; }
        public int ID { get => TreatmentPolygon.ID; }

        public bool DoTreatment = true;

        public TreatmentInstruction()
        {

        }

        public void UpdateValidTreatmentRoutes(List<WaypointRoute> allRoutes)
        {
            if (TreatmentPolygon == null)
                return;

            ValidTreatmentRouteIDs = (from route in allRoutes where route.Intersects(TreatmentPolygon) select route.ID).ToList();

            // Reconcile the current TreatmentRoute with the new valid treatment route information
            if (TreatmentRoute == null || !ValidTreatmentRouteIDs.Contains(TreatmentRoute.ID))
            {
                if (ValidTreatmentRouteIDs.Count > 0)
                    TreatmentRoute = allRoutes.Where(r => r.ID == ValidTreatmentRouteIDs[0]).FirstOrDefault();
                else
                    TreatmentRoute = null;
            }
                
        }

        public void RecalculateAutoLocks()
        {
            if (TreatmentPolygon == null || TreatmentRoute == null)
                return;
            if (AutoCalcLock)
                CalcAndSetPayloadLock();
            if (AutoCalcUnlock)
                CalcAndSetPayloadUnlock();
        }

        public void CalcAndSetPayloadUnlock()
        {
            if (TreatmentPolygon == null || TreatmentRoute == null)
                throw new Exception($"Tried to set the payload unlock coordinate for route {Name} but the treatment route or polygon does not exist.");
            PayloadUnlockCoordinate = TreatmentRoute.CalcIntersectWithArea(TreatmentPolygon);
        }

        public void CalcAndSetPayloadLock()
        {
            if (TreatmentPolygon == null || TreatmentRoute == null)
                throw new Exception($"Tried to set the payload lock coordinate for route {Name} but the treatment route or polygon does not exist.");
            PayloadLockCoordinate = TreatmentRoute.CalcIntersectWithArea(TreatmentPolygon, true);
        }

        public bool DoesWaypointRouteIntersectArea(WaypointRoute waypointRoute)
        {
            if (TreatmentPolygon == null || waypointRoute == null)
                throw new Exception("Tried to check a waypoint route and area intersection with an area scan or waypoint route that doesn't exist in the mission.");

            return waypointRoute.Intersects(TreatmentPolygon);
        }

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
