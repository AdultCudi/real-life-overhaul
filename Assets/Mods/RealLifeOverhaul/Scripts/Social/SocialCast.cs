using System;
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

namespace RealLifeOverhaul
{
    /// <summary>
    /// The people of the player's life when they are on the street: two characters at most, the
    /// social life's own (never the accident pool's, the living city's or the jobs'), made the
    /// first time somebody is needed and reused after that. A character wears the look of the
    /// person it stands for: built by the game's own appearance system from that person's seed,
    /// so Dana looks like Dana every time, and rebuilt only when the body is needed for somebody
    /// else. One costs what one of the game's pedestrians costs while drawn and nothing on the
    /// shelf. Nothing here runs by itself: the director calls <see cref="Cull"/> on its tick
    /// while somebody is out.
    /// </summary>
    public sealed class SocialCast
    {
        public const int Size = 2;

        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;
        private const float HideBehind = 6f;
        private const float MaxBuild = 0.55f;   // the clips are made for slim to average (see CityPeople)

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };

        private readonly CityPerson[] _people = new CityPerson[Size];
        private readonly int[] _wears = { -1, -1 };   // whose look each body has
        private readonly bool[] _out = new bool[Size];
        private readonly bool[] _seated = new bool[Size];
        private readonly MonoBehaviour _host;
        private readonly CityProps _props;
        private readonly IModLogger _logger;
        private bool _failed;

        public SocialCast(MonoBehaviour host, CityProps props, IModLogger logger)
        {
            _host = host;
            _props = props;
            _logger = logger;
        }

        /// <summary>True while somebody is on the street (not riding in the car).</summary>
        public bool AnyOut => (_out[0] && !_seated[0]) || (_out[1] && !_seated[1]);

        /// <summary>True if a body is free for one more person.</summary>
        public bool HasRoom => !_failed && (!_out[0] || !_out[1]);

        /// <summary>
        /// Puts a person of the cast on the pavement at a spot that was found and tested, facing a
        /// point, standing. Null if both bodies are out, the spot will not take them or characters
        /// cannot be made; callers cope with that.
        /// </summary>
        public CityPerson Put(SocialPerson who, Vector3 spot, Vector3 face)
        {
            if (_failed || who == null)
            {
                return null;
            }
            int slot = -1;
            for (int i = 0; i < Size; i++)
            {
                if (!_out[i] && (slot < 0 || _wears[i] == who.Index))
                {
                    slot = i;   // the body that already looks like them, if it is free
                }
            }
            if (slot < 0)
            {
                return null;
            }
            try
            {
                CityPerson person = _people[slot];
                if (person == null || !person.Valid)
                {
                    person = Create(spot);
                    _people[slot] = person;
                    _wears[slot] = -1;
                }
                person.Character.gameObject.SetActive(true);
                person.Reset(_props);
                if (_wears[slot] != who.Index)
                {
                    Dress(person, who);
                    _wears[slot] = who.Index;
                }
                person.InUse = true;
                person.Actor.InUse = true;
                if (!person.Place(spot, face))
                {
                    Shelve(slot);
                    return null;
                }
                _out[slot] = true;
                _seated[slot] = false;
                return person;
            }
            catch (Exception e)
            {
                _failed = true;
                _logger?.Error(e);
                _logger?.Warn("Social: characters could not be made (see the error above); nobody is put on the street for this session.");
                return null;
            }
        }

