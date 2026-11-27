using System;
using System.Collections.Generic;
using Dialogs;
using Entities;
using Localizor;
using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul
{
    /// <summary>The moment being played: which it is, where, and what choosing a reaction does (the scene applies it and says what came of it).</summary>
    public sealed class MomentPlay
    {
        public MomentDef Def;
        public string Place;
        public Func<int, string> Choose;   // applies the reaction (0 to 2) and returns the words of the page that says how it landed
        public bool Chosen;
    }

    /// <summary>
    /// A moment in the game's own dialogue panel, the one the outings talk in (SceneDialog hands over to it): the situation and three
    /// reactions as the panel's three buttons, then their reply, the figures that moved and a way to carry on. COST: a few strings
    /// when a page is shown; nothing between presses.
    /// </summary>
    public static class SocialMomentTalk
    {
        /// <summary>The first page: what happened, and the three reactions.</summary>
        public static DialogEntry Open(SceneTalk talk, SocialPerson p)
        {
            MomentPlay play = talk.Play;
            MomentDef d = play.Def;
            Dictionary<string, string> data = SocialLife.Data(p, "place", play.Place ?? string.Empty);
            SocialLife.Said(p, d.Situation, data);
            var entry = new DialogEntry { messageData = d.Situation.Localize(data) };
            MomentReaction a = d.Reactions[0], b = d.Reactions[1], c = d.Reactions[2];
            entry.ConfirmTextOverride = a.Label.Localize(data);
            entry.OnConfirm = () => Result(play, 0);
            entry.SecondOptionTextOverride = b.Label;
            entry.OnSecondOption = () => Result(play, 1);
            entry.CancelTextOverride = c.Label;
            entry.OnCancel = () => DialogController.current.ShowEntry(Guard.Entry(Result(play, 2)));
            Cost(entry, DialogButton.Confirm, a);
            Cost(entry, DialogButton.Second, b);
            Cost(entry, DialogButton.Cancel, c);
            return entry;
        }

        private static void Cost(DialogEntry entry, DialogButton button, MomentReaction r)
        {
            if (r.Cost > 0)
            {
                entry.Pays(button, "reallife_ui_dialog_cost_now", "amount", SocialLife.Money(Prices.At(r.Cost)));
            }
        }

        // Their reply and what it moved: one page, and a way to carry on.
        private static DialogEntry Result(MomentPlay play, int reaction)
        {
            string words = play.Chosen ? string.Empty : play.Choose(reaction);
            play.Chosen = true;
            var entry = new DialogEntry { messageData = SocialLinks.WordsKey.Localize(new Dictionary<string, string> { { "text", words } }) };
            entry.ConfirmTextOverride = SocialMomentCatalogue.ButtonOn.Localize();
            entry.OnConfirm = () =>
            {
                DialogController.current.FinishDialog();
                return null;
            };
            return entry;
        }
    }
}
