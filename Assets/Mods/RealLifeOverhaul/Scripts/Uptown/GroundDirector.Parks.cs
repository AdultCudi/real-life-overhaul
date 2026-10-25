using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's parks as the load lays them and the test reads them: the plan's two park squares laid with the street
    /// furniture, behind the loading screen and before the walking ground grows round them (ParksGame), the line about them
    /// in the load's one line, and the test "Uptown: parks" under Options > Tests, which reads and counts, changes nothing
    /// and writes one "Uptown check (parks)" line with Developer diagnostics on. On a load and a press, never a frame.
    /// </summary>
    public sealed partial class GroundDirector
    {
        private const string ParksTest = "Uptown: parks";

        private double _parksMs;

        // With the street furniture, before the ground grows: the parks' trees, benches and fountain are walked round, and said once.
        private void LayParks()
        {
            long mark = Stopwatch.GetTimestamp();
            ParksGame.Lay(GroundGame.ArtRoot);
            _parksMs = Ms(mark);
            Logger?.Info("Uptown: the pieces of the game's the street furniture copies: " + (StreetsGame.FoundWords ?? "none looked for")
                         + "; the parks': " + (ParksGame.FoundWords ?? "none looked for") + ".");
        }

        private string ParksWords()
        {
            return ParksGame.Parks.Count + " of " + ParksPlanned() + " parks laid (" + ParksGame.Laid + " of " + ParksGame.Planned + " pieces of the game's, "
                   + ParksGame.Paved.ToString("F0", Inv) + " square metres of paths" + (ParksGame.Pathed ? string.Empty : ", the paving's material not found")
                   + (ParksGame.Missing != null ? "; not found: " + ParksGame.Missing : string.Empty) + ") in " + _parksMs.ToString("F0", Inv) + " ms";
        }

        private static int ParksPlanned()
        {
            var blocks = new List<int>(4);
            ParksRules.Blocks(blocks);
            return blocks.Count;
        }

        public static void TestParks()
        {
            if (Instance == null)
            {
                TestRefusals.Tell(Logger, ParksTest, TestRefusal.NotLoaded);
                return;
            }
            Instance.Parks();
        }

        // "Uptown: parks": what stands in each park against the plan.
        private void Parks()
        {
            if (!UptownLots.Ready)
            {
                TestRefusals.TellKey(Logger, ParksTest, GroundText.NotLaid, UptownLots.Why);
                return;
            }
            var blocks = new StringBuilder(32);
            for (int i = 0; i < ParksGame.Parks.Count; i++)
            {
                blocks.Append(i > 0 ? ", " : string.Empty).Append(ParksGame.Parks[i]);
            }
            bool pass = ParksGame.Parks.Count == ParksPlanned() && ParksGame.Missing == null && ParksGame.Pathed && ParksGame.Laid == ParksGame.Planned && ParksGame.Laid > 0;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Uptown check (parks): " + ParksWords() + "; blocks " + blocks + "; trees " + ParksGame.Count(ParkKind.Tree) + ", bushes " + ParksGame.Count(ParkKind.Bush)
                             + ", benches " + ParksGame.Count(ParkKind.Bench) + ", lamps " + ParksGame.Count(ParkKind.Lamp) + ", bins " + ParksGame.Count(ParkKind.Bin) + ", fountains "
                             + ParksGame.Count(ParkKind.Fountain) + ", statues " + ParksGame.Count(ParkKind.Statue) + "; the game's pieces " + (ParksGame.FoundWords ?? "none")
                             + ": " + (pass ? "PASS" : "FAIL") + ".");
            }
            Notice(StreetsText.ParksDone, new Dictionary<string, string>(7)
            {
                { "parks", ParksGame.Parks.Count.ToString(Inv) },
                { "blocks", blocks.Length > 0 ? blocks.ToString() : "-" },
                { "trees", ParksGame.Count(ParkKind.Tree).ToString(Inv) },
                { "bushes", ParksGame.Count(ParkKind.Bush).ToString(Inv) },
                { "benches", ParksGame.Count(ParkKind.Bench).ToString(Inv) },
                { "lamps", ParksGame.Count(ParkKind.Lamp).ToString(Inv) },
                { "bins", ParksGame.Count(ParkKind.Bin).ToString(Inv) }
            });
        }
    }
}
