using System;
using BAModAPI;
using RealLifeOverhaul.GameUi;
using UI.Smartphone;
using UnityEngine;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// The Parties app on the phone. On the RLO interface its pages are the kit's (Ui/Parties: Host a party, Staff events,
    /// the Diary). Without the interface the panel built here from the game's own shows what is coming, with each
    /// thing's answer and its way out, and says that planning a new one is done on the interface, as the living economy's
    /// other pages are. Nothing per frame: the page is written when the app opens, a tab is pressed or a button is.
    /// </summary>
    internal sealed class PartyApp
    {
        private const string GuideTitle = "reallife_meta_title_parties";

        public static IModLogger Logger;

        private readonly PartyDirector _director;
        private PhoneApp _app;
        private AppShell _shell;
        private bool _gaveUp;
        private int _tab;

        public PartyApp(PartyDirector director)
        {
            _director = director;
        }

        private bool Showing => _shell != null && _shell.Root != null && _shell.Root.activeInHierarchy;

        /// <summary>Called once a second until it takes, since the phone is built a little after the city.</summary>
        public void Install()
        {
            if (_app != null || _gaveUp || !PhoneApp.PhoneUp)
            {
                return;   // on the phone already; or the game's phone is not built yet: asked again next time, nothing made for it
            }
            var def = new PhoneAppDef
            {
                Id = PartyDirector.AppId,
                TitleKey = "reallife_party_app",
                Icon = like => AppIconPainter.Paint(like, Glyph(), new Color32(232, 118, 150, 255), new Color32(150, 52, 96, 255), 0.42f, "AppIcon-Parties"),
                Build = Build,
                Opened = () => UiClone.Safe(Write),
                Closed = () => { },
                Kit = () => new RealLifeOverhaul.Ui.VentScreen(RealLifeOverhaul.Ui.PartiesLive.Instance, RealLifeOverhaul.Ui.PartiesLive.ScreenId),
                KitScreen = RealLifeOverhaul.Ui.PartiesLive.ScreenId,
                KitIcon = RealLifeOverhaul.Ui.UiIcon.ForkKnife
            };
            _app = PhoneApp.Install(def, out bool tryAgain);
            if (_app == null)
            {
                _gaveUp = !tryAgain;
                if (_gaveUp)
                {
                    Logger?.Warn("Parties: the game's phone is not built as this version expects, so the Parties app is left off. What is planned still runs, "
                                 + "and the notices and the Planner still say what is coming.");
                }
                return;
            }
            PhoneApp.Wants[PartyDirector.AppId] = RealLifeOverhaul.Ui.PartiesLive.WantFrom;
            RealLifeOverhaul.Ui.UiShell.Face(_app, def);   // the kit's face over the app, once its switch is on
        }

        /// <summary>The game's own pictures the app's icon is drawn from, the first it has: asked for before the one look behind the loading screen.</summary>
        public static readonly string[] AppGlyphs = { "Icon-People", "Icon-Home", "Icon-Calendar" };

        private static Sprite Glyph()
        {
            for (int i = 0; i < AppGlyphs.Length; i++)
            {
                Sprite sprite = GameSprites.Get(AppGlyphs[i]);
                if (sprite != null)
                {
                    return sprite;
                }
            }
            return null;
        }

        public void Remove()
        {
            _app?.Remove();
            _app = null;
            _shell = null;
            _gaveUp = false;
        }

        private GameObject Build(FullMenu menu, Transform dormant)
        {
            using var piece = SessionWatch.Piece("parties: the Parties app being built from the game's MyEmployees");   // once per city
            _shell = AppShell.Make(menu, dormant, PartyDirector.AppId);
            if (_shell == null)
            {
                return null;
            }
            UiClone.Key(_shell.TabLabels[0], "reallife_party_tab_diary");
            UiClone.Key(_shell.TabLabels[1], "reallife_party_tab_past");
            UiClone.Rewire(_shell.Tabs[0], () => UiClone.Safe(() => Show(0)));
            UiClone.Rewire(_shell.Tabs[1], () => UiClone.Safe(() => Show(1)));
            return _shell.Root;
        }

        private void Show(int tab)
        {
            _tab = tab;
            Write();
        }

        // The page as the game's own panel: what is coming (or what is over), each with its answer and its way out.
        private void Write()
        {
            if (_shell == null)
            {
                return;
            }
            _shell.ShowTab(_tab);
            UiClone.Words(_shell.Header, PartyText.T("reallife_party_app"));
            InfoBox box = _shell.Box;
            box.Clear();
            box.Title(PartyText.T("reallife_party_app"));
            PartyFile f = PartyDirector.On ? _director.File : null;
            IPartyWorld w = _director.World;
            if (f == null)
            {
                box.Line(PartyText.T("reallife_party_off_body"), CellTone.Yellow);
                Meta.Guide.Point(box, GuideTitle);
                box.Done();
                return;
            }
            if (w.Guest)
            {
                box.Line(PartyText.T(PartyKeys.Refusal(PartyRefusal.Guest)), CellTone.Yellow);
            }
            int shown = 0, now = w.Now;
            for (int i = 0; i < f.Parties.Count; i++)
            {
                Party p = f.Parties[i];
                if (p.Over != (_tab == 1))
                {
                    continue;
                }
                shown++;
                int id = p.Id;
                box.Entry(p.Name, w.Cash(p.Paid), PartyText.Fill("reallife_party_row_party", "when", w.When(p.Day, p.Hour), "count", PartyText.Whole(p.Over ? p.Came : p.Coming),
                    "state", PartyText.T(PartyKeys.State(p.State))));
                if (p.State == PartyState.Planned && !w.Guest)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_party_d_cancel", () => UiClone.Safe(() => { _director.CancelParty(id); Write(); }));
                }
            }
            for (int i = 0; i < f.Invites.Count; i++)
            {
                Invite v = f.Invites[i];
                if (v.Open == (_tab == 1))
                {
                    continue;
                }
                shown++;
                int id = v.Id;
                SocialPerson host = w.Person(v.Person);
                box.Entry(PartyText.Fill("reallife_party_row_invite_title", "name", host != null ? host.Name : string.Empty), string.Empty,
                    PartyText.Fill("reallife_party_row_invite", "place", v.Name, "when", w.When(v.Day, v.Hour), "state", PartyText.T(PartyKeys.Invite(v.State, v.Began >= 0))));
                if (v.State == InviteState.Asked && now <= v.Start && !w.Guest)
                {
                    Transform row = box.ButtonRow();
                    InfoBox.Button(row, InfoBox.Positive, "reallife_party_d_accept", () => UiClone.Safe(() => { _director.Reply(id, true); Write(); }));
                    InfoBox.Button(row, InfoBox.Additional, "reallife_party_d_decline", () => UiClone.Safe(() => { _director.Reply(id, false); Write(); }));
                }
            }
            for (int i = 0; i < f.Events.Count; i++)
            {
                StaffEvent e = f.Events[i];
                bool over = e.State != StaffEventState.Planned && e.State != StaffEventState.Running;
                if (over != (_tab == 1))
                {
                    continue;
                }
                shown++;
                int id = e.Id;
                box.Entry(PartyText.Fill("reallife_party_row_event_title", "kind", PartyText.T(PartyKeys.Kind(e.Kind)), "business", e.Name), w.Cash(e.Paid),
                    PartyText.Fill("reallife_party_row_event", "when", w.When(e.Day, e.Hour), "count", PartyText.Whole(over ? e.Came : e.Heads),
                        "state", PartyText.T(PartyKeys.Event(e.State))));
                if (e.State == StaffEventState.Planned && !w.Guest)
                {
                    InfoBox.Button(box.ButtonRow(), InfoBox.Negative, "reallife_party_d_cancel", () => UiClone.Safe(() => { _director.CancelEvent(id); Write(); }));
                }
            }
            if (shown == 0)
            {
                box.Line(PartyText.T(_tab == 1 ? "reallife_party_d_past_none" : "reallife_party_d_none_title"));
            }
            box.Note(PartyText.T("reallife_party_classic_note"));
            Meta.Guide.Point(box, GuideTitle);
            box.Done();
        }
    }
}
