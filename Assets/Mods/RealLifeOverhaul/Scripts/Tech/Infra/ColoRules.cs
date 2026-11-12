using System;
using System.Collections.Generic;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul.Tech
{
    /// <summary>What happened to a colocation contract on a day, for the notices: a rival's offer, a renewal, a loss, a client that left.</summary>
    public struct ColoNote
    {
        public const int Offer = 0, Renewed = 1, Lost = 2, Left = 3;
        public int Kind, Client, Racks, Rival, Day;
        public string Place;
    }

    /// <summary>
    /// The named colocation contracts of a data centre and the rival data centres that bid for them. The racks a centre lets (DataCentre.Colo, which the day's letting moves and which
    /// the income is made from) are shared out among named clients of the city: a contract is made when racks let to a new client, and a client that leaves ends the smallest.
    /// Forty-five days before a term ends a rival hall may bid for the client, if the rival can host the racks under the same supply limits as the player's halls (InfraLimits); at
    /// the term's end a client with an offer chooses by tier and by the hall's uptime (SectorRules.RackShare), one with none renews. The rival halls are the catalogue's firms, each
    /// with a floor, a tier and a load worked from its index, so they are held to the same sums. Seeded from the file's seed and the day, so a host and a guest agree. Game-free.
    /// </summary>
    public static class ColoRules
    {
        public const int TermLeast = 180, TermMost = 540, MovingDays = 7, OfferBefore = 45, EndsSoon = 30, ContractsMost = 60, RacksLeast = 4, RacksMost = 24;
        public const int OfferChance = 450;          // per mille a client with a term ending is bid for, where a rival can host it
        public const int RivalSqm = 2400, RivalMbps = 60000;

        /// <summary>The company ids a client may be: the listed companies of the city that are not a tycoon's holding, in the catalogue's order.</summary>
        public static int[] Pool()
        {
            var ids = new List<int>(64);
            for (int i = 0; i < MarketCatalogue.All.Length; i++)
            {
                CompanyDef c = MarketCatalogue.All[i];
                if (!c.Holding && !c.Pipeline && c.Size != MarketSize.Small)
                {
                    ids.Add(c.Id);
                }
            }
            return ids.ToArray();
        }

        public static string NameOf(int client)
        {
            for (int i = 0; i < MarketCatalogue.All.Length; i++)
            {
                if (MarketCatalogue.All[i].Id == client)
                {
                    return MarketCatalogue.All[i].Name;
                }
            }
            return string.Empty;
        }

        /// <summary>The racks of a centre that named clients hold.</summary>
        public static int Held(InfraFile f, string place)
        {
            int n = 0;
            for (int i = 0; i < f.Colo.Count; i++)
            {
                n += f.Colo[i].Place == place ? f.Colo[i].Racks : 0;
            }
            return n;
        }

        /// <summary>A rival hall: its tier, its floor and a load of 55 to 85 per cent of it, worked from its index and the seed.</summary>
        public static DataCentre RivalHall(int seed, int rival)
        {
            int load = 550 + (int)(TechRandom.Hash(seed, rival, 5, 3) % 300);
            var h = new DataCentre { Place = "rival" + rival, Tier = rival % InfraCatalogue.Tiers, Sqm = RivalSqm };
            h.Racks = h.RacksRoom * load / 1000;
            return h;
        }

        /// <summary>Whether a rival hall can host racks: they must fit its floor, its power feed, its cooling and its connection, as they must a hall of the player's.</summary>
        public static bool RivalCanHost(int seed, int rival, int racks) => InfraLimits.Fit(RivalHall(seed, rival), false, RivalMbps, out _) >= racks;

        /// <summary>Makes and ends contracts so the named clients hold what the centre lets: a new client when four racks or more are unplaced, the smallest contract ended when racks went.</summary>
        public static void Sync(InfraFile f, DataCentre d, int day, List<ColoNote> notes)
        {
            int held = Held(f, d.Place);
            int[] pool = null;
            while (d.Colo - held >= RacksLeast && f.Colo.Count < ContractsMost)
            {
                pool = pool ?? Pool();
                int id = f.NextColo;
                int racks = RacksLeast + (int)(TechRandom.Hash(f.Seed, id, 11, 1) % (uint)(RacksMost - RacksLeast + 1));
                if (d.Colo - held < racks)
                {
                    break;   // the client is not yet in: racks are let a few at a time, and a client takes its racks together
                }
                int client = pool[(int)(TechRandom.Hash(f.Seed, id, 13, 1) % (uint)pool.Length)];
                for (int step = 0; step < pool.Length && Taken(f, d.Place, client); step++)
                {
                    client = pool[(Array.IndexOf(pool, client) + 1) % pool.Length];
                }
                if (Taken(f, d.Place, client))
                {
                    break;
                }
                f.NextColo++;
                f.Colo.Add(new ColoContract
                {
                    Id = id, Place = d.Place, Client = client, Racks = racks, Fee = (long)racks * InfraCatalogue.ColoMonth, Start = day,
                    End = day + TermLeast + (int)(TechRandom.Hash(f.Seed, id, 17, 2) % (uint)(TermMost - TermLeast + 1))
                });
                held += racks;
            }
            while (held > d.Colo)
            {
                ColoContract smallest = null;
                for (int i = 0; i < f.Colo.Count; i++)
                {
                    ColoContract c = f.Colo[i];
                    smallest = c.Place == d.Place && (smallest == null || c.Racks < smallest.Racks) ? c : smallest;
                }
                if (smallest == null)
                {
                    break;
                }
                held -= smallest.Racks;
                f.Colo.Remove(smallest);
                notes?.Add(new ColoNote { Kind = ColoNote.Left, Client = smallest.Client, Racks = smallest.Racks, Place = d.Place, Day = day });
            }
        }

        private static bool Taken(InfraFile f, string place, int client)
        {
            for (int i = 0; i < f.Colo.Count; i++)
            {
                if (f.Colo[i].Place == place && f.Colo[i].Client == client)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>One day of a centre's contracts: a rival's offer made before a term ends, and at its end the client's choice. <paramref name="uptime"/> is the hall's, thousandths.</summary>
        public static void Turn(InfraFile f, DataCentre d, int day, int uptime, List<ColoNote> notes)
        {
            for (int i = f.Colo.Count - 1; i >= 0; i--)
            {
                ColoContract c = f.Colo[i];
                if (c.Place != d.Place)
                {
                    continue;
                }
                if (!c.Weighed && day >= c.End - OfferBefore && day < c.End)
                {
                    c.Weighed = true;
                    int rival = (int)(TechRandom.Hash(f.Seed, c.Id, c.End, 19) % (uint)HiringCatalogue.RivalFirms.Length);
                    if (TechRandom.Hash(f.Seed, c.Id, c.End, 23) % 1000 < OfferChance && RivalCanHost(f.Seed, rival, c.Racks))
                    {
                        c.Rival = rival;
                        notes?.Add(new ColoNote { Kind = ColoNote.Offer, Client = c.Client, Racks = c.Racks, Rival = rival, Place = d.Place, Day = day });
                    }
                }
                if (day < c.End)
                {
                    continue;
                }
                bool stays = c.Rival < 0 || TechRandom.Hash(f.Seed, c.Id, c.End, 29) % 1000 < SectorRules.RackShare(d.Tier, Math.Max(0, Math.Min(1000, uptime)));
                if (stays)
                {
                    c.End = day + TermLeast + (int)(TechRandom.Hash(f.Seed, c.Id, day, 31) % (uint)(TermMost - TermLeast + 1));
                    c.Rival = -1;
                    c.Weighed = false;
                    notes?.Add(new ColoNote { Kind = ColoNote.Renewed, Client = c.Client, Racks = c.Racks, Rival = -1, Place = d.Place, Day = day });
                }
                else
                {
                    d.Colo = Math.Max(0, d.Colo - c.Racks);
                    f.Colo.RemoveAt(i);
                    notes?.Add(new ColoNote { Kind = ColoNote.Lost, Client = c.Client, Racks = c.Racks, Rival = c.Rival, Place = d.Place, Day = day });
                }
            }
        }

        /// <summary>The state of a contract on a day, for the page: 0 active, 1 moving in, 2 a rival's offer, 3 ends soon.</summary>
        public static int State(ColoContract c, int day) => c.Rival >= 0 ? 2 : day - c.Start < MovingDays ? 1 : c.End - day <= EndsSoon ? 3 : 0;
    }
}
