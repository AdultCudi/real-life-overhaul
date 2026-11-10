using System.Collections.Generic;

namespace RealLifeOverhaul.Schemes
{
    /// <summary>One act on a trail: what was done, when, what it brought, and how plainly it can be read.</summary>
    public sealed class SchemeAct
    {
        public SchemeKind Kind;
        public int Day;
        public long Gain;                      // what it brought the player, whole money: what is taken back if it is found
        public long Back;                      // of that, what was paid or taken back already
        public long Paid;                      // what the player paid out for it (a favour's price): what a penalty is worked out on
        public int Size;                       // thousandths of the most that is plausible
        public int Knows;                      // people who know of it
        public bool Paper;                     // it went through the bank account
        public int Inquiry;                    // the inquiry that found it, 0 for none: found once, never again
        public int Ref;                        // what it is about: a claim's id, a favour, a sale's day
        public string Place = string.Empty;    // the business it was done at, as the company writes a place; empty for none

        /// <summary>What an inquiry would still take back.</summary>
        public long Owed => Gain > Back ? Gain - Back : 0L;
    }

    /// <summary>One inquiry into one kind of scheme, from its quiet opening to its end.</summary>
    public sealed class SchemeInquiry
    {
        public int Id;
        public SchemeKind Kind;
        public int Opened;                     // the day
        public InquiryStage Stage;
        public int Evidence;                   // thousandths
        public bool Lost;                      // the investigator was bought: part of the file is lost
        public bool Asked;                     // the investigator was offered money once, whatever came of it
        public bool Coop;                      // the player told them everything
        public int Pressed;                    // evidence added by people who talked or reported
        public int Case;                       // the court's number once charged; 0 for none
        public InquiryEnd End;
        public int Closed;                     // the day it ended; 0 while it runs
        public long Found;                     // what was taken back, whole money
        public long Surcharge;                 // ...and on top of it
        public long Repaid;                    // what the player paid back under this inquiry, whole money: it carries half the surcharge

        public bool Open => Stage != InquiryStage.Closed;
        public bool Known => Stage >= InquiryStage.Letter;
    }

    /// <summary>Somebody who was paid: for as long as they hold what they know, they can talk.</summary>
    public sealed class SchemePaid
    {
        public string Who = string.Empty;      // their id: a contact, a councillor's seat, an inquiry's investigator, a case's judge
        public int Day;
        public long Amount;
        public Favour What;
        public bool Talked;
    }

    /// <summary>A business's own settings and its window at the bank.</summary>
    public sealed class SchemeShop
    {
        public string Place = string.Empty;
        public int Lift;                       // thousandths the books show over the truth
        public int Wash;                       // thousandths of an average day's takings washed a day
        public long Window;                    // washed since WindowFrom
        public int WindowFrom;                 // the day the bank's window began
        public bool Flagged;                   // the window's pattern was reported
        public int ClubDay = -1000;            // the day of the last club sold from here
        public int ClaimDay = -1000;           // ...and of the last invented claim
    }

    /// <summary>A rival tycoon's own trail, kept by the same rules as the player's.</summary>
    public sealed class SchemeRival
    {
        public int Heat;
        public int Acts;                       // schemes on file
        public int LastAct = -1000;            // the day of the newest
        public long Gained;                    // what they brought, still to be found
        public int Size;                       // the largest on file, thousandths
        public int Opened;                     // inquiries opened so far
        public InquiryStage Stage = InquiryStage.Closed;
        public int Since;                      // the day the open inquiry began
        public int Evidence;
        public int Convictions;
        public int TrialDay;                   // the day their case is heard; 0 for none
        public long Found;                     // what the last findings took back
        public int NewsDay = -1000;            // the day of the last thing the paper prints of them
        public int News;                       // ...and what it was (SchemeRules.RivalNews)
        public SchemeKind NewsKind;
        public long NewsAmount;
        public long Owes;                      // what a sham fund of theirs took from the player: it comes back when they are found out
    }

