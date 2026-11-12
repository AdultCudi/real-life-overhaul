using RealLifeOverhaul.Ui;

// The sweep's samples: a desk session's pages as the activity panel writes them, on the decision card. Made-up places; the
// words are the panel's own, cut into lines by SweepRules as the screen cuts them.
internal static partial class UiSamples
{
    private static UiPrompt[] DeskPrompts(UiWords t) => new[]
    {
        new UiPrompt { Action = UiAction.Move, Label = t("reallife_ui_prompt_move", "Move") },
        new UiPrompt { Action = UiAction.Confirm, Label = t("reallife_ui_prompt_choose", "Choose") },
        new UiPrompt { Action = UiAction.Back, Label = t("reallife_ui_prompt_back", "Back") },
    };

    /// <summary>The weekly meeting's page: the question in the card's words, the two ways to hold it, Cancel at the foot.</summary>
    public static UiDecisionModel DeskCard(UiWords t)
    {
        var m = new UiDecisionModel
        {
            Kicker = t("reallife_session_enabled", "Desk sessions"), Title = "Weekly team meeting", Emblem = UiIcon.Presentation, EmblemTint = UiRole.Accent,
            Body = "What is this week's meeting for?", BodyLines = 3, Selected = 0, Start = 0,
            Choices = new[]
            {
                new UiDecisionChoice
                {
                    Key = UiAction.Pick1, Name = "Lift morale",
                    Lines = SweepRules.Consequences("Burnout eases for the people present. Output this week is a little lower.", "Lift morale")
                },
                new UiDecisionChoice
                {
                    Key = UiAction.Pick2, Name = "Push output",
                    Lines = SweepRules.Consequences("Push output: More gets done this week. Burnout builds each day they work.", "Push output")
                },
            },
            Buttons = new[] { new UiActionModel { Label = "Cancel", Kind = UiButtonKind.Secondary, Key = UiAction.Back } },
        };
        m.Prompts = DeskPrompts(t);
        m.PromptCount = m.Prompts.Length;
        return m;
    }

    /// <summary>A page without choices: what the panel says, then Start on the main key and Cancel.</summary>
    public static UiDecisionModel DeskStart(UiWords t)
    {
        var m = new UiDecisionModel
        {
            Kicker = t("reallife_session_enabled", "Desk sessions"), Title = "Review the roadmap", Emblem = UiIcon.Presentation, EmblemTint = UiRole.Accent,
            Body = "You sit down at the desk of Lantern Bay Games. Two of your people are in today and the next milestone is nine days away. "
                   + "The session takes about an hour of your day. Nothing is spent until you start it, and you can leave at any time before then.",
            BodyLines = 8, Start = -1, StartButton = 1,
            Buttons = new[]
            {
                new UiActionModel { Label = "Cancel", Kind = UiButtonKind.Secondary, Key = UiAction.Back },
                new UiActionModel { Label = "Start the session", Kind = UiButtonKind.Primary, Key = UiAction.Alt },
            },
        };
        m.Prompts = DeskPrompts(t);
        m.PromptCount = m.Prompts.Length;
        return m;
    }
}
