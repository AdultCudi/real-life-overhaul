using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Tags;
using Buildings;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;
using UI;
using UI.Notification;
using UI.Smartphone.Apps.MyEmployees;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Hiring several picked candidates to one business from My Employees' Candidates tab, as the game's own "assign to business
    /// and hire" action does (AssignToBusinessAndHireMassAction): the same list of the player's businesses, the same skills a
    /// business needs, and for each candidate the game's own hire (EmployeeHelper.HireCandidate, then the candidates table's
    /// RemoveCandidate), so a patch on the game's hire (Going Public's, another mod's) runs for every one. What the game does
    /// not do, the kit's review sheet does before the question: it says for each candidate whether they can be hired there
    /// (<see cref="StaffBatchRules.HireVerdict"/>) and why not, and after the yes it tries every candidate, naming those it could
    /// not hire and why in one notice. The game's hire has no check of money, free places or schedule (nothing in
    /// EmployeeHelper.HireCandidate asks), so the sheet adds none. Going Public: only the player's own business and the
    /// player's own candidates; nothing is sent. COST: nothing until the player confirms a business; then a pass over the picked.
    /// </summary>
    internal static class StaffBatchHire
    {
        private static IModLogger Logger => UiDirector.Logger;

        private static readonly List<EmployeeInstance> Picked = new List<EmployeeInstance>(16);
        private static readonly List<string> Ids = new List<string>(16), Names = new List<string>(16), Roles = new List<string>(16);
        private static readonly List<int> Wages = new List<int>(16);
        private static readonly List<BatchState> Verdicts = new List<BatchState>(16);

        /// <summary>
        /// The business at this place in the game's panel of choices (the list its own action builds), for the review: fills the sheet
        /// and opens it. False when the choice is none, the picked are not candidates or the business is gone, so the game's own
        /// confirm runs instead.
        /// </summary>
        public static bool Review(int choice)
        {
            List<BuildingRegistration> places = BuildingHelper.GetPlayerBuildingRegistrations(PlayerBuildingFilter);
            if (choice < 0 || places == null || choice >= places.Count || !ReadPicked())
            {
                return false;
            }
            BuildingRegistration business = places[choice];
            List<string> skills = Skills(business);
            Verdicts.Clear();
            for (int i = 0; i < Picked.Count; i++)
            {
                Verdicts.Add(Verdict(Picked[i], business, skills));
            }
            var candidates = new List<EmployeeInstance>(Picked);
            StaffSheet sheet = StaffBatchRules.HireSheet(Ids, Names, Roles, Wages, Verdicts, business.BusinessName);
            sheet.Give = () => Hire(candidates, business, skills);
            StaffBatch.Open(sheet);
            return true;
        }

        // The candidates picked in the game's list, in the order picked, with what the sheet shows of each.
        private static bool ReadPicked()
        {
            Picked.Clear();
            Ids.Clear();
            Names.Clear();
            Roles.Clear();
            Wages.Clear();
            List<EmployeeInstance> picked = MyEmployeesMassActionsUI.massActionSelectedEmployees;
            for (int i = 0; picked != null && i < picked.Count; i++)
            {
                EmployeeInstance e = picked[i];
                if (e == null || e.characterData == null)
                {
                    continue;
                }
                Picked.Add(e);
                Ids.Add(e.id);
                Names.Add(e.characterData.name);
                Roles.Add(e.GetPrimarySkill());
                Wages.Add(WageShown.PaidCents(e));   // what the candidate costs an hour at the city's prices, as the candidates' table shows it
            }
            return Picked.Count > 0;
        }

        // The game's own list of the player's businesses a mass action may assign to: rented by the player, named, not empty.
        private static bool PlayerBuildingFilter(BuildingRegistration registration)
        {
            return registration.RentedByPlayer && !string.IsNullOrEmpty(registration.BusinessName) && registration.businessTypeName != "ba:businesstype_empty";
        }

        // The skills a business needs, as the game's action counts them: the business type's primary skills, cleaning where the
        // building needs cleaning, a guard where the business can be stolen from, a driver where the building needs one.
        private static List<string> Skills(BuildingRegistration business)
        {
            BusinessType data = BusinessTypeHelper.GetData(business);
            var list = new List<string>(data.employeePrimarySkills);
            BuildingTypeData building = BuildingTypeHelper.GetData(business);
            if (building.NeedsCleaning)
            {
                list.Add("ba:skill_cleaning");
            }
            if (data.HasTag(TagRef.Businesstag.allowtheft))
            {
                list.Add("ba:skill_securityguard");
            }
            if (building.requiredBuildingSkills != null && Array.IndexOf(building.requiredBuildingSkills, "ba:skill_deliverydriver") >= 0)
            {
                list.Add("ba:skill_deliverydriver");
            }
            return list;
        }

        // One candidate against one business, read afresh: still a candidate, the business the player's, one skill the business needs.
        private static BatchState Verdict(EmployeeInstance e, BuildingRegistration business, List<string> skills)
        {
            bool fits = false;
            for (int s = 0; e.characterData != null && e.characterData.skills != null && s < e.characterData.skills.Count && !fits; s++)
            {
                fits = skills.Contains(e.characterData.skills[s].name);
            }
            return StaffBatchRules.HireVerdict(e.IsCandidate, GoingPublic.Mine(business), fits);
        }

        // The yes: every candidate is tried, in the order picked; one that cannot be hired, or throws, does not stop the others.
        private static void Hire(List<EmployeeInstance> candidates, BuildingRegistration business, List<string> skills)
        {
            MyEmployeesMassActionsUI ui = Ui();
            BatchOutcome outcome = StaffBatchRules.Run(candidates.Count, i => HireOne(candidates[i], business, skills), (i, e) =>
            {
                string who = candidates[i] != null && candidates[i].characterData != null ? candidates[i].characterData.name : "a candidate";
                Logger?.Warn("Staff batch: hiring " + who + " threw, the others went on: " + e.Message);
            });
            var left = new List<string>(outcome.Failed.Count);
            for (int i = 0; i < outcome.Failed.Count; i++)
            {
                EmployeeInstance e = candidates[outcome.Failed[i]];
                string name = e != null && e.characterData != null ? e.characterData.name : "?";
                left.Add(name + " (" + StaffBatchRules.ReasonKey(outcome.Why[i]).GetLocalization() + ")");
                Logger?.Info("Staff batch: " + name + " was not hired at " + business.BusinessName + ": " + outcome.Why[i]);
            }
            Logger?.Info("Staff batch: " + outcome.Done + " of " + candidates.Count + " hired at " + business.BusinessName + " (each by EmployeeHelper.HireCandidate).");
            Tell(outcome.Done, candidates.Count, business.BusinessName, left);
            ui?.OnMassActionPerformed(true);   // as the game's own action ends: the panel shut, the ticks cleared, the table loaded again
        }

        private static BatchState HireOne(EmployeeInstance e, BuildingRegistration business, List<string> skills)
        {
            BatchState verdict = e != null ? Verdict(e, business, skills) : BatchState.Gone;
            if (verdict != BatchState.Ready)
            {
                return verdict;
            }
            Address was = e.assignedAddress;
            e.assignedAddress = business.Address;   // as the game's action does, then its own hire
            try
            {
                EmployeeHelper.HireCandidate(e);
                MyEmployees app = App();
                app?.candidateScrollerController.RemoveCandidate(e);
            }
            catch
            {
                if (e.IsCandidate)
                {
                    e.assignedAddress = was;
                }
                throw;
            }
            if (e.IsCandidate)
            {
                e.assignedAddress = was;   // the game's hire ignored it (a duplicate): nothing changed
                return BatchState.Failed;
            }
            return BatchState.Ready;
        }

        // One notice: how many were hired and where, and those who were not with the reason, named to fit the notice.
        private static void Tell(int done, int all, string business, List<string> left)
        {
            string list = StaffBatchRules.Listed(left, "reallife_staff_batch_more".GetLocalization(), StaffBatchRules.ListBudget);
            var words = new Dictionary<string, string>
            {
                { "count", done.ToString() }, { "all", all.ToString() }, { "business", business ?? string.Empty }, { "list", list }
            };
            if (left.Count == 0)
            {
                Notifications.Show(NotificationType.Success, "reallife_staff_batch_hired", words, 9f, null, null, true, false);
            }
            else
            {
                Notifications.Show(NotificationType.Error, done > 0 ? "reallife_staff_batch_hired_some" : "reallife_staff_batch_hired_none", words, 12f, null, null, true, false);
            }
        }

        private static MyEmployees App()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.fullMenu != null ? uis.fullMenu.myEmployees : null;
        }

        private static MyEmployeesMassActionsUI Ui()
        {
            MyEmployees app = App();
            return app != null ? app.massActionsUI : null;
        }
    }
}
