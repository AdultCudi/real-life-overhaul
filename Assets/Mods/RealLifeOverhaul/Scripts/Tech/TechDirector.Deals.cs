using System;
using UI.Notification;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What the player does in the venture part: backing startups, the fund, and raising money for their own firms.</summary>
    public sealed partial class TechDirector
    {
        /// <summary>The founders' pitch meeting for a startup that is raising.</summary>
        internal bool MeetFounders(int id)
        {
            Startup s = _ventureOn ? _venture.StartupOf(id) : null;
            if (s == null || !s.Alive || _pitch == null || _pitch.Active)
            {
                return false;
            }
            return _pitch.Founders(s);
        }

        /// <summary>A due diligence report bought on a startup: its true revenue and the founders' quality.</summary>
        internal bool Diligence(int id)
        {
            Startup s = _ventureOn ? _venture.StartupOf(id) : null;
            if (s == null || s.Diligence)
            {
                return false;
            }
            if (!VentureGame.Pay(Level(VentureCatalogue.DiligenceFee), VentureGame.DiligenceKey, s.Name))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            s.Diligence = true;
            s.Revealed |= 3;
            ChangedTech();
            return true;
        }

        /// <summary>The founders' ask taken up, as an angel (the player's cash) or from the fund.</summary>
        internal bool Back(int id, bool fund)
        {
            Startup s = _ventureOn ? _venture.StartupOf(id) : null;
            if (s == null || s.Ask == null || Save == null)
            {
                return false;
            }
            long amount = s.Ask.Amount;
            if (fund)
            {
                if (!_venture.Fund.Open || _venture.Fund.Cash < amount)
                {
                    Notifications.ShowInsufficientMoney();
                    return false;
                }
            }
            else if (!VentureGame.Pay(Level(amount), VentureGame.InvestKey, s.Name))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            if (!VentureRules.Invest(_venture, s, fund ? (int)HolderKind.Fund : (int)HolderKind.Angel, false, Save.Day, HiringGame.Cycle, HypeNow, null))
            {
                if (!fund)
                {
                    VentureGame.Give(Level(amount), VentureGame.ReturnKey, s.Name);   // the round could not close: the money comes back
                }
                return false;
            }
            if (fund)
            {
                _venture.Fund.Cash -= amount;
                _venture.Fund.Invested += amount;
            }
            TellTech(NotificationType.Success, "reallife_vc_note_backed", Data("name", s.Name, "amount", TechText.Cents(Level(amount))), TabStartups, TechApp.StartupRow + s.Id);
            ChangedTech();
            return true;
        }

        internal void Haggle(int id)
        {
            Startup s = _ventureOn ? _venture.StartupOf(id) : null;
            if (s == null || s.Ask == null || Save == null)
            {
                return;
            }
            bool yes = VentureRules.Haggle(_venture, s, HiringGame.Cycle, Save.Day);
            TellTech(yes ? NotificationType.Success : NotificationType.Info, yes ? "reallife_vc_note_haggle_yes" : "reallife_vc_note_haggle_no",
                Data("founder", s.Founder, "pre", TechText.Cents(Level(s.Ask.Pre))), TabStartups, TechApp.StartupRow + s.Id);
            ChangedTech();
        }

        internal void Pass(int id)
        {
            Startup s = _ventureOn ? _venture.StartupOf(id) : null;
            if (s != null)
            {
                s.Ask = null;
                ChangedTech();
            }
        }

        /// <summary>A follow-on taken: the player keeps their share of the last round, as an angel or from the fund.</summary>
        internal bool FollowOn(int id, bool fund)
        {
            Startup s = _ventureOn ? _venture.StartupOf(id) : null;
            if (s == null || !s.Alive)
            {
                return false;
            }
            long amount = VentureRules.FollowOnAmount(s);
            if (amount <= 0)
            {
                return false;
            }
            if (fund)
            {
                if (_venture.Fund.Cash < amount)
                {
                    Notifications.ShowInsufficientMoney();
                    return false;
                }
                _venture.Fund.Cash -= amount;
                _venture.Fund.Invested += amount;
            }
            else if (!VentureGame.Pay(Level(amount), VentureGame.InvestKey, s.Name))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            VentureRules.TakeFollowOn(s, fund ? (int)HolderKind.Fund : (int)HolderKind.Angel, amount);
            ChangedTech();
            return true;
        }

        /// <summary>The fund opened, or more of the player's money put in (cents at today's prices).</summary>
        internal bool FundIn(long cents)
        {
            if (!_ventureOn || cents <= 0 || !VentureGame.Pay(cents, VentureGame.FundKey, TechText.T("reallife_vc_fund_name")))
            {
                Notifications.ShowInsufficientMoney();
                return false;
            }
            long at = cents * 1000 / Math.Max(1, Prices.Level);
            _venture.Fund.Open = true;
            _venture.Fund.Committed += at;
            _venture.Fund.Cash += at;
            ChangedTech();
            return true;
        }

        /// <summary>The fund's cash not invested taken back by the player.</summary>
        internal void FundOut()
        {
            if (!_ventureOn || _venture.Fund.Cash <= 0)
            {
                return;
            }
            VentureGame.Give(Level(_venture.Fund.Cash), VentureGame.FundBackKey, TechText.T("reallife_vc_fund_name"));
            _venture.Fund.Committed = Math.Max(0, _venture.Fund.Committed - _venture.Fund.Cash);
            _venture.Fund.Cash = 0;
            ChangedTech();
        }

        internal Cap CapFor(string place, bool make)
        {
            if (!_ventureOn)
            {
                return null;
            }
            Cap c = _venture.CapOf(place);
            if (c == null && make)
            {
                c = VentureRules.NewCap(place);
                _venture.Caps.Add(c);
            }
            return c;
        }

        /// <summary>A raise started for a firm: venture firms ask to meet over the next days.</summary>
        internal bool StartRaise(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = firm != null ? CapFor(place, true) : null;
            if (c == null || Save == null || c.RaiseUntil > 0)
            {
                return false;
            }
            c.RaiseUntil = Save.Day + VentureCatalogue.RaiseDays;
            c.Meetings = 0;
            c.Sheets.Clear();
            TellTech(NotificationType.Info, "reallife_vc_note_raise_started", Data("firm", firm.File.Name, "day", c.RaiseUntil.ToString()), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        /// <summary>The player's pitch to the next venture firm that will meet the firm.</summary>
        internal bool MeetVc(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || c.RaiseUntil == 0 || Save == null || _pitch == null || _pitch.Active || c.Meetings >= VentureCatalogue.VcMeetingsMost)
            {
                return false;
            }
            int vc = NextVc(c, Save.Day);
            if (vc < 0)
            {
                Notifications.ShowError("reallife_vc_none_to_meet");
                return false;
            }
            long mrr = FirmMrr(place);
            long growth = c.LastMrr > 0 ? Math.Max(0, (mrr * 1000 / Math.Max(1, Prices.Level) - c.LastMrr) * 1000 / c.LastMrr) : 100;
            if (!_pitch.Player(place, vc, mrr, growth, firm.People))
            {
                return false;   // the meeting never opened: none of the raise's meetings is used
            }
            c.Meetings++;
            return true;
        }

        /// <summary>The term sheet a meeting earned, and the words for the meeting's last page.</summary>
        internal string VcSheet(string place, int vc, int score)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || Save == null)
            {
                return string.Empty;
            }
            long arr = FirmsRules.Arr(_firmsFile, place);
            long value = Math.Max(VentureCatalogue.FloorValue[0], FirmsRules.Value(firm.File, arr, 500));
            long amount = Math.Max(VentureCatalogue.TicketLeast * 4, value / 4);
            TermSheet t = VentureRules.Sheet(vc, value, amount, score, VentureRules.Mood(HiringGame.Cycle), Save.Day);
            if (score < 350)
            {
                return TechText.T("reallife_vc_meet_no_sheet", "firm", VentureCatalogue.Firms[vc].Name);
            }
            c.Sheets.Add(t);
            TellTech(NotificationType.Success, "reallife_vc_note_sheet", Data("vc", VentureCatalogue.Firms[vc].Name, "amount", TechText.Cents(Level(t.Amount)), "pre", TechText.Cents(Level(t.Pre))),
                TabFunding, TechApp.CapRow + place);
            return TechText.Fill("reallife_vc_meet_sheet", "firm", VentureCatalogue.Firms[vc].Name, "amount", TechText.Cents(Level(t.Amount)), "pre", TechText.Cents(Level(t.Pre)),
                "share", TechText.Mille(t.EquityMille), "day", t.Lapse.ToString());
        }

        /// <summary>A venture firm that caught the player stretching the truth closes its door for a while.</summary>
        internal void VcCaught(string place, int vc)
        {
            if (_ventureOn && Save != null && vc >= 0 && vc < VentureCatalogue.FirmCount)
            {
                _venture.ClosedUntil[vc] = Save.Day + 60;
                TechBridges.Fame?.Invoke(3, -1);
            }
        }

        /// <summary>A term sheet taken: the money comes into the firm and the investor joins the cap table and the board.</summary>
        internal bool TakeSheet(string place, int index)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || index < 0 || index >= c.Sheets.Count || Save == null)
            {
                return false;
            }
            TermSheet t = c.Sheets[index];
            long money = VentureRules.Accept(c, t, FirmMrr(place) * 1000 / Math.Max(1, Prices.Level), Save.Day);
            VentureGame.Give(Level(money), VentureGame.RaiseKey, firm.File.Name, firm.Reg);
            Story("reallife_tech_news_raised", "name", firm.File.Name, "amount", TechText.Cents(Level(money)));
            string lead = VentureCatalogue.Firms[TalentCatalogue.Clamp(t.Vc, 0, VentureCatalogue.FirmCount - 1)].Name;
            TellTech(NotificationType.Success, "reallife_vc_note_closed", Data("firm", firm.File.Name, "amount", TechText.Cents(Level(money)), "vc", lead), TabFunding, TechApp.CapRow + place);
            ChangedTech();
            return true;
        }

        /// <summary>A bridge loan from the last lead: six months of the firm's burn, owed until the next round converts it.</summary>
        internal bool TakeBridge(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || c.VcSeats == 0 || c.Bridge > 0 || Save == null)
            {
                return false;
            }
            long burn = Math.Max(0, -FirmProfit(firm)) * 1000 / Math.Max(1, Prices.Level);
            long loan = Math.Max(VentureCatalogue.TicketLeast, VentureRules.BridgeFor(burn));
            c.Bridge = loan;
            c.BridgeDay = Save.Day;
            VentureGame.Give(Level(loan), VentureGame.BridgeKey, firm.File.Name, firm.Reg);
            ChangedTech();
            return true;
        }

        /// <summary>The firm's average day of profit over the last week, cents at today's prices (negative: its burn).</summary>
        internal long FirmProfit(FirmPlace firm)
        {
            try
            {
                return (long)(Economy.Books.AverageProfit(firm.Reg.Address, 7, out _) * 100f);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>Share options split among the firm's people with any still waiting; refused, with a warning, while nobody works there.</summary>
        internal bool GrantOptions(string place)
        {
            Cap c = CapFor(place, true);
            if (c == null || Save == null || c.OptionsGranted >= 150)
            {
                return false;
            }
            int people = PeopleAt(place);
            if (people == 0)
            {
                FirmPlace firm = FirmOf(place);
                TellTech(NotificationType.Warning, "reallife_vc_options_nobody", Data("firm", firm != null ? firm.File.Name : string.Empty), TabFunding, TechApp.CapRow + place);
                return false;
            }
            long shares = VentureRules.Grant(c, VentureCatalogue.OptionGrant);
            GiveOptions(c, place, shares + c.Unassigned, people);
            ChangedTech();
            return true;
        }

        internal void SetChief(string place, string id)
        {
            Cap c = CapFor(place, true);
            if (c != null)
            {
                c.Ceo = id ?? string.Empty;
                ChangedTech();
            }
        }

        /// <summary>A tenth of the player's shares sold to the lead investor at a fifth off the last round's price.</summary>
        internal bool SellShares(string place)
        {
            FirmPlace firm = FirmOf(place);
            Cap c = CapFor(place, false);
            if (firm == null || c == null || c.VcSeats == 0 || c.Post <= 0)
            {
                return false;
            }
            int lead = 0;
            for (int i = c.Holders.Count - 1; i >= 0; i--)
            {
                if (c.Holders[i].Kind == (int)HolderKind.Vc)
                {
                    lead = c.Holders[i].Who;
                    break;
                }
            }
            long paid = VentureRules.SellShares(c, 100, lead);
            VentureGame.Give(Level(paid), VentureGame.ShareSaleKey, firm.File.Name);
            ChangedTech();
            return paid > 0;
        }

        /// <summary>A listing asked for: the share issue feature takes it, or the player is told it is not open yet.</summary>
        internal void PrepareListing(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null)
            {
                return;
            }
            bool taken = TechBridges.Ipo != null && TechBridges.Ipo();
            Notifications.Show(taken ? NotificationType.Success : NotificationType.Info, taken ? "reallife_vc_note_listing_taken" : "reallife_vc_note_listing_closed",
                Data("firm", firm.File.Name), 8f, null, null, true, false);
        }
    }
}
