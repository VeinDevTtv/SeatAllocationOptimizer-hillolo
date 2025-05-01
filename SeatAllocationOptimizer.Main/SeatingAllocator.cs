using System;
using System.Collections.Generic;
using System.Linq;

namespace SeatAllocationOptimizer.Main
{
    public class SeatingAllocator
    {
        private readonly int _planeRows;
        private readonly int _seatsPerRow;
        private BoardingGroup?[,] _seatMap; // Still useful for final map and validation
        // New structure to track available blocks: List per row, each containing tuples of (startSeat, length)
        private List<List<(int startSeat, int length)>> _availableBlocksPerRow;

        public SeatingAllocator(int planeRows = MainClass.DefaultPlaneRows, int seatsPerRow = MainClass.DefaultPlaneWidth)
        {
            if (planeRows <= 0) throw new ArgumentOutOfRangeException(nameof(planeRows), "Plane must have at least one row.");
            if (seatsPerRow <= 0) throw new ArgumentOutOfRangeException(nameof(seatsPerRow), "Rows must have at least one seat.");

            _planeRows = planeRows;
            _seatsPerRow = seatsPerRow;
            // Initialize map and available blocks
            InitializeSeating();
        }

        // Helper to initialize or reset the seating map and available blocks
        private void InitializeSeating()
        {
            _seatMap = new BoardingGroup?[_planeRows, _seatsPerRow];
            _availableBlocksPerRow = new List<List<(int startSeat, int length)>>(_planeRows);
            for (int r = 0; r < _planeRows; r++)
            {
                // Each row starts with one large block of available seats
                _availableBlocksPerRow.Add(new List<(int startSeat, int length)> { (0, _seatsPerRow) });
            }
        }


        // Represents the result of the allocation
        public class AllocationResult
        {
            public BoardingGroup?[,] SeatMap { get; }
            public double TotalRevenue { get; }
            public List<BoardingGroup> UnseatedGroups { get; }

            public AllocationResult(BoardingGroup?[,] seatMap, double totalRevenue, List<BoardingGroup> unseatedGroups)
            {
                SeatMap = seatMap;
                TotalRevenue = totalRevenue;
                UnseatedGroups = unseatedGroups;
            }
        }

        // Refactored allocation logic
        public AllocationResult AllocateSeats(List<BoardingGroup> initialGroups)
        {
            InitializeSeating(); // Reset map and available blocks

            var boardingQueue = new PriorityQueue<BoardingGroup, BoardingGroup>(new BoardingGroupComparator());
            foreach (var group in initialGroups)
            {
                boardingQueue.Enqueue(group, group);
            }

            double currentTotalRevenue = 0;
            List<BoardingGroup> unseatedGroups = new List<BoardingGroup>();

            while (boardingQueue.TryDequeue(out BoardingGroup? currentGroup, out _))
            {
                if (currentGroup == null) continue;

                bool seated = TryFindAndPlaceGroup(currentGroup, ref currentTotalRevenue);

                if (!seated)
                {
                    unseatedGroups.Add(currentGroup);
                }
            }

            Console.WriteLine($"Allocation complete. Total Revenue: {currentTotalRevenue:C}");
            if (unseatedGroups.Any())
            {
                Console.WriteLine($"Could not seat {unseatedGroups.Sum(g => g.SeatsNeeded)} passengers from {unseatedGroups.Count} groups.");
            }

            return new AllocationResult(_seatMap, currentTotalRevenue, unseatedGroups);
        }

