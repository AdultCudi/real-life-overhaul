using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using UI.Load;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// Parties and company events: the player hosts the people they know at home or at a place in the city, holds events
    /// for the staff of their businesses, and is now and then asked to a party of somebody else's; the rival tycoons'
    /// outlets hold staff events by the same rule. The rules and every step are game-free (PartyDesk, StaffDesk), run here
    /// against the game (PartyGame). Under the master option Living population and its own switch: off, nothing is booked,
    /// begun, charged or missed, and what is planned is kept. The cost: no Update; a one-second timer on the real clock
    /// that compares the game's minute, and when it has moved a pass over the handful of records that are open; the day's
    /// round once a day; while a party's time has come, one look a second at whether the host is there.
    /// </summary>
    public sealed partial class PartyDirector : MonoBehaviour
    {
        public const string AppId = "RealLifeParties";
        private const float AskGapSeconds = 20f;   // real seconds before the host is asked again after "Not yet"
        private static readonly int HotTick = HotSpots.Slot("Parties: PartyDirector.Tick");

        public static IModLogger Logger;
        public static PartyDirector Instance { get; private set; }

        private readonly PartyGame _world = new PartyGame();
        private PartyFile _file;
        private GameInstance _save;
        private Action _onSave, _onBegin, _onNotYet;
        private bool _broken;
        private int _minute = -1;
        private int _asking;          // the party (positive) or invitation (negative) the question box is about; 0 none
        private float _askAfter;
        private PartyApp _app;

        private static GameInstance Save => SaveGameManager.Current;
        private static bool Verbose => Meta.Dev.Verbose;

        /// <summary>True while parties run: the master option and their own on, and no fault has stopped them.</summary>
        public static bool On => Settings.LivingPopulation && Settings.LivingParties && Instance != null && !Instance._broken;

        /// <summary>The save's file, for the page; null before the save that is open now has been read (the previous save's file is never handed out in that first second).</summary>
        public PartyFile File => Mine ? _file : null;

        // The file belongs to the save that is open now: a second save loaded in one run is read by the next look, and until then the old file is nobody's to book, pay or draw from.
        private bool Mine => _file != null && ReferenceEquals(Save, _save);

        /// <summary>The game as the rules ask it, for the page's drafts.</summary>
        public IPartyWorld World => _world;

        /// <summary>Moves whenever what a page shows may have changed.</summary>
        public int Revision { get; private set; }

        private void Awake()
        {
            Instance = this;
            _onSave = OnSave;
            _onBegin = BeginAsked;
            _onNotYet = NotYet;
            _world.Changed = Touch;
            _world.Open = OpenDiary;
            GameUi.GameSprites.Want(PartyApp.AppGlyphs);   // asked for before the one look behind the loading screen (GameSprites.Prewarm)
            StartCoroutine(Guard.Routine(Tick()));
        }

        private void OnDestroy()
        {
            SaveHook.Drop(_onSave);
            try
            {
                _app?.Remove();
                _app = null;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private void Touch()
        {
            Revision++;
        }

        private IEnumerator Tick()
        {
            var second = new WaitForSecondsRealtime(1f);
            while (true)
            {
                yield return second;
                if (_broken)
                {
                    continue;
                }
                try
                {
                    Slow();
                }
                catch (Exception e) when (Guard.StandsDown(e, "parties and company events"))
                {
                    _broken = true;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    Logger?.Warn("Parties: parties and company events are off for this session after the error above; what they kept is saved as it was.");
                    _broken = true;
                }
            }
        }

        private void Slow()
        {
            using var hotSpot = HotSpots.Time(HotTick);
            GameInstance save = Save;
            if (save == null || LoadScene.isLoading || GameManager.isCitySceneBeingUnloaded || Meta.SaveTools.Dormant || !ModState.Loaded)
            {
                return;
            }
            if (!ReferenceEquals(save, _save))
            {
                _save = save;
                Load(save);
            }
            SaveHook.Keep(_onSave);   // the line is written while off too, so switching off keeps what was planned
            AppLook();
            int now = _world.Now;
            if (now != _minute)
            {
                _minute = now;
                bool changed = PartyDesk.Tick(_file, _world);   // with the option off this only notes since when
                changed |= PartyDesk.Day(_file, _world, now % 1440 / 60);
                if (changed)
                {
                    Touch();
                }
            }
            if (On)
            {
                AskLook(now);
            }
        }

        private void Load(GameInstance save)
        {
            if (save.modData == null)
            {
                save.modData = new Dictionary<string, string>();
            }
            PartyFile f = PartyStore.Open(save.modData, NewSeed, out int version, out bool fresh);
            if (version < 0)
            {
                SaveLines.Unreadable(save.modData, PartyStore.Key, "it could not be read");   // put back as it was read at every save
                Logger?.Warn("Parties: the save's line of parties and events could not be read (" + PartyStore.Key + "); nothing is planned, and the line is kept in the save as it is.");
            }
            _file = f;   // a new file has a seed of its own for every draw, written with the save's first line
            _minute = -1;
            _asking = 0;
            Touch();
            if (!fresh || Verbose)
            {
                Logger?.Info("Parties: " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + ": " + _file.Parties.Count + " parties, "
                             + _file.Events.Count + " company events, " + _file.Invites.Count + " invitations, " + _file.Lifts.Count + " lifts.");
            }
        }

        // A seed for a new save, drawn once: it goes into the save's first line, from where every later load reads it.
        private static int NewSeed() => new System.Random().Next(1, int.MaxValue);

        // Inside the game's save event: the line, from the very first save, so the seed is kept even while nothing is booked.
        private void OnSave()
        {
            GameInstance save = Save;
            if (save == null || save.modData == null || _file == null || !ReferenceEquals(save, _save) || SaveLines.IsKept(save.modData, PartyStore.Key))
            {
                return;
            }
            PartyStore.Put(save.modData, _file);
        }

        /// <summary>The feature's data is reset (Meta/SaveTools): what the save kept is forgotten. Nothing is given back: a reset is the player's own.</summary>
        public void Forget()
        {
            _file = new PartyFile { Seed = NewSeed() };
            _minute = -1;
            _asking = 0;
            Save?.modData?.Remove(PartyStore.Key);
            Touch();
        }

        /// <summary>The app off the phone (the options' clean-up); it comes back with the next look.</summary>
        public void Release()
        {
            try
            {
                _app?.Remove();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _app = null;
        }

        // The app is on the phone while the option is on, and off it otherwise: one comparison a second once it stands.
        private void AppLook()
        {
            if (!On)
            {
                if (_app != null)
                {
                    Release();
                }
                return;
            }
            if (_app == null)
            {
                _app = new PartyApp(this);
            }
            _app.Install();
        }

        // ------------------------------------------------------------------------------------------------- being there

        // While a party's or an accepted invitation's time has come and the host is at the place, the game's own question box asks once; "Not yet" asks again a little later.
        private void AskLook(int now)
        {
            if (_asking != 0)
            {
                if (HudConfirm.isOpen)
                {
                    return;
                }
                NotYet();   // the box went without an answer: asked again a little later
            }
            if (Time.unscaledTime < _askAfter || !Mine || HudConfirm.isOpen)
            {
                return;
            }
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.timeMachine == null || uis.timeMachine.isRunning || PlayerActivity.PlayerActivityUI.IsPanelOpen)
            {
                return;
            }
            for (int i = 0; i < _file.Parties.Count; i++)
            {
                Party p = _file.Parties[i];
                if (p.State == PartyState.Planned && now >= p.Start - PartyCatalogue.EarlyMinutes && now <= p.Start + PartyCatalogue.MissedMinutes
                    && PartyDesk.CanBegin(p, _world) == PartyRefusal.None)
                {
                    _asking = p.Id;
                    GameConfirm.Show(PartyText.T("reallife_party_ask_begin_title"),
                        PartyText.Fill("reallife_party_ask_begin", "place", p.Name, "count", PartyText.Whole(p.Coming), "until", _world.When(p.End / 1440, p.End % 1440 / 60)),
                        Guard.Wrap(_onBegin), Guard.Wrap(_onNotYet), "reallife_party_ask_begin_yes", "reallife_party_ask_not_yet", false, false);
                    return;
                }
            }
            for (int i = 0; i < _file.Invites.Count; i++)
            {
                Invite invite = _file.Invites[i];
                if (invite.State == InviteState.Accepted && invite.Began < 0 && now >= invite.Start - PartyCatalogue.EarlyMinutes
                    && now <= invite.Start + PartyCatalogue.MissedMinutes && PartyDesk.CanJoin(invite, _world) == PartyRefusal.None)
                {
                    _asking = -invite.Id;
                    SocialPerson host = _world.Person(invite.Person);
                    GameConfirm.Show(PartyText.T("reallife_party_ask_join_title"),
                        PartyText.Fill("reallife_party_ask_join", "name", host != null ? host.Name : string.Empty, "place", invite.Name,
                            "until", _world.When(invite.End / 1440, invite.End % 1440 / 60)),
                        Guard.Wrap(_onBegin), Guard.Wrap(_onNotYet), "reallife_party_ask_join_yes", "reallife_party_ask_not_yet", false, false);
                    return;
                }
            }
        }

        private void NotYet()
        {
            _asking = 0;
            _askAfter = Time.unscaledTime + AskGapSeconds;
        }

        // The host said yes at the place: the party begins by its rules, and the time passes to its end with the game's own skip.
        private void BeginAsked()
        {
            int asked = _asking;
            _asking = 0;
            _askAfter = Time.unscaledTime + AskGapSeconds;
            if (!Mine || asked == 0)
            {
                return;
            }
            if (asked > 0)
            {
                PartyRefusal why = PartyDesk.Begin(_file, _world, asked);
                Party p = _file.Find(asked);
                if (why != PartyRefusal.None)
                {
                    Refused(why);
                }
                else if (p != null && p.State == PartyState.Running)
                {
                    PartyGame.Skip(p.End, "reallife_party_skip_caption");
                }
                Check("begin", "party " + asked + " begun at the place: " + why + (p != null ? ", " + p.Came + " of " + p.Coming + " came, state " + p.State : string.Empty),
                    why != PartyRefusal.None || p != null && (p.State == PartyState.Running || p.State == PartyState.Empty));
            }
            else
            {
                PartyRefusal why = PartyDesk.Join(_file, _world, -asked);
                Invite invite = _file.Invitation(-asked);
                if (why != PartyRefusal.None && why != PartyRefusal.VenueShut)
                {
                    Refused(why);
                }
                else if (invite != null && invite.Began >= 0)
                {
                    PartyGame.Skip(invite.End, "reallife_party_skip_caption");
                }
            }
            Touch();
        }

        // ------------------------------------------------------------------------------------------------- what the pages and other features ask

        /// <summary>Books a party the player hosts; a refusal is told in a notice and returned.</summary>
        public PartyRefusal Host(PartyAsk ask)
        {
            if (!Mine)
            {
                return Refused(PartyRefusal.NoSave);
            }
            PartyRefusal why = PartyDesk.Host(_file, _world, ask, out Party booked);
            Check("host", "asked for " + ask.Place + " on day " + ask.Day + " at " + ask.Hour + ":00 with " + (ask.Guests != null ? ask.Guests.Count : 0) + " asked: " + why
                          + (booked != null ? ", booked as party " + booked.Id + " for " + booked.Paid + " with " + booked.Coming + " coming" : string.Empty),
                (why == PartyRefusal.None) == (booked != null));
            Touch();
            return why == PartyRefusal.None ? why : Refused(why);
        }

        public PartyRefusal CancelParty(int id)
        {
            PartyRefusal why = Mine ? PartyDesk.Cancel(_file, _world, id) : PartyRefusal.NoSave;
            Touch();
            return why == PartyRefusal.None ? why : Refused(why);
        }

        public PartyRefusal Reply(int id, bool yes)
        {
            PartyRefusal why = Mine ? PartyDesk.Reply(_file, _world, id, yes) : PartyRefusal.NoSave;
            Touch();
            return why == PartyRefusal.None ? why : Refused(why);
        }

        /// <summary>Books a company event at one of the player's businesses; a refusal is told in a notice and returned.</summary>
        public PartyRefusal Book(StaffAsk ask)
        {
            if (!Mine)
            {
                return Refused(PartyRefusal.NoSave);
            }
            PartyRefusal why = StaffDesk.Book(_file, _world, ask, out StaffEvent booked);
            Check("event", "asked for " + ask.Kind + " at " + ask.Place + " on day " + ask.Day + " at " + ask.Hour + ":00: " + why
                           + (booked != null ? ", booked as event " + booked.Id + " for " + booked.Paid + " at the business, " + booked.Heads + " staff" : string.Empty),
                (why == PartyRefusal.None) == (booked != null));
            Touch();
            return why == PartyRefusal.None ? why : Refused(why);
        }

        public PartyRefusal CancelEvent(int id)
        {
            PartyRefusal why = Mine ? StaffDesk.Cancel(_file, _world, id) : PartyRefusal.NoSave;
            Touch();
            return why == PartyRefusal.None ? why : Refused(why);
        }

        public PartyRefusal Schedule(string place, int every, StaffEventKind kind)
        {
            PartyRefusal why = Mine ? StaffDesk.Schedule(_file, _world, place, every, kind) : PartyRefusal.NoSave;
            Touch();
            return why == PartyRefusal.None ? why : Refused(why);
        }

        /// <summary>The player lost touch with whoever had this place in the social life's cast (Wiring: SocialLinks.PartiesForget).</summary>
        public void ForgetPerson(int person)
        {
            if (Mine && PartyDesk.Forget(_file, person))
            {
                Touch();
            }
        }

        /// <summary>
        /// The rival tycoons' outlets' day, asked by the rivals' own day (Wiring: RivalBridges.StaffEvents): what they paid for
        /// staff events today, already off their fortunes. Nothing while parties are off.
        /// </summary>
        public long RivalDay(Rivals.RivalFile rivals, int day, int level)
        {
            if (!On || !Mine || rivals == null)
            {
                return 0L;
            }
            long paid = StaffDesk.RivalDay(_file, rivals, day, level);
            if (paid > 0)
            {
                Touch();
                Logger?.Info("Parties: the tycoons' outlets paid " + paid + " for staff events on day " + day + ".");
            }
            return paid;
        }

        /// <summary>The parties the player hosted on or after a day that are still kept: the door for another feature (PartyHosted).</summary>
        public int Hosted(int fromDay, List<HostedParty> into)
        {
            return PartyHosted.Since(File, fromDay, into);
        }

        /// <summary>Opens the app on a tab: the People page's Host a party, the Company app's staff page, a notice.</summary>
        public static bool Open(int tab)
        {
            return GameUi.PhoneApp.OpenById(AppId + GameUi.PhoneApp.WantMark + tab.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private void OpenDiary()
        {
            Open(RealLifeOverhaul.Ui.PartyPages.TabDiary);
        }

        private static PartyRefusal Refused(PartyRefusal why)
        {
            Notifications.Show(NotificationType.Warning, PartyText.RefusalKey(why), null, 8f, null, null, true, false);
            return why;
        }

        // One line an event with Developer diagnostics on: what was asked against what happened, then PASS or FAIL.
        private static void Check(string what, string line, bool pass)
        {
            if (Verbose)
            {
                Logger?.Info("Parties check (" + what + "): " + line + ": " + (pass ? "PASS" : "FAIL"));
            }
        }
    }
}
