using NetTopologySuite.Geometries;
using Pbdrone;
using System.Collections.Generic;
using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface ITreatmentInstruction
    {
        Coordinate AreaEntryCoordinate { get; }
        Coordinate AreaExitCoordinate { get; }
        AreaResult.Types.Status AreaStatus { get; set; }
        bool CanBeEnabled { get; }
        TreatmentInstruction.UploadStatus CurrentUploadStatus { get; set; }
        bool Enabled { get; set; }
        bool FirstInList { get; }
        bool FirstInstruction { get; }
        int ID { get; }
        bool LastInList { get; }
        bool LastInstruction { get; }
        string Name { get; }
        int? Order { get; }
        float Swath { get; }
        AreaScanPolygon TreatmentPolygon { get; }
        WaypointRoute TreatmentRoute { get; set; }
        IEnumerable<WaypointRoute> ValidTreatmentRoutes { get; }
        long LastEntryExitModification { get; }

        event PropertyChangedEventHandler PropertyChanged;

        string GetEntryCoordianteString();
        string GetExitCoordinateString();
        string GetTreatmentAreaString();
        bool HasValidTreatmentRoute();
        bool IsTreatmentRouteValid();
        void RevalidateTreatmentRoute();
        void SetOrder(int? order, bool firstInstruction, bool lastInstruction, bool firstItem, bool lastItem);
        void UpdateTreatmentArea(AreaScanPolygon treatmentArea);
    }
}