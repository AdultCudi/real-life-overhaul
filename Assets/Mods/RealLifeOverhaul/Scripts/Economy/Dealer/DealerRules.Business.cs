using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Dealership
{
    /// <summary>What a showroom's event and the perks of its reputation make of an hour's buyers (<see cref="DealerRules.Mood"/>).</summary>
    public struct OfferMood
    {
        public int Promotion;               // points on the building's promotion, for the showroom's own count of visitors
        public int Convert;                 // the chance a visitor looked after makes an offer, thousandths of itself
        public int First;                   // added to a buyer's first offer, of the asking price
        public int Accept;                  // added to the chance a counter is taken
        public int Drive;                   // added to the buyers who ask for a test drive
        public int TradeIn;                 // added to the buyers who bring a car in part exchange
        public int Allowance;               // added to what is allowed for it, of its worth
        public int TakeUp;                  // added to each extra's and warranty's take-up
        public bool Extras;                 // the showroom sells extras
        public int Featured;                // the kind of car an event features (the car catalogue's kinds); 0 none

        /// <summary>No event, no perk and no extras.</summary>
        public static OfferMood Plain => new OfferMood { Convert = 1000 };
    }

    /// <summary>Why an event could not be booked at a showroom.</summary>
    public enum EventRefusal { None, Running, Rested, Kind }

    public static partial class DealerRules
    {
        /// <summary>Whether a showroom may book an event of this kind today: none running there, and the same kind rested since its last.</summary>
        public static EventRefusal CanBook(DealerYard y, int kind, int day)
        {
            if (kind < 0 || kind >= DealerCatalogue.EventKinds)
            {
                return EventRefusal.Kind;
            }
            if (y.Event >= 0 && day <= y.EventUntil)
            {
                return EventRefusal.Running;
            }
            return day <= RestUntil(y, kind) ? EventRefusal.Rested : EventRefusal.None;
        }

        /// <summary>The last day an event of this kind is still resting at a showroom after the last one; -1 if it never ran there.</summary>
        public static int RestUntil(DealerYard y, int kind)
        {
            int last = kind >= 0 && kind < y.EventLast.Length ? y.EventLast[kind] : -1;
            return last < 0 ? -1 : last + DealerCatalogue.EventRest;
        }

        /// <summary>
        /// An event booked today: it runs from today for its days. A featured kind is kept only for the event that features one.
        /// One that ended while nothing closed it is closed first, so its kind rests.
        /// </summary>
        public static void Book(DealerYard y, int kind, int feature, int day)
        {
            if (EventOver(y, day))
            {
                EndEvent(y);
            }
            y.Event = kind;
            y.Feature = kind == DealerCatalogue.FeaturedKind ? Math.Max(0, feature) : 0;
            y.EventFrom = day;
            y.EventUntil = day + DealerCatalogue.EventDays[kind] - 1;
            Count(y, DealerCatalogue.TallyEvents, 1);
        }

        public static bool EventOver(DealerYard y, int day)
        {
            return y.Event >= 0 && day > y.EventUntil;
        }

        /// <summary>An event over: the day it ended is kept for its kind's rest, and the showroom runs none.</summary>
        public static void EndEvent(DealerYard y)
        {
            if (y.Event >= 0 && y.Event < y.EventLast.Length)
            {
                y.EventLast[y.Event] = y.EventUntil;
            }
            y.Event = -1;
            y.Feature = 0;
        }

        /// <summary>The building's promotion with an event's points on it, never past the game's own top.</summary>
        public static int Lifted(int promotion, int points)
        {
            return DealerCatalogue.Clamp(promotion + Math.Max(0, points), 0, DealerCatalogue.PromotionMost);
        }

        /// <summary>The buyers of a showroom's hour: what its event (if one runs today) and the perks of its reputation make of them.</summary>
        public static OfferMood Mood(DealerYard y, int day)
        {
            OfferMood m = OfferMood.Plain;
            m.Extras = y.Extras;
            int step = Step(y.Rep);
            if (step >= DealerCatalogue.PerkOfferStep)
            {
                m.First = DealerCatalogue.PerkOffer;
            }
            if (step >= DealerCatalogue.PerkExtrasStep)
            {
                m.TakeUp = DealerCatalogue.PerkExtras;
            }
            if (step >= DealerCatalogue.PerkConvertStep)
            {
                m.Convert = DealerCatalogue.PerkConvert;
            }
            if (y.EventOn(day) && y.Event < DealerCatalogue.EventKinds)
            {
                int e = y.Event;
                m.Promotion = DealerCatalogue.EventPromotion[e];
                m.Convert = (int)((long)m.Convert * DealerCatalogue.EventOffer[e] / 1000);
                m.Accept = DealerCatalogue.EventAccept[e];
                m.Drive = DealerCatalogue.EventDrive[e];
                m.TradeIn = DealerCatalogue.EventTradeIn[e];
                m.Allowance = DealerCatalogue.EventAllowance[e];
                m.Featured = e == DealerCatalogue.FeaturedKind ? y.Feature : 0;
            }
            return m;
        }

        /// <summary>A bit for each car of the lot that is of the featured kind (a lot holds at most <see cref="DealerCatalogue.CarsMost"/>, so one number holds them all).</summary>
        public static int FeaturedCars(DealerYard y, IList<MarketModel> models, int kind)
        {
            if (kind <= 0 || models == null)
            {
                return 0;
            }
            int mask = 0;
            for (int c = 0; c < y.Cars.Count && c < 31; c++)
            {
                for (int m = 0; m < models.Count; m++)
                {
                    if (models[m].Type == y.Cars[c].Type)
                    {
                        mask |= models[m].Kind == kind ? 1 << c : 0;
                        break;
                    }
                }
            }
            return mask;
        }

        /// <summary>The step of a reputation: 0 for a new showroom up to the last of <see cref="DealerCatalogue.RepSteps"/>.</summary>
        public static int Step(int rep)
        {
            int step = 0;
            for (int s = 1; s < DealerCatalogue.RepSteps.Length; s++)
            {
                step = rep >= DealerCatalogue.RepSteps[s] ? s : step;
            }
            return step;
        }

        /// <summary>A change to a showroom's reputation, kept from 0 to the most. Returns the step it was at before.</summary>
        public static int Repute(DealerYard y, int change)
        {
            int before = Step(y.Rep);
            y.Rep = DealerCatalogue.Clamp(y.Rep + change, 0, DealerCatalogue.RepMost);
            return before;
        }

        /// <summary>What a car sold to a customer does to the reputation: a sale, more for a car in fine condition, less for each fault the buyer finds.</summary>
        public static int SaleRep(int condition, int faults)
        {
            int rep = DealerCatalogue.RepSale + (condition >= DealerCatalogue.RepGoodAt ? DealerCatalogue.RepGoodCar : 0);
            return rep - Math.Min(DealerCatalogue.RepFaultsMost, FaultCount(faults) * DealerCatalogue.RepFault);
        }

        /// <summary>Something a showroom has done, counted for its missions.</summary>
        public static void Count(DealerYard y, int tally, int n)
        {
            if (tally >= 0 && tally < y.Tally.Length && n > 0)
            {
                y.Tally[tally] = (int)Math.Min(int.MaxValue, (long)y.Tally[tally] + n);
            }
        }

        /// <summary>A tally that keeps the most it has reached (the cars on the lot at once).</summary>
        public static void Most(DealerYard y, int tally, int value)
        {
            if (tally >= 0 && tally < y.Tally.Length && value > y.Tally[tally])
            {
                y.Tally[tally] = value;
            }
        }

        /// <summary>How far a mission has come, at most its target.</summary>
        public static int Progress(DealerYard y, int mission)
        {
            int tally = DealerCatalogue.MissionTally[mission];
            return Math.Min(DealerCatalogue.MissionTarget[mission], Math.Max(0, y.Tally[tally]));
        }

        public static bool MissionDone(DealerYard y, int mission)
        {
            return (y.Done & (1 << mission)) != 0;
        }

        /// <summary>The missions newly reached, marked done and rewarded, into <paramref name="done"/>. Returns the reputation given.</summary>
        public static int Missions(DealerYard y, List<int> done)
        {
            done?.Clear();
            int given = 0;
            for (int m = 0; m < DealerCatalogue.Missions; m++)
            {
                if (MissionDone(y, m) || Progress(y, m) < DealerCatalogue.MissionTarget[m])
                {
                    continue;
                }
                y.Done |= 1 << m;
                int before = y.Rep;
                Repute(y, DealerCatalogue.MissionReward[m]);
                given += y.Rep - before;
                done?.Add(m);
            }
            return given;
        }

        /// <summary>An extra's price on a car of this price, at least its least at today's prices.</summary>
        public static int ExtraPrice(int price, int extra, int leastAtPrices)
        {
            if (extra < 0 || extra >= DealerCatalogue.ExtraKinds || price <= 0)
            {
                return 0;
            }
            return Math.Max(leastAtPrices, DealerCatalogue.Named(DealerCatalogue.Share(price, DealerCatalogue.ExtraPrice[extra])));
        }

        /// <summary>What the showroom pays for an extra it sells at this price.</summary>
        public static int ExtraCost(int extraPrice, int extra)
        {
            if (extra < 0 || extra >= DealerCatalogue.ExtraKinds || extraPrice <= 0)
            {
                return 0;
            }
            return DealerCatalogue.Share(extraPrice, DealerCatalogue.ExtraCost[extra]);
        }

        /// <summary>The extras a buyer would take, a bit for each: by their take-up, the perk's and more for a keen buyer.</summary>
        public static int DrawExtras(int profile, in OfferMood mood, ref DealerDice dice)
        {
            if (!mood.Extras)
            {
                return 0;
            }
            int extras = 0;
            for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
            {
                if (dice.Chance(DealerCatalogue.ExtraTakeUp[e] + mood.TakeUp + (profile == 2 ? DealerCatalogue.ExtraKeen : 0)))
                {
                    extras |= 1 << e;
                }
            }
            return extras;
        }

        public static int ExtraCount(int extras)
        {
            int n = 0;
            for (int e = 0; e < DealerCatalogue.ExtraKinds; e++)
            {
                n += (extras & (1 << e)) != 0 ? 1 : 0;
            }
            return n;
        }

        /// <summary>A new car's price for a showroom: the wholesaler's, less the perk of a respected showroom's reputation.</summary>
        public static int WholesalePrice(int listed, int rep)
        {
            return Step(rep) >= DealerCatalogue.PerkWholesaleStep ? DealerCatalogue.Named(DealerCatalogue.Share(listed, 1000 - DealerCatalogue.PerkWholesale)) : listed;
        }

        /// <summary>How a car's condition draws buyers (thousandths): a tired one is passed by, one from 950 up draws in full.</summary>
        public static int ConditionDraw(int condition)
        {
            return Line(DealerCatalogue.ConditionDrawAt, DealerCatalogue.ConditionDraw, condition);
        }

        /// <summary>What the workshop charges: the base, each fault as preparing charges, and a share of what missing condition costs.</summary>
        public static int WorkshopCost(int worthNew, int condition, int faults, int baseAtPrices)
        {
            int missing = Math.Max(0, 1000 - condition - FaultCondition(faults));
            int cost = baseAtPrices + (int)((long)worthNew * missing * DealerCatalogue.WorkshopShare / 1000000L);
            for (int f = 0; f < DealerCatalogue.Faults; f++)
            {
                cost += (faults & (1 << f)) != 0 ? DealerCatalogue.Share(worthNew, DealerCatalogue.FaultCost[f]) : 0;
            }
            return DealerCatalogue.Named(cost);
        }

        /// <summary>The condition a car comes back from the workshop with: every fault put right, then most of what is still missing.</summary>
        public static int Worked(int condition, int faults)
        {
            int c = condition + FaultCondition(faults);
            int missing = Math.Max(0, 1000 - c);
            int back = Math.Max(Math.Min(missing, DealerCatalogue.WorkshopLeast), DealerCatalogue.Share(missing, DealerCatalogue.WorkshopRestore));
            return DealerCatalogue.Clamp(c + back, 0, 1000);
        }

        /// <summary>The hours the work takes: a day, and an hour more for each <see cref="DealerCatalogue.WorkshopPerHour"/> of condition missing.</summary>
        public static int WorkshopHours(int condition)
        {
            int missing = Math.Max(0, 1000 - condition);
            return DealerCatalogue.Clamp(DealerCatalogue.WorkshopHoursLeast + missing / DealerCatalogue.WorkshopPerHour, DealerCatalogue.WorkshopHoursLeast, DealerCatalogue.WorkshopHoursMost);
        }

        /// <summary>True when the workshop has something to do on a car: condition missing or a fault.</summary>
        public static bool WorkshopWorth(StockCar car)
        {
            return car.Condition < 1000 || car.Faults != 0;
        }

        /// <summary>The hour a lot won at this hour arrives: a few days on, in the morning. The same lot always gives the same hour.</summary>
        public static int DeliveryDue(int seed, int lot, int now)
        {
            var dice = new DealerDice(seed, lot, 97);
            int day = now / 24 + dice.Range(DealerCatalogue.DeliveryDaysLeast, DealerCatalogue.DeliveryDaysMost);
            return day * 24 + DealerCatalogue.DeliveryHour;
        }

        /// <summary>The deliveries due by this hour, in the order they were booked, into the list given; the caller settles each.</summary>
        public static void Due(DealerState s, int now, List<DealerDelivery> into)
        {
            into.Clear();
            for (int i = 0; i < s.Deliveries.Count; i++)
            {
                if (s.Deliveries[i].Due <= now)
                {
                    into.Add(s.Deliveries[i]);
                }
            }
        }

        /// <summary>What is read off a showroom as it stands: its cars and their days, its plans' arrears and its reputation.</summary>
        public static void Snapshot(DealerState s, DealerYard y, int day, DealerBook into)
        {
            into.Cars = y.Cars.Count;
            int age = 0;
            for (int c = 0; c < y.Cars.Count; c++)
            {
                age += Math.Max(0, day - y.Cars[c].Bought);
            }
            into.Age = age;
            int arrears = 0, late = 0;
            for (int i = 0; s != null && i < s.Plans.Count; i++)
            {
                DealerPlan p = s.Plans[i];
                if (p.Place == y.Place && p.Missed > 0)
                {
                    arrears = (int)Math.Min(int.MaxValue, (long)arrears + p.Owed);
                    late++;
                }
            }
            into.Arrears = arrears;
            into.Late = late;
            into.Rep = y.Rep;
        }
    }
}