    /// <summary>A rival's fund offered to the player: honest or a sham, fixed by the seed when it is made.</summary>
    public sealed class SchemeFund
    {
        public int Tycoon = -1;
        public FundState State;
        public int Day;                        // offered
        public int Due;                        // the day it pays, or the last day to answer while open
        public long Amount;
        public bool Sham;
        public bool Checked;                   // the player had it looked at
        public bool Reported;                  // the player reported the sham to the police
        public int Number;                     // offers made so far: the next one's draw
    }

    /// <summary>
    /// Everything the schemes remember in a save: the trails and their heat, the inquiries, the cash in hand, the people
    /// paid and those who said no, each business's settings, the rivals' own trails and the fund on offer. Game-free.
    /// </summary>
    public sealed class SchemeState
    {
        public const int Rivals = 3;           // as many as the rival tycoons (Rivals/RivalCatalogue.TycoonCount)

        public int Seed;                       // from the save's own seed: every draw comes from it
        public int StartDay;
        public int Day = -1;                   // the last day played; -1 for a new file
        public int NextInquiry = 1;
        public long Dirty;                     // cash in hand that no bank has seen
        public long ClubTaken;                 // what the clubs took from households, on file
        public long ClubBack;                  // ...and what went back to them
        public int ExpenseDay = -1000;         // the day of the last invented expense
        public int Acted;                      // acts so far: each one's own draw
        public bool Watched;                   // the cash in hand is watched: none of it moves
        public int Cautioned;                  // a bit for each kind an inquiry ended with a caution for: the next file of that kind is charged
        public readonly int[] Heat = new int[SchemeCatalogue.Kinds];
        public readonly int[] Inquiries = new int[SchemeCatalogue.Kinds];   // inquiries opened so far, by kind
        public readonly List<SchemeAct> Acts = new List<SchemeAct>(32);
        public readonly List<SchemeInquiry> Cases = new List<SchemeInquiry>(8);
        public readonly List<SchemePaid> Paid = new List<SchemePaid>(8);
        public readonly List<string> Cold = new List<string>(8);            // those who said no and remember
        public readonly List<int> Bought = new List<int>(8);                // contacts whose next favour is bought
        public readonly List<SchemeShop> Shops = new List<SchemeShop>(8);
        public readonly SchemeRival[] Rival = new SchemeRival[Rivals];
        public readonly SchemeFund Fund = new SchemeFund();

        public SchemeState()
        {
            for (int i = 0; i < Rival.Length; i++)
            {
                Rival[i] = new SchemeRival();
            }
        }

        /// <summary>The business's settings, made if asked for; null for a place not kept.</summary>
        public SchemeShop Shop(string place, bool make)
        {
            if (string.IsNullOrEmpty(place))
            {
                return null;
            }
            for (int i = 0; i < Shops.Count; i++)
            {
                if (Shops[i].Place == place)
                {
                    return Shops[i];
                }
            }
            if (!make)
            {
                return null;
            }
            var shop = new SchemeShop { Place = place };
            Shops.Add(shop);
            return shop;
        }

        /// <summary>The open inquiry into a kind, or null: never two at once.</summary>
        public SchemeInquiry OpenOf(SchemeKind kind)
        {
            for (int i = 0; i < Cases.Count; i++)
            {
                if (Cases[i].Open && Cases[i].Kind == kind)
                {
                    return Cases[i];
                }
            }
            return null;
        }

        public SchemeInquiry Inquiry(int id)
        {
            for (int i = 0; i < Cases.Count; i++)
            {
                if (Cases[i].Id == id)
                {
                    return Cases[i];
                }
            }
            return null;
        }

        /// <summary>True while anything still runs its course with the option off: an inquiry, money placed, cash in hand or a setting still on.</summary>
        public bool CarriesOn
        {
            get
            {
                for (int i = 0; i < Cases.Count; i++)
                {
                    if (Cases[i].Open)
                    {
                        return true;
                    }
                }
                for (int i = 0; i < Rival.Length; i++)
                {
                    if (Rival[i].Stage != InquiryStage.Closed || Rival[i].TrialDay > 0 || (Rival[i].Owes > 0 && Rival[i].Heat > 0))
                    {
                        return true;
                    }
                }
                return Fund.State == FundState.Placed || Fund.State == FundState.Lost || Paid.Count > 0 || Acts.Count > 0;
            }
        }
    }
}
