using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Ledger
{
    public sealed partial class LedgerPage
    {
        // ================================================================ a front page of its own (LedgerSpecial)

        private LedgerSpecial _special;
        private Action _specialClosed;
        private readonly List<string> _boxScratch = new List<string>(16);

        /// <summary>True while the page is on a special front page rather than an edition.</summary>
        public bool Special => _special != null;

        /// <summary>
        /// Opens the page on a front page of its own (an obituary): the masthead, the dateline and the rules are the paper's,
        /// every word is the special's, the inside pages' tabs are hidden while it is up. <paramref name="closed"/> is told
        /// once, when the page goes down by any way (the cancel key, the menu's close button, another app). False if the page
        /// is not in the menu yet.
        /// </summary>
        public bool ShowSpecial(LedgerSpecial special, Action closed)
        {
            if (_app == null || !_app.Installed || special == null || _shell == null)
            {
                return false;
            }
            _special = special;
            _specialClosed = closed;
            _page = Front;
            Tabs(false);
            if (Showing)
            {
                UiClone.Safe(Opened);
            }
            else
            {
                _app.Open();
            }
            return true;
        }

        // The special page has gone down: whoever opened it is told, once, and the page is the paper's again.
        private void SpecialClosed()
        {
            Action closed = _specialClosed;
            _special = null;
            _specialClosed = null;
            Tabs(true);
            if (closed != null)
            {
                try
                {
                    closed();
                }
                catch (Exception e)
                {
                    LedgerDirector.Logger?.Error(e);
                }
            }
        }

        private void Tabs(bool shown)
        {
            if (_shell == null || _shell.Tabs == null)
            {
                return;
            }
            for (int i = 0; i < _shell.Tabs.Length; i++)
            {
                if (_shell.Tabs[i] != null && _shell.Tabs[i].gameObject.activeSelf != (shown || i == Front))
                {
                    _shell.Tabs[i].gameObject.SetActive(shown || i == Front);
                }
            }
        }

        // The front page laid out as the morning's is, with the special's words: the masthead between its two ears, the
        // dateline, the lead's head across the main area, four columns (the lead under a drop cap, then each section under
        // its label), the box down the side, and the framed notice in what the columns leave.
        private void WriteSpecial(LedgerSpecial sp, Rules rules)
        {
            float u = _height;
            Margins(_pages[Front], u, out float W, out float H);

            float mh = 0.122f * u;
            _fTitle.fontSize = 0.083f * u;
            _fTitle.characterSpacing = 6f;
            Set(_fTitle, Plain(LedgerText.T("reallife_ledger_masthead")));
            Put(_fTitle.rectTransform, 0.2f * W, TopFor(mh, _fTitle.fontSize), 0.6f * W, mh);
            _b.Clear();
            Label(sp.EarLeftLabel, "80%");
            _b.Append(VT).Append("<i angle=12>").Append(Plain(sp.EarLeft)).Append("</i>");
            Ear(_fEarLeft, _b.ToString(), 0f, W, mh, u, rules);
            _b.Clear();
            Label(sp.EarRightLabel, "80%");
            _b.Append(VT).Append("<b>").Append(Plain(sp.EarRight)).Append("</b>");
            Ear(_fEarRight, _b.ToString(), W - 0.148f * W, W, mh, u, rules);

            rules.Across(0f, mh + 0.0015f * u, W, 3f * _px);
            rules.Across(0f, mh + 0.007f * u, W, _px);
            float dy = mh + 0.007f * u, dh = 0.031f * u, ds = 0.0148f * u;
            Dateline(_fDateLeft, LedgerText.Fill("reallife_ledger_dateline", "weekday", Weekday(sp.Day), "day", Mathf.Max(1, sp.Day).ToString(CultureInfo.CurrentCulture)), 0f, dy, W, dh, ds);
            Dateline(_fDateMiddle, sp.DateMiddle, 0f, dy, W, dh, ds);
            Dateline(_fDateRight, sp.DateRight, 0f, dy, W, dh, ds);
            rules.Across(0f, dy + dh, W, _px);
            float bt = dy + dh + 0.018f * u;

            // ---- the box down the side
            float mw = 0.755f * W, bx = mw + 0.017f * W, bw = W - bx;
            SpecialBox(sp, bx, bt, bw, H - bt, u, rules);

            // ---- the lead and the columns, sized together as the morning's front page is
            float gut = 0.025f * u;
            float cw = (mw - (FrontColumns - 1) * gut) / FrontColumns;
            float s = BodyStart * u, hs = HeadStart * u;
            Lead lw = LeadWords(_fColumns, sp.Body, s, cw);
            lw.Stand = sp.Stand;   // the special's own standfirst, not the lead's first sentence
            lw = Restand(lw, sp.Body, cw);
            string flow = SpecialFlow(sp, lw);
            float top = Header(sp.Kicker, sp.Headline, lw.Stand, sp.Byline, bt, mw, hs, u, false);
            float need = Tall(_fColumns.Measure, flow, cw);
            s = Mathf.Clamp(s * Mathf.Sqrt(0.94f * FrontColumns * (H - top) / Mathf.Max(1f, need)), BodyLeast * u, BodyMost * u);
            bool notice = false;
            for (int pass = 0; pass < 6; pass++)
            {
                _fColumns.Size(s);
                lw = Restand(lw, sp.Body, cw);
                flow = SpecialFlow(sp, lw);
                top = Header(sp.Kicker, sp.Headline, lw.Stand, sp.Byline, bt, mw, hs, u, false);
                float adv = Advance(s, BodySpacing);
                need = Tall(_fColumns.Measure, flow, cw);
                float depth = Mathf.Ceil(need / FrontColumns / adv + 0.5f) * adv;
                float left = H - top - depth;
                notice = sp.Notice.Length > 0 && left >= (BandLeast + BandGap) * u;
                float spare = left - (notice ? (BandGap + BandMost) * u : 0f);
                if (spare <= 0.01f * u || hs >= HeadMost * u)
                {
                    break;
                }
                hs = Mathf.Min(HeadMost * u, hs + spare * 0.5f);
            }
            top = Header(sp.Kicker, sp.Headline, lw.Stand, sp.Byline, bt, mw, hs, u, true);
            float most = H - top - (notice ? (BandLeast + BandGap) * u : 0f);
            _fColumns.Lay(flow, 0f, cw, gut, top, most, lw.Cap, lw.CapLines, lw.EndsBeside);
            _fColumns.Marks(rules, _px);
            _fColumns.Gutters(rules, gut, _px);

            // ---- under the columns: the notice, framed twice as the paper frames its own
            float below = top + _fColumns.Depth + BandGap * u * 0.6f;
            Show(_fBand, false);
            Show(_fCaption, false);
            Show(_fChart, false);
            Show(_fAd, notice);
            if (notice)
            {
                Notice(sp, 0f, below, mw, H - below, u, rules);
            }
        }

        // The lead's text again at the columns' size: the cap and the lines beside it, with the special's standfirst kept.
        private Lead Restand(Lead lw, string body, float cw)
        {
            string stand = lw.Stand;
            lw.Text = body ?? string.Empty;
            lw.CapLines = _fColumns.CapLines(lw.Text, cw, out lw.EndsBeside);
            lw.Cap = lw.CapLines > 0 && lw.Text.Length > 0 ? lw.Text.Substring(0, 1) : null;
            lw.Stand = stand;
            return lw;
        }

        // The columns' words: the lead after its drop cap, then each section under its label over a rule.
        private string SpecialFlow(LedgerSpecial sp, Lead lw)
        {
            _b.Clear();
            if (lw.CapLines > 0 && lw.Text.Length > 1)
            {
                _b.Append(AfterCap(lw.Text)).Append('\n');
            }
            else if (lw.Text.Length > 0)
            {
                _b.Append(Plain(lw.Text)).Append('\n');
            }
            for (int i = 0; i < sp.Labels.Count; i++)
            {
                List<string> lines = i < sp.Texts.Count ? sp.Texts[i] : null;
                if (lines == null || lines.Count == 0)
                {
                    continue;
                }
                _b.Append(MarkRule).Append(MarkKeep).Append("<align=left>");
                Label(sp.Labels[i], "84%");
                _b.Append("</align>").Append(VT).Append(MarkEnd);
                for (int k = 0; k < lines.Count; k++)
                {
                    _b.Append(Plain(lines[k])).Append(k + 1 < lines.Count ? " " : "\n");
                }
            }
            return Words();
        }

        // The box down the side: each label over its rule, its lines under it; smaller type if it would not fit.
        private void SpecialBox(LedgerSpecial sp, float x, float y, float w, float h, float u, Rules rules)
        {
            float pad = 0.014f * u, size = BoxSize * u;
            float iw = w - 2f * pad, ih = h - 1.4f * pad;
            string words = null;
            for (int pass = 0; pass < 5; pass++)
            {
                _fBox.fontSize = size;
                words = SpecialBoxWords(sp);
                if (Tall(_fBox, words, iw) <= ih)
                {
                    break;
                }
                size *= 0.92f;
            }
            Set(_fBox, words);
            Put(_fBox.rectTransform, x + pad, y + 0.5f * pad, iw, h - pad);
            _fBox.ForceMeshUpdate();
            Under(_fBox, rules);
            rules.Frame(x, y - 0.003f * u, w, h + 0.003f * u, _px);
        }

        private string SpecialBoxWords(LedgerSpecial sp)
        {
            _b.Clear();
            for (int i = 0; i < sp.BoxLabels.Count; i++)
            {
                _b.Append(MarkUnder).Append("<align=left>");
                Label(sp.BoxLabels[i], "86%");
                _b.Append("</align>\n");
                List<string> lines = i < sp.BoxLines.Count ? sp.BoxLines[i] : null;
                _boxScratch.Clear();
                if (lines != null)
                {
                    _boxScratch.AddRange(lines);
                }
                for (int k = 0; k < _boxScratch.Count; k++)
                {
                    string line = _boxScratch[k];
                    int cut = line.IndexOf(": ", StringComparison.Ordinal);
                    if (cut > 0)
                    {
                        _b.Append("<b>").Append(Plain(line.Substring(0, cut + 1))).Append("</b> ").Append(Plain(line.Substring(cut + 2)));
                    }
                    else
                    {
                        _b.Append(Plain(line));
                    }
                    _b.Append(k + 1 < _boxScratch.Count ? VT : '\n');
                }
                if (_boxScratch.Count == 0)
                {
                    _b.Append('\n');
                }
            }
            return Words();
        }

        // The notice at the foot: a box framed twice, as deep as the room, its words in the middle.
        private void Notice(LedgerSpecial sp, float x, float y, float w, float h, float u, Rules rules)
        {
            float bw = 0.8f * w, size = 0.022f * u;
            _fAd.fontSize = size;
            string words = "<size=150%><b><smallcaps><cspace=0.1em>" + Plain(sp.NoticeHead) + "</cspace></smallcaps></b></size>" + VT + Plain(sp.Notice);
            float th = Tall(_fAd, words, bw - 0.08f * u);
            float bh = Mathf.Max(th + 0.05f * u, Mathf.Min(h - 0.03f * u, th + 0.12f * u));
            float bx = x + (w - bw) * 0.5f, by = y + Mathf.Max(0f, h - bh) * 0.5f;
            Set(_fAd, words);
            Put(_fAd.rectTransform, bx + 0.04f * u, by + (bh - th) * 0.5f, bw - 0.08f * u, th + 2f);
            rules.Frame(bx, by, bw, bh, 3f * _px);
            rules.Frame(bx + 0.006f * u, by + 0.006f * u, bw - 0.012f * u, bh - 0.012f * u, _px);
        }
    }
}
