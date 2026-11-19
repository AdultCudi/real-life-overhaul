using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy
{
    /// <summary>Why a pitch for an offer was refused, or that it was taken. Never saved.</summary>
    public enum PitchResult { Pitched = 0, NoOffer = 1, Already = 2, NoAgents = 3, NoHours = 4, Full = 5, NoAgency = 6 }

    /// <summary>
    /// The agency's book of other owners' blocks by rule: landlords call for pitches, rival agencies of the save pitch too, the owner chooses, and a block held is run by
    /// the same measures a landlord's building is (satisfaction, repair calls, the city's inspection) with the agents' hours as the only means. Game-free and seeded:
    /// every draw is a <see cref="EstateRules.Roll"/> of the save's seed, so a save always gives the same history, and nothing here moves money (the director does, from
    /// the events, once for each). Cost: a few dozen rolls a day for at most <see cref="AgencyBookCatalogue.BlocksMost"/> blocks; nothing allocated but an event.
    /// </summary>
    public static class AgencyBookRules
    {
        private const int ForOffer = 7401, ForDistrict = 7402, ForOwner = 7403, ForUnits = 7404, ForSqm = 7405, ForFee = 7406, ForStandard = 7407, ForCond = 7408, ForLet = 7409;
        private const int ForRival = 7410, ForBidShare = 7411, ForBidPct = 7412, ForAgency = 7413, ForCity = 7414, ForInspectFirst = 7415;
        private const int ForCall = 7416, ForAnswer = 7417, ForEnd = 7418, ForRenew = 7419, ForRelet = 7420, ForHome = 7421;

        // ------------------------------------------------------------------------------------------------- the rival agencies

        /// <summary>Whether a rival of the save who is a landlord runs an agency: a trait of the rival, fixed by the save's seed.</summary>
        public static bool RunsAgency(int seed, string rival)
        {
            return !string.IsNullOrEmpty(rival) && EstateRules.Roll(seed, ForAgency, EstateRules.Hash(rival), 0) < AgencyBookCatalogue.RivalAgencyShare;
        }

        /// <summary>The skill of a rival agency's agents, 0 to 100: more for a landlord that holds more.</summary>
        public static int RivalSkill(int units)
        {
            return EstateCatalogue.Clamp(AgencyBookCatalogue.RivalSkillLeast + Math.Max(0, units) / AgencyBookCatalogue.RivalUnitsPerPoint,
                AgencyBookCatalogue.RivalSkillLeast, AgencyBookCatalogue.RivalSkillMost);
        }

        /// <summary>How a pitch is weighed: the agents' skill counts double a point of the fee, the district's reputation adds a little, a dearer fee takes away.</summary>
        public static int Score(int skill, int reputation, int pct)
        {
            return EstateCatalogue.Clamp(skill, 0, 100) * AgencyBookCatalogue.SkillWeight + EstateCatalogue.Clamp(reputation, 0, 1000) / AgencyBookCatalogue.RepDivisor - pct;
        }

        /// <summary>
        /// The rival agencies that pitch for an offer, put into its bids: each rival landlord of the save who runs an agency and is not the block's owner, in the order of their
        /// ids (the game's own order is nobody's rule), each pitching on a draw of the save's seed, the owner and the block, at most <see cref="AgencyBookCatalogue.RivalBidsMost"/>.
        /// </summary>
        public static int Bid(int seed, ManagedBlock offer, IList<AgencyLandlord> landlords)
        {
            offer.Bids.Clear();
            if (landlords == null)
            {
                return 0;
            }
            var order = new List<AgencyLandlord>(landlords.Count);
            for (int i = 0; i < landlords.Count; i++)
            {
                if (!string.IsNullOrEmpty(landlords[i].Id))
                {
                    order.Add(landlords[i]);
                }
            }
            order.Sort((a, b) => string.CompareOrdinal(a.Id, b.Id));
            for (int i = 0; i < order.Count && offer.Bids.Count < AgencyBookCatalogue.RivalBidsMost; i++)
            {
                AgencyLandlord l = order[i];
                int who = EstateRules.Hash(l.Id);
                if (string.Equals(l.Id, offer.Owner, StringComparison.Ordinal) || !RunsAgency(seed, l.Id)
                    || EstateRules.Roll(seed, ForBidShare, who, offer.Id) >= AgencyBookCatalogue.RivalBidShare)
                {
                    continue;
                }
                offer.Bids.Add(new AgencyBid
                {
                    Rival = l.Id, Skill = RivalSkill(l.Units),
                    Pct = EstateRules.Between(EstateRules.Roll(seed, ForBidPct, who, offer.Id), AgencyBookCatalogue.BidPctLeast, AgencyBookCatalogue.BidPctMost)
                });
            }
            return offer.Bids.Count;
        }

        /// <summary>The rival agency whose pitch beats the player's (or the best of them when the player did not pitch), or -1. A tie is the player's.</summary>
        public static int Winner(ManagedBlock offer, int skill, int reputation)
        {
            int best = -1, bestScore = offer.Pitched ? Score(skill, reputation, offer.FeePct) : int.MinValue;
            for (int i = 0; i < offer.Bids.Count; i++)
            {
                int score = Score(offer.Bids[i].Skill, 0, offer.Bids[i].Pct);
                if (score > bestScore)
                {
                    best = i;
                    bestScore = score;
                }
            }
            return best;
        }

        // ------------------------------------------------------------------------------------------------- offers

        /// <summary>A flat's rent a day at the agreed rate: a year's yield of its value at the district's prices and the market's level, over the game's year.</summary>
        public static int FlatRentDay(int sqm, int sqmPrice, int real, int index, int daysPerYear)
        {
            long value = (long)sqm * Math.Max(0, sqmPrice) * EstateCatalogue.Clamp(real, EstateCatalogue.RealLeast, EstateCatalogue.RealMost) / 1000 * EstateCatalogue.Clamp(index, 500, 4000) / 1000;
            long year = EstateCatalogue.Share(Math.Min(int.MaxValue / 4, value), EstateCatalogue.AgencyYield);
            return (int)Math.Max(1, year / Math.Max(1, daysPerYear));
        }

        /// <summary>The hours a day a block takes from the agents, tenths.</summary>
        public static int Hours(int units)
        {
            return Math.Max(0, units) * AgencyBookCatalogue.HoursPerUnit;
        }

        /// <summary>A landlord of the city calls for pitches today, or null: never with no office, no district known, the offers or the blocks at their most, or too soon after the last.</summary>
        public static ManagedBlock Draw(AgencyFile agency, IList<AgencyLandlord> landlords, int seed, int day, EstateClimate c, int real)
        {
            AgencyBook book = agency.Book;
            if (!agency.Running || day - book.LastOfferDay < AgencyBookCatalogue.OfferEveryDays || book.Count(BlockStage.Offered) >= AgencyBookCatalogue.OffersMost
                || book.Count(BlockStage.Held) >= AgencyBookCatalogue.BlocksMost)
            {
                return null;
            }
            book.LastOfferDay = day;
            var districts = new List<DistrictRep>(agency.Districts.Count);
            for (int i = 0; i < agency.Districts.Count; i++)
            {
                if (agency.Districts[i].SqmPrice > 0)
                {
                    districts.Add(agency.Districts[i]);
                }
            }
            if (districts.Count == 0)
            {
                return null;
            }
            int n = book.NextId;
            string owner = OwnerFor(seed, day, n, landlords);
            string place;
            DistrictRep d = HomeFor(agency, districts, landlords, owner, seed, day, n, out place)
                            ?? districts[EstateRules.Between(EstateRules.Roll(seed, day, ForDistrict, n), 0, districts.Count - 1)];
            if (book.OfBlock(owner, d.Name) != null)
            {
                return null;   // the same owner never asks twice for the same district: tomorrow's draw is another
            }
            book.NextId++;
            int sqm = EstateRules.Between(EstateRules.Roll(seed, day, ForSqm, n), AgencyBookCatalogue.FlatSqmLeast, AgencyBookCatalogue.FlatSqmMost);
            var offer = new ManagedBlock
            {
                Id = n, Owner = owner, District = d.Name, Place = place, Stage = (int)BlockStage.Offered,
                Units = EstateRules.Between(EstateRules.Roll(seed, day, ForUnits, n), AgencyBookCatalogue.UnitsLeast, AgencyBookCatalogue.UnitsMost),
                Sqm = sqm, RentDay = FlatRentDay(sqm, d.SqmPrice, real, c.Index, c.DaysPerYear),
                FeeAsk = EstateRules.Between(EstateRules.Roll(seed, day, ForFee, n), AgencyBookCatalogue.FeeAskLeast, AgencyBookCatalogue.FeeAskMost),
                Standard = AgencyBookCatalogue.Standards[EstateRules.Between(EstateRules.Roll(seed, day, ForStandard, n), 0, AgencyBookCatalogue.Standards.Length - 1)],
                Until = day + AgencyBookCatalogue.OfferOpenDays,
                Cond = EstateRules.Between(EstateRules.Roll(seed, day, ForCond, n), AgencyBookCatalogue.StartCondLeast, AgencyBookCatalogue.StartCondMost)
            };
            offer.Let = Math.Min(offer.Units, offer.Units * EstateRules.Between(EstateRules.Roll(seed, day, ForLet, n), AgencyBookCatalogue.StartLetLeast, AgencyBookCatalogue.StartLetMost) / 1000);
            Bid(seed, offer, landlords);
            book.Blocks.Add(offer);
            return offer;
        }

        // A rival owner who really owns residential buildings in the save has the block tied to one of them, by a seeded pick among those in a district the agency knows and not already
        // tied to a block; null (and no place) for a landlord of the city, or a rival with none.
        private static DistrictRep HomeFor(AgencyFile agency, List<DistrictRep> districts, IList<AgencyLandlord> landlords, string owner, int seed, int day, int n, out string place)
        {
            place = string.Empty;
            for (int i = 0; landlords != null && i < landlords.Count; i++)
            {
                if (!string.Equals(landlords[i].Id, owner, StringComparison.Ordinal) || landlords[i].Homes == null)
                {
                    continue;
                }
                var fit = new List<RentHome>(landlords[i].Homes.Count);
                for (int h = 0; h < landlords[i].Homes.Count; h++)
                {
                    RentHome home = landlords[i].Homes[h];
                    if (!TiedTo(agency.Book, home.Place) && districts.Exists(x => x.Name == home.District))
                    {
                        fit.Add(home);
                    }
                }
                if (fit.Count == 0)
                {
                    return null;
                }
                RentHome pick = fit[EstateRules.Between(EstateRules.Roll(seed, day, ForHome, n), 0, fit.Count - 1)];
                place = pick.Place;
                return districts.Find(x => x.Name == pick.District);
            }
            return null;
        }

        private static bool TiedTo(AgencyBook book, string place)
        {
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                if (book.Blocks[i].Stage != (int)BlockStage.Lost && book.Blocks[i].Place == place)
                {
                    return true;
                }
            }
            return false;
        }

        // A rival of the save who is a landlord asks on a draw, else a landlord of the city; with no rival landlord the city's alone.
        private static string OwnerFor(int seed, int day, int n, IList<AgencyLandlord> landlords)
        {
            if (landlords != null && landlords.Count > 0 && EstateRules.Roll(seed, day, ForOwner, n) < AgencyBookCatalogue.OwnerRivalShare)
            {
                var ids = new List<string>(landlords.Count);
                for (int i = 0; i < landlords.Count; i++)
                {
                    if (!string.IsNullOrEmpty(landlords[i].Id))
                    {
                        ids.Add(landlords[i].Id);
                    }
                }
                if (ids.Count > 0)
                {
                    ids.Sort(string.CompareOrdinal);
                    return ids[EstateRules.Between(EstateRules.Roll(seed, day, ForRival, n), 0, ids.Count - 1)];
                }
            }
            return CityOwner(EstateRules.Between(EstateRules.Roll(seed, day, ForCity, n), 0, AgencyBookCatalogue.CityOwners - 1));
        }

        public const string CityPrefix = "city:";

        public static string CityOwner(int number)
        {
            return CityPrefix + EstateCatalogue.Clamp(number, 0, AgencyBookCatalogue.CityOwners - 1).ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        /// <summary>The number of a landlord of the city, or -1 for an owner that is a rival of the game.</summary>
        public static int CityNumber(string owner)
        {
            int n;
            return owner != null && owner.StartsWith(CityPrefix, StringComparison.Ordinal)
                   && int.TryParse(owner.Substring(CityPrefix.Length), System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out n)
                ? EstateCatalogue.Clamp(n, 0, AgencyBookCatalogue.CityOwners - 1) : -1;
        }

        /// <summary>The hours the agents have to spare today for a new block, tenths.</summary>
        public static int Spare(AgencyFile agency)
        {
            return Math.Max(0, agency.Capacity - agency.Reserved);
        }

        /// <summary>The player puts in a pitch for an offer, at the asking fee or undercutting it. Nothing is paid; the owner chooses on the offer's last day.</summary>
        public static PitchResult Pitch(AgencyFile agency, int id, bool undercut)
        {
            ManagedBlock offer = agency.Book.Find(id);
            if (!agency.Running)
            {
                return PitchResult.NoAgency;
            }
            if (offer == null || offer.Stage != (int)BlockStage.Offered)
            {
                return PitchResult.NoOffer;
            }
            if (offer.Pitched)
            {
                return PitchResult.Already;
            }
            if (agency.Negotiators <= 0)
            {
                return PitchResult.NoAgents;
            }
            if (agency.Book.Count(BlockStage.Held) + PitchesOpen(agency.Book) >= AgencyBookCatalogue.BlocksMost)
            {
                return PitchResult.Full;
            }
            if (Hours(offer.Units) > Spare(agency) - PitchedHours(agency.Book))
            {
                return PitchResult.NoHours;
            }
            offer.Pitched = true;
            offer.FeePct = undercut ? AgencyBookCatalogue.UndercutPct : 100;
            return PitchResult.Pitched;
        }

        /// <summary>The player takes a pitch back before the day.</summary>
        public static bool Withdraw(AgencyBook book, int id)
        {
            ManagedBlock offer = book.Find(id);
            if (offer == null || offer.Stage != (int)BlockStage.Offered || !offer.Pitched)
            {
                return false;
            }
            offer.Pitched = false;
            offer.FeePct = 100;
            return true;
        }

        private static int PitchesOpen(AgencyBook book)
        {
            int n = 0;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                n += book.Blocks[i].Stage == (int)BlockStage.Offered && book.Blocks[i].Pitched ? 1 : 0;
            }
            return n;
        }

        private static int PitchedHours(AgencyBook book)
        {
            int hours = 0;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                hours += book.Blocks[i].Stage == (int)BlockStage.Offered && book.Blocks[i].Pitched ? Hours(book.Blocks[i].Units) : 0;
            }
            return hours;
        }

        /// <summary>The owner chooses on the offer's last day: the better pitch takes the block. Every outcome goes into <paramref name="events"/>.</summary>
        public static void Decide(AgencyFile agency, int day, List<BlockEvent> events)
        {
            AgencyBook book = agency.Book;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                ManagedBlock b = book.Blocks[i];
                if (b.Stage != (int)BlockStage.Offered || day < b.Until)
                {
                    continue;
                }
                DistrictRep d = agency.District(b.District);
                int rep = d != null ? d.Rep : 0;
                bool mine = b.Pitched && agency.Running && book.Count(BlockStage.Held) < AgencyBookCatalogue.BlocksMost;
                int rival = Winner(b, agency.Skill, rep);
                if (mine && rival < 0)
                {
                    b.Stage = (int)BlockStage.Held;
                    b.Until = day;
                    b.Happy = -1;
                    b.Served = book.Served;
                    b.Bids.Clear();
                    b.Strikes = b.Missed = 0;
                    b.NextInspect = day + EstateCatalogue.InspectEvery;
                    book.Won++;
                    Reputation(agency, b.District, AgencyBookCatalogue.RepWon);
                    events.Add(new BlockEvent { Kind = BlockEvent.Won, Id = b.Id });
                    continue;
                }
                string winner = rival >= 0 ? b.Bids[rival].Rival : string.Empty;
                if (b.Pitched)
                {
                    book.Lost++;
                    Reputation(agency, b.District, AgencyBookCatalogue.RepRival);
                    events.Add(new BlockEvent { Kind = BlockEvent.LostToRival, Id = b.Id, Name = rival >= 0 ? b.Bids[rival].Rival : string.Empty });
                }
                else
                {
                    events.Add(new BlockEvent { Kind = BlockEvent.Unwanted, Id = b.Id, Name = rival >= 0 ? b.Bids[rival].Rival : string.Empty });
                }
                Lose(book, b, rival >= 0 ? BlockWhy.Rival : BlockWhy.Nobody);
                b.Rival = winner;
            }
            Tidy(book);
        }

        private static void Reputation(AgencyFile agency, string district, int change)
        {
            AgencyRules.Reputation(agency, district, change);
        }

        private static void Lose(AgencyBook book, ManagedBlock b, BlockWhy why)
        {
            b.Stage = (int)BlockStage.Lost;
            b.Why = (int)why;
            b.Pitched = false;
            b.Bids.Clear();
            b.Calls.Clear();
            b.Collected = b.HappySum = b.HappyDays = 0;
        }

        /// <summary>Only the newest of the blocks lost are kept for the page.</summary>
        public static void Tidy(AgencyBook book)
        {
            int lost = book.Count(BlockStage.Lost);
            for (int i = 0; i < book.Blocks.Count && lost > AgencyBookCatalogue.LostKept; i++)
            {
                if (book.Blocks[i].Stage == (int)BlockStage.Lost)
                {
                    book.Blocks.RemoveAt(i);
                    i--;
                    lost--;
                }
            }
        }

        /// <summary>The player hands a held block back: nothing owed, a little reputation lost. False if it is not held.</summary>
        public static bool Resign(AgencyFile agency, int id)
        {
            ManagedBlock b = agency.Book.Find(id);
            if (b == null || !b.Held)
            {
                return false;
            }
            Lose(agency.Book, b, BlockWhy.Resigned);
            Reputation(agency, b.District, AgencyBookCatalogue.RepResigned);
            Tidy(agency.Book);
            return true;
        }

        /// <summary>Every held block lapses: the agency shut, or the part went off. Nothing is paid for the running month.</summary>
        public static void CloseAll(AgencyFile agency)
        {
            AgencyBook book = agency.Book;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                if (book.Blocks[i].Stage != (int)BlockStage.Lost)
                {
                    Lose(book, book.Blocks[i], BlockWhy.Resigned);
                }
            }
            Tidy(book);
        }

        // ------------------------------------------------------------------------------------------------- the hours

        /// <summary>
        /// The morning's allocation of the agents' hours: the player's own buildings under the agency first, then the blocks. What they cover, in thousandths, is how well
        /// every block is served today, and with under <see cref="AgencyBookCatalogue.OwnCoverLeast"/> the player's own buildings are left to themselves for the day.
        /// </summary>
        public static void Allocate(AgencyFile agency, int ownUnits)
        {
            AgencyBook book = agency.Book;
            int blocks = 0, own = Math.Max(0, ownUnits) * AgencyBookCatalogue.OwnUnitHours, capacity = Math.Max(0, agency.Capacity);
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                blocks += book.Blocks[i].Held ? Hours(book.Blocks[i].Units) : 0;
            }
            book.OwnUnits = Math.Max(0, ownUnits);
            book.Served = Cover(capacity, blocks + own);
            book.OwnPaused = own > 0 && book.Served < AgencyBookCatalogue.OwnCoverLeast;
            if (book.OwnPaused)
            {
                own = 0;
                book.Served = Cover(capacity, blocks);   // the buildings of his own wait, so the blocks have the hours
            }
            agency.Reserved = agency.Running ? Math.Min(capacity, blocks + own) : 0;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                book.Blocks[i].Served = book.Served;
            }
        }

        /// <summary>The share of the hours needed that the hours there are cover, in thousandths: all of them with none needed.</summary>
        public static int Cover(int capacity, int need)
        {
            return need <= 0 ? 1000 : (int)Math.Min(1000L, Math.Max(0, capacity) * 1000L / need);
        }

        // ------------------------------------------------------------------------------------------------- a block's day

        /// <summary>The satisfaction a block's tenants head for: the building as it stands, less the repairs waiting, less what the agents cannot cover.</summary>
        public static int Target(ManagedBlock b, int day)
        {
            long target = EstateCatalogue.HappyBase;
            target += (long)(EstateCatalogue.Clamp(b.Cond, 0, 1000) - EstateCatalogue.HappyCondFrom) * EstateCatalogue.HappyCondPer / 1000;
            int weight = 0;
            for (int i = 0; i < b.Calls.Count; i++)
            {
                int late = Math.Max(0, day - b.Calls[i] - EstateCatalogue.CallGraceDays);
                weight += EstateCatalogue.HappyCall + late * EstateCatalogue.HappyCallPerDay;
            }
            target -= Math.Min(EstateCatalogue.HappyCallMost, weight);
            target -= (1000 - EstateCatalogue.Clamp(b.Served, 0, 1000)) * AgencyBookCatalogue.ServiceSlope / 1000;
            return EstateCatalogue.Clamp((int)target, 0, 1000);
        }

        /// <summary>The chance, in thousandths, that an empty flat is let again today: with the market, and with the agents' hours covered.</summary>
        public static int ReletChance(int cycle, int served)
        {
            long chance = (long)AgencyBookCatalogue.ReletBase * EstateCatalogue.ApplicantCycle[EstateCatalogue.Clamp(cycle, 0, 3)] / 1000 * EstateCatalogue.Clamp(served, 0, 1000) / 1000;
            return (int)Math.Min(AgencyBookCatalogue.ReletMost, chance * EstateCatalogue.ManagedApplicants / 1000);
        }

        /// <summary>Repair calls waiting longer than the inspector forgives, which count against the building.</summary>
        public static int OldCalls(ManagedBlock b, int day)
        {
            int old = 0;
            for (int i = 0; i < b.Calls.Count; i++)
            {
                old += day - b.Calls[i] > EstateCatalogue.InspectCallOld ? 1 : 0;
            }
            return old;
        }

        /// <summary>
        /// One day of a held block: calls answered or left, tenants' satisfaction, leases ending and flats let again, the building's condition, the rent collected and the city's
        /// inspection.
        /// </summary>
        public static void BlockDay(AgencyFile agency, ManagedBlock b, int seed, int day, EstateClimate c, List<BlockEvent> events)
        {
            if (!b.Held)
            {
                return;
            }
            int served = EstateCatalogue.Clamp(b.Served, 0, 1000);
            int key = b.Id * 64;
            for (int k = b.Calls.Count - 1; k >= 0; k--)
            {
                if (EstateRules.Roll(seed, day, ForAnswer, key + k) < served * AgencyBookCatalogue.AnswerPermille / 1000)
                {
                    b.Calls.RemoveAt(k);
                }
                else if (day - b.Calls[k] > EstateCatalogue.CallLapseDays)
                {
                    b.Calls.RemoveAt(k);
                    b.Cond = Math.Max(0, b.Cond - EstateCatalogue.CallLapseLoss);
                }
            }
            int chance = EstateRules.CallChance(b.Cond);
            for (int u = 0; u < b.Let && b.Calls.Count < AgencyBookCatalogue.CallsMost; u++)
            {
                if (EstateRules.Roll(seed, day, ForCall, key + u) < chance)
                {
                    b.Calls.Add(day);
                }
            }
            b.Happy = EstateRules.Drift(b.Happy, Target(b, day));
            int left = 0, renew = EstateRules.RenewChance(b.Happy);
            for (int u = 0; u < b.Let; u++)
            {
                if (EstateRules.Roll(seed, day, ForEnd, key + u) < AgencyBookCatalogue.LeaseEndPermille && EstateRules.Roll(seed, day, ForRenew, key + u) >= renew)
                {
                    left++;
                }
            }
            b.Let -= left;
            int relet = ReletChance(c.Cycle, served), taken = 0;
            for (int u = 0; u < b.Empty && taken < b.Empty; u++)
            {
                if (EstateRules.Roll(seed, day, ForRelet, key + u) < relet)
                {
                    taken++;
                }
            }
            if (taken > 0)
            {
                b.Let += taken;
                b.LettingsDone += taken;
                int letFee = LettingFee(b.RentDay) * taken;
                agency.Book.MonthLettings = (int)Math.Min(int.MaxValue / 4, (long)agency.Book.MonthLettings + letFee);
                events.Add(new BlockEvent { Kind = BlockEvent.Letting, Id = b.Id, Amount = letFee });
            }
            int old = OldCalls(b, day);
            int wear = served >= AgencyBookCatalogue.ServedGood && old == 0 ? AgencyBookCatalogue.CondUp : served < AgencyBookCatalogue.ServedPoor ? -AgencyBookCatalogue.CondDown : 0;
            b.Cond = EstateCatalogue.Clamp(b.Cond + wear, 0, 1000);
            b.Collected = (int)Math.Min(int.MaxValue / 4, (long)b.Collected + (long)b.Let * b.RentDay);
            b.HappySum += b.Happy;
            b.HappyDays++;
            if (day >= b.NextInspect)
            {
                Inspect(agency, b, day, c, old, events);
            }
        }

        /// <summary>The agency's fee for a flat it lets again.</summary>
        public static int LettingFee(int rentDay)
        {
            return Math.Max(0, rentDay) * AgencyBookCatalogue.LettingFeeDays;
        }

        // The city's inspection of a block: a pass sets the next day; a failure is a strike, a fine the agency pays, and the next look soon; three in a row and the owner takes the block away.
        private static void Inspect(AgencyFile agency, ManagedBlock b, int day, EstateClimate c, int old, List<BlockEvent> events)
        {
            int score = Math.Max(0, b.Cond - old * EstateCatalogue.InspectCallPenalty);
            if (score >= EstateCatalogue.InspectPass)
            {
                b.Strikes = 0;
                b.NextInspect = day + EstateCatalogue.InspectEvery;
                return;
            }
            b.Strikes = Math.Min(9, b.Strikes + 1);
            b.NextInspect = day + EstateCatalogue.ReinspectDays;
            events.Add(new BlockEvent { Kind = BlockEvent.Fined, Id = b.Id, Amount = EstateRules.InspectFine(b.Strikes, c.Index) });
            if (b.Strikes >= AgencyBookCatalogue.StrikesMost)
            {
                Reputation(agency, b.District, AgencyBookCatalogue.RepLost);
                Lose(agency.Book, b, BlockWhy.Inspection);
                agency.Book.Lost++;
                events.Add(new BlockEvent { Kind = BlockEvent.Dropped, Id = b.Id });
            }
        }

        // ------------------------------------------------------------------------------------------------- the month

        /// <summary>What an owner pays for a month: the agreed share of the rent collected, at the fee asked and the pitch made.</summary>
        public static int MonthFee(int collected, int feeAsk, int feePct)
        {
            return EstateCatalogue.Share(Math.Max(0, collected), (long)Math.Max(0, feeAsk) * Math.Max(0, feePct) / 100);
        }

        /// <summary>
        /// The market's month has turned: each held block pays its fee on the rent collected in the month, less half of it when the tenants were not kept as satisfied as the owner
        /// asked, and a third miss in a row ends the block. Done once for a month (<see cref="AgencyBook.Month"/>): a second call for the same month does nothing, so no fee is paid
        /// twice. False if the month was already done.
        /// </summary>
        public static bool MonthEnd(AgencyFile agency, int month, List<BlockEvent> events)
        {
            AgencyBook book = agency.Book;
            if (month <= book.Month)
            {
                return false;
            }
            book.Month = month;
            book.LastMonthLettings = book.MonthLettings;
            book.MonthLettings = 0;
            book.LastMonthFees = book.LastMonthPenalty = 0;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                ManagedBlock b = book.Blocks[i];
                if (!b.Held)
                {
                    continue;
                }
                int average = b.HappyDays > 0 ? b.HappySum / b.HappyDays : Math.Max(0, b.Happy);
                bool kept = average >= b.Standard;
                int fee = MonthFee(b.Collected, b.FeeAsk, b.FeePct), withheld = kept ? 0 : EstateCatalogue.Share(fee, AgencyBookCatalogue.PenaltyPct * 10);
                fee -= withheld;
                b.Collected = b.HappySum = b.HappyDays = 0;
                b.Months++;
                if (withheld > 0)
                {
                    b.Penalised += withheld;
                    book.LastMonthPenalty += withheld;
                    events.Add(new BlockEvent { Kind = BlockEvent.Penalty, Id = b.Id, Amount = withheld });
                }
                if (fee > 0)
                {
                    b.Fees += fee;
                    b.Earned += fee;
                    book.LastMonthFees += fee;
                    events.Add(new BlockEvent { Kind = BlockEvent.Fee, Id = b.Id, Amount = fee });
                }
                if (kept)
                {
                    b.Missed = 0;
                    Reputation(agency, b.District, AgencyBookCatalogue.RepKept);
                    continue;
                }
                b.Missed++;
                Reputation(agency, b.District, AgencyBookCatalogue.RepMissed);
                if (b.Missed >= AgencyBookCatalogue.MissesMost)
                {
                    Reputation(agency, b.District, AgencyBookCatalogue.RepLost);
                    Lose(book, b, BlockWhy.Standard);
                    book.Lost++;
                    events.Add(new BlockEvent { Kind = BlockEvent.Dropped, Id = b.Id });
                }
            }
            Tidy(book);
            return true;
        }

        /// <summary>The fees the held blocks would pay for a month at today's rents: what the page shows as the month's fees to come.</summary>
        public static int FeesToCome(AgencyBook book)
        {
            long fees = 0;
            for (int i = 0; i < book.Blocks.Count; i++)
            {
                ManagedBlock b = book.Blocks[i];
                fees += b.Held ? MonthFee(b.Collected, b.FeeAsk, b.FeePct) : 0;
            }
            return (int)Math.Min(int.MaxValue / 4, fees);
        }

        /// <summary>The monthly rent a held block collects at the figures it has now, dollars: its let flats over a month of days.</summary>
        public static int RentRoll(ManagedBlock b, int monthDays)
        {
            return (int)Math.Min(int.MaxValue / 4, (long)b.Let * b.RentDay * Math.Max(1, monthDays));
        }
    }
}
