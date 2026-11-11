using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.Interiors;
using RealLifeOverhaul.Loyalty;
using UI.Smartphone.Apps.BizMan;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The mod's own Interior tab on the kit: the tab's pages write their lines here as they write them into the game's
    /// boxes, the scores into the left column and the page chosen into the right, its buttons the page's own. Written when a
    /// business opens, a page is chosen or a button pressed; never a frame.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private readonly KitLines _scores = new KitLines(null);
        private readonly List<Action> _lineActs = new List<Action>(4);
        private KitLines _page;
        private int _interiorTick;   // a button of the tab pressed: what it says may have moved

        private void Interior(BizManBusiness b)
        {
            _m.Part = BizManPart.Interior;
            _page = _page ?? new KitLines(_lineActs);
            _scores.Clear();
            _page.Clear();
            if (_shown == BizManRules.Loyalty)
            {
                LoyaltyLines(b);
                return;
            }
            InteriorTab.Lines(b.buildingRegistration, _scores, _page);
            _m.Pages = new string[InteriorTab.Pages];
            for (int i = 0; i < _m.Pages.Length; i++)
            {
                _m.Pages[i] = InteriorText.Page(i);
            }
            // The head names the business and the chosen tab its page: their titles over the columns would say it twice.
            Untitle(_scores.Lines, _m.Head != null ? _m.Head.Name : null);
            Untitle(_page.Lines, _m.Pages[InteriorTab.Shown]);
            _m.Lines = _scores.Lines.ToArray();
            _m.SecondLines = _page.Lines.ToArray();
            _m.PageShown = InteriorTab.Shown;
            _m.PagesOpen = _pagesOpen;
            _m.Actions = _page.Actions.ToArray();
        }

        private long InteriorSignature() => InteriorTab.Shown * 7919L + _interiorTick;

        // The mod's own Loyalty tab, written as the Interior tab is: the week's figures into the left column, the scheme and its buttons into the right. One page.
        private void LoyaltyLines(BizManBusiness b)
        {
            LoyaltyTab.Lines(b.buildingRegistration, _scores, _page);
            _m.Pages = new[] { UiText.T("reallife_loyalty_head_scheme") };
            Untitle(_scores.Lines, _m.Head != null ? _m.Head.Name : null);
            _m.Lines = _scores.Lines.ToArray();
            _m.SecondLines = _page.Lines.ToArray();   // its title stays: one page has no tab to say it
            _m.PageShown = 0;
            _m.PagesOpen = false;
            _m.Actions = _page.Actions.ToArray();
        }

        // A press on the tab, a day's turn or a choice confirmed in the game's question box moves what the tab says.
        private long LoyaltySignature() => (LoyaltyDirector.Instance != null ? LoyaltyDirector.Instance.Revision : 0) * 7919L + _interiorTick;

        private static void Untitle(List<BizManLine> lines, string said)
        {
            if (lines.Count > 0 && lines[0].Kind == BizManLineKind.Title && string.Equals(lines[0].Words, said, StringComparison.Ordinal))
            {
                lines.RemoveAt(0);
            }
        }

        // A page of the right column chosen, as the game's own selector chooses it.
        private bool InteriorPage(int page)
        {
            if (_shown == BizManRules.Loyalty)
            {
                return false;   // one page: nothing to choose
            }
            InteriorTab.Shown = page;
            _pagesOpen = false;
            _m.SecondOffset = 0f;   // a new page starts at its top
            UiDirector.Logger?.Info("BizMan (kit): the Interior tab's page " + page + " chosen");
            return true;
        }

        // A button of the tab's page, its own click as the game's box would run it.
        private bool InteriorDo(int index)
        {
            if (index < 0 || index >= _lineActs.Count)
            {
                return false;
            }
            UiClone.Safe(_lineActs[index]);   // as the game's box runs its own button: a failure logged, never thrown into the frame
            _interiorTick++;
            return true;
        }

        // The tab's lines kept for the kit: titles and headings as stops, lines in their tone, notes quieter, buttons as the
        // page's actions with their clicks kept to run.
        private sealed class KitLines : IInteriorLines
        {
            private static readonly Regex Rich = new Regex("<[^>]+>", RegexOptions.Compiled);
            public readonly List<BizManLine> Lines = new List<BizManLine>(32);
            public readonly List<UiActionModel> Actions = new List<UiActionModel>(4);
            private readonly List<Action> _acts;

            public KitLines(List<Action> acts)
            {
                _acts = acts;
            }

            public void Clear()
            {
                Lines.Clear();
                Actions.Clear();
                _acts?.Clear();
            }

            public void Title(string words) => Add(BizManLineKind.Title, words, UiRole.None);

            public void Headline(string key) => Add(BizManLineKind.Heading, UiText.T(key), UiRole.None);

            public void HeadlineWords(string words) => Add(BizManLineKind.Heading, words, UiRole.None);

            public void Line(string words) => Add(BizManLineKind.Line, words, UiRole.None);

            public void Line(string words, CellTone tone) =>
                Add(BizManLineKind.Line, words, tone == CellTone.Red ? UiRole.DangerText : tone == CellTone.Yellow ? UiRole.WarnText : tone == CellTone.Green ? UiRole.OkText : UiRole.None);

            public void Note(string words) => Add(BizManLineKind.Note, words, UiRole.None);

            public void Button(int kind, string key, Action click)
            {
                if (_acts == null || click == null)
                {
                    return;
                }
                Actions.Add(new UiActionModel
                {
                    Label = UiText.T(key), Kind = kind == InfoBox.Negative ? UiButtonKind.Destructive : UiButtonKind.Secondary, Key = UiAction.Count,
                    Icon = kind == InfoBox.Negative ? UiIcon.Hammer : UiIcon.Wrench, Command = BizManPages.Action, Arg = BizManPages.DoLine + _acts.Count
                });
                _acts.Add(click);
            }

            // The Guide's page for the tab, as the game's box writes it at its foot: once, under the page.
            public void Guide(string titleKey)
            {
                if (_acts == null)
                {
                    return;
                }
                string label = UiText.T("reallife_guide_app");
                Add(BizManLineKind.Note, label + LookRules.Colon(label) + UiText.T(titleKey), UiRole.None);
            }

            public void Settings(string sectionId)
            {
                if (_acts == null || !Meta.SettingsApp.Installed)
                {
                    return;
                }
                string target = "s:" + sectionId;
                Actions.Add(new UiActionModel
                {
                    Label = UiText.T("reallife_set_link"), Kind = UiButtonKind.Quiet, Key = UiAction.Count, Icon = UiIcon.Gear, Command = BizManPages.Action,
                    Arg = BizManPages.DoLine + _acts.Count
                });
                _acts.Add(() => Meta.SettingsApp.Open(target));
            }

            private void Add(BizManLineKind kind, string words, UiRole tone)
            {
                if (!string.IsNullOrEmpty(words))
                {
                    Lines.Add(new BizManLine { Kind = kind, Words = Rich.Replace(words, string.Empty), Tone = tone });
                }
            }
        }
    }
}
