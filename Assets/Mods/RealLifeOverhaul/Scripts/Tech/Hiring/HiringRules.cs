using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a day of the labour market did, for the director to tell the player about.</summary>
    public struct MarketNote
    {
        public int Kind;            // 1 an applicant withdrew (took another job), 2 an applicant answered an offer, 3 a search delivered, 4 an application lapsed
        public int Applicant, Ad;
    }

    /// <summary>
    /// The rules of the labour market and of hiring from it. Game-free; every roll is <see cref="TechRandom"/>'s and the game's wage is
    /// handed in (<see cref="RateOf"/>).
    /// </summary>
    public static class HiringRules
    {
        /// <summary>The game's own wage for a role's skill at a main skill, cents an hour.</summary>
        public delegate int RateOf(RoleDef role, int mainPoints);

        public const int WithdrewNote = 1, AnsweredNote = 2, DeliveredNote = 3, LapsedNote = 4;

        public static int Unemployment(int cycle)
        {
            return HiringCatalogue.Unemployment[TalentCatalogue.Clamp(cycle, 0, HiringCatalogue.Unemployment.Length - 1)];
        }

        /// <summary>How many professionals look for work at once: more in a slump, and graduates in the summer.</summary>
        public static int Target(int cycle, bool summer)
        {
            int t = 60 + Unemployment(cycle) * 9 / 10 + (summer ? HiringCatalogue.GraduatesSummer : 0);
            return TalentCatalogue.Clamp(t, HiringCatalogue.SeekersLeast, HiringCatalogue.SeekersMost);
        }

        private static int Pick(int[] shares, int roll)
        {
            int total = 0;
            for (int i = 0; i < shares.Length; i++)
            {
                total += shares[i];
            }
            int at = (int)((long)roll * total / 1000);
            for (int i = 0; i < shares.Length; i++)
            {
                if (at < shares[i])
                {
                    return i;
                }
                at -= shares[i];
            }
            return shares.Length - 1;
        }

        /// <summary>A role of a family, by a roll; -1 if the family has none.</summary>
        public static int RoleOf(Family family, int roll)
        {
            int count = 0;
            for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
            {
                count += TalentCatalogue.Roles[i].Family == family ? 1 : 0;
            }
            if (count == 0)
            {
                return -1;
            }
            int n = (int)((long)roll * count / 1000);
            for (int i = 0; i < TalentCatalogue.Roles.Length; i++)
            {
                if (TalentCatalogue.Roles[i].Family == family && n-- == 0)
                {
                    return i;
                }
            }
            return -1;
        }

        /// <summary>A new seeker by the market's shares; a <paramref name="role"/> or <paramref name="level"/> of -1 is drawn.</summary>
        public static Seeker NewSeeker(int seed, int id, int day, bool summer, int role, int level, RateOf rate, int shortage, string district)
        {
            int n = id * 7919;
            if (level < 0)
            {
                level = summer && TechRandom.Mille(seed, n, 3) < 500 ? TalentCatalogue.Junior : Pick(HiringCatalogue.LevelShare, TechRandom.Mille(seed, n, 4));
            }
            if (role < 0)
            {
                // a few tries for a role of the family held at the level (a junior is no CTO)
                Family family = (Family)Pick(HiringCatalogue.FamilyShare, TechRandom.Mille(seed, n, 1));
                for (int t = 0; t < 6 && (role < 0 || level < TalentCatalogue.Role(role).LeastLevel || level > TalentCatalogue.Role(role).MostLevel); t++)
                {
                    role = RoleOf(family, TechRandom.Mille(seed, n, 2, t));
                }
                role = role < 0 ? TalentCatalogue.SoftwareEngineer : role;
            }
            RoleDef r = TalentCatalogue.Role(role);
            level = TalentCatalogue.Clamp(level, r.LeastLevel, r.MostLevel);
            var s = new Seeker { Id = id, District = district ?? string.Empty };
            s.P = TalentRules.Make(seed, n, role, level, day);
            s.P.Id = "s" + id;
            s.Employed = TechRandom.Mille(seed, n, 5) < HiringCatalogue.EmployedShare && !(summer && level == TalentCatalogue.Junior);
            s.Want = (int)TalentRules.Wants(s.P, seed);
            s.Until = day + TechRandom.Range(seed, n, 6, 0, HiringCatalogue.SeekerDaysLeast, HiringCatalogue.SeekerDaysMost);
            s.Fit = TechRandom.Range(seed, n, 7, 0, 150, 950);
            int honest = TechRandom.Mille(seed, n, 8);
            s.Claim = honest < 450 ? 0 : TechRandom.Range(seed, n, 9, 0, 1, HiringCatalogue.ClaimMost);
            s.Refs = TalentCatalogue.Clamp(1 + (s.Fit / 250) + (s.Claim > 8 ? -1 : 0) + TechRandom.Range(seed, n, 10, 0, 0, 1), 1, 5);
            s.Notice = s.Employed ? 7 + level * 5 : 0;
            s.Ask = AskOf(seed, s, rate, shortage);
            return s;
        }

        /// <summary>What a seeker asks, cents an hour: the going rate, more in work, less out of it, and a little either side.</summary>
        public static int AskOf(int seed, Seeker s, RateOf rate, int shortage)
        {
            RoleDef r = s.P.Def;
            int going = TalentRules.Going(rate != null ? rate(r, s.P.Main / 1000) : TalentCatalogue.RateLeast, r, shortage, TalentRules.CertPayBp(s.P));
            int bp = s.Employed ? HiringCatalogue.EmployedPremiumBp : -HiringCatalogue.UnemployedDiscountBp;
            bp += TechRandom.Range(seed, s.Id * 7919, 11, 0, -HiringCatalogue.AskSpreadBp, HiringCatalogue.AskSpreadBp);
            return Math.Max(TalentCatalogue.RateLeast, (int)((long)going * (10000 + bp) / 10000));
        }

        /// <summary>How short of people a family is, thousandths: its scarcity, moved by open jobs against seekers still looking.</summary>
        public static int Shortage(HiringFile f, Family family, int day)
        {
            int seekers = 0, jobs = 0;
            for (int i = 0; i < f.Seekers.Count; i++)
            {
                seekers += !f.Seekers[i].Taken && !f.Seekers[i].Passive && f.Seekers[i].P.Def.Family == family ? 1 : 0;
            }
            for (int i = 0; i < f.RivalAds.Count; i++)
            {
                jobs += f.RivalAds[i].Family == (int)family && f.RivalAds[i].To > day ? 1 : 0;
            }
            for (int i = 0; i < f.Ads.Count; i++)
            {
                jobs += !f.Ads[i].Search && f.Ads[i].Open(day) && TalentCatalogue.Role(f.Ads[i].Role).Family == family ? 1 : 0;
            }
            int scarcity = HiringCatalogue.FamilyScarcity[TalentCatalogue.Clamp((int)family, 0, HiringCatalogue.FamilyScarcity.Length - 1)];
            int pressure = (jobs * 1000 / (seekers + 2) - 300) / 2;
            return TalentCatalogue.Clamp(scarcity + pressure, HiringCatalogue.ShortageLeast, HiringCatalogue.ShortageMost);
        }

        /// <summary>A day of the labour market: seekers leave, the rivals post and hire, the list is filled again.</summary>
        public static void Day(HiringFile f, int day, int cycle, bool summer, IList<string> rivals, RateOf rate, IList<string> districts, List<MarketNote> notes)
        {
            for (int i = f.Seekers.Count - 1; i >= 0; i--)
            {
                Seeker s = f.Seekers[i];
                if (s.Passive && !Applied(f, s.Id))
                {
                    f.Seekers.RemoveAt(i);   // a search firm's candidate not taken on goes back to their own job
                    continue;
                }
                if (day < s.Until && !s.Taken)
                {
                    continue;
                }
                if (Starting(f, s.Id))
                {
                    continue;   // hired by the player and not started yet
                }
                Withdraw(f, s.Id, notes);
                if (!Applied(f, s.Id))
                {
                    f.Seekers.RemoveAt(i);
                }
                else
                {
                    s.Taken = true;
                }
            }
            for (int i = f.RivalAds.Count - 1; i >= 0; i--)
            {
                if (f.RivalAds[i].To <= day)
                {
                    f.RivalAds.RemoveAt(i);
                }
            }
            int wanted = HiringCatalogue.RivalAdsByCycle[TalentCatalogue.Clamp(cycle, 0, HiringCatalogue.RivalAdsByCycle.Length - 1)];
            for (int k = 0; f.RivalAds.Count < wanted && f.RivalAds.Count < HiringCatalogue.RivalAdsMost && k < HiringCatalogue.RivalAdsMost; k++)
            {
                int n = day * 131 + k;
                f.RivalAds.Add(new RivalAd
                {
                    Rival = rivals != null && rivals.Count > 0 ? rivals[TechRandom.Range(f.Seed, n, 21, 0, 0, rivals.Count - 1)] : HiringCatalogue.RivalFirms[0],
                    Family = Pick(Staffing.StaffIndex.RivalFamilyShares, TechRandom.Mille(f.Seed, n, 22)),
                    Level = Pick(HiringCatalogue.LevelShare, TechRandom.Mille(f.Seed, n, 23)),
                    To = day + HiringCatalogue.RivalAdDays,
                    PayBp = TechRandom.Range(f.Seed, n, 24, 0, 0, 1500)
                });
            }
            for (int i = 0; i < f.RivalAds.Count; i++)
            {
                RivalAd ad = f.RivalAds[i];
                if (TechRandom.Mille(f.Seed, day, 25, i) >= HiringCatalogue.RivalTakeChance)
                {
                    continue;
                }
                for (int j = 0; j < f.Seekers.Count; j++)
                {
                    Seeker s = f.Seekers[j];
                    if (!s.Taken && !s.Passive && (int)s.P.Def.Family == ad.Family && s.P.Level == ad.Level && !Starting(f, s.Id) && !Offered(f, s.Id))
                    {
                        s.Taken = true;
                        s.Employer = ad.Rival;
                        Withdraw(f, s.Id, notes);
                        break;
                    }
                }
            }
            int target = Target(cycle, summer);
            for (int k = 0; Open(f) < target && f.Seekers.Count < HiringCatalogue.SeekersMost && k < HiringCatalogue.SeekersMost; k++)
            {
                int id = f.Next();
                string district = districts != null && districts.Count > 0 ? districts[TechRandom.Range(f.Seed, id, 26, 0, 0, districts.Count - 1)] : string.Empty;
                Seeker s = NewSeeker(f.Seed, id, day, summer, -1, -1, rate, 1000, district);
                s.Ask = AskOf(f.Seed, s, rate, Shortage(f, s.P.Def.Family, day));
                f.Seekers.Add(s);
            }
            // seekers the rivals took with nothing pending at the player's are dropped
            for (int i = f.Seekers.Count - 1; i >= 0; i--)
            {
                if (f.Seekers[i].Taken && !Applied(f, f.Seekers[i].Id) && !Starting(f, f.Seekers[i].Id))
                {
                    f.Seekers.RemoveAt(i);
                }
            }
        }

        /// <summary>Seekers still looking on the open market.</summary>
        public static int Open(HiringFile f)
        {
            int n = 0;
            for (int i = 0; i < f.Seekers.Count; i++)
            {
                n += !f.Seekers[i].Taken && !f.Seekers[i].Passive ? 1 : 0;
            }
            return n;
        }

        private static bool Applied(HiringFile f, int seeker)
        {
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                if (f.Applicants[i].Seeker == seeker && f.Applicants[i].Live)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Starting(HiringFile f, int seeker)
        {
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                if (f.Applicants[i].Seeker == seeker && f.Applicants[i].Stage == (int)ApplyStage.Accepted)
                {
                    return true;
                }
            }
            return false;
        }

        private static bool Offered(HiringFile f, int seeker)
        {
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                Applicant a = f.Applicants[i];
                if (a.Seeker == seeker && (a.Stage == (int)ApplyStage.Offered || a.Stage == (int)ApplyStage.Countered))
                {
                    return true;
                }
            }
            return false;
        }

        // A seeker took another job: every live application of theirs that has not been accepted is withdrawn.
        private static void Withdraw(HiringFile f, int seeker, List<MarketNote> notes)
        {
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                Applicant a = f.Applicants[i];
                if (a.Seeker == seeker && a.Live && a.Stage != (int)ApplyStage.Accepted)
                {
                    a.Stage = (int)ApplyStage.Withdrawn;
                    notes?.Add(new MarketNote { Kind = WithdrewNote, Applicant = a.Id, Ad = a.Ad });
                }
            }
        }

        /// <summary>An ad's cost at the start's prices: its days at a day's cost, more when boosted.</summary>
        public static int AdCost(int days, bool boost)
        {
            long cost = (long)days * HiringCatalogue.AdDayCost;
            return (int)(boost ? cost * HiringCatalogue.BoostCostBp / 10000 : cost);
        }

        /// <summary>The applicants a week of an ad may bring, counted from its first day.</summary>
        public static int WeekCap(JobAd ad, int day)
        {
            int weeks = Math.Max(0, day - ad.From) / 7 + 1;
            return weeks * (ad.Boost ? HiringCatalogue.AdWeekMostBoosted : HiringCatalogue.AdWeekMost);
        }

        /// <summary>True if a seeker could do the job: the same family, a level within one of it.</summary>
        public static bool Matches(Seeker s, JobAd ad)
        {
            RoleDef want = TalentCatalogue.Role(ad.Role);
            return s.P.Def.Family == want.Family && Math.Abs(s.P.Level - ad.Level) <= 1 && s.P.Level >= want.LeastLevel && s.P.Level <= want.MostLevel;
        }

        /// <summary>The per mille chance a matching seeker applies to an ad today.</summary>
        public static int ApplyChance(Seeker s, JobAd ad, int esteem, int rivalAds)
        {
            long c = HiringCatalogue.ApplyChance;
            if (ad.PayMost < s.Ask)
            {
                long ratio = (long)ad.PayMost * 1000 / Math.Max(1, s.Ask);
                c = c * ratio / 1000 * ratio / 1000 * ratio / 1000 * ratio / 1000;
            }
            if (s.Want == (int)Arrangement.Remote && ad.Work == (int)Arrangement.OnSite)
            {
                c = c * 450 / 1000;
            }
            else if (s.Want == ad.Work)
            {
                c = c * 1200 / 1000;
            }
            c = c * (1000 + TalentCatalogue.Clamp(esteem, -10, 10) * 30) / 1000;
            c = c * 1000 / (1000 + rivalAds * 330);
            if (ad.Boost)
            {
                c = c * HiringCatalogue.BoostReach / 1000;
            }
            if (s.P.Role == ad.Role)
            {
                c = c * 1300 / 1000;
            }
            if (s.CvMain < ad.MinSkill)
            {
                c = c * HiringCatalogue.UnderRequirement / 1000;
            }
            return (int)Math.Max(0, Math.Min(1000, c));
        }

        /// <summary>A day's applications to the player's open ads, up to each ad's weekly cap. Returns how many came.</summary>
        public static int Apply(HiringFile f, int day, int esteem)
        {
            int came = 0;
            for (int a = 0; a < f.Ads.Count; a++)
            {
                JobAd ad = f.Ads[a];
                if (ad.Search || !ad.Open(day))
                {
                    continue;
                }
                int rivals = 0;
                Family family = TalentCatalogue.Role(ad.Role).Family;
                for (int r = 0; r < f.RivalAds.Count; r++)
                {
                    rivals += f.RivalAds[r].Family == (int)family && f.RivalAds[r].To > day ? 1 : 0;
                }
                for (int i = 0; i < f.Seekers.Count && ad.Came < WeekCap(ad, day) && f.Applicants.Count < HiringCatalogue.ApplicantsMost; i++)
                {
                    Seeker s = f.Seekers[i];
                    if (s.Taken || s.Passive || !Matches(s, ad) || HasApplied(f, s.Id, ad.Id))
                    {
                        continue;
                    }
                    if (TechRandom.Mille(f.Seed, s.Id, day, ad.Id) < ApplyChance(s, ad, esteem, rivals))
                    {
                        f.Applicants.Add(new Applicant { Id = f.Next(), Seeker = s.Id, Ad = ad.Id, Day = day, Interest = Interest(s, ad) });
                        ad.Came++;
                        came++;
                    }
                }
            }
            return came;
        }

        private static bool HasApplied(HiringFile f, int seeker, int ad)
        {
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                if (f.Applicants[i].Seeker == seeker && f.Applicants[i].Ad == ad)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>How keen an applicant is at first, thousandths: half how the pay and arrangement suit them, half how they would settle.</summary>
        public static int Interest(Seeker s, JobAd ad)
        {
            int pay = ad.PayMost >= s.Ask ? 1000 : (int)((long)ad.PayMost * 1000 / Math.Max(1, s.Ask));
            int work = s.Want == ad.Work ? 1000 : s.Want == (int)Arrangement.Remote && ad.Work == (int)Arrangement.OnSite ? 400 : 750;
            return TalentCatalogue.Clamp((pay + work) / 4 + s.Fit / 2, 0, 1000);
        }

        public const int RevealSkill = 1, RevealYears = 2, RevealFit = 4, RevealAsk = 8;
        public const int TopicTechnical = 0, TopicProject = 1, TopicMotive = 2, TopicPay = 3, TopicTeam = 4, Topics = 5;

        /// <summary>A question at an interview: its topic reveals one thing. Returns the points of interest it moved.</summary>
        public static int Ask(Applicant a, Seeker s, int topic)
        {
            int before = a.Interest;
            switch (topic)
            {
                case TopicTechnical:
                    a.Revealed |= RevealSkill;
                    a.Interest += s.P.Level >= TalentCatalogue.Senior ? 60 : 20;
                    break;
                case TopicProject:
                    a.Revealed |= RevealYears;
                    a.Interest += 30;
                    break;
                case TopicMotive:
                    a.Revealed |= RevealFit;
                    a.Interest += s.Fit > 600 ? 80 : -20;
                    break;
                case TopicPay:
                    a.Revealed |= RevealAsk;
                    a.Interest -= 10;
                    break;
                default:
                    a.Interest += s.Fit / 10;
                    if (s.Fit > 700)
                    {
                        a.Revealed |= RevealFit;
                    }
                    break;
            }
            a.Interest = TalentCatalogue.Clamp(a.Interest, 0, 1000);
            a.Interviewed = true;
            if (a.Stage < (int)ApplyStage.Interviewed)
            {
                a.Stage = (int)ApplyStage.Interviewed;
            }
            return a.Interest - before;
        }

        /// <summary>The share of their ask, basis points, an applicant takes: less for a good employer and a keen applicant.</summary>
        public static int Threshold(Applicant a, int esteem)
        {
            int bp = HiringCatalogue.AcceptBp - TalentCatalogue.Clamp(esteem, -10, 10) * HiringCatalogue.EsteemBp - (a.Interest - 500) / 2;
            return TalentCatalogue.Clamp(bp, 8800, 10500);
        }

        /// <summary>An offer made: they answer the next day.</summary>
        public static void Offer(Applicant a, int cents, int day)
        {
            a.Offer = cents;
            a.OfferDay = day;
            a.Stage = (int)ApplyStage.Offered;
        }

        /// <summary>The answer to an offer: accepted, countered (a rival's bid or their ask) or declined.</summary>
        public static ApplyStage Answer(HiringFile f, Applicant a, Seeker s, int day, int esteem, int shortage)
        {
            int threshold = (int)((long)s.Ask * Threshold(a, esteem) / 10000);
            bool rival = a.RivalBid == 0 && !s.Passive && shortage >= 1150 && TechRandom.Mille(f.Seed, a.Id, day, 31) < HiringCatalogue.RivalOfferChance;
            if (a.Offer >= threshold && !(rival && a.Offer < s.Ask * (10000L + HiringCatalogue.RivalOfferBp) / 10000))
            {
                a.Stage = (int)ApplyStage.Accepted;
                a.Start = day + (s.Employed
                    ? Math.Max(HiringCatalogue.StartLeast, s.Notice)
                    : TechRandom.Range(f.Seed, a.Id, 32, 0, HiringCatalogue.StartLeast, HiringCatalogue.StartUnemployedMost));
                return ApplyStage.Accepted;
            }
            if (rival)
            {
                a.RivalBid = (int)(s.Ask * (10000L + HiringCatalogue.RivalOfferBp) / 10000);
                a.Counter = a.RivalBid;
                a.OfferDay = day;
                a.Stage = (int)ApplyStage.Countered;
                return ApplyStage.Countered;
            }
            if (a.Offer >= (long)s.Ask * HiringCatalogue.CounterBp / 10000 && a.Counter == 0)
            {
                a.Counter = s.Ask;
                a.OfferDay = day;
                a.Stage = (int)ApplyStage.Countered;
                return ApplyStage.Countered;
            }
            a.Stage = (int)ApplyStage.Declined;
            return ApplyStage.Declined;
        }

        /// <summary>Offers waiting a day are answered; stale applications lapse, and a counter left unanswered is taken as a no.</summary>
        public static void Answers(HiringFile f, int day, int esteem, Func<Family, int> shortage, List<MarketNote> notes)
        {
            for (int i = 0; i < f.Applicants.Count; i++)
            {
                Applicant a = f.Applicants[i];
                Seeker s = f.SeekerOf(a.Seeker);
                if (s == null)
                {
                    continue;
                }
                if (a.Stage == (int)ApplyStage.Offered && day > a.OfferDay)
                {
                    Answer(f, a, s, day, esteem, shortage != null ? shortage(s.P.Def.Family) : 1000);
                    notes?.Add(new MarketNote { Kind = AnsweredNote, Applicant = a.Id, Ad = a.Ad });
                }
                else if (a.Stage == (int)ApplyStage.Countered && day > a.OfferDay + HiringCatalogue.OfferDays)
                {
                    a.Stage = (int)ApplyStage.Declined;
                    notes?.Add(new MarketNote { Kind = LapsedNote, Applicant = a.Id, Ad = a.Ad });
                }
                else if (a.Stage <= (int)ApplyStage.Interviewed && day > a.Day + HiringCatalogue.ApplicantDays)
                {
                    a.Stage = (int)ApplyStage.Lapsed;
                }
            }
        }

        /// <summary>The extra candidates the recruitment office brings for a campaign of so many.</summary>
        public static int OfficeBonus(int asked)
        {
            return Math.Max(HiringCatalogue.OfficeBonusLeast, (Math.Max(0, asked) * HiringCatalogue.OfficeBonusShare + 999) / 1000);
        }

        /// <summary>The seeker the recruitment office puts forward: a wanted role first, then the most able; null if the market has nobody.</summary>
        public static Seeker ForOffice(HiringFile f, string gameSkill, int[] wanted, int wantedCount)
        {
            Seeker best = null;
            int bestScore = int.MinValue;
            for (int i = 0; i < f.Seekers.Count; i++)
            {
                Seeker s = f.Seekers[i];
                RoleDef r = s.P.Def;
                if (s.Taken || s.Passive || r.GameSkill != gameSkill || s.P.Main / 1000 < HiringCatalogue.OfficeSkillFloor || Applied(f, s.Id))
                {
                    continue;
                }
                int score = s.P.Main / 1000;
                for (int w = 0; w < wantedCount && w < wanted.Length; w++)
                {
                    if (wanted[w] == r.Id)
                    {
                        score += 1000 - w * 100;
                        break;
                    }
                }
                if (score > bestScore)
                {
                    bestScore = score;
                    best = s;
                }
            }
            return best;
        }

        /// <summary>A search firm's fee for a role at an ask, cents: a share of a year's pay.</summary>
        public static int SearchFee(int askCents)
        {
            return (int)Math.Min(int.MaxValue, (long)askCents * TalentCatalogue.YearHours * HiringCatalogue.SearchFeeBp / 10000);
        }

        /// <summary>What is paid when a search starts, a share of the fee.</summary>
        public static int Retainer(int fee)
        {
            return (int)((long)fee * HiringCatalogue.SearchRetainerShare / 1000);
        }

        /// <summary>A search delivers passive candidates in work, in the role at its level or one over, each applying to the brief.</summary>
        public static int Deliver(HiringFile f, JobAd search, int day, RateOf rate, int shortage, IList<string> districts)
        {
            int made = 0;
            RoleDef r = TalentCatalogue.Role(search.Role);
            int most = HiringCatalogue.SeekersMost + HiringCatalogue.SearchSeekersMost;
            for (int k = 0; k < HiringCatalogue.SearchCandidates && f.Seekers.Count < most; k++)
            {
                int id = f.Next();
                int level = TalentCatalogue.Clamp(search.Level + (TechRandom.Mille(f.Seed, id, 41) < 300 ? 1 : 0), r.LeastLevel, r.MostLevel);
                string district = districts != null && districts.Count > 0 ? districts[TechRandom.Range(f.Seed, id, 42, 0, 0, districts.Count - 1)] : string.Empty;
                Seeker s = NewSeeker(f.Seed, id, day, false, r.Id, level, rate, shortage, district);
                int bandMiddle = (TalentCatalogue.LevelSkillLeast[level] + TalentCatalogue.LevelSkillMost[level]) / 2;
                s.P.Main = Math.Max(s.P.Main, bandMiddle * 1000);
                s.Employed = true;
                s.Passive = true;
                s.Claim = 0;
                s.Notice = 7 + level * 5;
                s.Until = day + HiringCatalogue.ApplicantDays;
                s.Ask = AskOf(f.Seed, s, rate, shortage);
                f.Seekers.Add(s);
                f.Applicants.Add(new Applicant { Id = f.Next(), Seeker = s.Id, Ad = search.Id, Day = day, Stage = (int)ApplyStage.Shortlisted, Interest = 600, Refs = true });
                search.Came++;
                made++;
            }
            return made;
        }

        /// <summary>Whether a role looks after a data centre's racks on site, as a crew search finds them.</summary>
        public static bool CrewRole(int role) => Array.IndexOf(HiringCatalogue.CrewRoles, role) >= 0;

        /// <summary>True for a business type the game gives no desks and no shifts: a warehouse, which a data centre is. Nobody of the game's can be seated there.</summary>
        public static bool NoShifts(string businessType) => businessType == HiringCatalogue.NoShifts;

        /// <summary>
        /// Whether a business seats a role. A business with shifts seats every role; one with none (a data centre) seats only the family that
        /// looks after racks, who are on the payroll there. A data engineer, a designer or a software engineer belong at a firm's desks.
        /// </summary>
        public static bool Seats(bool shifts, RoleDef role) => shifts || (role != null && role.Family == Family.Infrastructure);

        /// <summary>
        /// Whether a hire is on the mod's own payroll rather than the game's employee: a role the game has no job for, a job from home, a data
        /// centre's crew by a search, or anyone hired where the game has no shifts to give them (who would never be given work).
        /// </summary>
        public static bool OnPayroll(RoleDef role, int work, bool crewSearch, bool shifts) => !role.InGame || work == (int)Arrangement.Remote || crewSearch || !shifts;

        /// <summary>
        /// True for a professional who is the game's own employee at a business with no shifts: on its staff list, but never given a desk,
        /// a shift or a walk to work. The game's own unassigning ends it and leaves them waiting in its staff list for a place.
        /// </summary>
        public static bool Stranded(bool shifts, bool gameEmployee) => !shifts && gameEmployee;

        /// <summary>The level a crew search finds for a role: junior to staff, inside the role's own range.</summary>
        public static int CrewLevel(int role, int level)
        {
            RoleDef r = TalentCatalogue.Role(role);
            int least = Math.Max(r.LeastLevel, HiringCatalogue.CrewLeastLevel), most = Math.Min(r.MostLevel, HiringCatalogue.CrewMostLevel);
            return TalentCatalogue.Clamp(level, least, Math.Max(least, most));
        }

        /// <summary>A crew search's fee for one hire, cents: a share of a year's pay at the going rate, larger the more senior.</summary>
        public static int CrewFeeEach(int goingCents, int level)
        {
            int bp = HiringCatalogue.CrewFeeBp[TalentCatalogue.Clamp(level, 0, HiringCatalogue.CrewFeeBp.Length - 1)];
            return (int)Math.Min(int.MaxValue, (long)Math.Max(0, goingCents) * TalentCatalogue.YearHours * bp / 10000);
        }

        /// <summary>What a crew search takes when it is briefed: a third of the fee, rounded up, so it covers a third of the hires' shares to the cent.</summary>
        public static int CrewRetainer(int fee) => (int)(((long)Math.Max(0, fee) + 2) / 3);

        /// <summary>The candidates a crew search brings for so many technicians: half as many again, one more at the least, within its cap.</summary>
        public static int CrewCandidates(int crew)
        {
            int c = TalentCatalogue.Clamp(crew, 1, HiringCatalogue.CrewMost);
            return Math.Min(HiringCatalogue.CrewCandidatesMost, c + Math.Max(1, c / 2));
        }

        /// <summary>
        /// What a crew search is owed now, cents: its fee a hire for each started, less what is paid (the retainer counts towards
        /// the first). Each hire past the brief pays its share too; nothing more while nobody has started.
        /// </summary>
        public static int CrewDue(JobAd search)
        {
            if (search == null || search.Crew <= 0)
            {
                return 0;
            }
            long earned = (long)search.Fee * Math.Max(0, search.Hired) / search.Crew;
            return (int)Math.Max(0, Math.Min(int.MaxValue, earned - search.FeePaid));
        }

        /// <summary>
        /// A crew search delivers: candidates for the role at its level, vetted (no claim past the truth, references taken), each
        /// shortlisted for the brief. Seeded by the save's seed and the ids, so the same on every machine.
        /// </summary>
        public static int DeliverCrew(HiringFile f, JobAd search, int day, RateOf rate, int shortage, IList<string> districts)
        {
            int made = 0, want = CrewCandidates(search.Crew);
            int most = HiringCatalogue.SeekersMost + HiringCatalogue.SearchSeekersMost;
            for (int k = 0; k < want && f.Seekers.Count < most; k++)
            {
                int id = f.Next();
                string district = districts != null && districts.Count > 0 ? districts[TechRandom.Range(f.Seed, id, 44, 0, 0, districts.Count - 1)] : string.Empty;
                Seeker s = NewSeeker(f.Seed, id, day, false, search.Role, CrewLevel(search.Role, search.Level), rate, shortage, district);
                int bandMiddle = (TalentCatalogue.LevelSkillLeast[s.P.Level] + TalentCatalogue.LevelSkillMost[s.P.Level]) / 2;
                s.P.Main = Math.Max(s.P.Main, bandMiddle * 1000);
                s.Passive = true;
                s.Claim = 0;
                s.Until = day + HiringCatalogue.ApplicantDays;
                s.Ask = AskOf(f.Seed, s, rate, shortage);
                f.Seekers.Add(s);
                f.Applicants.Add(new Applicant { Id = f.Next(), Seeker = s.Id, Ad = search.Id, Day = day, Stage = (int)ApplyStage.Shortlisted, Interest = 600, Refs = true });
                search.Came++;
                made++;
            }
            return made;
        }
    }
}
