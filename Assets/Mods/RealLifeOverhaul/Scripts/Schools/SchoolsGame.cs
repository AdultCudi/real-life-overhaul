using System;
using System.Collections.Generic;
using BA_Packages.Rivals.Scripts;
using Controllers;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.NewBuildings;
using RealLifeOverhaul.NewGround;
using RealLifeOverhaul.Uptown;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Schools
{
    /// <summary>
    /// The only part of the schools that reads or changes the game: the clock, the player and the way they are taken to the
    /// gate, money in the game's books, the game's notices and names, the main school declared to the copy engine and cut out
    /// of the walking meshes, the campus's pieces copied from the game's own (its playground fence, basketball court and
    /// hoops, benches and street trees) and the building sites' bundle's pitch, the hall and the junior block as the city's
    /// designs, the families at the gates as the game's own pedestrians, the classroom doors kept from the game's diplomas and
    /// the traffic of the district's buildings round the campus. Nothing here keeps state but the templates found at a load
    /// and the scratch lists. Called at a load, on a second's look, a day's pass or a press, never per frame.
    /// </summary>
    internal static class SchoolsGame
    {
        public const string MainId = "schools_birchwood";    // the main school's id among the made buildings, and its claim in the book
        private const string RootName = "RealLifeOverhaul Schools campus";
        private const string Owner = "schools";              // the copy engine's log names the feature that asked
        private const float CutInset = 0.5f;                 // m inside a footprint each side: a carve reaches the walker's radius past its box

        // The height flat pieces lie at over the ground: the block ground's grass top is 0.0552 m (its mesh, Build 3682), so a piece laid at 0.03 (the court as it
        // was, Player.log of 26 Oct 2026 and the owner's screenshot: "the hoops stand on bare grass") is under the lawn. The parks' paths lie at the same 0.09.
        private const float Surface = ParksGame.PavingRise;

        // Build 3682, read from the game's level files (not-in-repo scratch scan of 25 and 26 Oct 2026): every one of these is drawn on its own, never in a
        // static batch (a mesh of its own, never "Combined Mesh"). Found and copied by GamePieces, as Uptown's street furniture and parks are.
        private static readonly PieceTemplate[] Templates =
        {
            new PieceTemplate { Kind = (int)CampusPieceKind.Fence, Root = "Roads", Name = "Playground_Fence_Modular", X = -1600.71f, Z = -1168.75f, Y = 0f },
            new PieceTemplate { Kind = (int)CampusPieceKind.Post, Root = "Roads", Name = "Playground_Fence_ModularFinal", X = -1659.65f, Z = -1168.76f, Y = 0f },
            // The game's court is a HALF court (SchoolGrounds): its plane lies at the surface, over the grass.
            new PieceTemplate { Kind = (int)CampusPieceKind.Court, Root = "BuildingBlocks", Name = "SM_BasketballCourt", X = -452.71f, Z = -326.32f, Y = Surface },
            new PieceTemplate { Kind = (int)CampusPieceKind.Hoop, Root = "BuildingBlocks", Name = "Prefab_BasketballHoop", X = -454.67f, Z = -326.22f, Y = 1.15f },
            // The game keeps its pavement benches under a scene root of their own, "SleepingBench" (props_scan, 25 Oct 2026: SleepingBench/SleepingBench (11)),
            // the one the street furniture and the parks take, and Madison Square Park's group beside it when that is not there at a load.
            new PieceTemplate
            {
                Kind = (int)CampusPieceKind.Bench, Root = GamePieces.BenchRoot, Name = GamePieces.BenchName, X = GamePieces.BenchX, Z = GamePieces.BenchZ, Y = 0.03f,
                Within = GamePieces.BenchReach, AltRoot = GamePieces.BenchAltRoot, AltX = GamePieces.BenchAltX, AltZ = GamePieces.BenchAltZ, Sittable = true
            },
            // The free trees and bushes of the Vegetation root, the parks' own, a few of each kind.
            new PieceTemplate { Kind = (int)CampusPieceKind.Oak, Root = "Vegetation", Name = "Tree Broadleaf L1", X = -327.16f, Z = 313.58f, Y = 0f, Within = TreeReach },
            new PieceTemplate { Kind = (int)CampusPieceKind.Birch, Root = "Vegetation", Name = "Tree Birch", X = 4.05f, Z = -129.34f, Y = 0f, Within = TreeReach },
            new PieceTemplate { Kind = (int)CampusPieceKind.Beech, Root = "Vegetation", Name = "Tree Broadleaf M1", X = 440.06f, Z = -369.64f, Y = 0f, Within = TreeReach },
            new PieceTemplate { Kind = (int)CampusPieceKind.Maple, Root = "Vegetation", Name = "Tree Maple", X = -144.69f, Z = 441.56f, Y = 0f, Within = TreeReach },
            new PieceTemplate { Kind = (int)CampusPieceKind.Bush, Root = "Vegetation", Name = "Broadleaf Bush L1", X = -182.53f, Z = 313.33f, Y = 0f, Within = TreeReach },
            new PieceTemplate { Kind = (int)CampusPieceKind.Bin, Root = "Trash Cans", Name = "SM_Trash Can", X = -87.00f, Z = 453.90f, Y = 0.06f, Within = TreeReach },
            // Madison Square Park's table group: a round table and four chairs people sit on (the chairs are the game's OutsideInteractableItemToRest).
            new PieceTemplate { Kind = (int)CampusPieceKind.Table, Root = "Madison Square Park", Name = "Prefab_TableGroup", X = 272.70f, Z = 231.84f, Y = 0.03f, Within = TreeReach },
            // The Industry City playground's climbing frame: the one piece of it whose mesh is its own (the swings, stalls and roundabout are drawn in a static batch).
            new PieceTemplate { Kind = (int)CampusPieceKind.Climb, Root = "BuildingBlock(1,1)", Name = "Playground_Climbing", X = -1655.96f, Z = -1178.12f, Y = 0f, Within = TreeReach },
            ParksGame.PavingTemplate((int)CampusPieceKind.Pavement)
        };

        private const float TreeReach = 40f;   // m from its fixed place a free piece may be: farther, and the game's scene has moved (the parks' reach)

        private static readonly GameObject[] Found = new GameObject[Templates.Length];

        internal static bool Loading => LoadScene.isLoading || !ModState.Loaded || SaveGameManager.Current == null;

        internal static int Day => TimeHelper.CurrentDay;

        internal static int Hour => TimeHelper.CurrentHour;

        /// <summary>Minutes of the game's day, 0 to 1439.</summary>
        internal static int Minute => TimeHelper.CurrentHour * 60 + Mathf.Clamp((int)TimeHelper.CurrentMinute, 0, 59);

        internal static int Year => GameCalendar.DaysPerYear;

        internal static bool InSession => GoingPublic.InSession;

        internal static Vector3 Player => PlayerHelper.PlayerController != null ? PlayerHelper.GetPosition() : Vector3.zero;

        internal static bool Outdoors => PlayerHelper.PlayerController != null && !BuildingManager.IsInsideBuilding;

        // ------------------------------------------------------------------ words the game knows

        /// <summary>A district's name in the player's language (Uptown's id is its name's key, as the game's own are).</summary>
        internal static string District(string key) => string.IsNullOrEmpty(key) ? string.Empty : key.GetLocalization();

        /// <summary>A person's name the game's own way from a seed: the same on every machine.</summary>
        internal static string Person(int seed)
        {
            try
            {
                return new System.Random(seed).GetFullName();
            }
            catch (Exception)
            {
                return "?";
            }
        }

        /// <summary>A school's name in the player's language: a council school's is its district's.</summary>
        internal static string Name(int school)
        {
            SchoolDef d = SchoolRules.Def(school);
            return d.Kind == SchoolKind.Council ? SchoolsText.Fill(SchoolsText.Council, "district", District(d.District)) : SchoolsText.T(d.Key);
        }

        // ------------------------------------------------------------------ money and notices

        /// <summary>Money at today's prices out of the player's account for something; false (with the game's own notice) when they cannot pay.</summary>
        internal static bool Pay(long startPrices, string transaction)
        {
            int amount = Today(startPrices);
            return amount <= 0 || GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, false, true);
        }

        /// <summary>Money at today's prices into the player's account, or a cost that must be met (a week's loss): never refused.</summary>
        internal static void Book(long startPrices, string transaction)
        {
            int amount = startPrices >= 0 ? Today(startPrices) : -Today(-startPrices);
            if (amount != 0)
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transaction), null, null, true);
            }
        }

        /// <summary>An amount at the start's prices at today's, whole money, kept within what the game's books hold.</summary>
        internal static int Today(long startPrices)
        {
            long capped = Math.Max(0L, Math.Min(int.MaxValue / 4, startPrices));
            return Prices.At((int)capped);
        }

        /// <summary>A notice of the game's own, its names filled.</summary>
        internal static void Tell(NotificationType type, string key, params string[] pairs)
        {
            Dictionary<string, string> data = null;
            if (pairs != null && pairs.Length >= 2)
            {
                data = new Dictionary<string, string>(pairs.Length / 2);
                for (int i = 0; i + 1 < pairs.Length; i += 2)
                {
                    data[pairs[i]] = pairs[i + 1] ?? string.Empty;
                }
            }
            Notifications.Show(type, key, data, 10f);
        }

        // ------------------------------------------------------------------ the main school

        /// <summary>
        /// The main school declared to the copy engine for this load: a K1 office's look at 1 Birchwood Green, three storeys, on
        /// its lot on Uptown Avenue, filed under Uptown; the engine makes it behind the loading screen or says why not.
        /// </summary>
        internal static void Declare(Func<bool> wanted)
        {
            SchoolGrounds.MainLot(out float x, out float z, out float facing, out float width, out float depth, out float inset);
            var lot = new MadeLot(x, z, facing, width, depth) { Inset = inset };
            MadeBuildings.Declare(new MadeSpec
            {
                Id = MainId, Street = SchoolGrounds.Street, Number = SchoolGrounds.MainNumber, Size = SchoolGrounds.MainSize, Version = SchoolGrounds.MainVersion,
                Type = SchoolGrounds.MainType, District = DistrictValues.Id, Lots = new[] { lot }, Preferred = 0, Traffic = 30, Floors = SchoolGrounds.MainStoreys,
                Wanted = wanted
            });
        }

        /// <summary>The main school as made at this load; null when it stood down (Player.log says why) or before the load.</summary>
        internal static MadeBuilding Main => MadeBuildings.Get(MainId);

        /// <summary>
        /// A carving box over a made building's footprint on both walking meshes, so nobody walks through its walls: Uptown's
        /// ground is grown with none. Stationary, so it carves once.
        /// </summary>
        internal static bool Cut(MadeBuilding made)
        {
            IList<DesignEntry> designs = CopyGame.Catalogue;
            if (made == null || made.Controller == null || made.DesignIndex < 0 || made.DesignIndex >= designs.Count)
            {
                return false;
            }
            DesignEntry d = designs[made.DesignIndex];
            Transform root = made.Controller.transform;
            if (!root.TryGetComponent(out NavMeshObstacle cut))
            {
                cut = root.gameObject.AddComponent<NavMeshObstacle>();
            }
            Vector3 scale = root.lossyScale;
            float sx = Mathf.Max(Mathf.Abs(scale.x), 1e-4f), sy = Mathf.Max(Mathf.Abs(scale.y), 1e-4f), sz = Mathf.Max(Mathf.Abs(scale.z), 1e-4f);
            float width = Mathf.Max(0.5f, d.MaxX - d.MinX - 2f * CutInset), length = Mathf.Max(0.5f, d.MaxZ - d.MinZ - 2f * CutInset), height = Mathf.Max(2f, d.Height);
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3((d.MinX + d.MaxX) * 0.5f / sx, height * 0.5f / sy, (d.MinZ + d.MaxZ) * 0.5f / sz);
            cut.size = new Vector3(width / sx, height / sy, length / sz);
            cut.carveOnlyStationary = true;
            cut.carving = true;
            cut.enabled = true;
            return true;
        }

        // ------------------------------------------------------------------ the hall and the junior block

        /// <summary>One of the campus's own buildings as a design of the city's fitted to its plot (no door: the school's own).</summary>
        internal static CopyHandle Plot(CampusBlockPlot plot)
        {
            // Null while the copy engine is not there (its option off, or its director not made yet): the caller asks again when it is.
            return BuildingCopies.PlaceOnPlot(null, null, new Vector3(plot.X, UptownLots.GroundY, plot.Z), plot.Yaw, (int)plot.Width, (int)plot.Depth, Owner);
        }

        /// <summary>True while the copy engine takes requests: its option is on and its director is made.</summary>
        internal static bool CopiesAvailable => BuildingCopies.Available;

        /// <summary>A copy's footprint cut out of the walking meshes once it stands: its walls' box, a little inside. False while it does not stand.</summary>
        internal static bool CutCopy(CopyHandle handle)
        {
            Transform root = handle != null ? handle.Root : null;
            if (root == null)
            {
                return false;
            }
            if (root.TryGetComponent(out NavMeshObstacle cut))
            {
                return true;
            }
            Collider[] walls = root.GetComponentsInChildren<Collider>(false);
            bool any = false;
            var box = new Bounds();
            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] == null || walls[i].isTrigger)
                {
                    continue;
                }
                Bounds b = walls[i].bounds;
                if (!any)
                {
                    box = b;
                    any = true;
                }
                else
                {
                    box.Encapsulate(b);
                }
            }
            if (!any)
            {
                return false;
            }
            var holder = new GameObject(RootName + " cut");
            holder.transform.SetParent(root, true);
            holder.transform.SetPositionAndRotation(new Vector3(box.center.x, UptownLots.GroundY, box.center.z), Quaternion.identity);
            cut = holder.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3(0f, Mathf.Max(2f, box.size.y) * 0.5f, 0f);
            cut.size = new Vector3(Mathf.Max(0.5f, box.size.x - 2f * CutInset), Mathf.Max(2f, box.size.y), Mathf.Max(0.5f, box.size.z - 2f * CutInset));
            cut.carveOnlyStationary = true;
            cut.carving = true;
            return true;
        }

        internal static void Remove(CopyHandle handle)
        {
            if (handle != null)
            {
                BuildingCopies.Remove(handle);
            }
        }

        // ------------------------------------------------------------------ the pieces

        /// <summary>
        /// The game's pieces found once a load (behind the loading screen): of each kind, the one of its name nearest its fixed
        /// place, switched off or not (the far districts' tweak may have put it out of the picture). Returns the kinds missing,
        /// in a word each, or null.
        /// </summary>
        internal static string FindTemplates()
        {
            return GamePieces.Find(Templates, Found);
        }

        /// <summary>The line for Player.log about which of the game's pieces were found and which were not.</summary>
        internal static string FoundWords()
        {
            return GamePieces.Words(Templates, Found);
        }

        /// <summary>
        /// The campus's pieces stood under a root of their own (made asleep, woken together): each a copy of the game's piece of its kind, drawn as the game
        /// draws it; the pitch the building sites' bundle's; the paths the game's pavement material on a flat surface cut to rectangles; the two hoops each with a
        /// net; a run of fence, a hoop, a bench, a table group, a bush and a tree cut out of both walking meshes. Returns the root, or null when nothing stood.
        /// </summary>
        internal static GameObject Stand(List<CampusPiece> pieces, List<CampusFence> runs, List<CampusBox> paths, out int stood, out int missed, out int paved, out int nets)
        {
            stood = missed = paved = nets = 0;
            var root = new GameObject(RootName);
            root.SetActive(false);
            for (int i = 0; i < pieces.Count; i++)
            {
                CampusPiece p = pieces[i];
                GameObject made = p.Kind == CampusPieceKind.Pitch ? Pitch(p, root.transform) : Copy(p, root.transform);
                if (made == null)
                {
                    missed++;
                    continue;
                }
                stood++;
                if (p.Kind == CampusPieceKind.Hoop && Net(made))
                {
                    nets++;
                }
            }
            for (int i = 0; i < runs.Count; i++)
            {
                Fence(runs[i], root.transform);
            }
            paved = Pave(paths, root.transform);
            if (stood == 0 && paved == 0)
            {
                UnityEngine.Object.Destroy(root);
                return null;
            }
            root.SetActive(true);
            return root;
        }

        // A copy of the game's own piece of a kind, where the campus has it, its renderers drawn.
        private static GameObject Copy(CampusPiece p, Transform parent)
        {
            int t = TemplateOf(p.Kind);
            GameObject template = t >= 0 ? Found[t] : null;
            if (template == null)
            {
                return null;
            }
            Vector3 at = new Vector3(p.X, UptownLots.GroundY + Templates[t].Y, p.Z);
            // A table group is a prefab turned on its side with its children turned back: its own turn is kept, the campus's yaw put on it.
            GameObject copy = p.Kind == CampusPieceKind.Table
                ? GamePieces.StandAligned(template, parent, RootName + " " + p.Kind, at, p.Yaw)
                : GamePieces.Stand(template, parent, RootName + " " + p.Kind, at, p.Yaw);
            if (p.Kind == CampusPieceKind.Court)
            {
                copy.transform.localScale = copy.transform.localScale * p.StretchX;   // the game's own scale, stretched evenly
            }
            SchoolGrounds.RoomOf(p.Kind, out float across, out float deep, out float high);
            if (across > 0f)
            {
                Obstacle(parent, new Vector3(p.X, UptownLots.GroundY, p.Z), p.Yaw, new Vector3(across, high, deep));
            }
            return copy;
        }

        // What stands in the way: the walking meshes are cut round a piece laid after they were grown, on a holder of its own (the piece may be turned on its side).
        private static void Obstacle(Transform parent, Vector3 at, float yaw, Vector3 size)
        {
            var holder = new GameObject(RootName + " room");
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var cut = holder.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3(0f, size.y * 0.5f, 0f);
            cut.size = size;
            cut.carveOnlyStationary = true;
            cut.carving = true;
        }

        private static int TemplateOf(CampusPieceKind kind)
        {
            for (int t = 0; t < Templates.Length; t++)
            {
                if (Templates[t].Kind == (int)kind)
                {
                    return t;
                }
            }
            return -1;
        }

        // The playing field: the building sites' bundle's pitch, grass, lines and a track, stretched to the field.
        private static GameObject Pitch(CampusPiece p, Transform parent)
        {
            if (!PropLibrary.Has(Landmarks.LandmarkRules.Pitch) || !PropLibrary.TryGet(Landmarks.LandmarkRules.Pitch, out Mesh mesh, out Material material) || mesh == null || material == null)
            {
                return null;
            }
            var piece = new GameObject(RootName + " pitch");
            piece.transform.SetParent(parent, false);
            piece.transform.SetPositionAndRotation(new Vector3(p.X, UptownLots.GroundY + Surface, p.Z), Quaternion.Euler(0f, p.Yaw, 0f));
            piece.transform.localScale = new Vector3(p.StretchX, 1f, p.StretchZ);
            piece.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = piece.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return piece;
        }

        // ------------------------------------------------------------------ the paths

        private static readonly List<Mesh> Made = new List<Mesh>(2);   // the paving's meshes, destroyed with the campus
        private static readonly List<PlanBox> Rects = new List<PlanBox>(12);
        private static Mesh _net;                                       // the hoops' net, one for both

        /// <summary>The paths laid as one flat surface of the game's pavement material, the parks' way; the square metres laid (0 where the material was not found).</summary>
        private static int Pave(List<CampusBox> paths, Transform parent)
        {
            int t = TemplateOf(CampusPieceKind.Pavement);
            Material material = t >= 0 ? ParksGame.PavingFrom(Found[t]) : null;
            if (material == null || paths == null || paths.Count == 0)
            {
                return 0;
            }
            Rects.Clear();
            for (int i = 0; i < paths.Count; i++)
            {
                Rects.Add(new PlanBox(paths[i].X - paths[i].HalfX, paths[i].Z - paths[i].HalfZ, paths[i].X + paths[i].HalfX, paths[i].Z + paths[i].HalfZ, 0f));
            }
            Mesh mesh = ParksGame.PavingMesh(Rects, RootName + " paths", out float area);
            Made.Add(mesh);
            var paving = new GameObject(RootName + " paths");
            paving.transform.SetParent(parent, false);
            paving.AddComponent<MeshFilter>().sharedMesh = mesh;
            var shown = paving.AddComponent<MeshRenderer>();
            shown.sharedMaterial = material;
            shown.shadowCastingMode = ShadowCastingMode.Off;
            return Mathf.RoundToInt(area);
        }

        // ------------------------------------------------------------------ the hoops' nets

        /// <summary>
        /// A net hung from a hoop's rim: the game has no netted hoop it draws on its own (its Skatepark's and Sunset Park's are drawn in a static batch, its
        /// one hoop prefab has a bare rim), so a short cone of twelve sides drawn with the game's own chain-link material, found by name once a load.
        /// False where the material was not found.
        /// </summary>
        private static bool Net(GameObject hoop)
        {
            Material material = CampusLook.NetMaterial;
            if (hoop == null || material == null)
            {
                return false;
            }
            if (_net == null)
            {
                BuildNet();
            }
            var net = new GameObject(RootName + " net");
            net.transform.SetParent(hoop.transform, false);
            net.AddComponent<MeshFilter>().sharedMesh = _net;
            var shown = net.AddComponent<MeshRenderer>();
            shown.sharedMaterial = material;
            shown.shadowCastingMode = ShadowCastingMode.Off;
            return true;
        }

        // A tapering ring of strands in the hoop's own frame (y up, z the way it faces): wide at the rim, narrow at the foot; both faces.
        private static void BuildNet()
        {
            int sides = SchoolGrounds.NetSides;
            var vertices = new Vector3[(sides + 1) * 2];
            var uvs = new Vector2[vertices.Length];
            var triangles = new int[sides * 12];
            for (int i = 0; i <= sides; i++)
            {
                float a = i * 2f * Mathf.PI / sides, sin = Mathf.Sin(a), cos = Mathf.Cos(a);
                vertices[i] = new Vector3(sin * SchoolGrounds.NetTopRadius, SchoolGrounds.NetTop, SchoolGrounds.NetOut + cos * SchoolGrounds.NetTopRadius);
                vertices[sides + 1 + i] = new Vector3(sin * SchoolGrounds.NetFootRadius, SchoolGrounds.NetTop - SchoolGrounds.NetLength, SchoolGrounds.NetOut + cos * SchoolGrounds.NetFootRadius);
                uvs[i] = new Vector2(i * 3f / sides, 1f);
                uvs[sides + 1 + i] = new Vector2(i * 3f / sides, 0f);
            }
            for (int i = 0; i < sides; i++)
            {
                int t0 = i, t1 = i + 1, b0 = sides + 1 + i, b1 = sides + 2 + i, k = i * 12;
                triangles[k] = t0; triangles[k + 1] = b0; triangles[k + 2] = t1;
                triangles[k + 3] = t1; triangles[k + 4] = b0; triangles[k + 5] = b1;
                triangles[k + 6] = t0; triangles[k + 7] = t1; triangles[k + 8] = b0;
                triangles[k + 9] = t1; triangles[k + 10] = b1; triangles[k + 11] = b0;
            }
            _net = new Mesh { name = RootName + " net", vertices = vertices, uv = uvs, triangles = triangles };
            _net.RecalculateNormals();
            _net.RecalculateBounds();
        }

        // A run of fence cut out of the walking meshes: one thin box from end to end.
        private static void Fence(CampusFence run, Transform parent)
        {
            float dx = run.X1 - run.X0, dz = run.Z1 - run.Z0;
            float length = Mathf.Sqrt(dx * dx + dz * dz);
            if (length < 0.5f)
            {
                return;
            }
            var holder = new GameObject(RootName + " fence cut");
            holder.transform.SetParent(parent, false);
            holder.transform.SetPositionAndRotation(new Vector3((run.X0 + run.X1) * 0.5f, UptownLots.GroundY, (run.Z0 + run.Z1) * 0.5f),
                Quaternion.Euler(0f, Mathf.Atan2(dx, dz) * Mathf.Rad2Deg, 0f));
            var cut = holder.AddComponent<NavMeshObstacle>();
            cut.shape = NavMeshObstacleShape.Box;
            cut.center = new Vector3(0f, 1f, 0f);
            cut.size = new Vector3(SchoolGrounds.FenceCut, 2f, length);
            cut.carveOnlyStationary = true;
            cut.carving = true;
        }

        /// <summary>The campus's pieces taken away, their cuts with them. Safe on null.</summary>
        internal static void Take(ref GameObject root)
        {
            if (root != null)
            {
                UnityEngine.Object.Destroy(root);
            }
            root = null;
            for (int i = 0; i < Made.Count; i++)
            {
                if (Made[i] != null)
                {
                    UnityEngine.Object.Destroy(Made[i]);
                }
            }
            Made.Clear();
            if (_net != null)
            {
                UnityEngine.Object.Destroy(_net);
                _net = null;
            }
        }

        // ------------------------------------------------------------------ inside the main school

        /// <summary>
        /// The game's classroom doors on a floor of the main school kept from the click: they belong to the game's business
        /// school's diplomas, which are not taught here. Returns how many.
        /// </summary>
        internal static int QuietDoors()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;
            List<ItemController> all = manager != null ? manager.allItemControllers : null;
            int quiet = 0;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                if (all[i] is EducationDoorController door && door.TryGetComponent(out BoxCollider box) && box.enabled)
                {
                    box.enabled = false;
                    quiet++;
                }
            }
            return quiet;
        }

        // ------------------------------------------------------------------ the families at the gates

        /// <summary>A square of pavement the game's own pedestrians come to and stand in, or null with why.</summary>
        internal static StationaryAiSpawner Crowd(CampusCrowd c, out string why)
        {
            return PeopleGame.Zone(new Vector3(c.X, UptownLots.GroundY, c.Z), c.Yaw, new Vector3(c.Width, PeopleGame.ZoneHeight, c.Depth), out why);
        }

        /// <summary>A crowd's square taken away, its people back to the game's pools. Safe on null.</summary>
        internal static void NoCrowd(ref StationaryAiSpawner zone)
        {
            PeopleGame.Unzone(ref zone);
        }

        // ------------------------------------------------------------------ whether people can walk it

        private static NavMeshPath _route;

        /// <summary>
        /// True when someone of the agent type can walk from one spot to the other on the walking mesh as it is now: both spots on it within 2.5 m and a whole
        /// path between. <paramref name="why"/> says which end or the path failed. One path, reused; asked once a load.
        /// </summary>
        internal static bool Reaches(Vector3 from, Vector3 to, int agentType, out string why)
        {
            why = null;
            _route = _route ?? new NavMeshPath();
            var filter = new NavMeshQueryFilter { agentTypeID = agentType, areaMask = NavMesh.AllAreas };
            if (!NavMesh.SamplePosition(from, out NavMeshHit start, 2.5f, filter))
            {
                why = "its start is off the walking mesh";
                return false;
            }
            if (!NavMesh.SamplePosition(to, out NavMeshHit end, 2.5f, filter))
            {
                why = "its end is off the walking mesh";
                return false;
            }
            if (!NavMesh.CalculatePath(start.position, end.position, filter, _route) || _route.status != NavMeshPathStatus.PathComplete)
            {
                why = "no whole path (" + _route.status + ")";
                return false;
            }
            return true;
        }

        /// <summary>The game's agent type of the pedestrians and every other person on foot but the player, and the player's own.</summary>
        internal static int NpcAgent => NavMeshHelper.NpcAgentTypeId;

        internal static int PlayerAgent => NavMeshHelper.PlayerAgentTypeId;

        // ------------------------------------------------------------------ taking the player there

        /// <summary>
        /// The player put at a spot, facing a way, the game's own way: out of a building first by its own door, out of a vehicle
        /// by its own exit. False while the game is still taking them out (ask again in a moment).
        /// </summary>
        internal static bool TakeTo(Vector3 at, float heading)
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance : null;
            if (BuildingManager.IsInsideBuilding || (manager != null && (manager.enteringBuilding || manager.exitingBuilding)))
            {
                if (BuildingManager.IsInsideBuilding && manager != null && !manager.exitingBuilding)
                {
                    manager.ExitFromBuilding(0);
                }
                return false;
            }
            if (VehicleHelper.IsInsideVehicle())
            {
                GameManager game = InstanceBehavior<GameManager>.IsInitialized ? InstanceBehavior<GameManager>.Instance : null;
                game?.selectedVehicle?.ExitVehicle();
                return false;
            }
            WalkGame.Warp(at);
            if (PlayerHelper.PlayerController != null)
            {
                PlayerHelper.PlayerController.transform.rotation = Quaternion.Euler(0f, heading, 0f);
            }
            return true;
        }

        /// <summary>The game's destination guider set to the main school's address; false when it does not stand.</summary>
        internal static bool Guide()
        {
            MadeBuilding main = Main;
            if (main == null || main.Building == null || !InstanceBehavior<GuidersManager>.IsInitialized)
            {
                return false;
            }
            GuidersManager.SetGuiderTarget(main.Building.Address, DirectionGuiderType.Destination);
            return true;
        }

        // ------------------------------------------------------------------ the district round the campus

        /// <summary>A district building's traffic index as the game reads it for its customers and its price; false for one with no record.</summary>
        internal static bool Traffic(MadeBuilding made, int index)
        {
            if (made == null || made.Building == null)
            {
                return false;
            }
            made.Building.trafficIndex = Mathf.Clamp(index, 0, 100);
            return true;
        }
    }
}
