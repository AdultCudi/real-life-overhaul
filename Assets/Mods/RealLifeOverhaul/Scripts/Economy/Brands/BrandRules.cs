using System;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// A brand by rule: standards, recognition by district and city-wide, footfall, campaigns and scandals. Game-free
    /// whole numbers, so the same days give the same history. The balance is worked through in EconomyCheck's BrandBalance.
    /// </summary>
    public static class BrandRules
    {
        public const int PushLocalTimes = 4;   // a brand campaign in one district costs this many lots of flyers for each shop there

        /// <summary>One shop's standards from the game's own figures, each 0 to 100.</summary>
        public static int ShopStandards(int satisfaction, int clean, int stock, int skill)
        {
            int sum = BrandCatalogue.Clamp(satisfaction, 0, 100) * BrandCatalogue.WeightSatisfaction + BrandCatalogue.Clamp(clean, 0, 100) * BrandCatalogue.WeightClean
                      + BrandCatalogue.Clamp(stock, 0, 100) * BrandCatalogue.WeightStock + BrandCatalogue.Clamp(skill, 0, 100) * BrandCatalogue.WeightSkill;
            return (sum + 50) / 100;
        }

        /// <summary>A shop's standards with what its interior counts, at most <see cref="BrandCatalogue.InteriorMost"/> either way.</summary>
        public static int WithInterior(int standards, int interior)
        {
            return BrandCatalogue.Clamp(standards + BrandCatalogue.Clamp(interior, -BrandCatalogue.InteriorMost, BrandCatalogue.InteriorMost), 0, 100);
        }

        /// <summary>The brand's standards: mostly the mean of its shops, the rest its worst (a franchised shop as it really is). 0 with no shop.</summary>
        public static int Standards(BrandsState s, Brand b)
        {
            long sum = 0;
            int count = 0, worst = 100;
            for (int i = 0; i < s.Members.Count; i++)
            {
                if (s.Members[i].BrandId == b.Id)
                {
                    int v = BrandCatalogue.Clamp(s.Members[i].Standards, 0, 100);
                    sum += v;
                    worst = Math.Min(worst, v);
                    count++;
                }
            }
            for (int i = 0; i < s.Franchises.Count; i++)
            {
                if (s.Franchises[i].BrandId == b.Id)
                {
                    int v = BrandCatalogue.Clamp(s.Franchises[i].StandardsNow, 0, 100);
                    sum += v;
                    worst = Math.Min(worst, v);
                    count++;
                }
            }
            if (count == 0)
            {
                return 0;
            }
            int mean = (int)((sum + count / 2) / count);
            return (mean * BrandCatalogue.BrandMeanShare + worst * BrandCatalogue.BrandWorstShare + 500) / 1000;
        }

        /// <summary>The brand's shops in a district, member and franchised; in the whole city for a null district.</summary>
        public static int Shops(BrandsState s, Brand b, string district)
        {
            return Members(s, b, district) + Franchised(s, b, district);
        }

        public static int Members(BrandsState s, Brand b, string district)
        {
            int count = 0;
            for (int i = 0; i < s.Members.Count; i++)
            {
                count += s.Members[i].BrandId == b.Id && (district == null || string.Equals(s.Members[i].District, district, StringComparison.Ordinal)) ? 1 : 0;
            }
            return count;
        }

        public static int Franchised(BrandsState s, Brand b, string district)
        {
            int count = 0;
            for (int i = 0; i < s.Franchises.Count; i++)
            {
                count += s.Franchises[i].BrandId == b.Id && (district == null || string.Equals(s.Franchises[i].District, district, StringComparison.Ordinal)) ? 1 : 0;
            }
            return count;
        }

        /// <summary>The customers the brand's shops in a district served yesterday.</summary>
        public static int Customers(BrandsState s, Brand b, string district)
        {
            long sum = 0;
            for (int i = 0; i < s.Members.Count; i++)
            {
                sum += s.Members[i].BrandId == b.Id && string.Equals(s.Members[i].District, district, StringComparison.Ordinal) ? Math.Max(0, s.Members[i].Customers) : 0;
            }
            for (int i = 0; i < s.Franchises.Count; i++)
            {
                sum += s.Franchises[i].BrandId == b.Id && string.Equals(s.Franchises[i].District, district, StringComparison.Ordinal) ? Math.Max(0, s.Franchises[i].Customers) : 0;
            }
            return (int)Math.Min(sum, int.MaxValue / 4);
        }

        /// <summary>What a customer spent at the brand's member shops yesterday, hundredths; the last figure if nobody came.</summary>
        public static int Ticket(BrandsState s, Brand b)
        {
            long takings = 0, customers = 0;
            for (int i = 0; i < s.Members.Count; i++)
            {
                Member m = s.Members[i];
                if (m.BrandId == b.Id && m.Customers > 0 && m.Takings > 0)
                {
                    takings += m.Takings;
                    customers += m.Customers;
                }
            }
            return customers > 0 ? (int)Math.Min(takings * 100 / customers, int.MaxValue / 4) : b.Ticket;
        }

        /// <summary>A district's daily gain before it slows near the top, hundredths; standards under 50 take away.</summary>
        public static int Gain(int shops, int customers, int standards, int campaign)
        {
            if (shops <= 0)
            {
                return Math.Max(0, campaign);
            }
            int presence = Math.Min(shops, BrandCatalogue.PresenceShopsMost) * BrandCatalogue.PresencePerShop;
            int footfall = BrandCatalogue.Clamp(customers, 0, BrandCatalogue.FootfallFull) * BrandCatalogue.FootfallGainMost / BrandCatalogue.FootfallFull;
            int good = (BrandCatalogue.Clamp(standards, 0, 100) - 50) * BrandCatalogue.StandardsGainMost / 50;
            return presence + footfall + good + Math.Max(0, campaign);
        }

        /// <summary>What a district with a shop gains a day from the brand's shops elsewhere, hundredths: less for each one, so a wide brand does not run away.</summary>
        public static int Pull(int others)
        {
            int pull = 0;
            for (int k = 0; k < Math.Min(others, BrandCatalogue.PullShopsMost); k++)
            {
                pull += Math.Max(0, BrandCatalogue.PullFirst - k * BrandCatalogue.PullLess);
            }
            return pull;
        }

        public static int PullIn(BrandsState s, Brand b, int shops)
        {
            return shops > 0 ? Pull(Shops(s, b, null) - shops) : 0;
        }

        /// <summary>A district's gain on the brand's day of this age; the day's step and the app's "tomorrow" both ask this.</summary>
        public static int GainOn(BrandsState s, Brand b, DistrictFile d, int age)
        {
            int shops = Shops(s, b, d.Name);
            bool pushing = b.Push >= 0 && age >= b.PushFrom && age < b.PushEnd;
            int campaign = !pushing ? 0
                : b.Push == BrandCatalogue.PushCity ? (shops > 0 ? BrandCatalogue.CampaignCityGain : 0)
                : string.Equals(d.Name, b.PushDistrict, StringComparison.Ordinal) ? BrandCatalogue.CampaignLocalGain : 0;
            return Gain(shops, Customers(s, b, d.Name), b.Standards, campaign) + PullIn(s, b, shops);
        }

        /// <summary>Mornings since the brand began on which recognition took no step: Brands was off, or days jumped at once.</summary>
        public static int Missed(Brand b, int today)
        {
            return Math.Max(0, today - b.Founded - b.Age);
        }

        /// <summary>The text key for the brand page's line about its mornings.</summary>
        public static string StepsKey(int days, int moved)
        {
            return days <= 0 ? "reallife_brand_fig_steps_first" : days > 1 ? "reallife_brand_fig_steps" : moved > 0 ? "reallife_brand_fig_steps_one" : "reallife_brand_fig_steps_one_still";
        }

        public static string MissedKey(int missed)
        {
            return missed == 1 ? "reallife_brand_fig_missed_one" : "reallife_brand_fig_missed";
        }

        /// <summary>Recognition as the app shows it, tenths of a point, so that a day's gain of a few hundredths shows.</summary>
        public static int Shown(int hundredths)
        {
            return BrandCatalogue.Clamp(hundredths, 0, BrandCatalogue.RecMost) / 10;
        }

        /// <summary>A district's figure a day later: a gain slows near the top, a loss is whole, and a little fades (more with no shop left).</summary>
        public static int Step(int rec, int gain, bool present)
        {
            rec = BrandCatalogue.Clamp(rec, 0, BrandCatalogue.RecMost);
            int grown = gain > 0 ? (int)((long)gain * (BrandCatalogue.RecMost - rec) / BrandCatalogue.RecMost) : gain;
            int faded = BrandCatalogue.Bp(rec, present ? BrandCatalogue.DecayBp : BrandCatalogue.DecayEmptyBp);
            return BrandCatalogue.Clamp(rec + grown - faded, 0, BrandCatalogue.RecMost);
        }

        /// <summary>A new district's figure after some days of the same shops, customers and standards, hundredths: for the Guide.</summary>
        public static int After(int shops, int customers, int standards, int days)
        {
            int rec = 0, gain = Gain(shops, customers, standards, 0);
            for (int i = 0; i < days; i++)
            {
                rec = Step(rec, gain, shops > 0);
            }
            return rec;
        }

        /// <summary>For the Guide: each district's figure of a new brand with one shop in each of so many districts after some days, and the city-wide one.</summary>
        public static int AfterSpread(int districts, int customers, int standards, int days, out int city)
        {
            int rec = 0, gain = Gain(1, customers, standards, 0) + Pull(districts - 1);
            city = 0;
            for (int i = 0; i < days; i++)
            {
                rec = Step(rec, gain, true);
                int target = CityTarget((long)rec * districts, districts, districts);
                city = BrandCatalogue.Clamp(city + BrandCatalogue.Bp(target - city, BrandCatalogue.CityStepBp), 0, BrandCatalogue.RecMost);
            }
            return rec;
        }

        /// <summary>A district's file, made if asked and the brand has room for one more.</summary>
        public static DistrictFile District(Brand b, string name, bool make)
        {
            DistrictFile d = b.District(name);
            if (d == null && make && !string.IsNullOrEmpty(name) && b.Districts.Count < BrandCatalogue.DistrictsMost)
            {
                d = new DistrictFile { Name = name, Rec = CompanyCatalogue.Share(b.City, BrandCatalogue.CityFloorShare) };
                b.Districts.Add(d);
            }
            return d;
        }

        /// <summary>
        /// Where the city-wide figure heads, hundredths: the sum over eight times the districts with a shop, kept between
        /// the sum over eight and the mean. One district gives an eighth, two a quarter of their sum, three or more their mean.
        /// </summary>
        public static int CityTarget(long sum, int districts, int withShops)
        {
            long eighth = sum / BrandCatalogue.CityDistricts;
            long mean = districts > 0 ? sum / districts : 0;
            long spread = sum * Math.Max(1, withShops) / BrandCatalogue.CityDistricts;
            return (int)Math.Min(BrandCatalogue.RecMost, Math.Max(eighth, Math.Min(spread, mean)));
        }

        /// <summary>One day of the brand's own clock, on the figures read this morning: standards, each district's step, the city-wide figure, a campaign's end.</summary>
        public static void Day(BrandsState s, Brand b)
        {
            b.Age++;
            b.Standards = Standards(s, b);
            for (int i = 0; i < s.Members.Count; i++)
            {
                if (s.Members[i].BrandId == b.Id)
                {
                    District(b, s.Members[i].District, true);
                }
            }
            for (int i = 0; i < s.Franchises.Count; i++)
            {
                if (s.Franchises[i].BrandId == b.Id)
                {
                    District(b, s.Franchises[i].District, true);
                }
            }
            long sum = 0;
            int withShops = 0;
            for (int i = 0; i < b.Districts.Count; i++)
            {
                DistrictFile d = b.Districts[i];
                bool present = Shops(s, b, d.Name) > 0;
                d.Rec = Step(d.Rec, GainOn(s, b, d, b.Age), present);
                sum += d.Rec;
                withShops += present ? 1 : 0;
            }
            int target = CityTarget(sum, b.Districts.Count, withShops);
            b.City = BrandCatalogue.Clamp(b.City + BrandCatalogue.Bp(target - b.City, BrandCatalogue.CityStepBp), 0, BrandCatalogue.RecMost);
            if (b.Push >= 0 && b.Age + 1 >= b.PushEnd)
            {
                b.Push = -1;
                b.PushFrom = b.PushEnd = 0;
                b.PushDistrict = string.Empty;
            }
        }

        /// <summary>The recognition that counts in a district, hundredths: its own figure, never under half the city-wide one.</summary>
        public static int Effective(Brand b, string district)
        {
            DistrictFile d = b.District(district);
            return Math.Max(d != null ? d.Rec : 0, CompanyCatalogue.Share(b.City, BrandCatalogue.CityFloorShare));
        }

        public static int Best(Brand b)
        {
            int best = 0;
            for (int i = 0; i < b.Districts.Count; i++)
            {
                best = Math.Max(best, b.Districts[i].Rec);
            }
            return best;
        }

        /// <summary>A scandal at a shop of the brand's: the district's figure falls, the city-wide one by a quarter of that. Returns what the district lost.</summary>
        public static int Hit(Brand b, string district, Scandal kind)
        {
            return Hit(b, district, kind, 1000);
        }

        /// <summary>The same with the loss scaled, thousandths (how the talk of it spreads), from four fifths to a quarter more.</summary>
        public static int Hit(Brand b, string district, Scandal kind, int scale)
        {
            int loss = BrandCatalogue.ScandalLoss[BrandCatalogue.Clamp((int)kind, 0, BrandCatalogue.ScandalLoss.Length - 1)];
            loss = (int)((long)loss * BrandCatalogue.Clamp(scale, BrandCatalogue.ScandalScaleLeast, BrandCatalogue.ScandalScaleMost) / 1000);
            return Lose(b, district, loss, CompanyCatalogue.Share(loss, BrandCatalogue.ScandalCityShare));
        }

        /// <summary>Recognition a scandal took is given back in its district, and a quarter of it city-wide, never past the most. Returns what came back.</summary>
        public static int Regain(Brand b, string district, int points)
        {
            DistrictFile d = b.District(district);
            if (d == null || points <= 0)
            {
                return 0;
            }
            int back = Math.Min(points, BrandCatalogue.RecMost - d.Rec);
            d.Rec += Math.Max(0, back);
            b.City = Math.Min(BrandCatalogue.RecMost, b.City + CompanyCatalogue.Share(Math.Max(0, back), BrandCatalogue.ScandalCityShare));
            return Math.Max(0, back);
        }

        /// <summary>Recognition lost in a district and city-wide, never under nothing. Returns what the district lost.</summary>
        public static int Lose(Brand b, string district, int loss, int cityLoss)
        {
            DistrictFile d = b.District(district);
            int lost = 0;
            if (d != null)
            {
                lost = Math.Min(d.Rec, Math.Max(0, loss));
                d.Rec -= lost;
            }
            b.City = Math.Max(0, b.City - Math.Max(0, cityLoss));
            return lost;
        }

        /// <summary>Customers the name brings, per cent: half of it while the brand's standards are low.</summary>
        public static int RecFootfall(int rec, int standards)
        {
            int percent = (BrandCatalogue.Clamp(rec, 0, BrandCatalogue.RecMost) * BrandCatalogue.RecFootfallMost + BrandCatalogue.RecMost / 2) / BrandCatalogue.RecMost;
            return standards < BrandCatalogue.LowStandards ? percent / 2 : percent;
        }

        /// <summary>The most of what prices cost a shop that a trusted brand gets back, per cent of customers.</summary>
        public static int Tolerance(int standards)
        {
            return standards <= 50 ? 0 : (Math.Min(standards, 100) - 50) * BrandCatalogue.ToleranceMost / 50;
        }

        /// <summary>What is given back of the customers a shop's prices cost it (a loss is negative): never more than they cost.</summary>
        public static int Forgiven(int priceLoss, int standards)
        {
            return Math.Min(Math.Max(0, -priceLoss), Tolerance(standards));
        }

        public static int Distance(int x1, int z1, int x2, int z2)
        {
            long dx = x1 - x2, dz = z1 - z2;
            return (int)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>Customers one franchised shop in the books takes from a member shop, per cent, falling evenly to nothing at the radius.</summary>
        public static int Cannibal(int metres)
        {
            return metres < 0 || metres >= BrandCatalogue.CannibalRadius ? 0
                : (BrandCatalogue.CannibalEach * (BrandCatalogue.CannibalRadius - metres) + BrandCatalogue.CannibalRadius - 1) / BrandCatalogue.CannibalRadius;
        }

        /// <summary>The same from every franchised shop in the books only; one that is the game's business competes by the game's rules.</summary>
        public static int CannibalAll(BrandsState s, Member m)
        {
            int all = 0;
            for (int i = 0; m.HasSpot && i < s.Franchises.Count; i++)
            {
                Franchise f = s.Franchises[i];
                if (f.BrandId == m.BrandId && f.Hold == Hold.Books && f.HasSpot)
                {
                    all += Cannibal(Distance(m.X, m.Z, f.X, f.Z));
                }
            }
            return Math.Min(all, BrandCatalogue.CannibalMost);
        }

        /// <summary>Everything a brand asks for at one shop, per cent of customers, inside its own bounds.</summary>
        public static int Footfall(int rec, int forgiven, int cannibal)
        {
            return BrandCatalogue.Clamp(rec + forgiven - cannibal, BrandCatalogue.FootfallLeast, BrandCatalogue.FootfallMost);
        }

        /// <summary>Works out the three parts for a member shop and returns what they come to, within bounds.</summary>
        public static int Apply(BrandsState s, Brand b, Member m)
        {
            m.Rec = RecFootfall(Effective(b, m.District), b.Standards);
            m.Forgiven = Forgiven(m.PriceLoss, b.Standards);
            m.Cannibal = CannibalAll(s, m);
            return Footfall(m.Rec, m.Forgiven, m.Cannibal);
        }

        /// <summary>A name fit to keep: no divider of the save's line, no space at either end, not too long. Empty if nothing is left.</summary>
        public static string Name(string typed)
        {
            string name = BrandStore.Clean(typed).Trim();
            return name.Length > BrandCatalogue.NameMost ? name.Substring(0, BrandCatalogue.NameMost).Trim() : name;
        }

        /// <summary>A brand made from a shop, its first member. Null if the shop is under a brand or a chain, the name is empty or the save is full.</summary>
        public static Brand Create(BrandsState s, string name, string type, string place, string district, int day)
        {
            name = Name(name);
            if (name.Length == 0 || string.IsNullOrEmpty(place) || s.Brands.Count >= BrandCatalogue.BrandsMost || s.Member(place) != null || s.TakenAt(place) != null)
            {
                return null;
            }
            var b = new Brand { Id = s.NextId++, Name = name, Type = type ?? string.Empty, Flagship = place, Founded = day, Minimum = BrandCatalogue.OfferStandards };
            s.Brands.Add(b);
            s.Members.Add(new Member { Place = place, BrandId = b.Id, Joined = day, District = district });
            District(b, district, true);
            return b;
        }

        /// <summary>A shop of the brand's type joins it. False if it is under a brand or a chain already or the save is full.</summary>
        public static bool Join(BrandsState s, Brand b, string place, string type, string district, int day)
        {
            if (string.IsNullOrEmpty(place) || !string.Equals(b.Type, type, StringComparison.Ordinal) || s.Member(place) != null || s.TakenAt(place) != null
                || s.Members.Count >= BrandCatalogue.MembersMost)
            {
                return false;
            }
            s.Members.Add(new Member { Place = place, BrandId = b.Id, Joined = day, District = district });
            District(b, district, true);
            return true;
        }

        /// <summary>A shop leaves its brand. The flagship passes to the oldest member left.</summary>
        public static bool Leave(BrandsState s, string place)
        {
            Member m = s.Member(place);
            if (m == null)
            {
                return false;
            }
            s.Members.Remove(m);
            Brand b = s.Brand(m.BrandId);
            if (b != null && string.Equals(b.Flagship, place, StringComparison.Ordinal))
            {
                b.Flagship = string.Empty;
                for (int i = 0; i < s.Members.Count; i++)
                {
                    if (s.Members[i].BrandId == b.Id)
                    {
                        b.Flagship = s.Members[i].Place;
                        break;
                    }
                }
            }
            return true;
        }

        /// <summary>A brand is given up or sold: its members are plain shops again, its agreements and applicants gone.</summary>
        public static void Dissolve(BrandsState s, Brand b)
        {
            for (int i = s.Members.Count - 1; i >= 0; i--)
            {
                if (s.Members[i].BrandId == b.Id)
                {
                    s.Members.RemoveAt(i);
                }
            }
            for (int i = s.Franchises.Count - 1; i >= 0; i--)
            {
                if (s.Franchises[i].BrandId == b.Id)
                {
                    s.Franchises.RemoveAt(i);
                }
            }
            for (int i = s.Applicants.Count - 1; i >= 0; i--)
            {
                if (s.Applicants[i].BrandId == b.Id)
                {
                    s.Applicants.RemoveAt(i);
                }
            }
            s.Brands.Remove(b);
        }

        /// <summary>The mean of the brand's figures over the districts with a member shop, hundredths; <paramref name="districts"/> counts them.</summary>
        public static int MeanRec(BrandsState s, Brand b, out int districts)
        {
            long sum = 0;
            districts = 0;
            for (int i = 0; i < b.Districts.Count; i++)
            {
                if (Members(s, b, b.Districts[i].Name) > 0)
                {
                    sum += b.Districts[i].Rec;
                    districts++;
                }
            }
            return districts > 0 ? (int)(sum / districts) : 0;
        }

        /// <summary>That mean for a brand with member shops in two districts or more, else 0.</summary>
        public static int SpreadRec(BrandsState s, Brand b)
        {
            int districts;
            int mean = MeanRec(s, b, out districts);
            return districts >= BrandCatalogue.OfferMeanDistricts ? mean : 0;
        }

        /// <summary>Whether the brand may be offered: enough good member shops and enough recognition; <paramref name="good"/> counts the good shops.</summary>
        public static bool CanOffer(BrandsState s, Brand b, out int good)
        {
            good = 0;
            for (int i = 0; i < s.Members.Count; i++)
            {
                good += s.Members[i].BrandId == b.Id && s.Members[i].Standards >= BrandCatalogue.OfferStandards ? 1 : 0;
            }
            return good >= BrandCatalogue.OfferShops
                   && (b.City >= BrandCatalogue.OfferCityRec || Best(b) >= BrandCatalogue.OfferDistrictRec || SpreadRec(s, b) >= BrandCatalogue.OfferMeanRec);
        }

        /// <summary>What a brand campaign costs today: in one district, lots of the company's flyers for each shop there; city-wide, its radio spots.</summary>
        public static int PushCost(int kind, int shops, int index)
        {
            return kind == BrandCatalogue.PushCity
                ? CampaignRules.Cost((int)CampaignKind.Radio, index) * BrandCatalogue.PushCityTimes
                : CampaignRules.Cost((int)CampaignKind.Flyers, index) * PushLocalTimes * Math.Max(1, shops);
        }

        /// <summary>How long a brand campaign runs: as long as the company's own campaign of the same kind.</summary>
        public static int PushDays(int kind)
        {
            return CompanyCatalogue.CampaignDays[kind == BrandCatalogue.PushCity ? (int)CampaignKind.Radio : (int)CampaignKind.Flyers];
        }

        /// <summary>Booked today, it runs from the brand's next day.</summary>
        public static void BookPush(Brand b, int kind, string district)
        {
            b.Push = kind == BrandCatalogue.PushCity ? BrandCatalogue.PushCity : BrandCatalogue.PushLocal;
            b.PushFrom = b.Age + 1;
            b.PushEnd = b.PushFrom + PushDays(b.Push);
            b.PushDistrict = b.Push == BrandCatalogue.PushCity ? string.Empty : district ?? string.Empty;
        }

        /// <summary>The levy collected goes to a campaign first. Returns how much of the cost it covered.</summary>
        public static int FromFund(Brand b, int cost)
        {
            int used = Math.Min(Math.Max(0, b.Fund), Math.Max(0, cost));
            b.Fund -= used;
            return used;
        }
    }
}
