using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The game studio's screens (StudioViews: data only) drawn in the Tech app's box: the concept, a game's page and the
    /// studio's part of its firm's page. This is the drawing until the mod's own design system draws the same screens; it
    /// holds no rule of its own. Drawn on a refresh, nothing per frame.
    /// </summary>
    public sealed partial class TechApp
    {
        private int _beatPick, _projectPick;
        private readonly StudioWords _studioWords = new StudioWords();

        /// <summary>The screens' figures as the game writes them: money at today's prices, numbers, days and the mod's texts.</summary>
        private sealed class StudioWords : IStudioFormat
        {
            public string Money(long cents) => TechText.Cents(cents * Prices.Level / 1000);

            public string Whole(long n) => TechText.Whole(n);

            public string Mille(int thousandths) => TechText.Mille(thousandths);

            public string Day(int day) => TechText.Day(day);

            public string Text(string key) => TechText.T(key);
        }

        private static CellTone Tone(ViewTone t) => t == ViewTone.Good ? CellTone.Green : t == ViewTone.Warn ? CellTone.Yellow : t == ViewTone.Bad ? CellTone.Red : CellTone.Plain;

        private static string Words(ViewLine l) => l == null ? string.Empty : TechText.Fill(l.Key, l.Pairs ?? System.Array.Empty<string>());

        /// <summary>A game's page under the studio; false when the studio does not run it (the page of the firms' own rules is drawn then).</summary>
        private bool WriteGame(InfoBox box, Product p)
        {
            StudioScreen v = _director.StudioGameScreen(p, _beatPick, _projectPick, _studioWords);
            if (v == null)
            {
                return false;
            }
            Draw(box, v, p.Place, p.Id, true);
            return true;
        }

        /// <summary>The concept screen for a studio; false when the studio does not run there.</summary>
        private bool WriteConcept(InfoBox box, FirmPlace firm)
        {
            StudioScreen v = _director.StudioRuns(firm.File.K) ? _director.StudioConcept(firm, _studioWords) : null;
            if (v == null)
            {
                return false;
            }
            Draw(box, v, firm.File.Place, 0, true);
            return true;
        }

        /// <summary>The studio's own lines on its firm's page (its engine, what its team knows, the market), with no title of its own.</summary>
        private void WriteStudioPart(InfoBox box, FirmPlace firm)
        {
            StudioScreen v = _director.StudioFirmScreen(firm, _studioWords);
            if (v != null)
            {
                v.Primary = null;   // the firm's page has its own button to start a product, which opens the concept
                Draw(box, v, firm.File.Place, 0, false);
            }
        }

        // A screen: its head, what to do next, its one primary action, its sections (lines, choices with their trade-off, a button on a choice), and the rest.
        private void Draw(InfoBox box, StudioScreen v, string place, int id, bool head)
        {
            if (head)
            {
                box.Title(TechText.Fill(v.TitleKey, v.TitlePairs ?? System.Array.Empty<string>()));
                if (!string.IsNullOrEmpty(v.Subtitle))
                {
                    box.Subtitle(v.Subtitle);
                }
                if (v.ChipKey != null)
                {
                    box.Chip(TechText.T(v.ChipKey), Tone(v.ChipTone));
                }
            }
            if (v.Next != null)
            {
                box.Line(Words(v.Next), Tone(v.Next.Tone));
            }
            if (v.Primary != null && v.Primary.Enabled)
            {
                ViewAction a = v.Primary;
                InfoBox.Button(box.ButtonRow(), InfoBox.Positive, a.Key, () => Act(a.Act, place, id, a.Arg));
            }
            for (int s = 0; s < v.Sections.Count; s++)
            {
                ViewSection section = v.Sections[s];
                box.Section(section.TitleKey);
                for (int i = 0; i < section.Lines.Count; i++)
                {
                    box.Line(Words(section.Lines[i]), Tone(section.Lines[i].Tone));
                }
                for (int i = 0; i < section.Choices.Count; i++)
                {
                    DrawChoice(box, section.Choices[i], place, id);
                }
            }
            Transform rest = v.Others.Count > 0 ? box.ButtonRow() : null;
            for (int i = 0; i < v.Others.Count; i++)
            {
                ViewAction a = v.Others[i];
                if (a.Danger)
                {
                    InfoBox.Button(rest, InfoBox.Negative, a.Key, () => GameConfirm.Show("reallife_tech_confirm_withdraw_title", "reallife_tech_confirm_withdraw_body",
                        () => UiClone.Safe(() => Act(a.Act, place, id, a.Arg)), null, null, null, true, true));
                }
                else
                {
                    InfoBox.Button(rest, InfoBox.Additional, a.Key, () => Act(a.Act, place, id, a.Arg));
                }
            }
        }

        private void DrawChoice(InfoBox box, ViewChoice c, string place, int id)
        {
            if (c.Options.Count == 1)
            {
                box.Row(c.LabelKey, c.Options[0]);   // nothing to choose between: a fact with its button (the game's name)
            }
            else if (box.HasSelector && c.Options.Count > 0)
            {
                string label = TechText.T(c.LabelKey);
                int count = c.Options.Count, act = c.Act;
                System.Func<int, string> text = i => label + ": " + c.Options[Mathf.Clamp(i, 0, count - 1)];
                System.Action<int> picked = i => Act(act, place, id, c.Values.Count == count ? c.Values[Mathf.Clamp(i, 0, count - 1)] : i);
                if (c.Slider)
                {
                    box.Slider(c.LabelKey, count, Mathf.Clamp(c.Picked, 0, count - 1), i => c.Options[Mathf.Clamp(i, 0, count - 1)], picked);
                }
                else
                {
                    box.Selector(count, Mathf.Clamp(c.Picked, 0, count - 1), text, picked);
                }
            }
            if (c.Trade != null)
            {
                box.Line(Words(c.Trade), Tone(c.Trade.Tone));
            }
            if (c.Go != null && c.Go.Enabled)
            {
                ViewAction go = c.Go;
                InfoBox.Button(box.ButtonRow(), InfoBox.Additional, go.Key, () => Act(go.Act, place, id, go.Arg));
            }
        }

        // What a screen's choice or button asks: the app's own (close, the beat and project picked, the concept opened) or the director's.
        private void Act(int act, string place, int id, int value)
        {
            switch (act)
            {
                case StudioAct.Close:
                    ClosePick();
                    return;
                case StudioAct.BeatPick:
                    Set(() => _beatPick = value);
                    return;
                case StudioAct.ProjectPick:
                    Set(() => _projectPick = value);
                    return;
                case StudioAct.Concept:
                    Set(() =>
                    {
                        _tab = TechDirector.TabFirms;
                        _picked = NewProductRow + place;
                        _newKind = (int)ProductKind.Game;
                    });
                    return;
                case StudioAct.Withdraw:
                    Do(() => _director.Withdraw(id));
                    return;
                case StudioAct.Name:
                    // a game's own name on its page, or the concept's: an empty box hands the concept's name back to the studio
                    if (id > 0)
                    {
                        AskName(_director.TitleOf(id), false, text => _director.RenameProduct(id, text));
                    }
                    else
                    {
                        AskName(_director.DraftTyped(place), true, text => _director.NameDraft(place, text));
                    }
                    return;
                default:
                    Do(() => _director.StudioDo(act, place, id, value));
                    return;
            }
        }
    }
}
