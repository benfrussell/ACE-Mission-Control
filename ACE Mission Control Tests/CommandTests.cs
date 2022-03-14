using System;
using Xunit;
using Moq;
using ACE_Mission_Control.Core.Models;
using Pbdrone;
using System.Linq;
using System.Collections.Generic;

namespace ACE_Mission_Control_Tests
{
    public class CommandTests
    {
        private Mock<IOnboardComputerClient> ArrangeMockOBCForCommands(Mock<IRequestClient> requestClient)
        {
            requestClient.SetupGet(c => c.ReadyForCommand).Returns(true);

            var mockSubscriberClient = new Mock<ISubscriberClient>();

            var mockOBC = new Mock<IOnboardComputerClient>();
            mockOBC.SetupGet(c => c.IsDirectorConnected).Returns(true);
            mockOBC.SetupGet(c => c.DirectorRequestClient).Returns(requestClient.Object);
            mockOBC.SetupGet(c => c.DirectorMonitorClient).Returns(mockSubscriberClient.Object);

            return mockOBC;
        }

        private Mock<IMission> ArrangeMockMissionWithInstruction(
            int id, int order, long areaModTime, 
            string areaString, string startCoord, string stopCoord, 
            long propModTime, MissionRoute.Types.Status status, bool enabled, 
            Waypoint.TurnType turn, TreatmentInstruction.UploadStatus upload)
        {
            var mockMission = new Mock<IMission>();
            mockMission.SetupGet(m => m.MissionSet).Returns(true);
            mockMission.Setup(m => m.GetLastAreaModificationTime(It.Is<int>(i => i == id)))
                .Returns(areaModTime);
            mockMission.Setup(m => m.GetStartCoordinateString(It.Is<int>(i => i == id)))
                .Returns(startCoord);
            mockMission.Setup(m => m.GetStopCoordinateString(It.Is<int>(i => i == id)))
                .Returns(stopCoord);
            mockMission.Setup(m => m.GetLastPropertyModificationTime(It.Is<int>(i => i == id)))
                .Returns(propModTime);
            mockMission.Setup(m => m.GetStartingTurnType(It.Is<int>(i => i == id)))
                .Returns(turn);

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupGet(i => i.CurrentUploadStatus).Returns(upload);
            mockInstruction.SetupGet(i => i.Enabled).Returns(true);
            mockInstruction.SetupGet(i => i.ID).Returns(id);
            mockInstruction.SetupGet(i => i.Order).Returns(order);
            mockInstruction.SetupGet(i => i.AreaStatus).Returns(status);

            mockMission.Setup(m => m.GetInstructionByID(It.Is<int>(i => i == id)))
                .Returns(mockInstruction.Object);

            return mockMission;
        }

        [Fact]
        public void ResetMission_MissionResetEventSynced_Sent()
        {
            // Arrange
            string sentCommand = "";
            var mockRequestClient = new Mock<IRequestClient>();
            mockRequestClient.Setup(c => c.SendCommand(It.IsAny<string>())).Callback<string>((c) => sentCommand = c);

            var mockMission = new Mock<IMission>();
            mockMission.SetupGet(m => m.MissionSet).Returns(true);

            var sut = new Drone(0, "test_drone", ArrangeMockOBCForCommands(mockRequestClient).Object, mockMission.Object);
            sut.Synchronization = Drone.SyncState.Synchronized;

            // Act
            mockMission.Raise(m => m.ProgressReset += null, new EventArgs());

            // Assert
            Assert.Equal("reset_mission", sentCommand);
        }

        [Theory]
        [InlineData(true, Waypoint.TurnType.FlyThrough, "-fly_through")]
        [InlineData(false, Waypoint.TurnType.FlyThrough, "-fly_through")]
        [InlineData(true, Waypoint.TurnType.StopAndTurn, "-stopandgo")]
        [InlineData(false, Waypoint.TurnType.StopAndTurn, "-stopandgo")]
        public void Drone_Sends_Correct_Turn_Type_Command(
            bool instructionUploaded, 
            Waypoint.TurnType targetType, 
            string expectedCommandContains)
        {
            // Arrange
            string sentCommand = "";
            var mockRequestClient = new Mock<IRequestClient>();
            mockRequestClient.Setup(c => c.SendCommand(It.IsAny<string>())).Callback<string>((c) => sentCommand = c);

            var mockMission = ArrangeMockMissionWithInstruction(
                0, 1, 0, "start", "stop", "area", 0, MissionRoute.Types.Status.NotStarted, true, targetType, 
                instructionUploaded ? TreatmentInstruction.UploadStatus.Uploaded : TreatmentInstruction.UploadStatus.NotUploaded);

            var sut = new Drone(
                0, 
                "test_drone", 
                ArrangeMockOBCForCommands(mockRequestClient).Object,
                mockMission.Object);

            sut.Synchronization = Drone.SyncState.Synchronized;

            // Act
            mockMission.Raise(m => m.InstructionSyncedPropertyUpdated += null, new InstructionSyncedPropertyUpdatedArgs(0, new List<string> { "StartingTurnType" }));

            // Assert
            Assert.Contains(expectedCommandContains, sentCommand);
        }
    }
}
