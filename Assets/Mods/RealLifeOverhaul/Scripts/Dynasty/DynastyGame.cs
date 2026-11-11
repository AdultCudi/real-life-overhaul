using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Skills;
using Entities;
using Extensions;
using Helpers;
using RealLifeOverhaul.Economy;
using UI;
using UI.Notification;
using UnityEngine;

namespace RealLifeOverhaul.Dynasty
{
    /// <summary>
    /// The one file of the dynasty that touches the game: the clock, the played character's name and age, the game's own skills and courses, the cash through the mod's one
    /// booking call, and the notices. It never touches how a character looks: no model, face, body, hair or clothes is read or written here or anywhere in the feature; the
    /// heir is played in the body the save has, and the game's own daily ageing (PlayerHelper.IncreasePlayerAge) draws the age it finds. Nothing per frame.
    /// </summary>
    public static class DynastyGame
    {
        public const string TaxKey = "reallife_transaction_dynasty_tax";
        public const string TaxPartKey = "reallife_transaction_dynasty_tax_part";
        public const string BequestKey = "reallife_transaction_dynasty_bequest";
        public const string CharityKey = "reallife_transaction_dynasty_charity";
        public const string KeepKey = "reallife_transaction_dynasty_keep";
        public const string FundKey = "reallife_transaction_dynasty_fund";
        public const string FeeKey = "reallife_transaction_dynasty_will";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        public static int Day => Save != null ? Save.Day : 0;

        public static int DaysPerYear
        {
            get
            {
                int days = GameCalendar.DaysPerYear;
                return days >= 8 ? days : 60;
            }
        }

        public static int MonthDays => MacroCatalogue.MonthDays(DaysPerYear);

        public static int Level => Prices.Level;

        /// <summary>
        /// The played character: the one the game itself ages and reads for its end in old age (PlayerHelper.CharacterData), or the save's first while the city is not up.
        /// Null before a save is in.
        /// </summary>
        private static CharacterData Character
        {
            get
            {
                CharacterData live = Live();
                if (live != null)
                {
                    return live;
                }
                GameInstance save = Save;
                return save != null && save.charactersData != null && save.charactersData.Count > 0 ? save.charactersData[0] : null;
            }
        }

        private static CharacterData Live()
        {
            try
            {
                return PlayerHelper.PlayerController != null ? PlayerHelper.CharacterData : null;
            }
            catch (Exception)
            {
                return null;   // the city is not up yet
            }
        }

        // The save's own record of the played character, where it is another object than the one the game ages: kept in step.
        private static CharacterData Kept()
        {
            GameInstance save = Save;
            CharacterData kept = save != null && save.charactersData != null && save.charactersData.Count > 0 ? save.charactersData[0] : null;
            return kept != null && !ReferenceEquals(kept, Live()) ? kept : null;
        }

        public static string Name => Character != null ? Character.name ?? string.Empty : string.Empty;

        /// <summary>The played character's age in days of the game's calendar; -1 before a save is in.</summary>
        public static int AgeDays => Character != null ? Character.ageInDays : -1;

        public static int AgeYears => Math.Max(0, AgeDays) / DaysPerYear;

        /// <summary>True while the game ages the player (its own option): without it nobody reaches their last days.</summary>
        public static bool Ageing => Save != null && Save.gameVariables != null && !Save.gameVariables.disableAging;

        /// <summary>The age alone, in days: for holding the last day back and for the tests. The game draws it at its next midnight, as it does every day.</summary>
        public static void SetAgeDays(int days)
        {
            CharacterData c = Character;
            if (c != null)
            {
                c.ageInDays = Math.Max(0, days);
            }
            CharacterData kept = Kept();
            if (kept != null)
            {
                kept.ageInDays = Math.Max(0, days);
            }
        }

        /// <summary>The player's cash in whole money, never under nothing.</summary>
        public static long Cash => Save != null ? (long)Math.Floor(Mathf.Max(0f, Mathf.Min(Save.Money, 1e12f))) : 0L;

        /// <summary>What the investors of the founder's firms are owed out of the wallet, whole money rounded up.</summary>
        public static long Reserved
        {
            get
            {
                try
                {
                    return DynastyBridges.Reserved != null ? Math.Max(0L, DynastyBridges.Reserved()) : 0L;
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                    return 0L;
                }
            }
        }

        public static long Spare => Math.Max(0L, Cash - Reserved);