        // New method replacing TrySeatGroup - searches all available blocks
        private bool TryFindAndPlaceGroup(BoardingGroup group, ref double currentTotalRevenue)
        {
            int seatsNeeded = group.SeatsNeeded;
            bool wantsWindow = group.Passengers.Any(p => p.WantsWindowSeat);
            (int row, int startSeat)? bestFit = null; // Store the best placement found so far
            bool bestFitSatisfiesPref = false;

            // Iterate through all rows and their available blocks
            for (int r = 0; r < _planeRows; r++)
            {
                // Iterate backwards through blocks to allow safe removal/modification while iterating
                for (int blockIndex = _availableBlocksPerRow[r].Count - 1; blockIndex >= 0; blockIndex--)
                {
                    var block = _availableBlocksPerRow[r][blockIndex];
                    // Iterate through possible start positions within this block
                    for (int s = block.startSeat; s <= block.startSeat + block.length - seatsNeeded; s++)
                    {
                        // Check if this placement is valid for the family (if applicable)
                        bool isValidFamilyPlacement = true;
                        if (group.IsFamily && group.FamilyGroup!.HasChildren)
                        {
                            // Pass the specific family object
                            isValidFamilyPlacement = IsFamilyPlacementValid(group.FamilyGroup!, r, s, seatsNeeded);
                        }

                        if (isValidFamilyPlacement)
                        {
                            bool currentSatisfiesPref = !wantsWindow || DoesBlockHaveWindow(s, seatsNeeded);

                            // Prioritize preferred placement. If found, take it immediately.
                            // Optimization: Could collect all valid placements and choose the best,
                            // but finding the *first* preferred is simpler and often sufficient.
                            if (currentSatisfiesPref)
                            {
                                PlaceGroupAndUpdateAvailability(group, r, s, seatsNeeded, blockIndex);
                                currentTotalRevenue += group.GroupRevenue;
                                return true; // Found preferred placement
                            }
                            // If it's a valid placement but doesn't satisfy preference,
                            // store it as a potential fallback *only if* we haven't found any fit yet.
                            else if (bestFit == null)
                            {
                                bestFit = (r, s);
                                // We don't set bestFitSatisfiesPref = false here, it defaults to false
                            }
                        }
                    } // End loop through start positions 's' within the block
                } // End loop through blocks in row 'r'
            } // End loop through rows 'r'

            // After checking all possibilities, if we found a fallback (non-preferred) option
            if (bestFit.HasValue)
            {
                (int r_fb, int s_fb) = bestFit.Value;
                // We need to find the original block index again to update availability correctly
                 int originalBlockIndex = FindBlockIndexContainingSeat(r_fb, s_fb);
                 if (originalBlockIndex != -1)
                 {
                     Console.WriteLine($"Placing group {group.Identifier} in non-preferred fallback seat at [{r_fb},{s_fb}].");
                     PlaceGroupAndUpdateAvailability(group, r_fb, s_fb, seatsNeeded, originalBlockIndex);
                     currentTotalRevenue += group.GroupRevenue;
                     return true; // Group seated successfully in fallback spot
                 }
                 else
                 {
                    // This should ideally not happen if bestFit was found correctly
                     Console.Error.WriteLine($"Error: Could not find original block for fallback placement of group {group.Identifier} at [{r_fb},{s_fb}]. Seating failed.");
                    return false;
                 }
            }

            // No suitable placement found anywhere
            return false;
        }
        
        // Helper to find the index of the available block containing a specific seat start
        private int FindBlockIndexContainingSeat(int row, int seat)
        {
            if (row < 0 || row >= _availableBlocksPerRow.Count) return -1;
            
            var blocksInRow = _availableBlocksPerRow[row];
            for (int i = 0; i < blocksInRow.Count; i++)
            {
                if (seat >= blocksInRow[i].startSeat && seat < blocksInRow[i].startSeat + blocksInRow[i].length)
                {
                    return i;
                }
            }
            return -1; // Seat not found in any available block
        }


        // Helper to check if a block includes a window seat
        private bool DoesBlockHaveWindow(int startSeatIndex, int seatsNeeded)
        {
            // Window seats are at index 0 and _seatsPerRow - 1
            bool startsAtWindow = (startSeatIndex == 0);
            bool endsAtWindow = (startSeatIndex + seatsNeeded - 1 == _seatsPerRow - 1);
            return startsAtWindow || endsAtWindow;
        }

