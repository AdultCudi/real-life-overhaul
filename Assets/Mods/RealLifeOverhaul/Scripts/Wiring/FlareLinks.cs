using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Flare;
using RealLifeOverhaul.Law;
using RealLifeOverhaul.Leisure;
using RealLifeOverhaul.Markets;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// Flare, the city's social media app, with the rest of the mod (Social/Flare/FlareBridges). Flare calls no
        /// other feature and none calls it: what they have to do with each other is here, one line each.
        ///
        ///   People        the social life's people the player has met, as Flare reads them: their stage, what they
        ///                 like (their traits), how close they are, their job and part of town (Social/SocialState)
        ///   PublicStanding the law's public reputation, which the account's own is pulled towards (Standing.Public)
        ///   Civic         the election on now: has the player voted, endorsed, and for whom (CivicDirector's state)
        ///   Career        the player's employer and rank (a profession's, Careers/Work, else Jobs/CareerState): a rank up is a promotion to post about
        ///   drafts        a picture saved (PhotoMode.Saved), an outing gone to its end (SocialLinks.OutingDone), a good
        ///                 deed that counted (Reputation.Deed) and an arrest (News.Reported, PlayerArrested) become
        ///                 drafts in Flare (FlareDirector.Happened)
        ///
        /// What a post does elsewhere, one line each:
        ///   Footfall      Flare's talk about a business is added to <see cref="BusinessGame.Ambient"/> (on top of what
        ///                 the city's economy, the company and the brands add) and written by business pressure's own
        ///                 writer (BusinessDirector.Footfall), which keeps the sum inside its own bounds; Flare's part is
        ///                 6% at most. On a new day a business the others have had drawn already is not drawn again
        ///   Sponsored     a sponsored post is the company's fifth campaign (CampaignKind.Sponsored), offered from Local
        ///                 on and priced by Flare (CompanyBridges.Outside); booked, it is posted (CompanyBridges.Booked)
        ///   Tagged        a friend tagged is a little closer (SocialLife.Change, MemoryCode.Posted); a selfie taken with
        ///                 them on an outing counts again, the partner more; a post that went badly costs a little trust
        ///   scandals      a scandal at a brand's shop spreads by the account's reputation (BrandsBridges.ScandalScale,
        ///                 BrandRules.Hit) and an honest answer wins part of it back (BrandsDirector.Recover)
        ///   Story         a viral day is a piece in the City Ledger (Paper.Print, NewsKind.Story)
        ///   Following     the account's word in an election (CivicBridges.Following, two businesses' worth at most)
        ///   Wanted, Noticed  a picture posted while wanted: a day of closer police scrutiny (Reputation.Noticed)
        ///   OnShift, Complaint  a post on a rota shift, if the boss follows: a complaint (CareerState.Complaint)
        ///
        /// The city's influencers, one line each too:
        ///   hiring        a deal with influencers is one of the company's six campaigns after the sponsored post
        ///                 (CampaignKind 5 to 10), quoted (CompanyDirector.CampaignQuote) and booked
        ///                 (CompanyDirector.BookCampaign) from Flare's Business tab; Flare prices it (CompanyBridges.Outside:
        ///                 its fee as a share of the kind's base) and is told when it is booked (CompanyBridges.Booked)
        ///   BrandScandal  a hired influencer's scandal, or a hire that backfires, is one more scandal for the business's
        ///                 brand (BrandsDirector.Heard, Scandal.Influencer)
        ///   NightLate     the council's night-life policy draws night-life influencers (CivicPolicy)
        ///   Candidates    the election's candidates post, and influencers back them (the council's state)
        ///   Brands        the player's brands and the city's chains post (the brands' state)
        ///   MeetStar      a star influencer is somebody the player has still to meet, introduced by a friend
        ///                 (SocialDirector.NextStranger, QueueIntroduction)
        ///   CityNews      what the city's news reported today is what local news posts about (News.Reported)
        ///
        /// The rest of the city:
        ///   MeetOnline    somebody the player has still to meet answers a post: met online (SocialOrigin.Online, SocialLife.Meet)
        ///   PartnerSees, PartnerMinds, FriendMinds  the partner proud, cool or jealous; never posted; a friend whose texts
        ///                 wait (SocialLife.Change)
        ///   Market, Sold, MarketAbuse, MarketBuzz  the stock market's results and biggest move, the player's sales, a case
        ///                 of market abuse (TaxCases.FileMarketAbuse, Justice's own summons) and a buzz for a trade
        ///                 (MarketInputs.EventBuzz, MarketEngine.Event)
        ///   Headlines     the City Ledger's own account: the morning paper's headlines (News.Today)
        ///   Issue, Mood   a post raising an issue (CivicGovernment.Raised) and the feed's mood on the mayor's approval
        ///                 (CivicBridges.Mood), a point a day at most
        ///   Campaign      the player's own run for office or office: a draft to post (CivicBridges.CampaignPost,
        ///                 FlareSource.Campaign), and what a post of it is worth to the campaign (CivicOffice.Posted)
        ///   Fame, Heard   a Celebrity pays a lawyer a little more and their hearing is in the paper (Justice.Bridges)
        ///
        /// Everything is read from what those features already keep in public or heard through an event they already
        /// raise; nothing of theirs is changed but through an entry of theirs. COST: nothing per frame. A draft is one
        /// call when the thing happens; the people are read when Flare plays its day, makes a post or the app opens (a
        /// walk over the cast of eight); the footfall answer is a walk over Flare's talk (none most days).
        /// </summary>
        public static class FlareLinks
        {
            private const int PostFamiliarity = 1, PostAffection = 1;   // a friend tagged in a post that went well
            private const int PartnerAffection = 2;                     // ...the partner, more
            private const int BadTrust = -2;                            // ...one that went badly
            private const float ProudAffection = 1f, CoolAffection = -1f, JealousAffection = -2f, JealousTrust = -3f;   // the partner sees a post with somebody they could fall for
            private const float PartnerNeverAffection = -2f;           // ...and is never in a post
            private const float NeglectTrust = -1f;                    // a friend whose texts wait

            private static Func<BuildingRegistration, int> _ambientBefore;

            internal static void Connect()
            {
                FlareBridges.People = People;
                FlareBridges.SavedPeople = SavedPeople;
                FlareBridges.PublicStanding = () => LawState.Loaded ? Standing.Public : FlareRules.ReputationStart;
                FlareBridges.Civic = Civic;
                FlareBridges.Career = Career;
                FlareBridges.Graduated = () => Careers.CareersDirector.Instance != null ? Careers.CareersDirector.Instance.GraduatedToday() : null;
                PhotoMode.Saved += PictureSaved;
                SocialLinks.OutingDone = OutingDone;
                Reputation.Deed += Deed;
                News.Reported += Reported;
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
                FlareBridges.Footfall = Footfall;
                FlareBridges.Tagged = Tagged;
                FlareBridges.Answered = (place, points) => BrandsDirector.Instance != null ? BrandsDirector.Instance.Recover(place, points) : 0;
                FlareBridges.Story = Paper.Print;
                FlareBridges.Wanted = () => LawState.Loaded && Record.Has(Charge.Wanted, Reputation.ScrutinyDays);
                FlareBridges.Noticed = Reputation.Noticed;
                FlareBridges.OnShift = () => JobDirector.Instance != null && JobDirector.Instance.OnRotaShift && CareerState.Employed ? CareerState.EmployerIndex * 100 + CareerState.Rank : -1;
                FlareBridges.Complaint = CareerState.Complaint;
                BrandsBridges.ScandalScale = place => FlareDirector.Instance != null ? FlareDirector.Instance.ScandalScale() : 1000;
                BrandsBridges.ScandalHeard = (place, name, lost, scale) => FlareDirector.Instance?.Scandal(place, name, lost, scale);
                CivicBridges.Following = () => FlareDirector.Instance != null ? CivicGovernment.FollowingMost * FlareDirector.Instance.EndorseShare() / 1000f : 0f;
                CompanyBridges.Outside = Outside;
                CompanyBridges.Booked = Booked;
                FlareBridges.NightLate = () => Settings.Civic && CivicPolicy.Position(Lever.NightLife) > 0;
                FlareBridges.Candidates = Candidates;
                FlareBridges.Brands = Brands;
                FlareBridges.Quote = Quote;
                FlareBridges.Book = Book;
                FlareBridges.Campaigns = Campaigns;
                FlareBridges.EndCampaign = (place, kind, from) => CompanyDirector.Instance?.EndCampaign(place, kind, from);
                FlareBridges.BrandScandal = place => BrandsDirector.Instance?.Heard(place, Scandal.Influencer);
                FlareBridges.MeetStar = MeetStar;
                FlareBridges.MeetOnline = MeetOnline;
                FlareBridges.PartnerSees = PartnerSees;
                FlareBridges.PartnerMinds = () => Closer(SocialState.Loaded ? SocialState.Partner : null, 0f, PartnerNeverAffection, 0f, "minds never being posted");
                FlareBridges.FriendMinds = person => Closer(SocialState.Loaded ? SocialState.Get(person) : null, 0f, 0f, NeglectTrust, "minds the player posting all day while their texts wait");
                FlareBridges.Market = Market;
                FlareBridges.Sold = Sold;
                FlareBridges.MarketAbuse = company => TaxCases.FileMarketAbuse(company);
                FlareBridges.MarketBuzz = MarketBuzz;
                FlareBridges.Headlines = Headlines;
                FlareBridges.Issue = (issue, weight) => CivicDirector.Instance?.Government?.Raised(issue == (int)FlareIssue.Safety ? (int)RealLifeOverhaul.Issue.Safety : (int)RealLifeOverhaul.Issue.Roads, weight / 1000f);
                CivicBridges.Mood = () => FlareDirector.Instance != null ? FlareDirector.Instance.ApprovalPull() : 0f;
                CivicBridges.CampaignPost = (what, name) => FlareDirector.Instance?.Happened(FlareSource.Campaign, -1, null, name, what);
                FlareBridges.Campaign = weight => CivicDirector.Instance?.Office?.Posted(weight);
                Justice.Bridges.Fame = () => FlareDirector.Instance != null ? FlareDirector.Instance.Fame() : 0;
                Justice.Bridges.Heard = () =>
                {
                    if (FlareDirector.Instance != null && FlareDirector.Instance.Fame() > 0)
                    {
                        Paper.Print("reallife_flare_news_celebrity_court");
                    }
                };
            }

            internal static void Disconnect()
            {
                FlareBridges.Reset();
                PhotoMode.Saved -= PictureSaved;
                SocialLinks.OutingDone = null;
                Reputation.Deed -= Deed;
                News.Reported -= Reported;
                BusinessGame.Ambient = _ambientBefore;   // (the brands', the company's and the city's own pairings take theirs off in a moment)
                _ambientBefore = null;
                BrandsBridges.ScandalScale = null;
                BrandsBridges.ScandalHeard = null;
                CivicBridges.Following = null;
                CompanyBridges.Outside = null;
                CompanyBridges.Booked = null;
                CivicBridges.Mood = null;
                CivicBridges.CampaignPost = null;
                Justice.Bridges.Fame = null;
                Justice.Bridges.Heard = null;
            }

            // What was asked for before this pairing, and Flare's talk on top. Business pressure keeps the sum inside its own bounds.
            private static int Ambient(BuildingRegistration reg)
            {
                return (_ambientBefore != null ? _ambientBefore(reg) : 0) + FlareDirector.FootfallOf(reg);
            }

            private static bool Footfall(BuildingRegistration reg, bool newDay)
            {
                try
                {
                    bool drawn = newDay && (CityEconomyDirector.FootfallOf(reg) != 0 || CompanyDirector.FootfallOf(reg) != 0 || BrandsDirector.FootfallOf(reg) != 0);
                    int own, written;
                    return BusinessDirector.Footfall(reg, !drawn, newDay, out own, out written);
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Footfall", e);
                    return false;
                }
            }

            // A friend tagged: closer for it, and they remember it; a selfie with them counts again; the partner more; a bad post costs a little trust.
            private static void Tagged(int person, bool selfie, bool wentWell)
            {
                try
                {
                    SocialPerson p = Settings.Social && SocialState.Loaded ? SocialState.Get(person) : null;
                    if (p == null || !p.Met)
                    {
                        return;
                    }
                    if (!wentWell)
                    {
                        SocialLife.Change(p, 0f, 0f, BadTrust);
                        SocialDirector.Logger?.Info("Social: " + p.Name + " was tagged in a post that went badly and minds it (trust " + BadTrust + ").");
                        return;
                    }
                    float familiarity = PostFamiliarity + (selfie ? SocialRules.SelfieFamiliarity : 0f);
                    float affection = PostAffection + (selfie ? SocialRules.SelfieAffection : 0f) + (p.Together ? PartnerAffection : 0f);
                    SocialLife.Change(p, familiarity, affection, 0f);
                    SocialLife.Remember(p, MemoryCode.Posted);
                    SocialDirector.Logger?.Info("Social: " + p.Name + " was tagged in a post on Flare (familiarity +" + familiarity + ", affection +" + affection
                                                + (selfie ? ", the selfie together counted again" : string.Empty) + (p.Together ? ", the partner" : string.Empty) + ").");
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Tagged", e);
                }
            }

            // The company's question: can a campaign of a kind not its own be booked, at what share of its cost and customers.
            private static bool Outside(int kind, string place, out int cost, out int reach)
            {
                cost = reach = 1000;
                FlareDirector flare = FlareDirector.Instance;
                if (flare == null)
                {
                    return false;
                }
                if (kind == (int)CampaignKind.Sponsored)
                {
                    return flare.SponsoredQuote(out cost, out reach);
                }
                int fee;
                if (kind < FlareCast.FirstDealKind || kind >= CompanyCatalogue.CampaignKinds || !flare.HireFigures(kind, place, out fee, out reach))
                {
                    return false;   // an influencer's deal is offered only while Flare is booking it
                }
                cost = (int)((long)fee * 1000 / Math.Max(1, CompanyCatalogue.CampaignCost[kind]));   // both at the start's prices: the company puts today's on
                return true;
            }

            private static void Booked(string place, int kind, int from, int to, int percent)
            {
                if (kind == (int)CampaignKind.Sponsored)
                {
                    FlareDirector.Instance?.Sponsored(place, from, to, percent);
                }
                else if (kind >= FlareCast.FirstDealKind)
                {
                    FlareDirector.Instance?.Hired(place, kind, from, to, percent);
                }
            }

            // The company's quote for a deal Flare is making, or why there is none.
            private static bool Quote(string place, int kind, out int cost, out int percent, out string why)
            {
                cost = percent = 0;
                why = null;
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    CompanyShop shop = company != null && company.Running && company.On(CompanyPart.Marketing) ? company.ShopAt(place) : null;
                    if (shop == null)
                    {
                        why = "reallife_flare_hire_no_company";
                        return false;
                    }
                    if (!CampaignRules.CanBook(shop.File))
                    {
                        why = FlareDirector.BusyWhy;
                        return false;
                    }
                    if (!company.Promoted(shop))
                    {
                        why = "reallife_flare_hire_no_figure";
                        return false;
                    }
                    int share, reach, suit, repeat;
                    if (!company.Offered(shop, kind, out share, out reach))
                    {
                        why = "reallife_flare_hire_no_one";
                        return false;
                    }
                    company.CampaignQuote(shop, kind, out cost, out percent, out suit, out repeat);
                    return true;
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Quote", e);
                    why = "reallife_flare_hire_no_company";
                    return false;
                }
            }

            private static bool Book(string place, int kind)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    if (company == null)
                    {
                        return false;
                    }
                    company.BookCampaign(place, kind);
                    CompanyShop shop = company.ShopAt(place);
                    return shop != null && shop.File.Push == kind;
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Book", e);
                    return false;
                }
            }

            // The company's campaigns at the player's businesses, running or booked, for Flare's Business tab and its hiring page.
            private static void Campaigns(List<FlareCampaign> into)
            {
                try
                {
                    CompanyDirector company = CompanyDirector.Instance;
                    IReadOnlyList<CompanyShop> shops = company != null && company.Running && company.On(CompanyPart.Marketing) ? company.Shops : null;
                    for (int i = 0; shops != null && i < shops.Count; i++)
                    {
                        int kind, from, to;
                        bool started;
                        if (CampaignRules.Busy(shops[i].File, 0, out kind, out from, out to, out started))
                        {
                            into.Add(new FlareCampaign
                            {
                                Place = shops[i].File.Place, Name = shops[i].Name, Campaign = CompanyText.Campaign(kind), Kind = kind, From = from, To = to,
                                Percent = shops[i].File.PushPercent, OnFlare = CampaignRules.HiredOnFlare(kind)
                            });
                        }
                    }
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Campaigns", e);
                }
            }

            // The election's candidates, while one is being fought.
            private static void Candidates(List<string> into)
            {
                CivicGovernment gov = CivicDirector.Instance != null && Settings.Civic ? CivicDirector.Instance.Government : null;
                GovState s = gov != null ? gov.State : null;
                for (int i = 0; s != null && gov.Campaigning && i < s.CandidateCount; i++)
                {
                    if (!s.Candidates[i].Player)
                    {
                        into.Add(CivicText.Name(s.Candidates[i]));   // (the player's own account is theirs: the city does not post as them)
                    }
                }
            }

            // The player's brands and the city's chains.
            private static void Brands(List<string> into)
            {
                BrandsState s = BrandsDirector.Instance != null ? BrandsDirector.Instance.State : null;
                for (int i = 0; s != null && i < s.Brands.Count; i++)
                {
                    into.Add(s.Brands[i].Name);
                }
                for (int i = 0; s != null && i < BrandsText.ChainKeys.Length; i++)
                {
                    into.Add(BrandsText.ChainKeys[i].GetLocalization());
                }
            }

            // Somebody the player has still to meet answers a post and they swap numbers: met online.
            private static string MeetOnline()
            {
                try
                {
                    SocialPerson stranger = Settings.Social && Settings.SocialMeetings && SocialState.Loaded && SocialDirector.Instance != null ? SocialDirector.NextStranger() : null;
                    if (stranger == null)
                    {
                        return null;
                    }
                    stranger.Origin = SocialOrigin.Online;
                    SocialLife.Meet(stranger, true);
                    return stranger.Name;
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.MeetOnline", e);
                    return null;
                }
            }

            private static void PartnerSees(int partner, int view)
            {
                SocialPerson p = SocialState.Loaded ? SocialState.Get(partner) : null;
                if (view == (int)FlareRules.PartnerView.Proud)
                {
                    Closer(p, 0f, ProudAffection, 0f, "is proud of the post");
                }
                else if (view == (int)FlareRules.PartnerView.Jealous)
                {
                    Closer(p, 0f, JealousAffection, JealousTrust, "is jealous of the post");
                }
                else
                {
                    Closer(p, 0f, CoolAffection, 0f, "is cool about the post");
                }
            }

            // One small change to a relationship, said once.
            private static void Closer(SocialPerson p, float familiarity, float affection, float trust, string why)
            {
                try
                {
                    if (p == null || !p.Met || !Settings.Social)
                    {
                        return;
                    }
                    SocialLife.Change(p, familiarity, affection, trust);
                    SocialDirector.Logger?.Info("Social: " + p.Name + " " + why + " (affection " + affection + ", trust " + trust + ").");
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Closer", e);
                }
            }

            // The stock market's news of yesterday: results and the biggest move, with whether the player holds the company.
            private static void Market(List<FlareMarketNote> into)
            {
                MarketState s = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
                if (s == null)
                {
                    return;
                }
                int day = SaveGameManager.Current != null ? SaveGameManager.Current.Day - 1 : -1;
                FlareMarketNote mover = default(FlareMarketNote);
                bool moved = false;
                for (int i = 0; i < s.News.Count; i++)
                {
                    MarketNews n = s.News[i];
                    if (n.Day != day || (n.Kind != MarketNewsKind.Results && n.Kind != MarketNewsKind.Mover))
                    {
                        continue;
                    }
                    Stock stock = StockOf(s, n.Stock);
                    if (stock == null)
                    {
                        continue;
                    }
                    if (n.Kind == MarketNewsKind.Mover)
                    {
                        if (!moved || Math.Abs(n.A) > Math.Abs(mover.MoveBp))
                        {
                            mover = new FlareMarketNote { Stock = n.Stock, Name = stock.Name, Mover = true, MoveBp = n.A };
                            moved = true;
                        }
                        continue;
                    }
                    into.Add(new FlareMarketNote { Stock = n.Stock, Name = stock.Name, Beat = n.A >= n.B, MoveBp = n.C, Held = Holds(s, n.Stock) });
                }
                if (moved)
                {
                    into.Add(mover);
                }
            }

            private static Stock StockOf(MarketState s, int id)
            {
                for (int i = 0; i < s.Stocks.Count; i++)
                {
                    if (s.Stocks[i].Id == id)
                    {
                        return s.Stocks[i];
                    }
                }
                return null;
            }

            private static bool Holds(MarketState s, int stock)
            {
                for (int i = 0; i < s.Positions.Count; i++)
                {
                    if (s.Positions[i].StockId == stock && s.Positions[i].Shares > 0 && !s.Positions[i].Test)
                    {
                        return true;
                    }
                }
                return false;
            }

            private static bool Sold(int stock, int fromDay)
            {
                MarketState s = MarketDirector.Instance != null ? MarketDirector.Instance.State : null;
                for (int i = 0; s != null && i < s.Trades.Count; i++)
                {
                    Trade t = s.Trades[i];
                    if (t.Kind == TradeKind.Sold && t.StockId == stock && t.Day >= fromDay && !t.Test)
                    {
                        return true;
                    }
                }
                return false;
            }

            // A buzz on the market for the trade of one of the player's businesses: a viral post (+1) or a brand's scandal (-1).
            private static void MarketBuzz(string place, int sign)
            {
                try
                {
                    BuildingRegistration reg = BusinessGame.Find(place);
                    int trade = reg != null ? MarketInputs.TradeOf(reg.businessTypeName) : -1;
                    if (trade >= 0)
                    {
                        MarketDirector.Instance?.Engine?.Event(MarketInputs.EventBuzz, trade, Math.Sign(sign));
                    }
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.MarketBuzz", e);
                }
            }

            // The morning paper's headlines, as the City Ledger's own account posts them: each piece's words up to its first full stop.
            private static void Headlines(List<string> into)
            {
                IReadOnlyList<NewsItem> today = News.Today;
                for (int i = 1; today != null && i < today.Count && into.Count < 3; i++)   // (the first is the masthead)
                {
                    NewsItem item = today[i];
                    if (string.IsNullOrEmpty(item.Key))
                    {
                        continue;
                    }
                    string text = item.Key.GetLocalization();
                    if (item.Data != null)
                    {
                        foreach (KeyValuePair<string, string> pair in item.Data)
                        {
                            text = text.Replace("{" + pair.Key + "}", pair.Value ?? string.Empty);
                        }
                    }
                    int stop = text.IndexOf(". ", StringComparison.Ordinal);
                    into.Add(stop > 0 ? text.Substring(0, stop + 1) : text);
                }
            }

            // A star influencer: the first person the player has still to meet is them, and a friend will bring them along.
            private static int MeetStar(string handle)
            {
                try
                {
                    SocialDirector social = SocialDirector.Instance;
                    SocialPerson stranger = social != null && Settings.Social && SocialState.Loaded ? SocialDirector.NextStranger() : null;
                    SocialPerson friend = null;
                    for (int i = 0; stranger != null && i < SocialState.Count && friend == null; i++)
                    {
                        SocialPerson p = SocialState.Get(i);
                        friend = p != null && p.Met && p.Stage >= SocialStage.Friend ? p : null;
                    }
                    if (friend == null || !social.QueueIntroduction(friend, stranger))
                    {
                        return -1;
                    }
                    SocialDirector.Logger?.Info("Social: " + friend.Name + " will introduce " + stranger.Name + ", who posts on Flare as " + handle + ".");
                    return stranger.Index;
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.MeetStar", e);
                    return -1;
                }
            }

            // The people of the social life the player has met, as Flare sees them.
            private static void People(List<FlarePerson> into)
            {
                if (!Settings.Social || !SocialState.Loaded)
                {
                    return;
                }
                for (int i = 0; i < SocialState.Count; i++)
                {
                    SocialPerson p = SocialState.Get(i);
                    if (p == null || !p.Met)
                    {
                        continue;
                    }
                    into.Add(new FlarePerson
                    {
                        Index = p.Index, First = SocialLife.First(p), Stage = (int)p.Stage, Taste = Taste(p), Closeness = p.Closeness, Partner = p.Together, Compatible = p.Compatible,
                        Unanswered = p.LastTextDay > p.LastContactDay && SocialLife.Day - p.LastTextDay <= 3,
                        Job = SocialCatalogue.Jobs[Math.Max(0, Math.Min(p.Job, SocialCatalogue.Jobs.Length - 1))].GetLocalization(), District = SocialPlaces.District(p.District),
                        Female = p.Female, Age = p.Age, LookSeed = p.Seed
                    });
                }
            }

            // The people met as the save holds them, while the loading screen is up (Social/SocialState.PeekMet): their looks for Flare's faces.
            private static void SavedPeople(Dictionary<string, string> data, List<FlarePerson> into)
            {
                if (!Settings.Social)
                {
                    return;
                }
                var met = new List<SocialPerson>(SocialRules.BaseCast);
                SocialState.PeekMet(data, met);
                for (int i = 0; i < met.Count; i++)
                {
                    SocialPerson p = met[i];
                    into.Add(new FlarePerson { Index = p.Index, Stage = (int)p.Stage, Partner = p.Together, Female = p.Female, Age = p.Age, LookSeed = p.Seed });
                }
            }

            // The social life's traits as what Flare's friends care about.
            private static FlareTaste Taste(SocialPerson p)
            {
                FlareTaste t = FlareTaste.None;
                t |= p.Has(SocialTrait.Supportive) ? FlareTaste.Cheers : FlareTaste.None;
                t |= p.Has(SocialTrait.Envious) ? FlareTaste.Cool : FlareTaste.None;
                t |= p.Has(SocialTrait.Homebody) ? FlareTaste.Shops : FlareTaste.None;
                t |= p.Has(SocialTrait.Sporty) ? FlareTaste.Sport : FlareTaste.None;
                t |= p.Has(SocialTrait.Foodie) ? FlareTaste.Food : FlareTaste.None;
                t |= p.Has(SocialTrait.NightOwl) ? FlareTaste.Night : FlareTaste.None;
                t |= p.Has(SocialTrait.Flaky) ? FlareTaste.Flaky : FlareTaste.None;
                return t;
            }

            private static int Civic(out string voted, out string endorsed)
            {
                voted = endorsed = null;
                try
                {
                    CivicGovernment gov = CivicDirector.Instance != null && Settings.Civic ? CivicDirector.Instance.Government : null;
                    GovState s = gov != null ? gov.State : null;
                    if (s == null || s.Phase == ElectionPhase.None)
                    {
                        return 0;
                    }
                    int mask = 0;
                    if (s.Voted >= 0 && s.Voted < s.CandidateCount)
                    {
                        mask |= 1;
                        voted = CivicText.Name(s.Candidates[s.Voted]);
                    }
                    if (s.Endorsed >= 0 && s.Endorsed < s.CandidateCount)
                    {
                        mask |= 2;
                        endorsed = CivicText.Name(s.Candidates[s.Endorsed]);
                    }
                    return mask;
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Civic", e);
                    return 0;
                }
            }

            private static int Career(out string employer)
            {
                employer = null;
                try
                {
                    // A profession's job (Careers/Work) first: its employer, and a rung up the ladder is a promotion to post about.
                    Careers.CareersDirector careers = Careers.CareersDirector.Instance;
                    Careers.WorkFile work = careers != null ? careers.Work : null;
                    if (work != null && work.Job != null)
                    {
                        employer = Careers.CareersText.Employer(work.Job.Post.Employer);
                        return (ProfessionKeys + work.Job.Post.Prof) * 100 + work.Job.Post.Rank;
                    }
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.Career (a profession)", e);
                }
                if (!CareerState.Loaded || !CareerState.Employed)
                {
                    return -1;
                }
                employer = CareerState.Employer.NameKey.GetLocalization();
                return CareerState.EmployerIndex * 100 + CareerState.Rank;
            }

            private const int ProfessionKeys = 50;   // a profession's employer numbers start here, after the street careers' employers

            // A picture saved in photo mode: a draft, with the friend in it and the business it was taken in.
            private static void PictureSaved(string path, bool selfie, int person)
            {
                try
                {
                    FlareDirector flare = FlareDirector.Instance;
                    if (flare == null || !flare.Running)
                    {
                        return;
                    }
                    BuildingRegistration inside = FlareGame.Inside();
                    flare.Happened(selfie ? FlareSource.Selfie : FlareSource.Photo, person, inside != null ? FlareGame.Place(inside) : null, System.IO.Path.GetFileName(path), 0);
                }
                catch (Exception e)
                {
                    Fault("FlareLinks.PictureSaved", e);
                }
            }

            // An outing gone to its end: a draft with the friend (the two favours played on the street are not outings to post).
            private static void OutingDone(SocialPerson person, PlanKind kind)
            {
                if (person != null && kind != PlanKind.Lift && kind != PlanKind.Moving)
                {
                    FlareDirector.Instance?.Happened(FlareSource.Outing, person.Index, null, null, (int)kind);
                }
            }

            private static void Deed()
            {
                FlareDirector.Instance?.Happened(FlareSource.GoodDeed, -1, null, null, 0);
            }

            private static void Reported(NewsKind kind, string detail, int amount)
            {
                if (kind == NewsKind.PlayerArrested)
                {
                    FlareDirector.Instance?.Happened(FlareSource.Arrested, -1, null, null, 0);
                }
                int bit = kind == NewsKind.Accident || kind == NewsKind.PedestrianHit || kind == NewsKind.HitAndRun ? FlareCast.NewsAccident
                    : kind == NewsKind.Crime ? FlareCast.NewsCrime : kind == NewsKind.Arrest ? FlareCast.NewsArrest : kind == NewsKind.BreakIn || kind == NewsKind.CarBreakIn ? FlareCast.NewsBreakIn
                    : kind == NewsKind.Story && detail == "reallife_news_story_fire" ? FlareCast.NewsFire : 0;
                if (bit != 0)
                {
                    FlareDirector.Instance?.CityNews(bit);
                }
            }
        }
    }
}
