using NetTopologySuite.Geometries;
using System;
using System.Collections.Generic;
using System.Linq;
using Pbdrone;
using UGCS.Sdk.Protocol.Encoding;
using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Globalization;
using System.Reflection;

namespace ACE_Mission_Control.Core.Models
{
    [AttributeUsage(AttributeTargets.Property)]
    public class SyncedPropertyAttribute : Attribute { }

    public class TreatmentInstruction : INotifyPropertyChanged, ITreatmentInstruction
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
                LastSyncedPropertyModification = DateTimeOffset.Now.ToUnixTimeMilliseconds();
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
                SelectedInterceptRoute = InterceptCollection.GetIntercepts(TreatmentPolygon.Id).FirstOrDefault(r => r.WaypointRoute == value);
                NotifyPropertyChanged();
            }
        }

        private MissionRoute.Types.Status areaStatus;
        public MissionRoute.Types.Status AreaStatus
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

        private Coordinate areaEntryCoordinate;
        [SyncedProperty]
        public Coordinate AreaEntryCoordinate
        {
            get => areaEntryCoordinate;
            set
            {
                if (areaEntryCoordinate == value)
                    return;
                areaEntryCoordinate = value;
                NotifyPropertyChanged();
            }
        }

        private Coordinate areaExitCoordinate;
        [SyncedProperty]
        public Coordinate AreaExitCoordinate
        {
            get => areaExitCoordinate;
            set
            {
                if (areaExitCoordinate == value)
                    return;
                areaExitCoordinate = value;
                NotifyPropertyChanged();
            }
        }

        public IEnumerable<WaypointRoute> ValidTreatmentRoutes
        {
            get => InterceptCollection.GetIntercepts(TreatmentPolygon.Id).Select(r => r.WaypointRoute);
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
        [SyncedProperty]
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

        // Order includes both enabled and disabled instructions
        private int? order;
        [SyncedProperty]
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
        // Mark this as synced because being the first instruction changes whether this instruction's start point is set by start mode or by the area entry
        // It's not actually synced with the drone
        [SyncedProperty]
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

        private long lastSyncedPropertyModification;
        public long LastSyncedPropertyModification
        {
            get => lastSyncedPropertyModification;
            protected set
            {
                if (lastSyncedPropertyModification == value)
                    return;
                lastSyncedPropertyModification = value;
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
            AreaStatus = MissionRoute.Types.Status.NotStarted;

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

            InterceptCollection.AreaInterceptsModified += InterceptCollection_AreaInterceptsModified;

            RevalidateTreatmentRoute();
        }

        private void InterceptCollection_AreaInterceptsModified(object sender, InterceptCollectionChangedArgs e)
        {
            if (TreatmentPolygon != null && e.AreaIDsAffected.Contains(TreatmentPolygon.Id))
            {
                NotifyPropertyChanged("ValidTreatmentRoutes");
                if (e.InterceptsAffected.Contains(SelectedInterceptRoute))
                {
                    RevalidateTreatmentRoute();
                }
            }
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
            var interceptingRoutes = InterceptCollection.GetIntercepts(TreatmentPolygon.Id);
            return interceptingRoutes.Any(i => i.WaypointRoute.Id == SelectedInterceptRoute.WaypointRoute.Id);
        }

        // Check that the current treatment route is still valid (still intercepts the treatment area)
        // Try to replace it with a valid route if it is not
        public void RevalidateTreatmentRoute()
        {
            if (SelectedInterceptRoute == null || !IsTreatmentRouteValid())
            {
                var previousRoute = SelectedInterceptRoute;
                var newRoute = InterceptCollection.GetIntercepts(TreatmentPolygon.Id).FirstOrDefault();
                if (previousRoute != newRoute)
                    SelectedInterceptRoute = newRoute;
            }

            if (SelectedInterceptRoute == null)
            {
                AreaEntryCoordinate = null;
                AreaExitCoordinate = null;

                CanBeEnabled = false;
                if (lastEnabledState != null)
                    lastEnabledState = Enabled;
                Enabled = false;
            }
            else
            {
                AreaEntryCoordinate = SelectedInterceptRoute.EntryCoordinate;
                AreaExitCoordinate = SelectedInterceptRoute.ExitCoordinate;

                if (CanBeEnabled == false)
                {
                    CanBeEnabled = true;
                    Enabled = lastEnabledState ?? true;
                }
            }
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

        private void NotifyPropertyChanged([CallerMemberName] String propertyName = "")
        {
            PropertyInfo propertyInfo = null;
            try
            {
                propertyInfo = typeof(TreatmentInstruction).GetProperty(propertyName);
            }
            catch (ArgumentNullException) { }
            catch (AmbiguousMatchException) { }

            // Updated SyncedPropertyModification if the property has the SyncedProperty attribute
            if (propertyInfo != null)
            {
                var attributes = propertyInfo.GetCustomAttributes(false).ToList();

                if (attributes.Any(a => a.GetType() == typeof(SyncedPropertyAttribute)))
                    LastSyncedPropertyModification = DateTimeOffset.Now.ToUnixTimeMilliseconds();
            }
            
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }
}
