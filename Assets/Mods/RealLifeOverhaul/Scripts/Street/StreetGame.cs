using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.InputSystem;
using BigAmbitions.DayNightCycle;
using Dialogs;
using Entities;
using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets;
using Streets.Pedestrians;
using UI.Guiders;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The one place street talk touches the game: the game's own crowd (CityManager's pedestrian spawner keeps the walkers
    /// that are out; nothing is searched), who of them stands in front of the player, what the game shows of a person (sex,
    /// age, a phone at the ear), the game's dialogue panel over a person (DialogUI.ShowDialog, as its counters open one), its
    /// text box (TextInputUI), its destination (as BizManBusiness.SetDestination does), its notifications, the city's
    /// businesses by kind and by name, and the day's time and weather.
    /// COST: <see cref="Facing"/> is a pass over the walkers and the standing and sitting people that are out (a few dozen
    /// distance tests; a figure's kind told apart once) five times a second while the player is on foot out of doors;
    /// everything else runs on a press or a click. Nothing allocated per pass.
    /// </summary>
    public static class StreetGame
    {
        public static IModLogger Logger;

        private const string DialogTypeName = "reallife_street_calldialogtype";
        private static readonly int TalkingPhone = Animator.StringToHash("TalkingPhone");   // the game's own looping animations (CharacterAnimations: a PermanentAnimationType by its name)
        private static readonly int TextingPhone = Animator.StringToHash("TextingPhone");
        private static readonly List<Transform> Remote = new List<Transform>(4);
        private static float _remoteAt = -100f;
        private const float RemoteEvery = 5f;      // real seconds between two looks at the other players' figures, in a Going Public session
        private const float RemoteClear = 0.6f;    // metres: a walker this near another player's figure is never offered (it may be theirs)

        // ---------------------------------------------------------------- the crowd

        private const float NearAny = 12f;   // metres: the nearest person within this is named in the offer's check line, whatever their angle
        private static readonly Dictionary<int, StreetFigure> Kinds = new Dictionary<int, StreetFigure>(128);   // a standing or sitting figure's kind, by its instance (told apart once)
        private const int KindsMost = 512;    // figures told apart at most before the list starts again (the game's pools hold far fewer)

        /// <summary>For the offer's check line: the walkers and the standing and sitting people looked at in the last pass.</summary>
        public static int LastWalkers, LastFigures;

        /// <summary>For the offer's check line: the nearest person of the last pass within 12 m (any angle): found, metres squared, the cosine off the facing, usable, kind.</summary>
        public static bool NearFound, NearUsable;
        public static float NearSqr, NearCos;
        public static StreetFigure NearKind;

        /// <summary>
        /// The person in front of the player within reach (StreetOffer.Score: the nearest roughly in front), of the game's own
        /// walkers and of its people standing about and sitting (StreetPeople); the one already offered keeps the offer out to
        /// the wider reach and angle. Never one another feature holds (the ragdolls, a horn's turn, a borrowed walker: its own
        /// behaviour switched off), never one hidden, never one busy at something (StreetFigure.Busy), never another player's
        /// figure in a Going Public session. <paramref name="walker"/> is their walker (null for somebody standing or sitting).
        /// </summary>
        public static BaseHuman Facing(Vector3 player, Vector3 forward, BaseHuman current, float now, out Pedestrian walker, out StreetFigure kind)
        {
            walker = null;
            kind = StreetFigure.Busy;
            NearFound = NearUsable = false;
            NearSqr = NearAny * NearAny;
            if (GoingPublic.InSession && now - _remoteAt > RemoteEvery)
            {
                _remoteAt = now;
                Remote.Clear();
                GoingPublic.RemotePlayers(Remote);
            }
            BaseHuman best = null;
            Pedestrian bestWalker = null;
            StreetFigure bestKind = StreetFigure.Busy;
            float bestScore = float.MaxValue;
            List<Pedestrian> walkers = PerfGame.Pedestrians;
            LastWalkers = walkers != null ? walkers.Count : 0;
            for (int i = 0; walkers != null && i < walkers.Count; i++)
            {
                Pedestrian p = walkers[i];
                ThirdPersonCharacter body = p != null ? p.tpc : null;
                if (body == null)
                {
                    continue;
                }
                Vector3 offset = body.transform.position - player;
                bool usable = Usable(p) && !Theirs(body.transform);
                Note(offset, forward, usable, StreetFigure.Walker);
                bool isCurrent = ReferenceEquals(body, current);
                float score = usable ? StreetOffer.Score(offset.x, offset.y, offset.z, forward.x, forward.z, isCurrent) : -1f;
                if (score < 0f)
                {
                    continue;
                }
                if (isCurrent)
                {
                    walker = p;
                    kind = StreetFigure.Walker;
                    return body;   // (who was offered keeps the offer while near)
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = body;
                    bestWalker = p;
                    bestKind = StreetFigure.Walker;
                }
            }
            List<BaseHuman> standing = StreetPeople.Standing(Logger);
            LastFigures = standing.Count;
            for (int i = 0; i < standing.Count; i++)
            {
                BaseHuman h = standing[i];
                if (h == null)
                {
                    continue;
                }
                StreetFigure k = FigureOf(h);
                if (!StreetOffer.Offered(k))
                {
                    continue;
                }
                Vector3 offset = h.transform.position - player;
                bool usable = Usable(h) && !Theirs(h.transform);
                Note(offset, forward, usable, k);
                bool isCurrent = ReferenceEquals(h, current);
                float score = usable ? StreetOffer.Score(offset.x, offset.y, offset.z, forward.x, forward.z, isCurrent) : -1f;
                if (score < 0f)
                {
                    continue;
                }
                if (isCurrent)
                {
                    kind = k;
                    return h;
                }
                if (score < bestScore)
                {
                    bestScore = score;
                    best = h;
                    bestWalker = null;
                    bestKind = k;
                }
            }
            walker = bestWalker;
            kind = bestKind;
            return best;
        }

        // The nearest person of the pass, for the check line: a few multiplications, nothing kept but numbers.
        private static void Note(Vector3 offset, Vector3 forward, bool usable, StreetFigure kind)
        {
            if (offset.y > StreetRules.Height || offset.y < -StreetRules.Height)
            {
                return;
            }
            float sqr = offset.x * offset.x + offset.z * offset.z;
            if (sqr < NearSqr)
            {
                NearFound = true;
                NearSqr = sqr;
                NearCos = StreetOffer.Cos(offset.x, offset.z, forward.x, forward.z);
                NearUsable = usable;
                NearKind = kind;
            }
        }

        /// <summary>True while the one offered is still out, usable and within <see cref="StreetRules.GraceMetres"/> (their offer may outlast a step out of the front).</summary>
        public static bool Near(BaseHuman body, Pedestrian walker, Vector3 player)
        {
            if (body == null || !(walker != null ? Usable(walker) : Usable(body)))
            {
                return false;
            }
            Vector3 offset = body.transform.position - player;
            return offset.y <= StreetRules.Height && offset.y >= -StreetRules.Height
                   && offset.x * offset.x + offset.z * offset.z <= StreetRules.GraceMetres * StreetRules.GraceMetres;
        }

        /// <summary>True for a walker the game runs as it should, out and seen: not held by the ragdolls or borrowed by another feature.</summary>
        public static bool Usable(Pedestrian p)
        {
            return p.isActiveAndEnabled && p.tpc != null && p.tpc.visible && p.gameObject.activeInHierarchy && !RagdollSystem.Holds(p.tpc);
        }

        /// <summary>True for a standing or sitting figure that is out and drawn, and not held by the ragdolls (a casualty) or turned by a horn.</summary>
        public static bool Usable(BaseHuman h)
        {
            return h.isActiveAndEnabled && h.gameObject.activeInHierarchy && h.animator != null && h.appearanceSetter != null && !RagdollSystem.Holds(h)
                   && !HornReactions.Reacts(h);
        }

        /// <summary>
        /// The kind of one of the game's standing or sitting figures: a hangout spot's figure (StationaryAiBehavior) stands or
        /// sits, the plain figure of the bench pool sits; a performer, somebody homeless (the game's own box over their bucket)
        /// and the pools of the swimming pool, the golf course and the exercise machines are busy. Told apart once a figure.
        /// </summary>
        public static StreetFigure FigureOf(BaseHuman h)
        {
            int id = h.GetInstanceID();
            if (!Kinds.TryGetValue(id, out StreetFigure kind))
            {
                if (Kinds.Count >= KindsMost)
                {
                    Kinds.Clear();
                }
                kind = KindOf(h);
                Kinds[id] = kind;
            }
            return kind != StreetFigure.Busy && h.isSittingOn != null ? StreetFigure.Seated : kind;
        }

        private static StreetFigure KindOf(BaseHuman h)
        {
            if (h is ThirdPersonCharacter || h.GetComponent<Homeless>() != null || h.GetComponent<StreetPerformer>() != null)
            {
                return StreetFigure.Busy;
            }
            if (h.GetComponent<StationaryAiBehavior>() != null)
            {
                return StreetFigure.Standing;
            }
            Transform pool = h.transform.parent;
            return pool != null && pool.name.IndexOf("Bench", StringComparison.OrdinalIgnoreCase) >= 0 ? StreetFigure.Seated : StreetFigure.Busy;
        }

        private static bool Theirs(Transform body)
        {
            for (int i = 0; i < Remote.Count; i++)
            {
                Transform t = Remote[i];
                if (t != null && (body.IsChildOf(t) || (t.position - body.position).sqrMagnitude < RemoteClear * RemoteClear))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The game's own figure for a person: its instance stands for one look, so one person, for the session.</summary>
        public static int Key(BaseHuman b) => b.GetInstanceID();

        public static bool Female(BaseHuman p)
        {
            return p.appearanceSetter != null && p.appearanceSetter.data != null && p.appearanceSetter.data.gender == Gender.Female;
        }

        /// <summary>Their age in years, as the game made their look (AppearanceAgeHelper: days of the game's year).</summary>
        public static int Age(BaseHuman p)
        {
            int days = p.appearanceSetter != null && p.appearanceSetter.data != null ? p.appearanceSetter.data.ageInDays : 0;
            return days > 0 ? days / Math.Max(1, SocialPlaces.DaysPerYear) : 35;
        }

        /// <summary>Their look as the game made it (its appearance data), as a kept look's text (People/PersonLook); null if it has none.</summary>
        public static string Look(BaseHuman p)
        {
            return p != null && p.appearanceSetter != null ? PersonLookGame.Of(p.appearanceSetter.data) : null;
        }

        /// <summary>0 nothing, 1 looking at their phone, 2 on a call: the game's own animation on the figure.</summary>
        public static int Phone(BaseHuman p)
        {
            Animator animator = p != null ? p.animator : null;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return 0;
            }
            return animator.GetBool(TalkingPhone) ? 2 : animator.GetBool(TextingPhone) ? 1 : 0;
        }

        /// <summary>The save's own number for its people (the save's character, hashed the same way on every run).</summary>
        public static int SaveSeed()
        {
            GameInstance save = SaveGameManager.Current;
            string id = save != null ? save.characterId : null;
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; id != null && i < id.Length; i++)
                {
                    h = (h ^ id[i]) * 16777619u;
                }
                return (int)(h & 0x7fffffff);
            }
        }

        // ---------------------------------------------------------------- the player

        public static PlayerController Player => PlayerHelper.PlayerController;

        /// <summary>True out of doors, on foot, with nothing of the game's or the mod's holding the player.</summary>
        public static bool OnFootOutside() => NotOnFoot() == null;

        /// <summary>True out of doors and not in a vehicle (the offer's check line is written only then).</summary>
        public static bool OutOfDoorsOnFoot => !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle;

        /// <summary>Why the player is not free on foot out of doors (for the offer's check line), or null when they are.</summary>
        public static string NotOnFoot()
        {
            if (BuildingManager.IsInsideBuilding)
            {
                return "indoors";
            }
            if (PlayerHelper.IsUsingVehicle)
            {
                return "in a vehicle";
            }
            if (!LifeEvents.IsSafeForEvent())
            {
                return "the game is busy (a pause, the time machine, a question, the activity panel, the hospital, the subway or the casino boat)";
            }
            if (Wiring.Scenes.PlayerHeld)
            {
                return "the mod holds the player (custody, the court, a licence test, the options page or a window over the world)";
            }
            return null;
        }

        /// <summary>The player turns on the spot to face a point (the game's own turn).</summary>
        public static void TurnPlayer(MonoBehaviour host, Vector3 towards)
        {
            PlayerController player = Player;
            if (player == null || player.Character == null || host == null || !host.isActiveAndEnabled)
            {
                return;
            }
            Vector3 flat = towards - player.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.04f)
            {
                host.StartCoroutine(Guard.Routine(player.Character.RotateTowards(towards, 0.25f)));
            }
        }

        // ---------------------------------------------------------------- the game's panels

        /// <summary>Opens the game's own dialogue panel over a person with the dialogue the maker makes (as a counter's clerk opens one).</summary>
        public static void OpenDialog(ThirdPersonCharacter with, Func<Dialog> maker, Action closed)
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(maker));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(closed), with);
        }

        /// <summary>True while the game's dialogue panel is open (anybody's).</summary>
        public static bool DialogOpen => SocialPhone.DialogOpen;

        /// <summary>
        /// True in the frame the game's cancel key (Esc) was pressed: the dialogue panel's grey button and the key share one
        /// action (DialogUI.OnCancel, from CancelButtonHandler on PlayerAction.Cancel), and this tells the key from a click.
        /// </summary>
        public static bool CancelKey => PlayerAction.Cancel.Pressed();

        /// <summary>True when the game has its text box to ask for a line in.</summary>
        public static bool CanType => TextBox.CanType;

        /// <summary>The game's own text box over everything (its own canvas), for the player's words. False if the game has none.</summary>
        public static bool AskWords(Action<string> said, Action closed)
        {
            return TextBox.Ask("reallife_street_say_placeholder", "reallife_street_btn_say", Guard.Wrap(said), Guard.Wrap(closed), string.Empty, false, StreetSay.Most);
        }

        /// <summary>A notification of the game's.</summary>
        public static void Notice(NotificationType type, string key, Dictionary<string, string> data, float seconds = 10f)
        {
            Notifications.Show(type, key, data, seconds);
        }

        // ---------------------------------------------------------------- places

        /// <summary>The nearest business of a kind within <see cref="StreetRules.FindMetres"/>: a walk over the city's buildings, on a question.</summary>
        public static bool Nearest(StreetPlace kind, Vector3 from, out Venue venue, out float metres)
        {
            return Find(kind, null, from, out venue, out metres);
        }

        /// <summary>The nearest business whose name is the one asked for (StreetSay.Names).</summary>
        public static bool Named(string asked, Vector3 from, out Venue venue, out float metres)
        {
            return Find(StreetPlace.Count, asked, from, out venue, out metres);
        }

        private static bool Find(StreetPlace kind, string asked, Vector3 from, out Venue venue, out float metres)
        {
            venue = default;
            metres = 0f;
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            CityBuildingController best = null;
            float bestSqr = StreetRules.FindMetres * StreetRules.FindMetres;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (!Business(b, out BuildingRegistration reg))
                {
                    continue;
                }
                if (asked != null ? !StreetSay.Names(asked, reg.BusinessName) : !StreetRules.IsPlace(reg.businessTypeName, kind))
                {
                    continue;
                }
                Vector3 offset = b.transform.position - from;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < bestSqr)
                {
                    bestSqr = sqr;
                    best = b;
                }
            }
            if (best == null || !SocialPlaces.Make(best, out venue))
            {
                return false;
            }
            metres = Mathf.Sqrt(bestSqr);
            return true;
        }

        /// <summary>The names of the two nearest businesses of the city (not the player's own), for "what's good around here".</summary>
        public static int Around(Vector3 from, string[] into)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = city != null ? city.cityBuildingControllers : null;
            float first = float.MaxValue, second = float.MaxValue;
            into[0] = into[1] = null;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                CityBuildingController b = all[i];
                if (!Business(b, out BuildingRegistration reg) || GoingPublic.Mine(reg) || string.IsNullOrEmpty(reg.BusinessName))
                {
                    continue;
                }
                Vector3 offset = b.transform.position - from;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < first)
                {
                    second = first;
                    into[1] = into[0];
                    first = sqr;
                    into[0] = reg.BusinessName;
                }
                else if (sqr < second && reg.BusinessName != into[0])
                {
                    second = sqr;
                    into[1] = reg.BusinessName;
                }
            }
            return into[1] != null ? 2 : into[0] != null ? 1 : 0;
        }

        // A business that trades: established, not an empty unit, not behind a fence, of a part of the game the player has.
        private static bool Business(CityBuildingController b, out BuildingRegistration reg)
        {
            reg = b != null ? b.buildingRegistration : null;
            try
            {
                return reg != null && b.building != null && reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.businessTypeName)
                       && reg.businessTypeName != "ba:businesstype_empty" && !GameMembers.Fenced(b.building) && b.building.requiredDLC.DlcIsOwned();
            }
            catch (Exception)
            {
                return false;   // (a building the game has no data for is no business)
            }
        }

        /// <summary>The way to a place from the player's facing, by StreetRules.Way.</summary>
        public static int Way(Vector3 player, Vector3 forward, Vector3 place)
        {
            Vector3 to = place - player;
            to.y = 0f;
            forward.y = 0f;
            return StreetRules.Way(Vector3.SignedAngle(forward, to, Vector3.up));
        }

        /// <summary>The place becomes the player's destination, exactly as the game's own "Set destination" does it (BizManBusiness.SetDestination).</summary>
        public static bool Destination(in Venue venue)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || InstanceBehavior<GuidersManager>.Instance == null || string.IsNullOrEmpty(venue.Street))
            {
                return false;
            }
            var address = new Address(venue.Street, venue.Number);
            save.customDestination = address;
            GuidersManager.SetGuiderTarget(address, DirectionGuiderType.Destination);
            Notifications.Show(NotificationType.Success, "notification_destination_set", new Dictionary<string, string> { { "address", address.ToFormattedString() } });
            return true;
        }

        /// <summary>The player's own businesses that trade, by name and by address ("street~number"), into the lists (cleared first).</summary>
        public static void Shops(List<string> names, List<string> addresses)
        {
            names.Clear();
            addresses.Clear();
            GameInstance save = SaveGameManager.Current;
            for (int i = 0; save != null && save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count && names.Count < 4; i++)
            {
                BuildingRegistration reg = save.BuildingRegistrations[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.BusinessName) && reg.Address != null)
                {
                    names.Add(reg.BusinessName);
                    addresses.Add(AddressKey(reg.Address));
                }
            }
        }

        public static string AddressKey(Address a) => a != null ? a.streetName + "~" + a.streetNumber : null;

        /// <summary>The registration of the player's own business at an address key, or null.</summary>
        public static BuildingRegistration ShopAt(string key)
        {
            GameInstance save = SaveGameManager.Current;
            for (int i = 0; key != null && save != null && save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
            {
                BuildingRegistration reg = save.BuildingRegistrations[i];
                if (reg != null && GoingPublic.Mine(reg) && AddressKey(reg.Address) == key)
                {
                    return reg;
                }
            }
            return null;
        }

        // ---------------------------------------------------------------- the day

        /// <summary>The game's day, hour and weather, the player's name and businesses; the rest of what is known through the wiring (StreetBridges.Facts).</summary>
        public static void Fill(StreetFacts f, List<string> addresses)
        {
            f.Clear();
            f.Day = TimeHelper.CurrentDay;
            f.Hour = TimeHelper.CurrentHour;
            f.Minute = Mathf.FloorToInt(TimeHelper.NowInMinutes()) % 60;
            f.Weekend = SocialLife.Weekend(f.Day);
            f.Raining = RainHelper.isRaining;
            f.Player = Texts.TextsGame.PlayerName;
            Shops(f.Shops, addresses);
            StreetBridges.Facts?.Invoke(f);
        }

        /// <summary>Game minutes since day 0.</summary>
        public static long Now() => (long)TimeHelper.NowInMinutes();
    }

    /// <summary>
    /// Somebody of the street held for a conversation (StreetOffer says what is changed for each kind). A walker: its own
    /// walking, standing and range behaviours paused (their components switched off, as nothing else of theirs runs
    /// meanwhile), its navigation agent stopped where it stands and its turning left to the game's own turn towards the
    /// player. Somebody standing about: turned to the player on the spot (the game's standing figures have no turn of their
    /// own: a short turn over <see cref="StreetRules.TurnSeconds"/>, as a horn's look round turns them), their idling left
    /// to the game. Somebody seated: nothing changed, they stay seated as posed. Everything is given back exactly as found
    /// when it ends: the walker walks on to where it was going, the standing figure turns back to the way it stood. If the
    /// game takes them meanwhile (the pool puts them away, a car hits them) they are let go. While their answer is on its
    /// way from the player's AI service, somebody standing may talk with their hands (<see cref="Speak"/>: the mod's own
    /// talking clip over the upper body, as everyone the mod has talking), at rest again when the line shows, and handed
    /// back to the game's own animation at once when they are let go.
    /// COST: <see cref="Keep"/> is a few field reads on the conversation's tick; a turn is a coroutine of a few frames.
    /// </summary>
    public sealed class StreetHold
    {
        private const string SpeakClip = "talk_a";   // talking with the hands, to somebody in front (BusinessVisit, ChatActivity)
        private const float SpeakFade = 0.3f;        // seconds the hands take to begin and to come to rest

        private ActivityAnimator _speaking;          // the clip player of a figure talking with its hands, until Quiet or Release
        private bool _spoke;                         // its hands moved during this hold: its clip player is let go at Release
        private BaseHuman _body;             // who is held (a walker's own figure, or a standing or sitting figure)
        private Pedestrian _p;               // their walker, or null
        private StreetFigure _kind;
        private bool _ped, _walk, _stay;     // which of the walker's behaviours were on
        private bool _agent;                 // its agent was on the navigation mesh, and is stopped by us
        private bool _stopped, _turning;     // the agent's own stop and turning, as found
        private Vector3 _look;               // the figure's look target, as found
        private bool _turned;                // a standing figure was turned by us
        private Quaternion _stood;           // ...and the way it stood
        private Coroutine _turn;             // the turn towards the player under way
        private MonoBehaviour _host;         // what runs the turns (the director)
        private BaseHuman _back;             // a standing figure turning back after its talk, the way it stood, and that turn
        private Quaternion _backTo;
        private Coroutine _backTurn;

        public bool Held => _body != null;

        /// <summary>Their figure (a walker's or a standing or sitting one's).</summary>
        public BaseHuman Body => _body;

        public StreetFigure Kind => _kind;

        /// <summary>The game's character with its own turn (a walker's), for the dialogue panel; null for a standing or sitting figure.</summary>
        public ThirdPersonCharacter Character => _p != null ? _p.tpc : null;

        /// <summary>True if the game took them back while held (put away, hit, borrowed by the ragdolls).</summary>
        public bool Gone => (object)_body != null && (_body == null || !_body.gameObject.activeInHierarchy || RagdollSystem.Holds(_body)
                                                     || ((object)_p != null && (_p == null || _p.tpc == null)));

        /// <summary>Why the last <see cref="Speak"/> showed nothing, for the self-check line.</summary>
        public string Why { get; private set; }

        /// <summary>
        /// They begin to talk with their hands, about to answer: the mod's talking clip over the upper body. False, with
        /// nothing changed, for somebody seated, without the clip, or when the clips cannot play. Once until <see cref="Quiet"/>.
        /// </summary>
        public bool Speak()
        {
            Why = null;
            if (_body == null || _speaking != null)
            {
                Why = _body == null ? "nobody is held" : "already talking with their hands";
                return false;
            }
            if (_kind == StreetFigure.Seated)
            {
                Why = "seated, posed as the game sat them";
                return false;
            }
            ActivityAnimator player = ActivityAnimators.Working && _body.animator != null ? ActivityAnimators.For(_body.animator) : null;
            if (player == null || !player.Usable || !ClipLibrary.TryGet(SpeakClip, out CityClip clip))
            {
                Why = "the clip '" + SpeakClip + "' cannot play on them";
                return false;
            }
            BodyMask mask = clip.Mask == BodyMask.Full ? BodyMask.Upper : clip.Mask;
            if (!player.PlayOver(clip.Clip, mask, SpeakFade, 1f, ClipEnd.Return))
            {
                Why = "the clip '" + SpeakClip + "' did not start";
                return false;
            }
            _speaking = player;
            _spoke = true;
            ClipLibrary.Played(SpeakClip, "a stranger about to answer in the street");
            return true;
        }

        /// <summary>The hands come to rest (their line is said): the game's own animation again.</summary>
        public void Quiet()
        {
            ActivityAnimator player = _speaking;
            _speaking = null;
            if (player != null && player.Animator != null)
            {
                player.StopOver(SpeakFade);
            }
        }

        // Let go: the game's own animation at once (a walker goes back to the game's pool, which may reuse the figure).
        private void Unspeak(BaseHuman body)
        {
            bool spoke = _spoke;
            _speaking = null;
            _spoke = false;
            if (spoke && body != null && body.animator != null)
            {
                ActivityAnimators.Forget(body.animator);
            }
        }

        /// <summary>True while this figure is held (another feature's reactions leave them alone meanwhile).</summary>
        public bool Holds(BaseHuman figure) => _body != null && ReferenceEquals(_body, figure);

        /// <summary>Stops somebody where they are and turns them to the player as their kind allows. False if they cannot be held.</summary>
        public bool Take(BaseHuman body, Pedestrian p, StreetFigure kind, Vector3 player, MonoBehaviour host)
        {
            if (_body != null || body == null || !StreetOffer.Offered(kind) || (p != null ? !StreetGame.Usable(p) : !StreetGame.Usable(body)))
            {
                return false;
            }
            HornReactions.LetGo(body);   // (a horn's look round put back first: the reaction is over)
            _body = body;
            _p = kind == StreetFigure.Walker ? p : null;
            _kind = _p != null ? StreetFigure.Walker : kind == StreetFigure.Walker ? StreetFigure.Seated : kind;
            _host = host;
            _turned = false;
            _agent = false;
            if (_p != null)
            {
                HornReactions.LetGo(_p);
                TakeWalker(_p);
            }
            else if (StreetOffer.TurnsBody(_kind))
            {
                _stood = body.transform.rotation;
                if (ReferenceEquals(_back, body))
                {
                    _stood = _backTo;   // (still turning back from an earlier talk: the way it stood is the one kept then)
                    StopBack(false);
                }
                _turned = true;
            }
            Face(player);
            return true;
        }

        private void TakeWalker(Pedestrian p)
        {
            _ped = p.enabled;
            _walk = p.pedestrianWalkingAiBehavior != null && p.pedestrianWalkingAiBehavior.enabled;
            _stay = p.pedestrianStationaryBehavior != null && p.pedestrianStationaryBehavior.enabled;
            p.enabled = false;
            if (p.pedestrianWalkingAiBehavior != null)
            {
                p.pedestrianWalkingAiBehavior.enabled = false;
            }
            if (p.pedestrianStationaryBehavior != null)
            {
                p.pedestrianStationaryBehavior.enabled = false;
            }
            NavMeshAgent agent = p.tpc.navmeshAgent;
            _agent = agent != null && agent.enabled && agent.isOnNavMesh;
            if (_agent)
            {
                _stopped = agent.isStopped;
                _turning = agent.updateRotation;
                agent.isStopped = true;
                agent.updateRotation = false;
            }
            _look = p.tpc.LookTarget;
        }

        /// <summary>Turns them to face a point: the game's own turn and look target for a walker, a short turn for somebody standing, nothing for somebody seated.</summary>
        public void Face(Vector3 point)
        {
            if (_p != null)
            {
                ThirdPersonCharacter c = _p.tpc;
                if (c == null || !c.isActiveAndEnabled)
                {
                    return;
                }
                Vector3 to = point - c.transform.position;
                to.y = 0f;
                if (to.sqrMagnitude > 0.04f)
                {
                    c.StartCoroutine(Guard.Routine(c.RotateTowards(Quaternion.LookRotation(to), StreetRules.TurnSeconds)));
                }
                if (!_agent)
                {
                    c.LookTarget = new Vector3(point.x, c.transform.position.y, point.z);   // (a figure off the mesh, standing at a spot, turns to its look target each frame: the game's own)
                }
                return;
            }
            if (!_turned || _body == null)
            {
                return;   // (seated: stays as posed)
            }
            Vector3 flat = point - _body.transform.position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.04f)
            {
                StopTurn();
                Turn(_body, Quaternion.LookRotation(flat, Vector3.up));
            }
        }

        /// <summary>On the conversation's tick: a game rule that let the agent go again (a crossing's green light) is undone.</summary>
        public void Keep()
        {
            if (_p == null || !_agent || _p.tpc == null)
            {
                return;
            }
            NavMeshAgent agent = _p.tpc.navmeshAgent;
            if (agent != null && agent.enabled && agent.isOnNavMesh && !agent.isStopped)
            {
                agent.isStopped = true;
            }
        }

        /// <summary>Everything given back as found: a walker walks on, somebody standing turns back. Safe at any time, and after the game took them.</summary>
        public void Release()
        {
            BaseHuman body = _body;
            Pedestrian p = _p;
            bool turned = _turned;
            Unspeak(body);
            _body = null;
            _p = null;
            _turned = false;
            StopTurn();
            if (p != null)
            {
                ReleaseWalker(p);
                return;
            }
            if (!turned || body == null)
            {
                return;
            }
            try
            {
                StopBack(true);   // (another figure still turning back: set as it stood at once)
                if (body.gameObject.activeInHierarchy && _host != null && _host.isActiveAndEnabled)
                {
                    _back = body;
                    _backTo = _stood;
                    _backTurn = _host.StartCoroutine(Guard.Routine(Turning(body.transform, _stood)));   // (turned back as they stood: the last frame sets it exactly)
                }
                else
                {
                    body.transform.rotation = _stood;   // (unloading, or put away by the pool: at once)
                }
            }
            catch (Exception e)
            {
                StreetGame.Logger?.Error(e);
            }
        }

        private void ReleaseWalker(Pedestrian p)
        {
            if (p == null || p.tpc == null)
            {
                return;
            }
            try
            {
                p.tpc.LookTarget = _look;
                NavMeshAgent agent = p.tpc.navmeshAgent;
                if (_agent && agent != null && agent.enabled && agent.isOnNavMesh)
                {
                    agent.isStopped = _stopped;
                    agent.updateRotation = _turning;
                }
                if (p.pedestrianWalkingAiBehavior != null)
                {
                    p.pedestrianWalkingAiBehavior.enabled = _walk;
                }
                if (p.pedestrianStationaryBehavior != null)
                {
                    p.pedestrianStationaryBehavior.enabled = _stay;
                }
                p.enabled = _ped;
            }
            catch (Exception e)
            {
                StreetGame.Logger?.Error(e);
            }
        }

        private void Turn(BaseHuman body, Quaternion to)
        {
            if (_host != null && _host.isActiveAndEnabled)
            {
                _turn = _host.StartCoroutine(Guard.Routine(Turning(body.transform, to)));
            }
            else
            {
                body.transform.rotation = to;
            }
        }

        private void StopTurn()
        {
            if (_turn != null && _host != null)
            {
                _host.StopCoroutine(_turn);
            }
            _turn = null;
        }

        // A turn back under way is stopped; finish: the figure is set as it stood at once (else whoever takes it next keeps the way it stood).
        private void StopBack(bool finish)
        {
            if (_backTurn != null && _host != null)
            {
                _host.StopCoroutine(_backTurn);
            }
            if (finish && _back != null && _back.gameObject.activeInHierarchy)
            {
                _back.transform.rotation = _backTo;
            }
            _backTurn = null;
            _back = null;
        }

        // A standing figure turned on the spot at the speed of the walkers' own turn; the last frame sets the rotation exactly.
        private static IEnumerator Turning(Transform body, Quaternion to)
        {
            float rate = 180f / Mathf.Max(0.1f, StreetRules.TurnSeconds);   // degrees a second: half a turn in the turn's time
            while (body != null && body.gameObject.activeInHierarchy && Quaternion.Angle(body.rotation, to) > 0.5f)
            {
                body.rotation = Quaternion.RotateTowards(body.rotation, to, rate * Time.deltaTime);
                yield return null;
            }
            if (body != null && body.gameObject.activeInHierarchy)
            {
                body.rotation = to;
            }
        }
    }
}
