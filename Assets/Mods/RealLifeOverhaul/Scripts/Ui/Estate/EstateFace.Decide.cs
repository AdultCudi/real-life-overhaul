using System;

namespace RealLifeOverhaul.Ui
{
    public static partial class EstateFace
    {
        // A decision as the approved desk session's card: the kit's decision card (UiDecision) with this screen's own command numbers, so a press of a
        // choice is the screen's action kind and never another part's number one. One question, the facts that decide it, the choices; no title strip, no
        // back button, focus on the safe choice.

        /// <summary>The choices as the card draws them: a number key each, F on the first that can be taken and P on the next. Main and second are their indexes, or -1.</summary>
        public static UiDecisionChoice[] Choices(EstateDecision d, out int main, out int second)
        {
            var list = new UiDecisionChoice[d.Choices.Length];
            for (int i = 0; i < list.Length; i++)
            {
                EstateChoice c = d.Choices[i];
                list[i] = new UiDecisionChoice
                {
                    Name = c.Name, Lines = c.Lines, Note = c.Note, Disabled = c.Disabled || c.Run < 0,
                    Key = i < 9 ? (UiAction)((int)UiAction.Pick1 + i) : UiAction.Count
                };
            }
            main = DialogRules.Keys(list, -1, out second);
            return list;
        }

        /// <summary>The run F or P takes on a decision, or -1 when no choice carries that key.</summary>
        public static int DecisionKey(EstateDecision d, UiAction action)
        {
            if (d == null || (action != UiAction.Alt && action != UiAction.Option))
            {
                return -1;
            }
            Choices(d, out int main, out int second);
            int at = action == UiAction.Alt ? main : second;
            return at >= 0 ? d.Choices[at].Run : -1;
        }

        private static void DecideLay(UiScene s, EstateModel m, UiFrame frame, bool dark, IUiKeys keys)
        {
            EstateDecision d = m.Decision;
            UiDecisionChoice[] choices = Choices(d, out int _, out int _);
            var args = new int[choices.Length];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = d.Choices[i].Run;
            }
            var card = new UiDecisionModel
            {
                Kicker = d.Kicker, Title = d.Title, Body = d.Body, BodyLines = 4, Portrait = d.Portrait, PortraitLines = d.PortraitLines, Emblem = d.Emblem,
                EmblemTint = d.EmblemTint, EmblemLabel = d.EmblemLabel, FactLabels = d.FactLabels, FactValues = d.FactValues, FactInks = d.FactInks, Steps = d.Steps,
                Choices = choices, Buttons = d.Buttons, ChoiceCmd = ActionCommand, ChoiceArgs = args, ButtonsOwn = true, Start = d.Start, StartButton = d.Start < 0 ? 0 : -1,
                Prompts = m.Prompts, PromptCount = m.Prompts.Length, LiftChoices = true
            };
            UiDecision.Lay(s, card, frame, dark, keys);
            WrapWayOut(s);
        }

        // The way out at a card's foot is one press from the top of it and the top one press from the way out, so the pad goes round a card instead of back along it: every target
        // of a card is then reached once in a single run, whichever answer the keys started on.
        private static void WrapWayOut(UiScene s)
        {
            int way = -1, first = -1;
            for (int i = 0; i < s.NodeCount; i++)
            {
                way = !s.Nodes[i].Disabled && s.Nodes[i].Command == BackCommand ? i : way;
            }
            for (int i = 0; way >= 0 && i < s.NodeCount; i++)
            {
                ref UiNode n = ref s.Nodes[i];
                if (i == way || n.Disabled || n.Layer != s.Nodes[way].Layer)
                {
                    continue;
                }
                if (first < 0 || n.Y < s.Nodes[first].Y - 0.5f || Math.Abs(n.Y - s.Nodes[first].Y) <= 0.5f && n.X < s.Nodes[first].X)
                {
                    first = i;
                }
            }
            if (way >= 0 && first >= 0 && s.Nodes[way].Down < 0 && s.Nodes[first].Up < 0)
            {
                s.Nodes[first].Up = way;
                s.Nodes[way].Down = first;
            }
        }
    }
}
