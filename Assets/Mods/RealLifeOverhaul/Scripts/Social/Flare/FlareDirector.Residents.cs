using System;
using System.Collections.Generic;
using System.Globalization;
using System.Threading;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.Population;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// Flare's director, the city's residents (Social/Flare/City; DRAFT2.md section 4): their follows, what they hear of each business and the
    /// attention that draws them there from its rivals, reviews from real customers, ads, the funnel of each post and the voices. It runs under
    /// Living population and its option Flare in the city, while the population runs on this machine; then Flare's talk about a business leaves
    /// business pressure's figure (FlareLinks.Ambient) and is the population's attention instead (PopulationAttention, Wiring.FlareCityLinks). Off,
    /// or on a guest following the host, nothing of it runs and Flare is exactly as before. A fault stands this part down for the session, never
    /// Flare. COST: nothing per frame; once a second a few comparisons; the day's work after the population's day, in 2 ms slices (DaySpread).
    /// </summary>
    public sealed partial class FlareDirector
    {
        private FlareCityState _city;
        private readonly FlareCityPass _cityPass = new FlareCityPass();
        private DistrictSaveZip _cityZip;
        private bool _cityReading, _cityBroken, _cityQueued;
        private volatile bool _cityReadDone;
        private volatile FlareCityState _cityRead;
        private string _cityWhy;
        private Func<bool> _cityStep;
        private int _cityVoiced;                 // the voices' change count the city's people were last made from
        private int _cityVoices;
        private readonly List<CityPost> _cityLater = new List<CityPost>(16);
        private readonly List<FlareFunnel> _cityLaterFunnels = new List<FlareFunnel>(16);
        private static readonly int HotCityStep = HotSpots.Slot("Social: FlareDirector.CityStep");

        /// <summary>True on a guest that follows the host's residents (part R): its cards show the host's figures.</summary>
        public bool CityFollowing => _cityFollowed != null && !CityOn;

        /// <summary>True while the residents' side runs here.</summary>
        public bool CityOn => Running && _city != null && !_cityBroken;

        /// <summary>The residents' side, or null while it does not run.</summary>
        public FlareCityState City => CityOn ? _city : null;

        /// <summary>The population's business slots and indexes the residents' side reads (the day's pass's).</summary>
        public FlareCityPass CityPass => _cityPass;

        /// <summary>Whether the options want the residents' side (the population must also run here).</summary>
        public static bool CityWanted => Settings.LivingPopulation && Settings.LivingFlare;

        private static PopulationState Residents => FlareBridges.Ask(FlareBridges.Residents, null);

        // Once a second, from the timer: started when wanted and the population runs, stood down when not.
        private void CitySecond()
        {
            PopulationState s = Running && CityWanted && !_cityBroken ? Residents : null;
            if (s == null)
            {
                if (_city != null || _cityReading)
                {
                    CityStop(true, !CityWanted ? "the option is off" : "the population does not run here");
                }
                return;
            }
            if (_city == null)
            {
                CityStart(s);
                return;
            }
            if (!_cityQueued && _city.Day < s.Day && Save != null)
            {
                CityQueue(s);   // a day the population's event did not bring to Flare (a load, a skip)
            }
        }

        // The save's line read on a worker, then the residents' side begins with the day's pass; with no line, the follows are made.
        private void CityStart(PopulationState s)
        {
            if (!_cityReading)
            {
                Dictionary<string, string> data = FlareCityGame.Data();
                string line = data != null && data.TryGetValue(FlareCityStore.Key, out string text) ? text : null;
                _cityReading = true;
                _cityReadDone = false;
                _cityRead = null;
                if (string.IsNullOrEmpty(line))
                {
                    _cityWhy = "no line in this save";
                    _cityReadDone = true;
                    return;
                }
                ThreadPool.QueueUserWorkItem(_ =>
                {
                    try
                    {
                        FlareCityState read = FlareCityStore.Decode(line, out int version);
                        _cityWhy = read != null ? "version " + version : "a line it cannot read (left in the save until the next save)";
                        _cityRead = read;
                    }
                    catch (Exception e)
                    {
                        _cityWhy = e.GetType().Name;
                    }
                    _cityReadDone = true;
                });
                return;
            }
            if (!_cityReadDone)
            {
                return;
            }
            _cityReading = false;
            FlareCityState c = _cityRead ?? new FlareCityState { Seed = s.Seed };
            _cityRead = null;
            _city = c;
            Logger?.Info("Flare in the city: " + (c.Generated >= 0 ? "read (" + _cityWhy + "): " + c.Accounts.Count + " accounts, " + c.Funnels.Count + " measured posts, " + c.Ads.Count + " ads"
                                                    : _cityWhy + "; every adult resident's follows are made from who they are") + "; the residents' day runs in slices from now.");
            CityQueue(s);
        }

        /// <summary>The population lived a day (CityPopulation.DayLived, through the wiring): the residents' day is queued after it.</summary>
        public void ResidentsDayLived()
        {
            try
            {
                PopulationState s = CityOn ? Residents : null;
                if (s != null)
                {
                    CityQueue(s);
                }
            }
            catch (Exception e)
            {
                CityFault("the population's day", e);
            }
        }

        private void CityQueue(PopulationState s)
        {
            if (_cityQueued || _city == null || Save == null)
            {
                return;
            }
            FillCityInputs(s);
            _cityPass.Begin(_city, s, FlareBridges.Ask(FlareBridges.Pools, null), s.Day);
            _cityQueued = true;
            DaySpread.Shared.Add(this, "Flare: the residents' day", _cityStep, Time.frameCount, HotCityStep);
        }

        // A slice of the residents' day (DaySpread plays it, a frame at a time).
        private bool CityStep()
        {
            if (_city == null || !Running || _cityBroken)
            {
                _cityPass.Stop();
                _cityQueued = false;
                return true;
            }
            try
            {
                WorkSlice slice = WorkSlice.Of(FlareCityCatalogue.SliceMs, 1);
                if (!_cityPass.Step(ref slice))
                {
                    return false;
                }
                _cityQueued = false;
                CityDayDone();
                return true;
            }
            catch (Exception e)
            {
                _cityQueued = false;
                CityFault("the residents' day", e);
                return true;
            }
        }

        // The day done: the posts that waited for it heard, the city's people made again if a voice changed, one log line.
        private void CityDayDone()
        {
            FlareCityPass p = _cityPass;
            if (p.Changed > 0)
            {
                _cityVoices++;
                if (_file != null)
                {
                    EnsureCitizens(_file);
                }
            }
            HearLater();
            PopulationState residents = Residents;
            if (residents != null)
            {
                CreatorDay(residents, Save != null ? Save.Day : p.Day);   // offers, the paid posts due and a creator income (FlareDirector.Creator)
                if (p.Started != null)
                {
                    TrendNotice(p.Started, residents);   // the city's own talk started a trend today (FlareDirector.Trends)
                }
            }
            Logger?.Info("Flare in the city: day " + p.Day + (p.Placed >= 0 ? ", " + p.Placed + " residents' follows read back" : string.Empty)
                         + (p.Generated > 0 ? ", " + p.Generated + " residents' first follows made" : string.Empty) + ": " + _city.CityFollowers + " residents follow you"
                         + (p.Reconciled != 0 ? " (" + (p.Reconciled > 0 ? "+" : string.Empty) + p.Reconciled + " with the account)" : string.Empty) + ", " + p.Reviewed + " reviews by customers, "
                         + p.Followed + " new followers of businesses from their visits, " + p.Advertised + " ad(s) shown, " + p.Heard + " post(s) heard, " + p.Changed
                         + " voice(s) new; Flare's attention "
                         + (p.Lifted >= 0 ? "brought " : "kept away ") + Math.Abs(p.Lifted) / 100 + " of takings yesterday.");
            if (Meta.Dev.Verbose)
            {
                CityCheck(p);
            }
            if (GoingPublic.IsHost)
            {
                SharedCity.Changed(FlareCityShare.Part);   // the guests hear the day's voices and their businesses' figures
            }
            Changed();
        }

        // The posts heard while the day's indexes were being made.
        private void HearLater()
        {
            if (_cityLater.Count == 0 || !CityOn || _cityPass.Busy)
            {
                return;
            }
            PopulationState s = Residents;
            for (int i = 0; s != null && i < _cityLater.Count; i++)
            {
                HearNow(s, _cityLater[i], _cityLaterFunnels[i]);
            }
            _cityLater.Clear();
            _cityLaterFunnels.Clear();
        }

        // The self-check with Developer diagnostics on: what the day measured against what is wanted, once a day.
        private void CityCheck(FlareCityPass p)
        {
            PopulationState s = Residents;
            if (s == null)
            {
                return;
            }
            int adults = 0;
            for (int d = 0; d < _city.Adults.Length; d++)
            {
                adults += _city.Adults[d];
            }
            int want = FlareCityRules.PlayerCityTarget(_file.Followers, (int)_file.Tier, adults);
            bool followers = Math.Abs(_city.CityFollowers - want) <= Math.Max(5, want / 50);
            long attention = 0;
            int attended = 0;
            for (int b = 0; b < _city.BusinessCount; b++)
            {
                attention += _city.Attention[b];
                attended += _city.Attention[b] != 0 ? 1 : 0;
            }
            Logger?.Info("Flare check (the residents' day): " + _city.CityFollowers + " residents follow you against the account's city share of " + want + " (" + adults + " adults), "
                         + attended + " businesses with attention for tomorrow (" + (attended > 0 ? attention / attended : 0) + " thousandths on average), "
                         + p.Lifted / 100 + " of takings moved yesterday: " + (followers ? "PASS" : "FAIL (the city's followers are off the account's share)"));
        }

        // What the day's pass reads of the rest of Flare: the player's followers and tier, districts and businesses, the influencers, a scandal.
        private void FillCityInputs(PopulationState s)
        {
            CityInputs i = _cityPass.Inputs;
            BusinessTable bt = s.Businesses;
            int districts = s.Districts.Count;
            i.Day = s.Day;
            i.PlayerFollowers = _file.Followers;
            i.PlayerTier = (int)_file.Tier;
            i.Busy = FlareRules.Clamp(Settings.FlareBusy, 0, 2);
            i.Scandal = CityScandal();
            if (i.PlayerDistricts.Length != districts)
            {
                i.PlayerDistricts = new bool[districts];
            }
            Array.Clear(i.PlayerDistricts, 0, districts);
            i.PlayerBusinessCount = 0;
            for (int b = 0; b < bt.Count; b++)
            {
                if (bt.Owner[b] != (byte)Owner.Player || !bt.Seen[b])
                {
                    continue;
                }
                if (bt.District[b] < districts)
                {
                    i.PlayerDistricts[bt.District[b]] = true;
                }
                if (i.PlayerBusinessCount == i.PlayerBusinesses.Length)
                {
                    Array.Resize(ref i.PlayerBusinesses, Math.Max(8, i.PlayerBusinesses.Length * 2));
                }
                i.PlayerBusinesses[i.PlayerBusinessCount++] = b;
            }
            HouseholdTable hh = s.Households;
            for (int h = 0; h < hh.Count; h++)
            {
                if (hh.Alive(h) && (hh.Flags[h] & HouseholdFlags.Players) != 0 && hh.District[h] < districts)
                {
                    i.PlayerDistricts[hh.District[h]] = true;
                }
            }
            i.InfluencerCount = 0;
            for (int k = 0; k < _file.Influencers.Count; k++)
            {
                FlareInfluencer inf = _file.Influencers[k];
                if (!inf.Active)
                {
                    continue;
                }
                if (i.InfluencerCount == i.Influencers.Length)
                {
                    Array.Resize(ref i.Influencers, Math.Max(8, i.Influencers.Length * 2));
                }
                i.Influencers[i.InfluencerCount++] = new CityInfluencer
                {
                    Account = _city.Account("i:" + inf.Id.ToString(CultureInfo.InvariantCulture)), Kind = FlareCityRules.KindOfNiche(inf.Niche),
                    District = inf.District != null ? s.DistrictOf(inf.District) : -1, Followers = inf.Followers
                };
            }
            if (_cityPass.Bears.Length != districts)
            {
                _cityPass.Bears = new int[districts];
            }
            FlareCityGame.Bears(s, _cityPass.Bears);
        }

        // The player is in a scandal nobody answered: an arrest that trended this week, with no honest post about it since.
        private bool CityScandal()
        {
            int day = Save != null ? Save.Day : 0;
            if (_file == null)
            {
                return false;
            }
            bool arrest = _file.ArrestDay >= 0 && day - _file.ArrestDay <= FlareCityCatalogue.ScandalDays;
            FlarePost answer = arrest ? _file.LastOwn(FlareSource.Arrested) : null;
            if (arrest && (answer == null || answer.Day < _file.ArrestDay || answer.Tone != FlareTone.Honest))
            {
                return true;
            }
            int pileOn = _city != null ? _city.ScandalDay : -1;
            return pileOn >= 0 && day - pileOn <= FlareCityCatalogue.ScandalDays && !HonestSince(pileOn);
        }

        // An honest post of the player's on or after a day: what answers a pile-on.
        private bool HonestSince(int day)
        {
            for (int i = _file.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = _file.Posts[i];
                if (p.Author == FlareAuthor.Player && p.Day >= day && p.Tone == FlareTone.Honest && !p.Bad && p.Source != FlareSource.Reply)
                {
                    return true;
                }
            }
            return false;
        }

        // ---------------------------------------------------------------- posts heard by the residents

        /// <summary>A post of the player's heard by the city's residents: measured in the funnel, its city followers won among those it reached.</summary>
        private void CityPlayerPost(FlarePost p)
        {
            if (!CityOn || p == null || p.Author != FlareAuthor.Player)
            {
                return;
            }
            CityPost post = CityPostOf(p, FlareCityState.PlayerAccount, p.Bad ? -1 : 1);
            post.Gain = FlareCityRules.CityGain(p.Gained, (int)_file.Tier);
            post.Measured = true;
            _lastHeard = CityHear(post, CityFunnel(FunnelKind.Post, p.Id, p.Place, p.Day));
            _lastHeardPost = p.Id;
        }

        private HeardReport _lastHeard;   // the player's last post as the residents heard it, for its market and council effects (CityInfluence)
        private int _lastHeardPost = -1;

        /// <summary>The player's post as the residents heard it now: its report, or none when it is still to be heard (the day's indexes being made).</summary>
        private bool HeardNow(FlarePost p, out HeardReport r)
        {
            r = _lastHeard;
            return CityOn && p != null && _lastHeardPost == p.Id && r.Seen > 0;
        }

        /// <summary>A review or an announcement heard: by its author's followers (the player's, a voice's), good or bad by its stars.</summary>
        private void CityReviewPost(FlarePost p)
        {
            if (!CityOn || p == null || p.Place == null)
            {
                return;
            }
            int account = p.Author == FlareAuthor.Player ? FlareCityState.PlayerAccount : p.Author == FlareAuthor.Citizen ? VoiceAccount(p.Who)
                : p.Author == FlareAuthor.Network && p.AuthorId != null ? _city.Account("p:" + p.AuthorId) : -1;
            int sign = p.Source == FlareSource.Review ? (p.Arg >= 4 ? 1 : p.Arg <= 2 ? -1 : 0) : 1;
            if (sign == 0)
            {
                return;   // three stars: nobody remembers it either way
            }
            CityPost post = CityPostOf(p, account, sign);
            if (p.Author == FlareAuthor.Player)
            {
                post.Measured = true;
                CityHear(post, CityFunnel(FunnelKind.Post, p.Id, p.Place, p.Day));
            }
            else
            {
                CityHear(post, null);
            }
        }

        /// <summary>
        /// Talk about a business with no post of its own (an influencer's visit, a hire that backfired, an inspection): heard by an account's
        /// followers, or with none by the district who come across it, good or bad. False when the residents' side does not run (the old talk then).
        /// </summary>
        private bool CityTalk(string place, int sign, string account, int id, int seed)
        {
            if (!CityOn || place == null)
            {
                return false;
            }
            PopulationState s = Residents;
            int b = s != null ? _cityPass.BusinessOfPlace(place) : -1;
            if (b < 0)
            {
                return false;
            }
            var post = new CityPost
            {
                Id = id, Account = account != null ? _city.Account(account) : -1, Business = b, Kind = s.Businesses.Kind[b], Sign = sign, Quality = 1000, Fit = 1000,
                District = s.Businesses.District[b], Seed = seed
            };
            CityHear(post, null);
            return true;
        }

        /// <summary>
        /// A polite answer to a poor review of the player's business: what put residents off fades faster from now on (FlareCityRules.Answer), and
        /// the answer is heard a little itself. False when the residents' side does not run (the old talk then).
        /// </summary>
        private bool CityAnswer(string place, FlarePost reply)
        {
            int b = CityOn && place != null ? _cityPass.BusinessOfPlace(place) : -1;
            if (b < 0 || b >= _city.BusinessCount)
            {
                return false;
            }
            FlareCityRules.Answer(_city, b);
            CityPost post = CityPostOf(reply, FlareCityState.PlayerAccount, 1);
            post.Quality = FlareCityCatalogue.AnswerQuality;
            CityHear(post, null);
            return true;
        }

        // The account whose followers hear of a complaint: the player's, a voice's or a session player's; null for the city's own talk.
        private static string ReporterAccount(string reporter)
        {
            if (string.IsNullOrEmpty(reporter))
            {
                return null;
            }
            if (reporter == "me")
            {
                return "me";
            }
            if (reporter[0] == 'c' && int.TryParse(reporter.Substring(1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int voice))
            {
                return "v:" + voice.ToString(CultureInfo.InvariantCulture);
            }
            return reporter.StartsWith("p:", StringComparison.Ordinal) ? reporter : null;
        }

        // The city's businesses' own posts of a day (FlareLiveRules.CityDay), each heard by the business's followers (parity: the same rules as the player's).
        private void BusinessPostsHeard(int day)
        {
            if (!CityOn)
            {
                return;
            }
            for (int i = 0; i < _file.Posts.Count; i++)
            {
                FlarePost p = _file.Posts[i];
                if (p.Day != day || p.Author != FlareAuthor.Business || p.Source != FlareSource.BusinessPost || p.Place == null)
                {
                    continue;
                }
                string key = FlareCityPass.PlaceToKey(p.Place);
                if (key != null)
                {
                    CityHear(CityPostOf(p, _city.Account(FlareCityRules.BusinessKey(key)), 1), null);
                }
            }
        }

        private CityPost CityPostOf(FlarePost p, int account, int sign)
        {
            PopulationState s = Residents;
            int b = s != null && p.Place != null ? _cityPass.BusinessOfPlace(p.Place) : -1;
            int district = s != null && p.District != null ? s.DistrictOf(p.District) : -1;
            return new CityPost
            {
                Id = p.Id, Account = account, Business = b, Kind = b >= 0 ? s.Businesses.Kind[b] : -1, Sign = b >= 0 ? sign : 0, Quality = p.Quality > 0 ? p.Quality : 1000,
                Fit = FlareRules.ToneFit(p.Source, p.Tone), Viral = p.Viral, District = district, Seed = p.Seed
            };
        }

        private int VoiceAccount(int who)
        {
            return who >= 0 && who < FlareLiveRules.Citizens ? _city.AccountOf("v:" + who.ToString(CultureInfo.InvariantCulture)) : -1;
        }

        // Heard now, or once the day's indexes are made (then an empty report).
        private HeardReport CityHear(CityPost post, FlareFunnel funnel)
        {
            if (!CityOn)
            {
                return default(HeardReport);
            }
            PopulationState s = Residents;
            if (s == null || _cityPass.Busy || _city.Indexed < 0)
            {
                if (_cityLater.Count < FlareCityCatalogue.PostsDayMost)
                {
                    _cityLater.Add(post);
                    _cityLaterFunnels.Add(funnel);
                }
                return default(HeardReport);
            }
            return HearNow(s, post, funnel);
        }

        private HeardReport HearNow(PopulationState s, CityPost post, FlareFunnel funnel)
        {
            HeardReport r = default(HeardReport);
            try
            {
                if (post.Business >= _city.BusinessCount)
                {
                    post.Business = -1;   // a business newer than the day's figures: its talk counts from the next day
                }
                r = FlareCityRules.Hear(_city, s, post, Save != null ? Save.Day : s.Day, _cityPass.AccountBiz, _cityPass.Scratch, funnel);
                if (post.Business >= 0 && _cityPass.Weights.Length > 0)
                {
                    _city.Attention[post.Business] = FlareCityRules.AttentionOf(_city, s, post.Business, _cityPass.Weights);
                }
                if (post.Measured || Meta.Dev.Verbose)
                {
                    Logger?.Info("Flare in the city: post " + post.Id + " seen by " + r.Seen + " residents (" + r.Shared + " shared it, " + r.Discovered + " came across it), " + r.Interested
                                 + " interested" + (r.Followed > 0 ? ", " + r.Followed + " followed" : string.Empty)
                                 + (post.Business >= 0
                                     ? "; " + r.Impressions / 1000 + " now remember the business, its attention " + _city.Attention[post.Business] + " thousandths" : string.Empty) + ".");
                }
            }
            catch (Exception e)
            {
                CityFault("a post heard", e);
            }
            return r;
        }

        /// <summary>A post's or an ad's funnel record, new (the oldest goes past the most kept).</summary>
        private FlareFunnel CityFunnel(FunnelKind kind, int id, string place, int day)
        {
            FlareFunnel f = _city.Funnel(kind, id);
            if (f != null)
            {
                return f;
            }
            while (_city.Funnels.Count >= FlareCityCatalogue.FunnelsMost)
            {
                _city.Funnels.RemoveAt(0);
            }
            f = new FlareFunnel { Kind = kind, Post = id, Place = place, Day = day };
            _city.Funnels.Add(f);
            return f;
        }

        // ---------------------------------------------------------------- the attention source and the talk Flare keeps in business pressure

        /// <summary>
        /// The population's attention source (Wiring.FlareCityLinks registers it): what residents heard of a business, thousandths of its pull, as the
        /// population asks once a game day; the pools' habit as it stands is taken at the day's first ask, for the exact lift the day after.
        /// </summary>
        public static int AttentionOf(BuildingRegistration reg)
        {
            FlareDirector d = Instance;
            FlareCityState c = d != null ? d.City : null;
            PopulationState s = c != null ? Residents : null;
            if (s == null || reg == null || reg.Address == null)
            {
                return 0;
            }
            if (c.HabitFor != s.Day)
            {
                FlareCityRules.TakeHabit(c, s.Businesses, s.Day);
                Array.Clear(c.Given, 0, c.Given.Length);
            }
            int b = s.BusinessOfKey(FlareCityGame.KeyOf(reg));
            if (b < 0 || b >= c.BusinessCount)
            {
                return 0;
            }
            int a = c.Attention[b];
            c.Given[b] = a;
            return a;
        }

        // ---------------------------------------------------------------- ads

        /// <summary>The players' ad's targets, as the Business tab offers them: the business's district or the whole city, its customers' interest or anybody, an age band, a class.</summary>
        public FlareAd AdOf(string place, int target, int size, int days)
        {
            PopulationState s = Residents;
            int b = s != null ? _cityPass.BusinessOfPlace(place) : -1;
            BuildingRegistration reg = FlareGame.Business(place);
            var ad = new FlareAd
            {
                Place = place, Name = reg != null ? FlareGame.Name(reg) : place, Reach = FlareCityCatalogue.AdReach[FlareRules.Clamp(size, 0, FlareCityCatalogue.AdReach.Length - 1)],
                Days = FlareCityCatalogue.AdDays[FlareRules.Clamp(days, 0, FlareCityCatalogue.AdDays.Length - 1)], From = Save != null ? Save.Day : 0
            };
            int kind = b >= 0 ? s.Businesses.Kind[b] : -1;
            int district = b >= 0 ? s.Businesses.District[b] : -1;
            switch (FlareRules.Clamp(target, 0, FlareCityCatalogue.AdTargets - 1))
            {
                case 0: ad.District = district; ad.Interest = kind; break;          // its own district's residents who care for what it sells
                case 1: ad.Interest = kind; break;                                   // the whole city's who care for it
                case 2: ad.District = district; break;                               // its district, everybody
                case 3: ad.Interest = kind; ad.Age = 1; break;                       // the young who care for it
                default: ad.Interest = kind; ad.Class = (int)PopClass.Upper; break;   // the well off who care for it
            }
            return ad;
        }

        /// <summary>What an ad would cost and reach today: false with the reason's key when it cannot be run.</summary>
        public bool AdQuote(FlareAd ad, out long cost, out int target, out string why)
        {
            cost = 0;
            target = 0;
            why = null;
            PopulationState s = CityOn ? Residents : null;
            if (s == null || _city.Indexed < 0)
            {
                why = "reallife_flare_ad_why_off";
                return false;
            }
            if (FlareGame.Business(ad.Place) == null || _cityPass.BusinessOfPlace(ad.Place) < 0)
            {
                why = "reallife_flare_ad_why_business";
                return false;
            }
            target = FlareCityRules.TargetSize(_city, s, ad, s.Day);
            if (target <= 0)
            {
                why = "reallife_flare_ad_why_nobody";
                return false;
            }
            cost = FlareCityRules.AdCost(Math.Min(ad.Reach, target), ad.Days, FlareCityGame.PriceLevel);
            return true;
        }

        /// <summary>The player runs an ad: paid now at its business, shown today to its first day's residents, measured from tomorrow. False with the reason's key.</summary>
        public bool AdBuy(FlareAd ad, out string why)
        {
            if (!AdQuote(ad, out long cost, out int target, out why))
            {
                return false;
            }
            BuildingRegistration reg = FlareGame.Business(ad.Place);
            if (!FlareCityGame.PayAd(reg, cost))
            {
                why = "reallife_flare_ad_why_money";
                return false;
            }
            PopulationState s = Residents;
            ad.Reach = Math.Min(ad.Reach, target);
            ad.Cost = cost;
            ad.Id = _city.NextAd++;
            ad.Mine = true;
            while (_city.Ads.Count >= FlareCityCatalogue.AdsMost)
            {
                _city.Ads.RemoveAt(0);
            }
            _city.Ads.Add(ad);
            int b = _cityPass.BusinessOfPlace(ad.Place);
            FlareFunnel f = CityFunnel(FunnelKind.Ad, ad.Id, ad.Place, ad.From);
            int account = b >= 0 && b < _cityPass.BusinessAccount.Length ? _cityPass.BusinessAccount[b] : -1;
            HeardReport r = b >= 0 && b < _city.BusinessCount
                ? FlareCityRules.Deliver(_city, s, ad, b, s.Businesses.Kind[b], account, s.Day, _cityPass.AccountBiz, _cityPass.Scratch, f)
                : default(HeardReport);
            if (b >= 0 && b < _city.BusinessCount && _cityPass.Weights.Length > 0)
            {
                _city.Attention[b] = FlareCityRules.AttentionOf(_city, s, b, _cityPass.Weights);
            }
            Logger?.Info("Flare in the city: an ad for " + ad.Name + " (" + ad.Reach + " residents a day for " + ad.Days + " day(s), " + target + " in its target) paid " + cost / 100
                         + "; today " + r.Seen + " saw it, " + r.Interested + " interested.");
            Changed();
            return true;
        }

        /// <summary>The player's ads still running at a business.</summary>
        public int AdsRunning(string place)
        {
            int n = 0, day = Save != null ? Save.Day : 0;
            for (int i = 0; CityOn && i < _city.Ads.Count; i++)
            {
                FlareAd ad = _city.Ads[i];
                n += ad.Mine && ad.Place == place && day < ad.From + Math.Max(1, ad.Days) ? 1 : 0;
            }
            return n;
        }

        // ---------------------------------------------------------------- the screens (FlareCityViews), for the app and the interface kit

        /// <summary>The account in the city: followers here and beyond, and what the newest posts and ads did; null while the residents' side does not run.</summary>
        public FlareCityView CityAccountView()
        {
            return CityOn ? FlareCityViews.Account(_city, _file.Followers, PlaceName) : null;
        }

        /// <summary>A business's page as the residents see it; null while the residents' side does not run.</summary>
        public FlareCityView CityBusinessView(string place, string name)
        {
            PopulationState s = CityOn ? Residents : null;
            if (s == null || place == null)
            {
                return null;
            }
            int b = _cityPass.BusinessOfPlace(place);
            return FlareCityViews.Business(_city, s, _cityPass.PersonSlot, b, name ?? PlaceName(place), place, FlareGame.Business(place) != null);
        }

        /// <summary>The funnel record a screen's row stands for, or null.</summary>
        public FlareFunnel FunnelOf(FlareViewRow row)
        {
            if (!CityOn || row == null || row.Post < 0)
            {
                return null;
            }
            return _city.Funnel(row.Subtitle != null && row.Subtitle.EndsWith("row_ad", StringComparison.Ordinal) ? FunnelKind.Ad : FunnelKind.Post, row.Post);
        }

        /// <summary>A resident's name as a review signs it.</summary>
        public string ResidentName(int person, bool female)
        {
            return FlareLiveRules.ResidentName(_city != null ? _city.Seed : 0, person, female);
        }

        // A place's business name: the player's as the game names it, the city's by its address.
        private string PlaceName(string place)
        {
            BuildingRegistration reg = FlareGame.Business(place);
            return reg != null ? FlareGame.Name(reg) : FlareCityGame.NameOf(FlareCityPass.PlaceToKey(place));
        }

        // ---------------------------------------------------------------- the voices: the city's people are residents

        // The city's people made from the voices' residents: their sex, age, district and manner, and a name of their own.
        private void CityVoices(int seed)
        {
            PopulationState s = CityOn ? Residents : null;
            if (s == null && _cityFollowed != null)
            {
                FollowedVoices(seed);   // a guest: the host's residents, so the feed's people are the same on every machine
                return;
            }
            if (s == null || _cityPass.PersonSlot.Length == 0)
            {
                return;
            }
            var taken = new HashSet<string>(StringComparer.Ordinal);
            int day = Save != null ? Save.Day : s.Day;
            for (int i = 0; i < _citizens.Count && i < _city.Voices.Length; i++)
            {
                CityVoice v = FlareCityRules.VoiceOf(_city, s, i, day, _cityPass.PersonSlot);
                if (v.Person == 0)
                {
                    taken.Add(_citizens[i].First + " " + _citizens[i].Last);
                    continue;
                }
                string district = v.District >= 0 && v.District < s.Districts.Count ? s.Districts[v.District].Id : null;
                _citizens[i] = FlareLiveRules.ResidentCitizen(seed, i, v.Person, v.Female, v.Age, district, v.Persona, taken);
            }
            _cityVoiced = _cityVoices;
        }

        // ---------------------------------------------------------------- Going Public: the host's residents, followed by its guests (part R)

        private FlareCityFollowed _cityFollowed;   // a guest: the host's voices and its own businesses' figures, as last sent

        // Once, in Awake: Flare in the city's own part of the shared city (an older RLO does not know its letter and leaves it out).
        private void CityShareRegister()
        {
            SharedCity.Register(new SharedCityPart
            {
                Id = FlareCityShare.Part, Name = "Flare in the city",
                Ready = () => Running && Save != null,
                Write = CityShareWrite,
                Apply = CityShareApply,
                Left = () =>
                {
                    _cityFollowed = null;
                    _cityVoices++;
                    if (_file != null)
                    {
                        EnsureCitizens(_file);
                    }
                    Logger?.Info("Flare in the city: the host's residents are no longer followed; the city's people are this player's own again.");
                }
            });
        }

        // The host, whole: its voices and its guests' businesses' figures; none while the residents' side does not run here.
        private string CityShareWrite()
        {
            PopulationState s = CityOn ? Residents : null;
            if (s == null || _cityPass.PersonSlot.Length == 0)
            {
                return null;
            }
            return FlareCityShare.Write(_city, s, s.Day, _cityPass.PersonSlot, b => GuestPlace(s, b));
        }

        // A business of a session's other player as Flare writes its place, or null for anybody else's.
        private static string GuestPlace(PopulationState s, int b)
        {
            if (b < 0 || b >= s.Businesses.Count || s.Businesses.Owner[b] != (byte)Owner.Guest || s.Businesses.Key[b] == null)
            {
                return null;
            }
            string key = s.Businesses.Key[b];
            int cut = key.LastIndexOf('|');
            return cut > 0 && int.TryParse(key.Substring(cut + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out int number)
                ? Tech.PlaceRules.Key(key.Substring(0, cut), number) : null;
        }

        // A guest: the host's part, whole each time; the city's people made again from its voices.
        private bool CityShareApply(string text, bool whole)
        {
            if (!whole)
            {
                return true;   // the part only ever goes whole
            }
            var followed = new FlareCityFollowed();
            if (!FlareCityShare.Read(text, followed))
            {
                return false;
            }
            _cityFollowed = followed;
            _cityVoices++;
            if (_file != null)
            {
                EnsureCitizens(_file);
            }
            Changed();
            return true;
        }

        private void FollowedVoices(int seed)
        {
            var taken = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < _citizens.Count && i < _cityFollowed.Voices.Length; i++)
            {
                CityVoice v = _cityFollowed.Voices[i];
                if (v.Person == 0)
                {
                    taken.Add(_citizens[i].First + " " + _citizens[i].Last);
                    continue;
                }
                _citizens[i] = FlareLiveRules.ResidentCitizen(seed, i, v.Person, v.Female, v.Age, _cityFollowed.VoiceDistricts[i], v.Persona, taken);
            }
            _cityVoiced = _cityVoices;
        }

        /// <summary>A guest: one of its businesses' figures as the host's residents see it, or null.</summary>
        public FlareCityFigures FollowedFigures(string place)
        {
            return _cityFollowed != null && place != null && _cityFollowed.Businesses.TryGetValue(place, out FlareCityFigures f) ? f : null;
        }

        /// <summary>The host: a guest's post about a business, heard by the host's residents (their pools give the guest their customers).</summary>
        private void CityGuestPost(FlarePost p, string who)
        {
            if (!CityOn || p == null || p.Place == null || who == null || p.Source == FlareSource.Review || p.Source == FlareSource.Report || p.Source == FlareSource.Reply)
            {
                return;
            }
            CityHear(CityPostOf(p, _city.Account("p:" + who), p.Bad ? -1 : 1), null);
        }

        // ---------------------------------------------------------------- the save, standing down, a fault

        private void CitySave()
        {
            try
            {
                if (_city == null || Save == null)
                {
                    return;
                }
                DaySpread.Shared.Flush(this);   // the day under way finished: the line holds whole days
                Dictionary<string, string> data = FlareCityGame.Data();
                if (data == null)
                {
                    return;
                }
                if (_cityZip == null)
                {
                    _cityZip = new DistrictSaveZip(DistrictSaveZip.WorkersHere);
                }
                float started = Time.realtimeSinceStartup;
                string line = FlareCityStore.Line(_city, _cityZip) ?? FlareCityStore.Encode(_city);
                data[FlareCityStore.Key] = line;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Flare check (the residents' save): " + line.Length / 1024 + " KB in " + ((Time.realtimeSinceStartup - started) * 1000f).ToString("0.0", CultureInfo.InvariantCulture)
                                 + " ms on " + _cityZip.Workers + " worker(s): PASS");
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);   // the save goes on without this line: the last one stays
            }
        }

        // The residents' side stops; with 'keep', its line goes into the save first so switching back on goes on from here.
        private void CityStop(bool keep, string why)
        {
            if (keep && _city != null && !_cityBroken)
            {
                CitySave();
            }
            DaySpread.Shared.Drop(this);
            _cityPass.Stop();
            _cityQueued = false;
            _cityReading = false;
            bool was = _city != null;
            _city = null;
            _cityLater.Clear();
            _cityLaterFunnels.Clear();
            if (was)
            {
                _cityVoices++;
                if (_file != null)
                {
                    EnsureCitizens(_file);   // the city's people as Flare makes them without the population
                }
                Logger?.Info("Flare in the city: stood down (" + why + "); Flare's talk about a business is in business pressure's figure again.");
            }
        }

        private void CityFault(string where, Exception e)
        {
            _cityBroken = true;
            Logger?.Error(e);
            try
            {
                CityStop(false, "a fault");
            }
            catch (Exception second)
            {
                Logger?.Error(second);
            }
            Logger?.Warn("Flare in the city: switched off for this session after an error in " + where + ". Flare goes on as before; the save's line is untouched.");
        }

        /// <summary>For the options page and the tests: everything of the residents' side for this save forgotten; the follows are made again on the next second.</summary>
        public void ForgetCity()
        {
            CityStop(false, "forgotten");
            FlareCityGame.Data()?.Remove(FlareCityStore.Key);
            _cityBroken = false;
        }
    }
}
