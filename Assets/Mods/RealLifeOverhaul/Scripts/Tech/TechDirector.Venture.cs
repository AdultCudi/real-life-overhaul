using System;
using System.Collections.Generic;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The venture part: the city's startups and the venture firms (Tech/Venture/VentureRules), the player as an angel or
    /// through a fund of their own, and the player's own firms raising from the venture firms. It runs with the tech
    /// sector. Each game day, after the firms' day: a startup may be founded, every startup lives its day (and may come to
    /// the player for money), sales and failures pay out, and each firm of the player's that has taken money is looked
    /// at: its raise and term sheets, its bridge's interest, and its board's target. Everything else is the player's doing.
    /// </summary>
    public sealed partial class TechDirector
    {
        public const int TabStartups = 3, TabFunding = 4;

        private readonly List<VentureNote> _ventureNotes = new List<VentureNote>(8);
        private VentureFile _venture;
        private bool _ventureOn;
        private VentureMeeting _pitch;

        public bool VentureRunning => _ventureOn;

        /// <summary>The venture part's file; null while it is off.</summary>
        public VentureFile Ventures => _ventureOn ? _venture : null;

        /// <summary>The AI wave today, thousandths.</summary>
        public int HypeNow => _venture != null && Save != null ? VentureRules.Hype(_venture.Seed, Save.Day, Year) : 1000;

        public bool FundOpen => _ventureOn && _venture.Fund.Open;

        public long FundCash => _ventureOn ? Level(_venture.Fund.Cash) : 0;

        /// <summary>An amount written at the start's prices, at today's.</summary>
        internal long Level(long cents) => cents * Prices.Level / 1000;

        // ---------------------------------------------------------------- starting and stopping

        private void StartVenture()
        {
            GameInstance save = Save;
            int version;
            VentureFile file = VentureStore.Load(save.modData, out version);
            bool fresh = file == null;
            if (fresh)
            {
                file = new VentureFile { Seed = new System.Random().Next(1, int.MaxValue), FirstDay = save.Day };
            }
            _venture = file;
            _ventureOn = true;
            if (_pitch == null)
            {
                _pitch = new VentureMeeting(this);
            }
            if (fresh)
            {
                // the city already has a few startups
                int hype = VentureRules.Hype(file.Seed, save.Day, Year);
                for (int i = 0; i < 6; i++)
                {
                    Startup s = VentureRules.Born(file, save.Day - i * 20, hype);
                    s.Founder = VentureGame.Founder();
                    file.Startups.Add(s);
                }
            }
            Logger?.Info("Tech: startups " + (fresh ? "opened for this save" : "loaded (version " + version + ")") + " on day " + save.Day + ": " + _venture.Startups.Count + " startup(s), "
                         + _venture.Caps.Count + " firm(s) of the player's with investors.");
        }

        private void StopVenture(bool keep)
        {
            if (!_ventureOn)
            {
                return;
            }
            if (keep && Save != null && _venture != null)
            {
                WriteVenture(Save.modData);
            }
            _pitch?.End();
            _ventureOn = false;
        }

        private void WriteVenture(Dictionary<string, string> data)
        {
            if (_venture != null && data != null)
            {
                data[VentureStore.Key] = VentureStore.Write(_venture);
            }
        }

        // ---------------------------------------------------------------- the day

        private void VentureDay(int day)
        {
            if (_venture.LastDay >= day)
            {
                return;
            }
            float started = Time.realtimeSinceStartup;
            int cycle = HiringGame.Cycle, hype = VentureRules.Hype(_venture.Seed, day, Year), mood = VentureRules.Mood(cycle);
            _ventureNotes.Clear();
            int alive = 0;
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                alive += _venture.Startups[i].Alive ? 1 : 0;
            }
            if (alive < VentureCatalogue.StartupsMost && TechRandom.Mille(_venture.Seed, day, 3) < VentureCatalogue.BirthChance * mood / 1000)
            {
                Startup s = VentureRules.Born(_venture, day, hype);
                s.Founder = VentureGame.Founder();
                _venture.Startups.Add(s);
                if (s.Sector == (int)Venture.Ai)
                {
                    Story("reallife_tech_news_ai_founded", "name", s.Name, "founder", s.Founder);
                }
            }
            bool canPay = VentureGame.Cash >= Level(VentureCatalogue.TicketLeast) || FundCash >= Level(VentureCatalogue.TicketLeast);
            for (int i = 0; i < _venture.Startups.Count; i++)
            {
                VentureRules.Day(_venture, _venture.Startups[i], day, cycle, hype, canPay, _ventureNotes);
            }
            for (int i = 0; i < _ventureNotes.Count; i++)
            {
                Heard(_ventureNotes[i], day);
            }
            for (int i = _venture.Startups.Count - 1; i >= 0; i--)
            {
                Startup s = _venture.Startups[i];
                if (!s.Alive && day - s.LastRound > 60)
                {
                    _venture.Startups.RemoveAt(i);
                }
                else if (s.Ask != null && day > s.Ask.Lapse)
                {
                    s.Ask = null;
                }
            }
            CapsDay(day, mood);
            _venture.LastDay = day;
            if (Verbose)
            {
                Logger?.Info("Tech check (startups, day " + day + "): " + _venture.Startups.Count + " startup(s), mood " + mood + ", the AI wave " + hype + ", " + _ventureNotes.Count + " event(s), "
                             + _venture.Caps.Count + " firm(s) of the player's with investors; " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0") + " ms: PASS");
            }
        }

        // What a startup's day did, for the player and the paper; a sale's money paid out to the player's lines.
        private void Heard(VentureNote n, int day)
        {
            Startup s = _venture.StartupOf(n.Startup);
            if (s == null)
            {
                return;
            }
            string row = TechApp.StartupRow + s.Id;
            bool mine = Holds(s);
            switch (n.Kind)
            {
                case VentureRules.NoteRaised:
                    if (mine || n.Amount >= 500000000)
                    {
                        Story("reallife_tech_news_raised", "name", s.Name, "amount", TechText.Cents(Level(n.Amount)));
                    }
                    if (mine)
                    {
                        Tell2(NotificationType.Info, "reallife_vc_note_raised", Data("name", s.Name, "amount", TechText.Cents(Level(n.Amount)), "stage", TechText.RoundName((RoundStage)s.Stage)), TabStartups, row);
                    }
                    break;
                case VentureRules.NoteDown:
                    if (mine)
                    {
                        Tell2(NotificationType.Warning, "reallife_vc_note_down", Data("name", s.Name), TabStartups, row);
                    }
                    break;
                case VentureRules.NotePitch:
                    Tell2(NotificationType.Info, "reallife_vc_note_pitch", Data("name", s.Name, "founder", s.Founder, "amount", TechText.Cents(Level(n.Amount))), TabStartups, row);
                    break;
                case VentureRules.NoteFollowOn:
                    Tell2(NotificationType.Info, "reallife_vc_note_follow_on", Data("name", s.Name, "amount", TechText.Cents(Level(VentureRules.FollowOnAmount(s)))), TabStartups, row);
                    break;
                case VentureRules.NoteFailed:
                case VentureRules.NoteAcquired:
                case VentureRules.NoteAcquihire:
                    PayOut(s);
                    Story(n.Kind == VentureRules.NoteFailed ? "reallife_tech_news_startup_failed" : "reallife_tech_news_startup_sold", "name", s.Name, "amount", TechText.Cents(Level(SalePrice(s))));
                    if (mine)
                    {
                        Tell2(n.Kind == VentureRules.NoteFailed ? NotificationType.Warning : NotificationType.Success,
                            n.Kind == VentureRules.NoteFailed ? "reallife_vc_note_failed" : "reallife_vc_note_sold", Data("name", s.Name, "amount", TechText.Cents(Level(n.Amount))), TabStartups, row);
                    }
                    break;
                case VentureRules.NoteListing:
                    PayOut(s);
                    Story("reallife_tech_news_listing", "name", s.Name, "amount", TechText.Cents(Level(s.Post)));
                    if (mine)
                    {
                        Tell2(NotificationType.Success, "reallife_vc_note_listed", Data("name", s.Name, "amount", TechText.Cents(Level(n.Amount))), TabStartups, row);
                    }
                    break;
            }
        }

        private static bool Holds(Startup s)
        {
            for (int i = 0; i < s.Holders.Count; i++)
            {
                if (s.Holders[i].Players)
                {
                    return true;
                }
            }
            return false;
        }

        private static long SalePrice(Startup s)
        {
            long t = 0;
            for (int i = 0; i < s.Holders.Count; i++)
            {
                t += s.Holders[i].Paid;
            }
            return t;
        }

        // A sale's money to the player: an angel's line to their cash, the fund's to the fund.
        private void PayOut(Startup s)
        {
            for (int i = 0; i < s.Holders.Count; i++)
            {
                Holder h = s.Holders[i];
                if (h.Paid <= 0)
                {
                    continue;
                }
                if (h.Kind == (int)HolderKind.Angel)
                {
                    VentureGame.Give(Level(h.Paid), VentureGame.ReturnKey, s.Name);
                }
                else if (h.Kind == (int)HolderKind.Fund)
                {
                    _venture.Fund.Cash += h.Paid;
                    _venture.Fund.Returned += h.Paid;
                }
                h.Paid = 0;
            }
        }

        // ---------------------------------------------------------------- the player's firms with investors

        /// <summary>A firm's recurring revenue a month, at today's prices, cents.</summary>
        internal long FirmMrr(string place)
        {
            return _firmsOn ? Level(FirmsRules.Arr(_firmsFile, place) / 12) : 0;
        }

        private void CapsDay(int day, int mood)
        {
            for (int i = 0; i < _venture.Caps.Count; i++)
            {
                Cap c = _venture.Caps[i];
                FirmPlace firm = FirmOf(c.Place);
                if (firm == null)
                {
                    continue;
                }
                OptionsDay(c);
                for (int t = c.Sheets.Count - 1; t >= 0; t--)
                {
                    if (day > c.Sheets[t].Lapse)
                    {
                        c.Sheets.RemoveAt(t);
                    }
                }
                if (c.RaiseUntil > 0 && day > c.RaiseUntil && c.Sheets.Count == 0)
                {
                    c.RaiseUntil = 0;
                    Tell2(NotificationType.Info, "reallife_vc_note_raise_ended", Data("firm", firm.File.Name), TabFunding, TechApp.CapRow + c.Place);
                }
                else if (c.RaiseUntil > 0 && day <= c.RaiseUntil && c.Meetings < VentureCatalogue.VcMeetingsMost && TechRandom.Mille(_venture.Seed, day, 81, i) < 450)
                {
                    int vc = NextVc(c, day);
                    if (vc >= 0)
                    {
                        Tell2(NotificationType.Info, "reallife_vc_note_meet", Data("firm", firm.File.Name, "vc", VentureCatalogue.Firms[vc].Name), TabFunding, TechApp.CapRow + c.Place);
                    }
                }
                if (c.Bridge > 0)
                {
                    VentureGame.Pay(Level(VentureRules.BridgeInterest(c.Bridge)), VentureGame.InterestKey, firm.File.Name, firm.Reg, true);
                }
                int review = VentureRules.Review(c, FirmMrr(c.Place) * 1000 / Math.Max(1, Prices.Level), day);
                if (review == 1)
                {
                    Tell2(NotificationType.Success, "reallife_vc_note_target_met", Data("firm", firm.File.Name), TabFunding, TechApp.CapRow + c.Place);
                }
                else if (review == 2)
                {
                    Tell2(NotificationType.Warning, "reallife_vc_note_target_missed", Data("firm", firm.File.Name, "target", TechText.Cents(Level(c.TargetMrr))), TabFunding, TechApp.CapRow + c.Place);
                }
                else if (review == 3)
                {
                    ReplaceChief(c, firm, day);
                }
            }
        }

        /// <summary>A venture firm that would meet the player's firm now: one that leads at its next stage and has not closed its door on the player; -1 for none.</summary>
        internal int NextVc(Cap c, int day)
        {
            int stage = Math.Max(0, c.Stage + 1);
            for (int k = 0; k < VentureCatalogue.FirmCount; k++)
            {
                int vc = (c.Meetings + k + TechRandom.Range(_venture.Seed, day, 82, 0, 0, VentureCatalogue.FirmCount - 1)) % VentureCatalogue.FirmCount;
                VcDef def = VentureCatalogue.Firms[vc];
                if (stage >= def.Least && stage <= def.Most && day >= _venture.ClosedUntil[vc] && !SheetFrom(c, vc))
                {
                    return vc;
                }
            }
            return -1;
        }

        private static bool SheetFrom(Cap c, int vc)
        {
            for (int i = 0; i < c.Sheets.Count; i++)
            {
                if (c.Sheets[i].Vc == vc)
                {
                    return true;
                }
            }
            return false;
        }

        // The board, with a majority, replaces the chief executive: a professional who is goes (laid off), and a chief
        // executive of the board's choosing joins the payroll at the going rate.
        private void ReplaceChief(Cap c, FirmPlace firm, int day)
        {
            string was = c.Ceo;
            if (!string.IsNullOrEmpty(was))
            {
                LetGo(was, false);
            }
            Profile chief = TalentRules.Make(_venture.Seed, day * 31 + 7, TalentCatalogue.ChiefExecutive, TalentCatalogue.Principal, day);
            chief.Id = "pro:" + _talent.NextPro++;
            chief.Name = VentureGame.Founder();
            chief.Place = c.Place;
            chief.Work = (int)Arrangement.Hybrid;
            chief.Wage = TalentRules.Going(TalentGame.GameRate(TalentCatalogue.RateSkill, chief.Main / 1000), chief.Def, 1000, 0);
            chief.ReviewDay = TalentRules.FirstReview(day, Year);
            if (_talent.People.Count < TalentStore.PeopleMost)
            {
                _talent.People.Add(chief);
                c.Ceo = chief.Id;
            }
            Tell2(NotificationType.Error, string.IsNullOrEmpty(was) ? "reallife_vc_note_chief_appointed" : "reallife_vc_note_chief_replaced", Data("firm", firm.File.Name, "name", chief.Name), TabFunding, TechApp.CapRow + c.Place);
            Story("reallife_tech_news_chief", "firm", firm.File.Name, "name", chief.Name);
        }

        // ---------------------------------------------------------------- what the player does: startups

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
            ChangedVenture();
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
                    VentureGame.Give(Level(amount), VentureGame.ReturnKey, s.Name);   // (the round could not close: the money comes back)
                }
                return false;
            }
            if (fund)
            {
                _venture.Fund.Cash -= amount;
                _venture.Fund.Invested += amount;
            }
            Tell2(NotificationType.Success, "reallife_vc_note_backed", Data("name", s.Name, "amount", TechText.Cents(Level(amount))), TabStartups, TechApp.StartupRow + s.Id);
            ChangedVenture();
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
            Tell2(yes ? NotificationType.Success : NotificationType.Info, yes ? "reallife_vc_note_haggle_yes" : "reallife_vc_note_haggle_no", Data("founder", s.Founder, "pre", TechText.Cents(Level(s.Ask.Pre))),
                TabStartups, TechApp.StartupRow + s.Id);
            ChangedVenture();
        }

        internal void Pass(int id)
        {
            Startup s = _ventureOn ? _venture.StartupOf(id) : null;
            if (s != null)
            {
                s.Ask = null;
                ChangedVenture();
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
            ChangedVenture();
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
            ChangedVenture();
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
            ChangedVenture();
        }

        // ---------------------------------------------------------------- what the player does: their own firms

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
            Tell2(NotificationType.Info, "reallife_vc_note_raise_started", Data("firm", firm.File.Name, "day", c.RaiseUntil.ToString()), TabFunding, TechApp.CapRow + place);
            ChangedVenture();
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
            c.Meetings++;
            long mrr = FirmMrr(place);
            long growth = c.LastMrr > 0 ? Math.Max(0, (FirmMrr(place) * 1000 / Math.Max(1, Prices.Level) - c.LastMrr) * 1000 / c.LastMrr) : 100;
            return _pitch.Player(place, vc, mrr, growth, firm.People);
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
            Tell2(NotificationType.Success, "reallife_vc_note_sheet", Data("vc", VentureCatalogue.Firms[vc].Name, "amount", TechText.Cents(Level(t.Amount)), "pre", TechText.Cents(Level(t.Pre))), TabFunding, TechApp.CapRow + place);
            return TechText.Fill("reallife_vc_meet_sheet", "firm", VentureCatalogue.Firms[vc].Name, "amount", TechText.Cents(Level(t.Amount)), "pre", TechText.Cents(Level(t.Pre)), "share", TechText.Mille(t.EquityMille),
                "day", t.Lapse.ToString());
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
            Tell2(NotificationType.Success, "reallife_vc_note_closed", Data("firm", firm.File.Name, "amount", TechText.Cents(Level(money)), "vc", VentureCatalogue.Firms[TalentCatalogue.Clamp(t.Vc, 0, VentureCatalogue.FirmCount - 1)].Name),
                TabFunding, TechApp.CapRow + place);
            ChangedVenture();
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
            ChangedVenture();
            return true;
        }

        /// <summary>The firm's average day of profit over the last week, cents at today's prices (negative: its burn).</summary>
        internal long FirmProfit(FirmPlace firm)
        {
            try
            {
                int onRecord;
                return (long)(Economy.Books.AverageProfit(firm.Reg.Address, 7, out onRecord) * 100f);
            }
            catch (Exception)
            {
                return 0;
            }
        }

        /// <summary>
        /// Share options granted to the firm's people: a share of the firm, split among them, each a little happier and less
        /// likely to go. Refused while nobody works there (until 0.12.3 the shares were made and went to nobody), with a
        /// warning that says why; options still waiting for somebody (<see cref="Cap.Unassigned"/>) go out with the grant.
        /// </summary>
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
                Tell2(NotificationType.Warning, "reallife_vc_options_nobody", Data("firm", firm != null ? firm.File.Name : string.Empty), TabFunding, TechApp.CapRow + place);
                return false;
            }
            long shares = VentureRules.Grant(c, VentureCatalogue.OptionGrant);
            GiveOptions(c, place, shares + c.Unassigned, people);
            ChangedVenture();
            return true;
        }

        // The firm's people now: the professionals whose place it is.
        private int PeopleAt(string place)
        {
            int people = 0;
            for (int i = 0; i < _people.Count; i++)
            {
                people += _people[i].File.Place == place ? 1 : 0;
            }
            return people;
        }

        // What the firm's people hold now, shares.
        private long HeldAt(string place)
        {
            long held = 0L;
            for (int i = 0; i < _people.Count; i++)
            {
                held += _people[i].File.Place == place ? Math.Max(0, _people[i].File.Options) : 0;
            }
            return held;
        }

        // Options split evenly among the firm's people, each a little happier; what does not divide waits for the next split.
        private void GiveOptions(Cap c, string place, long shares, int people)
        {
            long each = VentureRules.Split(shares, people, out long left);
            c.Unassigned = left;
            if (each <= 0L)
            {
                return;
            }
            for (int i = 0; i < _people.Count; i++)
            {
                Profile p = _people[i].File;
                if (p.Place == place)
                {
                    p.Options = (int)Math.Min(int.MaxValue, p.Options + each);
                    p.Morale = Math.Min(1000, p.Morale + VentureCatalogue.OptionMorale);
                }
            }
        }

        // Once a day: a save from before 0.12.4 has its options looked over once (grants made while nobody worked at the firm
        // went to nobody: what nobody holds is waiting for somebody), and options waiting go to the firm's people when there
        // are enough for each of them to have one.
        private void OptionsDay(Cap c)
        {
            if (c.OptionsChecked == 0)
            {
                c.OptionsChecked = 1;
                if (c.OptionsGranted > 0)
                {
                    c.Unassigned = Math.Max(c.Unassigned, VentureRules.Unheld(c, HeldAt(c.Place)));
                }
            }
            int people = PeopleAt(c.Place);
            if (people > 0 && c.Unassigned >= people)
            {
                FirmPlace firm = FirmOf(c.Place);
                long given = c.Unassigned;
                GiveOptions(c, c.Place, given, people);
                Logger?.Info("Tech: share options waiting at " + (firm != null ? firm.File.Name : c.Place) + " went to its " + people + " professional(s): "
                             + (given - c.Unassigned) + " share(s), " + c.Unassigned + " left over.");
                ChangedVenture();
            }
        }

        internal void SetChief(string place, string id)
        {
            Cap c = CapFor(place, true);
            if (c != null)
            {
                c.Ceo = id ?? string.Empty;
                ChangedVenture();
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
            ChangedVenture();
            return paid > 0;
        }

        /// <summary>A listing asked for: the share issue's own feature takes it, or says it is not open yet.</summary>
        internal void PrepareListing(string place)
        {
            FirmPlace firm = FirmOf(place);
            if (firm == null)
            {
                return;
            }
            bool taken = TechBridges.Ipo != null && TechBridges.Ipo();
            Notifications.Show(taken ? NotificationType.Success : NotificationType.Info, taken ? "reallife_vc_note_listing_taken" : "reallife_vc_note_listing_closed", Data("firm", firm.File.Name), 8f, null, null, true, false);
        }

        internal void ChangedVenture()
        {
            ChangedTech();
        }
    }
}
