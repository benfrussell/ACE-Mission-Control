using System;
using Xunit;
using Moq;
using ACE_Mission_Control.Core.Models;
using Pbdrone;
using System.Linq;

namespace ACE_Mission_Control_Tests
{
    public class CommandTests
    {
        [Fact]
        public void ResetMission_MissionResetEventWithSync_Sent()
        {
            // Arrange
            var mockRequestClient = new Mock<IRequestClient>();
            string sentCommand = "";
            mockRequestClient.Setup(c => c.SendCommand(It.IsAny<string>())).Callback<string>((c) => sentCommand = c);
            mockRequestClient.SetupGet(c => c.ReadyForCommand).Returns(true);

            var mockSubscriberClient = new Mock<ISubscriberClient>();

            var mockOBC = new Mock<IOnboardComputerClient>();
            mockOBC.SetupGet(c => c.IsDirectorConnected).Returns(true);
            mockOBC.SetupGet(c => c.DirectorRequestClient).Returns(mockRequestClient.Object);
            mockOBC.SetupGet(c => c.DirectorMonitorClient).Returns(mockSubscriberClient.Object);

            var mockMission = new Mock<IMission>();
            mockMission.SetupGet(m => m.MissionSet).Returns(true);

            var sut = new Drone(0, "test_drone", mockOBC.Object, mockMission.Object);
            sut.Synchronization = Drone.SyncState.Synchronized;

            // Act
            mockMission.Raise(m => m.ProgressReset += null, new EventArgs());

            // Assert
            Assert.Equal("reset_mission", sentCommand);
        }

        [Fact]
        public void SetEntry_RouteUpdateConnectedEnabled_Sent()
        {
            // Arrange
            var mockRequestClient = new Mock<IRequestClient>();
            string sentCommand = "";
            mockRequestClient.Setup(c => c.SendCommand(It.IsAny<string>())).Callback<string>((c) => sentCommand = c);
            mockRequestClient.SetupGet(c => c.ReadyForCommand).Returns(true);

            var mockSubscriberClient = new Mock<ISubscriberClient>();

            var mockOBC = new Mock<IOnboardComputerClient>();
            mockOBC.SetupGet(c => c.IsDirectorConnected).Returns(true);
            mockOBC.SetupGet(c => c.DirectorRequestClient).Returns(mockRequestClient.Object);
            mockOBC.SetupGet(c => c.DirectorMonitorClient).Returns(mockSubscriberClient.Object);

            var mockMission = new Mock<IMission>();
            mockMission.SetupGet(m => m.MissionSet).Returns(true);

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupGet(i => i.Enabled).Returns(true);

            var sut = new Drone(0, "test_drone", mockOBC.Object, mockMission.Object);
            sut.Synchronization = Drone.SyncState.Synchronized;

            var eventArgs = new InstructionRouteUpdatedEventArgs();
            eventArgs.Instruction = mockInstruction.Object;

            // Act
            mockMission.Raise(m => m.InstructionRouteUpdated += (sender, e) => { }, mockMission.Object, eventArgs);

            // Assert
            Assert.Contains("set_entry -id 0", sentCommand);
        }
    }
}
