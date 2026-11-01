using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Hides the game's own pieces of a screen the kit draws over, and gives them back exactly as found. A leaf goes
    /// unseen and untouchable through a CanvasGroup (its own if it has one, else one added once and kept): the group's
    /// alpha, interactable and blocksRaycasts are read when hiding starts and written back when it ends, since another
    /// mod may have set them. Nothing is switched off, so every activeInHierarchy, IsOpen and IsPanelOpen another mod reads
    /// stays true. A shared parent never gets a group (UiHostRules.SharedParents, or one the screen names): only a
    /// graphic of its own is hidden, by its renderer's alpha, which no copy of it carries. Cost: nothing a frame; a few
    /// writes when a screen shows or hides.
    /// </summary>
    public sealed class UiConceal
    {
        private struct Leaf
        {
            public CanvasGroup Group;
            public float Alpha;
            public bool Interactable, Blocks;
            public bool KeepBlocking;   // a backdrop that holds clicks off the world under it keeps doing so
        }

        private struct Dimmed
        {
            public CanvasRenderer Renderer;
            public float Alpha;
        }

        private readonly List<Leaf> _leaves = new List<Leaf>(8);
        private readonly List<Dimmed> _graphics = new List<Dimmed>(8);
        private readonly List<Transform> _shared = new List<Transform>(4);
        private bool _hiding;

        /// <summary>True between Hide and Show.</summary>
        public bool Hiding => _hiding;

        /// <summary>How many leaves and graphics it holds, for the self-check line.</summary>
        public int Count => _leaves.Count + _graphics.Count;

        /// <summary>A parent of the game's that other mods fill, named by the screen: it is never given a group.</summary>
        public void Shared(Transform parent)
        {
            if (parent != null && !_shared.Contains(parent))
            {
                _shared.Add(parent);
            }
        }

        /// <summary>A leaf to hide; false (and not held) when it is a shared parent. Taken while hiding, it hides at once.</summary>
        public bool Add(Transform leaf, bool keepBlocking = false)
        {
            if (leaf == null || UiHostRules.Shared(leaf.name) || _shared.Contains(leaf))
            {
                return false;
            }
            for (int i = 0; i < _leaves.Count; i++)
            {
                if (_leaves[i].Group != null && _leaves[i].Group.transform == leaf)
                {
                    return true;
                }
            }
            CanvasGroup group = leaf.GetComponent<CanvasGroup>();
            if (group == null)
            {
                group = leaf.gameObject.AddComponent<CanvasGroup>();
            }
            var held = new Leaf { Group = group, KeepBlocking = keepBlocking };
            if (_hiding)
            {
                Take(ref held);
            }
            _leaves.Add(held);
            return true;
        }

        /// <summary>A graphic standing on a shared parent (the parent's own picture): hidden by its renderer alone, its children untouched.</summary>
        public void Add(Graphic graphic)
        {
            if (graphic == null)
            {
                return;
            }
            CanvasRenderer r = graphic.canvasRenderer;
            for (int i = 0; i < _graphics.Count; i++)
            {
                if (_graphics[i].Renderer == r)
                {
                    return;
                }
            }
            var held = new Dimmed { Renderer = r };
            if (_hiding)
            {
                held.Alpha = r.GetAlpha();
                r.SetAlpha(0f);
            }
            _graphics.Add(held);
        }

        /// <summary>Everything held goes unseen; what each was is kept.</summary>
        public void Hide()
        {
            if (_hiding)
            {
                return;
            }
            _hiding = true;
            for (int i = 0; i < _leaves.Count; i++)
            {
                Leaf l = _leaves[i];
                Take(ref l);
                _leaves[i] = l;
            }
            for (int i = 0; i < _graphics.Count; i++)
            {
                Dimmed d = _graphics[i];
                if (d.Renderer != null)
                {
                    d.Alpha = d.Renderer.GetAlpha();
                    d.Renderer.SetAlpha(0f);
                }
                _graphics[i] = d;
            }
        }

        /// <summary>Everything held back exactly as it was found.</summary>
        public void Show()
        {
            if (!_hiding)
            {
                return;
            }
            _hiding = false;
            for (int i = 0; i < _leaves.Count; i++)
            {
                Leaf l = _leaves[i];
                if (l.Group != null)
                {
                    l.Group.alpha = l.Alpha;
                    l.Group.interactable = l.Interactable;
                    l.Group.blocksRaycasts = l.Blocks;
                }
            }
            for (int i = 0; i < _graphics.Count; i++)
            {
                if (_graphics[i].Renderer != null)
                {
                    _graphics[i].Renderer.SetAlpha(_graphics[i].Alpha);
                }
            }
        }

        /// <summary>Forgets what the game has destroyed since (a toast gone): nothing to give back to it.</summary>
        public void Prune()
        {
            for (int i = _leaves.Count - 1; i >= 0; i--)
            {
                if (_leaves[i].Group == null)
                {
                    _leaves.RemoveAt(i);
                }
            }
            for (int i = _graphics.Count - 1; i >= 0; i--)
            {
                if (_graphics[i].Renderer == null)
                {
                    _graphics.RemoveAt(i);
                }
            }
        }

        /// <summary>Given back and forgotten: a new city, or the screen gone.</summary>
        public void Clear()
        {
            Show();
            _leaves.Clear();
            _graphics.Clear();
            _shared.Clear();
        }

        private static void Take(ref Leaf l)
        {
            if (l.Group == null)
            {
                return;
            }
            l.Alpha = l.Group.alpha;
            l.Interactable = l.Group.interactable;
            l.Blocks = l.Group.blocksRaycasts;
            l.Group.alpha = 0f;
            l.Group.interactable = false;
            l.Group.blocksRaycasts = l.KeepBlocking && l.Blocks;
        }
    }
}
