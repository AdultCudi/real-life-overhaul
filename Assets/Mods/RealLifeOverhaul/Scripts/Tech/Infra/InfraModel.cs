using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>A data centre of the player's: a warehouse fitted out. Game-free.</summary>
    public sealed class DataCentre
    {
        public string Place = string.Empty;
        public string District = string.Empty;
        public int Tier;
        public int Sqm;
        public int Racks, GpuRacks;
        public long Book;            // the racks' value in the books, cents
        public int Fitted;
        public int Colo;             // racks let to the city's companies
        public int Outages, DownUntil;
        public long Takings30;       // what it took over the last thirty days, cents (rolled)
        public readonly long[] Days = new long[FirmsCatalogue.MonthDays];
        // ---- read each day, never saved
        public string Name = string.Empty;
        public int Technicians;
        public bool Power = true, Net = true;

        public int RacksRoom => Sqm / InfraCatalogue.SqmPerRack;
        public bool Down(int day) => day < DownUntil;
    }

    /// <summary>The player's cloud: compute sold from the data centres. Game-free.</summary>
    public sealed class Cloud
    {
        public bool Open;
        public string Name = string.Empty;
        public int Launched;
        public int Price = 1000;         // thousandths of the rivals' list price
        public int RivalPrice = 1000;    // what the rival clouds charge now, thousandths of their list price
        public int LastCut;
        public int Reputation = 600;
        public int LicenceUntil;
        public bool Hyper;
        public int Sold, GpuSold;        // units sold yesterday
        public int Own;                  // ...of them to the player's own firms
    }

    /// <summary>What a data centre's day came to.</summary>
    public struct CentreDay
    {
        public long Takings, Power, Water, WriteDown;
        public bool Outage;
    }

    /// <summary>Everything the infrastructure part keeps in a save. Game-free.</summary>
    public sealed class InfraFile
    {
        public int Seed, FirstDay, LastDay = -1;
        public readonly List<DataCentre> Centres = new List<DataCentre>(4);
        public readonly Cloud Cloud = new Cloud();

        public DataCentre CentreAt(string place)
        {
            for (int i = 0; i < Centres.Count; i++)
            {
                if (Centres[i].Place == place)
                {
                    return Centres[i];
                }
            }
            return null;
        }
    }
}
