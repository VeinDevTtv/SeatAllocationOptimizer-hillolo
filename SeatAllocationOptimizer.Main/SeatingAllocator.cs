using System;
using System.Collections.Generic;
using System.Linq;

namespace SeatAllocationOptimizer.Main
{
    public class SeatingAllocator
    {
        private readonly int _planeRows;
        private readonly int _seatsPerRow;
        private BoardingGroup?[,] _seatMap; // Using nullable BoardingGroup to store which group is in which seat

        public SeatingAllocator(int planeRows = MainClass.DefaultPlaneRows, int seatsPerRow = MainClass.DefaultPlaneWidth)
        {
            // README mentions 20 seats across 4 rows initially, but also planeRows = 33 for 200 seats.
            // Let's make it configurable. Assuming 200 seats might mean 33 rows * 6 seats/row + 2 extra? Or maybe 40*5? Let's stick to configurable.
             if (planeRows <= 0) throw new ArgumentOutOfRangeException(nameof(planeRows), "Plane must have at least one row.");
             if (seatsPerRow <= 0) throw new ArgumentOutOfRangeException(nameof(seatsPerRow), "Rows must have at least one seat.");

            _planeRows = planeRows;
            _seatsPerRow = seatsPerRow;
            _seatMap = new BoardingGroup?[_planeRows, _seatsPerRow];
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


        // Corresponds to the idealRevenue method description
        public AllocationResult AllocateSeats(List<BoardingGroup> initialGroups)
        {
            _seatMap = new BoardingGroup?[_planeRows, _seatsPerRow]; // Reset map for new allocation
            var boardingQueue = new PriorityQueue<BoardingGroup, BoardingGroup>(new BoardingGroupComparator());
            foreach (var group in initialGroups)
            {
                boardingQueue.Enqueue(group, group);
            }

            int currentRow = 0;
            int currentSeat = 0;
            int totalSeatedPassengersCount = 0; // To track total passengers, not just groups
            List<BoardingGroup> temporarilyUnseated = new List<BoardingGroup>();

            // Loop until the main queue is empty and no more progress can be made in a cycle
            while (boardingQueue.Count > 0)
            {
                int passengersSeatedThisCycle = 0;

                // Process the current queue
                while (boardingQueue.TryDequeue(out BoardingGroup? currentGroup, out _))
                {
                     if (currentGroup == null) continue; // Should not happen with non-null enqueue, but safety check

                    bool seated = TrySeatGroup(currentGroup, ref currentRow, ref currentSeat);

                    if (seated)
                    {
                        passengersSeatedThisCycle += currentGroup.SeatsNeeded;
                    }
                    else
                    {
                        // Could not seat group in the current state, hold it back
                        temporarilyUnseated.Add(currentGroup);
                    }
                }

                // After trying to seat everyone once, re-enqueue those held back
                if (temporarilyUnseated.Any())
                {
                    // If no one was seated in this cycle, and we still have people waiting,
                    // advance to the next row to potentially find space.
                    // README: "If no new passengers were seated during a round, the seating process advances to the next row."
                    if (passengersSeatedThisCycle == 0 && currentRow < _planeRows - 1)
                    {
                         Console.WriteLine($"No progress made in row {currentRow}, advancing to next row.");
                        currentRow++;
                        currentSeat = 0; // Start at the beginning of the new row
                    }

                    // Re-enqueue for the next attempt cycle
                    foreach(var group in temporarilyUnseated)
                    {
                         boardingQueue.Enqueue(group, group);
                    }
                    temporarilyUnseated.Clear();

                     // Avoid infinite loops if we keep advancing rows but can't seat anyone
                     if (passengersSeatedThisCycle == 0 && currentRow >= _planeRows - 1 && boardingQueue.Count > 0) {
                         Console.WriteLine("Warning: Cannot seat remaining groups. Plane might be full or groups too large for remaining spaces.");
                         break; // Exit main loop if stuck
                     }
                }
                // If temporarilyUnseated is empty, it means everyone from the last cycle got seated or the initial queue was empty.
                // The main while loop condition (boardingQueue.Count > 0) handles exit.

                totalSeatedPassengersCount += passengersSeatedThisCycle;
            }


            // Calculate final revenue from the map
            double totalRevenue = CalculateRevenueFromMap();

            // Collect any groups that remained in the queue (e.g., if we broke the loop early)
             List<BoardingGroup> finalUnseated = new List<BoardingGroup>();
             while(boardingQueue.TryDequeue(out BoardingGroup? group, out _)) {
                 if (group != null) finalUnseated.Add(group);
             }
             finalUnseated.AddRange(temporarilyUnseated); // Add any leftover from the last cycle

             Console.WriteLine($"Allocation complete. Total Revenue: {totalRevenue:C}");
             if(finalUnseated.Any()) {
                Console.WriteLine($"Could not seat {finalUnseated.Sum(g => g.SeatsNeeded)} passengers from {finalUnseated.Count} groups.");
             }


            return new AllocationResult(_seatMap, totalRevenue, finalUnseated);
        }

        // Attempts to seat a group starting from the current position, considering window preference
        private bool TrySeatGroup(BoardingGroup group, ref int startRow, ref int startSeat)
        {
            int seatsNeeded = group.SeatsNeeded;
            bool wantsWindow = group.Passengers.Any(p => p.WantsWindowSeat);

            (int row, int seat)? fallbackOption = null; // Store a potential non-preferred location

             // Iterate through rows starting from startRow
            for (int r = startRow; r < _planeRows; r++)
            {
                // Determine starting seat for this row
                int s = (r == startRow) ? startSeat : 0;

                // Check remaining seats in the current row
                while (s <= _seatsPerRow - seatsNeeded)
                {
                    // Check if the block of seats is physically available
                    bool blockAvailable = true;
                    for (int i = 0; i < seatsNeeded; i++)
                    {
                        if (_seatMap[r, s + i] != null)
                        {
                            blockAvailable = false;
                            s += i; // Jump ahead: no point checking blocks starting within this occupied space
                            break;
                        }
                    }

                    if (blockAvailable)
                    {
                        bool isValidFamilyPlacement = true;
                        // Additional check for family groups: ensure valid placement
                        if (group.IsFamily && group.FamilyGroup!.HasChildren) {
                            isValidFamilyPlacement = IsFamilyPlacementValid(group.FamilyGroup!, r, s, seatsNeeded);
                        }

                        if (isValidFamilyPlacement)
                        {
                            bool satisfiesPreference = !wantsWindow || DoesBlockHaveWindow(s, seatsNeeded);

                            if (satisfiesPreference)
                            {
                                // Preferred placement found, place the group
                                PlaceGroup(group, r, s, seatsNeeded);
                        // Update the global position for the *next* attempt
                                UpdateGlobalPosition(ref startRow, ref startSeat, r, s + seatsNeeded);
                                return true; // Group seated successfully in preferred spot
                            }
                            else if (fallbackOption == null)
                            {
                                // Block is valid but doesn't satisfy window preference.
                                // Store it as a fallback if we haven't found one yet.
                                fallbackOption = (r, s);
                         }
                        }
                        // If family placement is invalid, blockAvailable becomes effectively false for this spot.
                    }

                        // Move to the next possible starting seat in this row
                        s++;
                    }
                // Finished checking row 'r'
            }

            // If we finished searching all rows without finding a preferred spot,
            // check if we found a fallback option.
            if (fallbackOption.HasValue)
            {
                (int r_fb, int s_fb) = fallbackOption.Value;
                Console.WriteLine($"Placing group {group.Identifier} in non-preferred (non-window) fallback seat at [{r_fb},{s_fb}].");
                PlaceGroup(group, r_fb, s_fb, seatsNeeded);
                // Update the global position based on where we actually placed them
                UpdateGlobalPosition(ref startRow, ref startSeat, r_fb, s_fb + seatsNeeded);
                return true; // Group seated successfully in fallback spot
            }

            // If we've checked all rows and couldn't seat the group anywhere (preferred or fallback)
            return false;
        }

        // Helper to check if a block includes a window seat
        private bool DoesBlockHaveWindow(int startSeatIndex, int seatsNeeded)
        {
             // Window seats are at index 0 and _seatsPerRow - 1
             bool startsAtWindow = (startSeatIndex == 0);
             bool endsAtWindow = (startSeatIndex + seatsNeeded - 1 == _seatsPerRow - 1);
             return startsAtWindow || endsAtWindow;
        }

        // Helper to place the group in the seat map
        private void PlaceGroup(BoardingGroup group, int row, int startSeat, int seatsNeeded)
        {
            for (int i = 0; i < seatsNeeded; i++)
            {
                _seatMap[row, startSeat + i] = group;
            }
        }

        // Helper to update the global startRow/startSeat pointers
        private void UpdateGlobalPosition(ref int globalStartRow, ref int globalStartSeat, int placedRow, int nextSeatInRow)
        {
            globalStartRow = placedRow;
            globalStartSeat = nextSeatInRow;
            // Handle moving to next row if current is filled
            if (globalStartSeat >= _seatsPerRow)
            {
                globalStartRow++;
                globalStartSeat = 0;
            }
        }

        // Helper to check if a potential family placement keeps children adjacent to adults within the proposed block
        // Assumes the family members from the list are placed sequentially into the block.
        // Current limitations: Does not explicitly handle aisle separation within the block (e.g., for 3+3 seating).
        // It only ensures a child has an adult neighbor immediately to the left or right within the assigned seat block.
        private bool IsFamilyPlacementValid(Family family, int row, int startSeat, int seatsNeeded)
        {
            List<Passenger> members = family.Members;
            // Basic checks
            if (members.Count == 0) return true; // Empty family is valid? Maybe should be false? Or handled earlier.
            if (members.Count != seatsNeeded) {
                 // Log an error instead of just a warning, and return false as this indicates a logic error elsewhere.
                 Console.Error.WriteLine($"Error: Family validity check failed for {family.FamilyId} - member count ({members.Count}) does not match seats needed ({seatsNeeded}). This indicates a problem before calling IsFamilyPlacementValid.");
                 return false; // Return false because the placement premise is wrong.
            }
            bool hasChild = family.HasChildren;
            if (!hasChild) return true; // No children, no adjacency requirement

            bool hasAdult = members.Any(p => p.IsAdult);
            if (!hasAdult) {
                // Family has children but no adults - invalid placement
                 Console.WriteLine($"Warning: Cannot seat family {family.FamilyId} - contains children but no adults.");
                return false;
            }

            // Simulate the placement within the block to check adjacency accurately.
            // Check each position in the potential block.
            for (int i = 0; i < seatsNeeded; i++)
            {
                Passenger currentPassenger = members[i]; // Assume members[i] goes into seat 'startSeat + i'
                if (!currentPassenger.IsAdult) // Found a child
                {
                    // Check if this child has at least one adult neighbor *within the allocated block*.
                    bool adultNeighborFound = false;
                    
                    // Check left neighbor (seat i-1 within the block)
                    if (i > 0)
                    {
                        Passenger leftNeighbor = members[i - 1];
                        if (leftNeighbor.IsAdult)
                    {
                        adultNeighborFound = true;
                    }
                    }
                    
                    // Check right neighbor (seat i+1 within the block)
                    if (!adultNeighborFound && i < seatsNeeded - 1)
                    {
                        Passenger rightNeighbor = members[i + 1];
                        if (rightNeighbor.IsAdult)
                    {
                        adultNeighborFound = true;
                        }
                    }

                    // If after checking both sides *within the block*, no adult neighbor was found for this child.
                    if (!adultNeighborFound)
                    {
                         Console.WriteLine($"Invalid placement for family {family.FamilyId}: Child at relative position {i} has no adjacent adult within the block.");
                        return false; // This placement is invalid according to the rule.
                    }
                }
            }

            // If all children have at least one adjacent adult within the block.
            return true; // Placement is valid
        }

        private double CalculateRevenueFromMap()
        {
            double totalRevenue = 0;
            HashSet<BoardingGroup> countedGroups = new HashSet<BoardingGroup>(); // Avoid double-counting revenue for groups spanning multiple seats

            for (int r = 0; r < _planeRows; r++)
            {
                for (int s = 0; s < _seatsPerRow; s++)
                {
                    BoardingGroup? group = _seatMap[r, s];
                    if (group != null && countedGroups.Add(group)) // Add returns true if item was added (i.e., not already present)
                    {
                         totalRevenue += group.GroupRevenue;
                    }
                }
            }
            return totalRevenue;
        }

        // Optional: Method to visualize the seating map (can be moved to a separate class later)
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
                         // Use the GroupId for display
                         // Pad or truncate for consistent width (e.g., 5 chars)
                         string displayId = group.GroupId.Length > 5 ? group.GroupId.Substring(0, 5) : group.GroupId.PadRight(5);
                         seatDisplay = $" {displayId} ";
                     }
                     Console.Write(seatDisplay);
                     if (s < _seatsPerRow - 1) Console.Write("|");
                }
                Console.WriteLine("]");
            }
            Console.WriteLine("-------------------\n");
        }
    }
} 