        /// <summary>What the estate is made of besides the cash (DynastyHoldings); the cash alone where it cannot be asked.</summary>
        public static Holdings Holdings
        {
            get
            {
                try
                {
                    if (DynastyBridges.Holdings != null)
                    {
                        return DynastyBridges.Holdings();
                    }
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
                return new Holdings { Wealth = Cash };
            }
        }

        /// <summary>The net worth the estate is made from: the Finance app's with what it leaves out, every debt taken off.</summary>
        public static long NetWorth => Holdings.NetWorth;

        /// <summary>The estate as it stands now, for a settlement: read once, at the freeze.</summary>
        public static EstateIn Estate(HandMode mode, DynastyState s)
        {
            long cash = Cash;
            int day = Day;
            return new EstateIn
            {
                Day = day, Level = Level, Mode = mode, Generation = s.Generation, From = Name, Cash = cash, Reserved = Reserved, Other = NetWorth - cash,
                Gifts = DynastyRules.GiftsInWindow(s.Gifts, day, DaysPerYear), Keep = s.Will.Keep, Session = GoingPublic.InSession,
                Charities = DynastyBridges.Charities != null && DynastyBridges.Charities()
            };
        }

        /// <summary>
        /// Money out of the wallet, whole money, through the mod's one call, in chunks the wallet's float holds exactly (DynastyBooking). A settlement's sums were fixed against the
        /// cash at the freeze, so they are taken whatever was spent since (<paramref name="force"/>); a fee is only taken when the cash over the investors' floor covers it.
        /// Returns the whole money really booked: it is all of it unless the game refused a chunk, and then nothing is booked beyond that chunk.
        /// </summary>
        public static long PayUnits(long amount, string key, bool force)
        {
            if (amount <= 0 || (!force && amount > Spare))
            {
                return 0L;
            }
            return DynastyBooking.Units(amount, chunk => BizBooks.Book(-chunk, new TransactionInfo(key), null, null, force: force, showNotification: false));
        }

        /// <summary>As <see cref="PayUnits"/>: true when all of it was booked (a fee).</summary>
        public static bool Pay(long amount, string key, bool force) => amount <= 0 || PayUnits(amount, key, force) >= amount;

        /// <summary>A part of the tax debt, in cents. Returns the cents really booked.</summary>
        public static long PayCents(long cents, string key, bool force)
        {
            if (cents <= 0 || (!force && cents > Spare * 100))
            {
                return 0L;
            }
            return DynastyBooking.Cents(cents, chunk => BizBooks.Book(-chunk, new TransactionInfo(key), null, null, force: force, showNotification: false));
        }

        /// <summary>Money into the wallet, whole money, in the same chunks: a retired founder's fund and a bequest that did not land. Returns what came in.</summary>
        public static long Receive(long amount, string key)
        {
            return amount <= 0 ? 0L : DynastyBooking.Units(amount, chunk => BizBooks.Book(chunk, new TransactionInfo(key), null, null, force: true, showNotification: false));
        }

        /// <summary>
        /// The heir becomes the played person: their name and their age, and their share of what the founder had learned in the game's own skills and courses. A course the
        /// heir has only part of is open again at the hours they have, to be finished at the game's school. The look is left exactly as it is.
        /// </summary>
        public static void Become(string name, int ageYears, int share)
        {
            CharacterData c = Character;
            GameInstance save = Save;
            if (c == null || save == null)
            {
                return;
            }
            string was = c.name;
            if (!string.IsNullOrEmpty(name))
            {
                c.name = name;
            }
            int days = DynastyCatalogue.Clamp(ageYears, DynastyCatalogue.HeirLeast, DynastyCatalogue.HeirMost) * DaysPerYear;
            c.ageInDays = days;
            CharacterData kept = Kept();
            if (kept != null)
            {
                kept.name = c.name;
                kept.ageInDays = c.ageInDays;
            }
            int skills = 0, courses = 0;
            bool ok = true;
            for (int i = 0; c.skills != null && i < c.skills.Count; i++)
            {
                Skill skill = c.skills[i];
                if (skill != null)
                {
                    float had = skill.value;
                    skill.value = DynastyRules.Passed(had, share);
                    ok &= Math.Abs(skill.value - Math.Max(0f, had) * DynastyCatalogue.Clamp(share, 0, 1000) / 1000f) < 0.0001f && skill.value <= Math.Max(0f, had) + 0.0001f;
                    skills++;
                }
            }
            List<Diploma> diplomas = save.PlayerDiplomas;
            for (int i = 0; diplomas != null && i < diplomas.Count; i++)
            {
                Diploma d = diplomas[i];
                if (d == null)
                {
                    continue;
                }
                int hours = d.minutesStudied;
                bool done = d.completed;
                d.minutesStudied = DynastyRules.CourseMinutes(hours, done, share);   // a course the founder finished stays finished (DynastyRules.CourseMinutes)
                ok &= d.completed == done && (done ? d.minutesStudied == Math.Max(0, hours) : d.minutesStudied <= hours && d.minutesStudied == DynastyRules.Passed(hours, share));
                courses++;
            }
            if (Meta.Dev.Verbose)
            {
                ok &= c.name == (string.IsNullOrEmpty(name) ? was : name) && c.ageInDays == days && (kept == null || (kept.name == c.name && kept.ageInDays == days));
                Logger?.Info("Dynasty check (the heir takes over): played as " + c.name + ", " + ageYears + " years old (" + c.ageInDays + " days); " + skills + " skill(s) and " + courses
                             + " course(s) at " + share / 10f + "% of the founder's, the courses the founder finished still finished: " + (ok ? "PASS" : "FAIL"));
            }
        }

        // ---- the clock and the death scene

        /// <summary>The game's time machine is running (a sleep, a skip, a stay in a cell): days pass faster than the director looks.</summary>
        public static bool Skipping
        {
            get
            {
                try
                {
                    UIs uis = InstanceBehavior<UIs>.Instance;
                    return uis != null && uis.timeMachine != null && uis.timeMachine.isRunning;
                }
                catch (Exception)
                {
                    return false;
                }
            }
        }

        /// <summary>The game's death scene has begun (FuneralHelper.PlayerDead): nothing of the dynasty runs under it.</summary>
        public static bool PlayerDead => GameMembers.PlayerDead;

        // ---- words

        public static string Money(long amount) => ((float)amount).ToCurrencyFormat();

        public static void Tell(NotificationType type, string key, Dictionary<string, string> data, Action click = null)
        {
            Notifications.Show(type, key, data, 12f, null, click != null ? Guard.Wrap(click) : null, true, false);
        }

        public static Dictionary<string, string> Data(params string[] pairs)
        {
            var d = new Dictionary<string, string>(pairs.Length / 2 + 1);
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                d[pairs[i]] = pairs[i + 1] ?? string.Empty;
            }
            return d;
        }
    }
}
