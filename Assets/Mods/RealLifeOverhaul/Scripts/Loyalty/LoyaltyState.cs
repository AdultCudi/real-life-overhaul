using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Loyalty
{
    /// <summary>One day of a scheme as the page adds a week up from: what the members did and what the cards cost and brought.</summary>
    public struct LoyaltyDay
    {
        public int Day;          // the game day these figures are of; 0 for none
        public int Visits;       // customers served who were members
        public int Signups;      // new members that day
        public int Lapsed;       // members who fell away that day
        public long Rewards;     // given back to members, cents
        public long Costs;       // the card's running fee and the sign-up push, cents
        public long Extra;       // takings the scheme brought (more visits, fuller baskets), cents
        public long Margin;      // what the shop kept of those takings after the goods' cost, cents
    }

    /// <summary>
    /// A business's loyalty scheme as the save keeps it. What the player chooses (<see cref="Want"/>, <see cref="NextRate"/>,
    /// <see cref="NextOffer"/>) comes into force at the next day's turn, so a day's lift and the rewards paid for it are always of
    /// one setting. Members and the customer base are kept in hundredths, so a small shop's scheme still grows. Game-free.
    /// </summary>
    public sealed class LoyaltyFile
    {
        public const int NoDay = -1;     // no members' day

        public string Place = string.Empty;
        public bool On;                  // the scheme runs today
        public bool Want;                // it runs from tomorrow morning (the player's switch)
        public int Rate = LoyaltyRules.RateStart, NextRate = LoyaltyRules.RateStart;   // the reward rate's step, in force and chosen
        public int Push;                 // the sign-up push's step
        public int OfferDay = NoDay, NextOffer = NoDay;   // the members' day (0 Monday to 6 Sunday), in force and chosen
        public int OfferAsked = NoDay;   // the members' day its notice is running for
        public int OfferFrom;            // the day that one comes into force; 0 while none is waiting
        public int Since;                // the day it was last switched on; 0 for never
        public int Members;              // members, hundredths
        public int Base;                 // customers served a day on average, hundredths
        public int LastDay;              // the last day's turn worked for it
        public int Lift;                 // per cent of customers asked for today
        public int Basket;               // per mille of today's customers who take one more product
        public int OpenHours, BadHours;  // today so far: hours it was meant to be open, and those of them it could serve nobody
        public int Added;                // products the scheme put in baskets today
        public int Carried;              // per cent of customers the lift really added to the figure written today
        public long Paid;                // everything the scheme has cost, whole money
        public readonly LoyaltyDay[] Week = new LoyaltyDay[LoyaltyRules.WeekDays];   // the last days, by the day's number

        // Worked out each morning, not saved.
        public string Chain = string.Empty;   // the brand its card is shared with; empty for a card of its own
        public int Sisters;                   // other shops of that brand whose scheme runs
        public int Rivalry;                   // the strength of rival schemes of its kind in its district, per mille
        public int Rivals, RivalsRunning;     // shops of its kind in its district that are not the player's, and those running a card
        public int MarginMille = LoyaltyRules.MarginUnknown;   // what it keeps of its takings after the goods' cost, per mille
        public int Written;                   // the lift last written into the game's figure, per cent
    }

    /// <summary>A shop of another owner as the scheme's rules see it: a rival's or the city's, of the same kind in the same district.</summary>
    public struct LoyaltyRival
    {
        public int Seed;         // from its address: the same on every machine and every day
        public int Quality;      // how well it serves, 0 to 100: the skill of its crew, since the game keeps a satisfaction only for the player's shops
        public int Days;         // days it has traded
        public bool Tycoon;      // a rival's business, not the city's
    }

    /// <summary>What a day's turn read of a business for its scheme: yesterday as the game booked it.</summary>
    public struct LoyaltyInput
    {
        public int Day, Weekday;             // yesterday's number, and its day of the week (0 Monday)
        public int Served;                   // customers who paid
        public int Items;                    // products sold
        public long Takings, Goods;          // cents: what they paid, and what the goods cost
        public int Satisfaction, Pricing, Clean;   // the game's own figures, 0 to 100
    }

    /// <summary>What a day's turn came to for a scheme.</summary>
    public struct LoyaltyResult
    {
        public int Visits, Signups, Lapsed;  // members' visits, new members and members lost, whole
        public long Rewards, Costs, Extra, Margin;   // cents
    }

    /// <summary>Why a press on the Loyalty page did nothing.</summary>
    public enum LoyaltyRefusal
    {
        None = 0,
        Off = 1,         // the feature is off or not ready
        NotShop = 2,     // not a business of the player's that takes money
        Cash = 3,        // the set-up or the change cannot be paid
        Full = 4         // the save holds as many schemes as it can
    }

    /// <summary>Everything the feature keeps in a save.</summary>
    public sealed class LoyaltyState
    {
        public readonly List<LoyaltyFile> Files = new List<LoyaltyFile>(8);

        public LoyaltyFile File(string place)
        {
            for (int i = 0; i < Files.Count; i++)
            {
                if (string.Equals(Files[i].Place, place, StringComparison.Ordinal))
                {
                    return Files[i];
                }
            }
            return null;
        }
    }
}
