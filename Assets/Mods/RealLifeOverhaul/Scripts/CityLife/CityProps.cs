using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The living city's props (brooms, cones, the coffee cart): made on first use from <see cref="PropLibrary"/>'s shared
    /// mesh and material, then shelved switched off, so nothing is built or destroyed as sites come and go. There are no
    /// stand-in shapes: without the modelled prop, Take returns null and the activity does not start.
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

        public int Made => _made;

        /// <summary>A prop switched on at the root; null if the prop library has none by that id.</summary>
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

        /// <summary>Takes a prop and stands it at a point, turned to a heading in degrees.</summary>
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
