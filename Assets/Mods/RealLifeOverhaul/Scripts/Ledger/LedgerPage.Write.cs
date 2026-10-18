using System.Collections.Generic;
using System.Globalization;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using TMPro;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    /// <summary>The paper's page: its type, and the parts every page is written with.</summary>
    public sealed partial class LedgerPage
    {
        // The type, in fractions of the sheet's height (some 1400 canvas units on a 1080p screen).
        private const float MarginX = 0.034f, MarginY = 0.02f;
        private const float BodyStart = 0.0205f, BodyLeast = 0.019f, BodyMost = 0.0255f;          // the front page's columns
        private const float MarketStart = 0.0195f, MarketLeast = 0.0175f, MarketMost = 0.0245f;   // the markets page's
        private const float HeadStart = 0.058f, HeadMost = 0.08f, HeadLeast = 0.046f;             // the lead's headline
        private const float BoxSize = 0.0168f, RightSize = 0.019f, PricesMost = 0.022f;
        private const float BandLeast = 0.13f, BandMost = 0.3f, BandGap = 0.02f;                   // the index's chart under the columns
        // Spacing in em/100, as TextMeshPro takes it: lines a little closer than the face's, items a paragraph apart.
        private const float BodySpacing = -14f, HeadSpacing = -24f, ItemSpacing = 85f, TableSpacing = -18f;
        private const float Spread = 0.15f;     // the part of a justified line's slack that goes between letters, the rest between words
        private const float MarkSize = 0.6f;    // the up and down marks, at this much of the type they stand in
        private const int FrontColumns = 4, MarketColumns = 3, PriceColumns = 5;
        private const int StoriesMost = 3;      // stories under a headline in a section of the front page; the rest are briefs
        private const int MarketStories = 9;
        private const int BoxMoversMost = 6, BoxMoversLeast = 3, MoversLeast = 5, MoversMost = 12;
        private const char VT = '\v';           // a line break inside an item: no paragraph spacing before the next line

        private const string Up = "\u25B2", Down = "\u25BC";   // the move's marks

        private void Dateline(TMP_Text t, string words, float x, float y, float w, float h, float size)
        {
            t.fontSize = size;
            Set(t, "<smallcaps><cspace=0.06em>" + Plain(words) + "</cspace></smallcaps>");
            Put(t.rectTransform, x, y + TopFor(h, size), w, h);
        }

        // A lead's head (kicker, headline in two lines at most, standfirst if there is a text for it, byline): placed,
        // or only measured. Returns where the columns under it start.
        private float Header(TMP_Text kickerText, TMP_Text headText, TMP_Text standText, TMP_Text bylineText, string kicker, string headline, string stand, string byline,
            float bt, float mw, float hs, float u, bool place)
        {
            float y = bt - 0.003f * u;
            float ks = 0.0148f * u;
            kickerText.fontSize = ks;
            if (place)
            {
                Set(kickerText, "<b><smallcaps><cspace=0.16em>" + Plain(kicker) + "</cspace></smallcaps></b>");
                Put(kickerText.rectTransform, 0f, y, mw, ks * 1.6f);
            }
            y += ks * (_asc + _desc) - 0.004f * u;
            string head = Plain(headline);
            float size = hs, least = Mathf.Min(HeadLeast * u, hs * 0.78f);
            while (size > least && Lines(headText, head, mw, size, HeadSpacing) > 2)
            {
                size -= 0.002f * u;
            }
            headText.fontSize = size;
            float h = Tall(headText, head, mw);
            if (place)
            {
                Set(headText, head);
                Put(headText.rectTransform, 0f, y, mw, h + 2f);
            }
            y += h + 0.005f * u;
            if (standText != null)
            {
                bool any = !string.IsNullOrEmpty(stand);
                if (any)
                {
                    standText.fontSize = 0.0232f * u * Mathf.Sqrt(size / (HeadStart * u));
                    string words = "<i angle=12>" + Plain(stand) + "</i>";
                    float sh = Tall(standText, words, mw * 0.7f);
                    if (place)
                    {
                        Set(standText, words);
                        Put(standText.rectTransform, 0f, y, mw * 0.7f, sh + 2f);
                    }
                    y += sh + 0.003f * u;
                }
                if (place)
                {
                    Show(standText, any);
                }
            }
            float bs = 0.0136f * u;
            bylineText.fontSize = bs;
            if (place)
            {
                Set(bylineText, "<smallcaps><cspace=0.12em>" + Plain(byline) + "</cspace></smallcaps>");
                Put(bylineText.rectTransform, 0f, y, mw, bs * 1.6f);
            }
            return y + bs * (_asc + _desc) + 0.011f * u;
        }

        // A section in columns: its label over a rule (none when it goes on from the lead), its stories, its briefs.
        private void Section(string labelKey, List<LedgerPiece> pieces, int most, bool label)
        {
            if (pieces.Count == 0)
            {
                return;
            }
            if (label)
            {
                _b.Append(MarkRule).Append(MarkKeep).Append("<align=left>");
                Label(LedgerText.T(labelKey), "84%");
                _b.Append("</align>").Append(VT);
            }
            int told = 0;
            string name = LedgerText.T(labelKey);
            for (int i = 0; i < pieces.Count; i++)
            {
                LedgerText.Words(pieces[i], out string kicker, out string headline, out string body);
                if (kicker != null && string.Equals(Sentence(kicker), name, System.StringComparison.CurrentCultureIgnoreCase))
                {
                    kicker = null;   // "BUSINESS:" under the business label says it twice
                }
                if (headline != null && told < most)
                {
                    told++;
                    if (i > 0 || !label)
                    {
                        _b.Append(MarkCut);
                    }
                    if (!(label && i == 0))
                    {
                        _b.Append(MarkKeep);
                    }
                    if (kicker != null)
                    {
                        _b.Append("<align=left><size=78%><smallcaps><cspace=0.1em>").Append(Plain(Sentence(kicker))).Append("</cspace></smallcaps></size></align>").Append(VT);
                    }
                    _b.Append("<align=left><size=120%><b><uppercase>").Append(Plain(headline)).Append("</uppercase></b></size></align>").Append(VT);
                    _b.Append(MarkEnd).Append(Plain(body)).Append('\n');
                }
                else
                {
                    if (label && i == 0)
                    {
                        _b.Append(MarkEnd);
                    }
                    Brief(kicker, headline, body);
                }
            }
        }

        private void Margins(RectTransform page, float u, out float W, out float H)
        {
            page.offsetMin = new Vector2(MarginX * u, MarginY * u);
            page.offsetMax = new Vector2(-MarginX * u, -MarginY * u);
            Rect r = page.rect;
            W = r.width;
            H = r.height;
        }

        // Where a one-line text's box starts for its capitals to sit in the middle of a band so deep.
        private float TopFor(float band, float size)
        {
            return (band - _cap * size) * 0.5f - (_asc - _cap) * size;
        }

        // A label: small capitals in bold, a little apart, at so much of the type.
        private void Label(string words, string size)
        {
            _b.Append("<size=").Append(size).Append("><b><smallcaps><cspace=0.14em>").Append(Plain(words)).Append("</cspace></smallcaps></b></size>");
        }

        private void Brief(LedgerPiece piece)
        {
            LedgerText.Words(piece, out string kicker, out string headline, out string body);
            Brief(kicker, headline, body);
        }

        private void Brief(string kicker, string headline, string body)
        {
            string lead = kicker != null ? Sentence(kicker) : headline;
            if (lead != null)
            {
                _b.Append("<b>").Append(Plain(lead)).Append(kicker != null ? ":" : ".").Append("</b> ");
            }
            _b.Append(Plain(body)).Append('\n');
        }

        // A row of a table: the name (smaller if it would run into the figures), then the figure and the move, each
        // right-aligned at a stop the same in every row, in even figures.
        private void Row(string name, string marks, float width, float size, TMP_Text measure, string figure, int moveBp, bool move)
        {
            float mono = _figure * size;
            float right = width - 2f * _px;
            float percent = _percent * size;
            float mark = _marks ? (_mark + 0.18f) * size : 0f;
            float moveField = move ? mark + Even("+00.00", mono) + percent : 0f;
            float figureLeft = figure != null ? right - (move ? moveField + 0.55f * size : 0f) - Even(figure, mono) : right - moveField;
            float room = figureLeft - 0.5f * size;
            string named = Plain(name) + marks;
            float wide = 0f;
            if (measure != null)
            {
                measure.fontSize = size;
                wide = measure.GetPreferredValues(named).x;
            }
            _b.Append("<align=left>");
            if (wide > room && wide > 1f)
            {
                _b.Append("<size=").Append(Mathf.Max(70, Mathf.FloorToInt(100f * room / wide)).ToString(CultureInfo.InvariantCulture)).Append("%>").Append(named).Append("</size>");
            }
            else
            {
                _b.Append(named);
            }
            if (figure != null)
            {
                Pos(figureLeft);
                EvenFigures(figure, mono);
            }
            if (move)
            {
                string change = LedgerText.Change(moveBp);
                bool sign = change.EndsWith("%", System.StringComparison.Ordinal);
                if (sign)
                {
                    change = change.Substring(0, change.Length - 1);
                }
                float at = right - (sign ? percent : 0f) - Even(change, mono);
                if (_marks && moveBp != 0)
                {
                    Pos(at - mark);
                    _b.Append("<size=").Append(Pct(MarkSize)).Append('>').Append(moveBp > 0 ? Up : Down).Append("</size>");
                }
                Pos(at);
                EvenFigures(change, mono);
                if (sign)
                {
                    _b.Append('%');
                }
            }
            _b.Append("</align>");
        }

        // The width of figures set even: every figure and sign one width, a stop, comma or colon half of it.
        private static float Even(string figure, float mono)
        {
            float w = 0f;
            for (int i = 0; i < figure.Length; i++)
            {
                char c = figure[i];
                w += c == '.' || c == ',' || c == ':' ? mono * 0.5f : mono;
            }
            return w;
        }

        private void EvenFigures(string figure, float mono)
        {
            _b.Append("<mspace=").Append(F(mono)).Append(" duospace=1>").Append(Plain(figure)).Append("</mspace>");
        }

        private void Pos(float x)
        {
            _b.Append("<pos=").Append(F(x)).Append('>');
        }

        private string Move(int bp)
        {
            string figure = LedgerText.Change(bp);
            return bp != 0 && _marks ? "<size=" + Pct(MarkSize) + ">" + (bp > 0 ? Up : Down) + "</size> " + figure : figure;
        }

        private static string Pct(float fraction)
        {
            return Mathf.RoundToInt(fraction * 100f).ToString(CultureInfo.InvariantCulture) + "%";
        }

        private static string F(float v)
        {
            return v.ToString("0.##", CultureInfo.InvariantCulture);
        }

        // What has been written, without a last line break (which would be one more, empty, line).
        private string Words()
        {
            int end = _b.Length;
            while (end > 0 && (_b[end - 1] == '\n' || _b[end - 1] == VT))
            {
                end--;
            }
            return _b.ToString(0, end);
        }

        // The lead's text after its drop cap (its first letter), the rest of its first word or two in small capitals.
        private static string AfterCap(string text)
        {
            string rest = text.Substring(1);
            int one = rest.IndexOf(' ');
            int two = one >= 0 ? rest.IndexOf(' ', one + 1) : -1;
            int end = two > 0 && two <= 22 ? two : one > 0 && one <= 22 ? one : Mathf.Min(rest.Length, 22);
            return "<smallcaps>" + Plain(rest.Substring(0, end)) + "</smallcaps>" + Plain(rest.Substring(end));
        }

        // A kicker as the page prints it, in small capitals: "COURT" becomes "Court".
        internal static string Sentence(string kicker)
        {
            if (string.IsNullOrEmpty(kicker))
            {
                return kicker;
            }
            string lower = CultureInfo.CurrentCulture.TextInfo.ToLower(kicker);
            return char.ToUpper(lower[0], CultureInfo.CurrentCulture) + lower.Substring(1);
        }

        internal static string BylineKey(LedgerSection section)
        {
            switch (section)
            {
                case LedgerSection.Economy: return "reallife_ledger_byline_economy";
                case LedgerSection.Business: return "reallife_ledger_byline_business";
                case LedgerSection.Markets: return "reallife_ledger_byline_markets";
                default: return "reallife_ledger_byline_city";
            }
        }

        // The bank rate and inflation, and the state of the economy, a line each. False if the edition has neither.
        private bool Rates(LedgerEdition e)
        {
            bool any = false;
            if (e.RateBp >= 0)
            {
                _b.Append(Plain(LedgerText.Fill("reallife_ledger_rates", "rate", LedgerText.Rate(e.RateBp), "inflation", e.InflationBp != int.MinValue ? LedgerText.Rate(e.InflationBp) : "?")));
                any = true;
            }
            if (!string.IsNullOrEmpty(e.CycleKey))
            {
                if (any)
                {
                    _b.Append(VT);
                }
                _b.Append(Plain(LedgerText.Fill("reallife_ledger_cycle", "state", LedgerText.T(e.CycleKey))));
                any = true;
            }
            return any;
        }

        // A piece's words without its kicker or headline: what a box that has its own name prints.
        internal static string Body(LedgerPiece p)
        {
            LedgerText.Words(p, out _, out string headline, out string body);
            return headline != null && p.Head == null ? headline + ". " + body : body;
        }

        // A lead without a headline of its own: its first sentence is the headline.
        internal static void FirstSentence(ref string body, out string headline)
        {
            body = body ?? string.Empty;
            int stop = body.IndexOf(". ", System.StringComparison.Ordinal);
            if (stop > 0 && stop < 110)
            {
                headline = body.Substring(0, stop);
                body = body.Substring(stop + 2);
            }
            else
            {
                headline = body.TrimEnd('.');
                body = string.Empty;
            }
        }

        internal static string Weekday(int day)
        {
            return TimeHelper.GetDayOfWeek(Mathf.Max(1, day)).GetLocalization();
        }

        internal static string Level(int hundredths)
        {
            return (hundredths / 100.0).ToString("N2", CultureInfo.CurrentCulture);
        }

        private static string Soft(string words)
        {
            return "<i angle=12>" + Plain(words) + "</i>";
        }

        internal static string Marks(int flags)
        {
            if (flags == 0)
            {
                return string.Empty;
            }
            string marks = ((flags & LedgerQuote.Bid) != 0 ? "b" : string.Empty) + ((flags & LedgerQuote.ExDividend) != 0 ? "x" : string.Empty)
                           + ((flags & LedgerQuote.New) != 0 ? "n" : string.Empty) + ((flags & LedgerQuote.Split) != 0 ? "s" : string.Empty);
            return " <size=80%><i angle=12>" + marks + "</i></size>";
        }

        // Words that came from a name in the game (a shop the player named) printed as they are, never read as mark-up.
        internal static string Plain(string words)
        {
            if (string.IsNullOrEmpty(words))
            {
                return string.Empty;
            }
            return words.IndexOf('<') < 0 ? words : "<noparse>" + words.Replace("</noparse>", string.Empty) + "</noparse>";
        }

        private static void Set(TMP_Text text, string words)
        {
            if (text != null)
            {
                UiClone.Words(text, words ?? string.Empty);
            }
        }

        private static void Show(Component part, bool on)
        {
            if (part != null && part.gameObject.activeSelf != on)
            {
                part.gameObject.SetActive(on);
            }
        }
    }
}
