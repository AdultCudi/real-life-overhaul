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
    /// The roommate as a character: ONE, ever, made the first time they are seen at home, kept on the shelf switched
    /// off, and brought out again in the same look, so the expensive part (the game merges a character's meshes when
    /// its look is built) is paid once per roommate. The look is built from the seed in the save: the same face in
    /// every session. Made exactly as family life makes the partner (Family/FamilyCast): the living city's kind of
    /// person (<see cref="CityPerson"/>: they only move by their navigation agent, clips never slide them), on
    /// whichever navigation mesh the home has.
    ///
    /// Nothing here runs by itself. While the roommate is out, the director calls <see cref="Tick"/> each frame: one
    /// turn towards a point while they are turning, one comparison of distance and side for hiding them. Nothing is
    /// allocated then.
    /// </summary>
    public sealed class RoommateCast
    {
        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;
        private const float HideBehind = 6f;
        private const float TurnRate = 240f;       // degrees a second
        private const float MaxBuild = 0.55f;      // the mod's clips are made for slim to average

        private static readonly AppearanceTag[] Casual = { AppearanceTag.Casual };

        private readonly Transform _parent;
        private readonly IModLogger _logger;
        private CityPerson _person;
        private long _worn;            // which look the character on the shelf has
        private Quaternion _turnTo;
        private bool _turning;

        public RoommateCast(Transform parent, IModLogger logger)
        {
            _parent = parent;
            _logger = logger;
            Props = new CityProps(parent);
        }

        /// <summary>The props they hold (a cup, the newspaper).</summary>
        public CityProps Props { get; }

        /// <summary>True while they are out.</summary>
        public bool Out { get; private set; }

        /// <summary>Characters could not be made this session: the roommate is not shown, everything else goes on.</summary>
        public bool Failed { get; private set; }

        /// <summary>
        /// Puts them at a spot that was found on a navigation mesh, facing a point, standing. Null if they are out
        /// already, the spot will not take them, or a character cannot be made (said once in the log).
        /// </summary>
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
                _logger?.Warn("Roommates: a character could not be made (see the error above); the roommate is not shown this session. Everything else goes on.");
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

        /// <summary>They turn to face a point over a moment.</summary>
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

        /// <summary>Called by the director each frame while they are out: a few comparisons.</summary>
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

        /// <summary>The city is going: destroy what was made.</summary>
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
            SessionWatch.Mark("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, near, Quaternion.identity, _parent);
            character.name = "RealLifeOverhaul Roommate";
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;   // idle fidgets would play over what they are asked to do
            }
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;   // they go where their route takes them
            }
            _logger?.Info("Roommates: a character was created (one at most).");
            return new CityPerson(character);
        }

        // On the mesh of the given agent type at a spot, standing, facing a point.
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
                appearance.RandomizeElements(who.Female ? Gender.Female : Gender.Male, SkinColorHelper.GetRandom(seed), Casual, seed);
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
