using System;
using RealLifeOverhaul.Ui;

// The owner, 11 Nov: the taxi's "Travel here" was not right. On the building's panel it read "TRAVEL HERE" with "(2 MINUTES, $6)" cut off at the
// button's foot: the game writes its label on two lines, and the panel drew it as one line in a one-line button. In the map dock's picked place it
// read "TRAVEL HERE, (2 / MINUTES, $6)": the dock joined the game's two lines with a comma and the button wrapped that where it fell. The words are
// now the game's two lines on both, and a button whose words take more than one line is as tall as they need, in every language.
// Run: --taxi-pick; the lines read "taxi label: ...".
internal static partial class Program
{
    private static readonly string[] TaxiLabelLangs = { "en", "de", "es", "fr", "ko", "nl", "pl", "pt", "ru", "tr", "zh-cn" };

    private static void UiTaxiLabelChecks()
    {
        var frame = new UiFrame { Width = 1920f, Height = 1080f, Band = UiFrame.BandHeight };
        float lh = UiTextRules.LineHeight(UiStyle.Button);
        foreach (string lang in TaxiLabelLangs)
        {
            UiWords t = UiWords(lang);
            string template = t("ba:buildingresume_taxi_travel", "Travel here\n({info})");
            string minutes = t("bizman_factory_production_time_per_product_label", "{minutes} minutes");
            foreach (string[] ride in new[] { new[] { "2", "$6" }, new[] { "14", "$54" }, new[] { "120", "$450" } })
            {
                // The game's own making (BuildingResume.SetClickLabels): its minutes, a comma and the fare, in the brackets of its two line label.
                string info = minutes.Replace("{minutes}", ride[0]) + ", " + ride[1];
                string raw = template.Replace("{info}", info);
                string what = "taxi label: " + lang + ", " + ride[0] + " min";
                string[] game = MapText.Plain(raw).Split('\n');
                string label = MapText.TaxiLabel(raw, "none");
                Check(label == string.Join("\n", game), what + ": the dock's words are the game's lines as the game writes them (\"" + label.Replace("\n", "|") + "\")");
                Check(label.IndexOf(", (", StringComparison.Ordinal) < 0 && label.IndexOf(",\n", StringComparison.Ordinal) < 0,
                    what + ": no comma stands between \"" + game[0] + "\" and the minutes");

                // The building's panel: the game's words, as the panel reads them off the game's label, in its footer's wide button.
                var info2 = new UiInfoModel
                {
                    Title = "22 2nd Avenue", Icon = UiIcon.Store, Lead = "The Green Salad", Mark = "TG", FooterLabel = MapText.Plain(raw),
                    Footer = new[] { UiIcon.Briefcase, UiIcon.Navigation }, FooterTints = new UiRole[0],
                };
                var ps = new UiScene();
                UiInfoPanel.Lay(ps, info2, frame, true, UiDefaultKeys.Keyboard);
                TaxiLabelWhole(ps, ps.FindNode(UiInfoPanel.FooterCommand, -1), MapText.Plain(raw), lh, what + ", the building's panel");

                // The map dock's picked place in taxi mode.
                var map = new MapScreenModel { Filters = UiSamples.MapFilters(t, "taxi"), Buttons = UiSamples.MapButtons(t, false) };
                map.Filters.Place.DestinationLabel = label;
                var ms = new UiScene { Draggable = true };
                UiMap.Lay(ms, map, frame, true, UiDefaultKeys.Keyboard);
                TaxiLabelWhole(ms, ms.FindNode(UiMapFilters.PickedDestinationCommand, 0), label, lh, what + ", the map dock");
            }
        }

        // A one line label keeps the one line button it always had.
        var bizman = new UiInfoModel { Title = "22 2nd Avenue", Icon = UiIcon.Store, FooterLabel = "Open in BizMan", Footer = new[] { UiIcon.Navigation } };
        var bs = new UiScene();
        UiInfoPanel.Lay(bs, bizman, frame, true, UiDefaultKeys.Keyboard);
        int bn = bs.FindNode(UiInfoPanel.FooterCommand, -1);
        Check(bn >= 0 && Math.Abs(bs.Nodes[bn].H - UiTokens.Control) < 0.01f, "taxi label: a one line footer button (Open in BizMan) keeps a control's height");
    }

    // The button's words are drawn whole: every line they take in the room they are given is drawn, and inside the button's face.
    private static void TaxiLabelWhole(UiScene s, int node, string label, float lh, string what)
    {
        if (node < 0)
        {
            Check(false, what + ": the button is drawn");
            return;
        }
        ref UiNode n = ref s.Nodes[node];
        int text = -1;
        for (int i = Math.Max(0, n.Face); i < s.ItemCount && text < 0; i++)
        {
            text = s.Items[i].Kind == UiKind.Text && s.Items[i].Text == label ? i : -1;
        }
        if (text < 0)
        {
            Check(false, what + ": the button's words are drawn as given");
            return;
        }
        ref UiItem it = ref s.Items[text];
        int needs = UiTextRules.Lines(it.Text, UiStyle.Button, it.W, 0, out float _);
        Check(needs <= it.Value + 0.01f, what + ": all " + needs + " lines of the words are drawn (it draws " + it.Value + ")");
        Check(it.Y >= n.Y - 0.5f && it.Y + needs * lh <= n.Y + n.H + 0.5f,
            what + ": the words stand inside the button (" + it.Y + " to " + (it.Y + needs * lh) + " in " + n.Y + " to " + (n.Y + n.H) + ")");
    }
}
