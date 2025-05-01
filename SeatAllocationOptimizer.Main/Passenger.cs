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

        // Constructor logic handled by DataParser.
    }
} 