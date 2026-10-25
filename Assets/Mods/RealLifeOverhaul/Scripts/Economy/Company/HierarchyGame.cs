using System;
using System.Collections.Generic;
using Entities;
using Helpers;
using UI.Smartphone.Apps.BizMan.Schedule;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// Everything the hierarchy does to the game beyond what the managers do (ManagerGame): a candidate another of the
    /// player's places found, hired for the place with the gap, and a member of staff moved from one store to another,
    /// each the way the game's own MyEmployees does it (CandidateCellView.AssignBusiness, MyEmployees.AssignSelectedEmployeeToBusiness).
    /// Never another player's: both places must be the player's own (GoingPublic.Mine). Runs on the hierarchy's day and
    /// hour and on the owner's buttons, never per frame.
    /// </summary>
    internal static class HierarchyGame
    {
        private static GameInstance Save => SaveGameManager.Current;

        /// <summary>Whether somebody could work at a place, as MyEmployees lists the businesses they may be given: one of its primary skills.</summary>
        public static bool Fits(EmployeeInstance e, BuildingRegistration reg)
        {
            if (e == null || reg == null || string.IsNullOrEmpty(reg.businessTypeName))
            {
                return false;
            }
            try
            {
                BusinessType type = BusinessTypeHelper.GetData(reg);
                string[] skills = type != null ? type.employeePrimarySkills : null;
                for (int i = 0; skills != null && i < skills.Length; i++)
                {
                    if (!string.IsNullOrEmpty(skills[i]) && e.HasSkill(skills[i]))
                    {
                        return true;
                    }
                }
            }
            catch (Exception)
            {
                return false;
            }
            return false;
        }

        /// <summary>The candidates the job boards of other places have found, waiting in MyEmployees, who could work at this one.</summary>
        public static void CandidatesFor(BuildingRegistration to, IReadOnlyList<RotaPlace> from, List<EmployeeInstance> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.CandidateEmployeeInstances : null;
            for (int i = 0; all != null && to != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e == null || e.candidateInfo == null || e.hourlyWage <= 0f || e.characterData == null || e.characterData.skills == null || e.characterData.skills.Count == 0
                    || e.characterData.skills[0] == null || ManagerGame.At(e, to) || !Fits(e, to))
                {
                    continue;
                }
                for (int p = 0; p < from.Count; p++)
                {
                    if (from[p].Reg != null && ManagerGame.At(e, from[p].Reg))
                    {
                        into.Add(e);
                        break;
                    }
                }
            }
        }

        /// <summary>A candidate of another place given to this one and hired, as the player does in MyEmployees.</summary>
        public static bool HireFor(EmployeeInstance e, BuildingRegistration to)
        {
            if (e == null || to == null || !e.IsCandidate || !GoingPublic.Mine(to) || !Fits(e, to))
            {
                return false;
            }
            e.assignedAddress = to.Address;
            EmployeeHelper.HireCandidate(e);
            return !e.IsCandidate;
        }

        /// <summary>
        /// Somebody of the player's moved from the store they work at to another, as MyEmployees does: the old store's
        /// automatic schedule stopped, their shifts there taken off, the new store given them idle, both stores' demands
        /// read again. Both stores must be the player's.
        /// </summary>
        public static bool Move(EmployeeInstance e, BuildingRegistration from, BuildingRegistration to)
        {
            if (e == null || from == null || to == null || e.IsCandidate || !ManagerGame.At(e, from) || !GoingPublic.Mine(from) || !GoingPublic.Mine(to) || !Fits(e, to))
            {
                return false;
            }
            Address was = e.assignedAddress;
            BizManSchedule.AbortAutoFillForBusiness(from);
            EmployeeHelper.UnassignEmployeeFromAllWorkshifts(e);
            e.assignedAddress = to.Address;
            CustomerDemandHelper.ReloadCachedFulfilled(was);
            CustomerDemandHelper.ReloadCachedFulfilled(to.Address);
            e.AddTodoTask(TodoTaskType.EmployeeIdle);
            GlobalEvents.onBuildingRegistrationChange?.Invoke(was);
            SaveGameManager.MarkChange();
            return ManagerGame.At(e, to);
        }

        /// <summary>Who works at a place now and could be moved to another (not the place's manager, nor the player).</summary>
        public static void Movable(RotaPlace from, BuildingRegistration to, string keep, List<EmployeeInstance> into)
        {
            into.Clear();
            List<EmployeeInstance> all = Save != null ? Save.EmployeeInstances : null;
            for (int i = 0; all != null && from != null && i < all.Count; i++)
            {
                EmployeeInstance e = all[i];
                if (e != null && !e.IsCandidate && !e.isBeingReplaced && !string.IsNullOrEmpty(e.id) && e.id != keep && ManagerGame.At(e, from.Reg) && Fits(e, to)
                    && e.characterData != null && e.characterData.skills != null && e.characterData.skills.Count > 0 && e.characterData.skills[0] != null && !Player(e))
                {
                    into.Add(e);
                }
            }
        }

        private static bool Player(EmployeeInstance e)
        {
            try
            {
                return ReferenceEquals(e, PlayerHelper.GetPlayerEmployeeInstance());
            }
            catch (Exception)
            {
                return false;
            }
        }
    }
}
