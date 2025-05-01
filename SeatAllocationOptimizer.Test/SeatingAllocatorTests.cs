using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeatAllocationOptimizer.Main;
using System.Collections.Generic;
using System.Linq;

namespace SeatAllocationOptimizer.Tests
{
    [TestClass]
    public class SeatingAllocatorTests
    {
        private Passenger P(bool adult, double revenue, bool window = false) => new Passenger(adult, revenue, 1, window);
        private Family F(string id, params Passenger[] members) {
            var fam = new Family(id);
            foreach(var p in members) fam.AddMember(p);
            return fam;
        }

        [TestMethod]
        public void AllocateSeats_BasicAllocation_SeatsHighRevenueFirst()
        {
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 3);
            var groups = new List<BoardingGroup>() {
                new BoardingGroup(P(true, 100)), // Lower revenue
                new BoardingGroup(P(true, 200))  // Higher revenue
            };

            var result = allocator.AllocateSeats(groups);

            Assert.AreEqual(300, result.TotalRevenue);
            Assert.AreEqual(0, result.UnseatedGroups.Count);
            Assert.IsNotNull(result.SeatMap[0, 0]); // Should be filled
            Assert.IsNotNull(result.SeatMap[0, 1]);
            Assert.IsNull(result.SeatMap[0, 2]);   // Third seat empty
            Assert.AreEqual(200, result.SeatMap[0, 0]?.GroupRevenue); // High revenue group should be first
        }

        [TestMethod]
        public void AllocateSeats_WindowPreference_SatisfiedWhenPossible()
        {
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 3);
            var groups = new List<BoardingGroup>() {
                new BoardingGroup(P(true, 100, window: true)), // Wants window
                new BoardingGroup(P(true, 50))                // Doesn't care
            };

            var result = allocator.AllocateSeats(groups);

