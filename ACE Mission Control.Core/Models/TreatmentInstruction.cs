﻿using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using Pbdrone;
using UGCS.Sdk.Protocol.Encoding;

namespace ACE_Mission_Control.Core.Models
{
    public class TreatmentInstruction
    {
        // This seems to be the easiest way to notify that this single property changed
        public event EventHandler<EventArgs> EnabledChangedEvent;
        
        public static WaypointRouteInterceptCollection InterceptCollection = new WaypointRouteInterceptCollection();

        private AreaScanPolygon treatmentPolygon;
        public AreaScanPolygon TreatmentPolygon 
        { 
            get => treatmentPolygon;
            private set
            {
                if (treatmentPolygon != null && 
                    treatmentPolygon.Id == value.Id && 
                    treatmentPolygon.LastModificationTime == value.LastModificationTime)
                    return;
                treatmentPolygon = value;
            } 
        }

        private WaypointRouteIntercept selectedInterceptRoute;
        public WaypointRoute TreatmentRoute 
        {
            get
            {
                if (selectedInterceptRoute == null)
                    RevalidateTreatmentRoute();
                return selectedInterceptRoute != null ? selectedInterceptRoute.WaypointRoute : null;
            } 
            set
            {
                if (selectedInterceptRoute != null && selectedInterceptRoute.WaypointRoute == value)
                    return;
                selectedInterceptRoute = InterceptCollection[TreatmentPolygon.Id].FirstOrDefault(r => r.WaypointRoute == value);
            }
        }

        private AreaResult.Types.Status areaStatus;
        public AreaResult.Types.Status AreaStatus { get => areaStatus; set => areaStatus = value; }

        public Coordinate AreaEntryCoordinate
        {
            get
            {
                if (selectedInterceptRoute == null)
                    RevalidateTreatmentRoute();
                if (selectedInterceptRoute != null)
                    return selectedInterceptRoute.EntryCoordinate;
                return null;
            }
        }

        public Coordinate AreaExitCoordinate
        {
            get
            {
                if (selectedInterceptRoute == null)
                    RevalidateTreatmentRoute();
                if (selectedInterceptRoute != null)
                    return selectedInterceptRoute.ExitCoordinate;
                return null;
            }
        }

        public IEnumerable<WaypointRoute> ValidTreatmentRoutes
        {
            get => InterceptCollection[TreatmentPolygon.Id].Select(r => r.WaypointRoute);
        }

        public string Name { get => TreatmentPolygon.Name; }

        private bool canBeEnabled;
        public bool CanBeEnabled
        {
            get => canBeEnabled;
            private set
            {
                if (canBeEnabled == value)
                    return;
                canBeEnabled = value;
            }
        }

        private bool enabled;
        public bool Enabled 
        { 
            get => enabled;
            set
            {
                if (enabled == value)
                    return;
                enabled = value;
                EnabledChangedEvent?.Invoke(this, new EventArgs());
            }
        }

        private float swath;
        public float Swath
        {
            get => swath;
            private set
            {
                if (swath == value)
                    return;
                swath = value;
            }
        }

        // Save the last enabled state so we can put enabled back into the user's preffered state if they fix the CanBeEnabled problem
        // Starts as null and will only save the state after being enabled for the first time
        private bool? lastEnabledState;

        public TreatmentInstruction(AreaScanPolygon treatmentArea)
        {
            TreatmentPolygon = treatmentArea;
            AreaStatus = AreaResult.Types.Status.NotStarted;
            // Swath is generally half of the side distance (metres)
            Swath = float.Parse(treatmentArea.Parameters.FirstOrDefault(p => p.Name == "sideDistance")?.Value) / 2;

            Enabled = true;
            lastEnabledState = null;

            RevalidateTreatmentRoute();
        }

        public void UpdateTreatmentArea(AreaScanPolygon treatmentArea)
        {
            TreatmentPolygon = treatmentArea;
            RevalidateTreatmentRoute();
        }

        public bool HasValidTreatmentRoute()
        {
            return selectedInterceptRoute != null;
        }

        public bool IsTreatmentRouteValid()
        {
            return InterceptCollection[TreatmentPolygon.Id].Contains(selectedInterceptRoute);
        }

        public bool RevalidateTreatmentRoute()
        {
            var routeUpdated = false;

            if (!IsTreatmentRouteValid())
            {
                selectedInterceptRoute = InterceptCollection[TreatmentPolygon.Id].FirstOrDefault();
                routeUpdated = true;
            }

            if (selectedInterceptRoute == null)
            {
                CanBeEnabled = false;
                if (lastEnabledState != null)
                    lastEnabledState = Enabled;
                Enabled = false;
            }
            else
            {
                if (CanBeEnabled == false)
                {
                    CanBeEnabled = true;
                    Enabled = lastEnabledState ?? true;
                }
            }

            return routeUpdated;
        }

        public string GetTreatmentAreaString()
        {
            string vertString = "";
            foreach (Coordinate position in TreatmentPolygon.Coordinates)
            {
                if (vertString.Length != 0)
                    vertString = vertString + ";";
                // Build string in latitude,longitude; format
                vertString = vertString + string.Format("{0},{1}", position.Y, position.X);
            }
            // Returns in radians
            return vertString;
        }

        public string GetEntryCoordianteString()
        {
            // Returns in radians - latitude,longitude
            string entryString = string.Format(
                "{0},{1}",
                AreaEntryCoordinate.Y,
                AreaEntryCoordinate.X);
            return entryString;
        }

        public string GetExitCoordinateString()
        {
            // Returns in radians - latitude,longitude
            string entryString = string.Format(
                "{0},{1}",
                AreaExitCoordinate.Y,
                AreaExitCoordinate.X);
            return entryString;
        }
    }
}
