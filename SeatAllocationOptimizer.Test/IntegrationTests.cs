using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeatAllocationOptimizer.Main;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SeatAllocationOptimizer.Tests
{
    [TestClass]
    public class IntegrationTests
    {
        private string _testDir = "TestInputData_Integration";

        [TestInitialize]
        public void Setup()
        {
             if (!Directory.Exists(_testDir))
             {
                 Directory.CreateDirectory(_testDir);
             }
            // Simple scenario: 3 seats, 2 groups (1 high revenue, 1 low)
            File.WriteAllText(Path.Combine(_testDir, "simple.csv"),
                "1,Adult,50,-,No\n" +
                "2,Adult,100,-,No"
            );
            // Scenario: Family needs window, another group doesn't
            File.WriteAllText(Path.Combine(_testDir, "family_window.csv"),
                "1,Adult,100,A,Yes\n" + // Member 1 of Family A (wants window)
                "2,Child,50,A,No\n" +  // Member 2 of Family A
                "3,Adult,75,-,No"      // Individual
            );
            // Scenario: Plane too small
            File.WriteAllText(Path.Combine(_testDir, "overfill.csv"),
                "1,Adult,100,-,No\n" +
                "2,Adult,90,-,No\n" +
                "3,Adult,80,-,No"
            );
        }

        [TestCleanup]
        public void Cleanup()
        {
             if (Directory.Exists(_testDir))
             {
                try { Directory.Delete(_testDir, true); } catch {} 
             }
        }

        [TestMethod]
        public void Integration_SimpleInput_CorrectAllocationAndRevenue()
        {
            string inputFile = Path.Combine(_testDir, "simple.csv");
            List<BoardingGroup> boardingGroups = DataParser.ParseInput(inputFile);
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 3);

            SeatingAllocator.AllocationResult result = allocator.AllocateSeats(boardingGroups);

            // Assertions
            Assert.AreEqual(150, result.TotalRevenue, "Total revenue should be sum of both groups");
            Assert.AreEqual(0, result.UnseatedGroups.Count, "All groups should be seated");
            Assert.IsNotNull(result.SeatMap[0, 0]);
            Assert.IsNotNull(result.SeatMap[0, 1]);
            Assert.IsNull(result.SeatMap[0, 2]);
            Assert.AreEqual(100, result.SeatMap[0, 0]?.GroupRevenue, "Higher revenue group should be seated first");
            Assert.AreEqual(50, result.SeatMap[0, 1]?.GroupRevenue);
        }

        [TestMethod]
        public void Integration_FamilyWindowPreference_CorrectPlacement()
        {
            string inputFile = Path.Combine(_testDir, "family_window.csv");
            List<BoardingGroup> boardingGroups = DataParser.ParseInput(inputFile);
            // Plane: 1 Row, 4 Seats
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 4);

            SeatingAllocator.AllocationResult result = allocator.AllocateSeats(boardingGroups);

            // Family A (150 revenue, needs 2 seats, wants window) vs Individual (75 revenue)
            // Family A should be seated first due to higher revenue.
            // It wants a window, so it should occupy seats [0, 1] or [2, 3]. Let's assume [0, 1].
            // Individual should be seated next, e.g., at seat 2.

            Assert.AreEqual(225, result.TotalRevenue);
            Assert.AreEqual(0, result.UnseatedGroups.Count);

            var familyGroup = boardingGroups.First(g => g.IsFamily);
            var individualGroup = boardingGroups.First(g => !g.IsFamily);

            Assert.AreEqual(familyGroup.GroupId, result.SeatMap[0, 0]?.GroupId, "Family should be at window seat 0");
            Assert.AreEqual(familyGroup.GroupId, result.SeatMap[0, 1]?.GroupId, "Family should occupy adjacent seat 1");
            Assert.AreEqual(individualGroup.GroupId, result.SeatMap[0, 2]?.GroupId, "Individual should be next");
            Assert.IsNull(result.SeatMap[0, 3]);
        }

        [TestMethod]
        public void Integration_OverfilledPlane_HighestRevenueSeatedUnseatedTracked()
        {
            string inputFile = Path.Combine(_testDir, "overfill.csv");
            List<BoardingGroup> boardingGroups = DataParser.ParseInput(inputFile);
             // Plane: 1 Row, 2 Seats - Cannot fit all 3 groups
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 2);

            SeatingAllocator.AllocationResult result = allocator.AllocateSeats(boardingGroups);

            // Expecting groups with 100 and 90 revenue to be seated.
            Assert.AreEqual(190, result.TotalRevenue);
            Assert.AreEqual(1, result.UnseatedGroups.Count, "One group should be unseated");
            Assert.AreEqual(80, result.UnseatedGroups[0].GroupRevenue, "Lowest revenue group should be unseated");

            Assert.IsNotNull(result.SeatMap[0, 0]);
            Assert.IsNotNull(result.SeatMap[0, 1]);
            Assert.AreEqual(100, result.SeatMap[0, 0]?.GroupRevenue);
            Assert.AreEqual(90, result.SeatMap[0, 1]?.GroupRevenue);
        }
    }
} 