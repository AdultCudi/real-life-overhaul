using System;
using System.Collections;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.SoundSystem;
using Characters;
using Characters.EmojiSystem;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The mod's people: twelve street characters shared by every scene, made once and handed out again in the same outfit,
    /// as building a look is the costly part (Looks). Four may be out only to be seen; they give way the instant a scene
    /// needs somebody. Cost: a handful of comparisons a frame while somebody is out, hiding whoever cannot be seen.
    /// </summary>
    public partial class ActorPool : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("people", "ActorPool", typeof(ActorPool));

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

        /// <summary>How many more people a scene could put out now; whoever is only there to be seen counts as free.</summary>
        public int Free => MaxActors - Count(yielding: false);


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
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
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

        /// <summary>Puts a person on the ground near a point (the road too, with <paramref name="onRoad"/>); null if nowhere or nobody.</summary>
        public StreetActor Spawn(Vector3 near, ActorLook look, float radius = 6f, bool onRoad = false)
        {
            return Put(near, look, radius, onRoad, yields: false);
        }

        /// <summary>Puts out someone only there to be seen, whom a scene may take back at any moment; null if none may be spared.</summary>
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
                actor.Uses++;
                actor.Stale = Looks.Worn(actor.Uses, actor.DressedAt);
            }
            Tend();
        }

        /// <summary>Called once behind the loading screen: the civilians are made now; the uniformed wait on the shelf for the modelled props.</summary>
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
                Logger?.Error(e);   // they are made on first need instead
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

        // Runs only while the shelf has something to see to: one look at a time, when Looks says one may be built.
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

        /// <summary>An emoji bubble over someone's head for a few seconds; skipped for anybody not drawn.</summary>
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
                    // Fitted out before the modelled props loaded: fitted again once, an officer from scratch as the cap replaces the hat.
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
                spare.Character.gameObject.SetActive(true);
                Dress(spare, look);
            }
            return spare;
        }

        // A new character, not yet dressed, born on the pavement where it can be: the prefab's pavement agent switches on as it
        // is created, and off its mesh the engine warns. Place moves it to where it is wanted in the same frame.
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
            // Left to physics, the body is shoved up and over whatever it brushes against between two cars, and stays there.
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;
            }
            var created = new StreetActor(character);
            _actors.Add(created);
            return created;
        }
    }
}
