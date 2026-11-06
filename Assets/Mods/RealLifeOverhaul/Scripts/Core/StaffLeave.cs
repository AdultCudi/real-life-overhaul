using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Entities;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The one way this mod lets one of the player's employees go (<see cref="StaffLeaveRules"/>): ComplaintResign's
    /// steps in the game's order, then off the save's list and the game's index as EmployeeHelper.RunHourly takes one
    /// off, so the count is paid once, one notice comes, and the game's hourly check does not resign them again. Never
    /// RemoveEmployee, the game's firing, which Going Public sends to a merged company's owner. Going Public: another
    /// player's employee is left alone; nothing is sent. Nothing per frame; one pass over the employees a leaving.
    /// </summary>
    public static class StaffLeave
    {
        public static IModLogger Logger;

        // Which day an employee last worked an hour: the game's private field; null on a game without it.
        private static readonly FieldInfo LastWorkedField = GameContract.LastWorkedDay.Field;

        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>
        /// Lets an employee go the game's own way, once; <paramref name="who"/> names the part of the mod, for the log.
        /// False, with nothing changed, for somebody not the player's to let go now (<see cref="StaffLeaveRules.Why"/>).
        /// </summary>
        public static bool Resign(EmployeeInstance e, string who)
        {
            GameInstance save = SaveGameManager.Current;
            List<EmployeeInstance> staff = save != null ? save.EmployeeInstances : null;
            bool exists = e != null && staff != null;
            StaffLeaveRules.Refusal why = StaffLeaveRules.Why(exists, exists && e.IsCandidate, exists && Listed(e, staff), exists && e.isBeingReplaced,
                exists && Foreign(e));
            string name = NameOf(e);
            if (why != StaffLeaveRules.Refusal.None)
            {
                Logger?.Info("Staff: " + name + " is not let go by " + who + ": " + StaffLeaveRules.Words(why) + ".");
                return false;
            }
            int counted = e.workedHoursToday;
            bool known = LastWorkedField != null;
            int lastWorked = known ? (int)LastWorkedField.GetValue(e) : -1;
            int pays = StaffLeaveRules.HoursPaid(counted, lastWorked, known, save.Day, save.Hour);
            List<(string, BuildingRegistration)> notices = EmployeeHelper.ResignedEmployeesAndLastRegistrationAssigned;
            int noticesBefore = notices.Count;
            e.poached = false;                         // ComplaintResign's steps: nobody named for the headhunter's reason
            e.complaintData.isComplaining = false;
            e.satisfaction = 0f;
            if (counted > 0 && pays == 0)
            {
                e.workedHoursToday = 0;                // the game's night paid it; its employees' day clears it in a moment, as here
            }
            bool replaced = e.ReplaceAutomaticallyOnResign();
            if (!replaced)
            {
                e.Resign();                            // the shifts and tasks, one wage (the count), one notice with the next hour's
                TakeOff(e, staff);                     // off the list now: nothing for the game's hourly check to resign again
            }
            Logger?.Info("Staff: " + name + " leaves (" + who + "), the game's own resignation: "
                         + (replaced ? "the headhunter replaces them, as their plan has it."
                             : pays + " hour(s) of their count paid" + (counted > pays ? " (" + counted + " counted " + (known ? "on day " + lastWorked : "before the day's first hour")
                                                                               + ", which the game's night has paid)" : string.Empty)
                               + ", off the game's list at once; the game's notice comes with its next hour."));
            if (Verbose)
            {
                bool listed = Listed(e, staff);
                int added = notices.Count - noticesBefore;
                bool right = replaced ? e.isBeingReplaced || listed : !listed && added == 1 && e.workedHoursToday == pays;
                Logger?.Info("Staff check (leaving, " + name + "): " + (replaced ? "handed to the headhunter, " : string.Empty) + "the count " + counted
                             + (known ? ", last worked on day " + lastWorked : ", the day it is of unread") + ", today " + save.Day + " hour " + save.Hour + ", "
                             + pays + " hour(s) paid by the resignation, " + added + " notice(s) waiting, on the list after: " + listed + ", satisfaction "
                             + e.satisfaction.ToString("0") + ": " + (right ? "PASS" : "FAIL (wanted: off the list, one notice, the owed hours paid once)"));
            }
            RivalOfferGuard.Look("an employee let go by " + who);
            return true;
        }

        // That very object is on the save's list, the one the game's hours go over.
        private static bool Listed(EmployeeInstance e, List<EmployeeInstance> staff)
        {
            for (int i = 0; i < staff.Count; i++)
            {
                if (ReferenceEquals(staff[i], e))
                {
                    return true;
                }
            }
            return false;
        }

        // Off the save's list and the game's index, as EmployeeHelper.RunHourly takes off one who resigned in it.
        private static void TakeOff(EmployeeInstance e, List<EmployeeInstance> staff)
        {
            for (int i = staff.Count - 1; i >= 0; i--)
            {
                if (ReferenceEquals(staff[i], e))
                {
                    staff.RemoveAt(i);
                }
            }
            if (!string.IsNullOrEmpty(e.id) && EmployeeHelper.EmployeeInstancesDictionary.TryGetValue(e.id, out EmployeeInstance indexed) && ReferenceEquals(indexed, e))
            {
                EmployeeHelper.EmployeeInstancesDictionary.Remove(e.id);
            }
        }

        private static bool Foreign(EmployeeInstance e)
        {
            if (!GoingPublic.InSession || e.assignedAddress == null)
            {
                return false;
            }
            BuildingRegistration reg = BuildingHelper.GetBuildingRegistration(e.assignedAddress);
            return reg != null && GoingPublic.Foreign(reg);
        }

        private static string NameOf(EmployeeInstance e)
        {
            return e != null && e.characterData != null && !string.IsNullOrEmpty(e.characterData.name) ? e.characterData.name : "?";
        }
    }
}
