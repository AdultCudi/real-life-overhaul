using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using Helpers;
using UnityEngine;
using Object = UnityEngine.Object;

namespace RealLifeOverhaul
{
    /// <summary>What an accident or a unit wears: the police car's look, borrowed from the game's parked police car prop.</summary>
    public static partial class TrafficEffects
    {
        private const string PoliceCarPrefab = "Vehicles/ParkedVehicles/PoliceCar";   // the game's parked police car prop

        /// <summary>Dresses a traffic car as a police car with a copy of the game's parked police car prop; it undoes itself as the car goes. Null if none.</summary>
        public static GameObject CreatePoliceBody(VehicleComponent car)
        {
            GameObject template = GetPoliceTemplate();
            if (template == null || !TryGetBounds(car.gameObject, out Bounds host))
            {
                return null;
            }

            // Collected before the copy exists, so only the car's own renderers are hidden.
            car.GetComponentsInChildren(true, ComponentScratch);

            GameObject body = Object.Instantiate(template, car.transform);
            body.name = "RealLifeOverhaul Police Body";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.SetActive(true);
            List<Renderer> hidden = body.AddComponent<PoliceBody>().Hidden;

            // The prop must be car-sized, or something about it is not what we expect.
            if (!TryGetBounds(body, out Bounds own) || Mathf.Max(own.size.x, own.size.z) < 2.5f || Mathf.Max(own.size.x, own.size.z) > 9f)
            {
                Object.Destroy(body);
                ComponentScratch.Clear();
                return null;
            }

            // Line the prop up with the car: centred on it, wheels on the same ground.
            var shift = new Vector3(host.center.x - own.center.x, host.min.y - own.min.y, host.center.z - own.center.z);
            if (shift.sqrMagnitude < 4f)
            {
                body.transform.position += shift;
            }

            for (int i = 0; i < ComponentScratch.Count; i++)
            {
                if (ComponentScratch[i] is Renderer renderer && !renderer.forceRenderingOff)
                {
                    renderer.forceRenderingOff = true;
                    hidden.Add(renderer);
                }
            }
            ComponentScratch.Clear();
            if (hidden.Count == 0)
            {
                Object.Destroy(body);
                return null;
            }
            return body;
        }

        /// <summary>A copy of the game's parked police car, meshes only, on its unturned parent's origin and facing its way. Null if the game has none.</summary>
        /// <param name="size">Its width, height and length in metres.</param>
        public static GameObject CreateParkedPatrol(Transform parent, out Vector3 size)
        {
            size = default;
            GameObject template = GetPoliceTemplate();
            if (template == null || parent == null)
            {
                return null;
            }
            GameObject body = Object.Instantiate(template, parent);
            body.name = "RealLifeOverhaul Parked Patrol Car";
            body.transform.localPosition = Vector3.zero;
            body.transform.localRotation = Quaternion.identity;
            body.SetActive(true);
            if (!TryGetBounds(body, out Bounds own) || Mathf.Max(own.size.x, own.size.z) < 2.5f || Mathf.Max(own.size.x, own.size.z) > 9f)
            {
                Object.Destroy(body);
                return null;
            }
            Vector3 origin = parent.position;
            body.transform.position += new Vector3(origin.x - own.center.x, origin.y - own.min.y, origin.z - own.center.z);
            size = own.size;
            return body;
        }

        // The game's police car is a parked prop, not a traffic car: loaded once and stripped to its meshes.
        private static GameObject GetPoliceTemplate()
        {
            if (_policeSearched)
            {
                return _policeTemplate;
            }
            _policeSearched = true;
            try
            {
                GameObject prefab = PrefabHelper.LoadPrefabAssetByName(PoliceCarPrefab);
                if (prefab == null)
                {
                    Logger?.Info("Police: the game's police car prop was not found; no patrol cars can be sent.");
                    return null;
                }
                _policeTemplate = Object.Instantiate(prefab, Holder);
                _policeTemplate.name = "RealLifeOverhaul Police Template";
                StripToVisuals(_policeTemplate);
                Logger?.Info("Police: using the game's police car prop for patrol cars.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                if (_policeTemplate != null)
                {
                    Object.Destroy(_policeTemplate);
                }
                _policeTemplate = null;
            }
            return _policeTemplate;
        }

        private static void StripToVisuals(GameObject root)
        {
            // Scripts first, since they may require the components removed after them.
            root.GetComponentsInChildren(true, ComponentScratch);
            for (int i = 0; i < ComponentScratch.Count; i++)
            {
                if (ComponentScratch[i] is MonoBehaviour script)
                {
                    Object.DestroyImmediate(script);
                }
            }
            // Twice, in case one component could not go before another that depended on it.
            for (int pass = 0; pass < 2; pass++)
            {
                root.GetComponentsInChildren(true, ComponentScratch);
                for (int i = 0; i < ComponentScratch.Count; i++)
                {
                    Component component = ComponentScratch[i];
                    if (component != null && !(component is Transform) && !(component is MeshFilter)
                        && !(component is Renderer) && !(component is LODGroup))
                    {
                        Object.DestroyImmediate(component);
                    }
                }
            }
            ComponentScratch.Clear();
        }

        /// <summary>Height of the car's roof above its origin, measured from its meshes while it stands upright.</summary>
        public static float RoofHeight(VehicleComponent car)
        {
            float height = TryGetBounds(car.gameObject, out Bounds bounds) ? bounds.max.y - car.transform.position.y : 0f;
            return height > 1f && height < 3.5f ? height : Mathf.Max(1.4f, car.coliderHeight);
        }

        private static bool TryGetBounds(GameObject root, out Bounds bounds)
        {
            bounds = default;
            bool any = false;
            root.GetComponentsInChildren(false, MeshScratch);
            for (int i = 0; i < MeshScratch.Count; i++)
            {
                MeshRenderer renderer = MeshScratch[i];
                if (!renderer.enabled || renderer.forceRenderingOff)
                {
                    continue;
                }
                if (any)
                {
                    bounds.Encapsulate(renderer.bounds);
                }
                else
                {
                    bounds = renderer.bounds;
                    any = true;
                }
            }
            MeshScratch.Clear();
            return any;
        }
    }
}
