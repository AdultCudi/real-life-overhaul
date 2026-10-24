using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// Uptown's offices as places of work: "Uptown: the offices with their businesses" (the census of its office buildings,
    /// the businesses the game's own generator opened in them, staffed and laid out, where the working life employs, and the
    /// share with a business against the rest of the city's) and "Uptown: a job at an Uptown office" (a job for the player at
    /// the nearest, through the working life, and the destination set at its door). Each works when pressed or says why not
    /// in a notice, with one "Uptown check (...)" line in Player.log. Nothing here runs a frame.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private static readonly List<OfficeView> OfficeViews = new List<OfficeView>(128);
        private static readonly List<MadeBuilding> OfficeBuildings = new List<MadeBuilding>(128);   // the building of each view, in the same order
        private static readonly HashSet<string> OfficeLots = new HashSet<string>(StringComparer.Ordinal);   // the plan's office lots, fixed data

        // Each of the district's office buildings standing, as the census reads it, into OfficeViews and OfficeBuildings.
        private static void ReadOffices()
        {
            OfficeViews.Clear();
            OfficeBuildings.Clear();
            if (OfficeLots.Count == 0)
            {
                for (int i = 0; i < Plan.Count; i++)
                {
                    if (Plan[i].Class == BuildingsClass.Office)
                    {
                        OfficeLots.Add(Plan[i].Id);
                    }
                }
            }
            Vector3 at = MadeGame.PlayerAt();
            for (int i = 0; i < Standing.Count; i++)
            {
                MadeBuilding made = Standing[i];
                if (!LifeGame.Office(made, at, out OfficeView view))
                {
                    continue;
                }
                view.OnOfficeLot = OfficeLots.Contains(made.Spec.Id);
                view.Leased = Array.IndexOf(LeasedUp, made) >= 0;
                OfficeViews.Add(view);
                OfficeBuildings.Add(made);
            }
        }

        // ---------------------------------------------------------------- the census of its offices

        /// <summary>"Uptown: the offices with their businesses": the census line, and a notice of its counts.</summary>
        public static void TestOfficeCensus()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                ReadOffices();
                OfficeTally t = OfficesRules.Tally(OfficeViews);
                LifeGame.CityOffices(out int cityOffices, out int cityBusinesses);
                int posts = 0, open = 0;
                bool work = DistrictWork.Openings != null;
                if (work)
                {
                    posts = DistrictWork.Openings(AnOffice, out open);
                }
                LifeGame.UnitsOf(Standing, "ba:buildingtype_retail", out int shops, out int shopsEmpty);
                int shopShare = Helpers.CompetitionHelper.GetIdealPercentageOfEmptyBuildings("ba:buildingtype_retail", DistrictValues.Id);
                string why = OfficesRules.Verdict(t, cityOffices, cityBusinesses);
                if (why == null && !work)
                {
                    why = "the working life is not there to employ at them (education and careers switched off, or stood down)";
                }
                bool pass = why == null;
                string line = "Uptown check (offices): " + t.Offices + " office building(s) standing (" + t.OnOfficeLots + " on the plan's office lots, "
                              + (t.Offices - t.OnOfficeLots) + " on its other lots), " + t.Businesses + " with a business (" + (t.Types.Length > 0 ? t.Types : "none") + "), "
                              + t.Staffed + " of them staffed, " + t.LaidOut + " laid out, " + t.Employers + " where the working life employs; " + t.ToLet + " empty to let, "
                              + t.Leased + " the city's builders', " + t.Players + " the player's; " + OfficesRules.Share(t.Businesses, t.Offices - t.Players)
                              + " % with a business against " + OfficesRules.Share(cityBusinesses, cityOffices) + " % of the rest of the city's (" + cityBusinesses + " of " + cityOffices + ")"
                              + "; shops " + shopsEmpty + " of " + shops + " empty to let (" + OfficesRules.Share(shopsEmpty, shops) + " %, the district keeps " + shopShare + " %)"
                              + "; the opening pass after the game's generator " + (_state.Opened ? "has run in this save" : "has not run yet")
                              + (work ? "; " + posts + " of the city's " + open + " open posts today at Uptown's offices" : string.Empty)
                              + (why != null ? " (" + why + ")" : string.Empty) + ": " + (pass ? "PASS" : "FAIL");
                if (pass)
                {
                    Logger?.Info(line);
                }
                else
                {
                    Logger?.Warn(line);
                }
                Notifications.Show(pass ? NotificationType.Info : NotificationType.Warning, LifeText.OfficeCensusDone, new Dictionary<string, string>
                {
                    { "offices", t.Offices.ToString(Plain) }, { "businesses", t.Businesses.ToString(Plain) }, { "employers", t.Employers.ToString(Plain) }, { "let", t.ToLet.ToString(Plain) }
                }, 14f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // True for an address of one of the district's office buildings read last (the open posts the census counts).
        private static bool AnOffice(string street, int number)
        {
            for (int i = 0; i < OfficeViews.Count; i++)
            {
                if (OfficeViews[i].Number == number && string.Equals(OfficeViews[i].Street, street, StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- a job there

        /// <summary>"Uptown: a job at an Uptown office": a job for the player at the nearest office business the working life employs at, and the destination set at its door.</summary>
        public static void TestOfficeJob()
        {
            try
            {
                if (Refused(true))
                {
                    return;
                }
                if (DistrictWork.HireAt == null)
                {
                    TestRefusals.TellKey(Logger, TestName, LifeText.OfficeJobNoWork, "the working life is not connected");
                    return;
                }
                ReadOffices();
                int pick = OfficesRules.Workplace(OfficeViews);
                if (pick < 0)
                {
                    Logger?.Warn("Uptown check (a job at an office): of " + OfficeViews.Count
                                 + " office building(s), none has a staffed, laid out business of the game's that the working life employs at: FAIL");
                    Notifications.Show(NotificationType.Warning, LifeText.OfficeJobNone, null, 12f);
                    return;
                }
                OfficeView office = OfficeViews[pick];
                MadeBuilding made = OfficeBuildings[pick];
                string refused = DistrictWork.HireAt(office.Street, office.Number);
                if (refused != null)
                {
                    Logger?.Warn("Uptown check (a job at an office): the working life gave no job at " + office.Address + " (" + OfficesRules.Word(office.Type) + "; " + refused + "): FAIL");
                    Notifications.Show(NotificationType.Warning, refused, null, 12f);
                    return;
                }
                LifeGame.Destination(made);
                Logger?.Info("Uptown check (a job at an office): the player works at " + office.Address + " (" + OfficesRules.Word(office.Type) + ", "
                             + Mathf.Sqrt(office.Distance2).ToString("F0", Plain) + " m away), the destination set at its door: PASS");
                Notifications.Show(NotificationType.Success, LifeText.OfficeJobDone, new Dictionary<string, string> { { "address", office.Address } }, 16f);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
