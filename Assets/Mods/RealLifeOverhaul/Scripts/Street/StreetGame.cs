using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InputSystem;
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

namespace RealLifeOverhaul.Street
{
    /// <summary>
    /// The one place street talk touches the game: its crowd (the walkers the spawner keeps; nothing is searched), what it
    /// shows of a person, its dialogue panel, text box, destination and notifications, and the city's businesses.
    /// COST: <see cref="Facing"/> is a pass over the people out (a few dozen distance tests) five times a second on foot out
    /// of doors; everything else runs on a press or a click. Nothing allocated per pass.
    /// </summary>
    public static class StreetGame
    {
        public static IModLogger Logger;

        private const string DialogTypeName = "reallife_street_calldialogtype";
        private static readonly int TalkingPhone = Animator.StringToHash("TalkingPhone");   // the game's looping animations, by PermanentAnimationType name
        private static readonly int TextingPhone = Animator.StringToHash("TextingPhone");
        private static readonly List<Transform> Remote = new List<Transform>(4);
        private static float _remoteAt = -100f;
        private const float RemoteEvery = 5f;      // real seconds between two looks at the other players' figures, in a Going Public session
        private const float RemoteClear = 0.6f;    // metres: a walker this near another player's figure is never offered (it may be theirs)

        private const float NearAny = 12f;   // metres: the nearest person within this is named in the offer's check line
        private static readonly Dictionary<int, StreetFigure> Kinds = new Dictionary<int, StreetFigure>(128);   // a standing or sitting figure's kind, by its instance
        private const int KindsMost = 512;    // figures told apart at most before the list starts again (the game's pools hold far fewer)

        /// <summary>For the offer's check line: the walkers and the standing and sitting people looked at in the last pass.</summary>
        public static int LastWalkers, LastFigures;

        /// <summary>For the offer's check line: the last pass's nearest person within <see cref="NearAny"/>, at any angle.</summary>
        public static bool NearFound, NearUsable;
        public static float NearSqr, NearCos;
        public static StreetFigure NearKind;

        /// <summary>
        /// The person in front of the player within reach, of the walkers and of the people standing and sitting; never one
        /// another feature holds, hidden, busy, or another player's figure. <paramref name="walker"/> is null for somebody
        /// standing or sitting.
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
                    return body;   // who was offered keeps the offer while near
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

        // Keeps the pass's nearest person for the check line.
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

        /// <summary>True while the one offered is still out, usable and within <see cref="StreetRules.GraceMetres"/>.</summary>
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

        /// <summary>True for a walker out and seen, not held by the ragdolls or borrowed by another feature.</summary>
        public static bool Usable(Pedestrian p)
        {
            return p.isActiveAndEnabled && p.tpc != null && p.tpc.visible && p.gameObject.activeInHierarchy && !RagdollSystem.Holds(p.tpc);
        }

        /// <summary>True for a standing or sitting figure out and drawn, not held by the ragdolls or turned by a horn.</summary>
        public static bool Usable(BaseHuman h)
        {
            return h.isActiveAndEnabled && h.gameObject.activeInHierarchy && h.animator != null && h.appearanceSetter != null && !RagdollSystem.Holds(h)
                   && !HornReactions.Reacts(h);
        }

        /// <summary>
        /// The kind of a standing or sitting figure: a hangout spot's figure stands or sits, the bench pool's sits; a
        /// performer, somebody homeless (the game's own box is theirs) and the other pools are busy. Told apart once a figure.
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

        /// <summary>A person's key: the figure's instance, one look and so one person for the session.</summary>
        public static int Key(BaseHuman b) => b.GetInstanceID();

        public static bool Female(BaseHuman p)
        {
            return p.appearanceSetter != null && p.appearanceSetter.data != null && p.appearanceSetter.data.gender == Gender.Female;
        }

        /// <summary>Their age in years, from their look's age in days (AppearanceAgeHelper).</summary>
        public static int Age(BaseHuman p)
        {
            int days = p.appearanceSetter != null && p.appearanceSetter.data != null ? p.appearanceSetter.data.ageInDays : 0;
            return days > 0 ? days / Math.Max(1, SocialPlaces.DaysPerYear) : 35;
        }

        /// <summary>Their look as a kept look's text (People/PersonLook); null if it has none.</summary>
        public static string Look(BaseHuman p)
        {
            return p != null && p.appearanceSetter != null ? PersonLookGame.Of(p.appearanceSetter.data) : null;
        }

        /// <summary>0 nothing, 1 looking at their phone, 2 on a call, by the game's animation on the figure.</summary>
        public static int Phone(BaseHuman p)
        {
            Animator animator = p != null ? p.animator : null;
            if (animator == null || animator.runtimeAnimatorController == null)
            {
                return 0;
            }
            return animator.GetBool(TalkingPhone) ? 2 : animator.GetBool(TextingPhone) ? 1 : 0;
        }

        /// <summary>The save's own number for its people: its character id, hashed the same way on every run.</summary>
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

        public static PlayerController Player => PlayerHelper.PlayerController;

        /// <summary>True out of doors, on foot, with nothing of the game's or the mod's holding the player.</summary>
        public static bool OnFootOutside() => NotOnFoot() == null;

        /// <summary>True out of doors and not in a vehicle: the offer's check line is written only then.</summary>
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

        /// <summary>Turns the player on the spot to face a point, by the game's own turn.</summary>
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

        /// <summary>Opens the game's dialogue panel over a person, as a counter's clerk opens one.</summary>
        public static void OpenDialog(ThirdPersonCharacter with, Func<Dialog> maker, Action closed)
        {
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogTypeName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(maker));
            InstanceBehavior<UI.UIs>.Instance.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(closed), with);
        }

        /// <summary>True while the game's dialogue panel is open, anybody's.</summary>
        public static bool DialogOpen => SocialPhone.DialogOpen;

        /// <summary>
        /// True in the frame the game's cancel key was pressed: the panel's grey button and the key share one action
        /// (DialogUI.OnCancel), and this tells the key from a click.
        /// </summary>
        public static bool CancelKey => PlayerAction.Cancel.Pressed();

        public static bool CanType => TextBox.CanType;

        /// <summary>Opens the game's text box for the player's words; false if the game has none.</summary>
        public static bool AskWords(Action<string> said, Action closed)
        {
            return TextBox.Ask("reallife_street_say_placeholder", "reallife_street_btn_say", Guard.Wrap(said), Guard.Wrap(closed), string.Empty, false, StreetSay.Most);
        }

        public static void Notice(NotificationType type, string key, Dictionary<string, string> data, float seconds = 10f)
        {
            Notifications.Show(type, key, data, seconds);
        }

        /// <summary>The nearest business of a kind within <see cref="StreetRules.FindMetres"/>: a walk over the city's buildings.</summary>
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
                return false;   // a building the game has no data for is no business
            }
        }

        /// <summary>Which way a place is from the player's facing (StreetRules.Way).</summary>
        public static int Way(Vector3 player, Vector3 forward, Vector3 place)
        {
            Vector3 to = place - player;
            to.y = 0f;
            forward.y = 0f;
            return StreetRules.Way(Vector3.SignedAngle(forward, to, Vector3.up));
        }

        /// <summary>Makes the place the player's destination, as the game's "Set destination" does (BizManBusiness.SetDestination).</summary>
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

        /// <summary>Fills the lists with the player's own trading businesses, by name and by address key, four at most.</summary>
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

        /// <summary>Fills what is known now: the game's day and weather, the player's name and businesses, the rest through StreetBridges.Facts.</summary>
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
}
