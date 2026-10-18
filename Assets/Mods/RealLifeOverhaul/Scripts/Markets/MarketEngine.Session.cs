using System;

namespace RealLifeOverhaul.Markets
{
    /// <summary>Part of the engine: the morning's results, the open, each step of the session, and the indices.</summary>
    public sealed partial class MarketEngine
    {
        private void Results(int day)
        {
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.ResultsDay == day && s.PendingQuarter >= 0)
                {
                    Say(s.Own != null ? ListingRules.Results(s, day, Year, State.Env) : MarketLife.Results(s, day, Year, _secret, State.Env));
                    if (s.Own != null)
                    {
                        OwnResults(s, day);
                    }
                    if (s.DeclaredCents > 0 && !TrustRules.Is(s))   // a property trust's distributions come every few days
                    {
                        Say(new MarketNews { Day = day, Kind = MarketNewsKind.Dividend, Stock = s.Id, A = s.DeclaredCents, B = s.RecordDay, C = s.PayDay });
                    }
                }
            }
        }

        // 09:30: the open

        // What the open's first pieces leave for its companies and its end.
        private MarketEnv _openEnv;
        private int _openFrom, _openShut, _openRate, _openIndex;
        private double _openNight, _openVariance, _openVs, _openMarketNews, _openMarketMood, _openMarketNewsVar;

        // The open's first piece: the city of the day; then every company's business since the last open.
        private void OpenBegin(int day)
        {
            MarketEnv env;
            if (Tape == null || !Tape.Env(day, out env))
            {
                env = _world.Env(day);
                Tape?.AddEnv(day, env);
            }
            _openRate = State.RateSeenBp;
            _openIndex = State.IndexLast;
            State.Env = env;
            _openEnv = env;
            int from = State.BusinessDay > 0 ? State.BusinessDay + 1 : day;
            _openFrom = Math.Max(from, day - 30);   // after a very long gap the last month is enough
            Begin(Part.OpenBusiness, day);
        }

        // The night: how long since the last session, and a crash or crazes that may begin with the morning.
        private void OpenNight(int day)
        {
            MarketEnv env = _openEnv;
            int shut = 1;
            for (int d = day - 1; d >= 1 && !MarketCatalogue.TradingDay(d); d--)
            {
                shut++;
            }
            double night = MarketCatalogue.OvernightShare * (1.0 + MarketCatalogue.WeekendExtra * (shut - 1));
            double variance = DayVariance(env);
            double vs = MarketCatalogue.VolScale(Year);
            double marketSd = MarketCatalogue.IndexDayVol * vs * Math.Sqrt(variance * night);
            double zMarket = MarketRandom.Fat(_secret, MarketRandom.OpenShock, MarketRandom.OpenTail, 0, 0);
            double zMood = MarketRandom.Normal(_secret, MarketRandom.OpenMood, 0, 0);
            double marketNews = marketSd * MarketNews * zMarket, marketMood = marketSd * MarketMood * zMood;
            State.DayZ = Math.Sqrt(night) * (MarketNews * zMarket + MarketMood * zMood);
            State.DayVar = night;
            if (Crashes(env))
            {
                double size = MarketCatalogue.CrashLeast + (MarketCatalogue.CrashMost - MarketCatalogue.CrashLeast) * MarketRandom.Unit(_secret, MarketRandom.CrashSize, 0, 0);
                // A jump of its own: the variance answers it below, not through the day's shock as well.
                marketNews -= size * (1.0 - MarketCatalogue.CrashMoodShare);
                marketMood -= size * MarketCatalogue.CrashMoodShare;
                State.Variance = Math.Min(MarketCatalogue.VarianceMost, State.Variance * 3.0);
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Crash, A = -(int)Math.Round(size * 10000.0) });
            }
            double marketNewsVar = marketSd * marketSd * (1.0 - MarketCatalogue.MarketMoodShare);
            for (int sec = 0; sec < Sectors; sec++)
            {
                double sd = MarketCatalogue.Sectors[sec].SectorVolBp / 10000.0 * vs * Math.Sqrt(SectorVariance(sec, env) * night);
                double z = MarketRandom.Fat(_secret, MarketRandom.OpenShock, MarketRandom.OpenTail, MarketRandom.MoodSector + sec, 0);
                double zs = MarketRandom.Normal(_secret, MarketRandom.OpenMood, MarketRandom.MoodSector + sec, 0);
                State.SectorDayZ[sec] = Math.Sqrt(night) * (SectorNews * z + SectorMood * zs);
                _sectorSd[sec] = sd;
                _sectorNews[sec] = sd * SectorNews * z;
                _sectorMood[sec] = sd * SectorMood * zs + MarketInputs.DayKickBp(env, (MarketSector)sec) / 10000.0 + Mania(sec, day);
            }
            // An election counted: the doubt is over.
            if (State.ElectionsSeen >= 0 && env.Elections > State.ElectionsSeen)
            {
                marketMood += MarketInputs.ElectionKickBp / 10000.0;
            }
            State.ElectionsSeen = env.Elections;
            _openShut = shut;
            _openNight = night;
            _openVariance = variance;
            _openVs = vs;
            _openMarketNews = marketNews;
            _openMarketMood = marketMood;
            _openMarketNewsVar = marketNewsVar;

            // A new session: every day's move is measured from the last close until the next open.
            MarketLife.DayBases(State, true);
            // A listing due today comes to the market, and the player's own company is priced or listed.
            if (State.ListingId > 0 && State.ListingDay <= day)
            {
                List(day);
            }
            OwnMorning(day, env);
        }

        // A company's open: its dividend out of the price, a split, the night's move.
        private void OpenCompany(Stock s, int day)
        {
            if (!s.Listed)
            {
                return;
            }
            MarketEnv env = _openEnv;
            int shut = _openShut;
            double night = _openNight, vs = _openVs, marketNews = _openMarketNews, marketMood = _openMarketMood, marketNewsVar = _openMarketNewsVar;
            // A dividend whose record day has passed goes out of the price.
            if (s.DeclaredCents > 0 && !s.ExDone && s.RecordDay > 0 && s.RecordDay < day)
            {
                s.ExDone = true;
                s.LastExDay = day;
            }
            if (s.SplitDay > 0 && s.SplitDay <= day)
            {
                MarketLife.Split(State, s, s.SplitRatio);
            }
            if (TrustRules.Is(s))
            {
                TrustRules.Declare(s, day, Year);   // a property trust's next distribution, as its last leaves the price
            }
            double beta = s.Def.Beta / 100.0;
            int sector = (int)s.Sector;
            double own = s.Def.IdioBp / 10000.0 * vs * Math.Sqrt(OwnVariance(_openVariance) * night) * (s.ResultsBoostDay == day ? Math.Sqrt(MarketCatalogue.ResultsDayVolScale) : 1.0);
            double z = MarketRandom.Fat(_secret, MarketRandom.OpenShock, MarketRandom.OpenTail, s.Id, 1);
            double zo = MarketRandom.Normal(_secret, MarketRandom.OpenMood, s.Id, 1);
            double founder = s.Own != null ? ListingRules.OpenNews(s, env) : 0.0;   // a player's company: what the city made of its founder yesterday
            s.LogFair = MarketLife.LogFairOf(s, env, day, Year);
            if (s.BidCents > 0)
            {
                double held = BidMarketShare * (beta * (marketNews + marketMood) + _sectorNews[sector] + _sectorMood[sector]) + BidOwnShare * own * z;
                s.Sent = s.Sent * Math.Max(0.0, 1.0 - BidKappa * night) + held + founder * BidOwnShare;
                s.Mis = s.Sent;
            }
            else
            {
                double news = beta * marketNews + _sectorNews[sector] + own * OwnNews * z
                              - 0.5 * (beta * beta * marketNewsVar + _sectorSd[sector] * _sectorSd[sector] * (1.0 - MarketCatalogue.SectorMoodShare)
                                       + own * own * (1.0 - MarketCatalogue.OwnMoodShare));
                double mood = beta * marketMood + _sectorMood[sector] + own * OwnMood * zo + founder;
                if (s.ListedDay == day && s.Daily.Count == 0)
                {
                    // A new listing's first trade, with the first morning's interest in it.
                    mood += MarketLife.IpoPopMean + MarketLife.IpoPopSpread * MarketRandom.Normal(_secret, MarketRandom.Listing, s.Id, 1);
                }
                double was = s.Sent;
                s.Sent = s.Sent * (1.0 - Kappa * night) + mood;
                s.Mis += news + (s.Sent - was);
            }
            if (s.LastBarCount >= 0 && s.BarCount > 0)
            {
                Array.Copy(s.Bars, s.LastBars, s.BarCount);
                s.LastBarCount = s.BarCount;
                s.LastBarsDay = day - shut;
            }
            s.BarCount = 0;
            int open = s.MidCents;
            s.Open = s.High = s.Low = s.Last = open;
            s.DayVolume = 0;
        }

        // The open's last piece: the index's open, the rate's news, the orders waiting for the open, the bots.
        private void OpenEnd(int day)
        {
            MarketEnv env = _openEnv;
            if (State.IndexBarCount > 0)
            {
                Array.Copy(State.IndexBars, State.IndexLastBars, State.IndexBarCount);
                State.IndexLastBarCount = State.IndexBarCount;
                State.IndexLastBarsDay = day - _openShut;
            }
            State.IndexBarCount = 0;
            Index(out int indexNow);
            State.IndexOpen = State.IndexHigh = State.IndexLow = State.IndexLast = indexNow;
            for (int sec = 0; sec < Sectors; sec++)
            {
                _sectorOpen[sec] = State.SectorLast[sec];
            }
            if (_openRate >= 0 && env.RateBp != _openRate && _openIndex > 0)
            {
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Rate, A = env.RateBp - _openRate, B = (int)((long)(indexNow - _openIndex) * 10000L / _openIndex) });
            }
            State.RateSeenBp = env.RateBp;
            OpenOrders(day);
            BotsOpen();   // stops the night went through, and floors
            Margin(day, MarketCatalogue.PhaseOpen);
        }

        // A company's business on every day of the calendar since the last open, its quarters.
        private void Business(Stock s, int day)
        {
            if (!s.Listed)
            {
                return;
            }
            int from = _openFrom;
            MarketEnv env = _openEnv;
            if (s.Own != null)
            {
                OwnBusiness(s, env, from, day);   // its business is its founder's books
                return;
            }
            double competition = Competition(s, env);
            for (int d = from; d <= day; d++)
            {
                int quarter = MarketCatalogue.QuarterOf(d, Year);
                if (quarter != s.Quarter)
                {
                    if (s.PendingQuarter >= 0)
                    {
                        // Results never given out (a long gap): out now, with the next quarter coming.
                        Say(MarketLife.Results(s, day, Year, _secret, env));
                    }
                    MarketLife.CloseQuarter(s, quarter, Year);
                    if (s.ResultsDay <= day)
                    {
                        s.ResultsDay = MarketTrading.NextTradingDay(day);   // this morning's results are already out
                    }
                }
                MarketLife.Business(s, env, Year, _secret, d, competition);
            }
        }

        // The share of a company's turnover the player's businesses in its trade take: half their takings there a year,
        // against all the listed companies in it.
        private double Competition(Stock s, in MarketEnv env)
        {
            if (s.Def.Trade == null || env.Trades <= 0 || env.TradeOf == null)
            {
                return 0.0;
            }
            int trade = Array.IndexOf(MarketInputs.Trades, s.Def.Trade);
            double sales = 0.0;
            for (int i = 0; i < env.Trades && i < env.TradeOf.Length; i++)
            {
                if (env.TradeOf[i] == trade)
                {
                    sales += env.TradeSales[i];
                }
            }
            if (sales <= 0.0)
            {
                return 0.0;
            }
            double all = 0.0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock other = State.Stocks[i];
                if (other.Listed && other.Def.Trade == s.Def.Trade)
                {
                    all += other.Rev;
                }
            }
            return all <= 0.0 ? 0.0 : Math.Min(MarketInputs.CompetitionMost, sales * Year * MarketInputs.PlayerShareTaken / all);
        }

        private double DayVariance(in MarketEnv env)
        {
            return State.Variance * MarketCatalogue.CycleVolatility[MarketValuation.Clamp(env.Cycle, 0, 3)] * (env.Campaign != 0 ? 1.1 : 1.0);
        }

        private double SectorVariance(int sector, in MarketEnv env)
        {
            return Math.Max(MarketCatalogue.VarianceLeast, State.SectorVar[sector] <= 0.0 ? 1.0 : State.SectorVar[sector])
                   * MarketCatalogue.CycleVolatility[MarketValuation.Clamp(env.Cycle, 0, 3)];
        }

        // A company's own moves grow in a nervous market too, by half as much.
        private static double OwnVariance(double marketVariance)
        {
            double m = 0.5 + 0.5 * Math.Sqrt(Math.Max(0.1, marketVariance));
            return m * m;
        }

        // Whether the market crashes this morning: likelier in a slowdown or recession, and when it is dear.
        private bool Crashes(in MarketEnv env)
        {
            double chance = MarketCatalogue.CrashChanceYear / TradingYear;
            if (env.Cycle == 3)
            {
                chance *= MarketCatalogue.CrashRecessionScale;
            }
            else if (env.Cycle == 2)
            {
                chance *= MarketCatalogue.CrashSlowdownScale;
            }
            if (Dearness() > 0.08)
            {
                chance *= MarketCatalogue.CrashDearScale;
            }
            return MarketRandom.Unit(_secret, MarketRandom.Crash, 0, 0) < chance;
        }

        // How far the large companies' prices are above what they are worth, on average.
        private double Dearness()
        {
            double sum = 0.0;
            int count = 0;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed && s.Size == MarketSize.Large)
                {
                    sum += s.Mis;
                    count++;
                }
            }
            return count > 0 ? sum / count : 0.0;
        }

        // A sector's craze may begin this morning or burst. While it runs its companies drift up; a burst gives back on the
        // morning what the drift was worth against the burst's chance, so riding it pays nothing on average.
        private double Mania(int sector, int day)
        {
            double perDay = 1.0 / Math.Max(5.0, (MarketCatalogue.ManiaDaysLeast + MarketCatalogue.ManiaDaysMost) / 2.0);
            if (State.ManiaDays[sector] > 0)
            {
                if (MarketRandom.Unit(_secret, MarketRandom.Mania, sector, 1) < perDay)
                {
                    State.ManiaDays[sector] = 0;
                    double back = MarketCatalogue.ManiaDrift / perDay;
                    State.ManiaAdded[sector] = 0.0;
                    State.Variance = Math.Min(MarketCatalogue.VarianceMost, State.Variance * 1.3);
                    State.SectorVar[sector] = Math.Min(MarketCatalogue.VarianceMost, Math.Max(1.0, State.SectorVar[sector]) * 2.5);
                    Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mania, A = sector, B = 2 });
                    return -back;
                }
                State.ManiaDays[sector]++;
                State.ManiaAdded[sector] += MarketCatalogue.ManiaDrift;
                return 0.0;   // the drift is added step by step
            }
            if (MarketRandom.Unit(_secret, MarketRandom.Mania, sector, 0) < MarketCatalogue.ManiaChanceYear / TradingYear)
            {
                State.ManiaDays[sector] = 1;
                State.ManiaAdded[sector] = 0.0;
                Say(new MarketNews { Day = day, Kind = MarketNewsKind.Mania, A = sector, B = 1 });
            }
            return 0.0;
        }

        // A step of the session

        private void Step(int day, int k)
        {
            MarketEnv env = State.Env;
            double w = _weight[k] * (1.0 - MarketCatalogue.OvernightShare);
            double variance = DayVariance(env);
            double vs = MarketCatalogue.VolScale(Year);
            double marketSd = MarketCatalogue.IndexDayVol * vs * Math.Sqrt(variance * w);
            double zm = MarketRandom.Fat(_secret, MarketRandom.MarketShock, MarketRandom.MarketTail, k, 0);
            double zmm = MarketRandom.Normal(_secret, MarketRandom.Mood, k, 0);
            double marketNews = marketSd * MarketNews * zm, marketMood = marketSd * MarketMood * zmm;
            double marketNewsVar = marketSd * marketSd * (1.0 - MarketCatalogue.MarketMoodShare);
            State.DayZ += Math.Sqrt(w) * (MarketNews * zm + MarketMood * zmm);
            State.DayVar += w;
            for (int sec = 0; sec < Sectors; sec++)
            {
                double sd = MarketCatalogue.Sectors[sec].SectorVolBp / 10000.0 * vs * Math.Sqrt(SectorVariance(sec, env) * w);
                double z = MarketRandom.Fat(_secret, MarketRandom.SectorShock, MarketRandom.SectorTail, k, sec);
                double zs = MarketRandom.Normal(_secret, MarketRandom.Mood, k, MarketRandom.MoodSector + sec);
                State.SectorDayZ[sec] += Math.Sqrt(w) * (SectorNews * z + SectorMood * zs);
                _sectorSd[sec] = sd;
                _sectorNews[sec] = sd * SectorNews * z;
                _sectorMood[sec] = sd * SectorMood * zs + (State.ManiaDays[sec] > 0 ? MarketCatalogue.ManiaDrift / Steps : 0.0);
            }
            double ownScale = vs * Math.Sqrt(OwnVariance(variance) * w);
            double fade = Kappa * w, bidFade = BidKappa * w;
            double decay = 1.0 - 1.0 / MarketCatalogue.ImpactMemorySteps;
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                double beta = s.Def.Beta / 100.0;
                int sector = (int)s.Sector;
                double own = s.Def.IdioBp / 10000.0 * ownScale * (s.ResultsBoostDay == day ? Math.Sqrt(MarketCatalogue.ResultsDayVolScale) : 1.0);
                double z = MarketRandom.Fat(_secret, MarketRandom.OwnShock, MarketRandom.OwnTail, k, s.Id);
                double before = s.LogFair + s.Mis;
                double sd;
                if (s.BidCents > 0)
                {
                    double held = BidMarketShare * (beta * (marketNews + marketMood) + _sectorNews[sector] + _sectorMood[sector]) + BidOwnShare * own * z;
                    s.Sent = s.Sent * (1.0 - bidFade) + held;
                    s.Mis = s.Sent;
                    sd = Math.Sqrt(BidMarketShare * BidMarketShare * (beta * beta * marketSd * marketSd + _sectorSd[sector] * _sectorSd[sector]) + BidOwnShare * BidOwnShare * own * own);
                }
                else
                {
                    double zo = MarketRandom.Normal(_secret, MarketRandom.Mood, k, MarketRandom.MoodOwn + s.Id);
                    double news = beta * marketNews + _sectorNews[sector] + own * OwnNews * z
                                  - 0.5 * (beta * beta * marketNewsVar + _sectorSd[sector] * _sectorSd[sector] * (1.0 - MarketCatalogue.SectorMoodShare)
                                           + own * own * (1.0 - MarketCatalogue.OwnMoodShare));
                    double mood = beta * marketMood + _sectorMood[sector] + own * OwnMood * zo;
                    double was = s.Sent;
                    s.Sent = s.Sent * (1.0 - fade) + mood;
                    s.Mis += news + (s.Sent - was);
                    sd = Math.Sqrt(beta * beta * marketSd * marketSd + _sectorSd[sector] * _sectorSd[sector] + own * own);
                }
                double after = s.LogFair + s.Mis;
                Bar(s, day, k, before, after, sd);
                if (s.Impact > 0.0)
                {
                    s.Impact = s.Impact > 1.0 ? s.Impact * decay : 0.0;
                }
            }
            Index(out int index);
            int prev = State.IndexBarCount > 0 ? State.IndexBars[State.IndexBarCount - 1].C : State.IndexOpen;
            if (State.IndexBarCount < State.IndexBars.Length)
            {
                State.IndexBars[State.IndexBarCount++] = new Candle(day, prev, Math.Max(prev, index), Math.Min(prev, index), index, 0);
            }
            State.IndexHigh = Math.Max(State.IndexHigh, index);
            State.IndexLow = Math.Min(State.IndexLow, index);
            State.IndexLast = index;
            RestingOrders(day, k);
            WorkedOrders(day, k);   // MarketEngine.Invest.cs
            BotsStep(day, k);
            Margin(day, MarketCatalogue.PhaseOpen + k);
        }

        // The step's candle: its ends, the extremes of a random walk tied to both (drawn exactly), and the shares traded.
        private void Bar(Stock s, int day, int k, double from, double to, double sd)
        {
            double width = Math.Abs(to - from);
            double var = sd * sd;
            double up = Math.Max(from, to) + Math.Max(0.0, (Math.Sqrt(width * width - 2.0 * var * Math.Log(MarketRandom.Unit(_secret, MarketRandom.BarHigh, k, s.Id))) - width) * 0.5);
            double down = Math.Min(from, to) - Math.Max(0.0, (Math.Sqrt(width * width - 2.0 * var * Math.Log(MarketRandom.Unit(_secret, MarketRandom.BarLow, k, s.Id))) - width) * 0.5);
            int o = Cents(from), c = Cents(to);
            int h = Math.Max(Math.Max(o, c), Cents(up)), l = Math.Min(Math.Min(o, c), Cents(down));
            double normal = sd > 0.0 ? width / sd : 0.0;
            long v = (long)(s.Adv * _weight[k] * (0.7 + 0.45 * normal) * Math.Exp(0.3 * MarketRandom.Normal(_secret, MarketRandom.Volume, k, s.Id)) * (s.ResultsBoostDay == day ? 2.5 : 1.0));
            if (s.BarCount < s.Bars.Length)
            {
                s.Bars[s.BarCount++] = new Candle(day, o, h, l, c, v);
            }
            s.High = Math.Max(s.High, h);
            s.Low = Math.Min(s.Low, l);
            s.Last = c;
            s.DayVolume += v;
        }

        private static int Cents(double logPrice)
        {
            double cents = Math.Exp(logPrice) * 100.0;
            return cents >= int.MaxValue ? int.MaxValue : Math.Max(1, (int)Math.Round(cents));
        }

        private bool[] _capped;   // MarketIndex's scratch

        // The city's index and the sectors': listed companies' free float value (capped for the city's) over their divisors.
        // An old save has its divisors set right first, so that it does not jump.
        private void Index(out int index)
        {
            if (!State.Weighted)
            {
                Rebase();
            }
            for (int sec = 0; sec < Sectors; sec++)
            {
                _sectorValue[sec] = 0.0;
            }
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (s.Listed)
                {
                    _sectorValue[(int)s.Sector] += MarketIndex.Value(s);
                }
            }
            double all = MarketIndex.Total(State.Stocks, null, null, ref _capped);
            if (State.IndexDivisor <= 0.0)
            {
                State.IndexDivisor = all / 1000.0;
            }
            index = State.IndexDivisor > 0.0 ? (int)Math.Round(all / State.IndexDivisor * 100.0) : 0;
            for (int sec = 0; sec < Sectors; sec++)
            {
                if (State.SectorDivisor[sec] <= 0.0 && _sectorValue[sec] > 0.0)
                {
                    State.SectorDivisor[sec] = _sectorValue[sec] / 1000.0;
                }
                State.SectorLast[sec] = State.SectorDivisor[sec] > 0.0 ? (int)Math.Round(_sectorValue[sec] / State.SectorDivisor[sec] * 100.0) : 0;
            }
        }

        // A company joins or leaves the indices: their divisors change so they do not jump. Called before the change.
        private void Reweigh(Stock s, bool joining)
        {
            Index(out int index);
            double value = MarketIndex.Value(s);
            if (index > 0 && State.IndexDivisor > 0.0)
            {
                double before = MarketIndex.Total(State.Stocks, null, null, ref _capped);
                double after = joining ? MarketIndex.Total(State.Stocks, null, s, ref _capped) : MarketIndex.Total(State.Stocks, s, null, ref _capped);
                State.IndexDivisor = before > 0.0 && after > 0.0 ? State.IndexDivisor * after / before : 0.0;
            }
            int sec = (int)s.Sector;
            int level = State.SectorLast[sec];
            if (level > 0 && State.SectorDivisor[sec] > 0.0)
            {
                double all = State.SectorDivisor[sec] * level / 100.0;
                double after = all + (joining ? value : -value);
                State.SectorDivisor[sec] = after > 0.0 ? after / (level / 100.0) : 0.0;
            }
        }

        // An old save's divisors set so the index and every sector stand where they stood when every share counted, now
        // weighed by float and capped. Once, at its first step.
        private void Rebase()
        {
            State.Weighted = true;
            double oldAll = 0.0;
            var oldSector = new double[Sectors];
            var newSector = new double[Sectors];
            for (int i = 0; i < State.Stocks.Count; i++)
            {
                Stock s = State.Stocks[i];
                if (!s.Listed)
                {
                    continue;
                }
                double whole = s.Shares * s.Mid;
                oldAll += whole;
                oldSector[(int)s.Sector] += whole;
                newSector[(int)s.Sector] += MarketIndex.Value(s);
            }
            double newAll = MarketIndex.Total(State.Stocks, null, null, ref _capped);
            if (oldAll > 0.0 && newAll > 0.0 && State.IndexDivisor > 0.0)
            {
                State.IndexDivisor *= newAll / oldAll;
            }
            for (int sec = 0; sec < Sectors; sec++)
            {
                if (oldSector[sec] > 0.0 && newSector[sec] > 0.0 && State.SectorDivisor[sec] > 0.0)
                {
                    State.SectorDivisor[sec] *= newSector[sec] / oldSector[sec];
                }
            }
        }
    }
}
