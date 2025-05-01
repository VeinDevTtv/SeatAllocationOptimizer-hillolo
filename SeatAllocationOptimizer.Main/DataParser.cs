using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Globalization; // For parsing double

namespace SeatAllocationOptimizer.Main
{
    public static class DataParser
    {
        // Parses the input CSV file (format: ID,Type,Revenue,FamilyID,WindowPref)
        public static List<BoardingGroup> ParseInput(string filePath)
        {
            Console.WriteLine($"Parsing input from: {filePath}");
            var passengersByFamily = new Dictionary<string, List<Passenger>>();
            var individualPassengers = new List<Passenger>();

            try
            {
                using (var reader = new StreamReader(filePath))
                {
                    string? line;
                    int lineNumber = 0;
                    while ((line = reader.ReadLine()) != null)
                    {
                        lineNumber++;
                        if (string.IsNullOrWhiteSpace(line)) continue; // Skip empty lines

                        var (parsedPassenger, familyId) = ParseLine(line, lineNumber);

                        if (parsedPassenger != null && familyId != null) // Check both passenger and familyId parsed ok
                        {
                            if (familyId == "-")
                            {
                                individualPassengers.Add(parsedPassenger);
                            }
                            else
                            {
                                if (!passengersByFamily.ContainsKey(familyId))
                                {
                                    passengersByFamily[familyId] = new List<Passenger>();
                                }
                                passengersByFamily[familyId].Add(parsedPassenger);
                            }
                        }
                        // If ParseLine returned null passenger or null familyId, it already printed a warning.
                    }
                }
            }
            catch (FileNotFoundException)
            {
                Console.WriteLine($"Error: Input file not found at {filePath}");
                return new List<BoardingGroup>(); // Return empty list
            }
             catch (IOException ex)
            {
                Console.WriteLine($"Error: Could not read input file {filePath}. {ex.Message}");
                return new List<BoardingGroup>(); // Return empty list
            }


            // Create BoardingGroup objects
            var boardingGroups = new List<BoardingGroup>();

            // Add individuals
            foreach (var individual in individualPassengers)
            {
                boardingGroups.Add(new BoardingGroup(individual));
            }

            // Add families
            foreach (var kvp in passengersByFamily)
            {
                var family = new Family(kvp.Key); // Pass familyId to constructor
                foreach (var member in kvp.Value)
                {
                    family.AddMember(member);
                }
                 if(family.Members.Any())
                 {
                    boardingGroups.Add(new BoardingGroup(family));
                 }
            }

            Console.WriteLine($"Parsed {boardingGroups.Count} boarding groups ({individualPassengers.Count} individuals, {passengersByFamily.Count} families).");
            return boardingGroups;
        }

        // Helper method to parse a single line.
        // Returns a tuple: (Passenger? passenger, string? familyId)
        // Returns (null, null) if parsing fails, and logs a warning.
        private static (Passenger?, string?) ParseLine(string line, int lineNumber)
        {
            string[] parts = line.Split(',');
            // Expecting ID,Type,Revenue,FamilyID,WindowPref - needs at least 5 parts for full parsing
            if (parts.Length < 5)
            {
                Console.WriteLine($"Warning: Skipping malformed line {lineNumber} (expected 5+ parts): {line}");
                return (null, null);
            }

            try
            {
                // Parse required fields
                // int id = int.Parse(parts[0].Trim()); // ID not parsed currently
                bool isAdult = parts[1].Trim().Equals("Adult", StringComparison.OrdinalIgnoreCase);
                double revenue = double.Parse(parts[2].Trim(), CultureInfo.InvariantCulture);
                string familyId = parts[3].Trim();
                bool wantsWindow = parts[4].Trim().Equals("Yes", StringComparison.OrdinalIgnoreCase);
                int seatsNeeded = 1; // Assuming 1 seat per passenger

                 if (string.IsNullOrEmpty(familyId)) {
                     Console.WriteLine($"Warning: Skipping line {lineNumber} due to empty FamilyID: {line}");
                     return (null, null); // Treat empty FamilyID as an error for grouping
                 }

                var passenger = new Passenger(isAdult, revenue, seatsNeeded, wantsWindow);
                return (passenger, familyId);
            }
            catch (FormatException ex)
            {
                Console.WriteLine($"Warning: Skipping line {lineNumber} due to parsing error ({ex.Message}): {line}");
                return (null, null);
            }
            catch (Exception ex) // Catch other potential errors per line
            {
                Console.WriteLine($"Warning: Skipping line {lineNumber} due to unexpected error ({ex.Message}): {line}");
                return (null, null);
            }
        }
    }
} 