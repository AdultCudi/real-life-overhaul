using System;
using System.Collections;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using BigAmbitions.SoundSystem;
using Character;
using Characters;
using Characters.EmojiSystem;
using Helpers;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The people a job puts on the street: a passenger at the kerb, a customer at a door.
    /// Two characters at most, the jobs' own (never the accident pool's, which scenes must be
    /// able to count on), built the first time a job needs somebody and reused after that.
    /// One costs what one of the game's pedestrians costs while drawn and nothing on the shelf.
    /// A person who has been seen gets a new look while shelved, a moment after leaving, so the
    /// next fare is not the same face. Nothing here runs by itself: the director calls
    /// <see cref="Cull"/> a few times a second while somebody is out.
    /// </summary>
    public sealed class JobCast
    {
        public const int Size = 2;

        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;
        private const float HideBehind = 6f;

        private static readonly AppearanceTag[] CasualWear = { AppearanceTag.Casual };

        private readonly StreetActor[] _people = new StreetActor[Size];
        private readonly bool[] _out = new bool[Size];
        private readonly bool[] _stale = new bool[Size];
        private readonly bool[] _fixed = new bool[Size];    // wearing a seeded look: rolled anew for whoever is put out next
        private readonly bool[] _seated = new bool[Size];   // riding in a car: off the street, still the same person, still held
        private readonly MonoBehaviour _host;
        private readonly IModLogger _logger;
        private Coroutine _refresh;
        private bool _failed;

        public JobCast(MonoBehaviour host, IModLogger logger)
        {
            _host = host;
            _logger = logger;
        }

        /// <summary>
        /// Behind the loading screen: the people are made and dressed now and put on the shelf, so that the first job,
        /// lesson or test that needs somebody does not make one (a character and a mesh merge) in the middle of play.
        /// A fault leaves them to be made on first need, as before.
        /// </summary>
        public bool Prewarm(int people)
        {
            if (_failed || _host == null)
            {
                return true;
            }
            try
            {
                for (int i = 0; i < Size && i < people; i++)
                {
                    if (_people[i] == null || !_people[i].IsValid)
                    {
                        _people[i] = Create(_host.transform.position);
                        Shelve(_people[i]);
                    }
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            return true;
        }

        /// <summary>True while somebody is out on the street.</summary>
        public bool AnyOut => (_out[0] && !_seated[0]) || (_out[1] && !_seated[1]);

        /// <summary>
        /// Puts a person on the ground at a point that was already found on the navigation mesh.
        /// Null if both are out, the point cannot be stood on, or characters cannot be made.
        /// </summary>
        /// <param name="onRoad">May step onto the road surface (to reach a car door), not only pavements.</param>
        public StreetActor Put(Vector3 spot, bool onRoad)
        {
            return Put(spot, onRoad, 0, false, false);
        }

        /// <summary>
        /// The same for somebody who is the same person every time (an employer's boss, a colleague): with a seed
        /// other than zero their age, build, face, hair and clothes are rolled from it, in formal wear, and nothing
        /// of the game's own random sequence is used up. Formal wear for a boss, everyday clothes otherwise.
        /// </summary>
        public StreetActor Put(Vector3 spot, bool onRoad, int seed, bool female, bool formal = true)
        {
            if (_failed)
            {
                return null;
            }
            int slot = !_out[0] ? 0 : !_out[1] ? 1 : -1;
            if (slot < 0)
            {
                return null;
            }
            try
            {
                StreetActor person = _people[slot];
                if (person == null || !person.IsValid)
                {
                    person = Create(spot, seed, female, formal);
                    _people[slot] = person;
                }
                else if (_stale[slot] || seed != 0 || _fixed[slot])
                {
                    person.Character.gameObject.SetActive(true);
                    Dress(person, seed, female, formal);
                }
                _stale[slot] = false;
                _fixed[slot] = seed != 0;
                person.Character.gameObject.SetActive(true);
                if (!person.Place(spot, onRoad))
                {
                    Shelve(person);
                    return null;
                }
                person.InUse = true;
                person.Lease++;
                _out[slot] = true;
                return person;
            }
            catch (Exception e)
            {
                _failed = true;
                _logger?.Error(e);
                _logger?.Warn("Jobs: characters could not be made (see the error above); jobs that need a person are off for this session.");
                return null;
            }
        }

        /// <summary>
        /// Someone gets into a car: they are off the street but stay who they are, and stay held,
        /// until <see cref="Unseat"/> puts them back or <see cref="Release"/> lets them go.
        /// </summary>
        public void Seat(StreetActor person)
        {
            for (int i = 0; i < Size; i++)
            {
                if (person != null && _people[i] == person && _out[i] && !_seated[i])
                {
                    _seated[i] = true;
                    Shelve(person);
                }
            }
        }

        /// <summary>Out of the car again at a point on the navigation mesh. False (and still seated) if they cannot stand there.</summary>
        public bool Unseat(StreetActor person, Vector3 spot, bool onRoad)
        {
            for (int i = 0; i < Size; i++)
            {
                if (person == null || _people[i] != person || !_out[i] || !_seated[i] || !person.IsValid)
                {
                    continue;
                }
                person.Character.gameObject.SetActive(true);
                if (!person.Place(spot, onRoad))
                {
                    person.Character.gameObject.SetActive(false);
                    return false;
                }
                _seated[i] = false;
                return true;
            }
            return false;
        }

        /// <summary>Takes a person off the street. Safe with null and with someone already taken in.</summary>
        public void Release(StreetActor person)
        {
            for (int i = 0; i < Size; i++)
            {
                if (person == null || _people[i] != person || !_out[i])
                {
                    continue;
                }
                _out[i] = false;
                _seated[i] = false;
                _stale[i] = true;
                person.InUse = false;
                Shelve(person);
                if (_refresh == null && _host != null && _host.isActiveAndEnabled)
                {
                    _refresh = _host.StartCoroutine(Refresh());
                }
            }
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < Size; i++)
            {
                Release(_people[i]);
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
                StreetActor person = _people[i];
                if (!_out[i] || _seated[i] || person == null || !person.IsValid)
                {
                    continue;
                }
                Vector3 offset = person.Position - eye.position;
                person.SetVisible(offset.sqrMagnitude < HideDistance * HideDistance && Vector3.Dot(offset, eye.forward) > -HideBehind);
            }
        }

        /// <summary>A small emoji over someone's head for a few seconds; skipped for people who are not being drawn.</summary>
        public void Emoji(StreetActor person, CharacterEmojiName emoji, float seconds)
        {
            if (person == null || !person.IsValid)
            {
                return;
            }
            Emoji(person.Character, emoji, seconds);
        }

        /// <summary>The same for any street character, such as one of the game's own pedestrians.</summary>
        public void Emoji(BaseHuman character, CharacterEmojiName emoji, float seconds)
        {
            try
            {
                if (character == null || (character is ThirdPersonCharacter full && !full.visible) || character.head == null || _host == null || !_host.isActiveAndEnabled
                    || GameManager.GetMainCamera() == null || CharacterEmojiSystem.GetCharacterEmojiByName(emoji) == null)
                {
                    return;
                }
                _host.StartCoroutine(CharacterEmojiSystem.ShowEmoji(character.head, emoji, false, seconds));
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>The sound of someone getting into or out of a car; the game has no animation for either.</summary>
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
                StreetActor person = _people[i];
                _people[i] = null;
                _out[i] = false;
                _seated[i] = false;
                try
                {
                    if (person != null && person.IsValid)
                    {
                        person.InUse = false;
                        person.ResetForReuse();
                        UnityEngine.Object.Destroy(person.Character.gameObject);
                    }
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
            }
        }

        private StreetActor Create(Vector3 position, int seed = 0, bool female = false, bool formal = true)
        {
            SessionWatch.Mark("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, position, Quaternion.identity, _host.transform);
            character.name = "RealLifeOverhaul Job Person";
            // Idle fidgets would play over what a job asks of them (waving, taking a parcel).
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
            var person = new StreetActor(character) { Look = ActorLook.Civilian };
            Dress(person, seed, female, formal);
            _logger?.Info("Jobs: a character was created for job scenes (two at most).");
            return person;
        }

        // Who they are is rolled anew: age, build, face, hair, casual clothes. One mesh merge.
        private static void Dress(StreetActor person)
        {
            AppearanceSetter appearance = person.Character.appearanceSetter;
            appearance.SetRandomAge();
            Gender gender = Random.value < 0.5f ? Gender.Male : Gender.Female;
            appearance.RandomizeElements(gender, SkinColorHelper.GetRandom(), CasualWear);
            Looks.Count("jobs: a character's look being built (a mesh merge)");
            appearance.UpdateVisuals();
            Looks.Done();
        }

        private static readonly AppearanceTag[] FormalWear = { AppearanceTag.Formal };

        // The same person every time: everything rolled from the seed, the game's own random sequence left as it was.
        private static void Dress(StreetActor person, int seed, bool female, bool formal)
        {
            if (seed == 0)
            {
                Dress(person);
                return;
            }
            AppearanceSetter appearance = person.Character.appearanceSetter;
            Random.State kept = Random.state;
            try
            {
                Random.InitState(seed);
                appearance.SetRandomAge();
                appearance.RandomizeElements(female ? Gender.Female : Gender.Male, SkinColorHelper.GetRandom(seed), formal ? FormalWear : CasualWear, seed);
            }
            finally
            {
                Random.state = kept;
            }
            Looks.Count("jobs: a character's look being built (a mesh merge)");
            appearance.UpdateVisuals();
            Looks.Done();
        }

        private void Shelve(StreetActor person)
        {
            try
            {
                if (person.IsValid)
                {
                    ActivityAnimators.Forget(person.Character.animator);   // a clip of the mod's that was still playing ends with them
                    person.ResetForReuse();
                    person.Character.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // A new look for whoever left the street, one at a time, while hidden and nothing is going on.
        private IEnumerator Refresh()
        {
            var wait = new WaitForSeconds(0.75f);
            yield return wait;
            for (int i = 0; i < Size; i++)
            {
                StreetActor person = _people[i];
                if (!_stale[i] || _out[i] || person == null || !person.IsValid)
                {
                    continue;
                }
                try
                {
                    GameObject body = person.Character.gameObject;
                    body.SetActive(true);
                    Dress(person);
                    body.SetActive(false);
                }
                catch (Exception e)
                {
                    _logger?.Error(e);
                }
                _stale[i] = false;
                yield return wait;
            }
            _refresh = null;
        }
    }
}
