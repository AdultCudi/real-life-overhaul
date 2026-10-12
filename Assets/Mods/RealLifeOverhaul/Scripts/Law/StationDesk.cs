using System;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UI;
using UI.MiniMenu;
using UI.Notification;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Law
{
    /// <summary>
    /// The front desk of a police station: what the desk sergeant can do for somebody who walks
    /// in (or, at a precinct that cannot be walked into, for somebody at its door). It is the game's
    /// own list (<see cref="TravelList"/>, a copy of the window the game lists its subway stations in):
    /// an entry for each thing the desk offers today, with the cost or the state under its name and its
    /// button off, with the reason, when it cannot be had.
    ///   report a crime     the same as a 911 call about what is going on nearby (the crime
    ///                      scene's own report, a traffic accident, a fire), with a little extra
    ///                      standing with the police for coming in; and never a "wasted call"
    ///   pay fines          whatever is still owed (Core/ModState's pending fine), now, before it
    ///                      is taken by itself
    ///   the record         a second page: both reputations and the record, an entry for each line
    ///   the pound          a car the police are holding after an arrest is brought to the kerb
    ///                      outside for the impound fee
    ///   licences           handed to the Licences feature, if that is there (Bridges.Licensing)
    ///   a police report    after a break-in or a crash: a reference number, and whatever the
    ///                      paperwork bridge makes of it (Bridges.PoliceReport)
    ///   hand yourself in   for somebody who got away after hurting someone: half the time
    ///   visiting hours     a word about how the station works
    /// What the desk answers is a notification. While the list is up the player stands still; the game's
    /// cancel key leaves it. If the game's window cannot be copied the panel at the desk has the four
    /// commonest things on its own keys (<see cref="Quick"/>). Nothing here runs unless LawDirector calls it.
    /// </summary>
    internal sealed class StationDesk
    {
        public static int PaperFee => Prices.At(15);
        public const int TurnInDays = 7;             // how long after getting away somebody can still hand themselves in

        private const NavigationBlocker Blocker = NavigationBlocker.DoctorOperation;   // (as Custody: any blocker stops the player; nothing else on a street or in an empty unit sets this one)
        private const string ButtonOpen = "reallife_law_list_open";
        private const string ButtonDo = "reallife_law_list_do";

        internal enum Service { None, Report, Fines, Record, Impound, Licence, Paper, TurnIn, Hours, Back }

        private readonly IModLogger _logger;
        private readonly Precincts _precincts;
        private readonly DeskList _window = new DeskList("RealLifeOverhaul Law", 1100f, 150f);
        private readonly StringBuilder _text = new StringBuilder(256);
        private readonly Action<int> _onPick;
        private readonly Action _onClosed;
        private Precinct _precinct;
        private bool _inside;
        private bool _held;
        private bool _record;                // the second page (the record) is showing
        private PlayerController _player;
        private string _bringOnExit;         // a car asked for at a desk indoors: brought when the player steps outside
        private Precinct _bringTo;
        private string _closing;

        public StationDesk(Transform root, Precincts precincts, IModLogger logger)
        {
            _precincts = precincts;
            _logger = logger;
            _onPick = Picked;
            _onClosed = Closed;
            TravelList.Logger = logger;
        }

        /// <summary>True while the desk's list is on screen.</summary>
        public bool IsOpen => _window.IsOpen;

        /// <summary>True when the game's list could not be copied: the desk's panel then offers the commonest things on its own keys.</summary>
        public bool Plain => _window.Failed;

        // ---------------------------------------------------------------- opening and closing

        /// <summary>Opens the desk of a precinct. <paramref name="inside"/>: the player is standing in it (otherwise at its door, or anywhere for the test button).</summary>
        public void Open(Precinct precinct, bool inside)
        {
            if (precinct == null || IsOpen)
            {
                return;
            }
            float began = Time.realtimeSinceStartup;
            TravelList list = _window.Take(this);
            if (list == null)
            {
                _logger?.Info("Law check (desk list): the game's list could not be made; the desk's panel has the commonest things on its own keys: FAIL.");
                return;
            }
            _precinct = precinct;
            _inside = inside;
            _record = false;
            _closing = null;
            _player = PlayerHelper.PlayerController;
            if (_player != null && !_held)
            {
                _held = true;
                _player.ResetNavigation();
                _player.SetNavigationBlocker(Blocker);
            }
            int entries = Fill(list);
            list.Open(_onPick, _onClosed);
            _logger?.Info("Law: front desk opened at " + precinct.Name + (inside ? " (inside)." : " (at the door)."));
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Law check (desk list): the game's list is open: " + entries + " entries at " + precinct.Name + ", " + list.Made + " entries made so far; "
                              + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1") + " ms: PASS.");
            }
        }

        /// <summary>Takes the desk off the screen and lets the player walk. Safe at any time.</summary>
        public void Close()
        {
            if (_window.IsOpen)
            {
                _window.Close();   // (its closing lets the player walk)
            }
            else
            {
                Release();
            }
        }

        public void Dispose()
        {
            Close();
            _window.Destroy(e => _logger?.Error(e));
        }

        // The list was closed (a pick that leaves, the Cancel button, the game's cancel key, or the director).
        private void Closed()
        {
            if (Meta.Dev.Verbose)
            {
                _logger?.Info("Law check (desk list): closed (" + (_closing ?? "cancelled by the player") + ").");
            }
            _closing = null;
            Release();
        }

        private void Release()
        {
            if (_held)
            {
                _held = false;
                try
                {
                    if (_player != null)
                    {
                        _player.UnsetNavigationBlocker(Blocker);
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
            _precinct = null;
            _record = false;
        }

        /// <summary>Four times a second while the list is up: it ends if the save is gone, the player is held, or one of the game's own screens has come up over it.</summary>
        public void Watch()
        {
            if (!IsOpen)
            {
                return;
            }
            if (SaveGameManager.Current == null || _precinct == null || Custody.Active)
            {
                _closing = "the player was taken away or the save went";
                Close();
            }
            else if (FullMenu.IsOpen || CityMap.IsOpen || MiniMenu.IsOpen || HudConfirm.isOpen)
            {
                _closing = "one of the game's own screens came up";
                Close();
            }
        }

        /// <summary>The player has stepped out into the street: a car asked for at the desk is brought round now.</summary>
        public void OnStreet()
        {
            if (_bringOnExit == null)
            {
                return;
            }
            string id = _bringOnExit;
            Precinct to = _bringTo;
            _bringOnExit = null;
            _bringTo = null;
            if (id == LawState.ImpoundVehicleId)
            {
                Bring(id, to);
            }
        }

        /// <summary>One thing done at once, with no list (the panel's own keys, when the game's list could not be made).</summary>
        public void Quick(Precinct precinct, bool inside, Service service)
        {
            if (precinct == null || IsOpen)
            {
                return;
            }
            _precinct = precinct;
            _inside = inside;
            try
            {
                Do(service);
            }
            finally
            {
                if (!IsOpen)
                {
                    _precinct = null;
                }
            }
        }

        // ---------------------------------------------------------------- what is offered

        private string Greeting()
        {
            string key = Wanted ? "reallife_law_desk_greeting_wanted"
                : Settings.LawReputation && Reputation.Police >= 75 ? "reallife_law_desk_greeting_friend"
                : Settings.LawReputation && Reputation.Police < 35 ? "reallife_law_desk_greeting_known"
                : "reallife_law_desk_greeting";
            return key.Tr().Replace("{precinct}", _precinct.Name);
        }

        private static bool Wanted =>
            LawState.Loaded && LawState.WantedCharges != 0 && SaveGameManager.Current != null && TimeHelper.CurrentDay - LawState.WantedDay <= TurnInDays;

        // The entries, as things stand now. A service that cannot be had is shown with its button off and the reason in its detail line;
        // one whose bridge is not there is not shown at all. Returns how many.
        private int Fill(TravelList list)
        {
            if (_record)
            {
                return FillRecord(list);
            }
            list.Begin("reallife_law_desk_title".Tr(), Greeting() + "\n<size=80%>" + _precinct.Name + "</size>");
            int count = 0;
            list.Add((int)Service.Report, "reallife_law_desk_report".Tr(), "reallife_law_list_report".Tr(), ButtonDo, true); count++;
            int owed = ModState.PendingFine;
            list.Add((int)Service.Fines, "reallife_law_list_fines".Tr(), owed > 0 ? Custody.Fill("reallife_law_list_fines_owed", "amount", ((float)owed).ToCurrencyFormat()) : "reallife_law_list_fines_none".Tr(), ButtonDo, owed > 0); count++;
            list.Add((int)Service.Record, "reallife_law_desk_record".Tr(), "reallife_law_list_record".Tr(), ButtonOpen, true); count++;
            bool held = LawState.ImpoundVehicleId != null;
            list.Add((int)Service.Impound, "reallife_law_list_impound".Tr(), held ? Custody.Fill("reallife_law_list_impound_fee", "amount", ((float)LawBook.ImpoundFee).ToCurrencyFormat()) : "reallife_law_list_impound_none".Tr(), ButtonDo, held); count++;
            if (Bridges.Licensing != null)
            {
                list.Add((int)Service.Licence, (string.IsNullOrEmpty(Bridges.LicensingLabelKey) ? "reallife_law_desk_licence" : Bridges.LicensingLabelKey).Tr(), "reallife_law_list_licence".Tr(), ButtonOpen, true); count++;
            }
            bool incident = Bridges.HasIncident;
            list.Add((int)Service.Paper, "reallife_law_list_paper".Tr(),
                incident ? Custody.Fill("reallife_law_list_paper_fee", "amount", ((float)PaperFee).ToCurrencyFormat()).Replace("{what}", IncidentName()) : "reallife_law_list_paper_none".Tr(), ButtonDo, incident); count++;
            if (Wanted)
            {
                bool ready = Custody.Ready;
                list.Add((int)Service.TurnIn, "reallife_law_list_turnin".Tr(), "reallife_law_list_turnin_detail".Tr() + (ready ? string.Empty : " " + "reallife_law_list_turnin_not_now".Tr()), ButtonDo, ready); count++;
            }
            list.Add((int)Service.Hours, "reallife_law_desk_hours".Tr(), "reallife_law_list_hours".Tr(), ButtonDo, true); count++;
            return count;
        }

        // The record: both standings in the info lines, then an entry for each line of it, newest first, and a way back.
        private int FillRecord(TravelList list)
        {
            list.Begin("reallife_law_desk_record".Tr(), Summary());
            list.Add((int)Service.Back, "reallife_law_desk_back".Tr(), "reallife_law_list_back".Tr(), ButtonOpen, true);
            int count = Record.Count;
            if (count == 0)
            {
                list.AddNote("reallife_law_desk_record".Tr(), "reallife_records_clean".GetLocalization(), 0f);
                return 1;
            }
            for (int index = count - 1; index >= 0; index--)
            {
                RecordEntry line = Record.Entry(index);
                string outcome;
                switch (line.Outcome)
                {
                    case Outcome.Held: outcome = "reallife_records_outcome_held".GetLocalization(); break;
                    case Outcome.Served: outcome = "reallife_records_outcome_served".GetLocalization().Replace("{hours}", line.Hours.ToString()); break;
                    case Outcome.Bailed: outcome = "reallife_records_outcome_bailed".GetLocalization().Replace("{hours}", line.Hours.ToString()); break;
                    case Outcome.Escaped: outcome = "reallife_records_outcome_escaped".GetLocalization(); break;
                    default: outcome = "reallife_records_outcome_cited".GetLocalization(); break;
                }
                string what = "reallife_law_list_entry_detail".Tr().Replace("{charges}", Custody.ChargeNames(line.Charges, ", ")).Replace("{outcome}", outcome)
                    .Replace("{amount}", ((float)line.Fines).ToCurrencyFormat());
                list.AddNote("reallife_law_list_entry".Tr().Replace("{number}", (count - index).ToString()).Replace("{count}", count.ToString()).Replace("{day}", line.Day.ToString()),
                    what, list.HeightFor(what.Length, false));
            }
            return count + 1;
        }

        private static string IncidentName()
        {
            switch (LawState.IncidentKind)
            {
                case Bridges.BreakIn: return "reallife_law_paper_breakin".Tr();
                case Bridges.CarBreakIn: return "reallife_law_paper_car".Tr();
                case Bridges.Accident: return "reallife_law_paper_accident".Tr();
                default: return "reallife_law_paper_other".Tr();
            }
        }

        // ---------------------------------------------------------------- what is done

        private void Picked(int id)
        {
            Do((Service)id);
        }

        private void Do(Service service)
        {
            try
            {
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Law check (choice): the desk at " + (_precinct != null ? _precinct.Name : "?") + ": " + service + ".");
                }
                switch (service)
                {
                    case Service.Report: Report(); break;
                    case Service.Fines: PayFines(); break;
                    case Service.Record: _record = true; break;
                    case Service.Back: _record = false; break;
                    case Service.Impound: Impound(); break;
                    case Service.Licence: Licence(); return;
                    case Service.Paper: Paper(); break;
                    case Service.TurnIn: TurnIn(); return;
                    case Service.Hours: Say("reallife_law_desk_hours_line".Tr()); break;
                    default: return;
                }
                if (IsOpen)
                {
                    TravelList list = _window.Take(this);
                    if (list != null)
                    {
                        Fill(list);
                        list.Open(_onPick, _onClosed);   // (the entries that are no longer used are switched off)
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
                _closing = "an error";
                Close();
            }
        }

        // What the desk answers: the game's own notification.
        private static void Say(string line)
        {
            if (!string.IsNullOrEmpty(line))
            {
                Notifications.Show(NotificationType.Info, "reallife_law_desk_toast", new Dictionary<string, string>(1) { { "text", line } }, 10f);
            }
        }

        // What a 911 call would do about whatever is going on near the player, said across a counter; with nothing going
        // on it is a word and no more (nobody is fined for asking in person).
        private void Report()
        {
            if (Events.EventDirector.FireBurning)
            {
                Say((Events.EventDirector.Report911() ? "reallife_911_dispatched_fire" : "reallife_911_noted").Tr());
                return;
            }
            ReportKind kind = CrimeDirector.Reportable();
            bool taken;
            if (kind == ReportKind.Accident)
            {
                taken = TrafficAccidents.Instance != null && TrafficAccidents.Instance.ReportAccident();
                if (taken)
                {
                    ModState.AddModifier(ModState.Helped);
                }
            }
            else
            {
                taken = kind != ReportKind.None && CrimeDirector.ReportScene();
            }
            if (taken)
            {
                Reputation.AtTheDesk(2);   // for coming in with it
                Say("reallife_law_desk_report_taken".Tr());
                _logger?.Info("Law: a report (" + kind + ") was taken at the front desk of " + _precinct.Name + ".");
            }
            else
            {
                Say((CrimeDirector.IsActive || PoliceAttention.IsBusy || kind != ReportKind.None ? "reallife_law_desk_report_known" : "reallife_law_desk_report_nothing").Tr());
            }
        }

        private void PayFines()
        {
            int owed = ModState.PendingFine;
            if (owed <= 0)
            {
                Say("reallife_law_desk_fines_none".Tr());
                return;
            }
            if (!Crime.Pay(owed, "reallife_transaction_fine"))
            {
                Say("reallife_law_desk_fines_short".Tr());
                return;
            }
            ModState.ClearPendingFine();
            Reputation.AtTheDesk(1);
            Say(Custody.Fill("reallife_law_desk_fines_paid", "amount", ((float)owed).ToCurrencyFormat()));
            _logger?.Info($"Law: outstanding fines of {owed} paid at the front desk.");
        }

        private static string Summary()
        {
            int since = Record.DaysSinceLastOffence;
            string last = since < 0 ? "reallife_records_since_never".GetLocalization()
                : since == 0 ? "reallife_records_since_today".GetLocalization()
                : "reallife_records_since_days".GetLocalization().Replace("{days}", since.ToString());
            return "reallife_records_summary".GetLocalization()
                .Replace("{police}", Reputation.PoliceWord.GetLocalization()).Replace("{policeScore}", Reputation.Police.ToString())
                .Replace("{people}", Reputation.PublicWord.GetLocalization()).Replace("{peopleScore}", Reputation.Public.ToString())
                .Replace("{count}", Record.Count.ToString()).Replace("{since}", last)
                .Replace("{watch}", (Reputation.NoWarnings ? "reallife_records_watch_high" : Reputation.ScrutinyLevel > 0 ? "reallife_records_watch_some" : "reallife_records_watch_none").GetLocalization());
        }

        // A car at the pound: brought to the kerb outside. Asked for indoors, it is there when the player steps out (the fee
        // is charged when it is brought, with the message that says where it is).
        private void Impound()
        {
            string id = LawState.ImpoundVehicleId;
            if (id == null)
            {
                Say("reallife_law_desk_impound_none".Tr());
                return;
            }
            if (Crime.Cash < LawBook.ImpoundFee)
            {
                Say(Custody.Fill("reallife_law_desk_impound_short", "amount", ((float)LawBook.ImpoundFee).ToCurrencyFormat()));
                return;
            }
            if (_inside || BuildingManager.IsInsideBuilding)
            {
                _bringOnExit = id;
                _bringTo = _precinct;
                Say("reallife_law_desk_impound_outside".Tr());
                return;
            }
            Bring(id, _precinct);
            Say("reallife_law_desk_impound_done".Tr());
        }

        private void Bring(string id, Precinct to)
        {
            LawState.ImpoundVehicleId = null;
            try
            {
                Vector3 near = to != null && Precincts.Front(to, out Vector3 front, out _) ? front : to != null ? to.Door : PlayerHelper.GetCityPosition();
                foreach (VehicleController vehicle in VehicleHelper.AllPlayerVehicles)
                {
                    if (vehicle != null && vehicle.vehicleInstance != null && vehicle.vehicleInstance.id == id)
                    {
                        if (JobSpots.Flat(vehicle.transform.position, near) < 70f)
                        {
                            Notifications.Show(NotificationType.Info, "reallife_law_desk_impound_here", null, 8f);   // it is already here: nothing to pay
                            return;
                        }
                        VehicleTow.BringNear(vehicle.vehicleInstance, near, LawBook.ImpoundFee, "reallife_law_car_impound", "reallife_law_car_impound_shop",
                            "reallife_law_car_impound_left", "reallife_transaction_impound");
                        _logger?.Info("Law: the car at the pound was asked for at the front desk and brought round.");
                        return;
                    }
                }
                Notifications.Show(NotificationType.Info, "reallife_law_desk_impound_gone", null, 8f);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Licence()
        {
            Action open = Bridges.Licensing;
            Close();
            try
            {
                open?.Invoke();
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private void Paper()
        {
            if (!Bridges.HasIncident)
            {
                Say("reallife_law_desk_paper_none".Tr());
                return;
            }
            if (!Crime.Pay(PaperFee, "reallife_transaction_police_report"))
            {
                Say(Custody.Fill("reallife_law_desk_paper_short", "amount", ((float)PaperFee).ToCurrencyFormat()));
                return;
            }
            string kind = LawState.IncidentKind;
            LawState.ReportsGiven++;
            _text.Length = 0;
            _text.Append("PR-").Append((_precinct.Index + 1).ToString("00")).Append('-').Append(LawState.IncidentDay.ToString("0000")).Append('-').Append(LawState.ReportsGiven.ToString("000"));
            string reference = _text.ToString();
            LawState.IncidentKind = null;
            LawState.IncidentDay = -1;
            try
            {
                Bridges.PoliceReport?.Invoke(kind, reference);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            Say(Custody.Fill("reallife_law_desk_paper_done", "reference", reference));
            _logger?.Info("Law: police report " + reference + " (" + kind + ") handed over at the front desk.");
        }

        private void TurnIn()
        {
            Precinct precinct = _precinct;
            Close();
            if (!Custody.TurnIn(LawState.WantedCharges, precinct))
            {
                Notifications.Show(NotificationType.Warning, "reallife_law_desk_turnin_refused", null, 8f);
            }
        }
    }
}
