using System;
using System.Collections.Generic;
using System.Linq;

namespace SeatAllocationOptimizer.Main
{
    public class Family
    {
        public string FamilyId { get; private set; } // Store Family ID
        public List<Passenger> Members { get; private set; } = new List<Passenger>();

        // Constructor to set the Family ID
        public Family(string familyId)
        {
             FamilyId = string.IsNullOrWhiteSpace(familyId) || familyId == "-" ? $"FamGen-{this.GetHashCode() % 10000}" : familyId;
        }

        public double TotalRevenue => Members.Sum(p => p.Revenue);
        public double AverageRevenue => Members.Any() ? TotalRevenue / Members.Count : 0;
        public int TotalSeatsNeeded => Members.Sum(p => p.SeatsNeeded);
        public bool HasChildren => Members.Any(p => !p.IsAdult);

        public void AddMember(Passenger passenger)
        {
            if (passenger != null)
            {
                Members.Add(passenger);
            }
        }

        // Basic family structure with member addition and calculated properties.
    }
} 