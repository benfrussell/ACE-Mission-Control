﻿using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using Pbdrone;
using UGCS.Sdk.Protocol.Encoding;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;

namespace ACE_Mission_Control.Core.Models
{
    public class TreatmentInstruction : INotifyPropertyChanged
    {
        public static WaypointRouteInterceptCollection InterceptCollection = new WaypointRouteInterceptCollection();

        public enum UploadStatus
        {
            NotUploaded = 0,
            Changes = 1,
            PreviousUpload = 2,
            Uploaded = 3
        }

        public event PropertyChangedEventHandler PropertyChanged;

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
        private WaypointRouteIntercept SelectedInterceptRoute
        {
            get => selectedInterceptRoute;
            set
            {
                if (selectedInterceptRoute == value)
                    return;
                selectedInterceptRoute = value;
                NotifyPropertyChanged("TreatmentRoute");
            }
        }

        public WaypointRoute TreatmentRoute 
        {
            get
            {
                if (SelectedInterceptRoute == null)
                    RevalidateTreatmentRoute();
                return SelectedInterceptRoute != null ? SelectedInterceptRoute.WaypointRoute : null;
            } 
            set
            {
                if (value == null || (SelectedInterceptRoute != null && SelectedInterceptRoute.WaypointRoute == value))
                    return;
                SelectedInterceptRoute = InterceptCollection[TreatmentPolygon.Id].FirstOrDefault(r => r.WaypointRoute == value);
                NotifyPropertyChanged();
            }
        }

        private AreaResult.Types.Status areaStatus;
        public AreaResult.Types.Status AreaStatus 
        { 
            get => areaStatus;
            set
            {
                if (areaStatus == value)
                    return;
                areaStatus = value;
                NotifyPropertyChanged();
            }
        }

        public Coordinate AreaEntryCoordinate
        {
            get
            {
                if (SelectedInterceptRoute == null)
                    RevalidateTreatmentRoute();
                if (SelectedInterceptRoute != null)
                    return SelectedInterceptRoute.EntryCoordinate;
                return null;
            }
        }

        public Coordinate AreaExitCoordinate
        {
            get
            {
                if (SelectedInterceptRoute == null)
                    RevalidateTreatmentRoute();
                if (SelectedInterceptRoute != null)
                    return SelectedInterceptRoute.ExitCoordinate;
                return null;
            }
        }

        public IEnumerable<WaypointRoute> ValidTreatmentRoutes
        {
            get => InterceptCollection[TreatmentPolygon.Id].Select(r => r.WaypointRoute);
        }

        private UploadStatus currentUploadStatus;
        public UploadStatus CurrentUploadStatus 
        { 
            get => currentUploadStatus; 
            set
            {
                if (currentUploadStatus == value)
                    return;
                currentUploadStatus = value;
                NotifyPropertyChanged();
            }
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
                NotifyPropertyChanged();
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
                NotifyPropertyChanged();
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
                NotifyPropertyChanged();
            }
        }

        // Keep track of this positional data so reordering can be done easier in an item by item view
        private int? order;
        public int? Order 
        {
            get => order; 
            private set
            {
                if (order == value)
                    return;
                order = value;
                NotifyPropertyChanged();
            }
        }

        private bool firstInstruction;
        public bool FirstInstruction
        {
            get => firstInstruction;
            private set
            {
                if (firstInstruction == value)
                    return;
                firstInstruction = value;
                NotifyPropertyChanged();
            }
        }

        private bool lastInstruction;
        public bool LastInstruction
        {
            get => lastInstruction;
            private set
            {
                if (lastInstruction == value)
                    return;
                lastInstruction = value;
                NotifyPropertyChanged();
            }
        }

        private bool firstInList;
        public bool FirstInList 
        { 
            get => firstInList; 
            private set
            {
                if (firstInList == value)
                    return;
                firstInList = value;
                NotifyPropertyChanged();
            } 
        }

        private bool lastInList;
        public bool LastInList
        {
            get => lastInList;
            private set
            {
                if (lastInList == value)
                    return;
                lastInList = value;
                NotifyPropertyChanged();
            }
        }

