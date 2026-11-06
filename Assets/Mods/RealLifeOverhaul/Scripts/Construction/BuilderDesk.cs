using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using UI.Notification;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The conversation at a front desk, in the game's own dialogue panel as its special desks open it (facing the person,
    /// the game paused): at a rival builder's office the receptionist gives the builder's quote for the player's ready
    /// project (the director's own quotes, the app's), signs the contract through the game's question box as the app's Hire
    /// this builder does, says what the builder is known for, opens the app, or says what to do first when nothing is ready;
    /// at the player's own office the office manager opens the company's pages. Pages are built when shown, never on a tick.
    /// </summary>
    public static class BuilderDesk
    {
        private const string DialogName = "reallife_builderdesk_calldialogtype";
        public const string NpcKey = "reallife_bld_desk_npc";          // the receptionist's role, over the pages and the pointer
        public const string ManagerKey = "reallife_bld_desk_manager";  // the player's own office manager's

        private static readonly Action Closed = Guard.Way(BuilderDeskGame.Release);

        /// <summary>The rival builder whose desk the conversation is at; -1 for the player's own company's office.</summary>
        internal static int Rival = -1;

        private static ConstructionDirector Director => ConstructionDirector.Instance != null && ConstructionDirector.Instance.Running ? ConstructionDirector.Instance : null;

        /// <summary>True while the game's dialogue panel is open, this conversation's or another's.</summary>
        public static bool Talking => DialogController.current != null;

        /// <summary>
        /// The conversation at a rival builder's desk (or the player's own office, <paramref name="rival"/> -1), facing
        /// <paramref name="with"/> when given. False, and nothing shown, when the game's panel is not there or one is open.
        /// </summary>
        public static bool Open(int rival, ThirdPersonCharacter with)
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.playerHUD == null || uis.playerHUD.dialogUI == null || DialogController.current != null)
            {
                return false;
            }
            Rival = rival >= 0 && rival < BuildCatalogue.RivalCount ? rival : -1;
            var type = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
            CallDialogFactory.RegisterDialog(type, Guard.Maker(() => new BuilderDeskDialog()));
            if (UI.Smartphone.FullMenu.IsOpen && uis.fullMenu != null)
            {
                uis.fullMenu.Toggle(false);
            }
            uis.playerHUD.dialogUI.ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, null, Closed, with);
            BuilderDeskGame.Hold();
            return true;
        }

        /// <summary>The first page: the office manager's at the player's own office, the receptionist's welcome at a builder's.</summary>
        internal static DialogEntry First()
        {
            return Rival < 0 ? Office() : Hello();
        }

        private static string Builder(int rival) => ConstructionText.T(BuildCatalogue.RivalKeys[BuildCatalogue.Clamp(rival, 0, BuildCatalogue.RivalCount - 1)]);

        // ------------------------------------------------------------------ a builder's office

        private static DialogEntry Hello()
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return Closing(ConstructionText.T("reallife_bld_desk_closed"));
            }
            DeskStep step = d.DeskStepNow(out int project);
            string hello = ConstructionText.T("reallife_bld_desk_hello", "builder", Builder(Rival));
            if (step == DeskStep.Ready)
            {
                return Page(hello, "reallife_bld_desk_ask_quote", () => Quote(project), "reallife_bld_desk_ask_about", About);
            }
            return Need(PlanningCall.Join(hello, NeedWords(d, step, project)), step, project);
        }

        // Nothing ready to quote: what to do first, and the app's page for it.
        private static DialogEntry Need(string text, DeskStep step, int project)
        {
            if (step == DeskStep.Land)
            {
                return Page(text, "reallife_bld_desk_open_land", () => App(ConstructionDirector.TabLand, 0), "reallife_bld_desk_ask_about", About);
            }
            return Page(text, "reallife_bld_desk_open_app", () => App(ConstructionDirector.TabProjects, project), "reallife_bld_desk_ask_about", About);
        }

        private static string NeedWords(ConstructionDirector d, DeskStep step, int project)
        {
            string key = FrontDeskRules.NeedKey(step);
            ProjectFile p = d.State.Project(project);
            if (key == null)
            {
                return null;
            }
            return ConstructionText.T(key, "project", p != null ? d.ProjectName(p) : string.Empty, "builder", p != null && p.Contractor >= 0 ? Builder(p.Contractor) : string.Empty);
        }

        // The builder's quote this week for the player's ready project, as the app's quotes page has it, and Hire.
        private static DialogEntry Quote(int project)
        {
            ConstructionDirector d = Director;
            ProjectFile p = d != null ? d.State.Project(project) : null;
            if (p == null || !ConstructionDirector.HireKind(p))
            {
                return d == null ? Closing(ConstructionText.T("reallife_bld_desk_closed")) : Hello();
            }
            int r = Rival;
            if (!d.DeskQuote(p, r, out TenderBid bid))
            {
                return Closing(ConstructionText.T("reallife_bld_desk_closed"));
            }
            long price = CommissionRules.Price(bid);
            string text = ConstructionText.Fill("reallife_bld_desk_quote", "project", d.ProjectName(p), "amount", ConstructionText.Money(price), "days", ConstructionText.N(bid.Days),
                "deposit", ConstructionText.Money(CommissionRules.Deposit(price)), "day", ConstructionText.Day(CommissionRules.StandUntil(ConstructionGame.Day)));
            string block = d.HireBlock(p);
            if (block != null)
            {
                return Page(PlanningCall.Join(text, d.BlockWords(p, block)), null, null, "reallife_bld_desk_open_app", () => Quotes(project));
            }
            return Page(text, "reallife_bld_desk_ask_hire", () => Sign(project, r), "reallife_bld_desk_open_app", () => Quotes(project));
        }

        // The contract signed as the app's Hire this builder signs it: the dialogue closed, the game's question box, then the hire.
        private static DialogEntry Sign(int project, int rival)
        {
            ConstructionDirector d = Director;
            ProjectFile p = d != null ? d.State.Project(project) : null;
            if (p == null || !d.DeskQuote(p, rival, out TenderBid bid))
            {
                return Closing(ConstructionText.T("reallife_bld_desk_closed"));
            }
            long deposit = CommissionRules.Deposit(CommissionRules.Price(bid));
            DialogController.current?.FinishDialog();
            var body = new Dictionary<string, string> { { "name", Builder(rival) }, { "amount", ConstructionText.Money(deposit) } };
            GameConfirm.Show("reallife_bld_confirm_hire".Localize(), "reallife_bld_confirm_hire_body".Localize(body), () => Hired(project, rival), null, null, null, true, false);
            return null;
        }

        private static void Hired(int project, int rival)
        {
            ConstructionDirector d = Director;
            ProjectFile p = d != null ? d.State.Project(project) : null;
            if (p == null)
            {
                return;
            }
            string answer = d.HireBuilder(p, rival, false);
            ConstructionDirector.Logger?.Info("Construction: project " + project + " put to builder " + rival + " at its front desk: " + answer + ".");
            Notifications.Show(NotificationType.Info, answer, null, 5f, null, null, true, false);
        }

        // What the builder is known for: its speciality, standing, crews, record and what it built for the player.
        private static DialogEntry About()
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return Closing(ConstructionText.T("reallife_bld_desk_closed"));
            }
            int r = BuildCatalogue.Clamp(Rival, 0, BuildCatalogue.RivalCount - 1);
            RivalFile rival = d.State.Rivals[r];
            string speciality = ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[r]]);
            string text = ConstructionText.Fill("reallife_bld_desk_about", "builder", Builder(r), "speciality", speciality,
                "reputation", ConstructionText.N(rival.Reputation), "crews", ConstructionText.N(BuildCatalogue.RivalCrews[r]), "won", ConstructionText.N(rival.Won),
                "built", ConstructionText.N(rival.Built), "yours", ConstructionText.N(d.HiredBy(r)));
            DeskStep step = d.DeskStepNow(out int project);
            if (step == DeskStep.Ready)
            {
                return Page(text, "reallife_bld_desk_ask_quote", () => Quote(project), "reallife_bld_desk_open_app", () => App(ConstructionDirector.TabRivals, r + 1));
            }
            return Page(text, "reallife_bld_desk_back", Hello, "reallife_bld_desk_open_app", () => App(ConstructionDirector.TabRivals, r + 1));
        }

        private static DialogEntry Quotes(int project)
        {
            DialogController.current?.FinishDialog();
            Director?.OpenQuotes(project);
            return null;
        }

        // ------------------------------------------------------------------ the player's own office

        private static DialogEntry Office()
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return Closing(ConstructionText.T("reallife_bld_desk_closed"));
            }
            int underWay = FrontDeskRules.UnderWay(d.State.Projects, out int building);
            string text = ConstructionText.T("reallife_bld_desk_office", "projects", ConstructionText.N(underWay), "building", ConstructionText.N(building), "crews",
                ConstructionText.N(d.State.Crews.Count));
            return Page(text, "reallife_bld_desk_open_overview", () => App(ConstructionDirector.TabOverview, 0), "reallife_bld_desk_more", OfficeMore);
        }

        private static DialogEntry OfficeMore()
        {
            return Page(ConstructionText.T("reallife_bld_desk_office_more"), "reallife_bld_desk_open_projects", () => App(ConstructionDirector.TabProjects, 0),
                "reallife_bld_desk_open_crews", () => App(ConstructionDirector.TabCrews, 0));
        }

        // ------------------------------------------------------------------ pages

        // The conversation over, then the app on a tab with a row picked (0 for none).
        private static DialogEntry App(int tab, int pick)
        {
            DialogController.current?.FinishDialog();
            Director?.Open(tab, pick);
            return null;
        }

        /// <summary>A page: words made here, a button that does something, an optional second, and Goodbye.</summary>
        private static DialogEntry Page(string text, string confirmKey, Func<DialogEntry> onConfirm, string secondKey, Func<DialogEntry> onSecond)
        {
            var entry = new DialogEntry
            {
                headerKey = Rival < 0 ? ManagerKey : NpcKey,
                messageData = "reallife_bld_page".Localize(new { text }),
                CancelTextOverride = "reallife_bld_desk_bye",
                OnCancel = Leave
            };
            if (onConfirm != null)
            {
                entry.ConfirmTextOverride = confirmKey.Localize();
                entry.OnConfirm = onConfirm;
            }
            if (onSecond != null)
            {
                entry.SecondOptionTextOverride = secondKey;
                entry.OnSecondOption = onSecond;
            }
            return entry;
        }

        private static DialogEntry Closing(string text)
        {
            return new DialogEntry
            {
                headerKey = Rival < 0 ? ManagerKey : NpcKey,
                messageData = "reallife_bld_page".Localize(new { text }),
                CancelTextOverride = "reallife_bld_desk_bye",
                OnCancel = DialogController.current != null ? DialogController.current.FinishDialog : (Action)null
            };
        }

        private static void Leave()
        {
            DialogController.current?.CancelDialog();
        }

        // ------------------------------------------------------------------ for the tests

        /// <summary>Every page's words at a builder's desk without showing one (the test run's check): the welcome, the quote, the builder.</summary>
        internal static string Words(int rival, out bool quoted)
        {
            int was = Rival;
            Rival = rival;
            try
            {
                ConstructionDirector d = Director;
                quoted = false;
                if (d == null)
                {
                    return null;
                }
                DeskStep step = d.DeskStepNow(out int project);
                string hello = ConstructionText.T("reallife_bld_desk_hello", "builder", Builder(rival));
                string need = NeedWords(d, step, project);
                string about = ConstructionText.Fill("reallife_bld_desk_about", "builder", Builder(rival), "speciality",
                    ConstructionText.T(BuildCatalogue.SpecialityKeys[(int)BuildCatalogue.RivalSpeciality[rival]]), "reputation", ConstructionText.N(d.State.Rivals[rival].Reputation),
                    "crews", ConstructionText.N(BuildCatalogue.RivalCrews[rival]), "won", ConstructionText.N(d.State.Rivals[rival].Won), "built", ConstructionText.N(d.State.Rivals[rival].Built),
                    "yours", ConstructionText.N(d.HiredBy(rival)));
                ProjectFile p = d.State.Project(project);
                quoted = step == DeskStep.Ready && p != null && d.DeskQuote(p, rival, out _);
                return PlanningCall.Join(hello, need, about);
            }
            finally
            {
                Rival = was;
            }
        }
    }

    /// <summary>The front desk's conversation in the game's dialogue panel, opened by BuilderDesk.Open.</summary>
    public sealed class BuilderDeskDialog : Dialog
    {
        public BuilderDeskDialog()
        {
            npcNameKey = BuilderDesk.Rival < 0 ? BuilderDesk.ManagerKey : BuilderDesk.NpcKey;
            DialogController.current.ShowEntry(Guard.Entry(BuilderDesk.First()));
        }
    }
}
