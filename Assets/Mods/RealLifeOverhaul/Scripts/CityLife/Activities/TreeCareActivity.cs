using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>A gardener watering or raking at one of the game's street trees, from tested pavement a step from the trunk.</summary>
    public sealed class TreeCareActivity : CityActivity
    {
        private const float Reach = 16f;   // metres
        private const float FromTrunk = 1.15f;   // metres

        private Vector3 _stand, _tree;

        public override bool Find(CitySite site)
        {
            var trees = TreeController.AllTrees;
            TreeController nearest = null;
            float best = Reach * Reach;
            for (int i = 0; i < trees.Count; i++)
            {
                TreeController tree = trees[i];
                if (tree == null || !tree.gameObject.activeInHierarchy)
                {
                    continue;
                }
                float sqr = CitySpots.Flat(tree.transform.position - site.Anchor);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = tree;
                }
            }
            if (nearest == null)
            {
                site.Refusal = CityRefusal.NoTree;
                return false;
            }
            _tree = nearest.transform.position;
            float start = Random.value * 360f;
            for (int i = 0; i < 8; i++)
            {
                Vector3 from = Quaternion.Euler(0f, start + i * 45f, 0f) * Vector3.forward;
                if (CitySpots.Stand(_tree + from * FromTrunk, 0.4f, out _stand) && Mathf.Abs(Mathf.Sqrt(CitySpots.Flat(_stand - _tree)) - FromTrunk) < 0.2f)
                {
                    Centre = _stand;
                    return true;
                }
            }
            return false;
        }

        public override bool Begin(CitySite site)
        {
            Start(site);
            CityPerson gardener = Add(CityLook.Gardener, _stand, _tree);
            if (gardener == null)
            {
                return false;
            }
            if (!gardener.Work(Random.value < 0.5f ? "water_plants" : "rake_leaves", Props, 0f))
            {
                site.Refusal = CityRefusal.NoProp;
                End();
                return false;
            }
            return true;
        }

        public override bool Tick(float now)
        {
            return Lasting();
        }
    }
}
