using NetTopologySuite.Geometries;
using System;

namespace ACE_Mission_Control.Core.Models
{
    public interface IStartTreatmentParameters
    {
        StartTreatmentParameters.Mode DefaultNoProgressMode { get; set; }
        StartTreatmentParameters.Mode DefaultProgressMode { get; set; }
        long LastStartPropertyModification { get; }
        StartTreatmentParameters.Mode SelectedMode { get; set; }
        Coordinate StartCoordinate { get; }
        Waypoint.TurnType StartingTurnType { get; }

        event EventHandler<EventArgs> SelectedModeChangedEvent;
        event EventHandler<StartParametersChangedArgs> StartParametersChanged;

        void SetSelectedWaypoint(string waypointID, ITreatmentInstruction nextInstruction);
        bool UpdateParameters(ITreatmentInstruction nextInstruction, Coordinate lastPosition, bool justReturned);
    }
}