            Assert.AreEqual(150, result.TotalRevenue);
            Assert.AreEqual(0, result.UnseatedGroups.Count);
            // Group wanting window should be at seat 0 or 2. Higher revenue passenger goes first.
            Assert.AreEqual(100, result.SeatMap[0, 0]?.GroupRevenue); // Window seat
            Assert.AreEqual(50, result.SeatMap[0, 1]?.GroupRevenue);
        }

         [TestMethod]
        public void AllocateSeats_WindowPreference_FallbackToNonWindow()
        {
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 2); // Only window seats
            var groups = new List<BoardingGroup>() {
                new BoardingGroup(P(true, 100, window: true)), // Wants window (A)
                new BoardingGroup(P(true, 50, window: true)),  // Wants window (B)
                new BoardingGroup(P(true, 150, window: false)) // High revenue, no pref (C)
            };

            var result = allocator.AllocateSeats(groups);

            Assert.AreEqual(300, result.TotalRevenue);
            Assert.AreEqual(0, result.UnseatedGroups.Count);
            // Allocator prioritizes C (150) first, takes seat 0 (window)
            // Then A (100), takes seat 1 (window)
            // Then B (50), cannot get window. Should fallback?
            // Wait, the implementation tries to seat high revenue first. C takes seat 0.
            // Then A wants window, seat 1 is available and is a window. A takes seat 1.
            // B wants window, no window seats left. Does TrySeatGroup allow fallback?
            // Yes, the logic should place B in a fallback if no preferred spot, but plane is full here.
            // Let's redefine test: 1 row, 3 seats. C->0, A->2 (window), B wants window, takes fallback 1.

             allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 3);
             result = allocator.AllocateSeats(groups);
             Assert.AreEqual(300, result.TotalRevenue); Assert.AreEqual(0, result.UnseatedGroups.Count);
             Assert.AreEqual(150, result.SeatMap[0, 0]?.GroupRevenue); // C at seat 0 (window)
             Assert.AreEqual(100, result.SeatMap[0, 2]?.GroupRevenue); // A at seat 2 (window)
             Assert.AreEqual(50, result.SeatMap[0, 1]?.GroupRevenue); // B at seat 1 (non-window fallback)
        }

        [TestMethod]
        public void AllocateSeats_FamilyPlacement_ValidSeatsKeptTogether()
        {
             var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 6);
             var familyA = F("A", P(true, 100), P(false, 50)); // Adult, Child
             var groups = new List<BoardingGroup>() {
                new BoardingGroup(familyA),
                new BoardingGroup(P(true, 20)) // Low revenue individual
             };

             var result = allocator.AllocateSeats(groups);
             Assert.AreEqual(170, result.TotalRevenue); Assert.AreEqual(0, result.UnseatedGroups.Count);
             Assert.AreEqual(familyA.FamilyId, result.SeatMap[0, 0]?.GroupId);
             Assert.AreEqual(familyA.FamilyId, result.SeatMap[0, 1]?.GroupId); // Family seated together
             Assert.AreEqual(20, result.SeatMap[0, 2]?.GroupRevenue); // Individual seated after
        }

        [TestMethod]
        public void AllocateSeats_FamilyPlacement_InvalidSkippedIfNoValidSpot()
        {
            // Test case: Family [C, A, C] size 3. SeatsPerRow=3. No valid arrangement possible.
             var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 3);
             var familyA = F("A", P(false, 50), P(true, 100), P(false, 50)); // C, A, C
             var individualB = P(true, 200); // High revenue individual
             var groups = new List<BoardingGroup>() {
                new BoardingGroup(familyA), // Low total revenue (200) but family
                new BoardingGroup(individualB) // High revenue (200)
             };

             // Comparator prioritizes high revenue first. Ind B takes seat 0.
             // Then Family A (needs 3 seats) tries to fit in [1, 2]. Fails (size).
             // It should be unseated.
             var result = allocator.AllocateSeats(groups);

             Assert.AreEqual(200, result.TotalRevenue); // Only individual B seated
             Assert.AreEqual(1, result.UnseatedGroups.Count);
             Assert.AreEqual(familyA.FamilyId, result.UnseatedGroups[0].GroupId);
             Assert.AreEqual(individualB.Revenue, result.SeatMap[0, 0]?.GroupRevenue);
             Assert.IsNull(result.SeatMap[0, 1]);
             Assert.IsNull(result.SeatMap[0, 2]);
        }

         [TestMethod]
        public void AllocateSeats_GroupLargerThanRowWidth_Unseated()
        {
            var allocator = new SeatingAllocator(planeRows: 2, seatsPerRow: 3);
            var familyA = F("A", P(true, 10), P(true, 10), P(true, 10), P(true, 10)); // Size 4
            var groups = new List<BoardingGroup>() { new BoardingGroup(familyA) };

            var result = allocator.AllocateSeats(groups);

            Assert.AreEqual(0, result.TotalRevenue);
            Assert.AreEqual(1, result.UnseatedGroups.Count);
            Assert.AreEqual(familyA.FamilyId, result.UnseatedGroups[0].GroupId);
        }

        [TestMethod]
        public void AllocateSeats_PlaneExactlyFull_AllSeated()
        {
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 2);
            var groups = new List<BoardingGroup>() {
                new BoardingGroup(P(true, 100)),
                new BoardingGroup(P(true, 50))
            };

            var result = allocator.AllocateSeats(groups);

            Assert.AreEqual(150, result.TotalRevenue);
            Assert.AreEqual(0, result.UnseatedGroups.Count);
            Assert.IsNotNull(result.SeatMap[0, 0]);
            Assert.IsNotNull(result.SeatMap[0, 1]);
        }

        [TestMethod]
        public void AllocateSeats_PlaneOverfilled_SomeUnseated()
        {
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 1);
            var groups = new List<BoardingGroup>() {
                new BoardingGroup(P(true, 100)), // P1
                new BoardingGroup(P(true, 50))   // P2
            };

            var result = allocator.AllocateSeats(groups);

            Assert.AreEqual(100, result.TotalRevenue); // Only highest revenue seated
            Assert.AreEqual(1, result.UnseatedGroups.Count);
            Assert.AreEqual(50, result.UnseatedGroups[0].GroupRevenue); // P2 unseated
            Assert.AreEqual(100, result.SeatMap[0, 0]?.GroupRevenue); // P1 seated
        }

        [TestMethod]
        public void AllocateSeats_FamilyPlacement_InvalidChildAdjacency_Unseated()
        {
            // Scenario: Plane 1x3. Family [C, A, C] revenue 200. Individual P revenue 10.
            // Comparator selects Family first.
            // Only block is [0,0] size 3. IsFamilyPlacementValid should fail for [C, A, C] in this block.
            // Family should be unseated. Individual P should be seated.
            var allocator = new SeatingAllocator(planeRows: 1, seatsPerRow: 3);
            var familyCAC = F("CAC", P(false, 50), P(true, 100), P(false, 50)); // C, A, C (Total Revenue 200)
            var individualP = P(true, 10); // Low revenue individual
            var groups = new List<BoardingGroup>() {
                new BoardingGroup(familyCAC),
                new BoardingGroup(individualP)
            };

            var result = allocator.AllocateSeats(groups);

            // Assertions:
            Assert.AreEqual(10, result.TotalRevenue, "Only the individual should be seated.");
            Assert.AreEqual(1, result.UnseatedGroups.Count, "The family should be unseated.");
            Assert.AreEqual(familyCAC.FamilyId, result.UnseatedGroups[0].GroupId, "Unseated group should be the family.");
            Assert.AreEqual(individualP.Revenue, result.SeatMap[0, 0]?.GroupRevenue, "Individual should be in the first seat.");
            Assert.IsNull(result.SeatMap[0, 1], "Seat 1 should be empty.");
            Assert.IsNull(result.SeatMap[0, 2], "Seat 2 should be empty.");
        }

    }
} 