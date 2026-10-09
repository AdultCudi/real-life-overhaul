using System;
using Helpers;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Family
{
    /// <summary>
    /// Somewhere to be: a scan at the clinic, dinner at a restaurant, a school play, the wedding
    /// itself, a cot to buy. Each is a line in the save with a day and hours (or none: shopping
    /// waits), and a real building of a kind the city has. While one is due there is a line under
    /// the top bar and the game's own destination marker; at the door, or inside, a key prompt
    /// does it. One that is missed is handled by its own rule, gently, and never left hanging.
    /// Costs nothing while there are none, and a few comparisons a second while there are.
    /// </summary>
    internal static class Errands
    {
        public const string Dinner = "dinner";
        public const string Scan = "scan";
        public const string BirthClass = "class";
        public const string Birth = "birth";
        public const string Checkup = "checkup";
        public const string AdoptFile = "adopt_file";
        public const string HomeVisit = "home_visit";
        public const string AdoptMeet = "adopt_meet";
        public const string BuyCot = "buy_cot";
        public const string BuyPushchair = "buy_pushchair";
        public const string BuySupplies = "buy_supplies";
        public const string SchoolRun = "school_run";
        public const string ParentsEvening = "parents_evening";
        public const string SchoolPlay = "school_play";
        public const string Party = "party";
        public const string Counselling = "counselling";
        public const string Holiday = "holiday";
        public const string Wedding = "wedding";
        public const string Flowers = "flowers";

        private sealed class Spec
        {
            public string Kind;
            public string[] Places;      // kinds of building; null for the home (or a place of the errand's own)
            public bool AtHome;
            public string Title;         // the line under the top bar: {place} is the venue's name, {text} the last hour
            public string Prompt;        // at the door: {name} is the child or the partner
            public Action<Errand> Do;
            public Action<Errand> Missed;
        }

        private static readonly Spec[] Specs =
        {
            new Spec { Kind = Wedding, Title = "reallife_family_errand_wedding", Prompt = "reallife_family_prompt_wedding", Do = e => WeddingDay.Begin(), Missed = e => WeddingDay.Missed() },
            new Spec { Kind = Birth, Places = FamilyPlaces.Hospital, Title = "reallife_family_errand_birth", Prompt = "reallife_family_prompt_birth", Do = Children.Birth, Missed = Children.BirthMissed },
            new Spec { Kind = Dinner, Places = FamilyPlaces.Restaurant, Title = "reallife_family_errand_dinner", Prompt = "reallife_family_prompt_dinner", Do = HomeLife.Dinner, Missed = e => HomeLife.StoodThemUp() },
            new Spec { Kind = Scan, Places = FamilyPlaces.Hospital, Title = "reallife_family_errand_scan", Prompt = "reallife_family_prompt_scan", Do = Children.Appointment, Missed = Children.AppointmentMissed },
            new Spec { Kind = BirthClass, Places = FamilyPlaces.Hospital, Title = "reallife_family_errand_class", Prompt = "reallife_family_prompt_class", Do = Children.Appointment, Missed = Children.AppointmentMissed },
            new Spec { Kind = Checkup, Places = FamilyPlaces.Hospital, Title = "reallife_family_errand_checkup", Prompt = "reallife_family_prompt_checkup", Do = Children.Checkup, Missed = Children.CheckupMissed },
            new Spec { Kind = AdoptFile, Places = FamilyPlaces.Agency, Title = "reallife_family_errand_adopt_file", Prompt = "reallife_family_prompt_adopt_file", Do = Children.AdoptFile },
            new Spec { Kind = HomeVisit, AtHome = true, Title = "reallife_family_errand_home_visit", Prompt = "reallife_family_prompt_home_visit", Do = Children.HomeVisit, Missed = Children.HomeVisitMissed },
            new Spec { Kind = AdoptMeet, Places = FamilyPlaces.Agency, Title = "reallife_family_errand_adopt_meet", Prompt = "reallife_family_prompt_adopt_meet", Do = Children.AdoptArrive, Missed = Children.AdoptMeetMissed },
            new Spec { Kind = SchoolRun, Places = FamilyPlaces.School, Title = "reallife_family_errand_school_run", Prompt = "reallife_family_prompt_school_run", Do = Children.SchoolRun, Missed = Children.SchoolRunMissed },
            new Spec { Kind = ParentsEvening, Places = FamilyPlaces.School, Title = "reallife_family_errand_parents_evening", Prompt = "reallife_family_prompt_parents_evening", Do = Children.ParentsEvening, Missed = Children.SchoolEventMissed },
            new Spec { Kind = SchoolPlay, Places = FamilyPlaces.Theatre, Title = "reallife_family_errand_school_play", Prompt = "reallife_family_prompt_school_play", Do = Children.SchoolPlay, Missed = Children.SchoolEventMissed },
            new Spec { Kind = Party, Places = FamilyPlaces.Restaurant, Title = "reallife_family_errand_party", Prompt = "reallife_family_prompt_party", Do = Children.VenueParty, Missed = Children.PartyMissed },
            new Spec { Kind = Counselling, Places = FamilyPlaces.Hospital, Title = "reallife_family_errand_counselling", Prompt = "reallife_family_prompt_counselling", Do = HardTimes.Session, Missed = HardTimes.SessionMissed },
            new Spec { Kind = Holiday, AtHome = true, Title = "reallife_family_errand_holiday", Prompt = "reallife_family_prompt_holiday", Do = Relatives.Dinner, Missed = Relatives.DinnerMissed },
            new Spec { Kind = BuyCot, Places = FamilyPlaces.Furniture, Prompt = "reallife_family_prompt_buy_cot", Do = Children.Buy },
            new Spec { Kind = BuyPushchair, Places = FamilyPlaces.BabyShop, Prompt = "reallife_family_prompt_buy_pushchair", Do = Children.Buy },
            new Spec { Kind = BuySupplies, Places = FamilyPlaces.Supplies, Prompt = "reallife_family_prompt_buy_supplies", Do = Children.Buy },
            new Spec { Kind = Flowers, Places = FamilyPlaces.Florist, Prompt = "reallife_family_prompt_flowers", Do = WeddingPlanner.OrderFlowers }
        };

        // The one errand being shown, and where it is: looked up when it becomes the one, not every second.
        private static Errand _shown;
        private static Venue _venue;
        private static bool _venueFound;
        private static string _lastHour;
        private static int _lastHourValue = -1;
        private static Venue _agency;
        private static bool _agencyFound, _agencyOpen;
        private static float _agencyNext;

        private static Spec Find(string kind)
        {
            for (int i = 0; i < Specs.Length; i++)
            {
                if (Specs[i].Kind == kind)
                {
                    return Specs[i];
                }
            }
            return null;
        }

        /// <summary>The kinds of building an errand is done at (for "show me the way"); null if it has none.</summary>
        public static string[] PlacesOf(string kind)
        {
            return Find(kind)?.Places;
        }

        /// <summary>Adds an errand for a day and hours. One of a kind at a time (per child, for the children's).</summary>
        public static Errand Add(string kind, int day, int fromHour, int toHour, int child = -1)
        {
            FamilySave s = FamilyState.S;
            for (int i = 0; i < s.Errands.Count; i++)
            {
                if (s.Errands[i].Kind == kind && s.Errands[i].Child == child)
                {
                    return s.Errands[i];
                }
            }
            var errand = new Errand { Kind = kind, Day = day, FromHour = fromHour, ToHour = toHour, Child = child };
            s.Errands.Add(errand);
            return errand;
        }

        public static bool DueNow(Errand e, int day, int hour)
        {
            return e.Day <= 0 || (e.Day == day && hour >= e.FromHour && hour < e.ToHour);
        }

        public static void Reset()
        {
            _shown = null;
            _venueFound = false;
            _agencyNext = 0f;
            _agencyFound = false;
        }

        /// <summary>Once a second while no scene and no sequence is on.</summary>
        public static void Tick(float now)
        {
            FamilySave s = FamilyState.S;
            if (s.Errands.Count == 0)
            {
                if (_shown != null)
                {
                    Reset();
                }
                return;
            }
            int day = TimeHelper.CurrentDay;
            int hour = TimeHelper.CurrentHour;
            FamilyDirector director = FamilyDirector.Instance;
            Vector3 player = PlayerHelper.GetPosition();

            // What has run out goes first, each by its own rule.
            for (int i = s.Errands.Count - 1; i >= 0; i--)
            {
                Errand e = s.Errands[i];
                if (e.Day > 0 && (day > e.Day || (day == e.Day && hour >= e.ToHour)))
                {
                    s.Errands.RemoveAt(i);
                    if (ReferenceEquals(e, _shown))
                    {
                        FamilyPlaces.Unguide();
                        Reset();
                    }
                    Spec spec = Find(e.Kind);
                    FamilyDirector.Logger?.Info("Family: errand '" + e.Kind + "' was missed.");
                    if (spec != null && spec.Missed != null)
                    {
                        director.Guarded(() => spec.Missed(e));
                    }
                }
            }

            // The first that is due now and dated is the one under the top bar; shopping only shows at the shop.
            Errand due = null;
            for (int i = 0; i < Specs.Length && due == null; i++)
            {
                if (Specs[i].Title == null)
                {
                    continue;
                }
                for (int k = 0; k < s.Errands.Count; k++)
                {
                    Errand e = s.Errands[k];
                    if (e.Kind == Specs[i].Kind && e.Day > 0 && DueNow(e, day, hour))
                    {
                        due = e;
                        break;
                    }
                }
            }
            if (!ReferenceEquals(due, _shown))
            {
                if (_shown != null)
                {
                    FamilyPlaces.Unguide();
                }
                _shown = due;
                _venueFound = false;
                _lastHour = null;
                if (due != null)
                {
                    Locate(due, player);
                    if (!due.Told)
                    {
                        due.Told = true;
                        Spec spec = Find(due.Kind);
                        FamilyText.Toast(NotificationType.Info, spec.Title, Fill(due, spec), 10f);
                    }
                }
            }
            if (due != null)
            {
                Spec spec = Find(due.Kind);
                if (hour != _lastHourValue)
                {
                    _lastHourValue = hour;
                    _lastHour = null;
                }
                director.Objective("reallife_family_objective_title", spec.Title, Where(due, spec), Who(due));
                if (Reached(due, spec, player))
                {
                    director.Prompt(spec.Prompt, Who(due), At(due, spec));
                    return;
                }
            }

            // Shopping, and anything else with no day: a prompt while standing in (or at the door of) the right kind of shop.
            for (int k = 0; k < s.Errands.Count; k++)
            {
                Errand e = s.Errands[k];
                if (e.Day > 0)
                {
                    continue;
                }
                Spec spec = Find(e.Kind);
                if (spec == null || spec.Places == null || spec.Do == null)
                {
                    continue;
                }
                bool here = FamilyPlaces.Inside(spec.Places);
                if (!here && e.Kind == AdoptFile)
                {
                    // An office that cannot be walked into is dealt with at its door. Looked up now and then, not every second.
                    if (now >= _agencyNext)
                    {
                        _agencyNext = now + 20f;
                        _agencyFound = FamilyPlaces.Nearest(spec.Places, player, out _agency);
                        try
                        {
                            _agencyOpen = _agencyFound && BuildingHelper.CanEnterBuilding(_agency.Building.building.Address);
                        }
                        catch (Exception)
                        {
                            _agencyOpen = false;
                        }
                    }
                    // (And a city with no such office at all: the papers go by post, from home.)
                    here = _agencyFound ? !_agencyOpen && FamilyPlaces.AtDoor(_agency, player, 5f) : FamilyPlaces.AtHome();
                }
                if (here)
                {
                    director.Prompt(spec.Prompt, Who(e), At(e, spec));
                    return;
                }
            }
        }

        // What the key does at the place, kept in fields so that asking for the prompt every second makes nothing new.
        private static Errand _atErrand;
        private static Spec _atSpec;
        private static readonly Action DoAt = () =>
        {
            Errand errand = _atErrand;
            Spec spec = _atSpec;
            if (errand != null && spec != null && spec.Do != null && FamilyState.S.Errands.Contains(errand))
            {
                FamilyDirector.Instance.Guarded(() => spec.Do(errand));
            }
        };

        private static readonly Action OpenRings = () => FamilyTalk.Open(() => Courtship.Rings(0));
        private static readonly Action OpenPlanner = () => FamilyTalk.Open(() => WeddingPlanner.Start(true));

        private static Action At(Errand errand, Spec spec)
        {
            _atErrand = errand;
            _atSpec = spec;
            return DoAt;
        }

        /// <summary>The key prompts for things that are not errands but happen at a real counter: rings at a jeweller, the wedding at a planner.</summary>
        public static bool Counters(FamilyDirector director)
        {
            if (!BuildingManager.IsInsideBuilding || !FamilyState.Together || JobDirector.OnShift || !Settings.FamilyWedding)
            {
                return false;
            }
            if (FamilyState.Bond == Bond.Dating && FamilyPlaces.Inside(FamilyPlaces.Jeweller))
            {
                director.Prompt("reallife_family_prompt_rings", Partner.Name, OpenRings);
                return true;
            }
            if (FamilyState.Bond == Bond.Engaged && FamilyPlaces.Inside(FamilyPlaces.Planner))
            {
                director.Prompt("reallife_family_prompt_planner", Partner.Name, OpenPlanner);
                return true;
            }
            return false;
        }

        private static void Locate(Errand e, Vector3 player)
        {
            Spec spec = Find(e.Kind);
            _venueFound = false;
            if (spec == null || spec.AtHome)
            {
                return;
            }
            if (e.Kind == Wedding)
            {
                WeddingPlan plan = FamilyState.S.Wedding;
                if (plan.Venue == 1)
                {
                    FamilyPlaces.Guide(new Vector3(plan.X, plan.Y, plan.Z), FamilyText.T("reallife_family_venue_outdoors"));
                    return;
                }
                _venueFound = FamilyPlaces.At(plan.Street, plan.Number, out _venue) || FamilyPlaces.Nearest(WeddingPlanner.KindsOf(plan.Venue), player, out _venue);
            }
            else if (!string.IsNullOrEmpty(e.Street))
            {
                _venueFound = FamilyPlaces.At(e.Street, e.Number, out _venue) || (spec.Places != null && FamilyPlaces.Nearest(spec.Places, player, out _venue));
            }
            else if (spec.Places != null)
            {
                _venueFound = FamilyPlaces.Nearest(spec.Places, PlayerHelper.GetCityPosition(), out _venue);
            }
            if (_venueFound)
            {
                FamilyPlaces.Guide(_venue);
            }
        }

        private static bool Reached(Errand e, Spec spec, Vector3 player)
        {
            if (spec.AtHome)
            {
                return FamilyPlaces.AtHome();
            }
            if (e.Kind == Wedding && FamilyState.S.Wedding.Venue == 1)
            {
                WeddingPlan plan = FamilyState.S.Wedding;
                Vector3 offset = new Vector3(plan.X, plan.Y, plan.Z) - player;
                return !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle && Mathf.Abs(offset.y) < 5f && offset.x * offset.x + offset.z * offset.z < 15f * 15f;
            }
            if (e.Kind == Wedding)
            {
                // The ceremony is held outside the venue's doors; a venue the city no longer has, wherever the player stands out of doors.
                return _venueFound ? FamilyPlaces.AtDoor(_venue, player, 14f) : !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle;
            }
            if (_venueFound)
            {
                return FamilyPlaces.Reached(_venue, string.IsNullOrEmpty(e.Street) ? spec.Places : null, player);
            }
            // The city has no such place (any more): it is done from home instead, so nothing is ever out of reach.
            return (spec.Places != null && FamilyPlaces.Inside(spec.Places)) || FamilyPlaces.AtHome();
        }

        private static string Where(Errand e, Spec spec)
        {
            // "<place>, until 18:00": rebuilt when the errand or the hour changes.
            if (_lastHour == null)
            {
                string place = spec.AtHome ? FamilyText.T("reallife_family_place_home")
                    : e.Kind == Wedding && FamilyState.S.Wedding.Venue == 1 ? FamilyText.T("reallife_family_venue_outdoors")
                    : _venueFound ? _venue.Name : FamilyText.T(e.Kind == Wedding ? "reallife_family_place_anywhere" : "reallife_family_place_home");
                _lastHour = FamilyText.T("reallife_family_objective_where", new Args { Place = place, N = e.ToHour.ToString() });
            }
            return _lastHour;
        }

        private static Args Fill(Errand e, Spec spec)
        {
            return new Args { Name = Partner.Name, Child = Who(e), Place = Where(e, spec), Text = Where(e, spec) };
        }

        // Whoever the errand is about: the child it names, else the partner.
        private static string Who(Errand e)
        {
            FamilySave s = FamilyState.S;
            return e.Child >= 0 && e.Child < s.Children.Count ? s.Children[e.Child].Name : Partner.Name;
        }

        /// <summary>The errands still to come, a line each, for the Family page.</summary>
        public static string Upcoming()
        {
            FamilySave s = FamilyState.S;
            var text = new System.Text.StringBuilder(160);
            for (int i = 0; i < s.Errands.Count; i++)
            {
                Errand e = s.Errands[i];
                Spec spec = Find(e.Kind);
                if (spec == null)
                {
                    continue;
                }
                if (text.Length > 0)
                {
                    text.Append('\n');
                }
                string what = FamilyText.T(spec.Prompt != null && spec.Title == null ? ShoppingLine(e.Kind) : spec.Title,
                    new Args { Name = Partner.Name, Child = Who(e), Place = string.Empty, Text = string.Empty });
                text.Append(e.Day > 0 ? FamilyText.T("reallife_family_page_date_line", new Args { Day = FamilyText.When(e.Day), Text = Trim(what), N = e.FromHour.ToString() }) : Trim(what));
            }
            return text.ToString();
        }

        private static string ShoppingLine(string kind)
        {
            switch (kind)
            {
                case BuyCot: return "reallife_family_todo_cot";
                case BuyPushchair: return "reallife_family_todo_pushchair";
                case BuySupplies: return "reallife_family_todo_supplies";
                default: return "reallife_family_todo_flowers";
            }
        }

        // A title's trailing "at <place>" is empty on the page: no dangling punctuation.
        private static string Trim(string line)
        {
            return line.TrimEnd(' ', ':', ',', '-');
        }
    }
}
