using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul.Ui;

internal static partial class Program
{
    // The page kind nobody drew (UiDashboard) is gone from the mod and the build, and every decision card lifts a choice under the pointer.
    private static void UiCleanupChecks()
    {
        string root = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
        string scripts = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts");
        string[] gone = { "UiDashboard", "UiDashboardModel", "UiPageShape", "UiLedgerSection", "UiDashboardScroll", "UiTemplate.Dashboard", "template_dashboard", "LiftChoices" };
        string found = "none";
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(path);
            foreach (string name in gone)
            {
                if (text.Contains(name))
                {
                    found = name + " in " + System.IO.Path.GetFileName(path);
                }
            }
        }
        string project = File.ReadAllText(System.IO.Path.Combine(root, "RealLifeOverhaul.Build", "EconomyCheck", "EconomyCheck.csproj"));
        Check(found == "none" && !project.Contains("UiDashboard") && !Enum.IsDefined(typeof(UiTemplate), "Dashboard") && typeof(UiDecisionModel).GetField("LiftChoices") == null,
            "ui cleanup: the dashboard page kind (UiDashboard, its page shapes and its template) and the decision card's lift switch are gone from Scripts and the project (" + found + ")");

        UiWords en = UiWords("en");
        SceneWords words = UiSamples.SceneWordsOf(en);
        var cards = new List<KeyValuePair<string, UiDecisionModel>>
        {
            new KeyValuePair<string, UiDecisionModel>("meeting", UiSamples.Meeting(en)),
            new KeyValuePair<string, UiDecisionModel>("desk card", UiSamples.DeskCard(en)),
            new KeyValuePair<string, UiDecisionModel>("desk start", UiSamples.DeskStart(en)),
            new KeyValuePair<string, UiDecisionModel>("ending", UiSamples.NewsEnding(en, "card").Decision),
        };
        foreach (string state in UiSamples.SceneStates)
        {
            var thread = new List<SceneSaid>();
            ScenePage page = UiSamples.ScenePageOf(en, state, thread);
            if (page.Entries.Count > 0)
            {
                cards.Add(new KeyValuePair<string, UiDecisionModel>("scene " + state, SceneViews.Decision(page, words)));
            }
        }
        var odd = new UiDecisionModel
        {
            Title = "Odd card", Body = "A greyed row, a chosen row and a row on no command.", ChoiceCmd = 0, Selected = 1,
            Choices = new[] { new UiDecisionChoice { Name = "Greyed", Disabled = true }, new UiDecisionChoice { Name = "Chosen" }, new UiDecisionChoice { Name = "Plain" } }
        };
        cards.Add(new KeyValuePair<string, UiDecisionModel>("odd", odd));

        var s = new UiScene();
        UiFrame frame = UiFrame.FullMenu(16f / 9f, "Decision");
        string bad = "none";
        int rows = 0, mostTargets = 0, disabledRows = 0;
        foreach (KeyValuePair<string, UiDecisionModel> card in cards)
        {
            UiDecisionModel m = card.Value;
            UiDecision.Lay(s, m, frame, true, UiDefaultKeys.Keyboard);
            mostTargets = Math.Max(mostTargets, s.NodeCount);
            for (int i = 0; i < m.Choices.Length; i++)
            {
                int arg = m.ChoiceArgs != null && i < m.ChoiceArgs.Length ? m.ChoiceArgs[i] : i;
                int node = s.FindNode(m.ChoiceCmd, arg);
                if (node < 0)
                {
                    bad = card.Key + " row " + i + " has no target";
                    continue;
                }
                rows++;
                UiItem face = s.Items[s.Nodes[node].Face];
                bool chosen = i == m.Selected;
                if (chosen)
                {
                    bad = face.Fill == UiRole.RowSel ? bad : card.Key + " row " + i + " chosen but not marked";
                    continue;
                }
                UiItem behind = s.Items[s.Nodes[node].Face - 1];
                bool lifts = face.Fill == UiRole.None && behind.Fill == UiRole.Row && (behind.Flags & UiFlags.Decor) != 0;
                if (!lifts)
                {
                    bad = card.Key + " row " + i + " does not lift";
                }
                if (m.Choices[i].Disabled)
                {
                    disabledRows++;
                    bool still = s.Nodes[node].Disabled && (face.Flags & UiFlags.Disabled) != 0 && (behind.Flags & UiFlags.Disabled) != 0 && s.FocusFirst != node;
                    if (!still)
                    {
                        bad = card.Key + " row " + i + " greyed but acts";
                    }
                }
            }
        }
        Check(bad == "none" && rows > 12 && disabledRows >= 1, "ui cleanup: on every decision sample (" + cards.Count + " cards, " + rows + " rows) each choice lifts under the pointer, "
              + "the chosen row keeps its fill, and a greyed row neither lifts nor acts (" + bad + ")");
        Check(mostTargets <= UiCheckRules.MostTargets, "ui cleanup: the longest decision sample holds " + mostTargets + " targets, within the " + UiCheckRules.MostTargets + " a layer may hold");
    }
}
