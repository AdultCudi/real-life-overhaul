using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// The venture part: the city's startups and venture firms, the player as an investor, and the player's firms raising. Each
    /// game day, after the firms'.
    /// </summary>
    public sealed partial class TechDirector
    {
        public const int TabStartups = 3, TabFunding = 4;

        private readonly List<VentureNote> _ventureNotes = new List<VentureNote>(8);
        private VentureFile _venture;
        private bool _ventureOn;
        private VentureMeeting _pitch;

        /// <summary>The venture part's file; null while it is off.</summary>
        public VentureFile Ventures => _ventureOn ? _venture : null;

        /// <summary>The AI wave today, thousandths.</summary>
        public int HypeNow => _venture != null && Save != null ? VentureRules.Hype(_venture.Seed, Save.Day, Year) : 1000;

        public bool FundOpen => _ventureOn && _venture.Fund.Open;

        public long FundCash => _ventureOn ? Level(_venture.Fund.Cash) : 0;

        /// <summary>An amount written at the start's prices, at today's.</summary>
        internal long Level(long cents) => cents * Prices.Level / 1000;

        private void StartVenture()
        {
            GameInstance save = Save;
            VentureFile file = VentureStore.Load(save.modData, out int version);
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
                Logger?.Info("Tech check (startups, day " + day + "): " + _venture.Startups.Count + " startup(s), mood " + mood + ", the AI wave " + hype + ", "
                             + _ventureNotes.Count + " event(s), " + _venture.Caps.Count + " firm(s) of the player's with investors; "
                             + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture) + " ms: PASS");
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
                        TellTech(NotificationType.Info, "reallife_vc_note_raised",
                            Data("name", s.Name, "amount", TechText.Cents(Level(n.Amount)), "stage", TechText.RoundName((RoundStage)s.Stage)), TabStartups, row);
                    }
                    break;
                case VentureRules.NoteDown:
                    if (mine)
                    {
                        TellTech(NotificationType.Warning, "reallife_vc_note_down", Data("name", s.Name), TabStartups, row);
                    }
                    break;
                case VentureRules.NotePitch:
                    TellTech(NotificationType.Info, "reallife_vc_note_pitch", Data("name", s.Name, "founder", s.Founder, "amount", TechText.Cents(Level(n.Amount))), TabStartups, row);
                    break;
                case VentureRules.NoteFollowOn:
                    TellTech(NotificationType.Info, "reallife_vc_note_follow_on", Data("name", s.Name, "amount", TechText.Cents(Level(VentureRules.FollowOnAmount(s)))), TabStartups, row);
                    break;
                case VentureRules.NoteFailed:
                case VentureRules.NoteAcquired:
                case VentureRules.NoteAcquihire:
                    PayOut(s);
                    bool failed = n.Kind == VentureRules.NoteFailed;
                    Story(failed ? "reallife_tech_news_startup_failed" : "reallife_tech_news_startup_sold", "name", s.Name, "amount", TechText.Cents(Level(SalePrice(s))));
                    if (mine)
                    {
                        TellTech(failed ? NotificationType.Warning : NotificationType.Success, failed ? "reallife_vc_note_failed" : "reallife_vc_note_sold",
                            Data("name", s.Name, "amount", TechText.Cents(Level(n.Amount))), TabStartups, row);
                    }
                    break;
                case VentureRules.NoteListing:
                    PayOut(s);
                    Story("reallife_tech_news_listing", "name", s.Name, "amount", TechText.Cents(Level(s.Post)));
                    if (mine)
                    {
                        TellTech(NotificationType.Success, "reallife_vc_note_listed", Data("name", s.Name, "amount", TechText.Cents(Level(n.Amount))), TabStartups, row);
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
                    TellTech(NotificationType.Info, "reallife_vc_note_raise_ended", Data("firm", firm.File.Name), TabFunding, TechApp.CapRow + c.Place);
                }
                else if (c.RaiseUntil > 0 && day <= c.RaiseUntil && c.Meetings < VentureCatalogue.VcMeetingsMost && TechRandom.Mille(_venture.Seed, day, 81, i) < 450)
                {
                    int vc = NextVc(c, day);
                    if (vc >= 0)
                    {
                        TellTech(NotificationType.Info, "reallife_vc_note_meet", Data("firm", firm.File.Name, "vc", VentureCatalogue.Firms[vc].Name), TabFunding, TechApp.CapRow + c.Place);
                    }
                }
                if (c.Bridge > 0)
                {
                    VentureGame.Pay(Level(VentureRules.BridgeInterest(c.Bridge)), VentureGame.InterestKey, firm.File.Name, firm.Reg, true);
                }
                int review = VentureRules.Review(c, FirmMrr(c.Place) * 1000 / Math.Max(1, Prices.Level), day);
                if (review == 1)
                {
                    TellTech(NotificationType.Success, "reallife_vc_note_target_met", Data("firm", firm.File.Name), TabFunding, TechApp.CapRow + c.Place);
                }
                else if (review == 2)
                {
                    TellTech(NotificationType.Warning, "reallife_vc_note_target_missed", Data("firm", firm.File.Name, "target", TechText.Cents(Level(c.TargetMrr))),
                        TabFunding, TechApp.CapRow + c.Place);
                }
                else if (review == 3)
                {
                    ReplaceChief(c, firm, day);
                }
            }
        }

        /// <summary>A venture firm that leads at the firm's next stage and would meet it now; -1 for none.</summary>
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

        // The board, with a majority, lays off the chief executive and puts one of its choosing on the payroll.
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
            TellTech(NotificationType.Error, string.IsNullOrEmpty(was) ? "reallife_vc_note_chief_appointed" : "reallife_vc_note_chief_replaced",
                Data("firm", firm.File.Name, "name", chief.Name), TabFunding, TechApp.CapRow + c.Place);
            Story("reallife_tech_news_chief", "firm", firm.File.Name, "name", chief.Name);
        }

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

        // Split evenly, each a little happier; what does not divide waits for the next split.
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

        // Once a day: a cap not yet looked over counts what nobody holds as waiting, and options waiting go out once there
        // are enough for one each.
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
                ChangedTech();
            }
        }
    }
}
