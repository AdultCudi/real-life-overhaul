using System.Collections.Generic;
using RealLifeOverhaul.Ui;
using UnityEngine;

public static partial class UiPreviewRunner
{
    // Persona, Rivals and Market Insider on the kit in every state, over the day street the full menu leaves (the French
    // over Broadway at night), with the frame's app row under each.
    static partial void AddPersona(List<Screen> screens)
    {
        int personaRow = System.Array.IndexOf(UiSamples.RowIds, "Persona");
        foreach (string state in UiSamples.PersonaStates)
        {
            string st = state;
            var screen = new Screen
            {
                Name = "persona_" + st, AppName = "Persona", Icon = PhoneLooks.Glyph(PersonaViews.App), Backdrop = st.EndsWith("_fr") ? "11.jpg" : st.StartsWith("new") ? "02.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(personaRow, false), Lay = (s, f, d, k) => UiPersona.Lay(s, UiSamples.Persona(English, st), f, d, k)
            };
            if (st.Contains("overview"))
            {
                screen.HoverCommand = UiPersona.LinkCommand;
                screen.HoverArg = UiPersona.Education;
                screen.FocusCommand = UiAppWindow.NavCommand;
                screen.Walk = new[] { Down, Down, Right, Down, Down, Left };
            }
            else
            {
                screen.HoverCommand = UiDataTable.First + UiDataTable.Row;
                screen.HoverArg = 3;
                screen.FocusCommand = UiDataTable.First + UiDataTable.Row;
                screen.FocusArg = 1;
                screen.Walk = st.Contains("goals") ? new[] { Up, Up, Right, Right, Down, Down } : new[] { Down, Down, Down, Up, Up, Up, Up };
            }
            screens.Add(screen);
        }
        int rivalsRow = System.Array.IndexOf(UiSamples.RowIds, "Rivals");
        foreach (string state in UiSamples.RivalsStates)
        {
            string st = state;
            bool league = st.StartsWith("league");
            screens.Add(new Screen
            {
                Name = "rivals_" + st, AppName = "Rivals", Icon = PhoneLooks.Glyph(RivalsViews.App), Backdrop = st.EndsWith("_fr") ? "11.jpg" : "13.jpg",
                Bar = () => UiSamples.ShellBar(rivalsRow, false), Lay = (s, f, d, k) =>
                {
                    if (st == "league_faces")
                    {
                        StandIns();
                    }
                    UiRivals.Lay(s, UiSamples.Rivals(English, st), f, d, k);
                },
                HoverCommand = st == "league_empty" ? UiAppWindow.NavCommand : league ? UiRivals.ChartTabCommand : UiRivals.RowLinkCommand,
                HoverArg = st == "league_empty" ? 1 : league ? 1 : 0,
                FocusCommand = st == "league_empty" ? UiAppWindow.NavCommand : UiDataTable.First + UiDataTable.Row, FocusArg = st == "league_empty" ? 0 : st == "league_quiet" ? 3 : st == "league_faces" ? 5 : league ? 1 : 2,
                Walk = league ? new[] { Down, Down, Right, Down, Down, Left, Up } : new[] { Down, Down, Up, Up, Up, Right, Left }
            });
        }
        int marketRow = System.Array.IndexOf(UiSamples.RowIds, "MarketInsider");
        foreach (string state in UiSamples.MarketStates)
        {
            string st = state;
            var screen = new Screen
            {
                Name = "market_" + st, AppName = "Market Insider", Icon = PhoneLooks.Glyph(MarketViews.App), Backdrop = st.EndsWith("_fr") ? "11.jpg" : "02.jpg",
                Bar = () => UiSamples.ShellBar(marketRow, false), Lay = (s, f, d, k) => UiMarket.Lay(s, UiSamples.Market(English, st), f, d, k)
            };
            if (st == "filter_open")
            {
                screen.HoverCommand = UiMarket.OptionCommand;
                screen.HoverArg = 2;
                screen.FocusCommand = UiMarket.OptionCommand;
                screen.Walk = new[] { Down, Down, Up };
            }
            else if (st.Contains("events"))
            {
                screen.HoverCommand = UiMarket.EventCommand;
                screen.HoverArg = 2;
                screen.FocusCommand = UiMarket.EventCommand;
                screen.FocusArg = 1;
                screen.Walk = new[] { Down, Down, Up, Up, Up, Right, Left };
            }
            else
            {
                screen.HoverCommand = st == "uptown_quiet" ? UiMarket.MapCommand : UiDataTable.First + UiDataTable.Row;
                screen.HoverArg = st == "uptown_quiet" ? 0 : 4;
                screen.FocusCommand = UiDataTable.First + UiDataTable.Row;
                screen.FocusArg = 2;
                screen.Walk = new[] { Down, Down, Right, Down, Left, Left, Up };
            }
            screens.Add(screen);
        }
    }

    // In the game a rival's picture is its own sprite; here a flat tone stands in for it, so the sample shows which faces the
    // game has (a picture, however it looks) beside the painted ones (a face from the sex and age the mod records).
    private sealed class StandIn : UiFaceSource
    {
        private readonly Sprite _sprite;

        public StandIn(int tone)
        {
            var tex = new Texture2D(64, 64, TextureFormat.RGBA32, false);
            var px = new Color32[64 * 64];
            Color32 top = new Color32((byte)(88 + tone * 9), (byte)(104 + tone * 5), (byte)(128 - tone * 7), 255), foot = new Color32(58, 52, 48, 255);
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    float dx = x - 32f, dy = y - 36f;
                    px[y * 64 + x] = dx * dx + dy * dy < 15f * 15f ? new Color32(196, 150, 118, 255) : y < 20 ? foot : top;   // a head on a backdrop
                }
            }
            tex.SetPixels32(px);
            tex.Apply();
            _sprite = Sprite.Create(tex, new Rect(0, 0, 64, 64), new Vector2(0.5f, 0.5f), 100f);
        }

        public override Sprite Get() => _sprite;
    }

    private static void StandIns()
    {
        for (int i = 0; i < UiSamples.GameFaces.Length; i++)
        {
            UiFaces.Source(UiSamples.GameFaces[i], new StandIn(i));
        }
    }
}
