using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.SoundSystem;
using Character;
using Characters;
using Characters.EmojiSystem;
using Extensions;
using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's people: a small, fixed pool of street characters shared by every scene
    /// (accident drivers, paramedics, officers, suspects, casualties).
    /// Building a character's look is the expensive part (the game merges its meshes), so
    /// characters are created once, kept, and handed back out in the same outfit whenever
    /// possible.
    /// Twelve at most. One of these costs what one of the game's own pedestrians costs while it
    /// is drawn (one merged skinned mesh, one animator, one navigation agent, of which the game
    /// keeps about a hundred on the street) and nothing while it is not: out of view its animator
    /// and renderer are off, standing still its agent is off, and on the shelf the whole object
    /// is. Twelve is the worst scene (two drivers, two paramedics, two officers, a suspect and
    /// the officer arresting them: eight) plus four who are only there to be seen: about a tenth
    /// more people on the street at the very most. Those four (officers on the beat, by a parked
    /// patrol car, at a junction) always give way: when a scene asks for somebody and nobody is
    /// free, one of them is taken off the street that instant and handed over. The pool also switches off the animation and rendering of people who are
    /// far away or behind the camera: a handful of comparisons per frame, and only while
    /// somebody is out on the street. With nobody out, nothing here runs.
    ///
    /// When looks are built (People/Looks has the rule): six civilians while the city loads, behind
    /// the loading screen; four officers and two paramedics as soon as the modelled props have loaded
    /// (their kit is made of them), one at a time. So all twelve exist before the first scene, each
    /// dressed for a part, and a scene is handed people who are ready. A civilian keeps a look for
    /// three outings or ten minutes and is then given a new one on the shelf, one at a time with
    /// seconds between; whoever came back longest ago goes out first, so the same face is not seen
    /// twice running. Only when a scene needs a part nobody on the shelf is dressed for is a look
    /// built on the spot (whoever there are most of on the shelf is the one re-dressed).
    /// </summary>
    public class ActorPool : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("people", "ActorPool", typeof(ActorPool));   // its Update, LateUpdate and FixedUpdate (Core/Guard)

        /// <summary>Hard cap on mod characters alive at once, across all scenes.</summary>
        public const int MaxActors = 12;

        /// <summary>How many of them may be out only to be seen (see <see cref="SpawnPresence"/>).</summary>
        public const int MaxPresence = 4;

        private const int PresenceHeadroom = 2;   // characters always left unused when someone is put out only to be seen

        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 170f;   // metres from the camera
        private const float HideBehind = 6f;       // metres behind the camera's own plane
        private const int ReadyCivilians = 6;      // made while the city loads
        private const int ReadyOfficers = 4;       // made once the modelled props are there (or have failed), one at a time
        private const int ReadyParamedics = 2;
        private const float PropsPatience = 25f;   // seconds the uniformed wait for the props before they are made with plain shapes

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };

        public static IModLogger Logger;
        public static ActorPool Instance { get; private set; }

        private readonly List<StreetActor> _actors = new List<StreetActor>(MaxActors);
        private bool _hidingFailed;   // hiding people threw once; it stays off for the rest of the session
        private bool _placeWarned;
        private Coroutine _refresh;   // looking after the shelf (making the uniformed, new looks for civilians); null when there is nothing to do
        private bool _uniformsDue;    // the officers and paramedics of the ready pool are still to be made
        private float _uniformsSince;

        /// <summary>How many more people a scene could put on the street right now. Whoever is only there to be seen counts as free: they give way.</summary>
        public int Free => MaxActors - Count(yielding: false);

        /// <summary>How many people are out only to be seen.</summary>
        public int Presence => Count(yielding: true);

        private int Count(bool yielding)
        {
            int count = 0;
            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i].InUse && _actors[i].Yields == yielding)
                {
                    count++;
                }
            }
            return count;
        }

        private void Awake()
        {
            Instance = this;
            enabled = false;   // nobody to look after yet
        }

        private void OnDestroy()
        {
            foreach (StreetActor actor in _actors)
            {
                try
                {
                    if (actor.IsValid)
                    {
                        actor.ResetForReuse();
                        Destroy(actor.Character.gameObject);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                actor.InUse = false;
            }
            _actors.Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        private static readonly int HotUpdate = HotSpots.Slot("People: ActorPool.Update");
        // Runs only while somebody is out. Hides whoever cannot be seen; shows them again the frame they can.
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // (its feature was switched off after its faults: Core/Guard)
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            try
            {
                Camera camera = GameManager.GetMainCamera();
                if (camera == null)
                {
                    return;
                }
                Transform eye = camera.transform;
                Vector3 from = eye.position;
                Vector3 forward = eye.forward;
                bool any = false;
                for (int i = 0; i < _actors.Count; i++)
                {
                    StreetActor actor = _actors[i];
                    if (!actor.InUse || !actor.IsValid)
                    {
                        continue;
                    }
                    any = true;
                    Vector3 offset = actor.Position - from;
                    actor.SetVisible(offset.sqrMagnitude < HideDistance * HideDistance && Vector3.Dot(offset, forward) > -HideBehind);
                }
                if (!any)
                {
                    enabled = false;
                }
            }
            catch (Exception e)
            {
                // Hiding is only a saving: give it up rather than fault again next frame.
                Logger?.Error(e);
                _hidingFailed = true;
                enabled = false;
                ShowEveryone();
            }
        }

        private void ShowEveryone()
        {
            for (int i = 0; i < _actors.Count; i++)
            {
                try
                {
                    if (_actors[i].InUse && _actors[i].IsValid)
                    {
                        _actors[i].SetVisible(true);
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        /// <summary>
        /// Puts a person on the ground near a point. Returns null if there is nowhere to stand
        /// within the radius or all characters are in use; callers must cope with that.
        /// </summary>
        /// <param name="onRoad">Allow the road surface, not just pavements (for people at a crash).</param>
        public StreetActor Spawn(Vector3 near, ActorLook look, float radius = 6f, bool onRoad = false)
        {
            return Put(near, look, radius, onRoad, yields: false);
        }

        /// <summary>
        /// Puts out someone who is only there to be seen. Null unless fewer than
        /// <see cref="MaxPresence"/> such people are out and characters are to spare. Whoever is
        /// returned may be taken back at any moment, without warning, for a scene that needs them.
        /// </summary>
        public StreetActor SpawnPresence(Vector3 near, ActorLook look, float radius = 6f, bool onRoad = false)
        {
            int yielding = Count(yielding: true);
            if (yielding >= MaxPresence || yielding + Count(yielding: false) > MaxActors - PresenceHeadroom - 1)
            {
                return null;
            }
            return Put(near, look, radius, onRoad, yields: true);
        }

        private StreetActor Put(Vector3 near, ActorLook look, float radius, bool onRoad, bool yields)
        {
            StreetActor actor = null;
            try
            {
                // Street level only: the nearest walkable point to a car's middle is not always the ground beside it.
                bool found = onRoad ? StreetSpots.OnRoad(near, radius, out Vector3 spot) : StreetSpots.OnPavement(near, radius, out spot);
                if (!found)
                {
                    return null;
                }

                actor = Take(look, spot, mayReclaim: !yields);
                if (actor == null)
                {
                    return null;
                }
                actor.Character.gameObject.SetActive(true);
                if (!actor.Place(spot, onRoad))
                {
                    if (!_placeWarned)
                    {
                        _placeWarned = true;
                        Logger?.Warn($"People: a character could not be put on the {(onRoad ? "road" : "pavement")} at {spot}; that person is left out. (Logged once.)");
                    }
                    Shelve(actor);
                    return null;
                }
                actor.InUse = true;
                actor.Yields = yields;
                actor.Lease++;
                enabled = !_hidingFailed;
                return actor;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                if (actor != null)
                {
                    actor.InUse = true;   // so that Release puts it away whatever state it got into
                    Release(actor);
                }
                return null;
            }
        }

        public void Release(StreetActor actor)
        {
            if (actor == null || !actor.InUse)
            {
                return;
            }
            actor.InUse = false;
            actor.Yields = false;
            Shelve(actor);
            actor.ReleasedAt = Time.unscaledTime;
            if (actor.Look == ActorLook.Civilian)
            {
                // A look is kept for a few outings (whoever came back longest ago goes out first, so it is not the
                // same face twice running), then changed on the shelf, quietly: People/Looks.
                actor.Uses++;
                actor.Stale = Looks.Worn(actor.Uses, actor.DressedAt);
            }
            Tend();
        }

        /// <summary>
        /// Called once while the city loads (the loading screen is up): the civilians of the ready pool are made
        /// now, and the uniformed are left to the shelf's own timer, which waits for the modelled props.
        /// </summary>
        public void Prewarm()
        {
            if (!Settings.People)
            {
                return;   // nobody is put on the street: nothing is made until something asks after all
            }
            try
            {
                for (int i = 0; i < ReadyCivilians && _actors.Count < MaxActors; i++)
                {
                    Make(ActorLook.Civilian);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // they are made on first need instead, as before
            }
            _uniformsDue = true;
            _uniformsSince = Time.unscaledTime;
            Tend();
        }

        private void Tend()
        {
            if (_refresh == null && gameObject.activeInHierarchy && (_uniformsDue || AnyStale()))
            {
                _refresh = StartCoroutine(Guard.Routine(Tending()));
            }
        }

        private bool AnyStale()
        {
            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i].Stale && !_actors[i].InUse)
                {
                    return true;
                }
            }
            return false;
        }

        // Runs only while the shelf has something to see to: one look at a time, when People/Looks says one may be built.
        private IEnumerator Tending()
        {
            while (_uniformsDue || AnyStale())
            {
                yield return null;
                if (!Looks.Free)
                {
                    continue;
                }
                try
                {
                    TendOne();
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    _uniformsDue = false;
                    for (int i = 0; i < _actors.Count; i++)
                    {
                        _actors[i].Stale = false;   // not tried again: they keep the look they have
                    }
                }
            }
            _refresh = null;
        }

        private void TendOne()
        {
            if (_uniformsDue)
            {
                if (!PropLibrary.Ready && Time.unscaledTime - _uniformsSince < PropsPatience)
                {
                    return;   // their kit is made of the modelled props: a little longer
                }
                if (_actors.Count < MaxActors && Have(ActorLook.Officer) < ReadyOfficers)
                {
                    Make(ActorLook.Officer);
                    return;
                }
                if (_actors.Count < MaxActors && Have(ActorLook.Paramedic) < ReadyParamedics)
                {
                    Make(ActorLook.Paramedic);
                    return;
                }
                _uniformsDue = false;
            }
            for (int i = 0; i < _actors.Count; i++)
            {
                StreetActor actor = _actors[i];
                if (actor.Stale && !actor.InUse && actor.IsValid && actor.Look == ActorLook.Civilian)
                {
                    Redress(actor);
                    return;
                }
                if (actor.Stale && !actor.InUse)
                {
                    actor.Stale = false;
                }
            }
        }

        private int Have(ActorLook look)
        {
            int have = 0;
            for (int i = 0; i < _actors.Count; i++)
            {
                if (_actors[i].IsValid && _actors[i].Look == look)
                {
                    have++;
                }
            }
            return have;
        }

        // One more character, dressed and put on the shelf.
        private void Make(ActorLook look)
        {
            Vector3 near = Vector3.down * 40f;   // under the street, out of sight
            var gm = InstanceBehavior<GameManager>.Instance;
            if (gm != null && gm.playerController != null)
            {
                Vector3 player = gm.playerController.transform.position;
                near = StreetSpots.OnPavement(player, 25f, out Vector3 pavement) ? pavement : player + Vector3.down * 40f;
            }
            StreetActor made = Create(near);
            Dress(made, look);
            made.Character.gameObject.SetActive(false);
            made.ReleasedAt = Time.unscaledTime;
            Logger?.Info($"People: character {_actors.Count} of {MaxActors} made ready as {look}.");
        }

        // A new look for someone on the shelf: awake only while it is put on, and never drawn in between.
        private void Redress(StreetActor actor)
        {
            try
            {
                GameObject body = actor.Character.gameObject;
                body.SetActive(true);
                Dress(actor, actor.Look);
                body.SetActive(false);
            }
            catch (Exception e)
            {
                actor.Stale = false;   // not tried again: they keep the look they have
                Logger?.Error(e);
            }
        }

        /// <summary>For when the street is unloaded under us, such as the player entering a building.</summary>
        public void ReleaseAll()
        {
            foreach (StreetActor actor in _actors)
            {
                Release(actor);
            }
        }

        // Back on the shelf: reset and switched off, so it costs nothing until it is wanted again.
        private void Shelve(StreetActor actor)
        {
            try
            {
                if (actor.IsValid)
                {
                    actor.ResetForReuse();
                    actor.Character.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>
        /// A small emoji bubble over someone's head for a few seconds. Skipped for people who
        /// are not being drawn: the game places the bubble by projecting the head onto the screen.
        /// </summary>
        public void ShowEmoji(StreetActor actor, CharacterEmojiName emoji, float seconds)
        {
            if (actor != null && actor.InUse && actor.IsValid)
            {
                ShowEmoji(actor.Character, emoji, seconds);
            }
        }

        /// <summary>The same over any head: for the game's plain standing and sitting figures, which are not street characters.</summary>
        public void ShowEmojiAt(Transform head, CharacterEmojiName emoji, float seconds)
        {
            try
            {
                if (head != null && head.gameObject.activeInHierarchy && GameManager.GetMainCamera() != null
                    && CharacterEmojiSystem.GetCharacterEmojiByName(emoji) != null)
                {
                    StartCoroutine(CharacterEmojiSystem.ShowEmoji(head, emoji, false, seconds));
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The same for any street character, such as one of the game's own pedestrians.</summary>
        public void ShowEmoji(ThirdPersonCharacter character, CharacterEmojiName emoji, float seconds)
        {
            try
            {
                if (character == null || !character.visible || character.head == null
                    || GameManager.GetMainCamera() == null || CharacterEmojiSystem.GetCharacterEmojiByName(emoji) == null)
                {
                    return;
                }
                StartCoroutine(CharacterEmojiSystem.ShowEmoji(character.head, emoji, false, seconds));
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>How many characters exist, in use or not. With <see cref="ActorAt"/>, for looking at who is on the street.</summary>
        public int ActorCount => _actors.Count;

        public StreetActor ActorAt(int index)
        {
            return _actors[index];
        }

        /// <summary>The sound of someone getting into or out of a vehicle; the game has no animation for either.</summary>
        public static void PlayDoorSound(Vector3 position)
        {
            try
            {
                SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
                if (sounds != null)
                {
                    sounds.PlayAudio(SoundType.CarDoorOpenClose, position);
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // An idle character already dressed this way (whoever came back longest ago), else a new one, else an idle
        // one re-dressed, else (for a scene) one of those who are only out to be seen, taken off the street this instant.
        private StreetActor Take(ActorLook look, Vector3 position, bool mayReclaim)
        {
            StreetActor ready = null;
            StreetActor yielding = null;
            int idleCivilians = 0, idleOfficers = 0, idleParamedics = 0;
            for (int i = _actors.Count - 1; i >= 0; i--)
            {
                StreetActor actor = _actors[i];
                if (!actor.IsValid)
                {
                    actor.InUse = false;
                    _actors.RemoveAt(i);   // destroyed with a scene we did not unload ourselves
                    continue;
                }
                if (actor.InUse)
                {
                    // Sooner one nobody is looking at, and never one who is lying in the road.
                    if (actor.Yields && !actor.Limp && (yielding == null || (yielding.Visible && !actor.Visible)))
                    {
                        yielding = actor;
                    }
                    continue;
                }
                if (actor.Look == ActorLook.Civilian)
                {
                    idleCivilians++;
                }
                else if (actor.Look == ActorLook.Officer)
                {
                    idleOfficers++;
                }
                else
                {
                    idleParamedics++;
                }
                // Dressed for the part: sooner one whose look is not yet due a change, then whoever came back longest ago.
                if (actor.Look == look && (ready == null || (ready.Stale && !actor.Stale) || (ready.Stale == actor.Stale && actor.ReleasedAt < ready.ReleasedAt)))
                {
                    ready = actor;
                }
            }
            if (ready != null)
            {
                if (ready.Kit != null && !ready.Kit.Modelled && PropLibrary.Ready)
                {
                    // Fitted out before the modelled props had loaded: once, fitted again with them
                    // (an officer from scratch, since the cap replaces the game's hat).
                    ready.Character.gameObject.SetActive(true);
                    if (look == ActorLook.Officer)
                    {
                        Dress(ready, look);
                    }
                    else
                    {
                        Equip(ready, look, false);
                    }
                }
                return ready;
            }

            if (_actors.Count < MaxActors)
            {
                StreetActor created = Create(StreetSpots.OnPavement(position, 25f, out Vector3 pavement) ? pavement : position);
                Dress(created, look);
                Logger?.Info($"People: character {_actors.Count} of {MaxActors} created as {look}.");
                return created;
            }

            // Nobody on the shelf is dressed for it: one of the part there are most of on the shelf is re-dressed, here
            // and now (a scene cannot wait for its person). Sooner one whose look is due a change, then the longest back.
            StreetActor spare = null;
            ActorLook plenty = idleCivilians >= idleOfficers && idleCivilians >= idleParamedics ? ActorLook.Civilian
                : idleOfficers >= idleParamedics ? ActorLook.Officer : ActorLook.Paramedic;
            for (int i = 0; i < _actors.Count; i++)
            {
                StreetActor actor = _actors[i];
                if (actor.InUse || actor.Look != plenty)
                {
                    continue;
                }
                if (spare == null || (actor.Stale && !spare.Stale) || (actor.Stale == spare.Stale && actor.ReleasedAt < spare.ReleasedAt))
                {
                    spare = actor;
                }
            }
            if (spare == null && mayReclaim && yielding != null)
            {
                // Everyone is out, and a scene needs somebody: whoever was only there to be seen gives way.
                Logger?.Info($"People: {(yielding.Visible ? "an officer in view" : "an officer out of view")} who was only on patrol was taken off the street; a scene needs the character.");
                Release(yielding);
                if (yielding.Look == look)
                {
                    return yielding;
                }
                spare = yielding;
            }
            if (spare != null)
            {
                // Awake while it is dressed, the same as a newly created one.
                spare.Character.gameObject.SetActive(true);
                Dress(spare, look);
            }
            return spare;
        }

        // A new character, not yet dressed. Born on the pavement where it can be: the prefab's navigation agent is the
        // pavement kind and switches itself on as it is created, and off its own mesh (the place asked for is often on the
        // road) the engine warns that it "is not close enough to the NavMesh". Place moves the character to where it is
        // wanted, in the same frame.
        private StreetActor Create(Vector3 birth)
        {
            using var piece = SessionWatch.Piece("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, birth, Quaternion.identity, transform);
            character.name = "RealLifeOverhaul Actor";
            // Idle fidgets would play over the poses scenes set (lying down, on the phone).
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;
            }
            // These people go where their route takes them. Left to physics, the body is shoved up
            // and over whatever it brushes against between two cars, and stays there.
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;
            }
            var created = new StreetActor(character);
            _actors.Add(created);
            return created;
        }

        // Who they are (age, build, face, hair) is rolled for everyone. Civilians wear whatever casual
        // clothes come up; officers and paramedics then get their uniform, the same one every time,
        // and the equipment that goes with it.
        private static void Dress(StreetActor actor, ActorLook look)
        {
            Looks.Count("people: a street character's look being built (a mesh merge)");   // counted, and named for the long-frame line of the session record
            actor.Look = look;
            actor.Stale = false;
            actor.Uses = 0;
            actor.DressedAt = Time.unscaledTime;
            actor.Resize(1f);   // every look is put on, and measured, at the size the game builds it
            AppearanceSetter appearance = actor.Character.appearanceSetter;
            Uniforms.Restore(actor.Dyes);   // the colours of whatever uniform this character wore before
            actor.Shirt = default;
            if (look == ActorLook.Officer || look == ActorLook.Paramedic)
            {
                ServiceLook(appearance);
            }
            else
            {
                appearance.SetRandomAge();
                Gender gender = Random.value < 0.5f ? Gender.Male : Gender.Female;
                appearance.RandomizeElements(gender, SkinColorHelper.GetRandom(), CasualWear);
            }
            bool cap = look == ActorLook.Officer && ServiceKit.CapToBeHad;
            if (Uniforms.Has(look))
            {
                Uniforms.Wear(appearance, look, actor.Dyes, cap, ref actor.Shirt);
            }
            appearance.UpdateVisuals();   // the one mesh merge for this outfit
            Looks.Done();
            if (Equip(actor, look, cap) && actor.Kit.CapRefused)
            {
                // Too much hair for the modelled cap, which hides none: the game's own hat after all (it
                // flattens the hair under it). A second merge, for this one officer, this once.
                Uniforms.Restore(actor.Dyes);
                Uniforms.Wear(appearance, look, actor.Dyes, false, ref actor.Shirt);
                Looks.Count("people: an officer's look being built again (the cap did not fit)");
                appearance.UpdateVisuals();
                Looks.Done();
                Equip(actor, look, false);
            }
            if (Uniforms.Has(look))
            {
                Heighten(actor, look, appearance.data.gender == Gender.Male);
            }
        }

        // An officer stands as tall as the service wants (or as the player, if taller), a paramedic no shorter than
        // an adult: the whole character is scaled, equipment and all (Stature). Said once for each of the four.
        private static void Heighten(StreetActor actor, ActorLook look, bool man)
        {
            float crown = actor.Kit != null ? actor.Kit.Crown : 0f;
            float size = Stature.For(actor.Character.animator, look, man, crown, out float height, out float player);
            actor.Resize(size);
            int bit = (look == ActorLook.Officer ? 1 : 4) << (man ? 0 : 1);
            if ((_heightLogged & bit) == 0)
            {
                _heightLogged |= bit;
                string who = (man ? "male " : "female ") + (look == ActorLook.Officer ? "officer" : "paramedic");
                Logger?.Info(height <= 0f
                    ? $"People: a {who} could not be measured (head {(crown > 0f ? "measured" : "not measured")}); they keep the height the game gave them. (Logged once.)"
                    : $"People: a {who} is {height:F2} m as the game builds them (crown {crown:F3} m above the head joint; the player is "
                      + $"{(player > 0f ? player.ToString("F2") + " m" : "not measured")}); scaled by {size:F3} to {height * size:F2} m. (Logged once.)");
            }
        }

        private static int _heightLogged;

        // Officers and paramedics are not rolled like the public: one man and one woman (four men in five), each the same
        // every time (the game's own roll with a fixed seed gives the same face), in the lightest or the darkest of the
        // game's skin tones, a fixed build, a short tidy haircut that sits under the cap, dark hair, no beard.
        // At most four appearances exist. The haircut is the first the game has of the names below, in their order
        // (a whole name, such as the women's bun, or a word in one). The game's men's styles are only numbered
        // (MaleHair01 to 08), which says nothing of their length, so where no name matches the style with the least
        // hair is taken: the smallest mesh. What was picked, and how, is logged once for each sex with every style's
        // size, so that a style can be put first in the list by its name.
        private const int ManSeed = 1207;
        private const int WomanSeed = 2311;
        private const float Lean = 0.38f;       // CharacterData.fatness: 0.5 is neither fat nor thin, 0 the thinnest the game draws
        private const float Strong = 0.7f;
        private static readonly string[] ManHair = { "buzz", "crew", "short", "fade", "crop" };
        private static readonly string[] WomanHair = { "FemHair06Bun", "bun", "pony", "tied", "bob", "pixie", "short" };
        private static readonly string[] NoBeard = { "none" };
        private static readonly System.Text.StringBuilder Sizes = new System.Text.StringBuilder(256);
        private static SkinColor _lightSkin;
        private static SkinColor _darkSkin;
        private static int _serviceLogged;

        private static void ServiceLook(AppearanceSetter appearance)
        {
            if (_lightSkin == null)
            {
                float low = float.MaxValue, high = float.MinValue;
                for (int seed = 0; seed < 64; seed++)   // the helper only hands tones out by seed: once per session
                {
                    SkinColor tone = SkinColorHelper.GetRandom(seed);
                    float light = tone.color.r * 0.30f + tone.color.g * 0.59f + tone.color.b * 0.11f;
                    if (light > high)
                    {
                        high = light;
                        _lightSkin = tone;
                    }
                    if (light < low)
                    {
                        low = light;
                        _darkSkin = tone;
                    }
                }
            }
            bool man = Random.value < 0.8f;
            appearance.data.ageInDays = 33 * 365;
            appearance.RandomizeElements(man ? Gender.Male : Gender.Female, Random.value < 0.5f ? _lightSkin : _darkSkin, CasualWear, man ? ManSeed : WomanSeed);
            appearance.data.strength = Strong;   // fit: the game rolls both of these 0..1
            appearance.data.fatness = Lean;
            int bit = man ? 1 : 2;
            bool log = (_serviceLogged & bit) == 0;
            Sizes.Clear();
            string hair = Choose(appearance, AppearanceElementType.Hair, man ? ManHair : WomanHair, darkest: true, bare: false, out bool hairNamed, log ? Sizes : null);
            string beard = man ? Choose(appearance, AppearanceElementType.Beard, NoBeard, darkest: false, bare: true, out _, null) : null;
            if (log)
            {
                _serviceLogged |= bit;
                Logger?.Info("People: every " + (man ? "male" : "female") + " officer and paramedic has the same look (seed " + (man ? ManSeed : WomanSeed)
                             + ", lightest or darkest skin tone, build: fatness " + Lean.ToString("F2") + " (lean), strength " + Strong.ToString("F1") + "), haircut "
                             + (hair == null ? "as rolled (the game's styles could not be read)"
                                 : "'" + hair + "' (" + (hairNamed ? "by name" : "the one with the least hair: no style has a name that says it is short") + "), its darkest colour")
                             + "; the game's styles and their sizes in litres: " + Sizes
                             + (man ? "; beard " + (beard != null ? "'" + beard + "'" : "as rolled (no empty choice found)") : string.Empty) + ".");
            }
            Sizes.Clear();
        }

        // Puts on the first variant of a slot whose name contains one of the words, in the order of the words;
        // failing that, the variant with the least to it (the smallest mesh; with "bare", one with no mesh at
        // all counts as the least). Null if the slot has nothing to choose from.
        private static string Choose(AppearanceSetter appearance, AppearanceElementType slot, string[] words, bool darkest, bool bare, out bool named, System.Text.StringBuilder sizes)
        {
            named = false;
            List<AppearanceElementVariant> variants = appearance.GetElementVariants(slot);
            AppearanceElementVariant picked = null;
            for (int w = 0; variants != null && w < words.Length && picked == null; w++)
            {
                for (int i = 0; i < variants.Count && picked == null; i++)
                {
                    if (variants[i] != null && variants[i].name != null && variants[i].name.IndexOf(words[w], StringComparison.OrdinalIgnoreCase) >= 0)
                    {
                        picked = variants[i];
                        named = true;
                    }
                }
            }
            if (picked == null || sizes != null)
            {
                AppearanceElementVariant least = Least(variants, bare, sizes);
                if (picked == null)
                {
                    picked = least;
                }
            }
            if (picked == null)
            {
                return null;
            }
            AppearanceElementData element = null;
            List<AppearanceElementData> elements = appearance.data.elements;
            for (int i = 0; i < elements.Count; i++)
            {
                if (elements[i].type == slot)
                {
                    element = elements[i];
                }
            }
            if (element == null)
            {
                element = new AppearanceElementData { type = slot };
                elements.Add(element);
            }
            element.variantId = picked.id;
            AppearanceElementColor colour = null;
            float lightest = float.MaxValue;
            for (int i = 0; picked.colors != null && i < picked.colors.Length; i++)
            {
                AppearanceElementColor entry = picked.colors[i];
                Color shade = entry.hasBaseColor ? entry.baseColor : entry.r;
                float light = darkest ? shade.r + shade.g + shade.b : i;
                if (light < lightest)
                {
                    lightest = light;
                    colour = entry;
                }
            }
            element.colorId = colour != null ? colour.id : null;
            return picked.name;
        }

        // The variant whose mesh takes up the least room (the box round it, which the engine keeps for every mesh):
        // of haircuts, the shortest. One without a mesh is nothing at all, and is only taken if that is wanted.
        private static AppearanceElementVariant Least(List<AppearanceElementVariant> variants, bool bare, System.Text.StringBuilder sizes)
        {
            AppearanceElementVariant least = null;
            float room = float.MaxValue;
            for (int i = 0; variants != null && i < variants.Count; i++)
            {
                AppearanceElementVariant variant = variants[i];
                if (variant == null)
                {
                    continue;
                }
                var skin = variant.GetComponent<SkinnedMeshRenderer>();
                Mesh mesh = skin != null ? skin.sharedMesh : null;
                bool drawn = mesh != null && mesh.vertexCount > 0;
                Vector3 box = drawn ? Vector3.Scale(mesh.bounds.size, variant.transform.lossyScale) : Vector3.zero;
                float volume = Mathf.Abs(box.x * box.y * box.z);
                if (sizes != null)
                {
                    sizes.Append(sizes.Length > 0 ? ", " : string.Empty).Append(variant.name).Append(' ').Append(drawn ? (volume * 1000f).ToString("F1") : "(no mesh)");
                }
                if ((drawn || bare) && volume < room)
                {
                    room = volume;
                    least = variant;
                }
            }
            return least;
        }

        // The equipment worn with the look (none for a civilian). An extra: if it cannot be fitted the
        // character goes without, and it is said once. False if there is none.
        private static bool Equip(StreetActor actor, ActorLook look, bool cap)
        {
            if (actor.Kit == null && !Uniforms.Has(look))
            {
                return false;
            }
            // Fitted at the size the game built the body, whatever size it stands at: the pieces hang on the bones
            // and grow with them, and every measure the kit knows is of a body that size.
            float size = actor.Size;
            actor.Resize(1f);
            try
            {
                if (actor.Kit == null)
                {
                    actor.Kit = new ServiceKit();
                }
                actor.Kit.Fit(actor.Character, look, cap);
                actor.Resize(size);
                return true;
            }
            catch (Exception e)
            {
                if (!_kitFailed)
                {
                    _kitFailed = true;
                    Logger?.Error(e);
                    Logger?.Warn("People: uniform equipment could not be fitted (see the error above); that character goes without. (Logged once.)");
                }
                actor.Resize(size);
                try
                {
                    actor.Kit.Remove();
                }
                catch (Exception)
                {
                    // nothing more to be done for it
                }
                return false;
            }
        }

        private static bool _kitFailed;
    }
}
