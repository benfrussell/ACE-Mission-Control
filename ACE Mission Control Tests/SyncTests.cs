using System;
using Xunit;
using Moq;
using ACE_Mission_Control.Core.Models;
using Pbdrone;
using System.Linq;

namespace ACE_Mission_Control_Tests
{
    public class SyncTests
    {
        [Fact]
        public void UnsentChanges_WithChangesUnsynced_IsNotEmpty()
        {
            // Arrange
            var mockRequestClient = new Mock<IRequestClient>();
            var mockMonitorClient = new Mock<ISubscriberClient>();

            var mockOBC = new Mock<IOnboardComputerClient>();
            mockOBC.SetupGet(c => c.IsDirectorConnected).Returns(true);
            mockOBC.SetupGet(c => c.DirectorRequestClient).Returns(mockRequestClient.Object);
            mockOBC.SetupGet(c => c.DirectorMonitorClient).Returns(mockMonitorClient.Object);

            var mockMission = new Mock<IMission>();
            mockMission.SetupGet(m => m.MissionSet).Returns(true);

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupGet(i => i.Enabled).Returns(true);

            var sut = new Drone(0, "test_drone", mockOBC.Object, mockMission.Object);
            sut.Synchronization = Drone.SyncState.NotSynchronized;

            // Load an unsent change by raising a route update while the drone isn't synchronized
            var routeUpdateArgs = new InstructionRouteUpdatedArgs();
            routeUpdateArgs.Instruction = mockInstruction.Object;

            // Act
            // Simulate a received config update
            // The drone must wait until it's knowledge on whether a mission is set on the OBC is current
            // If there are unsent route changes but no mission set, then sending the changes would fail
            mockMission.Raise(m => m.InstructionRouteUpdated += (sender, e) => { }, mockMission.Object, routeUpdateArgs);

            // Assert
            Assert.True(sut.HasUnsentChanges);
        }

        [Fact]
        public void UnsentChanges_WithChangesUnsyncedAfterConfigUpdate_IsEmpty()
        {
            // Arrange
            var mockRequestClient = new Mock<IRequestClient>();
            var mockMonitorClient = new Mock<ISubscriberClient>();

            var mockOBC = new Mock<IOnboardComputerClient>();
            mockOBC.SetupGet(c => c.IsDirectorConnected).Returns(true);
            mockOBC.SetupGet(c => c.DirectorRequestClient).Returns(mockRequestClient.Object);
            mockOBC.SetupGet(c => c.DirectorMonitorClient).Returns(mockMonitorClient.Object);

            var mockMission = new Mock<IMission>();
            mockMission.SetupGet(m => m.MissionSet).Returns(true);

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupGet(i => i.Enabled).Returns(true);

            var sut = new Drone(0, "test_drone", mockOBC.Object, mockMission.Object);
            sut.Synchronization = Drone.SyncState.NotSynchronized;

            // Load an unsent change by raising a route update while the drone isn't synchronized
            var routeUpdateArgs = new InstructionRouteUpdatedArgs();
            routeUpdateArgs.Instruction = mockInstruction.Object;
            mockMission.Raise(m => m.InstructionRouteUpdated += (sender, e) => { }, mockMission.Object, routeUpdateArgs);

            var configUpdateArgs = new MessageReceivedEventArgs();
            configUpdateArgs.MessageType = ACEEnums.MessageType.MissionConfig;
            configUpdateArgs.Message = new MissionConfig();

            // Act
            // Simulate a received config update
            // The drone must wait until it's knowledge on whether a mission is set on the OBC is current
            // If there are unsent route changes but no mission set, then sending the changes would fail
            mockMonitorClient.Raise(m => m.MessageReceivedEvent += (sender, e) => { }, mockMonitorClient.Object, configUpdateArgs);

            // Assert
            Assert.False(sut.HasUnsentChanges);
        }

        // Add paused state tests
    }
}
