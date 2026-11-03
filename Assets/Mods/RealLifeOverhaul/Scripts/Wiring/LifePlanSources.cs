using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Planner;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Planner rows the day already runs and no source listed: a delivery still to arrive, the night's staff pay, and a
    /// sponsored Flare post the player accepted. NPCs run the same deliveries and pay the same nights in their own books.
    /// The Planner shows only the player's. Nothing per frame: each source is asked when the Planner looks.
    /// </summary>
    public static class LifePlanSources
    {
        private static readonly DeliverySource Deliveries = new DeliverySource();
        private static readonly PaySource Pay = new PaySource();
        private static readonly PostSource Posts = new PostSource();

        public static void Connect()
        {
            PlannerDirector.Instance?.AddSource(Deliveries);
            PlannerDirector.Instance?.AddSource(Pay);
            PlannerDirector.Instance?.AddSource(Posts);
        }

        public static void Disconnect()
        {
        }

        /// <summary>A stop still to arrive on the player's routes. Rivals are supplied by the same trips in their books.</summary>
        private sealed class DeliverySource : IPlannerSource
        {
            private readonly List<TripDue> _due = new List<TripDue>(8);

            public string Id => "delivery";

            public bool Available => LogisticsDirector.Instance != null && LogisticsDirector.Instance.TripsOn;

            public void Collect(int fromDay, int toDay, PlannerList into)
            {
                LogisticsDirector director = LogisticsDirector.Instance;
                if (director == null)
                {
                    return;
                }
                director.DueStops(fromDay * PlannerTime.MinutesPerDay, (toDay + 1) * PlannerTime.MinutesPerDay, _due);
                for (int i = 0; i < _due.Count; i++)
                {
                    TripDue due = _due[i];
                    int day = PlannerTime.DayOf(due.Arrive);
                    PlannerEntry e = into.Add(Id, due.Arrive, i, PlannerCategory.Business, day);
                    if (e == null)
                    {
                        continue;
                    }
                    e.Start = PlannerTime.OfDay(due.Arrive);
                    e.End = e.Start + 30;
                    if (into.WantText)
                    {
                        bool coffee = due.Item != null && due.Item.IndexOf("coffee", System.StringComparison.OrdinalIgnoreCase) >= 0;
                        e.Title = PlannerText.T(coffee ? "reallife_ui_life_coffee_delivery" : "reallife_ui_life_delivery");
                        e.What = PlannerText.T("reallife_ui_life_delivery_what");
                    }
                }
            }
        }

        /// <summary>Staff pay for the player's businesses, the night the day's wages are booked. One line, however many shops.</summary>
        private sealed class PaySource : IPlannerSource
        {
            private readonly List<BuildingRegistration> _regs = new List<BuildingRegistration>(8);

            public string Id => "payroll";

            public bool Available => true;

            public void Collect(int fromDay, int toDay, PlannerList into)
            {
                _regs.Clear();
                BusinessGame.Businesses(_regs);
                int staff = 0;
                for (int i = 0; i < _regs.Count; i++)
                {
                    List<EmployeeInstance> people = BusinessGame.Staff(_regs[i]);
                    staff += people != null ? people.Count : 0;
                }
                if (staff <= 0)
                {
                    return;
                }
                for (int day = fromDay; day <= toDay; day++)
                {
                    PlannerEntry e = into.Add(Id, 1, day, PlannerCategory.Business, day);
                    if (e == null)
                    {
                        continue;
                    }
                    e.Start = 23 * 60;
                    e.End = 23 * 60 + 30;
                    if (into.WantText)
                    {
                        e.Title = PlannerText.T("reallife_ui_life_staff_pay", "count", staff.ToString());
                        e.What = PlannerText.T("reallife_ui_life_staff_pay_what");
                    }
                }
            }
        }

        /// <summary>A sponsored post the player accepted, on the day it is next due. The hour is the afternoon, the offer stores the day.</summary>
        private sealed class PostSource : IPlannerSource
        {
            private readonly List<FlareOffer> _offers = new List<FlareOffer>(4);

            public string Id => "flarepost";

            public bool Available => FlareDirector.Instance != null;

            public void Collect(int fromDay, int toDay, PlannerList into)
            {
                FlareDirector director = FlareDirector.Instance;
                if (director == null)
                {
                    return;
                }
                director.DuePosts(fromDay, toDay, _offers);
                for (int i = 0; i < _offers.Count; i++)
                {
                    FlareOffer offer = _offers[i];
                    PlannerEntry e = into.Add(Id, offer.Id, offer.Next, PlannerCategory.Business, offer.Next);
                    if (e == null)
                    {
                        continue;
                    }
                    e.Start = 14 * 60;
                    e.End = 15 * 60;
                    e.Deadline = true;
                    if (into.WantText)
                    {
                        e.Title = PlannerText.T("reallife_ui_life_flare_post", "name", string.IsNullOrEmpty(offer.Name) ? PlannerText.T("reallife_ui_life_a_business") : offer.Name);
                        e.What = PlannerText.T("reallife_ui_life_flare_post_what");
                    }
                }
            }
        }
    }
}
