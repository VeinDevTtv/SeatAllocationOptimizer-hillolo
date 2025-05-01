using Microsoft.VisualStudio.TestTools.UnitTesting;
using SeatAllocationOptimizer.Main;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace SeatAllocationOptimizer.Tests
{
    [TestClass]
    public class DataParserTests
    {
        private string _testDir = "TestInputData"; // Assumes a directory with test files

        [TestInitialize]
        public void Setup()
        {
            // Create dummy files for testing - requires file system access
            // In a real scenario, these might be embedded resources or copied via build steps.
             if (!Directory.Exists(_testDir))
             {
                 Directory.CreateDirectory(_testDir);
             }
            File.WriteAllText(Path.Combine(_testDir, "valid.csv"),
                "1,Adult,100,A,Yes\n" +
                "2,Child,50,A,No\n" +
                "3,Adult,120,-,No\n" +
                "4,Adult,0,B,Yes\n" // Zero revenue passenger
            );
             File.WriteAllText(Path.Combine(_testDir, "malformed.csv"),
                 "1,Adult,100,A,Yes\n" + // Valid
                 "2,Child,Fifty,A,No\n" + // Invalid revenue
                 "3,Adult\n" + // Missing columns
                 "\n" + // Empty line
                 "4,Adult,120,-,No,ExtraColumn\n" + // Extra column (should be parsed ok)
                 "5,Child,50,B,Maybe\n" // Invalid window pref text (parsed as false)
             );
             File.WriteAllText(Path.Combine(_testDir, "empty.csv"), "");
             File.WriteAllText(Path.Combine(_testDir, "headeronly.csv"), "ID,Type,Revenue,FamilyID,WindowPref");
        }

        [TestCleanup]
        public void Cleanup()
        {
             if (Directory.Exists(_testDir))
             {
                try { Directory.Delete(_testDir, true); } catch {} // Clean up test files
             }
        }

        [TestMethod]
        public void ParseInput_ValidFile_ParsesCorrectly()
        {
            string filePath = Path.Combine(_testDir, "valid.csv");
            List<BoardingGroup> groups = DataParser.ParseInput(filePath);

            Assert.AreEqual(3, groups.Count, "Should parse 3 groups (2 families, 1 individual)");

            // Family A
            var familyA = groups.FirstOrDefault(g => g.IsFamily && g.FamilyGroup?.FamilyId == "A");
            Assert.IsNotNull(familyA);
            Assert.AreEqual(2, familyA.SeatsNeeded);
            Assert.AreEqual(150, familyA.GroupRevenue);
            Assert.IsTrue(familyA.FamilyGroup?.Members.Any(p => p.WantsWindowSeat)); // Check window pref within family

             // Individual 3
             var individual3 = groups.FirstOrDefault(g => !g.IsFamily && g.GroupRevenue == 120);
             Assert.IsNotNull(individual3);
             Assert.AreEqual(1, individual3.SeatsNeeded);
             Assert.AreEqual(120, individual3.GroupRevenue);
             Assert.IsFalse(individual3.IndividualPassenger?.WantsWindowSeat);

             // Family B (single member, zero revenue)
             var familyB = groups.FirstOrDefault(g => g.IsFamily && g.FamilyGroup?.FamilyId == "B");
             Assert.IsNotNull(familyB);
             Assert.AreEqual(1, familyB.SeatsNeeded);
             Assert.AreEqual(0, familyB.GroupRevenue);
             Assert.IsTrue(familyB.FamilyGroup?.Members.First().WantsWindowSeat);
        }

        [TestMethod]
        public void ParseInput_MalformedFile_SkipsInvalidLines()
        {
            string filePath = Path.Combine(_testDir, "malformed.csv");
            List<BoardingGroup> groups = DataParser.ParseInput(filePath); // Should print warnings to console

            Assert.AreEqual(3, groups.Count, "Should parse 3 valid groups despite errors");

             // Check Family A (Line 1)
             var familyA = groups.FirstOrDefault(g => g.IsFamily && g.FamilyGroup?.FamilyId == "A");
             Assert.IsNotNull(familyA);
             Assert.AreEqual(1, familyA.SeatsNeeded); // Only one member parsed for family A
             Assert.AreEqual(100, familyA.GroupRevenue);
             Assert.IsTrue(familyA.FamilyGroup?.Members.First().WantsWindowSeat);

              // Check Individual 4 (Line 5) - Parsed correctly despite extra column
             var individual4 = groups.FirstOrDefault(g => !g.IsFamily && g.GroupRevenue == 120);
             Assert.IsNotNull(individual4);
             Assert.AreEqual(1, individual4.SeatsNeeded);
             Assert.IsFalse(individual4.IndividualPassenger?.WantsWindowSeat);

             // Check Family B (Line 6) - Parsed with WantsWindow=false due to 'Maybe'
             var familyB = groups.FirstOrDefault(g => g.IsFamily && g.FamilyGroup?.FamilyId == "B");
             Assert.IsNotNull(familyB);
             Assert.AreEqual(1, familyB.SeatsNeeded);
             Assert.AreEqual(50, familyB.GroupRevenue);
             Assert.IsFalse(familyB.FamilyGroup?.Members.First().WantsWindowSeat); // 'Maybe' is not 'Yes'
        }

        [TestMethod]
        public void ParseInput_EmptyFile_ReturnsEmptyList()
        {
            string filePath = Path.Combine(_testDir, "empty.csv");
            List<BoardingGroup> groups = DataParser.ParseInput(filePath);
            Assert.AreEqual(0, groups.Count);
        }

        [TestMethod]
        public void ParseInput_HeaderOnlyFile_ReturnsEmptyList()
        {
            // Assumes header fails parsing checks (e.g., "Revenue" is not a double)
            string filePath = Path.Combine(_testDir, "headeronly.csv");
            List<BoardingGroup> groups = DataParser.ParseInput(filePath);
            Assert.AreEqual(0, groups.Count);
        }

        [TestMethod]
        public void ParseInput_FileNotFound_ReturnsEmptyList()
        {
            string filePath = Path.Combine(_testDir, "nonexistent.csv");
            List<BoardingGroup> groups = DataParser.ParseInput(filePath); // Should print error to console
            Assert.AreEqual(0, groups.Count);
        }
    }
} 