using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>What a tenant asks the landlord to improve. Saved as a number.</summary>
    public enum EstateImprove : byte
    {
        None = 0,
        Boiler = 1,
        Windows = 2,
        Kitchen = 3,
        Decorate = 4
    }

    /// <summary>What a tenant asks for to stay content, and what answering does. Game-free.</summary>
    public static class ImproveRules
    {
        private const int ForAsk = 74, ForKind = 75;

        /// <summary>The quote for an improvement: a multiple of the quote for a call, by kind.</summary>
        public static int Quote(EstateHolding h, int kind, int index)
        {
            return Math.Max(1, EstateCatalogue.Share(EstateRules.CallCost(h, 1, index), EstateCatalogue.ImproveKindCost[EstateCatalogue.Clamp(kind, 0, EstateCatalogue.ImproveKinds)]));
        }

        /// <summary>A tenant who is not content asks for something, on a few mornings: nothing open, the unit let. True when they did.</summary>
        public static bool Ask(EstateHolding h, int unit, int seed, int day, EstateClimate c)
        {
            EstateUnit u = h.Unit(unit);
            if (u == null || !u.Let || u.Improve > 0 || u.Happy < 0 || u.Happy >= EstateCatalogue.ImproveBelow || u.Call > 0 || u.Arrears > 0 || h.Busy)
            {
                return false;
            }
            int key = EstateRules.UnitKey(h, unit);
            if (EstateRules.Roll(seed, day, ForAsk, key) >= EstateCatalogue.ImproveChance)
            {
                return false;
            }
            u.Improve = EstateRules.Between(EstateRules.Roll(seed, day, ForKind, key), 1, EstateCatalogue.ImproveKinds);
            u.ImproveDay = day;
            u.ImproveCost = Quote(h, u.Improve, c.Index);
            return true;
        }

        /// <summary>The request is met: the landlord pays; the tenant is glad and the building is better kept. Returns the satisfaction gained; the caller takes the money.</summary>
        public static int Accept(EstateHolding h, EstateUnit u)
        {
            if (h == null || u == null || u.Improve <= 0)
            {
                return 0;
            }
            if (u.Happy >= 0)
            {
                u.Happy = EstateCatalogue.Clamp(u.Happy + EstateCatalogue.ImproveGain, 0, 1000);
            }
            h.Cond = Math.Min(1000, h.Cond + (u.Improve == (int)EstateImprove.Decorate ? 30 : 60));
            u.Improve = u.ImproveDay = u.ImproveCost = 0;
            return EstateCatalogue.ImproveGain;
        }

        /// <summary>The request is refused: satisfaction falls and they are likelier to go.</summary>
        public static void Refuse(EstateUnit u)
        {
            if (u == null || u.Improve <= 0)
            {
                return;
            }
            if (u.Happy >= 0)
            {
                u.Happy = EstateCatalogue.Clamp(u.Happy - EstateCatalogue.ImproveRefusedLoss, 0, 1000);
            }
            u.Refused++;
            u.Improve = u.ImproveDay = u.ImproveCost = 0;
        }

        /// <summary>A request left too long lapses, with a small loss. True on the day it does.</summary>
        public static bool Lapse(EstateUnit u, int day)
        {
            if (u == null || u.Improve <= 0 || day - u.ImproveDay < EstateCatalogue.ImproveDays)
            {
                return false;
            }
            if (u.Happy >= 0)
            {
                u.Happy = EstateCatalogue.Clamp(u.Happy - EstateCatalogue.ImproveLapseLoss, 0, 1000);
            }
            u.Improve = u.ImproveDay = u.ImproveCost = 0;
            return true;
        }

        /// <summary>The thousandths a day added to a tenant's chance of giving notice by the requests they were refused.</summary>
        public static int QuitMore(EstateUnit u) => u == null ? 0 : u.Refused * EstateCatalogue.ImproveRefusedQuit;
    }
}
