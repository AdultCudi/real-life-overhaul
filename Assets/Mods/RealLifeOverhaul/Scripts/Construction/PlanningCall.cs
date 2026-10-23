using System;
using System.Text;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// City Hall's planning and building control, in the game's conversation panel: the sixth topic of the City Hall call
    /// (Civic's SiteBuilders.PlanningTopic), and the same pages opened from the Construction app (<see cref="Open"/>):
    /// planning applications, the contractor's licence and the council's zoning. And a site's notice board when the site is
    /// the player's company's: the works as they stand, a walk round with the foreman, the inspector booked. Pages are built
    /// when shown, never on a tick.
    /// </summary>
    public static class PlanningCall
    {
        public const string CityHallId = "reallife-cityhall";   // City Hall's contact: the call is theirs
        private const string DialogName = "reallife_planning_calldialogtype";

        private static readonly StringBuilder Text = new StringBuilder(512);

        /// <summary>The project a page opened from the app starts on; 0 for the topic's first page.</summary>
        internal static int Wanted;

        /// <summary>The planning pages opened face to face as a call to City Hall, from the app; the full menu closes first.</summary>
        public static void Open(int project)
        {
            Wanted = project;
            var dialogType = (CallDialogType)ModEnumHash.GetSafeHash(DialogName);
            CallDialogFactory.RegisterDialog(dialogType, Guard.Maker(() => new PlanningDialog()));
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (UI.Smartphone.FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
            uis.playerHUD.dialogUI.ShowDialog(dialogType, NavigationBlocker.SpecialEmployeeDialog);
        }

        private static ConstructionDirector Director => ConstructionDirector.Instance != null && ConstructionDirector.Instance.Running ? ConstructionDirector.Instance : null;

        /// <summary>The topic's first page in City Hall's call, with the way back to its first topic.</summary>
        public static DialogEntry Topic(Func<DialogEntry> back)
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return back();
            }
            BuildState s = d.State;
            int waiting = 0, deciding = 0;
            for (int i = 0; i < s.Projects.Count; i++)
            {
                ProjectFile p = s.Projects[i];
                waiting += Applicable(p) ? 1 : 0;
                deciding += p.Planning == PlanningState.Submitted || p.Planning == PlanningState.Appealed ? 1 : 0;
            }
            string text = ConstructionText.T("reallife_bld_call_topic", "ready", waiting.ToString(), "deciding", deciding.ToString(), "licence", ConstructionText.LicenceWords(s.Firm));
            return Page(text, "reallife_bld_call_applications", () => Applications(0, back), "reallife_civic_button_next_topic", back);
        }

        /// <summary>A project's application page when it may be made now, else the topic's first page.</summary>
        internal static DialogEntry ForProject(int project, Func<DialogEntry> back)
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return back();
            }
            BuildState s = d.State;
            for (int i = 0; i < s.Projects.Count; i++)
            {
                if (s.Projects[i].Id == project && Applicable(s.Projects[i]))
                {
                    return Applications(i, back);
                }
            }
            return Topic(back);
        }

        private static bool Applicable(ProjectFile p)
        {
            return p.PlansDone && (p.Stage == ProjectStage.Design || p.Planning == PlanningState.Refused || p.Planning == PlanningState.AppealLost) && p.Kind != ProjectKind.Contract;
        }

        // The projects whose plans may be submitted, one a page; then the licence, then zoning.
        private static DialogEntry Applications(int from, Func<DialogEntry> back)
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return Closing(ConstructionText.T("reallife_bld_call_closed"));
            }
            BuildState s = d.State;
            for (int i = Math.Max(0, from); i < s.Projects.Count; i++)
            {
                ProjectFile p = s.Projects[i];
                if (!Applicable(p))
                {
                    continue;
                }
                int next = i + 1, id = p.Id;
                int days = PlanningRules.WorkingDays(p.Area);
                string text = ConstructionText.Fill("reallife_bld_call_application", "project", d.ProjectName(p), "quality", p.Quality.ToString(), "fee", ConstructionText.Money(d.PlanningFee(p)),
                    "days", days.ToString());
                return Page(text, "reallife_bld_call_submit", () => Answer(d.Apply(s.Project(id))), "reallife_bld_call_next", () => Applications(next, back));
            }
            return Licence(back);
        }

        private static DialogEntry Licence(Func<DialogEntry> back)
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return Closing(ConstructionText.T("reallife_bld_call_closed"));
            }
            FirmFile f = d.State.Firm;
            string text = ConstructionText.T("reallife_bld_call_licence", "licence", ConstructionText.LicenceWords(f), "fee", ConstructionText.Money(ConstructionDirector.LicenceFee),
                "days", BuildCatalogue.LicenceDays.ToString());
            string ask = f.Licence == LicenceState.Granted ? "reallife_bld_call_renew" : "reallife_bld_call_apply";
            return Page(text, ask, () => Answer(d.AskLicence()), "reallife_bld_call_next", () => Zoning(0, 0, back));
        }

        // Each lot of the player's and each use it is not zoned for, a page; then back to the first topic.
        private static DialogEntry Zoning(int lot, int use, Func<DialogEntry> back)
        {
            ConstructionDirector d = Director;
            if (d == null)
            {
                return Closing(ConstructionText.T("reallife_bld_call_closed"));
            }
            BuildState s = d.State;
            for (int l = Math.Max(0, lot); l < s.Lots.Count; l++)
            {
                LotFile file = s.Lots[l];
                for (int u = l == lot ? use : 0; u < (int)BuildUse.Count; u++)
                {
                    if (LandRules.Zoned(file.Zones, (BuildUse)u) || file.ZoneAsk != 0)
                    {
                        continue;
                    }
                    int nextLot = u + 1 < (int)BuildUse.Count ? l : l + 1, nextUse = u + 1 < (int)BuildUse.Count ? u + 1 : 0;
                    string id = file.Id;
                    var asked = (BuildUse)u;
                    string text = ConstructionText.Fill("reallife_bld_call_zoning", "lot", d.LotWords(id), "zones", ConstructionText.ZonesWords(file.Zones),
                        "use", ConstructionText.T(BuildCatalogue.UseKeys[u]), "fee", ConstructionText.Money(ConstructionDirector.ZoneFee),
                        "day", ConstructionText.Day(LandRules.CouncilDay(ConstructionGame.Day)));
                    return Page(text, "reallife_bld_call_ask_council", () => Answer(d.AskZone(id, asked)), "reallife_bld_call_next", () => Zoning(nextLot, nextUse, back));
                }
            }
            return Page(ConstructionText.T("reallife_bld_call_done"), "reallife_bld_call_back", back, null, null);
        }

        // ------------------------------------------------------------------ a site's notice board

        /// <summary>The player's company's page at one of its sites' notice boards, or null for a site that is not its works.</summary>
        public static DialogEntry Notice(int site)
        {
            ConstructionDirector d = Director;
            ProjectFile p = d != null ? d.State.OnSite(site) : null;
            if (p == null)
            {
                return null;
            }
            int id = p.Id;
            string text = d.NoticeText(p);
            bool due = ConstructionDirector.DueInspection(p) >= 0;
            if (!p.Visited)
            {
                return Page(text, "reallife_bld_notice_walk", () => Answer(d.Visit(id)), due ? "reallife_bld_notice_book" : null, due ? (Func<DialogEntry>)(() => Answer(d.BookInspection(id))) : null);
            }
            return due ? Page(text, "reallife_bld_notice_book", () => Answer(d.BookInspection(id)), null, null) : Closing(text);
        }

        // ------------------------------------------------------------------ pages

        private static DialogEntry Answer(string key)
        {
            return Closing(ConstructionText.T(key ?? "reallife_bld_call_closed"));
        }

        /// <summary>A page: words made here, a button that does something, an optional second, and leave.</summary>
        internal static DialogEntry Page(string text, string confirmKey, Func<DialogEntry> onConfirm, string secondKey, Func<DialogEntry> onSecond)
        {
            var entry = new DialogEntry
            {
                messageData = "reallife_bld_page".Localize(new { text }),
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

        /// <summary>A last line: on the phone the call ends by itself, face to face there is a leave button.</summary>
        internal static DialogEntry Closing(string text)
        {
            bool phone = DialogController.current != null && DialogController.current.dialogType == DialogType.PhoneCall;
            return new DialogEntry
            {
                messageData = "reallife_bld_page".Localize(new { text }),
                OnVisible = phone ? DialogController.current.FinishDialog : (Action)null,
                OnCancel = phone ? (Action)null : DialogController.current.FinishDialog
            };
        }

        private static void Leave()
        {
            DialogController.current?.CancelDialog();
        }

        internal static string Join(params string[] lines)
        {
            Text.Clear();
            for (int i = 0; i < lines.Length; i++)
            {
                if (!string.IsNullOrEmpty(lines[i]))
                {
                    Text.Append(Text.Length > 0 ? "\n" : string.Empty).Append(lines[i]);
                }
            }
            return Text.ToString();
        }
    }

    /// <summary>City Hall's planning pages as a conversation opened from the Construction app.</summary>
    public sealed class PlanningDialog : Dialog
    {
        public PlanningDialog()
        {
            npcNameKey = PlanningCall.CityHallId;
            int wanted = PlanningCall.Wanted;
            PlanningCall.Wanted = 0;
            Func<DialogEntry> bye = () => PlanningCall.Closing(ConstructionText.T("reallife_bld_call_bye"));
            DialogController.current.ShowEntry(Guard.Entry(wanted > 0 ? PlanningCall.ForProject(wanted, bye) : PlanningCall.Topic(bye)));
            if (wanted > 0)
            {
                ConstructionDirector.Logger?.Info("Construction: City Hall's planning pages opened from the app for project " + wanted + ".");
            }
        }
    }
}
