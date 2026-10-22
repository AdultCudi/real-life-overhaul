using System;
using System.Collections.Generic;
using System.Reflection;
using System.Runtime.CompilerServices;
using BAModAPI;
using Localizor;
using Localizor.LanguageChangeEvent;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>On an app's panel: tells the app when the panel comes on and goes off. Costs nothing a frame.</summary>
    public sealed class PhoneAppRoot : MonoBehaviour
    {
        internal PhoneApp App;

        private void OnEnable()
        {
            App?.Shown();
        }

        private void OnDisable()
        {
            var group = GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.alpha = 1f;
            }
            App?.Hidden();
        }

        /// <summary>Fades the panel in over the game's tenth of a second, as FullMenu.SelectApp does.</summary>
        internal void FadeIn(float seconds)
        {
            var group = GetComponent<CanvasGroup>();
            if (group == null)
            {
                return;
            }
            if (seconds <= 0f)
            {
                group.alpha = 1f;
                return;
            }
            PanelFade fade = GetComponent<PanelFade>() ?? gameObject.AddComponent<PanelFade>();
            fade.Begin(group, seconds);
        }
    }

    /// <summary>A fade of one canvas group on the unscaled clock (the menu pauses the game). Runs a frame only while it lasts.</summary>
    public sealed class PanelFade : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PanelFade", typeof(PanelFade));   // its Update

        private CanvasGroup _group;
        private float _from, _for;

        internal void Begin(CanvasGroup group, float seconds)
        {
            _group = group;
            _from = Time.unscaledTime;
            _for = seconds;
            group.alpha = 0f;
            enabled = true;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Core: PanelFade.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float t = _group != null && _for > 0f ? (Time.unscaledTime - _from) / _for : 1f;
            if (_group != null)
            {
                _group.alpha = Mathf.Clamp01(t);
            }
            if (t >= 1f)
            {
                enabled = false;
            }
        }

        private void OnDisable()
        {
            if (_group != null)
            {
                _group.alpha = 1f;
            }
            enabled = false;
        }
    }
}
