using System;
using Xunit;
using Moq;
using ACE_Mission_Control.Core.Models;
using Pbdrone;
using System.Linq;
using NetTopologySuite.Geometries;
using System.Globalization;
using System.Threading;

namespace ACE_Mission_Control_Tests
{
    public class MissionTests
    {
        [Fact]
        public void ResetProgress_WithMissionProgress_ClearsInstructionAreaStatus()
        {
            // Arrange

            // Set up mission with mock treatment instruction
            var sut = new Mission();
            sut.Unlock();

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupProperty(i => i.AreaStatus);
            mockInstruction.Object.AreaStatus = MissionRoute.Types.Status.InProgress;
            sut.TreatmentInstructions.Add(mockInstruction.Object);

            // Act
            sut.ResetProgress();

            // Assert
            Assert.Equal(MissionRoute.Types.Status.NotStarted, sut.TreatmentInstructions.First().AreaStatus);
        }

        [Theory]
        [InlineData("en-CA")]
        [InlineData("fr-CA")]
        public void Start_Coordinate_String_Uses_Single_Comma(string applicationLanguage)
        {
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(applicationLanguage);
            }
            catch (CultureNotFoundException e)
            {
                return;
            }
            

            // Set up mission with mock start parameters
            var mockStartParams = new Mock<IStartTreatmentParameters>();
            mockStartParams.SetupGet(s => s.StartCoordinate).Returns(new Coordinate(45.123, -70.123));

            var sut = new Mission(mockStartParams.Object);

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupGet(i => i.FirstInstruction).Returns(true);
            mockInstruction.SetupGet(i => i.ID).Returns(0);
            sut.TreatmentInstructions.Add(mockInstruction.Object);

            var resultString = sut.GetStartCoordinateString(0);
            var commaOccurences = resultString.Count(c => c == ',');

            Assert.Equal(1, commaOccurences);
        }

        [Theory]
        [InlineData("en-CA")]
        [InlineData("fr-CA")]
        public void Stop_Coordinate_String_Uses_Single_Comma(string applicationLanguage)
        {
            try
            {
                Thread.CurrentThread.CurrentCulture = new CultureInfo(applicationLanguage);
            }
            catch (CultureNotFoundException e)
            {
                return;
            }


            // Set up mission 
            var sut = new Mission();

            var mockInstruction = new Mock<ITreatmentInstruction>();
            mockInstruction.SetupGet(i => i.AreaEntryExitCoordinates).Returns(
                new Tuple<Coordinate, Coordinate>(
                    new Coordinate(45.123, -70.123),
                    new Coordinate(45.456, -70.456)));
            mockInstruction.SetupGet(i => i.ID).Returns(0);
            sut.TreatmentInstructions.Add(mockInstruction.Object);

            var resultString = sut.GetStopCoordinateString(0);
            var commaOccurences = resultString.Count(c => c == ',');

            Assert.Equal(1, commaOccurences);
        }
    }
}
