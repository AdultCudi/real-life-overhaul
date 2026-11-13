using System;
using System.Collections;
using System.Collections.Generic;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The glyphs of a text are in an atlas before the text is built, and a text built without them is built again when they
    /// come. A fallback font takes a character into its atlas when it is first asked for. A text that holds one of a
    /// script the face lacks (Chinese, Korean, Greek, a name a player typed) used to be given its string and built at once:
    /// the Latin letters and the brackets showed and every other character was blank until something wrote the text again
    /// (13 Nov 2026, the BizMan table, blank until the list was scrolled). Now every text the kit sets goes through
    /// <see cref="Need"/> first: the characters of it that no font has been asked for are put into the copies' atlases, in
    /// the chain's order, before the text is handed to TextMeshPro. The kit's text widths never wait on this: a character the
    /// face lacks is measured by a table (<see cref="UiFallbackRules.AdvanceEm"/>), not by the atlas. If a font takes
    /// characters in behind the kit's back (a text's build asked for one the kit never saw: an input field's), or the chain
    /// changes, the texts that hold such characters are built again one frame later (<see cref="Redraw"/>), once for all
    /// of them, never a frame at a time. Cost: a byte read for each character of a text that changed, a hash set for the
    /// texts that hold such a character; on a text's build a comparison of at most eight counts; nothing while the screen
    /// stands as it is.
    /// </summary>
    public static partial class UiFonts
    {
        private static readonly UiGlyphBook Book = new UiGlyphBook();
        private static readonly List<uint> Fresh = new List<uint>(16);
        private static readonly HashSet<TMP_Text> Writing = new HashSet<TMP_Text>();   // the texts that hold a character beyond the face
        private static readonly List<TMP_Text> Gone = new List<TMP_Text>(4);
        private static UiFace _face;
        private const int MostKept = 4096;   // texts kept before the ones that are gone are let go
        private static readonly HashSet<uint> News = new HashSet<uint>();   // the characters the kit put into an atlas since the texts were last built again
        private static bool _hooked, _told, _every;   // _every: the next redraw takes every text, the characters are not known (a font took some in behind the kit's back, or the chain changed)
        private static int _redrawAt = -100;           // the frame a redraw was last asked for: one under way is waited for, a lost one (the director switched off between) is asked again
        private static Action<UnityEngine.Object> _onBuilt;

        /// <summary>
        /// A text is about to be given its string: its characters beyond the face are put into the fallback fonts' atlases first,
        /// and the text is kept among those to be built again if glyphs arrive later. Called by the view for every text it sets.
        /// </summary>
        internal static void Need(TMP_Text t, string text)
        {
            if (t == null)
            {
                return;
            }
            Fresh.Clear();
            if (!Beyond(text))
            {
                if (Writing.Count > 0)
                {
                    Writing.Remove(t);
                }
                return;
            }
            Writing.Add(t);
            if (Writing.Count > MostKept)
            {
                Writing.RemoveWhere(w => w == null);   // hosts of an earlier city: their texts are destroyed
            }
            if (Fresh.Count == 0 || !_gathered || Chain.Count == 0)
            {
                return;   // asked again when the next text holds them, or when the fonts' chain stands
            }
            bool gained = !_standIn && Supply(Fresh);
            Book.Asked(Fresh);
            if (gained)
            {
                for (int i = 0; i < Fresh.Count; i++)
                {
                    News.Add(Fresh[i]);
                }
                Arrived();   // a frame on, in case the atlas shows the new glyphs a moment after the table holds them
            }
        }

        // True when the text holds a character beyond the face; the ones no font has been asked for are left in Fresh.
        private static bool Beyond(string text)
        {
            if (_face == null)
            {
                _face = UiFontMetrics.Face(UiFamily.Overpass, 400);   // every weight of the face draws the same characters
            }
            return _face != null && Book.Beyond(text, _face, Fresh);
        }

        // The characters put into the first copy of the chain that draws them (the chain's order is the language's), one call
        // for all of them; true when an atlas grew.
        private static bool Supply(List<uint> characters)
        {
            uint[] rest = characters.ToArray();
            bool gained = false;
            int none = 0;
            for (int k = 0; k < Chain.Count && rest.Length > 0; k++)
            {
                Link link = LinkOf(Chain[k]);
                if (link == null)
                {
                    continue;
                }
                bool[] drawn;
                if (link.Copy != null)
                {
                    int before = CountOf(link.Copy);
                    drawn = Added(link.Copy, rest);
                    gained |= CountOf(link.Copy) != before;
                }
                else
                {
                    drawn = Drawn(link.Game, rest);
                }
                rest = Without(rest, drawn, link, ref none);
            }
            return gained;
        }

        private static int CountOf(TMP_FontAsset font) => font != null && font.characterTable != null ? font.characterTable.Count : 0;

        // TextMeshPro says nothing when an atlas gains glyphs, but it says when a text has been built: that is the moment a
        // font can have taken some in that the kit did not ask for.
        private static void Hook()
        {
            if (_hooked)
            {
                return;
            }
            _hooked = true;
            _onBuilt = Guard.Wrap<UnityEngine.Object>(TextBuilt);   // inside TextMeshPro's own build of a text: a fault here must never reach it
            TMPro_EventManager.TEXT_CHANGED_EVENT.Add(_onBuilt);
        }

        private static void TextBuilt(UnityEngine.Object text)
        {
            bool grew = false;
            for (int i = 0; i < Links.Count; i++)
            {
                int now = CountOf(Links[i].Copy ?? Links[i].Game);
                if (now != Links[i].Seen)
                {
                    Links[i].Seen = now;
                    grew = true;
                }
            }
            if (grew)
            {
                _every = true;
                Arrived();
            }
        }

        // The chain changed (a copy took the game's font's place): characters are asked of the new fonts and the texts written again.
        private static void Chained()
        {
            Book.Forget();
            _every = true;
            Arrived();
        }

        private static void Arrived()
        {
            UiDirector director = UiDirector.Instance;
            if (Writing.Count == 0 || director == null || !director.isActiveAndEnabled || Time.frameCount - _redrawAt < 3)
            {
                return;
            }
            _redrawAt = Time.frameCount;
            director.StartCoroutine(Guard.Routine(Redraw()));
        }

        // True when the text holds one of the characters the kit put into an atlas since the texts were last built again.
        private static bool HoldsNews(string text)
        {
            for (int i = 0; text != null && i < text.Length; i++)
            {
                uint code = text[i];
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    code = (uint)char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                if (News.Contains(code))
                {
                    return true;
                }
            }
            return false;
        }

        // A frame later, because Arrived can come inside a text's own build and a text is never made dirty inside the canvas's rebuild.
        // Each text that holds a character beyond the face is asked for them again (a new chain forgot them) and built whole.
        private static IEnumerator Redraw()
        {
            yield return null;
            _redrawAt = -100;
            bool every = _every;
            _every = false;
            Gone.Clear();
            int drawn = 0;
            foreach (TMP_Text t in Writing)
            {
                if (t == null)
                {
                    Gone.Add(t);
                    continue;
                }
                if (!t.isActiveAndEnabled || (!every && !HoldsNews(t.text)))
                {
                    continue;   // a text shown again builds itself as it comes on; one without the new characters had its glyphs already
                }
                Fresh.Clear();
                if (Beyond(t.text) && Fresh.Count > 0 && _gathered && Chain.Count > 0)
                {
                    if (!_standIn)
                    {
                        Supply(Fresh);
                    }
                    Book.Asked(Fresh);
                }
                t.ForceMeshUpdate(false, true);
                drawn++;
            }
            News.Clear();
            for (int i = 0; i < Gone.Count; i++)
            {
                Writing.Remove(Gone[i]);
            }
            if (drawn > 0 && !_told)
            {
                _told = true;
                UiDirector.Logger?.Info("UI: the interface's fonts took characters into their atlases while texts that hold them were on screen; " + drawn
                                        + " text(s) were built again so none is left blank (said once; the language is " + (_language ?? "not known yet") + ").");
            }
        }
    }
}
