using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character;
using Characters;
using Helpers;
using UnityEngine;
using UnityEngine.AI;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Family
{
    /// <summary>Who somebody in the family's own pool is dressed as.</summary>
    public enum FamilyLook { Partner, PartnerFormal, Officiant, Mother, Guest }

    /// <summary>
    /// The family's own people: THREE characters at most, ever (the partner, and two more for a
    /// wedding: the officiant and one guest of the family's). Made the first time each is
    /// needed, kept on the shelf switched off, and handed out again in the same look, so the
    /// expensive part (the game merges a character's meshes when its look is built) is paid
    /// once per look. The partner's look is built from the seed in the save: the same face in
    /// every session, in day clothes or dressed up.
    ///
    /// They are the living city's kind of person (CityPerson: they only move by their navigation
    /// agent, clips never slide them), on whichever navigation mesh the place has. Nothing here
    /// runs by itself: the director calls <see cref="Tick"/> while somebody is out, and that
    /// only turns people who are turning and hides whoever cannot be seen.
    /// </summary>
    public sealed class FamilyCast
    {
        public const int Size = 3;

        private const string CharacterPrefab = "Characters/HumanDefinitionLow";
        private const float HideDistance = 150f;
        private const float HideBehind = 6f;
        private const float TurnRate = 240f;       // degrees a second
        private const float MaxBuild = 0.55f;      // the mod's clips are made for slim to average
        private const int MotherSeed = FixedLooks.MomSeed;   // Mom: the one look of every game (Phone/Contacts/FixedLooks), as on her contact and in the street

        private static readonly AppearanceTag[] Casual = { AppearanceTag.Casual };
        private static readonly AppearanceTag[] Formal = { AppearanceTag.Formal };
        private static readonly AppearanceElementType[] Clothes =
        {
            AppearanceElementType.Torso, AppearanceElementType.TorsoAccessory, AppearanceElementType.Legs,
            AppearanceElementType.LegsAccessory, AppearanceElementType.Feet, AppearanceElementType.FeetAccessory
        };

        private readonly CityPerson[] _people = new CityPerson[Size];
        private readonly bool[] _out = new bool[Size];
        private readonly long[] _worn = new long[Size];          // which look the character on the shelf has
        private readonly Quaternion[] _turnTo = new Quaternion[Size];
        private readonly bool[] _turning = new bool[Size];
        private readonly Transform _parent;
        private readonly IModLogger _logger;
        private bool _failed;

        public FamilyCast(Transform parent, IModLogger logger)
        {
            _parent = parent;
            _logger = logger;
            Props = new CityProps(parent);
        }

        /// <summary>The modelled props the family's people hold or push (cups, the newspaper, the pushchair).</summary>
        public CityProps Props { get; }

        /// <summary>True while anybody is out.</summary>
        public bool AnyOut => _out[0] || _out[1] || _out[2];

        public bool Failed => _failed;

        /// <summary>
        /// Puts somebody at a spot that was found on a navigation mesh, facing a point, standing.
        /// Slot 0 is always the partner. Null if that slot is out already, the spot will not take
        /// them, or characters cannot be made (said once in the log; family life goes on as
        /// messages and conversations).
        /// </summary>
        public CityPerson Put(int slot, FamilyLook look, Vector3 spot, Vector3 face, int agentType)
        {
            if (_failed || slot < 0 || slot >= Size || _out[slot])
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
                    _worn[slot] = 0;
                }
                person.Character.gameObject.SetActive(true);
                person.Reset(Props);
                long wanted = LookId(look);
                if (_worn[slot] != wanted)
                {
                    Dress(person, look);
                    _worn[slot] = wanted;
                }
                person.InUse = true;
                person.Actor.InUse = true;
                if (!Place(person, spot, face, agentType))
                {
                    Release(person);
                    return null;
                }
                _out[slot] = true;
                _turning[slot] = false;
                return person;
            }
            catch (Exception e)
            {
                _failed = true;
                _logger?.Error(e);
                _logger?.Warn("Family: characters could not be made (see the error above); the partner and the wedding party are not shown this session. Everything else goes on.");
                return null;
            }
        }

        public void Release(CityPerson person)
        {
            for (int i = 0; i < Size; i++)
            {
                if (person == null || _people[i] != person)
                {
                    continue;
                }
                _out[i] = false;
                _turning[i] = false;
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
        }

        public void ReleaseAll()
        {
            for (int i = 0; i < Size; i++)
            {
                if (_people[i] != null)
                {
                    Release(_people[i]);
                }
            }
        }

        /// <summary>Somebody standing turns to face a point over a moment.</summary>
        public void Face(CityPerson person, Vector3 point)
        {
            for (int i = 0; i < Size; i++)
            {
                if (person == null || _people[i] != person || !_out[i] || person.Lost)
                {
                    continue;
                }
                Vector3 flat = point - person.Position;
                flat.y = 0f;
                if (flat.sqrMagnitude > 0.01f)
                {
                    _turnTo[i] = Quaternion.LookRotation(flat);
                    _turning[i] = true;
                }
            }
        }

        /// <summary>Called by the director each frame while somebody is out: a few comparisons a person.</summary>
        public void Tick(float delta)
        {
            Camera camera = GameManager.GetMainCamera();
            Transform eye = camera != null ? camera.transform : null;
            for (int i = 0; i < Size; i++)
            {
                CityPerson person = _people[i];
                if (!_out[i] || person == null)
                {
                    continue;
                }
                if (person.Lost)
                {
                    _out[i] = false;   // destroyed with the scene, or taken by the casualty system
                    _turning[i] = false;
                    continue;
                }
                if (_turning[i])
                {
                    Transform body = person.Character.transform;
                    body.rotation = Quaternion.RotateTowards(body.rotation, _turnTo[i], TurnRate * delta);
                    if (Quaternion.Angle(body.rotation, _turnTo[i]) < 0.5f)
                    {
                        _turning[i] = false;
                    }
                }
                if (eye != null)
                {
                    Vector3 offset = person.Position - eye.position;
                    person.SetVisible(offset.sqrMagnitude < HideDistance * HideDistance && Vector3.Dot(offset, eye.forward) > -HideBehind);
                }
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
        }

        private CityPerson Create(Vector3 near)
        {
            using var piece = SessionWatch.Piece("people: a character being made");
            var character = PrefabHelper.CreatePrefab<ThirdPersonCharacter>(CharacterPrefab, near, Quaternion.identity, _parent);
            character.name = "RealLifeOverhaul Family";
            var fidgets = character.GetComponentInChildren<BoredAnimations>(true);
            if (fidgets != null)
            {
                fidgets.pauseBoredAnimations = true;   // idle fidgets would play over what they are asked to do
            }
            if (character.characterRigidbody != null)
            {
                character.characterRigidbody.isKinematic = true;   // they go where their route takes them
            }
            _logger?.Info("Family: a character was created (three at most).");
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

        private static long LookId(FamilyLook look)
        {
            switch (look)
            {
                case FamilyLook.Partner: return ((long)Partner.Seed << 3) | 1L | (Partner.Female ? 4L : 0L);
                case FamilyLook.PartnerFormal: return ((long)Partner.Seed << 3) | 2L | (Partner.Female ? 4L : 0L);
                case FamilyLook.Officiant: return -2;
                case FamilyLook.Mother: return -3;
                default: return -4 - Random.Range(0, 1 << 20);   // a guest is somebody new each time
            }
        }

        // One mesh merge. The face, hair and build come from the seed with day clothes; dressing up changes the clothes only.
        /// <summary>The partner's age, from their seed: the same in the city and on their contact's picture.</summary>
        internal static int PartnerAge(int seed)
        {
            return 24 + (seed & 0x7fffffff) % 14;
        }

        private static void Dress(CityPerson person, FamilyLook look)
        {
            AppearanceSetter appearance = person.Character.appearanceSetter;
            int year = GameCalendar.DaysPerYear;
            int seed;
            bool female;
            int age;
            bool formal;
            switch (look)
            {
                case FamilyLook.Partner:
                case FamilyLook.PartnerFormal:
                    seed = Partner.Seed != 0 ? Partner.Seed : 1;
                    female = Partner.Female;
                    age = PartnerAge(seed);
                    formal = look == FamilyLook.PartnerFormal;
                    break;
                case FamilyLook.Mother:
                    seed = MotherSeed;
                    female = FixedLooks.MomFemale;
                    age = FixedLooks.MomAge;
                    formal = true;
                    break;
                case FamilyLook.Officiant:
                    seed = 40417;
                    female = false;
                    age = 52;
                    formal = true;
                    break;
                default:
                    seed = Random.Range(1, int.MaxValue);
                    female = (seed & 1) == 0;
                    age = 22 + (seed & 0x7fffffff) % 40;
                    formal = true;
                    break;
            }
            // The game colours a beard to match the hair by a roll of the engine's own dice: the same roll every time for the same seed.
            Random.State dice = Random.state;
            Random.InitState(seed);
            try
            {
                appearance.data.ageInDays = age * year;
                if (!PersonLookGame.Dress(appearance, seed))   // (a partner met in the street: the look they had there, People/PersonLook)
                {
                    appearance.RandomizeElements(female ? Gender.Female : Gender.Male, SkinColorHelper.GetRandom(seed), Casual, seed);
                }
                if (formal)
                {
                    var roll = new System.Random(seed ^ 0x5f3759df);
                    for (int i = 0; i < Clothes.Length; i++)
                    {
                        appearance.RandomizeElement(Clothes[i], Formal, true, false, roll);   // a kind with nothing formal keeps its day wear
                    }
                }
                appearance.data.fatness = Mathf.Min(appearance.data.fatness, MaxBuild);
                Looks.Count("family: a relative's look being built (a mesh merge)");
                appearance.UpdateVisuals();
                Looks.Done();
            }
            finally
            {
                Random.state = dice;
            }
        }
    }

    /// <summary>
    /// The family's own furniture and decorations, by id. Each is the modelled prop from the
    /// prop bundle when the bundle has one of that id, and until then a clean stand-in of plain
    /// shapes built here (Core/Shapes): a model of "cot" added to the bundle changes what
    /// is shown with no change to this code. One object each, made the first time it is wanted
    /// and kept; no script on any of them.
    /// </summary>
    public sealed class FamilyProps
    {
        public const string Arch = "wedding_arch";
        public const string Flowers = "flower_stand";
        public const string Runner = "aisle_runner";
        public const string Cot = "cot";
        public const string HighChair = "high_chair";
        public const string Baby = "baby_bundle";

        /// <summary>
        /// A swaddled baby in the cot, or not: the modelled <c>baby_bundle</c> lying on the modelled cot's blanket with
        /// its head to the pillow (0.53 m up, in the middle: where the cot was made to take it). It is a piece of the cot
        /// object, made the first time one is wanted and switched off otherwise, so it goes back to the shelf with the cot.
        /// The plain stand-in cot stays empty: its mattress is not at that height.
        /// </summary>
        public static void Tuck(Transform cot, bool baby)
        {
            if (cot == null)
            {
                return;
            }
            Transform bundle = cot.Find(Baby);
            if (bundle == null)
            {
                if (!baby || cot.Find(Cot) == null || !PropLibrary.TryGet(Baby, out Mesh mesh, out Material material) || mesh == null || material == null)
                {
                    return;
                }
                bundle = Shapes.Piece(Baby, cot, mesh, material, new Vector3(0f, 0.53f, 0f), Vector3.one);
            }
            if (bundle.gameObject.activeSelf != baby)
            {
                bundle.gameObject.SetActive(baby);
            }
        }

        private static readonly Color Ivory = new Color(0.94f, 0.92f, 0.86f);
        private static readonly Color Blossom = new Color(0.93f, 0.55f, 0.66f);
        private static readonly Color Leaf = new Color(0.36f, 0.56f, 0.34f);
        private static readonly Color Wood = new Color(0.80f, 0.68f, 0.50f);
        private static readonly Color Linen = new Color(0.72f, 0.82f, 0.90f);

        private readonly Dictionary<string, Stack<Transform>> _shelf = new Dictionary<string, Stack<Transform>>(8);
        private readonly List<Transform> _outNow = new List<Transform>(12);
        private readonly Transform _root;

        public FamilyProps(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul Family Props").transform;
            _root.SetParent(parent, false);
        }

        /// <summary>Stands a prop on the floor at a point, turned to face a direction. Null if nothing can be drawn at all.</summary>
        public Transform Stand(string id, Vector3 position, Vector3 forward, bool carve = false)
        {
            Transform prop = Take(id);
            if (prop == null)
            {
                return null;
            }
            forward.y = 0f;
            prop.SetPositionAndRotation(position, forward.sqrMagnitude > 0.001f ? Quaternion.LookRotation(forward) : Quaternion.identity);
            if (carve && prop.TryGetComponent(out NavMeshObstacle obstacle))
            {
                obstacle.enabled = true;   // people on foot go round it instead of through it
                MeshCuts.Note("a cot or a high chair at home");
            }
            _outNow.Add(prop);
            return prop;
        }

        public void Return(Transform prop)
        {
            if (prop == null)
            {
                return;
            }
            _outNow.Remove(prop);
            if (prop.TryGetComponent(out NavMeshObstacle obstacle) && obstacle.enabled)
            {
                obstacle.enabled = false;
                MeshCuts.Note("a cot or a high chair put away");
            }
            prop.SetParent(_root, false);
            prop.gameObject.SetActive(false);
            if (!_shelf.TryGetValue(prop.name, out Stack<Transform> spare))
            {
                spare = new Stack<Transform>(2);
                _shelf[prop.name] = spare;
            }
            spare.Push(prop);
        }

        public void ReturnAll()
        {
            for (int i = _outNow.Count - 1; i >= 0; i--)
            {
                Return(_outNow[i]);
            }
            _outNow.Clear();
        }

        private Transform Take(string id)
        {
            if (_shelf.TryGetValue(id, out Stack<Transform> spare))
            {
                while (spare.Count > 0)
                {
                    Transform again = spare.Pop();
                    if (again != null)
                    {
                        again.gameObject.SetActive(true);
                        return again;
                    }
                }
            }
            var made = new GameObject(id);
            made.transform.SetParent(_root, false);
            Vector3 size;
            if (Shapes.Prop(id, made.transform))
            {
                size = PropLibrary.SizeOf(id);
            }
            else if (!Shapes.Available || !StandIn(id, made.transform, out size))
            {
                UnityEngine.Object.Destroy(made);
                return null;
            }
            if (id == Cot || id == HighChair)
            {
                var obstacle = made.AddComponent<NavMeshObstacle>();
                obstacle.shape = NavMeshObstacleShape.Box;
                obstacle.size = new Vector3(Mathf.Max(0.4f, size.x), Mathf.Max(0.6f, size.y), Mathf.Max(0.4f, size.z));
                obstacle.center = new Vector3(0f, obstacle.size.y * 0.5f, 0f);
                obstacle.carving = true;
                obstacle.enabled = false;
            }
            return made.transform;
        }

        // The plain shapes that stand in until the prop is modelled. Origin on the floor, facing +z, in metres.
        private static bool StandIn(string id, Transform parent, out Vector3 size)
        {
            Material ivory = Shapes.Paint(Ivory);
            Material blossom = Shapes.Paint(Blossom);
            Material leaf = Shapes.Paint(Leaf);
            Material wood = Shapes.Paint(Wood);
            Material linen = Shapes.Paint(Linen);
            switch (id)
            {
                case Arch:
                    Shapes.Piece("Post", parent, Shapes.Cube, ivory, new Vector3(-0.95f, 1.15f, 0f), new Vector3(0.09f, 2.3f, 0.09f));
                    Shapes.Piece("Post", parent, Shapes.Cube, ivory, new Vector3(0.95f, 1.15f, 0f), new Vector3(0.09f, 2.3f, 0.09f));
                    Shapes.Piece("Beam", parent, Shapes.Cube, ivory, new Vector3(0f, 2.32f, 0f), new Vector3(2.1f, 0.09f, 0.09f));
                    for (int i = -2; i <= 2; i++)
                    {
                        Shapes.Piece("Bloom", parent, Shapes.Drum, i % 2 == 0 ? blossom : leaf, new Vector3(i * 0.42f, 2.36f, 0f), new Vector3(0.30f, 0.16f, 0.30f));
                    }
                    size = new Vector3(2.1f, 2.5f, 0.3f);
                    return true;
                case Flowers:
                    Shapes.Piece("Stand", parent, Shapes.Post, ivory, Vector3.zero, new Vector3(0.26f, 0.95f, 0.26f));
                    Shapes.Piece("Leaves", parent, Shapes.Drum, leaf, new Vector3(0f, 0.93f, 0f), new Vector3(0.40f, 0.10f, 0.40f));
                    Shapes.Piece("Blooms", parent, Shapes.Drum, blossom, new Vector3(0f, 1.02f, 0f), new Vector3(0.30f, 0.16f, 0.30f));
                    size = new Vector3(0.4f, 1.2f, 0.4f);
                    return true;
                case Runner:
                    Shapes.Piece("Cloth", parent, Shapes.Cube, ivory, new Vector3(0f, 0.012f, 0f), new Vector3(1.1f, 0.012f, 1f));
                    size = new Vector3(1.1f, 0.02f, 1f);
                    return true;
                case Cot:
                    for (int x = -1; x <= 1; x += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Shapes.Piece("Leg", parent, Shapes.Cube, wood, new Vector3(x * 0.33f, 0.45f, z * 0.60f), new Vector3(0.05f, 0.9f, 0.05f));
                        }
                        Shapes.Piece("Rail", parent, Shapes.Cube, wood, new Vector3(x * 0.33f, 0.86f, 0f), new Vector3(0.04f, 0.05f, 1.2f));
                        Shapes.Piece("Side", parent, Shapes.Cube, wood, new Vector3(x * 0.33f, 0.62f, 0f), new Vector3(0.02f, 0.42f, 1.16f));
                    }
                    Shapes.Piece("Base", parent, Shapes.Cube, wood, new Vector3(0f, 0.40f, 0f), new Vector3(0.66f, 0.06f, 1.2f));
                    Shapes.Piece("Mattress", parent, Shapes.Cube, linen, new Vector3(0f, 0.47f, 0f), new Vector3(0.60f, 0.09f, 1.12f));
                    size = new Vector3(0.72f, 0.9f, 1.26f);
                    return true;
                case HighChair:
                    for (int x = -1; x <= 1; x += 2)
                    {
                        for (int z = -1; z <= 1; z += 2)
                        {
                            Shapes.Piece("Leg", parent, Shapes.Cube, wood, new Vector3(x * 0.20f, 0.30f, z * 0.20f), new Vector3(0.04f, 0.6f, 0.04f));
                        }
                    }
                    Shapes.Piece("Seat", parent, Shapes.Cube, linen, new Vector3(0f, 0.62f, 0f), new Vector3(0.40f, 0.05f, 0.40f));
                    Shapes.Piece("Back", parent, Shapes.Cube, linen, new Vector3(0f, 0.84f, -0.18f), new Vector3(0.40f, 0.40f, 0.04f));
                    Shapes.Piece("Tray", parent, Shapes.Cube, ivory, new Vector3(0f, 0.80f, 0.22f), new Vector3(0.44f, 0.03f, 0.22f));
                    size = new Vector3(0.48f, 1.05f, 0.5f);
                    return true;
                default:
                    size = Vector3.zero;
                    return false;
            }
        }
    }
}
