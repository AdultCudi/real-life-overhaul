using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>Somebody who may carry on or be left something, as the family and the social life hold them on the day (read through the wiring).</summary>
    public sealed class KinPerson
    {
        public KinKind Kind;
        public int Id;
        public string Name = string.Empty;
        public int Age;                 // years; 0 where nobody keeps it
        public bool Married;            // a partner: married to the founder
        public bool Family;             // a person: one of the family (the social life's origin)
        public int School;              // a child: 0 none, 1 public, 2 private
        public int FundMille;           // a child: thousandths of the college fund paid in
        public int Closeness;           // 0 to 100: a child's closeness, a partner's affection, a person's closeness
        public bool Household;          // the population holds a household for them that money can reach
        public long Room;               // ... and the most it can take, whole money
    }

    /// <summary>What an estate is on the day, read from the game once (DynastyGame) or written by a check.</summary>
    public sealed class EstateIn
    {
        public int Day;
        public int Level = PriceRules.Start;
        public HandMode Mode;
        public int Generation = 1;
        public string From = string.Empty;
        public long Cash;               // whole money in the wallet
        public long Reserved;           // of it, the investors' floor, rounded up
        public long Other;              // the Finance app's net worth less the cash: everything else, every debt taken off
        public long Gifts;              // gifts to people inside the window
        public int Keep;                // index into KeepSteps (retiring only)
        public long Prior;              // an earlier taxable estate of the same founder to count with this one
        public bool Session;            // a Going Public session: the city's people and the charities are the host's
        public bool Charities;          // Giving runs and its pots can take a bequest
    }

    /// <summary>Why the founder cannot retire now.</summary>
    public enum RetireWhy
    {
        Ok = 0,
        Off = 1,        // the dynasty is not running for this save
        Running = 2,    // a handover is under way
        Young = 3,      // under the age the will sets
        NoHeir = 4,     // nobody who can carry on
        Session = 5,    // a Going Public session
        Law = 6,        // the law has not finished with the founder
        Elder = 7,      // the family already keeps a retired founder
        NoWill = 8      // no will sets an age to retire at
    }

    /// <summary>Why a will cannot be signed as drafted.</summary>
    public enum WillWhy
    {
        Ok = 0,
        TooMany = 1,
        Share = 2,      // a share out of its steps
        Over = 3,       // the bequests together take more than the heir's reserved part leaves
        Heir = 4,       // a bequest to the heir themselves, or the same person twice
        Age = 5,
        Keep = 6,
        Same = 7,       // nothing changed
        Broke = 8,      // the fee is not there
        Off = 9
    }

    /// <summary>What holds the last day back.</summary>
    public enum DeathHold
    {
        None = 0,
        Session = 1,    // in a Going Public session nothing of a player's changes hands
        Custody = 2,    // in a cell, in prison or before the court
        Final = 3,      // Permadeath is on for the save: a death is its own, and ends the save
        NoHeir = 4,     // nobody can carry on: the game's own end
        Off = 5
    }

    /// <summary>
    /// The rules of the dynasty, with no game in them: the tax and its bands, the gifts' window, a will's terms and its fee, who carries on, an estate settled to the last
    /// unit of money, the debt for unpaid tax, the retired founder's fund, what the heir knows and is known for, and what holds a handover back. Everything is a whole number
    /// worked out from its inputs, so the same inputs give the same answer on every machine. COST: a few operations a call; a settlement is made once a handover.
    /// </summary>
    public static class DynastyRules
    {
        // ---- money at today's prices

        /// <summary>An amount written at the start's prices, at the price level (tenths of a point), in whole money.</summary>
        public static long At(long amount, int level) => amount * Math.Max(PriceRules.Least, Math.Min(PriceRules.Most, level)) / PriceRules.Start;

        // ---- the tax

        /// <summary>The tax on a taxable estate, whole money of the day: nothing up to the threshold, then each band's rate on the part inside it.</summary>
        public static long Tax(long taxable, int level)
        {
            long over = taxable - At(DynastyCatalogue.Threshold, level);
            if (over <= 0)
            {
                return 0L;
            }
            long tax = 0;
            int last = DynastyCatalogue.BandRate.Length - 1;
            for (int i = 0; i < last && over > 0; i++)
            {
                long part = Math.Min(over, At(DynastyCatalogue.BandWidth[i], level));
                tax += part * DynastyCatalogue.BandRate[i] / 1000;
                over -= part;
            }
            return tax + Math.Max(0L, over) * DynastyCatalogue.BandRate[last] / 1000;
        }

        /// <summary>
        /// The tax on an estate that comes after an earlier one of the same founder: the two are taxed as one and what the earlier one alone would bear is taken off, so the
        /// threshold and the low bands are used once however the estate is split.
        /// </summary>
        public static long TaxOn(long taxable, long prior, int level)
        {
            long before = Math.Max(0L, prior);
            return Tax(Math.Max(0L, taxable) + before, level) - Tax(before, level);
        }

        // ---- gifts in life

        /// <summary>The days a gift counts for.</summary>
        public static int WindowDays(int yearDays) => DynastyCatalogue.WindowYears * Math.Max(1, yearDays);

        /// <summary>A gift to a person, dated. Past the most kept, the two oldest become one on the newer of their days: nothing is dropped, and none leaves the window sooner.</summary>
        public static void Gift(List<LifeGift> gifts, int day, long amount)
        {
            if (amount <= 0)
            {
                return;
            }
            if (gifts.Count > 0 && gifts[gifts.Count - 1].Day == day)
            {
                LifeGift same = gifts[gifts.Count - 1];
                same.Amount += amount;
                gifts[gifts.Count - 1] = same;
                return;
            }
            gifts.Add(new LifeGift { Day = day, Amount = amount });
            while (gifts.Count > DynastyCatalogue.GiftsKept)
            {
                LifeGift merged = gifts[1];
                merged.Amount += gifts[0].Amount;
                gifts[1] = merged;
                gifts.RemoveAt(0);
            }
        }

        /// <summary>The gifts still inside the window on a day, whole money. A gift made on the day itself counts.</summary>
        public static long GiftsInWindow(List<LifeGift> gifts, int day, int yearDays)
        {
            long sum = 0;
            int window = WindowDays(yearDays);
            for (int i = 0; i < gifts.Count; i++)
            {
                sum += day - gifts[i].Day < window ? gifts[i].Amount : 0L;
            }
            return sum;
        }

        /// <summary>Gifts that have left the window are let go.</summary>
        public static void ForgetOld(List<LifeGift> gifts, int day, int yearDays)
        {
            int window = WindowDays(yearDays);
            for (int i = gifts.Count - 1; i >= 0; i--)
            {
                if (day - gifts[i].Day >= window)
                {
                    gifts.RemoveAt(i);
                }
            }
        }

        // ---- the will

        /// <summary>What a will costs to sign: the first, or a change; nothing at a law firm of the founder's own.</summary>
        public static long Fee(bool first, bool ownFirm, int level) => ownFirm ? 0L : At(first ? DynastyCatalogue.WillFee : DynastyCatalogue.ChangeFee, level);

        /// <summary>Whether a will's terms stand, and the first reason if not.</summary>
        public static WillWhy Valid(Will w)
        {
            if (w.Bequests.Count > DynastyCatalogue.BequestsMost)
            {
                return WillWhy.TooMany;
            }
            for (int i = 0; i < w.Bequests.Count; i++)
            {
                Bequest b = w.Bequests[i];
                if (b.Share <= 0 || b.Share % DynastyCatalogue.ShareStep != 0 || b.Kind == KinKind.None)
                {
                    return WillWhy.Share;
                }
                if (b.Kind == KinKind.Charity ? b.Id < 0 || b.Id > 2 : w.HeirKind != KinKind.None && b.Same(w.HeirKind, w.HeirId, w.HeirName))
                {
                    return b.Kind == KinKind.Charity ? WillWhy.Share : WillWhy.Heir;
                }
                for (int k = 0; k < i; k++)
                {
                    if (w.Bequests[k].Same(b.Kind, b.Id, b.Name))
                    {
                        return WillWhy.Heir;
                    }
                }
            }
            if (w.Others() > DynastyCatalogue.OthersMost)
            {
                return WillWhy.Over;
            }
            if (w.RetireAge < DynastyCatalogue.RetireLeast || w.RetireAge > DynastyCatalogue.RetireMost
                || (w.RetireAge - DynastyCatalogue.RetireLeast) % DynastyCatalogue.RetireStep != 0)
            {
                return WillWhy.Age;
            }
            return w.Keep < 0 || w.Keep >= DynastyCatalogue.KeepSteps.Length ? WillWhy.Keep : WillWhy.Ok;
        }

        /// <summary>A will read from a save put inside its bounds: bequests that cannot stand are dropped, the last first, until the rest does.</summary>
        public static void Tidy(Will w)
        {
            w.RetireAge = DynastyCatalogue.RetireLeast + DynastyCatalogue.Clamp((w.RetireAge - DynastyCatalogue.RetireLeast) / DynastyCatalogue.RetireStep, 0,
                (DynastyCatalogue.RetireMost - DynastyCatalogue.RetireLeast) / DynastyCatalogue.RetireStep) * DynastyCatalogue.RetireStep;
            w.Keep = DynastyCatalogue.Clamp(w.Keep, 0, DynastyCatalogue.KeepSteps.Length - 1);
            while (w.Bequests.Count > 0 && Valid(w) != WillWhy.Ok)
            {
                w.Bequests.RemoveAt(w.Bequests.Count - 1);
            }
        }

        /// <summary>The share a bequest may still be raised to, with the others as they are.</summary>
        public static int ShareRoom(Will w, Bequest b) => Math.Max(0, DynastyCatalogue.OthersMost - w.Others() + (b != null ? b.Share : 0));

        // ---- who carries on

        /// <summary>Whether somebody can carry on: an adult the mod holds, not too old, and a person only if they are family.</summary>
        public static bool CanCarryOn(KinPerson c)
        {
            return c != null && (c.Kind == KinKind.Partner || c.Kind == KinKind.Child || (c.Kind == KinKind.Person && c.Family))
                   && c.Age >= DynastyCatalogue.HeirLeast && c.Age <= DynastyCatalogue.HeirMost;
        }

        /// <summary>
        /// Who carries on: the heir the will names if they are there and can, else the fixed order of the law: the partner, then the eldest grown child, then nobody (the
        /// state takes the estate, which is the game's own end). <paramref name="byLaw"/> says the will did not decide.
        /// </summary>
        public static KinPerson Choose(Will w, IList<KinPerson> people, out bool byLaw)
        {
            byLaw = true;
            if (w != null && w.Written && w.HeirKind != KinKind.None)
            {
                for (int i = 0; i < people.Count; i++)
                {
                    KinPerson c = people[i];
                    if (c.Kind == w.HeirKind && (c.Kind == KinKind.Person ? c.Id == w.HeirId : c.Name == w.HeirName) && CanCarryOn(c))
                    {
                        byLaw = false;
                        return c;
                    }
                }
            }
            KinPerson eldest = null;
            for (int i = 0; i < people.Count; i++)
            {
                KinPerson c = people[i];
                if (c.Kind == KinKind.Partner && CanCarryOn(c))
                {
                    return c;
                }
                if (c.Kind == KinKind.Child && CanCarryOn(c) && (eldest == null || c.Age > eldest.Age))
                {
                    eldest = c;
                }
            }
            return eldest;
        }

        /// <summary>A partner's age where nobody keeps it: a little under the founder's, inside what an heir may be.</summary>
        public static int PartnerAge(int kept, int founder)
        {
            return kept > 0 ? kept : DynastyCatalogue.Clamp(founder - DynastyCatalogue.PartnerGap, DynastyCatalogue.HeirLeast, DynastyCatalogue.HeirMost);
        }

        /// <summary>A grown child's age: eighteen on the day they were first seen grown, a year more for each of the game's years since.</summary>
        public static int ChildAge(float kept, bool grown, int grownDay, int day, int yearDays)
        {
            return grown ? DynastyCatalogue.HeirLeast + Math.Max(0, day - grownDay) / Math.Max(1, yearDays) : (int)Math.Floor(kept);
        }

        /// <summary>The share of the founder's learning the heir starts with, thousandths: what the family and the schools gave them.</summary>
        public static int Upbringing(KinPerson c)
        {
            if (c == null)
            {
                return 0;
            }
            int close = DynastyCatalogue.Clamp(c.Closeness, 0, 100);
            switch (c.Kind)
            {
                case KinKind.Child:
                    return DynastyCatalogue.ChildBase + (c.School >= 2 ? DynastyCatalogue.ChildPrivate : c.School == 1 ? DynastyCatalogue.ChildSchool : 0)
                           + DynastyCatalogue.ChildFund * DynastyCatalogue.Clamp(c.FundMille, 0, 1000) / 1000 + DynastyCatalogue.ChildClose * close / 100;
                case KinKind.Partner:
                    return DynastyCatalogue.PartnerBase + DynastyCatalogue.PartnerClose * close / 100;
                case KinKind.Person:
                    return DynastyCatalogue.RelativeBase + DynastyCatalogue.RelativeClose * close / 100;
                default:
                    return 0;
            }
        }

        /// <summary>What the heir has of a figure of the founder's learning (minutes studied, a skill's thousandths).</summary>
        public static int Passed(int value, int share) => (int)((long)Math.Max(0, value) * DynastyCatalogue.Clamp(share, 0, 1000) / 1000);

        /// <summary>The same of a skill the game keeps as a fraction.</summary>
        public static float Passed(float value, int share) => Math.Max(0f, value) * DynastyCatalogue.Clamp(share, 0, 1000) / 1000f;

        /// <summary>What the family name keeps of how well the city knew the founder.</summary>
        public static int FameKept(int value) => Math.Max(0, value) * DynastyCatalogue.FameShare / 1000;

        /// <summary>A reputation that was the founder's own (integrity) goes half the way back to where everybody starts.</summary>
        public static int OwnAfter(int value, int start) => start + (value - start) / 2;

        // ---- an estate settled

        /// <summary>Where a bequest lands on the day, and the most that can land there.</summary>
        public static Landing LandingOf(Bequest b, KinPerson heir, IList<KinPerson> people, EstateIn e, out long room, out int id)
        {
            room = long.MaxValue;
            id = b.Id;
            if (b.Kind == KinKind.Charity)
            {
                return e.Charities && !e.Session ? Landing.Pot : Landing.None;
            }
            KinPerson c = null;
            for (int i = 0; i < people.Count; i++)
            {
                if (people[i].Kind == b.Kind && (b.Kind == KinKind.Person ? people[i].Id == b.Id : people[i].Name == b.Name))
                {
                    c = people[i];
                    break;
                }
            }
            if (c == null || c == heir)
            {
                return Landing.None;   // no longer there, or the one who takes the rest anyway
            }
            if (c.Household && !e.Session)
            {
                room = Math.Max(0L, c.Room);
                id = c.Id;   // the person the household is found by (a partner's bequest is written by name)
                return Landing.Household;
            }
            return c.Kind != KinKind.Person || c.Family ? Landing.Family : Landing.None;
        }

        /// <summary>
        /// An estate settled. The estate is the cash over the investors' floor and everything else the Finance app counts, every debt taken off, so debts are paid before
        /// anybody inherits and nothing is written off. A retiring founder keeps their share of it in cash. Each bequest is its share of what passes, paid from the cash;
        /// if the cash left after the tax does not cover them all, every one is cut by the same part. Charities' bequests are exempt, a married partner's part up to half;
        /// gifts to people inside the window are counted back in. The tax is never cut: what the cash does not cover is owed. The heir takes the rest, debts and all.
        /// Net = Keep + Tax + Paid + Residue.
        /// </summary>
        public static Statement Settle(EstateIn e, Will will, KinPerson heir, bool byLaw, IList<KinPerson> people)
        {
            var s = new Statement
            {
                Day = e.Day, Mode = e.Mode, Generation = e.Generation, From = e.From ?? string.Empty, Level = e.Level, ByLaw = byLaw,
                Cash = Math.Max(0L, e.Cash), Other = e.Other, Gifts = Math.Max(0L, e.Gifts), Prior = Math.Max(0L, e.Prior)
            };
            s.Reserved = DynastyCatalogue.Clamp(e.Reserved, 0L, s.Cash);
            long spare = s.Cash - s.Reserved;
            s.Net = spare + s.Other;
            if (heir != null)
            {
                s.To = heir.Name ?? string.Empty;
                s.HeirKind = heir.Kind;
                s.HeirId = heir.Id;
                s.HeirAge = heir.Age;
                s.Upbringing = Upbringing(heir);
                s.SpouseHeir = heir.Kind == KinKind.Partner && heir.Married;
            }
            s.UnderWater = s.Net <= 0;
            long passes = s.UnderWater ? 0L : s.Net;
            if (!s.UnderWater && e.Mode == HandMode.Retire)
            {
                int keep = DynastyCatalogue.KeepSteps[DynastyCatalogue.Clamp(e.Keep, 0, DynastyCatalogue.KeepSteps.Length - 1)];
                s.Keep = Math.Min(passes * keep / 1000, spare);
                passes -= s.Keep;
            }
            long cash = spare - s.Keep;
            bool useWill = will != null && will.Written && !s.UnderWater && e.Mode != HandMode.Elder;
            long spouseLine = -1;
            for (int i = 0; useWill && i < will.Bequests.Count; i++)
            {
                Bequest b = will.Bequests[i];
                var line = new BequestLine { Kind = b.Kind, Id = b.Id, Name = b.Name, Share = b.Share };
                line.Landing = LandingOf(b, heir, people, e, out long room, out int id);
                line.Id = id;
                line.Asked = line.Landing == Landing.None ? 0L : Math.Min(passes * b.Share / 1000, room);
                if (b.Kind == KinKind.Partner && line.Landing != Landing.None && Married(people))
                {
                    spouseLine = s.Lines.Count;
                }
                s.Lines.Add(line);
            }
            int scale = 1000;
            for (int guard = 0; guard <= 1001; guard++)
            {
                long sum = 0, charity = 0;
                for (int i = 0; i < s.Lines.Count; i++)
                {
                    BequestLine l = s.Lines[i];
                    l.Amount = l.Asked * scale / 1000;
                    sum += l.Amount;
                    charity += l.Landing == Landing.Pot ? l.Amount : 0L;
                }
                long half = passes * DynastyCatalogue.SpouseShare / 1000;
                long spouse = s.SpouseHeir ? Math.Min(passes - sum, half) : spouseLine >= 0 ? Math.Min(s.Lines[(int)spouseLine].Amount, half) : 0L;
                s.Charity = charity;
                s.Spouse = Math.Max(0L, spouse);
                s.Taxable = Math.Max(0L, passes - s.Charity - s.Spouse) + s.Gifts;
                s.Tax = TaxOn(s.Taxable, s.Prior, e.Level);
                s.Paid = sum;
                s.Scale = scale;
                long left = Math.Max(0L, cash - s.Tax);
                if (sum <= left || scale == 0)
                {
                    break;
                }
                int next = (int)(scale * left / sum);
                scale = Math.Max(0, next >= scale ? scale - 1 : next);
            }
            s.TaxCash = Math.Min(s.Tax, Math.Max(0L, cash));
            s.Loan = s.Tax - s.TaxCash;
            s.Residue = s.Net - s.Keep - s.Tax - s.Paid;
            return s;
        }

        private static bool Married(IList<KinPerson> people)
        {
            for (int i = 0; i < people.Count; i++)
            {
                if (people[i].Kind == KinKind.Partner)
                {
                    return people[i].Married;
                }
            }
            return false;
        }

        /// <summary>True when a statement adds up: the estate is what was kept, the tax, the bequests paid and what the heir is left, and the tax is the cash part and the debt.</summary>
        public static bool Balanced(Statement s)
        {
            long paid = 0;
            for (int i = 0; i < s.Lines.Count; i++)
            {
                paid += s.Lines[i].Amount;
            }
            return paid == s.Paid && s.Net == s.Keep + s.Tax + s.Paid + s.Residue && s.Tax == s.TaxCash + s.Loan && s.Tax >= 0 && s.Keep >= 0 && s.Paid >= 0;
        }

        /// <summary>The cash a settlement takes out of the wallet in all (the tax's cash part, what is kept and the bequests): never more than the cash over the floor.</summary>
        public static long CashOut(Statement s) => s.TaxCash + s.Keep + s.Paid;

        // ---- the retired founder

        /// <summary>The retired founder as the family keeps them: their fund, and a month's living out of it so that it lasts to the game's own last age.</summary>
        public static Elder Retired(string name, int ageDays, int day, long fund, long taxable, long tax, int yearDays, int monthDays)
        {
            int left = Math.Max(1, (DynastyCatalogue.LastAge * Math.Max(1, yearDays) - ageDays) / Math.Max(1, monthDays));
            return new Elder { Name = name ?? string.Empty, Born = day - ageDays, Since = day, Fund = Math.Max(0L, fund), Draw = Math.Max(0L, fund) / left, Taxable = taxable, Tax = tax };
        }

        /// <summary>A month's living drawn from the fund, once a month. Returns what was drawn.</summary>
        public static long ElderMonth(Elder e, int month)
        {
            if (e == null || month == e.Month)
            {
                return 0L;
            }
            e.Month = month;
            long draw = Math.Min(e.Draw, e.Fund);
            e.Fund -= draw;
            return draw;
        }

        public static int ElderAge(Elder e, int day, int yearDays) => (day - e.Born) / Math.Max(1, yearDays);

        public static bool ElderDies(Elder e, int day, int yearDays) => e != null && day - e.Born >= DynastyCatalogue.LastAge * Math.Max(1, yearDays);

        /// <summary>
        /// What is left of a retired founder's fund when they die: an estate of its own, counted with the one they handed over if that was inside the window, so keeping a fund
        /// back never uses the threshold twice. The rest goes to the head of the family.
        /// </summary>
        public static Statement SettleElder(Elder e, string to, int day, int level, int yearDays, int generation)
        {
            var s = new Statement
            {
                Day = day, Mode = HandMode.Elder, Generation = generation, From = e.Name, To = to ?? string.Empty, Level = level, Cash = e.Fund, Net = e.Fund,
                Prior = day - e.Since < WindowDays(yearDays) ? Math.Max(0L, e.Taxable) : 0L, Taxable = e.Fund
            };
            s.Tax = Math.Min(e.Fund, TaxOn(s.Taxable, s.Prior, level));
            s.TaxCash = s.Tax;
            s.Residue = s.Net - s.Tax;
            return s;
        }

        // ---- the debt for tax the cash did not cover

        /// <summary>Tax owed is added to the debt: a part a day over the years it runs, sized for the interest it will bear.</summary>
        public static void Borrow(EstateLoan l, long amount, int day, int yearDays)
        {
            if (amount <= 0)
            {
                return;
            }
            l.Principal += amount;
            l.Owed += amount * 100;
            int days = DynastyCatalogue.LoanYears * Math.Max(1, yearDays);
            long withInterest = l.Owed + l.Owed * DynastyCatalogue.LoanRate * DynastyCatalogue.LoanYears / 2000;
            l.PerDay = Math.Max(1L, (withInterest + days - 1) / days);
            l.Since = l.Since > 0 ? l.Since : day;
            l.LastDay = day;
            l.Late = 0;
        }

        /// <summary>
        /// One day of the debt: the day's interest is added, then a part is due. It is paid when the cash over the floor covers it; if not it is a late day, and after too many
        /// in a row it is taken whatever the cash (<paramref name="forced"/>). Returns the cents to book; nothing while nothing is owed.
        /// </summary>
        public static long LoanDay(EstateLoan l, long spareCents, int yearDays, out bool forced)
        {
            forced = false;
            if (l.Owed <= 0)
            {
                return 0L;
            }
            l.Owed += (l.Owed * DynastyCatalogue.LoanRate + 500L * Math.Max(1, yearDays)) / (1000L * Math.Max(1, yearDays));
            long due = Math.Min(l.PerDay, l.Owed);
            if (spareCents < due)
            {
                l.Late++;
                if (l.Late <= DynastyCatalogue.LoanLateMost)
                {
                    return 0L;
                }
                forced = true;
            }
            l.Late = forced ? l.Late : 0;
            l.Owed -= due;
            l.PaidCents += due;
            if (l.Owed <= 0)
            {
                l.Owed = 0;
                l.Late = 0;
            }
            return due;
        }

        // ---- when a handover may happen

        /// <summary>The age in days from which the founder is in their last days: the handover is made then, before the game's own end comes.</summary>
        public static int LastDays(int yearDays) => DynastyCatalogue.LastAge * Math.Max(1, yearDays) - 2;

        /// <summary>The age in days a founder whose handover is held back is kept at, so the game's own end does not come first.</summary>
        public static int HeldAge(int yearDays) => DynastyCatalogue.LastAge * Math.Max(1, yearDays) - 5;

        /// <summary>
        /// Whether the founder is in their last days: from the age they begin at, and, once the last day has been held back, from the age it is held at, so the estate
        /// passes the moment what held it is over and not days later.
        /// </summary>
        public static bool InLastDays(int ageDays, int yearDays, bool held) => ageDays >= LastDays(yearDays) || (held && ageDays >= HeldAge(yearDays));

        /// <summary>What holds the last day back, the first reason; None and the estate passes today.</summary>
        public static DeathHold Hold(bool running, bool final, bool session, bool custody, bool heir)
        {
            return !running ? DeathHold.Off : final ? DeathHold.Final : !heir ? DeathHold.NoHeir : session ? DeathHold.Session : custody ? DeathHold.Custody : DeathHold.None;
        }

        /// <summary>Whether the last day is put off (the age held) for a reason: only while somebody can carry on and the death is the dynasty's to settle.</summary>
        public static bool Holds(DeathHold why) => why == DeathHold.Session || why == DeathHold.Custody;

        /// <summary>
        /// Whether the founder may retire now, and the first reason if not. Retiring is a choice, so it is refused while the law has anything open against the founder: it is no
        /// way out of a sentence, a case, probation or a suspended licence.
        /// </summary>
        public static RetireWhy MayRetire(bool running, bool handover, Will w, int age, bool heir, bool session, bool law, bool elder)
        {
            if (!running)
            {
                return RetireWhy.Off;
            }
            if (handover)
            {
                return RetireWhy.Running;
            }
            if (w == null || !w.Written)
            {
                return RetireWhy.NoWill;
            }
            if (age < w.RetireAge)
            {
                return RetireWhy.Young;
            }
            return !heir ? RetireWhy.NoHeir : session ? RetireWhy.Session : law ? RetireWhy.Law : elder ? RetireWhy.Elder : RetireWhy.Ok;
        }

        /// <summary>A fixed hash of a seed and three numbers: the same on every machine.</summary>
        public static uint Hash(int seed, int a, int b, int c)
        {
            unchecked
            {
                uint h = (uint)seed * 2654435761u ^ (uint)a * 2246822519u ^ (uint)b * 3266489917u ^ (uint)c * 668265263u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                h *= 3266489917u;
                h ^= h >> 16;
                return h;
            }
        }
    }
}
