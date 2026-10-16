using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.DayNightCycle;
using Dialogs;
using Extensions;
using Helpers;
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
    /// COST: <see cref="Facing"/> is a pass over the walkers that are out (a few dozen distance tests) four times a second
    /// while the player is on foot out of doors; everything else runs on a press or a click.
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

        /// <summary>
        /// The walker of the game's own in front of the player within reach, nearest first; the one already offered keeps the
        /// offer until it is <paramref name="letGo"/> away. Never one another feature holds (the ragdolls, a borrowed walker:
        /// its own behaviour switched off), never one hidden, never another player's figure in a Going Public session.
        /// </summary>
        public static Pedestrian Facing(Vector3 player, Vector3 forward, Pedestrian current, float now)
        {
            List<Pedestrian> walkers = PerfGame.Pedestrians;
            if (walkers == null)
            {
                return null;
            }
            if (GoingPublic.InSession && now - _remoteAt > RemoteEvery)
            {
                _remoteAt = now;
                Remote.Clear();
                GoingPublic.RemotePlayers(Remote);
            }
            forward.y = 0f;
            Pedestrian best = null;
            float bestSqr = StreetRules.Reach * StreetRules.Reach;
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian p = walkers[i];
                if (p == null || !Usable(p))
                {
                    continue;
                }
                Vector3 offset = p.transform.position - player;
                if (Mathf.Abs(offset.y) > 1.5f)
                {
                    continue;
                }
                offset.y = 0f;
                float sqr = offset.sqrMagnitude;
                float most = ReferenceEquals(p, current) ? StreetRules.LetGo * StreetRules.LetGo : bestSqr;
                if (sqr > most || sqr < 0.01f || Vector3.Dot(offset.normalized, forward.normalized) < StreetRules.Facing || Theirs(p))
                {
                    continue;
                }
                if (ReferenceEquals(p, current))
                {
                    return p;   // (who was offered keeps the offer while near)
                }
                best = p;
                bestSqr = sqr;
            }
            return best;
        }

        /// <summary>True for a walker the game runs as it should, out and seen: not held by the ragdolls or borrowed by another feature.</summary>
        public static bool Usable(Pedestrian p)
        {
            return p.isActiveAndEnabled && p.tpc != null && p.tpc.visible && p.gameObject.activeInHierarchy && !RagdollSystem.Holds(p.tpc);
        }

        private static bool Theirs(Pedestrian p)
        {
            for (int i = 0; i < Remote.Count; i++)
            {
                Transform t = Remote[i];
                if (t != null && (p.transform.IsChildOf(t) || (t.position - p.transform.position).sqrMagnitude < RemoteClear * RemoteClear))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The game's own figure for a person: its instance stands for one look, so one person, for the session.</summary>
        public static int Key(Pedestrian p) => p.GetInstanceID();

        public static bool Female(Pedestrian p)
        {
            return p.tpc != null && p.tpc.appearanceSetter != null && p.tpc.appearanceSetter.data != null && p.tpc.appearanceSetter.data.gender == Gender.Female;
        }

        /// <summary>Their age in years, as the game made their look (AppearanceAgeHelper: days of the game's year).</summary>
        public static int Age(Pedestrian p)
        {
            int days = p.tpc != null && p.tpc.appearanceSetter != null && p.tpc.appearanceSetter.data != null ? p.tpc.appearanceSetter.data.ageInDays : 0;
            return days > 0 ? days / Math.Max(1, SocialPlaces.DaysPerYear) : 35;
        }

        /// <summary>0 nothing, 1 looking at their phone, 2 on a call: the game's own animation on the figure.</summary>
        public static int Phone(Pedestrian p)
        {
            Animator animator = p.tpc != null ? p.tpc.animator : null;
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
        public static bool OnFootOutside()
        {
            return !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle && LifeEvents.IsSafeForEvent() && !Wiring.Scenes.PlayerHeld;
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

        /// <summary>True when the game has its text box to ask for a line in.</summary>
        public static bool CanType
        {
            get
            {
                var uis = InstanceBehavior<UI.UIs>.Instance;
                return uis != null && uis.textInputUi != null;
            }
        }

        /// <summary>The game's own text box over everything (its own canvas), for the player's words. False if the game has none.</summary>
        public static bool AskWords(Action<string> said, Action closed)
        {
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.textInputUi == null)
            {
                return false;
            }
            uis.textInputUi.Show("reallife_street_say_placeholder", "reallife_street_btn_say", Guard.Wrap(said), Guard.Wrap(closed), string.Empty, false, StreetSay.Most);
            return true;
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
    /// One of the game's walkers held still for a conversation: its own walking, standing and range behaviours paused (their
    /// components switched off, as nothing else of theirs runs meanwhile), its navigation agent stopped where it stands and
    /// its turning left to the game's own turn towards the player; everything given back exactly as found when it ends, so
    /// it walks on to where it was going. If the game takes it meanwhile (the pool puts it away, a car hits it) it is let go.
    /// COST: <see cref="Keep"/> is a few field reads on the conversation's tick.
    /// </summary>
    public sealed class StreetHold
    {
        private Pedestrian _p;
        private bool _ped, _walk, _stay;     // which of its behaviours were on
        private bool _agent;                 // its agent was on the navigation mesh, and is stopped by us
        private bool _stopped, _turning;     // the agent's own stop and turning, as found
        private Vector3 _look;               // the figure's look target, as found

        public bool Held => _p != null;

        public Pedestrian Walker => _p;

        public ThirdPersonCharacter Character => _p != null ? _p.tpc : null;

        /// <summary>True if the game took the walker back while it was held (put away, hit, borrowed by the ragdolls).</summary>
        public bool Gone => (object)_p != null && (_p == null || !_p.gameObject.activeInHierarchy || _p.tpc == null || RagdollSystem.Holds(_p.tpc));

        /// <summary>Stops a walker where it stands and turns it to the player. False if it cannot be held.</summary>
        public bool Take(Pedestrian p, Vector3 player)
        {
            if (_p != null || p == null || p.tpc == null || !StreetGame.Usable(p))
            {
                return false;
            }
            _p = p;
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
            Face(player);
            return true;
        }

        /// <summary>Turns them to face a point: the game's own turn, and the look target that keeps a standing figure facing it.</summary>
        public void Face(Vector3 point)
        {
            ThirdPersonCharacter c = Character;
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

        /// <summary>Everything given back as found: they walk on. Safe at any time, and after the game took them.</summary>
        public void Release()
        {
            Pedestrian p = _p;
            _p = null;
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
    }
}
