using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// How a night with somebody turned out (<see cref="LoveRules.NightOutcome"/>). Saved as a number: new members go at the end.
    /// </summary>
    public enum NightOutcome { Warm = 0, Awkward = 1, Regret = 2, WantsMore = 3, GotAround = 4, Mishap = 5 }

    /// <summary>Where an evening's offer came from.</summary>
    public enum NightKind { Drinks = 0, Party = 1 }

    /// <summary>How the one who minds found out. Saved as a number: new members go at the end.</summary>
    public enum CatchHow { Seen = 0, Friend = 1, Post = 2, Night = 3, Child = 4 }

    /// <summary>What the player says when somebody asks about the other. Not saved.</summary>
    public enum LoveAnswer { Confess = 0, Deny = 1, EndOther = 2, EndThis = 3 }

    /// <summary>What a roll against the chance of being found out came to.</summary>
    public enum CatchVerdict { Nothing = 0, Sign = 1, Caught = 2 }

    /// <summary>A public outing of the player's with somebody, kept a few days so two on one day can be told apart (<see cref="LoveRules.Closeness"/>).</summary>
    public sealed class LoveOuting
    {
        public int Person;
        public int Day;
        public int Minute;       // minute of the day it began at
        public int District = -1;   // the neighbourhood it was in, in the order of the game's list; -1 not known
    }

    /// <summary>One night, kept a little while: who, how it went and what is still to come of it.</summary>
    public sealed class LoveHook
    {
        public int Person;
        public int Day;
        public int Hour;
        public NightOutcome Outcome;
        public int Drinks;           // tenths of a drink in the blood when the night began
        public bool Taken;           // the player was with somebody
        public bool Mine;            // it was the player's place
        public int TextDay;          // the day they text about it; 0 none, or already sent
        public int QuietUntil;       // a day before which they do not get in touch of their own accord; 0 none
        public bool Around;          // it got around
    }

    /// <summary>A child that came of a night: the news, how the player chose to be a parent, the birth, what is paid.</summary>
    public sealed class LoveBaby
    {
        public int Person;           // slot of the other parent in the cast
        public int Day;              // the night it came of
        public int NewsDay;          // the day the news comes
        public int DueDay;           // the day it is born
        public int Carrier;          // 0 the other parent, 1 the player
        public bool Told;            // the news has come
        public LoveChoice Choice;    // how the player chose to be a parent
        public int ChoiceDay;        // the day they chose; 0 not yet
        public bool Born;
        public bool Girl;
        public string Name;          // the child's name; kept here until they are in the family's list
        public int Support;          // a month's support, at the start's prices; 0 none
        public int PaidDay;          // the day support was last paid
        public int Behind;           // months of support not paid for want of the money
        public bool Partner;         // the player's partner (not the other parent) has learned of the child
    }

    /// <summary>How the player chose to be a parent. Saved as a number.</summary>
    public enum LoveChoice { None = 0, Involved = 1, Support = 2, StayOut = 3 }

    /// <summary>
    /// Everything the second relationship, the nights and what comes of them keep in a save (key reallife:love): who the second
    /// person is and what each of the two suspects, who has found out and is waiting to be answered, the recent nights, the children
    /// of nights, and when the last scandal was. Game-free; <see cref="LoveStore"/> writes and reads it.
    /// </summary>
    public sealed class LoveFile
    {
        public const int OutingsMost = 8;
        public const int HooksMost = 8;
        public const int BabiesMost = 3;

        /// <summary>
        /// The slot of the second person in the player's life while they have somebody else (the first being the one the social life
        /// calls the partner, <see cref="SocialState.Partner"/>); -1 for nobody.
        /// </summary>
        public int Second = -1;
        public int SecondSince;
        public readonly int[] Suspicion = new int[2];      // 0 to 100 for the first and the second
        public readonly int[] SignDay = new int[2];        // the day of the last sign each was given
        public readonly int[] DeniedDay = new int[2];      // the day a denial held; 0 none
        public readonly int[] Knows = new int[2];          // 1 once they know about the other
        public readonly int[] TagDay = new int[2];         // the last day a post of the player's on Flare tagged each
        public int Forgiven;                               // times the person in <see cref="ForgivenWho"/> forgave
        public int ForgivenWho = -1;
        public int AroundUntil;                            // a night got around: until this day the catch chance is higher

        // Who has found out and waits to be answered (-1: nobody).
        public int ConfrontWho = -1;
        public int ConfrontOf = -1;
        public int ConfrontDay;
        public CatchHow ConfrontHow;

        // A night's offer to date, still to be answered.
        public int DateOfferWho = -1;
        public int DateOfferDay;

        public int LastOfferDay;                           // the day of the last offer of a night
        public int LastScandalDay;                         // the day of the last scandal; 0 none
        public int Nights;                                 // nights had, all told
        public int LastSupportDay;

        public readonly List<LoveOuting> Outings = new List<LoveOuting>(OutingsMost);
        public readonly List<LoveHook> Hooks = new List<LoveHook>(HooksMost);
        public readonly List<LoveBaby> Babies = new List<LoveBaby>(BabiesMost);

        /// <summary>Lines of a later version's, kept as they were read and written back.</summary>
        public readonly List<string> Kept = new List<string>(2);

        /// <summary>0 for the first partner, 1 for the second; -1 for anybody else.</summary>
        public int SideOf(int person, int first)
        {
            return person == first ? 0 : person == Second && person >= 0 ? 1 : -1;
        }

        public bool Has => Second >= 0 || ConfrontWho >= 0 || DateOfferWho >= 0 || Hooks.Count > 0 || Babies.Count > 0 || Nights > 0 || Outings.Count > 0
                           || LastScandalDay > 0 || Forgiven > 0 || AroundUntil > 0;

        /// <summary>The second relationship over: nobody is secret from anybody and nobody suspects.</summary>
        public void EndSecond()
        {
            Second = -1;
            SecondSince = 0;
            for (int i = 0; i < 2; i++)
            {
                Suspicion[i] = 0;
                SignDay[i] = 0;
                DeniedDay[i] = 0;
                Knows[i] = 0;
                TagDay[i] = 0;
            }
            Outings.Clear();
        }

        public void RememberOuting(int person, int day, int minute, int district)
        {
            while (Outings.Count >= OutingsMost)
            {
                Outings.RemoveAt(0);
            }
            Outings.Add(new LoveOuting { Person = person, Day = day, Minute = minute, District = district });
        }

        public void RememberHook(LoveHook hook)
        {
            while (Hooks.Count >= HooksMost)
            {
                Hooks.RemoveAt(0);
            }
            Hooks.Add(hook);
        }

        public LoveHook LastHook(int person)
        {
            for (int i = Hooks.Count - 1; i >= 0; i--)
            {
                if (Hooks[i].Person == person)
                {
                    return Hooks[i];
                }
            }
            return null;
        }

        public LoveBaby BabyOf(int person)
        {
            for (int i = 0; i < Babies.Count; i++)
            {
                if (Babies[i].Person == person)
                {
                    return Babies[i];
                }
            }
            return null;
        }
    }
}
