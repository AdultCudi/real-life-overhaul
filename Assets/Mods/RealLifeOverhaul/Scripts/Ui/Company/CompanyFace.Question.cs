using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class CompanyFace
    {
        private const float LeadFace = 64f;

        private static UiDecisionChoice[] ChoicesOf(CompanyQuestion q)
        {
            var list = new UiDecisionChoice[q.Choices.Length];
            for (int i = 0; i < list.Length; i++)
            {
                CompanyChoice c = q.Choices[i];
                var lines = new UiConsequence[c.Lines != null ? c.Lines.Length : 0];
                for (int k = 0; k < lines.Length; k++)
                {
                    lines[k] = new UiConsequence { Text = c.Lines[k].Text, Tone = c.Lines[k].Tone, Change = c.Lines[k].Change };
                }
                list[i] = new UiDecisionChoice { Key = (UiAction)((int)UiAction.Pick1 + Math.Min(i, 8)), Name = c.Name, Lines = lines, Note = c.Note, Disabled = c.Disabled };
            }
            return list;
        }

        /// <summary>The buttons of a question: where it opens, put it off, and the answer chosen. The main one last, carrying F; the second P.</summary>
        internal static CompanyButton[] QuestionButtons(CompanyQuestion q, CompanyView v, CompanyWords w)
        {
            var buttons = new System.Collections.Generic.List<CompanyButton>(3);
            if (!string.IsNullOrEmpty(q.OpenLabel) && q.Open.Do != CompanyDo.None)
            {
                buttons.Add(new CompanyButton { Label = q.OpenLabel, Kind = UiButtonKind.Quiet, Icon = UiIcon.Next, Key = UiAction.Count, Act = q.Open });
            }
            buttons.Add(new CompanyButton
            {
                Label = string.IsNullOrEmpty(q.LaterLabel) ? w.T("reallife_co_decide_later", "Decide later") : q.LaterLabel, Kind = UiButtonKind.Secondary, Key = UiAction.Option,
                Act = CompanyAct.Of(CompanyDo.Go, "later", q.Id)
            });
            if (q.Deep)
            {
                buttons.Add(new CompanyButton { Label = w.T("reallife_hier_do_try", "Try now"), Kind = UiButtonKind.Primary, Key = UiAction.Alt, Act = CompanyAct.Of(CompanyDo.TryNow, null, q.Id) });
                buttons.Add(new CompanyButton { Label = w.T("reallife_hier_do_pass", "Pass it up"), Kind = UiButtonKind.Secondary, Act = CompanyAct.Of(CompanyDo.Pass, null, q.Id) });
                return buttons.ToArray();
            }
            int at = Math.Max(0, Math.Min(q.Choices.Length - 1, v.Choice));
            if (q.Choices.Length > 0)
            {
                CompanyChoice c = q.Choices[at];
                buttons.Add(new CompanyButton
                {
                    Label = string.IsNullOrEmpty(c.Button) ? c.Name : c.Button, Kind = UiButtonKind.Primary, Icon = UiIcon.Check, Key = UiAction.Alt, Act = c.Act, Disabled = c.Disabled, Why = c.Why
                });
            }
            return buttons.ToArray();
        }

        /// <summary>
        /// The question on the Lead, in the page's own room: the one who asks in their ring, the kicker and the headline, what it is, the choices numbered with their
        /// consequences, and the buttons. Returns its height.
        /// </summary>
        internal static float LeadCard(UiScene s, UiAt at, CompanyQuestion q, CompanyView v, CompanyWords w, CompanyLayout lay, float x, float y, float width, IUiKeys keys)
        {
            float pad = UiTokens.S5;
            float inner = width - pad * 2f;
            float tx = x + pad + LeadFace + UiTokens.S4, tw = width - pad * 2f - LeadFace - UiTokens.S4;
            float kh = UiDraw.Snap(UiTextRules.LineHeight(UiStyle.SectionHeader));
            float hh = UiDraw.Snap(UiTextRules.WrappedHeight(q.Headline ?? q.Title ?? string.Empty, UiStyle.Title, tw, 2));
            float bh = string.IsNullOrEmpty(q.Body) ? 0f : UiDraw.Snap(UiTextRules.WrappedHeight(q.Body, UiStyle.Body, tw, 3));
            float textH = kh + UiTokens.S1 + hh + (bh > 0f ? UiTokens.S2 + bh : 0f);
            float choicesH = 0f;
            UiDecisionChoice[] choices = ChoicesOf(q);
            for (int i = 0; i < choices.Length; i++)
            {
                choicesH += UiDecision.ChoiceHeight(ref choices[i]) + UiTokens.S2;
            }
            float total = pad + Math.Max(LeadFace, textH) + UiTokens.S4 + choicesH + UiTokens.Control + pad;
            int card = UiDraw.Box(s, at, x, y, width, total, UiRole.Row, UiRole.None, UiTokens.RadiusLarge, UiFlags.Decor);
            UiAt inside = at.In(card);
            UiDraw.Portrait(s, inside, x + pad, y + pad, LeadFace, q.Asker ?? q.Subject ?? string.Empty, q.AskerHue, q.AskerRing);
            float cy = y + pad;
            UiDraw.TextMiddle(s, inside, tx, cy, kh, tw, UiStyle.SectionHeader, UiRole.Text2, q.Kicker ?? string.Empty, UiFlags.Label | UiFlags.Truncate);
            cy += kh + UiTokens.S1;
            UiDraw.Text(s, inside, tx, cy, tw, UiStyle.Title, UiRole.Text, q.Headline ?? q.Title ?? string.Empty, UiFlags.Wrap | UiFlags.Truncate, 2);
            cy += hh;
            if (bh > 0f)
            {
                cy += UiTokens.S2;
                UiDraw.Text(s, inside, tx, cy, tw, UiStyle.Body, UiRole.Text2, q.Body, UiFlags.Wrap | UiFlags.Truncate, 3);
            }
            cy = y + pad + Math.Max(LeadFace, textH) + UiTokens.S4;
            for (int i = 0; i < choices.Length; i++)
            {
                cy += UiDecision.Choice(s, inside, ref choices[i], i, i == v.Choice, x + pad + UiTokens.S3, cy, inner - UiTokens.S3 * 2f, keys, CompanyFace.Choice) + UiTokens.S2;
            }
            CompanyButton[] buttons = QuestionButtons(q, v, w);
            // The answers at the left (the main one first on this row, as the mockup has it), the link to where it opens at the right.
            float bx = x + pad;
            string openWords = null;
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].Kind == UiButtonKind.Quiet)
                {
                    openWords = buttons[i].Label;
                    int idx = lay.Add(buttons[i]);
                    float ow = Math.Min(UiDraw.SnapDown(inner * 0.4f), UiDraw.Snap(UiTextRules.Width(openWords, UiStyle.BodyStrong) + UiTokens.IconSmall + UiTokens.S4));
                    UiDraw.LinkRow(s, inside, x + width - pad - ow, cy, ow, UiTokens.Control, openWords, Act, idx, false);
                }
            }
            CompanyButton main = default(CompanyButton), later = default(CompanyButton);
            bool hasMain = false, hasLater = false;
            var extra = new System.Collections.Generic.List<CompanyButton>(2);
            for (int i = 0; i < buttons.Length; i++)
            {
                if (buttons[i].Kind == UiButtonKind.Quiet)
                {
                    continue;
                }
                if (buttons[i].Key == UiAction.Alt)
                {
                    main = buttons[i];
                    hasMain = true;
                }
                else if (buttons[i].Key == UiAction.Option)
                {
                    later = buttons[i];
                    hasLater = true;
                }
                else
                {
                    extra.Add(buttons[i]);
                }
            }
            float rightLimit = x + width - pad;
            if (hasMain)
            {
                float mw = UiDraw.ButtonWidth(main.Kind, main.Label, main.Icon, main.Key, keys);
                Button(s, inside, lay, v, ref main, bx, cy, mw, keys);
                bx += mw + UiTokens.S3;
            }
            if (hasLater)
            {
                float lw = UiDraw.ButtonWidth(later.Kind, later.Label, later.Icon, later.Key, keys);
                Button(s, inside, lay, v, ref later, bx, cy, lw, keys);
                bx += lw + UiTokens.S3;
            }
            for (int i = 0; i < extra.Count; i++)
            {
                CompanyButton e = extra[i];
                float ew = UiDraw.ButtonWidth(e.Kind, e.Label, e.Icon, e.Key, keys);
                if (bx + ew > rightLimit)
                {
                    break;
                }
                Button(s, inside, lay, v, ref e, bx, cy, ew, keys);
                bx += ew + UiTokens.S3;
            }
            return total;
        }

        /// <summary>The question as a card over the dimmed world (the decision card): the manager and the person it is about at its side, what it is, the choices and the buttons.</summary>
        internal static void LayQuestion(UiScene s, CompanyQuestion q, CompanyView v, CompanyWords w, CompanyLayout lay, UiFrame frame, bool dark, IUiKeys keys)
        {
            var faces = new System.Collections.Generic.List<string>(2);
            var roles = new System.Collections.Generic.List<string>(2);
            var rings = new System.Collections.Generic.List<UiRole>(2);
            if (!string.IsNullOrEmpty(q.Asker))
            {
                faces.Add(q.Asker);
                roles.Add(q.AskerRole);
                rings.Add(q.AskerRing);
            }
            if (!string.IsNullOrEmpty(q.Subject))
            {
                faces.Add(q.Subject);
                roles.Add(q.SubjectRole);
                rings.Add(q.SubjectRing);
            }
            CompanyButton[] buttons = QuestionButtons(q, v, w);
            var actions = new UiActionModel[buttons.Length];
            for (int i = 0; i < buttons.Length; i++)
            {
                lay.Add(buttons[i]);   // the card's button i answers with the action at index i (Act, as every button does)
                actions[i] = new UiActionModel { Label = buttons[i].Label, Kind = buttons[i].Kind, Icon = buttons[i].Icon, Key = buttons[i].Key, Disabled = buttons[i].Disabled };
            }
            var m = new UiDecisionModel
            {
                Kicker = q.Kicker, Title = q.Title, Body = q.Body, BodyLines = 5, Faces = faces.ToArray(), FaceRoles = roles.ToArray(), FaceRings = rings.ToArray(), Speaking = faces.Count > 0 ? 0 : -1,
                FactLabels = q.FactLabels, FactValues = q.FactValues, Choices = ChoicesOf(q), Selected = Math.Max(0, Math.Min(q.Choices.Length - 1, v.Choice)), Buttons = actions,
                ChoiceCmd = Choice, ButtonCmd = Act,
                Prompts = new[]
                {
                    new UiPrompt { Action = UiAction.Move, Label = w.T("reallife_ui_prompt_move", "Move") },
                    new UiPrompt { Action = UiAction.Confirm, Label = w.T("reallife_ui_prompt_select", "Select") },
                    new UiPrompt { Action = UiAction.Back, Label = w.T("reallife_co_prompt_later", "Decide later") }
                },
                PromptCount = 3
            };
            UiDecision.Lay(s, m, frame, dark, keys);
        }
    }
}
