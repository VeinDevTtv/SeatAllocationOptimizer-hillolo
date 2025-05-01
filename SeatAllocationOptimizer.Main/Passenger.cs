using System;

namespace SeatAllocationOptimizer.Main
{
    public class Passenger
    {
        public bool IsAdult { get; set; }
        public double Revenue { get; set; }
        public int SeatsNeeded { get; set; } // Typically 1, could be different for specific needs
        public bool WantsWindowSeat { get; set; }

        public Passenger(bool isAdult, double revenue, int seatsNeeded = 1, bool wantsWindow = false)
        {
            IsAdult = isAdult;
            Revenue = revenue;
            SeatsNeeded = seatsNeeded;
            WantsWindowSeat = wantsWindow;
        }

        // TODO: Add constructor(s) and potentially methods mentioned or implied in README
        // For example, parsing from input might be a static factory method or constructor logic.
    }
} 