using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The prop objects of the living city: brooms, cones, the coffee cart. An object is made
    /// the first time a prop is wanted (one GameObject, the shared mesh and shared material from
    /// <see cref="PropLibrary"/>, no collider), handed out, and put back on the shelf switched
    /// off when its site ends, so nothing is built or destroyed while sites come and go.
    /// There are no plain stand-in shapes here: if the modelled prop cannot be had, Take says
    /// no and the activity that wanted it does not start. A worker with a box for a broom is
    /// worse than no worker.
    /// </summary>
    public sealed class CityProps
    {
        private readonly Dictionary<string, Stack<Transform>> _shelf = new Dictionary<string, Stack<Transform>>(16);
        private readonly Transform _root;
        private int _made;

        public CityProps(Transform parent)
        {
            _root = new GameObject("RealLifeOverhaul City Props").transform;
            _root.SetParent(parent, false);
        }

        /// <summary>How many prop objects exist, in use or on the shelf.</summary>
        public int Made => _made;

        /// <summary>A prop object, switched on, at the root. Null if the prop library has no such prop (yet).</summary>
        public Transform Take(string id)
        {
            if (_shelf.TryGetValue(id, out Stack<Transform> spare) && spare.Count > 0)
            {
                Transform again = spare.Pop();
                if (again != null)
                {
                    again.gameObject.SetActive(true);
                    return again;
                }
            }
            if (!PropLibrary.TryGet(id, out Mesh mesh, out Material material))
            {
                return null;
            }
            var made = new GameObject(id);
            made.transform.SetParent(_root, false);
            made.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = made.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.lightProbeUsage = UnityEngine.Rendering.LightProbeUsage.Off;
            _made++;
            return made.transform;
        }

        /// <summary>Stands a prop on the ground at a point, turned about the upright axis.</summary>
        public Transform Stand(string id, Vector3 position, float heading)
        {
            Transform prop = Take(id);
            if (prop != null)
            {
                prop.SetPositionAndRotation(position, Quaternion.Euler(0f, heading, 0f));
            }
            return prop;
        }

        /// <summary>Back on the shelf, switched off. Safe with null and with a prop that has been destroyed.</summary>
        public void Return(Transform prop)
        {
            if (prop == null || (prop.parent == _root && !prop.gameObject.activeSelf))
            {
                return;   // gone, or already on the shelf: never twice
            }
            if (prop.TryGetComponent(out Renderer renderer))
            {
                renderer.enabled = true;   // whoever hid it with their own body is done with it
            }
            prop.SetParent(_root, false);
            prop.localScale = Vector3.one;
            prop.gameObject.SetActive(false);
            if (!_shelf.TryGetValue(prop.name, out Stack<Transform> spare))
            {
                spare = new Stack<Transform>(4);
                _shelf[prop.name] = spare;
            }
            spare.Push(prop);
        }
    }
}
