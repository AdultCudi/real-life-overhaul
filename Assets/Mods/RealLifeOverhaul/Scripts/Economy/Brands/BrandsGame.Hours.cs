using System;
using System.Collections.Generic;
using Helpers;
using UI;
using UI.Smartphone.Apps.BizMan.Schedule;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>The game part for opening hours copied from the flagship, with the game's own follow-ups.</summary>
    internal static partial class BrandsGame
    {
        /// <summary>The flagship's opening hours at another shop, as the game's hour switches leave them; no shift changes. Returns how many days changed.</summary>
        public static int ShareHours(BuildingRegistration from, BuildingRegistration to)
        {
            if (from == null || to == null || from.scheduleDays == null || to.scheduleDays == null || ReferenceEquals(from, to))
            {
                return 0;
            }
            BizManSchedule.AbortAutoFillForBusiness(to);
            int changed = 0;
            for (int i = 0; i < to.scheduleDays.Count; i++)
            {
                ScheduleDay day = to.scheduleDays[i], source = null;
                for (int k = 0; day != null && k < from.scheduleDays.Count; k++)
                {
                    source = from.scheduleDays[k] != null && from.scheduleDays[k].day == day.day ? from.scheduleDays[k] : source;
                }
                if (day == null || source == null || SameHours(day, source))
                {
                    continue;
                }
                day.isOpen = source.isOpen;
                var slots = new List<OpeningHourSlot>(source.openingHourSlots != null ? source.openingHourSlots.Count : 0);
                for (int k = 0; source.openingHourSlots != null && k < source.openingHourSlots.Count; k++)
                {
                    if (source.openingHourSlots[k] != null)
                    {
                        slots.Add(source.openingHourSlots[k].Clone());
                    }
                }
                day.openingHourSlots = slots;
                changed++;
            }
            if (changed > 0)
            {
                After(to);
            }
            return changed;
        }

        private static bool SameHours(ScheduleDay a, ScheduleDay b)
        {
            int count = a.openingHourSlots != null ? a.openingHourSlots.Count : 0;
            if (a.isOpen != b.isOpen || count != (b.openingHourSlots != null ? b.openingHourSlots.Count : 0))
            {
                return false;
            }
            for (int i = 0; i < count; i++)
            {
                OpeningHourSlot x = a.openingHourSlots[i], y = b.openingHourSlots[i];
                if (x == null || y == null || x.startingHour != y.startingHour || x.endingHour != y.endingHour)
                {
                    return false;
                }
            }
            return true;
        }

        // What the game does when its schedule screen closes after the hours changed (BizManSchedule.OnDisable).
        private static void After(BuildingRegistration reg)
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            Guarded("the security level", () => reg.UpdateSecurityLevel());
            Guarded("the game's schedule screen", () =>
            {
                BizManBusiness shown = uis != null && uis.fullMenu != null && uis.fullMenu.bizMan != null ? uis.fullMenu.bizMan.business : null;
                BizManSchedule schedule = uis != null && uis.fullMenu != null ? uis.fullMenu.schedule : null;
                if (shown != null && shown.isActiveAndEnabled && shown.buildingRegistration == reg && schedule != null && schedule.isActiveAndEnabled)
                {
                    schedule.LoadScheduler();
                }
            });
            Guarded("the game's schedule events", () => GameEvent.Invoke("ba:gameevent_changedbuildingregistration"));
            Guarded("today's customers and alerts", () =>
            {
                CustomerDraw.Redraw(reg);   // the game's own redraw; Core/CustomerDraw keeps the hour under way while Meaningful Ambience is on
                BusinessHelper.CheckIfTheaterHasNoActors(reg);
                if (!BusinessHelper.IsMissingEmployeeTaskActive(reg))
                {
                    BusinessHelper.ForceRecheckMissingEmployeeAlert(reg);
                }
                GlobalEvents.onBuildingRegistrationChange?.Invoke(reg.Address);
                var work = uis != null && uis.playerActivityUI != null ? uis.playerActivityUI.GetCurrentActivity as PlayerActivity.WorkActivity : null;
                if (work != null)
                {
                    work.SetTimeToWork();
                    uis.playerActivityUI.UpdateActivityDisplay();
                }
            });
            SaveGameManager.MarkChange();
        }

        // One of the game's own follow-ups: a fault in it is logged and the rest still run.
        private static void Guarded(string what, Action work)
        {
            try
            {
                work();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Brands: " + what + " could not be brought up to date after the opening hours were changed; the hours themselves are in place.");
            }
        }
    }
}