        // Modified PlaceGroup to also update the _availableBlocksPerRow structure
        private void PlaceGroupAndUpdateAvailability(BoardingGroup group, int row, int startSeat, int seatsNeeded, int blockIndex)
        {
            // 1. Place group in the seat map (for visualization/final state)
            for (int i = 0; i < seatsNeeded; i++)
            {
                _seatMap[row, startSeat + i] = group;
            }

            // 2. Update the available blocks list for the affected row
            var blockToModify = _availableBlocksPerRow[row][blockIndex];
            _availableBlocksPerRow[row].RemoveAt(blockIndex); // Remove the old block

            // Check if there's remaining space before the placed group in the original block
            int prefixLength = startSeat - blockToModify.startSeat;
            if (prefixLength > 0)
            {
                _availableBlocksPerRow[row].Insert(blockIndex, (blockToModify.startSeat, prefixLength));
                 blockIndex++; // Adjust index because we inserted before the original position
            }

            // Check if there's remaining space after the placed group in the original block
            int suffixStart = startSeat + seatsNeeded;
            int suffixLength = (blockToModify.startSeat + blockToModify.length) - suffixStart;
            if (suffixLength > 0)
            {
                _availableBlocksPerRow[row].Insert(blockIndex, (suffixStart, suffixLength));
            }
            
            // Optional: Keep the list sorted by startSeat for efficiency? For now, simple insertion is fine.
             _availableBlocksPerRow[row].Sort((a, b) => a.startSeat.CompareTo(b.startSeat));
        }


        // Helper to check if a potential family placement keeps children adjacent to adults within the proposed block
        // Assumes the family members from the list are placed sequentially into the block.
        // Current limitations: Does not explicitly handle aisle separation within the block (e.g., for 3+3 seating).
        // It only ensures a child has an adult neighbor immediately to the left or right within the assigned seat block.
        // *No functional change needed here for the refactor, assuming sequential placement into the block*
        private bool IsFamilyPlacementValid(Family family, int row, int startSeat, int seatsNeeded)
        {
            List<Passenger> members = family.Members;
            // Basic checks
            if (members.Count == 0) return true; // Or handle earlier. Seems okay for now.
            if (members.Count != seatsNeeded) {
                 Console.Error.WriteLine($"Error: Family validity check failed for {family.FamilyId} - member count ({members.Count}) does not match seats needed ({seatsNeeded}).");
                 return false;
            }
            bool hasChild = family.HasChildren;
            if (!hasChild) return true; // No children, no adjacency requirement

            bool hasAdult = members.Any(p => p.IsAdult);
            if (!hasAdult) {
                 Console.WriteLine($"Warning: Cannot seat family {family.FamilyId} - contains children but no adults.");
                return false;
            }

            // Simulate the placement within the block to check adjacency accurately.
            for (int i = 0; i < seatsNeeded; i++)
            {
                Passenger currentPassenger = members[i];
                if (!currentPassenger.IsAdult) // Found a child
                {
                    bool adultNeighborFound = false;
                    // Check left neighbor (seat i-1 within the block)
                    if (i > 0 && members[i - 1].IsAdult) adultNeighborFound = true;
                    // Check right neighbor (seat i+1 within the block)
                    if (!adultNeighborFound && i < seatsNeeded - 1 && members[i + 1].IsAdult) adultNeighborFound = true;

                    if (!adultNeighborFound)
                    {
                         //Console.WriteLine($"Invalid placement for family {family.FamilyId}: Child at relative position {i} has no adjacent adult within the block [{row},{startSeat}] block length {seatsNeeded}.");
                        return false; // This placement is invalid according to the rule.
                    }
                }
            }
            return true; // Placement is valid
        }

        // PrintSeatingMap remains the same
        public void PrintSeatingMap()
        {
            Console.WriteLine("\n--- Seating Map ---");
            for (int r = 0; r < _planeRows; r++)
            {
                 Console.Write($"Row {r+1}: [");
                for (int s = 0; s < _seatsPerRow; s++)
                {
                    BoardingGroup? group = _seatMap[r, s];
                     string seatDisplay;
                     if (group == null) {
                         seatDisplay = " --- ";
                     } else {
                         string displayId = group.DisplayId.Length > 5 ? group.DisplayId.Substring(0, 5) : group.DisplayId.PadRight(5);
                         seatDisplay = $" {displayId} ";
                     }
                     Console.Write(seatDisplay);
                     if (s < _seatsPerRow - 1) Console.Write("|");
                }
                Console.WriteLine("]");
            }
            Console.WriteLine("-------------------\n");
        }

        // Need methods removed in the original refactoring proposal:
        // - CalculateRevenueFromMap() -> Removed as planned.
        // - TrySeatGroup() -> Replaced by TryFindAndPlaceGroup().
        // - UpdateGlobalPosition() -> Removed as planned.
    }
} 