        private int id;
        public int ID
        {
            get => id;
            private set
            {
                if (id == value)
                    return;
                id = value;
                NotifyPropertyChanged();
            }
        }

        // Save the last enabled state so we can put enabled back into the user's preffered state if they fix the CanBeEnabled problem
        // Starts as null and will only save the state after being enabled for the first time
        private bool? lastEnabledState;

        public TreatmentInstruction(AreaScanPolygon treatmentArea)
        {
            TreatmentPolygon = treatmentArea;
            ID = treatmentArea.SequentialID;
            AreaStatus = AreaResult.Types.Status.NotStarted;

            // Swath is generally half of the side distance (metres)
            if (!treatmentArea.Parameters.ContainsKey("sideDistance"))
                throw new Exception("Tried to create a treatment instruction with an area that does not have a side distance specified.");
            var sideDistance = treatmentArea.Parameters["sideDistance"];

            Swath = float.Parse(sideDistance, CultureInfo.InvariantCulture) / 2;
            CurrentUploadStatus = UploadStatus.NotUploaded;

            Order = null;
            FirstInstruction = false;
            FirstInList = false;
            LastInList = false;

            Enabled = true;
            lastEnabledState = null;

            RevalidateTreatmentRoute();
        }

        public void UpdateValidRoutes()
        {
            NotifyPropertyChanged("ValidTreatmentRoutes");
            NotifyPropertyChanged("TreatmentRoute");
        }

        // Update the treatment route with any changes made to it
        public void UpdateTreatmentRoute()
        {
            var matchedRoute = InterceptCollection[TreatmentPolygon.Id].FirstOrDefault(i => i.WaypointRoute.Id == SelectedInterceptRoute.WaypointRoute.Id);
            SelectedInterceptRoute = null;

            RevalidateTreatmentRoute();
        }

        public void UpdateTreatmentArea(AreaScanPolygon treatmentArea)
        {
            TreatmentPolygon = treatmentArea;

            if (!treatmentArea.Parameters.ContainsKey("sideDistance"))
                throw new Exception("Tried to update a treatment instruction with an area that does not have a side distance specified.");
            var sideDistance = treatmentArea.Parameters["sideDistance"];

            Swath = float.Parse(sideDistance, CultureInfo.InvariantCulture) / 2;

            var nameChanged = TreatmentPolygon.Name != treatmentArea.Name;
            if (nameChanged)
                NotifyPropertyChanged("Name");

            RevalidateTreatmentRoute();
            if (CurrentUploadStatus == UploadStatus.Uploaded)
                CurrentUploadStatus = UploadStatus.Changes;
        }

        public bool HasValidTreatmentRoute()
        {
            return SelectedInterceptRoute != null;
        }

        public bool IsTreatmentRouteValid()
        {
            return InterceptCollection[TreatmentPolygon.Id].Contains(SelectedInterceptRoute);
        }

        // Check that the current treatment route is still valid (still intercepts the treatment area)
        // Try to replace it with a valid route if it is not
        public bool RevalidateTreatmentRoute()
        {
            var routeUpdated = false;

            if (SelectedInterceptRoute == null || !IsTreatmentRouteValid())
            {
                var previousRoute = SelectedInterceptRoute;
                var newRoute = InterceptCollection[TreatmentPolygon.Id].FirstOrDefault();
                if (previousRoute != newRoute)
                {
                    SelectedInterceptRoute = newRoute;
                    routeUpdated = true;
                }
                    
            }

            if (SelectedInterceptRoute == null)
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

            if (routeUpdated)
                UpdateValidRoutes();

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

        public void SetOrder(int? order, bool firstInstruction, bool lastInstruction, bool firstItem, bool lastItem)
        {
            if (Order != order)
            {
                if (CurrentUploadStatus == UploadStatus.Uploaded)
                    CurrentUploadStatus = UploadStatus.Changes;
            }

            Order = order;
            FirstInstruction = firstInstruction;
            LastInstruction = lastInstruction;
            FirstInList = firstItem;
            LastInList = lastItem;
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

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
