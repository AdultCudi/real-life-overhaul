using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's special places (SpecialsRules, docs/features/uptown.md "Special places"): which lot each has is fixed data;
    /// its building is declared with the game's structure its service's layout is for and the service to copy (the made
    /// buildings' engine attaches it and fills the registration as the game does); at the game's late "game loaded" call each
    /// is made whole (an importer among the game's importers, a dealership's kerb spot). Its tests: the census of every place,
    /// and the destination set at each in turn. Nothing here runs a frame.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly MadeBuilding[] SpecialUp = new MadeBuilding[SpecialsRules.All.Length];   // the building each place stands in at this load
        private static int _lastSpecial = -1;   // the place the test last took the player to

        // A lot of the table: its building is the place's structure, with no other to take (its layout is for this one alone).
        private static bool SpecialStructure(BuildingsLot lot, out BuildingsStructure structure, out SpecialPlace place)
        {
            place = SpecialsRules.At(lot.Id);
            structure = place != null ? new BuildingsStructure { Size = place.Size, Version = place.Version, Type = place.Type } : default;
            return place != null;
        }

        private static void ForgetSpecials()
        {
            for (int i = 0; i < SpecialUp.Length; i++)
            {
                SpecialUp[i] = null;
            }
        }

        // At the game's late "game loaded" call: each place made whole; how many stand as the game's special places.
        private static int Specials()
        {
            int up = 0;
            for (int i = 0; i < SpecialUp.Length; i++)
            {
                SpecialUp[i] = null;
                SpecialPlace place = SpecialsRules.All[i];
                MadeBuilding made = StandingOnLot(place.Lot);
                if (made == null)
                {
                    Logger?.Info("Uptown: " + (place.Name ?? place.Service) + " has no building at this load: its lot " + place.Lot + " has none standing (Player.log says why).");
                    continue;
                }
                string why = LifeGame.Special(made, place);
                if (why != null)
                {
                    Logger?.Info("Uptown: " + (place.Name ?? place.Service) + " at " + made.Address + " is not a special place at this load: " + why + ".");
                    continue;
                }
                SpecialUp[i] = made;
                up++;
            }
            _stamp++;
            return up;
        }

        // ---------------------------------------------------------------- the tests

        /// <summary>
        /// "Uptown: the special places": the census line of every place (standing, its service, registered, listed, laid
        /// out, staffed, on the map's rows) and a notice of how many are whole.
        /// </summary>
        public static void TestSpecials()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                bool pass = SpecialsCensus("the test", true, out int whole);
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.SpecialsDone, new Dictionary<string, string>
                {
                    { "count", whole.ToString(Plain) }, { "all", SpecialsRules.All.Length.ToString(Plain) }
                }, 14f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The census of the special places: one line, every place's verdict (a FAIL always written).
        private static bool SpecialsCensus(string when, bool asked, out int whole)
        {
            whole = 0;
            var line = new StringBuilder(1024);
            for (int i = 0; i < SpecialsRules.All.Length; i++)
            {
                SpecialPlace place = SpecialsRules.All[i];
                MadeBuilding made = SpecialUp[i] ?? StandingOnLot(place.Lot);
                string name = LifeGame.SpecialName(made, place, out string kind);
                string why = made == null ? "no building stands on its lot " + place.Lot : SpecialUp[i] == null ? "not made a special place at this load" : LifeGame.SpecialWhy(made, place);
                whole += why == null ? 1 : 0;
                line.Append(i > 0 ? "; " : string.Empty).Append(name).Append(" (").Append(place.ServiceType).Append(made != null ? ") at " + made.Address : ")")
                    .Append(why == null ? ": standing, its service, registered, listed, laid out, staffed" : ": " + why);
            }
            bool pass = whole == SpecialsRules.All.Length;
            string text = "Uptown check (special places, " + when + "): " + whole + " of " + SpecialsRules.All.Length + " whole: " + line + ": " + (pass ? "PASS" : "FAIL");
            if (!pass)
            {
                Logger?.Warn(text);
            }
            else if (asked || Meta.Dev.Verbose)
            {
                Logger?.Info(text);
            }
            return pass;
        }

        /// <summary>
        /// "Uptown: take me to the next special place": the game's destination set at the next place of the table that stands,
        /// round again, and a notice naming it and its kind.
        /// </summary>
        public static void TestSpecialGo()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                int count = SpecialsRules.All.Length, at = _lastSpecial;
                for (int tried = 0; tried < count; tried++)
                {
                    at = SpecialsRules.Next(at, count);
                    MadeBuilding made = SpecialUp[at];
                    if (made == null)
                    {
                        continue;
                    }
                    _lastSpecial = at;
                    LifeGame.Destination(made);
                    string name = LifeGame.SpecialName(made, SpecialsRules.All[at], out string kind);
                    Logger?.Info("Uptown test: the destination is set at " + name + " (" + SpecialsRules.All[at].ServiceType + ") at " + made.Address + ", place " + (at + 1) + " of " + count + ".");
                    Notifications.Show(NotificationType.Info, LifeText.SpecialGo, new Dictionary<string, string> { { "name", name }, { "kind", kind }, { "address", made.Address } }, 14f);
                    return;
                }
                Logger?.Warn("Uptown check (special places): none of the " + count + " stands as a special place at this load: FAIL");
                Notifications.Show(NotificationType.Warning, LifeText.SpecialsNone, null, 12f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
