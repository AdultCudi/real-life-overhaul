using System;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What a data centre's supplies hold and what its racks use of each: its power feed, its cooling plant and its bandwidth, in the units a page writes them (kW, kW, Mbps).</summary>
    public struct CentreLimits
    {
        public long PowerUse, PowerCap;       // kW drawn by the racks against what the feed carries
        public long CoolUse, CoolCap;         // kW of heat the racks give off against what the plant takes away
        public long NetUse, NetCap;           // Mbps the racks move against the connection's plan
    }

    /// <summary>
    /// The supply limits of a data centre (the owner's mockup: power in MW, cooling as a share of the plant, bandwidth in Gbps; growth stops when one is full). A hall's feed and plant
    /// are sized to its floor and its tier: a standard rack draws what a floor of them would at the tier's share of the full room, so an accelerator rack, which draws four times
    /// as much on twice the floor, fills the feed long before the floor is full. The connection is the office's internet plan at the Utilities (the data centre's own, which the
    /// firms already need). The rival data centres are held to the same sums through <see cref="SectorRules.RackShare"/>, which takes a hall's tier and name. Pure; game-free.
    /// </summary>
    public static class InfraLimits
    {
        /// <summary>Thousandths of the floor's standard racks' draw the power feed carries, by tier.</summary>
        public static readonly int[] FeedShare = { 700, 850, 1000, 1150 };

        /// <summary>Thousandths of the floor's standard racks' heat the cooling plant takes away, by tier.</summary>
        public static readonly int[] CoolShare = { 750, 900, 1000, 1100 };

        public const int MbpsPerRack = 100, MbpsPerGpuRack = 400;

        public const int Power = 0, Cooling = 1, Bandwidth = 2, Floor = 3, None = 4;

        public static CentreLimits Of(DataCentre d, int planMbps)
        {
            int tier = Math.Max(0, Math.Min(d.Tier, InfraCatalogue.Tiers - 1));
            long full = (long)d.RacksRoom * InfraCatalogue.RackKw;
            long load = (long)d.Racks * InfraCatalogue.RackKw + (long)d.GpuRacks * InfraCatalogue.GpuRackKw;
            return new CentreLimits
            {
                PowerUse = load, PowerCap = full * FeedShare[tier] / 1000, CoolUse = load, CoolCap = full * CoolShare[tier] / 1000,
                NetUse = (long)d.Racks * MbpsPerRack + (long)d.GpuRacks * MbpsPerGpuRack,
                NetCap = planMbps > 0 ? planMbps : InfraCatalogue.NetNeeded   // an office with no plan read yet is held to the business line a centre needs
            };
        }

        /// <summary>How many more racks of a kind fit under every limit and on the floor: the least of what each supply and the floor have left.</summary>
        public static int Fit(DataCentre d, bool gpu, int planMbps, out int binding)
        {
            CentreLimits l = Of(d, planMbps);
            int kw = gpu ? InfraCatalogue.GpuRackKw : InfraCatalogue.RackKw, mbps = gpu ? MbpsPerGpuRack : MbpsPerRack;
            int floor = InfraRules.Room(d) / (gpu ? 2 : 1);
            int power = (int)Math.Max(0L, (l.PowerCap - l.PowerUse) / kw), cool = (int)Math.Max(0L, (l.CoolCap - l.CoolUse) / kw), net = (int)Math.Max(0L, (l.NetCap - l.NetUse) / mbps);
            int fit = Math.Min(Math.Min(power, cool), Math.Min(net, floor));
            binding = fit == floor ? Floor : fit == power ? Power : fit == cool ? Cooling : fit == net ? Bandwidth : None;
            return fit;
        }

        /// <summary>The share of a supply in use, 0 to 100 (over 100 for a hall whose supply fell after its racks were bought).</summary>
        public static int Percent(long use, long cap) => cap <= 0 ? (use > 0 ? 100 : 0) : (int)Math.Min(999L, use * 100 / cap);
    }
}
