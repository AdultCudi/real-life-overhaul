using System.Collections.Generic;

namespace RealLifeOverhaul.Economy.Ventures
{
    /// <summary>
    /// The save's investors and listed companies, made once from its seed. Game-free and the same on every machine
    /// for the same seed and the same rival names. The investors: two or three tycoons (the first patient, the second
    /// demanding), three angels (patient or even) and two funds (demanding). The companies: one invented firm in each
    /// of the eight sectors, then the game's own rivals by their names (four at most), then a second invented firm in
    /// as many sectors as it takes to make twelve.
    /// </summary>
    public static class Cast
    {
        // The sectors a rival of the game's is listed in (they run shops, food and venues, and own buildings), in turn.
        private static readonly MarketSector[] RivalSectors = { MarketSector.Retail, MarketSector.Food, MarketSector.Entertainment, MarketSector.Property };

        public static void Make(VenturesFile file, int seed, IList<string> rivalNames)
        {
            file.Seed = seed;
            file.Investors.Clear();
            file.Companies.Clear();
            var random = new VenturesRandom(seed);
            int firstUsed = 0, lastUsed = 0, firmUsed = 0;
            int tycoons = random.Range(VenturesCatalogue.TycoonsLeast, VenturesCatalogue.TycoonsMost);
            for (int i = 0; i < tycoons; i++)
            {
                Temperament temper = i == 0 ? Temperament.Patient : i == 1 ? Temperament.Demanding : (Temperament)random.Below(3);
                file.Investors.Add(Person(ref random, file.Investors.Count + 1, InvestorKind.Tycoon, temper, ref firstUsed, ref lastUsed));
            }
            for (int i = 0; i < VenturesCatalogue.Angels; i++)
            {
                file.Investors.Add(Person(ref random, file.Investors.Count + 1, InvestorKind.Angel, random.Below(2) == 0 ? Temperament.Patient : Temperament.Even, ref firstUsed, ref lastUsed));
            }
            for (int i = 0; i < VenturesCatalogue.Firms; i++)
            {
                Investor firm = Terms(ref random, file.Investors.Count + 1, InvestorKind.Firm, Temperament.Demanding);
                firm.First = Unused(ref random, VenturesCatalogue.FirmNames.Length, ref firmUsed);
                file.Investors.Add(firm);
            }

            for (int s = 0; s < (int)MarketSector.Count; s++)
            {
                file.Companies.Add(Listed(ref random, file.Companies.Count + 1, (MarketSector)s, 0, null));
            }
            for (int r = 0; rivalNames != null && r < rivalNames.Count && r < VenturesCatalogue.RivalsListed && file.Companies.Count < VenturesCatalogue.Companies; r++)
            {
                string name = VenturesStore.Clean(rivalNames[r]).Trim();
                if (name.Length > 0)
                {
                    file.Companies.Add(Listed(ref random, file.Companies.Count + 1, RivalSectors[r % RivalSectors.Length], 0, name));
                }
            }
            int sectorUsed = 0;
            while (file.Companies.Count < VenturesCatalogue.Companies)
            {
                var sector = (MarketSector)Unused(ref random, (int)MarketSector.Count, ref sectorUsed);
                file.Companies.Add(Listed(ref random, file.Companies.Count + 1, sector, 1, null));
            }
        }

        private static Investor Person(ref VenturesRandom random, int id, InvestorKind kind, Temperament temper, ref int firstUsed, ref int lastUsed)
        {
            Investor person = Terms(ref random, id, kind, temper);
            person.First = Unused(ref random, VenturesCatalogue.FirstNames.Length, ref firstUsed);
            person.Last = Unused(ref random, VenturesCatalogue.LastNames.Length, ref lastUsed);
            return person;
        }

        private static Investor Terms(ref VenturesRandom random, int id, InvestorKind kind, Temperament temper)
        {
            int k = (int)kind;
            var investor = new Investor
            {
                Id = id, Kind = kind, Temper = temper,
                ChequeLeast = VenturesCatalogue.ChequeLeast[k][random.Below(VenturesCatalogue.ChequeLeast[k].Length)],
                ChequeMost = VenturesCatalogue.ChequeMost[k][random.Below(VenturesCatalogue.ChequeMost[k].Length)],
                Diligence = random.Range(VenturesCatalogue.DiligenceLeast[k], VenturesCatalogue.DiligenceMost[k])
            };
            int used = 0;
            for (int i = 0; i < VenturesCatalogue.LikedSectors; i++)
            {
                investor.Likes |= 1 << Unused(ref random, (int)Sector.Count, ref used);
            }
            return investor;
        }

        private static Company Listed(ref VenturesRandom random, int id, MarketSector sector, int nameIndex, string rivalName)
        {
            var company = new Company
            {
                Id = id, Sector = sector, NameIndex = nameIndex, Rival = rivalName != null, RivalName = rivalName ?? string.Empty,
                PriceCents = random.Range(VenturesCatalogue.PriceLeastCents / 100, VenturesCatalogue.PriceMostCents / 100) * 100,
                VolBp = random.Range(VenturesCatalogue.VolLeastBp, VenturesCatalogue.VolMostBp),
                YieldBp = random.Range(VenturesCatalogue.DividendLeastBp, VenturesCatalogue.DividendMostBp)
            };
            company.FairCents = company.PriceCents;
            company.Remember(company.PriceCents);
            return company;
        }

        // One of 'count' things that has not been taken yet (a bit each in 'used'); any of them once all are.
        private static int Unused(ref VenturesRandom random, int count, ref int used)
        {
            int pick = random.Below(count);
            for (int i = 0; i < count; i++)
            {
                int at = (pick + i) % count;
                if ((used & (1 << at)) == 0)
                {
                    used |= 1 << at;
                    return at;
                }
            }
            return pick;
        }
    }
}
