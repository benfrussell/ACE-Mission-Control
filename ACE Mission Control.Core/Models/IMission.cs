using NetTopologySuite.Geometries;
using Pbdrone;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;

namespace ACE_Mission_Control.Core.Models
{
    public interface IMission : INotifyPropertyChanged
    {
        bool Activated { get; }
        List<string> AvailablePayloads { get; }
        bool CanBeModified { get; }
        bool CanBeReset { get; }
        bool CanToggleActivation { get; }
        bool CanUpload { get; }
        bool FlyThroughMode { get; }
        Coordinate LastPosition { get; }
        bool Locked { get; }
        bool MissionHasProgress { get; }
        bool MissionSet { get; }
        int SelectedPayload { get; set; }
        MissionStatus.Types.Stage Stage { get; }
        StartTreatmentParameters.Mode StartMode { get; set; }
        bool StopAndTurnStartMode { get; }
        int TreatmentDuration { get; set; }
        ObservableCollection<ITreatmentInstruction> TreatmentInstructions { get; set; }

        event EventHandler<InstructionAreasUpdatedEventArgs> InstructionAreasUpdated;
        event EventHandler<InstructionRouteUpdatedEventArgs> InstructionRouteUpdated;
        event EventHandler<EventArgs> ProgressReset;
        event EventHandler<EventArgs> StartParametersChangedEvent;

        ITreatmentInstruction GetNextInstruction();
        List<ITreatmentInstruction> GetRemainingInstructions();
        Coordinate GetStartCoordinate();
        string GetStartCoordinateString();
        void Lock();
        void ReorderInstruction(ITreatmentInstruction instruction, int newPosition);
        void ResetProgress();
        void ResetStatus();
        void SetInstructionUploaded(int id);
        void SetSelectedStartWaypoint(string waypointID);
        void Unlock();
        void UpdateMissionConfig(MissionConfig newConfig);
        void UpdateMissionStatus(MissionStatus newStatus);
    }
}