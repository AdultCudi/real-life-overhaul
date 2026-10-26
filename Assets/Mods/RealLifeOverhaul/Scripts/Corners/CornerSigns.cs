using System;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul.Corners
{
    /// <summary>One sign anchor of a standing corner building: its letters, and whether the business's own door sign covers it.</summary>
    internal sealed class CornerText
    {
        public CornerSign Sign;
        public TextMeshPro Text;
        public MeshRenderer Renderer;
        public Material Glow;          // a neon sign's own glowing material, made once and let go with it
        public bool Covered;           // the business's own door sign stands over it: left blank
        public string Shown;           // what it says now, null for a blank board
    }

    /// <summary>
    /// The corner buildings' live signs (the owner, 26 Oct 2026: "make them live business signs. Whoever buys that can keep their
    /// own signs"): on each anchor of the model the name of the business trading there in the game's own font, a fascia and a frieze
    /// in the business's colour (else the game's colour for its sign) on its board, the neon glowing, the blade on both its sides,
    /// the address plate the building's number; an empty unit's boards say "For lease" while it is to let, else stay blank. The
    /// business's own door sign of the game's stands at the entrance as the game draws it. Written as the building stands and again
    /// on the game's own registration change event for its address (a business opened, renamed or closed, a tenant come or gone):
    /// once an event, never per frame.
    /// </summary>
    internal static class CornerSigns
    {
        private const float FontFill = 3.4f;        // TextMeshPro's world size a metre of a board's height takes, the board full (the game's world signs' ratio)
        private const float FontLeast = 0.25f;      // ... the least share of that a long name is shrunk to before it is cut short
        private const float GlowBright = 3f;        // a neon sign's letters' brightness over its colour, so the game's exposure reads it as lit
        private static readonly int FaceColor = Shader.PropertyToID("_FaceColor");
        private static readonly int GlowColor = Shader.PropertyToID("_GlowColor");
        private static readonly int GlowPower = Shader.PropertyToID("_GlowPower");
        private static readonly int GlowOuter = Shader.PropertyToID("_GlowOuter");

        private static Action<Address> _onChange, _changeSeen;

        /// <summary>The signs' objects of a corner building, one a sign anchor, made with its model (so the game's fade and the city map switch them with it).</summary>
        public static void Prepare(CornerHandle h)
        {
            h.Texts.Clear();
            CornerSign[] signs = h.Model.Signs ?? new CornerSign[0];
            TMP_FontAsset font = GameFont.Asset;
            for (int i = 0; i < signs.Length; i++)
            {
                CornerSign s = signs[i];
                var go = new GameObject("sign " + i + " " + s.Kind) { layer = h.Root.layer };
                go.transform.SetParent(h.Root.transform, false);
                go.transform.localPosition = new Vector3(s.X + s.OutX * CornerRules.SignOut, s.Y, s.Z + s.OutZ * CornerRules.SignOut);
                go.transform.localRotation = Quaternion.LookRotation(new Vector3(-s.OutX, 0f, -s.OutZ), Vector3.up);   // world-space letters read from the side their back is turned to
                TextMeshPro text = go.AddComponent<TextMeshPro>();
                if (font != null)
                {
                    text.font = font;
                }
                text.alignment = TextAlignmentOptions.Center;
                text.textWrappingMode = TextWrappingModes.NoWrap;
                text.overflowMode = TextOverflowModes.Ellipsis;
                text.enableAutoSizing = true;
                text.fontSizeMax = s.Height * FontFill;
                text.fontSizeMin = s.Height * FontFill * FontLeast;
                text.rectTransform.sizeDelta = new Vector2(s.Width, s.Height);
                text.text = string.Empty;
                MeshRenderer renderer = go.GetComponent<MeshRenderer>();
                if (renderer != null)
                {
                    renderer.shadowCastingMode = ShadowCastingMode.Off;
                }
                h.Texts.Add(new CornerText { Sign = s, Text = text, Renderer = renderer });
            }
        }

        /// <summary>The signs written for the first time, once the building's own door sign has been carried to the entrance: those it covers stay blank.</summary>
        public static void Make(CornerHandle h)
        {
            for (int i = 0; i < h.Texts.Count; i++)
            {
                CornerText t = h.Texts[i];
                t.Covered = h.SignCarried && CornerRules.Covered(t.Sign, h.SignX, h.SignY, h.SignZ, h.Model.OutX, h.Model.OutZ, h.SignWidth, h.SignHeight);
            }
            Write(h);
        }

        /// <summary>Each sign written from the building's registration now: the business's name, "For lease", the number, or a blank board.</summary>
        public static void Write(CornerHandle h)
        {
            if (h == null || h.Made == null)
            {
                return;
            }
            BuildingRegistration reg = h.Made.Registration;
            bool business = reg != null && !string.IsNullOrEmpty(reg.BusinessName) && reg.businessTypeName != "ba:businesstype_empty";
            bool toLet = reg != null && reg.AvailableForRent;
            int number = reg != null ? reg.StreetNumber : h.Made.Spec != null ? h.Made.Spec.Number : 0;
            Color own = reg != null && reg.logoSettings != null ? (Color)reg.logoSettings.fontColor : Color.white;
            Color light = reg != null && reg.signAppearanceSettings != null ? (Color)reg.signAppearanceSettings.signLight : Color.white;
            string lease = CornersText.T(CornersText.Lease);
            h.SignsDrawn = 0;
            h.SignsBlank = 0;
            for (int i = 0; i < h.Texts.Count; i++)
            {
                CornerText t = h.Texts[i];
                if (t.Text == null)
                {
                    continue;
                }
                int shows = t.Covered ? CornerRules.ShowsNothing : CornerRules.Shows(t.Sign.Kind, business, toLet);
                string words = CornerRules.Words(t.Sign.Kind, shows, reg != null ? reg.BusinessName : null, lease, number);
                CornerRules.Colour(t.Sign.Kind, own.r, own.g, own.b, light.r, light.g, light.b, out float r, out float g, out float b);
                var colour = new Color(r, g, b, 1f);
                t.Shown = words;
                t.Text.text = words ?? string.Empty;
                t.Text.color = colour;
                if (t.Sign.Kind == SignKind.Neon)
                {
                    Glow(t, colour);
                }
                t.Text.enabled = words != null;
                h.SignsDrawn += words != null ? 1 : 0;
                h.SignsBlank += words == null ? 1 : 0;
            }
        }

        // A neon sign's letters lit in their colour: its own material, made once (TextMeshPro's instance of the font's), its face and glow set.
        private static void Glow(CornerText t, Color colour)
        {
            if (t.Glow == null)
            {
                t.Glow = t.Text.fontMaterial;   // the text's own instance of its font's material, made once here
                t.Glow.EnableKeyword("GLOW_ON");
            }
            t.Glow.SetColor(FaceColor, new Color(colour.r * GlowBright, colour.g * GlowBright, colour.b * GlowBright, 1f));
            t.Glow.SetColor(GlowColor, new Color(colour.r, colour.g, colour.b, 0.6f));
            t.Glow.SetFloat(GlowPower, 0.6f);
            t.Glow.SetFloat(GlowOuter, 0.3f);
        }

        /// <summary>The signs hidden or shown with the model.</summary>
        public static void Show(CornerHandle h, bool shown)
        {
            for (int i = 0; i < h.Texts.Count; i++)
            {
                if (h.Texts[i].Renderer != null)
                {
                    h.Texts[i].Renderer.forceRenderingOff = !shown;
                }
            }
        }

        /// <summary>A corner building's signs let go: the neon's own materials.</summary>
        public static void Drop(CornerHandle h)
        {
            for (int i = 0; i < h.Texts.Count; i++)
            {
                if (h.Texts[i].Glow != null)
                {
                    UnityEngine.Object.Destroy(h.Texts[i].Glow);
                    h.Texts[i].Glow = null;
                }
            }
            h.Texts.Clear();
        }

        /// <summary>
        /// The handler kept on the game's registration change event (emptied by the game at each load): put back each time a corner
        /// building stands, looked at only when the event's list changed (EventHold).
        /// </summary>
        public static void Listen()
        {
            _onChange ??= Guard.Wrap<Address>(OnChange);   // made once: a fault here never reaches the game's own handlers
            EventHold.Keep(ref GlobalEvents.onBuildingRegistrationChange, _onChange, ref _changeSeen);
        }

        // A registration changed: a corner building's at that address writes its signs again.
        private static void OnChange(Address address)
        {
            if (address == null)
            {
                return;
            }
            CornerHandle h = CornerLook.AtAddress(address.streetName, address.streetNumber);
            if (h != null && h.Root != null)
            {
                Write(h);
            }
        }
    }
}
