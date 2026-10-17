using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BigAmbitions.DayNightCycle;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>A trip under way, played as cards in the travel list: the departure, a choice and its outcome each day, the return.</summary>
    public sealed partial class Holidays
    {
        private const int SetOff = 101, Relax = 102, Explore = 103, Splurge = 104, NextDay = 105, Home = 106, Close = 107;   // list entries
        private const string ButtonDo = "reallife_travel_list_do";

        private Action<int> _onPick;
        private Action _onClosed;

        public bool ShowingCancel => _cancelLine != null;

        /// <summary>True while a trip is played through with no list, the game's window not being copyable.</summary>
        public bool Auto => _auto;

        public void Resume()
        {
            if (!TravelState.Away)
            {
                return;
            }
            if (TravelCatalogue.TripById(TravelState.TicketId) == null || !_director.CanSeclude(out _))
            {
                Abort();
                return;
            }
            _director.Seclude(true);
            ShowStep();
        }

        /// <summary>The trip cannot go on: it ends here, the player in the street, nothing owed either way.</summary>
        public void Abort()
        {
            if (TravelState.Away)
            {
                _director.Log("Travel: the trip was cut short.");
                TravelState.AddPast(TravelState.TicketId, TravelState.AwayStartDay, Mathf.Max(TimeHelper.CurrentDay - TravelState.AwayStartDay, 0));
            }
            _skipping = false;
            _auto = false;
            _cancelLine = null;
            TravelState.ClearAway();
            TravelState.ClearTicket();
            HotelStay.EndSleepEffects();
        }

        /// <summary>Drops a cancelled trip's notice and a trip played through; part of the way out of everything.</summary>
        public void ClearCancel()
        {
            _cancelLine = null;
            _auto = false;
        }

        /// <summary>The test button: wherever the trip is, the return card is next.</summary>
        public bool ReturnNow()
        {
            Trip trip = TravelState.Away ? TravelCatalogue.TripById(TravelState.TicketId) : null;
            if (trip == null || _skipping)
            {
                return false;
            }
            TravelState.AwayStep = 1 + 2 * trip.Days;
            ShowStep();
            return true;
        }

        public void Press(int id)
        {
            if (id == Close && _cancelLine != null)
            {
                _cancelLine = null;
                _director.Seclude(false);
                _director.Window.Close();
                return;
            }
            Trip trip = TravelState.Away ? TravelCatalogue.TripById(TravelState.TicketId) : null;
            if (trip == null || _skipping)
            {
                return;
            }
            int last = 1 + 2 * trip.Days;
            int step = TravelState.AwayStep;
            switch (id)
            {
                case SetOff:
                    if (step == 0)
                    {
                        TravelState.AwayStep = 1;
                        ShowStep();
                    }
                    break;
                case Relax:
                case Explore:
                case Splurge:
                    if (step >= 1 && step < last && step % 2 == 1)
                    {
                        Choose(trip, id == Relax ? TripChoice.Relax : id == Explore ? TripChoice.Explore : TripChoice.Splurge);
                    }
                    break;
                case NextDay:
                    if (step >= 2 && step < last && step % 2 == 0)
                    {
                        _director.Run(NextDayRoutine());
                    }
                    break;
                case Home:
                    if (step >= last)
                    {
                        ComeHome(trip);
                    }
                    break;
            }
        }

        private void Choose(Trip trip, TripChoice choice)
        {
            bool companion = TravelState.CompanionId != null;
            TravelEvent[] pool;
            switch (choice)
            {
                case TripChoice.Relax:
                    TravelEffects.Energy(15f);
                    TravelState.AwayRelaxed++;
                    pool = TravelCatalogue.RelaxEvents;
                    break;
                case TripChoice.Explore:
                    TravelEffects.Energy(-8f);
                    pool = TravelCatalogue.ExploreEvents;
                    break;
                default:
                    if (!TravelEffects.Pay(trip.Splurge, "reallife_transaction_holiday_spending"))
                    {
                        TravelText.Toast(NotificationType.Warning, TravelText.T("reallife_travel_trip_no_money"), 6f);
                        return;
                    }
                    TravelState.AwaySpent += trip.Splurge;
                    TravelEffects.Mood(1);
                    pool = TravelCatalogue.SplurgeEvents;
                    break;
            }
            TravelEvent happened = null;
            float roll = Random.value;
            if (companion && roll < 0.35f)
            {
                happened = TravelState.Deal(TravelCatalogue.CompanionEvents, true, () => Random.value);
            }
            else if (roll > 0.72f && trip.Events != null && trip.Events.Length > 0)
            {
                happened = OwnEvent(trip);
            }
            if (happened == null)
            {
                happened = TravelState.Deal(pool, companion, () => Random.value);
            }
            int before = Mathf.RoundToInt(SaveGameManager.Current.Money);
            TravelEffects.Apply(happened);
            if (happened != null)
            {
                int paid = before - Mathf.RoundToInt(SaveGameManager.Current.Money);
                if (paid > 0)
                {
                    TravelState.AwaySpent += paid;
                }
                if (happened.Souvenir)
                {
                    TravelState.AwaySouvenirs++;
                }
            }
            TravelState.AwayEvent = happened != null ? happened.Key : null;
            TravelState.AwayChoice = (int)choice;
            TravelState.AwayStep++;
            ShowStep();
        }

        // One of the destination's own events that this save has not had; null when all have been.
        private static TravelEvent OwnEvent(Trip trip)
        {
            int start = Random.Range(0, trip.Events.Length);
            for (int i = 0; i < trip.Events.Length; i++)
            {
                string key = trip.Events[(start + i) % trip.Events.Length];
                if (!TravelState.Seen.Contains(key))
                {
                    TravelState.Seen.Add(key);
                    TravelState.LastEvent = key;
                    return TravelCatalogue.PlaceEvent(key);
                }
            }
            return null;
        }

        // A day goes by at home through the game's own time skip, so shops trade, wages are paid and rent falls due.
        private IEnumerator NextDayRoutine()
        {
            _skipping = true;
            var goal = new Timestamp(TimeHelper.CurrentDay + 1, 9, 0f);
            EnergyHelper.RemoveEnergySpender("move");
            EnergyHelper.SetCurrentEnergyRegen(EnergyRegen.Bench);
            EnergyHelper.energyRegenMultiplier = EnergyHelper.DefaultEnergyRegenMultiplier * 0.5f;
            yield return _director.Skip(goal, "reallife_travel_tm_trip", true);
            HotelStay.EndSleepEffects();
            _skipping = false;
            if (!TravelState.Away)
            {
                yield break;   // cut short meanwhile
            }
            // Three meals a day are part of any holiday.
            GameInstance save = SaveGameManager.Current;
            if (save.Hunger < 70f)
            {
                TravelEffects.Feed(70f - save.Hunger);
            }
            TravelState.AwayStep++;
            ShowStep();
        }

        // The window for this trip's screens, made ready; null if the game's list cannot be copied.
        private TravelList Screen()
        {
            TravelList list = _director.Window.Take(this);
            if (list != null && _onPick == null)
            {
                _onPick = _director.Pressed;
                _onClosed = TripClosed;
            }
            return list;
        }

        // The player cannot close a trip under way (the list is locked); closing a cancelled trip's notice ends it.
        private void TripClosed()
        {
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (trip list): closed" + (TravelState.Away ? " while the trip is under way (the watchdog ends the trip if nothing reopens it)." : "."));
            }
            if (_cancelLine != null)
            {
                _cancelLine = null;
                _director.Seclude(false);
            }
        }

        private void ShowCancel(Trip trip)
        {
            TravelList list = Screen();
            if (list == null)
            {
                TravelText.Toast(NotificationType.Warning, _cancelLine, 12f);
                _cancelLine = null;
                _director.Seclude(false);
                return;
            }
            list.Begin(TravelText.T(trip.NameKey), string.Empty);
            list.AddNote(TravelText.T("reallife_travel_cancel_title"), _cancelLine, list.HeightFor(_cancelLine.Length, false));
            list.Add(Close, TravelText.T("reallife_travel_btn_close"), null, ButtonDo, true);
            list.Open(_onPick, _onClosed);
            list.Locked = false;
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (trip list): " + trip.Id + ": cancelled at the gate, 1 choice(s): PASS.");
            }
        }

        private void ShowStep()
        {
            Trip trip = TravelCatalogue.TripById(TravelState.TicketId);
            if (trip == null)
            {
                Abort();
                _director.Seclude(false);
                _director.Window.Close();
                return;
            }
            TravelList list = Screen();
            if (list == null)
            {
                Plain();
                return;
            }
            float began = Time.realtimeSinceStartup;
            bool opening = !list.IsOpen;
            int step = TravelState.AwayStep;
            int last = 1 + 2 * trip.Days;
            string with = TravelState.CompanionName;
            string side = TravelText.T(TravelState.TicketFirst ? "reallife_travel_trip_side_first" : "reallife_travel_trip_side_economy")
                          + (with != null ? "  " + TravelText.T("reallife_travel_trip_side_with", "name", with) : string.Empty)
                          + (TravelState.TicketInsured ? "  " + TravelText.T("reallife_travel_trip_side_insured") : string.Empty);
            list.Begin(TravelText.T(trip.NameKey), side);
            string heading;
            string body;
            int entries;
            if (step <= 0)
            {
                heading = TravelText.T("reallife_travel_trip_departure");
                body = TravelText.T(trip.DepartKey)
                       + " " + TravelText.T(TravelState.Suitcase ? "reallife_travel_trip_packed" : "reallife_travel_trip_unpacked")
                       + (with != null ? " " + TravelText.T("reallife_travel_trip_with", "name", with) : string.Empty);
                list.AddNote(heading, body, list.HeightFor(body.Length, false));
                list.Add(SetOff, TravelText.T("reallife_travel_btn_set_off"), TravelText.T("reallife_travel_list_set_off", "days", TravelText.Number(trip.Days)), ButtonDo, true);
                entries = 1;
            }
            else if (step >= last)
            {
                heading = TravelText.T("reallife_travel_trip_home");
                body = Summary(trip);
                list.AddNote(heading, body, list.HeightFor(body.Length, false));
                list.Add(Home, TravelText.T("reallife_travel_btn_home"), TravelText.T("reallife_travel_list_home"), ButtonDo, true);
                entries = 1;
            }
            else
            {
                int day = (step - 1) / 2;
                heading = TravelText.T("reallife_travel_trip_day", "day", TravelText.Number(day + 1), "days", TravelText.Number(trip.Days));
                if (step % 2 == 1)
                {
                    body = TravelText.T(trip.DayKeys[Mathf.Min(day, trip.DayKeys.Length - 1)]);
                    list.AddNote(heading, body, list.HeightFor(body.Length, false));
                    bool afford = SaveGameManager.Current.Money >= trip.Splurge;
                    list.Add(Relax, TravelText.T("reallife_travel_btn_relax"), TravelText.T("reallife_travel_list_relax"), ButtonDo, true);
                    list.Add(Explore, TravelText.T("reallife_travel_btn_explore"), TravelText.T("reallife_travel_list_explore"), ButtonDo, true);
                    list.Add(Splurge, TravelText.T("reallife_travel_btn_splurge", "amount", TravelText.Money(trip.Splurge)),
                        TravelText.T(afford ? "reallife_travel_list_splurge" : "reallife_travel_list_no_splurge"), ButtonDo, afford);
                    entries = 3;
                }
                else
                {
                    string choice = TravelText.T(TravelState.AwayChoice == (int)TripChoice.Relax ? "reallife_travel_trip_did_relax"
                        : TravelState.AwayChoice == (int)TripChoice.Explore ? "reallife_travel_trip_did_explore" : "reallife_travel_trip_did_splurge");
                    string what = TravelState.AwayEvent != null ? TravelText.T(TravelState.AwayEvent, "name", with ?? string.Empty) : string.Empty;
                    body = choice + " " + what;
                    list.AddNote(heading, body, list.HeightFor(body.Length, false));
                    bool final = day + 1 >= trip.Days;
                    list.Add(NextDay, TravelText.T(final ? "reallife_travel_btn_head_home" : "reallife_travel_btn_next_day"),
                        TravelText.T(final ? "reallife_travel_list_head_home" : "reallife_travel_list_next_day"), ButtonDo, true);
                    entries = 1;
                }
            }
            list.Open(_onPick, _onClosed);
            list.Locked = true;   // a trip is left by its own entries, or ended by the watchdog
            if (Meta.Dev.Verbose)
            {
                _director.Log("Travel check (trip list): " + (opening ? "the game's list is open: " : "") + trip.Id + ", step " + step + " of " + last + ", " + entries
                              + " choice(s), " + list.Made + " entries made so far; " + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
        }

        // No window to show: the trip is played through taking it easy each day, so that a ticket can never trap anyone.
        private void Plain()
        {
            if (_auto)
            {
                return;
            }
            _auto = true;
            _director.Log("Travel check (trip list): the game's list could not be made; the trip is played through taking it easy each day: FAIL.");
            _director.Run(AutoRoutine());
        }

        private IEnumerator AutoRoutine()
        {
            while (TravelState.Away && _auto)
            {
                Trip trip = TravelCatalogue.TripById(TravelState.TicketId);
                if (trip == null)
                {
                    break;
                }
                int last = 1 + 2 * trip.Days;
                int step = TravelState.AwayStep;
                if (step <= 0)
                {
                    TravelState.AwayStep = 1;
                }
                else if (step >= last)
                {
                    ComeHome(trip);
                    break;
                }
                else if (step % 2 == 1)
                {
                    Choose(trip, TripChoice.Relax);
                }
                else
                {
                    yield return NextDayRoutine();
                }
                yield return null;
            }
            if (_auto && TravelState.Away)
            {
                Abort();
                _director.Seclude(false);
            }
            _auto = false;
        }

        // What the days away came to, read from the game's own daily accounts.
        private string Summary(Trip trip)
        {
            var text = new StringBuilder(600);
            int today = TimeHelper.CurrentDay;
            int away = Mathf.Max(today - TravelState.AwayStartDay, 0);
            text.Append(TravelText.T(trip.ReturnKey)).Append(' ');
            text.Append(TravelText.T("reallife_travel_summary_days", "days", TravelText.Number(away),
                "spent", TravelText.Money(TravelState.TicketPaid + TravelState.AwaySpent)));
            try
            {
                float during = 0f, before = 0f;
                int daysDuring = 0, daysBefore = 0;
                bool any = false;
                List<FinancialSummary> summaries = SaveGameManager.Current.financialSummaries;
                for (int i = 0; summaries != null && i < summaries.Count; i++)
                {
                    FinancialSummary summary = summaries[i];
                    if (summary == null)
                    {
                        continue;
                    }
                    if (summary.dayNumber >= TravelState.AwayStartDay && summary.dayNumber < today)
                    {
                        during += summary.totalBusinessProfit;
                        daysDuring++;
                        any |= summary.businessIncomeStatements != null && summary.businessIncomeStatements.Count > 0;
                    }
                    else if (summary.dayNumber >= TravelState.AwayStartDay - 7 && summary.dayNumber < TravelState.AwayStartDay)
                    {
                        before += summary.totalBusinessProfit;
                        daysBefore++;
                    }
                }
                if (any && daysDuring > 0)
                {
                    text.Append(' ').Append(TravelText.T(daysBefore > 0 ? "reallife_travel_summary_business" : "reallife_travel_summary_business_new",
                        "total", TravelText.Money(during), "daily", TravelText.Money(during / daysDuring),
                        "before", TravelText.Money(daysBefore > 0 ? before / daysBefore : 0f)));
                }
                int money = Mathf.RoundToInt(SaveGameManager.Current.Money);
                text.Append(' ').Append(TravelText.T("reallife_travel_summary_balance", "amount", TravelText.Money(money - TravelState.AwayMoneyAtStart)));
            }
            catch (Exception e)
            {
                _director.Fault(e);
            }
            Missed(text, TravelBridges.Social.MissedWhileAway(TravelState.AwayStartDay, today));
            Missed(text, TravelBridges.Justice.MissedWhileAway(TravelState.AwayStartDay, today));
            if (TravelState.AwaySouvenirs > 0)
            {
                text.Append(' ').Append(TravelText.T("reallife_travel_summary_souvenirs", "count", TravelText.Number(TravelState.AwaySouvenirs)));
            }
            text.Append(' ').Append(TravelText.T(trip.JetLag ? (TravelState.TicketFirst ? "reallife_travel_summary_jetlag_first" : "reallife_travel_summary_jetlag")
                : trip.Kind == TripKind.Business ? "reallife_travel_summary_fair" : "reallife_travel_summary_rested"));
            return text.ToString();
        }

        private static void Missed(StringBuilder text, List<string> keys)
        {
            for (int i = 0; keys != null && i < keys.Count && i < 3; i++)
            {
                text.Append(' ').Append(TravelText.T(keys[i]));
            }
        }

        private void ComeHome(Trip trip)
        {
            int away = Mathf.Max(TimeHelper.CurrentDay - TravelState.AwayStartDay, 1);
            int hours = Mathf.Min(72 + 12 * TravelState.AwayRelaxed + (TravelState.TicketFirst ? 24 : 0), 144);
            if (trip.Kind == TripKind.Business)
            {
                ModState.AddModifier(TravelEffects.TradeFair);
                hours = 0;   // a trade fair is work
            }
            TakeBreak(trip, away, hours, trip.JetLag);
            TravelState.Souvenirs += TravelState.AwaySouvenirs + (TravelState.Suitcase && TravelState.AwaySouvenirs > 0 ? 1 : 0);   // a proper case brings one more home whole
            GiveSouvenirs();
            TravelState.AddPast(trip.Id, TravelState.AwayStartDay, away);
            _director.Log("Travel: back from " + trip.Id + " after " + away + " day(s); " + TravelState.AwaySpent + " spent there.");
            TravelState.ClearAway();
            TravelState.ClearTicket();
            _director.Seclude(false);
            _director.Window.Close();
            TravelText.Toast(NotificationType.Success, TravelText.T("reallife_travel_welcome_back"), 8f);
        }

        // What a holiday leaves behind: rested for days, the grind forgotten, perhaps jet lag, the companion closer.
        private static void TakeBreak(Trip trip, int days, int restedHours, bool jetLag)
        {
            if (restedHours > 0)
            {
                TravelBridges.Home.Holiday(days);
                ModState.AddModifier(TravelEffects.Rested, restedHours);
                TravelState.Streak = 0;
                TravelState.Warned = false;
                TravelState.BurntOut = false;
                ModState.RemoveModifier(TravelEffects.Burnout);
            }
            if (jetLag)
            {
                ModState.AddModifier(TravelEffects.JetLag, TravelState.TicketFirst ? 18 : 36);
                TravelEffects.Energy(TravelState.TicketFirst ? -8f : -18f);
            }
            else if (restedHours > 0)
            {
                TravelEffects.Energy(20f);
            }
            if (TravelState.CompanionId != null)
            {
                TravelBridges.Social.TripTogether(TravelState.CompanionId, days);
                ModState.AddModifier(TravelEffects.Together);
            }
        }

        // Souvenirs go to the social feature's friends, or else one each to the Friend and Mom contacts.
        private static void GiveSouvenirs()
        {
            if (TravelState.Souvenirs <= 0)
            {
                return;
            }
            if (TravelBridges.HasSocial)
            {
                TravelState.Souvenirs = Mathf.Max(TravelState.Souvenirs - Mathf.Max(TravelBridges.Social.Souvenirs(TravelState.Souvenirs), 0), 0);
                return;
            }
            if (PhoneContacts.Friend != null)
            {
                TravelState.Souvenirs--;
                PhoneContacts.Friend.SendMessage(new TextMessage("reallife_travel_text_friend_souvenir", null, false, true), notify: true, sendNotificationInstantly: true);
                ModState.AddModifier(ModState.Social);
            }
            if (TravelState.Souvenirs > 0 && PhoneContacts.Mom != null)
            {
                TravelState.Souvenirs--;
                PhoneContacts.Mom.SendMessage(new TextMessage("reallife_travel_text_mom_souvenir", null, false, true), notify: true, sendNotificationInstantly: true);
                ModState.AddModifier(ModState.Family);
            }
        }
    }
}