        /// <summary>
        /// Puts somebody who is already out at another tested spot at once, standing, facing a
        /// point. Only ever for somebody nobody is looking at (they took too long, or their way
        /// ended short). False, and they stay where they were, if the spot will not take them.
        /// </summary>
        public bool Move(CityPerson person, Vector3 spot, Vector3 face)
        {
            int slot = SlotOf(person);
            if (slot < 0 || _seated[slot] || person.Lost)
            {
                return false;
            }
            NavMeshAgent agent = person.Character.navmeshAgent;
            Vector3 before = person.Position;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }
            if (!agent.Warp(spot) || !agent.isOnNavMesh)
            {
                agent.Warp(before);
                return false;
            }
            person.Idle(0.1f);
            person.Turn(face);
            person.Stand();
            return true;
        }

        /// <summary>
        /// The walk somebody's own navigation agent would make from where they stand to a place:
        /// how long, and whether it gets the whole way there. False if there is no route at all.
        /// Asked when somebody is put out; nothing is changed.
        /// </summary>
        public static bool Walk(CityPerson person, Vector3 to, out float length, out bool whole)
        {
            length = 0f;
            whole = false;
            if (person == null || person.Lost)
            {
                return false;
            }
            NavMeshAgent agent = person.Character.navmeshAgent;
            if (!agent.enabled || !agent.isOnNavMesh)
            {
                return false;
            }
            if (_path == null)
            {
                _path = new NavMeshPath();
            }
            if (!agent.CalculatePath(to, _path) || _path.status == NavMeshPathStatus.PathInvalid)
            {
                return false;
            }
            whole = _path.status == NavMeshPathStatus.PathComplete;
            length = SocialPlaces.Length(_path);
            return true;
        }

        private static NavMeshPath _path;

        /// <summary>Lets somebody walk on the carriageway as well as the pavement: to reach a car door.</summary>
        public static void OnRoad(CityPerson person)
        {
            if (person == null || person.Lost)
            {
                return;
            }
            NavMeshAgent agent = person.Character.navmeshAgent;
            if (agent.enabled && agent.agentTypeID != NavMeshHelper.PlayerAgentTypeId)
            {
                Vector3 here = person.Position;
                agent.agentTypeID = NavMeshHelper.PlayerAgentTypeId;
                agent.Warp(here);
            }
        }

        /// <summary>Somebody gets into the car: off the street, still the same person, still held.</summary>
        public void Seat(CityPerson person)
        {
            int slot = SlotOf(person);
            if (slot < 0 || _seated[slot])
            {
                return;
            }
            _seated[slot] = true;
            try
            {
                person.Reset(_props);
                person.Character.gameObject.SetActive(false);
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Out of the car at a point beside it. False (and still seated) if they cannot stand there.</summary>
        public bool Unseat(CityPerson person, Vector3 spot, Vector3 face)
        {
            int slot = SlotOf(person);
            if (slot < 0 || !_seated[slot] || !person.Valid)
            {
                return false;
            }
            person.Character.gameObject.SetActive(true);
            NavMeshAgent agent = person.Character.navmeshAgent;
            agent.agentTypeID = NavMeshHelper.PlayerAgentTypeId;
            if (!agent.enabled)
            {
                person.Character.transform.position = spot;
            }
            agent.enabled = true;
            person.Character.capsuleCollider.enabled = true;
            if (!agent.Warp(spot) || !agent.isOnNavMesh)
            {
                person.Character.gameObject.SetActive(false);
                return false;
            }
            person.Character.LookTarget = Vector3.zero;
            person.Character.SetWalkingSpeed(ThirdPersonCharacter.WalkingSpeed.Walk);
            person.Turn(face);
            person.Stand();
            _seated[slot] = false;
            return true;
        }

        public bool Seated(CityPerson person)
        {
            int slot = SlotOf(person);
            return slot >= 0 && _seated[slot];
        }

        /// <summary>Takes a person off the street. Safe with null and with someone already taken in.</summary>
        public void Release(CityPerson person)
        {
            int slot = SlotOf(person);
            if (slot >= 0)
            {
                Shelve(slot);
            }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < Size; i++)
            {
                if (_out[i])
                {
                    Shelve(i);
                }
            }
        }

        /// <summary>Animation and rendering off for whoever is far away or behind the camera. A few comparisons.</summary>
        public void Cull()
        {
            Camera camera = GameManager.GetMainCamera();
            if (camera == null)
            {
                return;
            }
            Transform eye = camera.transform;
            for (int i = 0; i < Size; i++)
            {
                CityPerson person = _people[i];
                if (!_out[i] || _seated[i] || person == null || person.Lost)
                {
                    continue;
                }
                Vector3 offset = person.Position - eye.position;
                person.SetVisible(offset.sqrMagnitude < HideDistance * HideDistance && Vector3.Dot(offset, eye.forward) > -HideBehind);
            }
        }

        /// <summary>A small emoji over someone's head for a few seconds; skipped for people who are not being drawn.</summary>
        public void Emoji(CityPerson person, CharacterEmojiName emoji, float seconds)
        {
            try
            {
                if (person == null || person.Lost || !person.Character.visible || person.Character.head == null || _host == null || !_host.isActiveAndEnabled
                    || GameManager.GetMainCamera() == null || CharacterEmojiSystem.GetCharacterEmojiByName(emoji) == null)
                {
                    return;
                }
                _host.StartCoroutine(CharacterEmojiSystem.ShowEmoji(person.Character.head, emoji, false, seconds));
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>The sound of someone getting into or out of a car.</summary>
        public static void DoorSound(Vector3 position)
        {
            SfxManager sounds = InstanceBehavior<SfxManager>.Instance;
            if (sounds != null)
            {
                sounds.PlayAudio(SoundType.CarDoorOpenClose, position);
            }
        }

        /// <summary>The city is going: destroy what was made.</summary>
        public void Dispose()
        {
            for (int i = 0; i < Size; i++)
            {
                CityPerson person = _people[i];
                _people[i] = null;
                _out[i] = false;
                _seated[i] = false;
                _wears[i] = -1;
                try
                {
                    if (person != null && person.Valid)
                    {
                        person.InUse = false;
                        person.Actor.InUse = false;
                        ActivityAnimators.Forget(person.Character.animator);
                        if (!RagdollSystem.Holds(person.Character))
                        {
                            UnityEngine.Object.Destroy(person.Character.gameObject);
                        }
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
        }

        private int SlotOf(CityPerson person)
        {
            for (int i = 0; i < Size; i++)
            {
                if (person != null && _people[i] == person && _out[i])
                {
                    return i;
                }
            }
            return -1;
        }

        private void Shelve(int slot)
        {
            CityPerson person = _people[slot];
            _out[slot] = false;
            _seated[slot] = false;
            if (person == null)
            {
                return;
            }
            bool taken = person.Valid && RagdollSystem.Holds(person.Character);
            person.InUse = false;
            if (taken)
            {
                // Knocked down: the casualty system has the body and puts it away itself. It is not ours again.
                _people[slot] = null;
                _wears[slot] = -1;
                return;
            }
            person.Actor.InUse = false;
            try
            {
                if (person.Valid)
                {
                    person.Reset(_props);
                    person.Character.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        private CityPerson Create(Vector3 position)
        {
            using var piece = SessionWatch.Piece("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, position, Quaternion.identity, _host.transform);
            character.name = "RealLifeOverhaul Friend";
            // Idle fidgets would play over what a scene asks of them (a wave, a cup).
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;
            }
            // They go where their route takes them; left to physics a body is shoved about by whatever it brushes.
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;
            }
            _logger?.Info("Social: a character was created for the people in the player's life (two at most).");
            return new CityPerson(character);
        }

        // The look is the game's own, from the person's seed: the same face, hair and clothes every time. One mesh merge.
        private static void Dress(CityPerson person, SocialPerson who)
        {
            person.Look = CityLook.Civilian;
            AppearanceSetter appearance = person.Character.appearanceSetter;
            CityOutfits.Restore(person.Dyes);
            appearance.data.ageInDays = Mathf.Clamp(who.Age, 18, 85) * SocialPlaces.DaysPerYear;
            appearance.RandomizeElements(who.Female ? Gender.Female : Gender.Male, SkinColorHelper.GetRandom(who.Seed), CasualWear, who.Seed);
            appearance.data.fatness = Mathf.Min(appearance.data.fatness, MaxBuild);
            Looks.Count("social: a friend's look being built (a mesh merge)");
            appearance.UpdateVisuals();
            Looks.Done();
        }
    }
}
