using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own construction pieces and materials, found once a city behind the loading screen and kept asleep: its
    /// scaffold modules and fabric copied from a building that wears them (the Buildings scene stacks them on 26), with the
    /// scripts that tie them to that building taken off; and its concrete, metal and fence wire by name for the frames, slabs
    /// and fences of new plots. A copy shares the game's meshes and materials: no video memory of its own. Where a scene's
    /// pieces are static-batched (their mesh is the batch's) the pieces are made from the game's meshes found by name instead.
    /// Cost: nothing per frame; one walk over the city's buildings and one search of memory, behind the loading screen.
    /// </summary>
    internal static class SiteKit
    {
        private const string GroupName = "Scaffolding";          // the game's parent of a building's scaffold pieces
        private const int BuildingsLayer = 17;                   // "Buildings": the game's own pieces stand on it, so the camera's fade finds them
        private const int StreetPropsLayer = 26;                 // "StreetProps": the fabric's drawn levels, as the game has them
        private const float PostHeight = 5.12f;                  // metres: the game's module posts, as their colliders
        private const float PostInset = 0.15f;                   // metres from a module's end to its posts' middle
        private const float PostRow = 1.33f;                     // metres from a module's middle to its two rows of posts
        private const float FabricHeight = 5.15f;                // metres: the game's fabric's collider
        private static readonly float[] FabricLods = { 0.20f, 0.07f, 0.015f };   // screen heights, a guess at the game's for the fallback only

        // The game's names: the scene objects first, then the meshes they draw (for the fallback).
        private static readonly string[] Objects = { "SM_Scaffolding_8m", "SM_Scaffolding_4m", "SM_Scaffolding_Corner_4m", "SM_Scaffolding_LargeFabric", "SM_Scaffolding_SmallFabric" };
        private static readonly string[] Meshes = { "SM_Scaffolding_Bottom_Middle_8m", "SM_Scaffolding_Bottom_Middle_4m", "SM_Scaffolding_Bottom_Corner_4m",
            "SM_Scaffolding_LargeFabric_LOD", "SM_Scaffolding_SmallFabric_LOD" };
        private static readonly string[] ConcreteNames = { "M_ConcretePavement_03", "M_OldConcrete", "M_ConcretePavement_04" };
        private static readonly string[] MetalNames = { "M_Metal_GrayLight", "M_Metal_Gray 3", "M_RustedMetalgray" };
        private static readonly string[] WireNames = { "M_FenceWire_Occlussion" };
        private const string ScaffoldMaterial = "M_Scaffolding";
        private const string FabricMaterial = "M_ScaffoldingFabric";

        private static readonly GameObject[] Templates = new GameObject[5];
        private static readonly List<Renderer> Renderers = new List<Renderer>(8);
        private static readonly List<Component> Components = new List<Component>(16);
        private static Transform _asleep;
        private static bool _fromScene;

        /// <summary>The game's concrete, metal and fence wire; null for one the game build does not have.</summary>
        public static Material Concrete { get; private set; }
        public static Material Metal { get; private set; }
        public static Material Wire { get; private set; }

        /// <summary>True once every scaffold piece has a template to copy.</summary>
        public static bool Ready
        {
            get
            {
                for (int i = 0; i < Templates.Length; i++)
                {
                    if (Templates[i] == null)
                    {
                        return false;
                    }
                }
                return true;
            }
        }

        /// <summary>For the log: where the pieces came from.</summary>
        public static string Source => !Ready ? "none found" : _fromScene ? "copied from the game's own scaffolding" : "made from the game's scaffold meshes";

        /// <summary>
        /// Behind the loading screen, after the game's own start of the city: the templates and the materials. False while the
        /// city's buildings are not there yet (tried again); true once looked for, found or not.
        /// </summary>
        public static bool Prewarm(Transform holder)
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            if (city == null || city.cityBuildingControllers == null || city.cityBuildingControllers.Length == 0)
            {
                return false;
            }
            Forget();
            var asleep = new GameObject("RealLifeOverhaul Site Kit (asleep)");
            asleep.SetActive(false);
            asleep.transform.SetParent(holder, false);
            _asleep = asleep.transform;
            _fromScene = FromScene(GameDlc.Open(city));
            Material[] materials = SceneFinds.InMemory<Material>("construction: the game's concrete, metal, fence wire and scaffold materials by name");
            Concrete = Named(materials, ConcreteNames);
            Metal = Named(materials, MetalNames);
            Wire = Named(materials, WireNames);
            if (!_fromScene)
            {
                FromMeshes(materials);
            }
            CivicDirector.Logger?.Info("Construction: the game's scaffold pieces " + Source + "; concrete " + Say(Concrete) + ", metal " + Say(Metal) + ", fence wire " + Say(Wire) + ".");
            return true;
        }

        private static string Say(Material material) => material != null ? "'" + material.name + "'" : "not found (a plain colour instead)";

        /// <summary>A copy of a piece under a parent, switched on with it; null if the piece has no template.</summary>
        public static GameObject Make(WrapPieceKind kind, Transform parent)
        {
            GameObject template = Templates[(int)kind];
            if (template == null)
            {
                return null;
            }
            GameObject copy = UnityEngine.Object.Instantiate(template, parent, false);
            copy.name = template.name;
            copy.SetActive(true);
            return copy;
        }

        /// <summary>The templates go with the city.</summary>
        public static void Forget()
        {
            for (int i = 0; i < Templates.Length; i++)
            {
                Templates[i] = null;
            }
            if (_asleep != null)
            {
                SceneryTrees.Destroy(_asleep.gameObject);
            }
            _asleep = null;
            Concrete = null;
            Metal = null;
            Wire = null;
        }

        // The first building wearing each piece, unbatched: a piece in a static batch draws the batch's mesh, not its own.
        private static bool FromScene(CityBuildingController[] buildings)
        {
            for (int b = 0; b < buildings.Length && !Ready; b++)
            {
                CityBuildingController building = buildings[b];
                if (building == null || building.cityBuildingParts == null)
                {
                    continue;
                }
                for (int p = 0; p < building.cityBuildingParts.Count; p++)
                {
                    ViewBlockingEntityPart part = building.cityBuildingParts[p];
                    Transform group = part != null ? part.transform.Find(GroupName) : null;
                    if (group == null)
                    {
                        continue;
                    }
                    for (int c = 0; c < group.childCount; c++)
                    {
                        Transform piece = group.GetChild(c);
                        int kind = KindOf(piece.name);
                        if (kind >= 0 && Templates[kind] == null && Unbatched(piece))
                        {
                            Templates[kind] = Asleep(piece.gameObject, Objects[kind]);
                        }
                    }
                }
            }
            return Ready;
        }

        private static int KindOf(string name)
        {
            for (int i = 0; i < Objects.Length; i++)
            {
                if (name == Objects[i] || (name.StartsWith(Objects[i], StringComparison.Ordinal) && name.Length > Objects[i].Length && name[Objects[i].Length] == ' '))
                {
                    return i;
                }
            }
            return -1;
        }

        private static bool Unbatched(Transform piece)
        {
            Renderers.Clear();
            piece.GetComponentsInChildren(true, Renderers);
            bool clean = Renderers.Count > 0;
            for (int i = 0; i < Renderers.Count && clean; i++)
            {
                clean = !Renderers[i].isPartOfStaticBatch;
            }
            Renderers.Clear();
            return clean;
        }

        // A copy kept asleep, with every script taken off: what tied it to its building (the camera's fade, the walking mesh's
        // modifier) is the wrap's to give it again, for the building it then stands on.
        private static GameObject Asleep(GameObject source, string name)
        {
            GameObject copy = UnityEngine.Object.Instantiate(source, _asleep, false);
            copy.name = name;
            copy.transform.localPosition = Vector3.zero;
            copy.transform.localRotation = Quaternion.identity;
            copy.transform.localScale = Vector3.one;
            Components.Clear();
            copy.GetComponentsInChildren(true, Components);
            for (int i = Components.Count - 1; i >= 0; i--)
            {
                if (Components[i] is MonoBehaviour)
                {
                    UnityEngine.Object.DestroyImmediate(Components[i]);
                }
            }
            Components.Clear();
            return copy;
        }

        private static Material Named(Material[] materials, string[] names)
        {
            for (int n = 0; n < names.Length; n++)
            {
                for (int i = 0; i < materials.Length; i++)
                {
                    if (materials[i] != null && materials[i].name == names[n])
                    {
                        return materials[i];
                    }
                }
            }
            return null;
        }

        // The fallback: the pieces built from the game's meshes and materials found by name, with the game's colliders.
        private static void FromMeshes(Material[] materials)
        {
            Material scaffold = Named(materials, new[] { ScaffoldMaterial });
            Material fabric = Named(materials, new[] { FabricMaterial });
            if (scaffold == null || fabric == null)
            {
                return;
            }
            Mesh[] meshes = SceneFinds.InMemory<Mesh>("construction: the game's scaffold meshes by name");
            for (int kind = 0; kind < 3; kind++)
            {
                Mesh mesh = MeshNamed(meshes, Meshes[kind]);
                if (Templates[kind] == null && mesh != null)
                {
                    Templates[kind] = Module(Objects[kind], mesh, scaffold, kind == 0 ? 2f * WrapRules.Unit : WrapRules.Unit);
                }
            }
            for (int kind = 3; kind < 5; kind++)
            {
                if (Templates[kind] != null)
                {
                    continue;
                }
                Mesh lod0 = MeshNamed(meshes, Meshes[kind] + "0"), lod1 = MeshNamed(meshes, Meshes[kind] + "1"), lod2 = MeshNamed(meshes, Meshes[kind] + "2");
                if (lod0 != null && lod1 != null && lod2 != null)
                {
                    Templates[kind] = Fabric(Objects[kind], fabric, kind == 3 ? 2f * WrapRules.Unit : WrapRules.Unit, lod0, lod1, lod2);
                }
            }
        }

        private static Mesh MeshNamed(Mesh[] meshes, string name)
        {
            for (int i = 0; i < meshes.Length; i++)
            {
                if (meshes[i] != null && meshes[i].name == name)
                {
                    return meshes[i];
                }
            }
            return null;
        }

        private static GameObject Module(string name, Mesh mesh, Material material, float length)
        {
            var made = new GameObject(name) { layer = BuildingsLayer };
            made.transform.SetParent(_asleep, false);
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            made.AddComponent<MeshRenderer>().sharedMaterial = material;
            float x = length * 0.5f - PostInset;
            for (int i = 0; i < 4; i++)
            {
                BoxCollider post = made.AddComponent<BoxCollider>();
                post.center = new Vector3(i % 2 == 0 ? -x : x, PostHeight * 0.5f, i < 2 ? -PostRow : PostRow);
                post.size = new Vector3(0.14f, PostHeight, 0.12f);
            }
            return made;
        }

        private static GameObject Fabric(string name, Material material, float length, Mesh lod0, Mesh lod1, Mesh lod2)
        {
            var made = new GameObject(name) { layer = BuildingsLayer };
            made.transform.SetParent(_asleep, false);
            BoxCollider face = made.AddComponent<BoxCollider>();
            face.center = new Vector3(0f, FabricHeight * 0.5f, 0f);
            face.size = new Vector3(length - 0.17f, FabricHeight, 0.33f);
            Mesh[] levels = { lod0, lod1, lod2 };
            var lods = new LOD[levels.Length];
            for (int i = 0; i < levels.Length; i++)
            {
                var level = new GameObject(name + "_LOD" + i) { layer = StreetPropsLayer };
                level.transform.SetParent(made.transform, false);
                level.AddComponent<MeshFilter>().sharedMesh = levels[i];
                var renderer = level.AddComponent<MeshRenderer>();
                renderer.sharedMaterial = material;
                renderer.shadowCastingMode = i < 2 ? ShadowCastingMode.On : ShadowCastingMode.Off;
                lods[i] = new LOD(FabricLods[i], new Renderer[] { renderer });
            }
            made.AddComponent<LODGroup>().SetLODs(lods);
            return made;
        }
    }
}
