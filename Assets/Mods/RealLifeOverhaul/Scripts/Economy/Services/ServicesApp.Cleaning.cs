using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The three tabs of the cleaning service. Cleaning: the round's businesses and the player's other shops in the
    /// table; in the box the service as a whole, or one business with its visiting pattern. Crew: every cleaner the
    /// player employs. Contracts: offers from other businesses and the contracts that run.
    /// </summary>
    public sealed partial class ServicesApp
    {
        private readonly List<BuildingRegistration> _shops = new List<BuildingRegistration>(16);
        private readonly List<EmployeeInstance> _cleaners = new List<EmployeeInstance>(16);
        private readonly CrewMember _member = new CrewMember();

        // ---------------------------------------------------------------- Cleaning

        private void WriteCleaning(InfoBox box, ServicesState state, int day, ref string selected)
        {
            CleaningClient picked = null;
            bool isPicked;
            _spec = ServicesTables.Cleaning;
            int now = ServicesGame.Now, planned = 0, left = 0;
            for (int i = 0; i < state.Clients.Count; i++)
            {
                CleaningClient c = state.Clients[i];
                int need = _director.NeedMinutes(c);
                string next = c.Unserved > 0 ? ServicesText.Why(c.Unserved)
                    : c.NextStart >= 0 ? ServicesText.T(c.NextStart <= now ? "reallife_svc_row_in_now" : "reallife_svc_row_next", "time", ServicesText.Clock(c.NextStart), "name", c.NextBy)
                    : !c.Outside && !c.Open ? ServicesText.T("reallife_svc_row_closed") : ServicesText.T(c.Mode == ServicesCatalogue.ModeAsNeeded ? "reallife_svc_row_waiting" : "reallife_svc_row_done");
                left += c.Unserved > 0 ? 1 : 0;
                TableRow row = Pick("cli:" + c.Place, c.Outside ? ServicesTables.Contract : ServicesTables.Own, ref selected, out isPicked);
                row.Set(0, c.Name);
                row.Set(1, ServicesText.District(c.District));
                if (need > 0)
                {
                    row.Set(2, need.ToString(), need);
                }
                else
                {
                    row.Set(2, ServicesText.T("reallife_svc_row_needs_unknown"));
                }
                row.Set(3, c.MinutesToday.ToString(), c.MinutesToday);
                row.Set(4, ServicesText.Trend(c.Now, c.CleanBefore), c.Now, c.Now < ServicesCatalogue.CleanlinessWarn ? TableRow.Yellow : TableRow.Plain);
                if (c.NextStart >= 0 && c.Unserved == 0)
                {
                    row.Set(5, next, c.NextStart);
                }
                else
                {
                    row.Set(5, next, c.Unserved > 0 ? TableRow.Red : TableRow.Plain);
                }
                row.Set(6, ServicesText.T(c.Outside ? "reallife_svc_row_contract" : "reallife_svc_row_on"));
                picked = isPicked ? c : picked;
            }
            IReadOnlyList<DayVisit> visits = _director.Visits;
            for (int i = 0; i < visits.Count; i++)
            {
                planned += visits[i].End > now ? 1 : 0;
            }
            BuildingRegistration adding = null;
            ServicesGame.Shops(_shops);
            for (int i = 0; i < _shops.Count; i++)
            {
                string place = ServicesGame.Place(_shops[i]);
                if (state.Client(place) != null)
                {
                    continue;
                }
                int clean = ServicesGame.Cleanliness(_shops[i]);
                TableRow row = Pick("add:" + place, ServicesTables.NotOn, ref selected, out isPicked);
                row.Set(0, ServicesGame.Name(_shops[i]));
                row.Set(1, ServicesText.District(_shops[i].Neighborhood));
                row.Set(4, clean.ToString(), clean, clean < ServicesCatalogue.CleanlinessWarn ? TableRow.Yellow : TableRow.Plain);
                row.Set(6, ServicesText.T("reallife_svc_row_not_on"));
                adding = isPicked ? _shops[i] : adding;
            }
            _shops.Clear();
            if (_rows.Count == 0)
            {
                Say("none", ServicesText.T("reallife_svc_row_no_shop"));
            }
            if (picked != null)
            {
                WriteClient(box, picked, day);
            }
            else if (adding != null)
            {
                WriteAdd(box, adding);
            }
            else
            {
                WriteService(box, state, planned, left);
            }
        }

        // The service as a whole: who is on it, what it costs against one cleaner per shop, the van, today's round.
        private void WriteService(InfoBox box, ServicesState state, int planned, int left)
        {
            int wages, van, own = _director.OwnWeek(), fees = _director.ContractWeekFees();
            int service = _director.ServiceWeek(out wages, out van);
            box.Title(ServicesText.T("reallife_svc_tab_cleaning"));
            if (state.Crew.Count == 0)
            {
                box.Line(ServicesText.T("reallife_svc_service_no_crew"));
            }
            box.Line(ServicesText.T("reallife_svc_service_who", "crew", state.Crew.Count.ToString(), "businesses", state.Clients.Count.ToString(), "contracts", ContractRules.Running(state).ToString()));
            box.Row("reallife_svc_service_week", ServicesText.T("reallife_svc_service_week_value", "amount", ServicesText.Money(service), "wages", ServicesText.Money(wages), "van", ServicesText.Money(van)));
            box.Row("reallife_svc_service_own", ServicesText.Money(own));
            if (fees > 0)
            {
                box.Row("reallife_svc_service_fees", ServicesText.T("reallife_svc_service_fees_value", "amount", ServicesText.Money(fees), "booked", ServicesText.Signed(ServicesState.Week(state.FeeIncome))));
            }
            box.Line(ServicesText.T(left > 0 ? "reallife_svc_service_round_left" : "reallife_svc_service_round", "count", planned.ToString(), "left", left.ToString()));
            if (!box.HasSwitch || !box.Switch("reallife_svc_van", state.Van, on => _director.SetVan(on)))
            {
                bool van2 = state.Van;
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, van2 ? "reallife_svc_van_off" : "reallife_svc_van_on", () => _director.SetVan(!van2));
            }
            box.Line(ServicesText.T(state.Van ? "reallife_svc_van_says_on" : "reallife_svc_van_says_off", "cost", ServicesText.Money(ServicesCatalogue.AtPrices(ServicesCatalogue.VanCostPerKm, ServicesGame.Index))));
            if (state.Crew.Count > 0)
            {
                box.Line(ServicesText.T("reallife_svc_service_pick"));
            }
            Guide(box);
        }

        // One business on the round: how it stands, and how it is served.
        private void WriteClient(InfoBox box, CleaningClient c, int day)
        {
            string place = c.Place;
            int need = _director.NeedMinutes(c);
            box.Title(c.Name);
            if (c.Unserved > 0)
            {
                box.Line(ServicesText.Why(c.Unserved));
            }
            else if (!c.Outside && !c.Open)
            {
                box.Line(ServicesText.T("reallife_svc_client_closed"));
            }
            box.Row("reallife_svc_client_clean", ServicesText.Trend(c.Now, c.CleanBefore));
            box.Line(need > 0
                ? ServicesText.T("reallife_svc_client_needs", "need", ServicesText.Minutes(need), "today", ServicesText.Minutes(c.MinutesToday), "before", ServicesText.Minutes(c.MinutesBefore))
                : ServicesText.T("reallife_svc_client_needs_unknown", "today", ServicesText.Minutes(c.MinutesToday)));
            if (c.LastDay > 0)
            {
                box.Line(ServicesText.T(c.LastDay == day ? "reallife_svc_client_last_today" : "reallife_svc_client_last", "name", c.LastCleaner, "time", ServicesText.Clock(c.LastMinute), "day", c.LastDay.ToString()));
            }
            if (c.Unserved == 0 && c.NextStart >= 0)
            {
                box.Line(ServicesText.T("reallife_svc_client_next", "name", c.NextBy, "from", ServicesText.Clock(c.NextStart), "to", ServicesText.Clock(c.NextEnd)));
            }
            if (c.Outside)
            {
                box.Line(ServicesText.T("reallife_svc_client_contract", "amount", ServicesText.Money(c.Fee), "standard", c.Standard.ToString()));
            }
            int mode = c.Mode;
            if (!box.HasSelector || !box.Selector(ServicesCatalogue.VisitsMost + 1, mode, ServicesText.Mode, m => _director.SetMode(place, m)))
            {
                box.Line(ServicesText.Mode(mode));
            }
            else if (mode == ServicesCatalogue.ModeAsNeeded)
            {
                int[] steps = ServicesCatalogue.Thresholds;
                int at = 0;
                for (int i = 0; i < steps.Length; i++)
                {
                    at = steps[i] <= c.Threshold ? i : at;
                }
                box.Selector(steps.Length, at, i => ServicesText.T("reallife_svc_threshold", "value", ServicesCatalogue.Thresholds[i].ToString()), i => _director.SetThreshold(place, ServicesCatalogue.Thresholds[i]));
            }
            else
            {
                for (int k = 0; k < mode; k++)
                {
                    int visit = k;
                    box.Selector(24, c.Hours[k], h => ServicesText.T("reallife_svc_visit_at", "number", (visit + 1).ToString(), "time", ServicesText.Hour(h)), h => _director.SetHour(place, visit, h));
                }
            }
            Transform buttons = box.ButtonRow();
            if (!c.Outside)
            {
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_svc_client_remove", () =>
                {
                    _pick = null;
                    _director.RemoveClient(place);
                });
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            Guide(box);
        }

        // A shop of the player's that is not on the round.
        private void WriteAdd(InfoBox box, BuildingRegistration reg)
        {
            string place = ServicesGame.Place(reg);
            int hours = ServicesGame.OwnCleaningHours(reg);
            box.Title(ServicesGame.Name(reg));
            box.Row("reallife_svc_client_clean", ServicesGame.Cleanliness(reg).ToString());
            box.Line(ServicesText.T(hours > 0 ? "reallife_svc_add_own_hours" : "reallife_svc_add_no_hours", "hours", hours.ToString()));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_svc_add_do", () =>
            {
                _pick = "cli:" + place;
                _director.AddClient(place);
            });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            box.Line(ServicesText.T("reallife_svc_add_says"));
            Guide(box);
        }

        // ---------------------------------------------------------------- Crew

        private void WriteCrew(InfoBox box, ServicesState state, ref string selected)
        {
            EmployeeInstance picked = null;
            bool isPicked;
            _spec = ServicesTables.Crew;
            ServicesGame.Cleaners(_cleaners);
            for (int i = 0; i < _cleaners.Count; i++)
            {
                ServicesGame.Fill(_member, _cleaners[i]);
                string today = !_member.Present ? ServicesText.T("reallife_svc_crew_sick")
                    : _member.TodayFrom >= 0 ? ServicesText.T("reallife_svc_crew_shift", "from", ServicesText.Clock(_member.TodayFrom), "to", ServicesText.Clock(Mathf.Min(_member.TodayTo, 24 * 60 - 1)))
                    : ServicesText.T("reallife_svc_crew_no_shift");
                bool crew = state.InCrew(_member.Id);
                int week = (int)((long)_member.Wage * _member.WeekHours / 100);
                TableRow row = Pick("emp:" + _member.Id, crew ? ServicesTables.OnCrew : ServicesTables.OffCrew, ref selected, out isPicked);
                row.Set(0, _member.Name);
                row.Set(1, _member.Base != null ? _member.BaseName : ServicesText.T("reallife_svc_crew_no_base"));
                row.Set(2, _member.Skill.ToString(), _member.Skill);
                row.Set(3, _member.Mood + "%", _member.Mood);
                row.Set(4, CompanyText.Wage(_member.Wage), _member.Wage / 100.0);
                row.Set(5, _member.WeekHours.ToString(), _member.WeekHours);
                row.Set(6, ServicesText.Money(week), week);
                if (_member.Present && _member.TodayFrom >= 0)
                {
                    row.Set(7, today, _member.TodayFrom);
                }
                else
                {
                    row.Set(7, today, crew ? TableRow.Yellow : TableRow.Plain);
                }
                row.Set(8, ServicesText.T(crew ? "reallife_svc_crew_on" : "reallife_svc_crew_off"), crew ? TableRow.Green : TableRow.Plain);
                row.NoTotal = !crew;   // (the total is the crew's wages a week)
                picked = isPicked ? _cleaners[i] : picked;
            }
            if (_cleaners.Count == 0)
            {
                Say("none", ServicesText.T("reallife_svc_row_no_cleaner"));
            }
            _cleaners.Clear();
            if (picked == null)
            {
                box.Title(ServicesText.T("reallife_svc_tab_crew"));
                box.Line(ServicesText.T("reallife_svc_crew_says_hire"));
                box.Line(ServicesText.T("reallife_svc_crew_says_hours"));
                box.Line(ServicesText.T("reallife_svc_crew_says_pay"));
                box.Line(ServicesText.T("reallife_svc_crew_says_pick"));
                Guide(box);
                _member.Employee = null;
                _member.Base = null;
                return;
            }
            ServicesGame.Fill(_member, picked);
            string id = _member.Id;
            bool on = state.InCrew(id);
            box.Title(_member.Name);
            if (_member.Base == null || _member.WeekHours == 0)
            {
                box.Line(ServicesText.T("reallife_svc_crew_needs_shift"));
            }
            box.Line(ServicesText.T("reallife_svc_crew_skill", "skill", _member.Skill.ToString(), "mood", _member.Mood.ToString(), "rate", (CleaningRules.Rate(_member.Skill, _member.Mood, 1) / 100).ToString()));
            box.Line(ServicesText.T("reallife_svc_crew_wage", "wage", CompanyText.Wage(_member.Wage), "hours", _member.WeekHours.ToString()));
            box.Row("reallife_svc_crew_base", _member.Base != null ? _member.BaseName : ServicesText.T("reallife_svc_crew_no_base"));
            IReadOnlyList<DayVisit> visits = _director.Visits;
            int shown = 0;
            for (int i = 0; on && i < visits.Count && shown < 4; i++)
            {
                DayVisit v = visits[i];
                CleaningClient c = v.CleanerId == id ? state.Client(v.Place) : null;
                if (c != null)
                {
                    box.Line(ServicesText.Fill(v.Skipped ? "reallife_svc_crew_visit_missed" : "reallife_svc_crew_visit", "from", ServicesText.Clock(v.Start), "to", ServicesText.Clock(v.End), "business", c.Name, "way", ServicesText.Distance(v.Metres)));
                    shown++;
                }
            }
            if (on && shown == 0)
            {
                box.Line(ServicesText.T("reallife_svc_crew_no_visits"));
            }
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, on ? InfoBox.Negative : InfoBox.Positive, on ? "reallife_svc_crew_take_off" : "reallife_svc_crew_put_on", () => _director.SetCrew(id, !on));
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
            Guide(box);
            _member.Employee = null;
            _member.Base = null;
        }

        // ---------------------------------------------------------------- Contracts

        private void WriteContracts(InfoBox box, ServicesState state, int day, ref string selected)
        {
            CleaningOffer offer = null;
            CleaningClient contract = null;
            bool isPicked;
            _spec = ServicesTables.Contracts;
            for (int i = 0; i < state.Offers.Count; i++)
            {
                CleaningOffer o = state.Offers[i];
                TableRow row = Pick("offer:" + o.Place, ServicesTables.Offer, ref selected, out isPicked);
                row.Set(0, o.Name);
                row.Set(1, ServicesText.District(o.District));
                row.Set(2, ServicesText.Money(o.Fee), o.Fee);
                row.Set(3, o.Standard.ToString(), o.Standard);
                row.Set(5, ServicesText.T("reallife_econ_day", "day", o.Until.ToString()), o.Until);
                row.Set(6, ServicesText.T("reallife_svc_row_answer_by", "day", o.Until.ToString()), TableRow.Yellow);
                row.NoTotal = true;   // (the total is the fees of the contracts that run)
                offer = isPicked ? o : offer;
            }
            for (int i = 0; i < state.Clients.Count; i++)
            {
                CleaningClient c = state.Clients[i];
                if (!c.Outside)
                {
                    continue;
                }
                int weekEnds = c.WeekFrom + ServicesCatalogue.ContractWeekDays;
                TableRow row = Pick("con:" + c.Place, ServicesTables.Running, ref selected, out isPicked);
                row.Set(0, c.Name);
                row.Set(1, ServicesText.District(c.District));
                row.Set(2, ServicesText.Money(c.Fee), c.Fee);
                row.Set(3, c.Standard.ToString(), c.Standard);
                row.Set(4, ServicesText.Trend(c.Now, c.CleanBefore), c.Now, c.Now < c.Standard ? TableRow.Red : TableRow.Plain);
                row.Set(5, ServicesText.T("reallife_econ_day", "day", weekEnds.ToString()), weekEnds);
                row.Set(6, ServicesText.T("reallife_svc_row_week", "under", c.DaysUnder.ToString(), "most", ServicesCatalogue.ContractDaysUnderMost.ToString()),
                    c.DaysUnder > ServicesCatalogue.ContractDaysUnderMost ? TableRow.Red : TableRow.Plain);
                contract = isPicked ? c : contract;
            }
            if (_rows.Count == 0)
            {
                Say("none", ServicesText.T(state.Crew.Count == 0 ? "reallife_svc_row_no_offer_no_crew" : "reallife_svc_row_no_offer"));
            }
            if (offer != null)
            {
                string place = offer.Place;
                box.Title(offer.Name);
                box.Row("reallife_svc_contract_district", ServicesText.District(offer.District));
                box.Row("reallife_svc_contract_fee", ServicesText.T("reallife_svc_row_a_week", "amount", ServicesText.Money(offer.Fee)));
                box.Line(ServicesText.T("reallife_svc_contract_standard", "standard", offer.Standard.ToString(), "days", ServicesCatalogue.ContractDaysUnderMost.ToString()));
                box.Line(ServicesText.T("reallife_svc_contract_work", "size", offer.Sqm.ToString(), "minutes", ServicesText.Minutes(CleaningRules.Minutes(ContractRules.DirtDay(offer.Capacity), _director.CrewRate(CleaningRules.Force(offer.Sqm))))));
                box.Line(ServicesText.T("reallife_svc_contract_penalty", "share", ServicesCatalogue.ContractPenaltyPct.ToString(), "weeks", ServicesCatalogue.ContractMissesMost.ToString()));
                box.Line(ServicesText.T("reallife_svc_contract_until", "day", offer.Until.ToString()));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_svc_contract_take", () =>
                {
                    _pick = "con:" + place;
                    _director.AcceptOffer(place);
                });
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_svc_contract_refuse", () =>
                {
                    _pick = null;
                    _director.RefuseOffer(place);
                });
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
                Guide(box);
                return;
            }
            if (contract != null)
            {
                string place = contract.Place;
                box.Title(contract.Name);
                if (contract.Missed > 0)
                {
                    box.Line(ServicesText.T("reallife_svc_contract_missed", "count", contract.Missed.ToString(), "most", ServicesCatalogue.ContractMissesMost.ToString()));
                }
                box.Row("reallife_svc_contract_fee", ServicesText.T("reallife_svc_row_a_week", "amount", ServicesText.Money(contract.Fee)));
                box.Row("reallife_svc_client_clean", ServicesText.T("reallife_svc_row_standard", "now", ServicesText.Trend(contract.Now, contract.CleanBefore), "standard", contract.Standard.ToString()));
                box.Line(ServicesText.T("reallife_svc_contract_week", "under", contract.DaysUnder.ToString(), "most", ServicesCatalogue.ContractDaysUnderMost.ToString(), "day", (contract.WeekFrom + ServicesCatalogue.ContractWeekDays).ToString()));
                box.Row("reallife_svc_contract_earned", ServicesText.Money(contract.Earned));
                box.Line(ServicesText.T("reallife_svc_contract_pattern", "mode", ServicesText.Mode(contract.Mode)));
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, "reallife_svc_contract_visits", () =>
                {
                    _tab = ServicesDirector.TabCleaning;
                    _pick = "cli:" + place;
                    Refresh();
                });
                InfoBox.Button(buttons, InfoBox.Negative, "reallife_svc_contract_end", () => _director.EndContract(place));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_co_back", Back);
                Guide(box);
                return;
            }
            box.Title(ServicesText.T("reallife_svc_tab_contracts"));
            box.Row("reallife_svc_contracts_running", ContractRules.Running(state).ToString());
            box.Row("reallife_svc_service_fees", ServicesText.T("reallife_svc_service_fees_value", "amount", ServicesText.Money(_director.ContractWeekFees()), "booked", ServicesText.Signed(ServicesState.Week(state.FeeIncome))));
            box.Line(ServicesText.T("reallife_svc_contracts_says", "days", ServicesCatalogue.OfferEveryDays.ToString(), "most", ServicesCatalogue.OffersMost.ToString()));
            box.Line(ServicesText.T("reallife_svc_contracts_says_week", "days", ServicesCatalogue.ContractDaysUnderMost.ToString(), "share", ServicesCatalogue.ContractPenaltyPct.ToString(), "weeks", ServicesCatalogue.ContractMissesMost.ToString()));
            Guide(box);
        }
    }
}
