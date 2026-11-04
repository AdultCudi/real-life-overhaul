using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The Company app's live source: it reads the company director's own state (the businesses, the staff, the managers, the hierarchy, the rota, the tax and the
    /// licences, the cover and the campaigns) into the facts of the tab in view, from a reset state, and does what a button stands for through the director that already
    /// owns it. It holds no data of its own, never a sample: every figure and name is read from the save. Read when the company changes or the player's choice does,
    /// never every frame.
    /// </summary>
    internal sealed partial class CompanyLive : ICompanySource
    {
        public static readonly CompanyLive Instance = new CompanyLive();

        private static int _generation = 1;
        private static int _wantedTab = -1, _wantedSub, _wantedQuestion;
        private static string _wantedPick, _wantedSubPick;
        private static bool _opened;

        private CompanyDirector D => CompanyDirector.Instance;

        public int Generation => _generation;

        /// <summary>The company changed, or the app opened: the screen reads again.</summary>
        public static void Touch()
        {
            _generation++;
        }

        /// <summary>The app was opened (the director's AppOpened): its sub pages start from the tab as left.</summary>
        public static void Opened()
        {
            _opened = true;
            _generation++;
        }

        /// <summary>A part of the game asked for a tab of the old app's numbering, with something picked; the kit's own numbering is made here.</summary>
        public static void Want(int oldTab, string pick)
        {
            _wantedTab = OldToKit(oldTab);
            _wantedPick = pick;
            _wantedSub = CompanyView.SubMain;
            _wantedSubPick = null;
            _wantedQuestion = 0;
            OldPick(oldTab, pick);
            _generation++;
        }

        // The old pages' picks, as the notices and the tests ask for them, in the kit's own: "mp:place" the manager of a place, "db:place" its dashboard, "dr:area" a region's, "he:id" a question,
        // "hx:" the inbox, "xs:" the seats, "rota:place" a place's rota, "ret" the return.
        private static void OldPick(int oldTab, string pick)
        {
            if (string.IsNullOrEmpty(pick))
            {
                return;
            }
            int colon = pick.IndexOf(':');
            string kind = colon >= 0 ? pick.Substring(0, colon + 1) : pick, rest = colon >= 0 ? pick.Substring(colon + 1) : string.Empty;
            switch (oldTab)
            {
                case CompanyDirector.TabManagers:
                    switch (kind)
                    {
                        case "mp:": _wantedPick = "mgr:" + rest; break;
                        case "db:": _wantedPick = "mgr:" + rest; _wantedSub = CompanyView.SubDashboard; break;
                        case "dr:": _wantedPick = "reg:" + rest; _wantedSub = CompanyView.SubRegion; break;
                        case "hx:": _wantedPick = "own"; _wantedSub = CompanyView.SubInbox; break;
                        case "xs:": _wantedPick = "seat:" + (int)Careers.ExecSeat.Coo; _wantedSub = CompanyView.SubSeat; break;
                        case "he:": OldQuestion(rest); break;
                        default: _wantedPick = pick; break;
                    }
                    break;
                case CompanyDirector.TabRota:
                    _wantedPick = kind == "rota:" ? "p:" + rest : pick;
                    break;
                case CompanyDirector.TabOverview:
                    if (kind == "ls:")
                    {
                        _wantedQuestion = LeaseRules.QuestionId(rest);   // the landlord's offer, answered whether the hierarchy runs or not
                        _wantedPick = string.Empty;
                    }
                    break;
                case CompanyDirector.TabTax:
                    if (pick == "ret")
                    {
                        _wantedSub = CompanyView.SubReturn;
                    }
                    break;
            }
        }

        // A question the owner holds opens as the decision card; one held below (or settled) is read in the inbox.
        private static void OldQuestion(string id)
        {
            CompanyDirector d = CompanyDirector.Instance;
            int number;
            Escalation e = d != null && d.HierarchyOn && int.TryParse(id, out number) ? d.HierarchyData.Find(number) : null;
            if (e != null && e.Open && e.Level >= EscalationRules.LevelOwner)
            {
                _wantedQuestion = e.Id;
                _wantedPick = string.Empty;
                return;
            }
            _wantedPick = "own";
            _wantedSub = CompanyView.SubInbox;
            _wantedSubPick = e != null ? "he:" + e.Id : null;
        }

        // The old app's tabs were Overview, Staff, Cover, Marketing, Tax, Rota, Managers; the kit's are Overview, Staff, Managers, Rota, Marketing, Cover, Tax.
        private static int OldToKit(int tab)
        {
            switch (tab)
            {
                case CompanyDirector.TabStaff: return CompanyView.Staff;
                case CompanyDirector.TabCover: return CompanyView.Cover;
                case CompanyDirector.TabMarketing: return CompanyView.Marketing;
                case CompanyDirector.TabTax: return CompanyView.Tax;
                case CompanyDirector.TabRota: return CompanyView.Rota;
                case CompanyDirector.TabManagers: return CompanyView.Managers;
                default: return CompanyView.Overview;
            }
        }

        public bool TakeWanted(out int tab, out string pick, out int sub, out string subPick, out int question)
        {
            tab = _wantedTab;
            pick = _wantedPick;
            sub = _wantedSub;
            subPick = _wantedSubPick;
            question = _wantedQuestion;
            bool has = _wantedTab >= 0;
            _wantedTab = -1;
            _wantedPick = _wantedSubPick = null;
            _wantedSub = _wantedQuestion = 0;
            return has;
        }

        public bool TakeOpened()
        {
            bool was = _opened;
            _opened = false;
            return was;
        }

        public static CompanyWords Vocabulary() => new CompanyWords(UiText.Words, m => CompanyText.Money(m));

        // ------------------------------------------------------------------------------------------------- the read

        public void Read(CompanyFacts f, CompanyView v)
        {
            Reset(f);
            CompanyDirector d = D;
            if (d == null || !d.Running || d.State == null || d.Today <= 0)
            {
                f.Ready = false;
                return;
            }
            f.Ready = true;
            Words w = new Words(Vocabulary());
            f.Deep = d.HierarchyOn && d.HierarchyData != null && d.HierarchyData.Deep;
            ReadHead(f, d, w);
            ReadCounts(f, d, v);
            // An escalation open as a card.
            if (v.Question != 0)
            {
                Escalation e = v.Question < 0 ? d.LeaseQuestion(v.Question) : d.HierarchyOn ? d.HierarchyData.Find(v.Question) : null;
                if (e != null && e.Open)
                {
                    f.Question = QuestionOf(d, e, w, true);
                }
                if (f.Question == null)
                {
                    v.Question = 0;
                }
            }
            try
            {
                switch (v.Tab)
                {
                    case CompanyView.Overview: ReadOverview(f, d, v, w); break;
                    case CompanyView.Staff: ReadStaff(f, d, v, w); break;
                    case CompanyView.Managers: ReadManagers(f, d, v, w); break;
                    case CompanyView.Rota: ReadRota(f, d, v, w); break;
                    case CompanyView.Marketing: ReadMarketing(f, d, v, w); break;
                    case CompanyView.Cover: ReadCover(f, d, v, w); break;
                    default: ReadTax(f, d, v, w); break;
                }
            }
            catch (Exception e)
            {
                CompanyDirector.Logger?.Error(e);   // a page that cannot be read shows its own empty state; the rest of the app still opens
            }
        }

        // The state before each read: nothing of the page before is left in it, so no tab can show another's.
        private static void Reset(CompanyFacts f)
        {
            f.Ready = false;
            f.OffTitle = f.OffBody = f.Name = f.Mark = null;
            f.Counts = new string[CompanyView.Tabs];
            f.Overview = new CompanyOverviewFacts();
            f.Staff = new CompanyPage();
            f.Managers = new CompanyManagersFacts();
            f.Rota = new CompanyRotaFacts();
            f.Marketing = new CompanyPage();
            f.Cover = new CompanyPage();
            f.Tax = new CompanyTaxFacts();
            f.Business = null;
            f.Question = null;
            f.Deep = false;
        }

        /// <summary>The words a read writes with, shortened to what the live source needs.</summary>
        internal sealed class Words
        {
            public readonly CompanyWords W;

            public Words(CompanyWords w)
            {
                W = w;
            }

            public string T(string key, string english) => W.T(key, english);

            public string L(string key, string english) => W.L(key, english);

            /// <summary>A wage an hour, hundredths, as the page writes it: the money, then the per-hour words.</summary>
            public string PerHour(int hundredths) => W.F("reallife_co_per_hour", "{wage} / h", "wage", CompanyText.Wage(hundredths));

            public string F(string key, string english, params string[] pairs) => W.F(key, english, pairs);

            public string Money(int amount) => CompanyText.Money(amount);
        }

        // The company's name (its headquarters', else the player's), its mark, today.
        private static void ReadHead(CompanyFacts f, CompanyDirector d, Words w)
        {
            string name = HeadName(w);
            f.Name = name;
            f.Mark = CompanyFace.Initials(name);
        }

        private static string HeadName(Words w)
        {
            string hq = CompanyGame.HeadName();
            if (!string.IsNullOrEmpty(hq))
            {
                return hq;
            }
            string owner = CompanyGame.OwnerName();
            return string.IsNullOrEmpty(owner) ? w.T("reallife_co_app", "Company") : w.F("reallife_co_company_of", "{name}'s company", "name", owner);
        }

        // What the sidebar counts: staff, managers, and what waits on each of the others.
        private static void ReadCounts(CompanyFacts f, CompanyDirector d, CompanyView v)
        {
            CompanyState s = d.State;
            f.Counts[CompanyView.Staff] = s.Staff.Count > 0 ? s.Staff.Count.ToString() : null;
            int managers = d.ManagersOn && d.ManagerData != null ? d.ManagerData.Managers.Count : 0;
            f.Counts[CompanyView.Managers] = managers > 0 ? managers.ToString() : null;
            int licences = 0, claims = 0, campaigns = 0;
            for (int i = 0; i < d.Shops.Count; i++)
            {
                licences += d.On(CompanyPart.Licences) && LicenceRules.Missing(d.Shops[i].File, d.Shops[i].Needs, d.Today) != 0 ? 1 : 0;
                campaigns += d.Shops[i].File.Pushing(d.Today) ? 1 : 0;
            }
            for (int i = 0; d.On(CompanyPart.Cover) && i < s.Claims.Count; i++)
            {
                claims += s.Claims[i].Stage == ClaimStage.Open ? 1 : 0;
            }
            int tax = licences + (d.On(CompanyPart.Tax) && s.Tax.ReturnWaiting ? 1 : 0);
            f.Counts[CompanyView.Tax] = tax > 0 ? tax.ToString() : null;
            f.Counts[CompanyView.Cover] = claims > 0 ? claims.ToString() : null;
            f.Counts[CompanyView.Marketing] = campaigns > 0 ? campaigns.ToString() : null;
        }
    }
}
