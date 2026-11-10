using System;

namespace RealLifeOverhaul.Parties
{
    /// <summary>
    /// The rules of a party with friends, game-free: time as the game counts it, the seeded draws, the cap that refills over a
    /// window (<see cref="Lift"/>), what a party costs, who it holds, what somebody answers and why, whether they turn up, how
    /// it went and what it is worth to each who came. Whole numbers and the save's own seed only, so every machine reads the
    /// same. Nothing here allocates.
    /// </summary>
    public static class PartyRules
    {
        public const int PurposeAnswer = 811, PurposeFar = 812, PurposeRough = 813, PurposeShow = 814, PurposeInvite = 815, PurposeHost = 816;
        public const int PurposeStaff = 821, PurposeRivalKind = 822, PurposeRivalWhen = 823, PurposeInvitePlace = 824;

        /// <summary>The game minute a day's hour starts at, as the game's own clock counts (day x 1440 + hour x 60).</summary>
        public static int Stamp(int day, int hour) => day * 1440 + hour * 60;

        /// <summary>Saturday or Sunday, as the game names its days: day 1 is a Monday.</summary>
        public static bool Weekend(int day) => day >= 1 && (day - 1) % 7 >= 5;

        /// <summary>A number from 0 to 999, always the same for the same save, purpose and two subjects.</summary>
        public static int Roll(int seed, int purpose, int a, int b)
        {
            unchecked
            {
                uint h = 2166136261u;
                h = (h ^ (uint)seed) * 16777619u;
                h = (h ^ (uint)purpose) * 16777619u;
                h = (h ^ (uint)a) * 16777619u;
                h = (h ^ (uint)b) * 16777619u;
                h ^= h >> 15;
                h *= 2246822519u;
                h ^= h >> 13;
                return (int)(h % 1000u);
            }
        }

        /// <summary>A text as a number for <see cref="Roll"/>: the same on every machine (never string.GetHashCode).</summary>
        public static int Hash(string text)
        {
            unchecked
            {
                uint h = 2166136261u;
                for (int i = 0; text != null && i < text.Length; i++)
                {
                    h = (h ^ text[i]) * 16777619u;
                }
                return (int)h;
            }
        }

        // ------------------------------------------------------------------------------------------------- the cap

        /// <summary>What is still counted of a lift on a day, per mille of the cap: it leaks evenly over the window, and a clock set back refills nothing.</summary>
        public static int Left(Lift lift, int day, int window)
        {
            if (lift == null || lift.Used <= 0)
            {
                return 0;
            }
            int used = Math.Min(1000, lift.Used), days = day - lift.Day;
            if (days <= 0)
            {
                return used;
            }
            long leaked = (long)days * 1000 / Math.Max(1, window);
            return leaked >= used ? 0 : used - (int)leaked;
        }

        /// <summary>
        /// Gives what there is room for of a worth (per mille of the cap) and returns it: never more than the cap less what is
        /// still counted, however many parties or events ask and however the money was split.
        /// </summary>
        public static int Give(Lift lift, int day, int window, int worth)
        {
            int left = Left(lift, day, window);
            int gain = PartyCatalogue.Clamp(worth, 0, 1000 - left);
            lift.Used = left + gain;
            lift.Day = day;
            return gain;
        }

        // ------------------------------------------------------------------------------------------------- money and room

        /// <summary>The food and drink a head at a place, at a price level: at a venue of the player's own the goods at cost.</summary>
        public static int HeadCost(PartyTier tier, PartyPlace place, bool own, int level)
        {
            int share = own ? PartyCatalogue.OwnShare : PartyCatalogue.PlaceShare[PartyCatalogue.Clamp((int)place, 0, PartyCatalogue.PlaceShare.Length - 1)];
            return Math.Max(1, PriceRules.At(PartyCatalogue.HeadBase(tier) * share / 100, level));
        }

        /// <summary>What a party costs: a head for everybody who said yes and for the host.</summary>
        public static int Cost(PartyTier tier, PartyPlace place, bool own, int level, int coming)
        {
            return HeadCost(tier, place, own, level) * (Math.Max(0, coming) + 1);
        }

        /// <summary>Guests a place holds, by its floor: a guest to <see cref="PartyCatalogue.SqmPerGuest"/> square metres, within the place's bounds.</summary>
        public static int Capacity(PartyPlace place, int sqm)
        {
            int most = PartyCatalogue.GuestsMost[PartyCatalogue.Clamp((int)place, 0, PartyCatalogue.GuestsMost.Length - 1)];
            return sqm <= 0 ? PartyCatalogue.GuestsLeast : PartyCatalogue.Clamp(sqm / PartyCatalogue.SqmPerGuest, PartyCatalogue.GuestsLeast, most);
        }

        /// <summary>What comes back when the host calls a party or an event off so long before its start, per cent of what was paid.</summary>
        public static int RefundShare(int start, int now)
        {
            int ahead = start - now;
            return ahead >= PartyCatalogue.EarlyHours * 60 ? PartyCatalogue.RefundEarly : ahead >= PartyCatalogue.LeadHours * 60 ? PartyCatalogue.RefundLate
                : PartyCatalogue.RefundLast;
        }

        public static int Refund(int paid, int start, int now) => (int)((long)Math.Max(0, paid) * RefundShare(start, now) / 100);

        // ------------------------------------------------------------------------------------------------- who comes

