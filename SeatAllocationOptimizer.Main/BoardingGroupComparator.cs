using System;
using System.Collections.Generic;

namespace SeatAllocationOptimizer.Main
{
    // Compares BoardingGroups for prioritization (e.g., in a PriorityQueue)
    // Prioritizes higher revenue per seat, then smaller groups (fewer seats needed).
    public class BoardingGroupComparator : IComparer<BoardingGroup>
    {
        public int Compare(BoardingGroup? x, BoardingGroup? y)
        {
            if (x == null && y == null) return 0;
            if (x == null) return 1; // Prefer non-null y
            if (y == null) return -1; // Prefer non-null x

            // Primary sort: Higher RevenuePerSeat comes first (descending order)
            int revenueCompare = y.RevenuePerSeat.CompareTo(x.RevenuePerSeat);
            if (revenueCompare != 0)
            {
                return revenueCompare;
            }

            // Secondary sort: Smaller groups (fewer seats needed) come first (ascending order)
            return x.SeatsNeeded.CompareTo(y.SeatsNeeded);
        }
    }
} 