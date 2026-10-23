using System;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game leaves the mod's buildings alone. Every address the mod holds or reserves is held here
    /// (<see cref="HeldLedger"/>, counted by claim): the book of special buildings tells it of each change at once
    /// (<see cref="BookChanged"/>, called by <see cref="SpecialBuildings"/>), so every present and future claim made
    /// through the book is covered without its feature knowing, and a feature outside the book holds and lets go itself
    /// (<see cref="Hold"/>, <see cref="Release"/>: the empty shops made offices). While held, a unit standing empty sits
    /// out of the game's night of letting, and an address with no rival landlord or business is on the game's ignore list
    /// too (<see cref="HeldUnitsRules"/>; the game's side is <see cref="HeldUnitsGame"/>). Nothing is saved: a claim let
    /// go, the city unloading or the save prepared for removing the mod puts the game's lists back at once.
    /// Going Public: every machine holds the same addresses (the features' picks are the same everywhere); the game's
    /// night runs on the host only, and a guest puts nothing on the ignore list; nothing is sent.
    /// COST: no Update; a one-second real-time timer looks at the held addresses (a registration each); the game's night
    /// two passes over them; a claim's change one pass.
    /// </summary>
    public sealed class HeldUnits : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "HeldUnits", typeof(HeldUnits));   // its handlers and timer

        private const string NewHourEvent = "ba:gameevent_newhour";   // after the hour's rivals' step, which may have opened a business

        public static IModLogger Logger;

        private static readonly HeldLedger Ledger = new HeldLedger();
        private static readonly List<HeldClaim> BookClaims = new List<HeldClaim>(48);
        private static readonly List<HeldSpot> Spots = new List<HeldSpot>(48);
        private static readonly List<string> Offers = new List<string>(4);
        private static readonly int[] ByKind = new int[(int)HeldKind.Count];

        private static HeldUnits _live;          // the city's instance; null between cities
        private static bool _bookDirty = true;   // the book changed while no city was there to hold for
        private static bool _checkDue;           // a change since the last self-check
        private static bool _failed;
        private static string _lastCheck;

        private Action _onHour, _onDay, _hourSeen, _daySeen;
        private Action<string> _onGameEvent, _gameEventSeen;
        private bool _night;                     // between the night's first hour event and its day event

        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>True while a claim is on an address (reserved, put in the book, or held by a feature outside it).</summary>
        public static bool Holds(string street, int number) => Ledger.Holds(street, number);

        // The game's side may be written: a save in play, not loading, not prepared for removal, nothing failed.
        private static bool Ready => _live != null && !_failed && !FrameGuard.Off && SaveGameManager.Current != null && !LoadScene.isLoading && !Meta.SaveTools.Dormant;

        private void Awake()
        {
            _live = this;
            _bookDirty = true;
            _checkDue = true;
            _lastCheck = null;
            _onHour = Guard.Wrap(FrameGuard, OnHour);
            _onDay = Guard.Wrap(FrameGuard, OnDay);
            _onGameEvent = Guard.Wrap<string>(FrameGuard, OnGameEvent);
            StartCoroutine(Guard.Routine(Tick()));
            Apply();   // the claims made while the city loaded, taken in now; the game's side follows once it is ready
        }

        private void OnDestroy()
        {
            GlobalEvents.onNewHour -= _onHour;
            GlobalEvents.onNewDay -= _onDay;
            GameEvent.onGameEventTriggered -= _onGameEvent;
            if (_live == this)
            {
                _live = null;
                Unload("the city is unloading");
            }
        }

        /// <summary>The book of special buildings changed: what it holds is held now, and what it let go is let go now.</summary>
        internal static void BookChanged()
        {
            _bookDirty = true;
            if (_live != null)
            {
                Apply();
            }
        }

        /// <summary>A claim of a feature outside the book (one holder, one address); moved if the holder held another.</summary>
        public static void Hold(string holder, string street, int number, HeldKind kind)
        {
            if (Ledger.Hold(holder, street, number, kind))
            {
                Apply();
            }
        }

        /// <summary>A claim let go; the address stays held while another claim is on it.</summary>
        public static void Release(string holder)
        {
            if (Ledger.Release(holder))
            {
                Apply();
            }
        }

        private static readonly int HotApply = HotSpots.Slot("Core: HeldUnits.Apply");
        private static readonly Func<HeldSpot, bool> MayListNow = spot => HeldUnitsGame.MayList(spot, SaveGameManager.Current);
        private static readonly Func<HeldSpot, bool> ListedNow = HeldUnitsGame.Listed;
        private static readonly Func<HeldSpot, bool, bool> IgnoreNow = HeldUnitsGame.Ignore;

        // The book's claims taken in, then every held address on or off the game's ignore list as the rule says now.
        // Never throws into the feature whose claim changed.
        private static void Apply()
        {
            using var hotSpot = HotSpots.Time(HotApply);
            try
            {
                if (_bookDirty && _live != null)
                {
                    _bookDirty = false;
                    SpecialBuildings.Holding(BookClaims);
                    int changed = Ledger.Sync(HeldUnitsRules.BookPrefix, BookClaims) + Ledger.Sync(HeldUnitsRules.ReservePrefix, BookClaims);
                    _checkDue |= changed > 0;
                }
                _checkDue |= Ledger.Apply(Ready, MayListNow, ListedNow, IgnoreNow) > 0;
            }
            catch (Exception e) when (Guard.StandsDown(e, "the mod's buildings left alone by the game's letting"))
            {
                StandDown();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Logger?.Warn("Held buildings: after the error above the game's lists are as they were without the mod, for the rest of this session; "
                             + "the mod's places may move when the game lets their units.");
                StandDown();
            }
        }

        // A fault: the game's lists put back and nothing held on the game's side again this session.
        private static void StandDown()
        {
            _failed = true;
            try
            {
                HeldUnitsGame.Back();
                Ledger.Apply(false, MayListNow, ListedNow, IgnoreNow);   // every entry of the mod's off; the claims are kept, written nowhere
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // The city is going (or the mod is being removed from the save): every list as the game had it, every claim let go.
        private static void Unload(string why)
        {
            try
            {
                int lists = HeldUnitsGame.Back();
                int claims = Ledger.ReleaseAll();
                Apply();   // with no city every entry of the mod's comes off, and the ledger is emptied
                if (claims > 0 || lists > 0)
                {
                    Logger?.Info("Held buildings: " + why + "; " + claims + " claim(s) let go and the game's lists put back as the game had them.");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _bookDirty = true;   // the reservations outlive the city: held again in the next
            _failed = false;
        }

        private IEnumerator Tick()
        {
            var wait = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return wait;
                if (FrameGuard.Off)
                {
                    if (!_failed)
                    {
                        StandDown();
                    }
                    continue;
                }
                if (SaveGameManager.Current == null || LoadScene.isLoading)
                {
                    continue;
                }
                EventHold.Keep(ref GlobalEvents.onNewHour, _onHour, ref _hourSeen);
                EventHold.Keep(ref GlobalEvents.onNewDay, _onDay, ref _daySeen);
                EventHold.Keep(ref GameEvent.onGameEventTriggered, _onGameEvent, ref _gameEventSeen);
                if (HeldUnitsGame.IsOut)
                {
                    // A later frame than the night: its day event never came (a step of the game's night threw).
                    int lists = HeldUnitsGame.Back();
                    _night = false;
                    Logger?.Warn("Held buildings: the game's night stopped before its day event; the " + lists + " district list(s) it read are put back as they were.");
                }
                Apply();   // a rival's landlord or business, a sale or another player's change since the last look
                if (_checkDue && Verbose && Ready)
                {
                    _checkDue = false;
                    Check();
                }
            }
        }

        private static readonly int HotNight = HotSpots.Slot("Core: HeldUnits, the night's first hour");

        // Every hour, before its rivals' step: the ignore list as the rule says. At the night's first hour, before the
        // game's real estate and letting: the held empty units out of the lists the night reads.
        private void OnHour()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || !Ready)
            {
                return;
            }
            Apply();
            if (save.Hour != 0 || !Ready)
            {
                return;
            }
            using var hotSpot = HotSpots.Time(HotNight);
            Ledger.Spots(Spots);
            int taken = HeldUnitsGame.Out(Spots);
            _night = true;
            if (Verbose)
            {
                Logger?.Info("Held buildings: the game's night of day " + save.Day + ": " + taken + " of the " + Ledger.Count
                             + " address(es) the mod holds stand empty and sit out of its letting until its day event.");
            }
        }

        // After the game's night of letting (CompetitionHelper.RunDaily is before the day event): the lists as they were.
        private void OnDay()
        {
            HeldUnitsGame.Back();
            _night = false;
            Apply();
        }

        private void OnGameEvent(string name)
        {
            if (name == NewHourEvent && !_night)
            {
                Apply();   // the hour's rivals' step may have opened a business in a held unit: off the ignore list before any look of theirs
            }
        }

        // With Developer diagnostics on, after a load and after each change (at most once a second): what is held, what of it is
        // on the game's ignore list, and the game's own choice of empty units run as at night.
        private static void Check()
        {
            Ledger.Spots(Spots);
            Ledger.Tally(ByKind);
            int held = 0, listed = 0;
            for (int i = 0; i < Spots.Count; i++)
            {
                held += Spots[i].Holders > 0 ? 1 : 0;
                listed += Spots[i].Ours ? 1 : 0;
            }
            int units = HeldUnitsGame.Offered(Ledger, Spots, Offers, out int districts, out string why);
            var line = new StringBuilder(512);
            line.Append("Held buildings check: ").Append(held).Append(" addresses held by the mod (").Append(HeldUnitsRules.Counts(ByKind)).Append("); ")
                .Append(listed).Append(" of them on the game's ignore list (the rest have a rival landlord or business, or are for sale, ")
                .Append("and stay off it so that nothing of theirs is written into the save); ");
            if (units < 0)
            {
                line.Append("the game's empty-unit pass could not be run: ").Append(why);
            }
            else if (Offers.Count == 0)
            {
                line.Append("the game's empty-unit pass (").Append(units).Append(" empty unit(s) in ").Append(districts).Append(" district(s)) would offer none of them: PASS");
            }
            else
            {
                line.Append("the game's empty-unit pass would offer ").Append(string.Join(", ", Offers)).Append(": FAIL, a held unit stayed in the lists its night reads");
            }
            string text = line.ToString();
            if (text != _lastCheck)
            {
                _lastCheck = text;
                Logger?.Info(text);
            }
        }

        /// <summary>The test button: the game's own choice of empty units to let, in every district now, against the held units.</summary>
        public static void Test()
        {
            if (_live == null || SaveGameManager.Current == null || LoadScene.isLoading)
            {
                Notifications.Show(NotificationType.Warning, "reallife_held_test_none", null, 8f, trackOnSaveGame: false);
                return;
            }
            Ledger.Spots(Spots);
            int units = HeldUnitsGame.Offered(Ledger, Spots, Offers, out int districts, out string why);
            if (units < 0)
            {
                Logger?.Info("Held buildings test: " + why + ".");
                Notifications.Show(NotificationType.Warning, "reallife_held_test_missing", null, 10f, trackOnSaveGame: false);
                return;
            }
            Ledger.Tally(ByKind);
            Logger?.Info("Held buildings test: " + Ledger.Count + " address(es) held (" + HeldUnitsRules.Counts(ByKind) + "); the game's choice of empty units offered " + units + " in "
                         + districts + " district(s), " + (Offers.Count == 0 ? "none of them held: PASS" : "held ones among them: " + string.Join(", ", Offers) + ": FAIL"));
            var data = new Dictionary<string, string>(3)
            {
                { "count", Ledger.Count.ToString() }, { "units", units.ToString() }, { "districts", districts.ToString() }
            };
            if (Offers.Count == 0)
            {
                Notifications.Show(NotificationType.Success, "reallife_held_test_pass", data, 12f, trackOnSaveGame: false);
                return;
            }
            data["address"] = Offers[0];
            Notifications.Show(NotificationType.Warning, "reallife_held_test_fail", data, 14f, trackOnSaveGame: false);
        }
    }
}