        /// <summary>A quarrel that keeps somebody away: stood up, snapped at, their money kept or a break-up, in the last days.</summary>
        public static bool Quarrelled(SocialPerson p, int today)
        {
            for (int i = 0; i < p.Memories.Count; i++)
            {
                MemoryCode code = p.Memories[i].Code;
                if ((code == MemoryCode.StoodUp || code == MemoryCode.Rude || code == MemoryCode.NotRepaid || code == MemoryCode.BreakUp)
                    && today - p.Memories[i].Day <= PartyCatalogue.QuarrelDays)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>
        /// What somebody answers to an invitation, by the first reason that holds, from what the social life keeps of them:
        /// how close they are, a quarrel, their sleep and working hours, the district, the day they are having, and a draw
        /// for something else on. The same for a save, a party and a person.
        /// </summary>
        public static PartyAnswer Answer(int seed, int party, SocialPerson p, PartyPlace place, int day, int hour, int today, int district)
        {
            if (p == null || !p.Met || p.Drifted)
            {
                return PartyAnswer.NotClose;
            }
            if (Quarrelled(p, today))
            {
                return PartyAnswer.Quarrel;
            }
            bool close = p.IsFamily || p.Together || p.Stage >= SocialStage.Friend;
            if (!close && p.Closeness < (place == PartyPlace.Home ? PartyCatalogue.HomeCloseness : PartyCatalogue.VenueCloseness))
            {
                return PartyAnswer.NotClose;
            }
            if (p.Asleep(hour))
            {
                return PartyAnswer.Asleep;
            }
            if (!p.FreeAt(hour, Weekend(day)))
            {
                return PartyAnswer.Working;
            }
            bool away = district >= 0 && p.District != district;
            if (away && !p.MovedIn && (p.Has(SocialTrait.Homebody) || (!close && Roll(seed, PurposeFar, party, p.Index) < PartyCatalogue.FarChance)))
            {
                return PartyAnswer.Far;
            }
            if (p.Mood <= -2 && !p.IsFamily && Roll(seed, PurposeRough, party, p.Index) < PartyCatalogue.RoughChance)
            {
                return PartyAnswer.RoughDay;
            }
            if (!p.Has(SocialTrait.Reliable) && !p.IsFamily && !p.Together
                && Roll(seed, PurposeAnswer, party, p.Index) < (p.Has(SocialTrait.Flaky) ? PartyCatalogue.BusyFlaky : PartyCatalogue.BusyChance))
            {
                return PartyAnswer.Busy;
            }
            return PartyAnswer.Yes;
        }

        /// <summary>Whether somebody who said yes turns up, by the odds the social life cancels any plan with (never the reliable), drawn once a party.</summary>
        public static bool ShowsUp(int seed, int party, SocialPerson p, PartyPlace place)
        {
            if (p == null || !p.Met)
            {
                return false;
            }
            int odds = (int)Math.Round(SocialRules.CancelOdds(p, PartyCatalogue.KindOf(place), false) * 1000f);
            return Roll(seed, PurposeShow, party, p.Index) >= odds;
        }

        // ------------------------------------------------------------------------------------------------- how it went

        /// <summary>
        /// A party's quality, 0 to 100: its tier, how many of those who said yes came, the home it was held in (tidiness,
        /// power, water; a venue has none of these), and a host who came late or was gone at the end.
        /// </summary>
        public static int Quality(PartyTier tier, PartyPlace place, int came, int coming, HomeFacts home, bool late, bool left)
        {
            int q = PartyCatalogue.TierQuality[PartyCatalogue.Clamp((int)tier, 0, PartyCatalogue.TierQuality.Length - 1)];
            if (coming > 0)
            {
                int turnout = came * 100 / coming;
                q += turnout >= PartyCatalogue.TurnoutGood ? PartyCatalogue.TurnoutBonus : turnout < PartyCatalogue.TurnoutPoor ? PartyCatalogue.TurnoutPenalty : 0;
            }
            if (place == PartyPlace.Home)
            {
                if (home.Tidy >= 0)
                {
                    q += home.Tidy >= PartyCatalogue.TidyGood ? PartyCatalogue.TidyBonus : home.Tidy < PartyCatalogue.TidyPoor ? PartyCatalogue.TidyPenalty : 0;
                }
                q += home.Dark ? PartyCatalogue.DarkPenalty : 0;
                q += home.Dry ? PartyCatalogue.DryPenalty : 0;
            }
            q += late ? PartyCatalogue.LatePenalty : 0;
            q += left ? PartyCatalogue.LeftPenalty : 0;
            return PartyCatalogue.Clamp(q, 0, 100);
        }

        /// <summary>
        /// What a party is worth to one who came, per mille of the week's cap: its tier, how it went and how much it is their
        /// kind of thing (the social life's own liking, 0.7 to 1.4). A bad party is worth nothing.
        /// </summary>
        public static int Worth(PartyTier tier, int quality, SocialPerson p, PartyPlace place)
        {
            if (quality < PartyCatalogue.BadBelow)
            {
                return 0;
            }
            int liking = (int)Math.Round(SocialRules.Liking(p, PartyCatalogue.KindOf(place)) * 1000f);
            long worth = (long)PartyCatalogue.TierWorth[PartyCatalogue.Clamp((int)tier, 0, PartyCatalogue.TierWorth.Length - 1)] * quality / 100 * liking / 1000;
            return (int)Math.Min(1000L, worth);
        }

        /// <summary>
        /// A share of the cap as points for one figure, never past the ceiling parties stop at: what is already at or over it
        /// gains nothing, so no number of parties makes a close friend or a partner by itself.
        /// </summary>
        public static float Points(float cap, int gain, float now)
        {
            float points = cap * gain / 1000f;
            float room = PartyCatalogue.Ceiling - now;
            return room <= 0f ? 0f : Math.Min(points, room);
        }
    }
}
