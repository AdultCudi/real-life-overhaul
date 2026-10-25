using System;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character;
using Characters;
using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Roommates
{
    /// <summary>
    /// The roommate as one character, ever, kept on the shelf switched off so the mesh merge of a look is paid once; dressed
    /// from the seed in the save. Household staff have a cast of their own of the same kind. While they are out the director
    /// calls <see cref="Tick"/> each frame, which allocates nothing.
    /// </summary>
    public sealed class RoommateCast
    {
        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;   // metres from the camera
        private const float HideBehind = 6f;       // metres behind the camera
        private const float TurnRate = 240f;       // degrees a second
        private const float MaxBuild = 0.55f;      // the mod's clips are made for slim to average

        private static readonly AppearanceTag[] Casual = { AppearanceTag.Casual };

        private readonly Transform _parent;
        private readonly IModLogger _logger;
        private readonly string _objectName;   // the character's object in the scene
        private readonly string _owner;        // whose character it is, as the log names the feature
        private CityPerson _person;
        private long _worn;            // which look the character on the shelf has
        private Quaternion _turnTo;
        private bool _turning;

        public RoommateCast(Transform parent, IModLogger logger, string objectName = "RealLifeOverhaul Roommate", string owner = "Roommates")
        {
            _parent = parent;
            _logger = logger;
            _objectName = objectName;
            _owner = owner;
            Props = new CityProps(parent);
        }

        public CityProps Props { get; }

        public bool Out { get; private set; }

        /// <summary>Characters could not be made this session: the roommate is not shown, everything else goes on.</summary>
        public bool Failed { get; private set; }

        /// <summary>Puts them, standing, at a spot on a navigation mesh. Null if they are out already, the spot will not take them, or no character can be made.</summary>
        public CityPerson Put(SocialPerson who, Vector3 spot, Vector3 face, int agentType)
        {
            if (Failed || Out || who == null)
            {
                return null;
            }
            try
            {
                CityPerson person = _person;
                if (person == null || !person.Valid)
                {
                    person = Create(spot);
                    _person = person;
                    _worn = 0;
                }
                person.Character.gameObject.SetActive(true);
                person.Reset(Props);
                long wanted = ((long)who.Seed << 1) | (who.Female ? 1L : 0L);
                if (_worn != wanted)
                {
                    Dress(person, who);
                    _worn = wanted;
                }
                person.InUse = true;
                person.Actor.InUse = true;
                if (!Place(person, spot, face, agentType))
                {
                    Release();
                    return null;
                }
                Out = true;
                _turning = false;
                return person;
            }
            catch (Exception e)
            {
                Failed = true;
                _logger?.Error(e);
                _logger?.Warn(_owner + ": a character could not be made (see the error above); they are not shown this session. Everything else goes on.");
                return null;
            }
        }

        /// <summary>Back on the shelf, standing, with empty hands, off whatever seat they were on. Safe at any time.</summary>
        public void Release()
        {
            Out = false;
            _turning = false;
            CityPerson person = _person;
            if (person == null)
            {
                return;
            }
            try
            {
                person.InUse = true;   // Reset is refused for somebody already marked as put away
                if (person.Valid && !RagdollSystem.Holds(person.Character))
                {
                    person.Reset(Props);
                    person.Character.gameObject.SetActive(false);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
            person.InUse = false;
            person.Actor.InUse = false;
        }

        public void Face(Vector3 point)
        {
            if (!Out || _person == null || _person.Lost)
            {
                return;
            }
            Vector3 flat = point - _person.Position;
            flat.y = 0f;
            if (flat.sqrMagnitude > 0.01f)
            {
                _turnTo = Quaternion.LookRotation(flat);
                _turning = true;
            }
        }

        public void Tick(float delta)
        {
            CityPerson person = _person;
            if (!Out || person == null)
            {
                return;
            }
            if (person.Lost)
            {
                Out = false;   // destroyed with the scene, or taken by the casualty system
                _turning = false;
                return;
            }
            if (_turning)
            {
                Transform body = person.Character.transform;
                body.rotation = Quaternion.RotateTowards(body.rotation, _turnTo, TurnRate * delta);
                if (Quaternion.Angle(body.rotation, _turnTo) < 0.5f)
                {
                    _turning = false;
                }
            }
            Camera camera = GameManager.GetMainCamera();
            if (camera != null)
            {
                Transform eye = camera.transform;
                Vector3 offset = person.Position - eye.position;
                person.SetVisible(offset.sqrMagnitude < HideDistance * HideDistance && Vector3.Dot(offset, eye.forward) > -HideBehind);
            }
        }

        public void Dispose()
        {
            CityPerson person = _person;
            _person = null;
            Out = false;
            try
            {
                if (person != null && person.Valid)
                {
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

        private CityPerson Create(Vector3 near)
        {
            using var piece = SessionWatch.Piece("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, near, Quaternion.identity, _parent);
            character.name = _objectName;
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;   // idle fidgets would play over what they are asked to do
            }
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;   // they go where their route takes them
            }
            _logger?.Info(_owner + ": a character was created (one at most).");
            return new CityPerson(character);
        }

        private static bool Place(CityPerson person, Vector3 spot, Vector3 face, int agentType)
        {
            ThirdPersonCharacter character = person.Character;
            NavMeshAgent agent = character.navmeshAgent;
            agent.agentTypeID = agentType;
            if (!agent.enabled)
            {
                character.transform.position = spot;   // there first, then the agent: switched on off the mesh the engine complains
            }
            agent.enabled = true;
            character.capsuleCollider.enabled = true;
            if (!agent.Warp(spot) || !agent.isOnNavMesh)
            {
                return false;
            }
            character.LookTarget = Vector3.zero;
            character.SetWalkingSpeed(ThirdPersonCharacter.WalkingSpeed.Walk);
            person.Turn(face);
            person.Stand();
            return true;
        }

        // One mesh merge: the face, hair, build and day clothes all come from the seed.
        private static void Dress(CityPerson person, SocialPerson who)
        {
            AppearanceSetter appearance = person.Character.appearanceSetter;
            int year = GameCalendar.DaysPerYear;
            int seed = who.Seed != 0 ? who.Seed : 1;
            // The game colours a beard to match the hair by a roll of the engine's own dice: the same roll every time for the same seed.
            Random.State dice = Random.state;
            Random.InitState(seed);
            try
            {
                appearance.data.ageInDays = Mathf.Clamp(who.Age, 18, 80) * year;
                if (!PersonLookGame.Dress(appearance, seed))   // (somebody met in the street: the look they had there, People/PersonLook)
                {
                    appearance.RandomizeElements(who.Female ? Gender.Female : Gender.Male, SkinColorHelper.GetRandom(seed), Casual, seed);
                }
                appearance.data.fatness = Mathf.Min(appearance.data.fatness, MaxBuild);
                Looks.Count("roommates: the roommate's look being built (a mesh merge)");
                appearance.UpdateVisuals();
            }
            finally
            {
                Random.state = dice;
            }
        }
    }
}
