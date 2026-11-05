using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, its working pages: a business's stars, reviews and what Flare brought it; the creator studio's week,
    /// results, campaigns and tier; the influencers for hire; and what the city is doing, each card with its action. Every
    /// figure is the model's own (the star rows, the boosts, the hires, the city's views), read when the page is written.
    /// </summary>
    public sealed partial class FlareScreen
    {
        private const int WeekDays = 7;
        private readonly List<FlareInfluencer> _hireable = new List<FlareInfluencer>(InfluencersMost);
        private readonly List<string> _hireDistricts = new List<string>(8);

        // ------------------------------------------------------------------------------------------------ Business

        private void Business(FlareFile file, long now)
        {
            FlareBusinessModel b = _m.Business;
            List<FlareShop> shops = _flare.MyShops();
            _m.Head = FlareText.T("reallife_flare_kit_business_title");
            _m.List.EmptyIcon = UiIcon.Store;
            if (shops.Count == 0)
            {
                _m.HeadLine = null;
                b.Picker = None;
                _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_no_business");
                _m.List.EmptyBody = FlareText.T("reallife_flare_kit_no_business_body");
                return;
            }
            _shop = Math.Max(0, Math.Min(_shop, shops.Count - 1));
            FlareShop shop = shops[_shop];
            b.Picker = new string[shops.Count];
            for (int i = 0; i < shops.Count; i++)
            {
                b.Picker[i] = shops[i].Name ?? "?";
            }
            b.Chosen = _shop;
            _m.HeadLine = BusinessLine(shop);
            b.Promote = _flare.CityOn ? FlareText.T("reallife_flare_kit_promote_ad") : null;
            b.ReachTitle = FlareText.T("reallife_flare_kit_reach_title");
            b.ReachEmpty = FlareText.T("reallife_flare_kit_reach_empty");
            b.Reach = ReachLine(file);
            b.Rival = _flare.CompetitorOf(shop.Place, out string rival, out int tenths)
                ? FlareText.Fill("reallife_flare_kit_rival_line", "business", rival, "stars", (tenths / 10f).ToString("0.0", CultureInfo.InvariantCulture)) : null;
            // The stars, as the city's residents gave them, the answered ones weighed as the reply rules say.
            FlareStarRow row = _flare.StarsFor(shop.Place);
            int total = row != null ? row.Total : 0;
            int average = FlareReplyRules.Average(row);
            b.RatingTitle = FlareText.T("reallife_flare_kit_rating");
            b.Rating = total > 0 ? (average / 1000f).ToString("0.0", CultureInfo.InvariantCulture) : FlareText.T("reallife_flare_kit_rating_none");
            b.Halves = total > 0 ? (average + 250) / 500 : 0;
            b.RatingLine = total > 0 ? ReviewsCount(total) : FlareText.T("reallife_flare_kit_week_no_reviews");
            b.Bars = row != null ? new[] { row.Five, row.Four, row.Three, row.Two, row.One } : new int[5];
            // What Flare brought it this week: the posts' and the campaigns' visitors and takings, its followers in the city, a campaign.
            Brought(file, shop.Place, now, out int visitors, out long cents, out _, out _);
            FlareCityView view = _flare.CityBusinessView(shop.Place, shop.Name);
            long followers = view != null && view.Hero.Count > 0 ? view.Hero[0].Value : 0;
            bool campaign = _flare.CampaignAt(shop.Place, out FlareCampaign running);
            b.VisitsTitle = FlareText.T("reallife_flare_kit_visits_title");
            b.Figures = new[]
            {
                FlareText.N(visitors), FlareText.Money(cents / 100), FlareFeedRules.Count((int)Math.Min(int.MaxValue, followers)),
                campaign ? FlareText.Fill("reallife_flare_kit_campaign_percent", "percent", running.Percent.ToString(CultureInfo.InvariantCulture)) : FlareText.T("reallife_flare_kit_campaign_none")
            };
            b.FigureLabels = new[]
            {
                FlareText.T("reallife_flare_kit_fig_visitors"), FlareText.T("reallife_flare_kit_fig_spent"), FlareText.T("reallife_flare_kit_fig_city_followers"),
                FlareText.T("reallife_flare_kit_fig_campaign")
            };
            b.Warning = FlareReplyRules.Turning(row)
                ? FlareText.Fill("reallife_flare_kit_turning", "now", (row.Recent / 1000f).ToString("0.0", CultureInfo.InvariantCulture),
                    "before", (row.Earlier / 1000f).ToString("0.0", CultureInfo.InvariantCulture))
                : null;
            b.ReviewsTitle = FlareText.T("reallife_flare_kit_reviews_title");
            Reviews(file, shop, now);
            _m.List.EmptyIcon = UiIcon.Star;
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_no_reviews");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_no_reviews_body");
        }

        // The account's reach a day, only the days that have one (the days before the first post are not zeros of a chart); null with fewer than two.
        private static float[] ReachLine(FlareFile file)
        {
            int first = 0;
            while (first < file.ReachDays.Length && file.ReachDays[first] <= 0)
            {
                first++;
            }
            int n = file.ReachDays.Length - first;
            if (n < 2)
            {
                return null;
            }
            var line = new float[n];
            for (int i = 0; i < n; i++)
            {
                line[i] = file.ReachDays[first + i];
            }
            return line;
        }

        // A business's line under its name: its kind, how many hours a day it is open, its address and district, and the day it opened.
        private string BusinessLine(FlareShop shop)
        {
            int hours = _flare.OpenHours(shop.Place);
            string key = FlareCityPass.PlaceToKey(shop.Place);
            int cut = key != null ? key.LastIndexOf('|') : -1;
            string address = cut > 0 ? key.Substring(cut + 1) + " " + key.Substring(0, cut) : null;
            string line = FlareText.Fill("reallife_flare_kit_business_line", "kind", FlareText.Niche(shop.Niche), "district", FlareText.District(shop.District));
            if (address != null)
            {
                line += " · " + address;
            }
            if (hours > 0)
            {
                line += " · " + FlareText.Fill("reallife_flare_kit_open_hours", "hours", hours.ToString(CultureInfo.InvariantCulture));
            }
            return shop.OpenedDay > 0 ? line + " · " + FlareText.Fill("reallife_flare_kit_opened", "day", shop.OpenedDay.ToString(CultureInfo.InvariantCulture)) : line;
        }

        // The reviews of a business, newest first: the ones posted on Flare and the ones residents left, merged by day.
        private void Reviews(FlareFile file, FlareShop shop, long now)
        {
            _flare.ReviewNotesOf(shop.Place, _notes, ReviewsMost);
            int note = 0;
            for (int i = file.Posts.Count - 1; i >= -1 && _m.List.Count < ReviewsMost; i--)
            {
                FlarePost p = i >= 0 ? file.Posts[i] : null;
                if (p != null && (p.Source != FlareSource.Review || p.Author == FlareAuthor.Player || p.Place != shop.Place || p.At > now || Hidden(file, p)))
                {
                    continue;
                }
                while (note < _notes.Count && (p == null || _notes[note].Day > p.Day) && _m.List.Count < ReviewsMost)
                {
                    NoteRow(_notes[note++], shop, now);
                }
                if (p != null && _m.List.Count < ReviewsMost)
                {
                    ReviewPostRow(file, p, now);
                }
            }
        }

        private void ReviewPostRow(FlareFile file, FlarePost p, long now)
        {
            Author(p, out string name, out _, out string key, out _);
            ref FlareRow r = ref Add(_m.List, RowAct.Review, p, key);
            r.Kind = FlareRowKind.Review;
            r.Name = name;
            r.Hue = UiColour.HueOf(key ?? name);
            r.Stars = FlareRules.Clamp(p.Arg, 1, 5);
            LookOfPost(p, ref r, key);
            r.Flags |= FlareRowFlags.Person;   // someone wrote it, a person: their face is ringed by how they left
            r.Ring = FlareFace.StarRing(r.Stars);
            r.Time = Ago(p.At, now);
            r.Text = FlareFeedRules.Plain(p.Text ?? _flare.WordsFor(p, name));
            FlarePost answer = MyAnswer(file, p.Id);
            if (answer != null)
            {
                r.Flags |= FlareRowFlags.Answered;
                r.Line = FlareText.Fill("reallife_flare_kit_your_reply", "words", FlareFeedRules.Plain(answer.Text ?? string.Empty));
            }
            else
            {
                r.Button = FlareText.T("reallife_flare_kit_reply");
            }
        }

        private static FlarePost MyAnswer(FlareFile file, int review)
        {
            for (int i = file.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.ReplyTo == review && p.Author == FlareAuthor.Player)
                {
                    return p;
                }
            }
            return null;
        }

        private void NoteRow(FlareReviewNote n, FlareShop shop, long now)
        {
            ref FlareRow r = ref Add(_m.List, RowAct.Note, null, null, 0, n);
            r.Kind = FlareRowKind.Review;
            r.Name = _flare.ReviewerName(n.Person);
            r.Stars = FlareRules.Clamp(n.Stars, 1, 5);
            r.Hue = UiColour.HueOf(r.Name ?? "?");
            r.Flags |= FlareRowFlags.Person;   // a resident who shopped there: their face is ringed by how they left (the stars of the review)
            r.Ring = FlareFace.StarRing(r.Stars);
            r.Time = Ago(FlareLiveRules.Now(n.Day, FlareLiveRules.NoticeHour, 0), now);
            r.Text = NoteWords(n, shop.Name);
            if (!string.IsNullOrEmpty(n.Answer))
            {
                r.Flags |= FlareRowFlags.Answered;
                string words = n.Answer.StartsWith("reallife_", StringComparison.Ordinal) ? FlareText.T(n.Answer) : n.Answer;
                r.Line = FlareText.Fill("reallife_flare_kit_your_reply", "words", words);
            }
            else
            {
                r.Button = FlareText.T("reallife_flare_kit_reply");
            }
        }

        // A review's words by its stars, one to five, and what it was about (FlareReviewNote.Why, 0 to 4), good then poor: whole
        // keys, so the catalogue checks each one is written.
        private static readonly string[] ReviewKeys =
        {
            "reallife_flare_review_post_1", "reallife_flare_review_post_2", "reallife_flare_review_post_3", "reallife_flare_review_post_4", "reallife_flare_review_post_5"
        };

        private static readonly string[] WhyKeys =
        {
            "reallife_flare_kit_why_0_good", "reallife_flare_kit_why_0_poor", "reallife_flare_kit_why_1_good", "reallife_flare_kit_why_1_poor",
            "reallife_flare_kit_why_2_good", "reallife_flare_kit_why_2_poor", "reallife_flare_kit_why_3_good", "reallife_flare_kit_why_3_poor",
            "reallife_flare_kit_why_4_good", "reallife_flare_kit_why_4_poor"
        };

        // A resident's review in words: its stars, then what it was about, good or poor as its stars say.
        private static string NoteWords(FlareReviewNote n, string business)
        {
            int why = FlareRules.Clamp(n.Why, 0, 4);
            string stars = FlareText.Fill(ReviewKeys[FlareRules.Clamp(n.Stars, 1, 5) - 1], "name", business ?? "?");
            string about = FlareText.T(WhyKeys[why * 2 + (n.Stars >= 3 ? 0 : 1)]);
            return stars + " " + about;
        }

        // What Flare brought a business (or all of them, with no place) over the last week and the week before: the posts' boosts and the campaigns.
        private static void Brought(FlareFile file, string place, long now, out int visitors, out long cents, out int visitorsBefore, out long centsBefore)
        {
            visitors = visitorsBefore = 0;
            cents = centsBefore = 0;
            int today = (int)(now / 1440), minute = (int)Math.Min(int.MaxValue, now);
            for (int i = 0; i < file.Boosts.Count; i++)
            {
                FlareBoost b = file.Boosts[i];
                if (place != null && b.Place != place)
                {
                    continue;
                }
                FlareUseRules.Credited(b, minute, out int given, out int givenCents);   // what the hours that ran brought
                int age = today - b.FromMinute / 1440;
                if (age >= 0 && age < WeekDays)
                {
                    visitors += given;
                    cents += givenCents;
                }
                else if (age >= WeekDays && age < WeekDays * 2)
                {
                    visitorsBefore += given;
                    centsBefore += givenCents;
                }
            }
            for (int i = 0; i < file.PastHires.Count; i++)
            {
                FlarePastHire h = file.PastHires[i];
                if (place != null && h.Place != place)
                {
                    continue;
                }
                int age = today - h.Day;
                if (age >= 0 && age < WeekDays)
                {
                    visitors += h.Visitors;
                    cents += h.Cents;
                }
                else if (age >= WeekDays && age < WeekDays * 2)
                {
                    visitorsBefore += h.Visitors;
                    centsBefore += h.Cents;
                }
            }
        }

        // ------------------------------------------------------------------------------------------------ the creator studio

        private void Studio(FlareFile file, long now)
        {
            FlareStudioModel st = _m.Studio;
            int today = (int)(now / 1440);
            _m.Head = FlareText.T("reallife_flare_kit_studio_title");
            _m.HeadLine = null;   // its first tile is the followers and the sponsorship card is the tier: a head saying them again told them twice
            Brought(file, null, now, out int visitors, out long cents, out int visitorsBefore, out long centsBefore);
            FlareFigures.StudioTiles(WeekFacts(file, now), visitors, visitorsBefore, cents / 100, centsBefore / 100, Words, Money, st);
            st.LineTitle = FlareText.T("reallife_flare_kit_line_title");
            st.Line = FlareFeedRules.Line(file.History, file.Followers);
            Creator(st);
            Tier(file, st);
            Results(file, now, today);
        }

        // The creator income: what Flare paid last and when the next is due; below the tier that is paid one, what it takes.
        private void Creator(FlareStudioModel st)
        {
            st.CreatorTitle = st.Creator = st.CreatorLine = null;
            if (!_flare.CityOn)
            {
                return;
            }
            st.CreatorTitle = FlareText.T("reallife_flare_kit_creator_title");
            _flare.CreatorPaid(out long last, out long total);
            int in_ = _flare.CreatorIn();
            st.Creator = FlareText.Money((int)Math.Min(int.MaxValue, last / 100));
            if (in_ < 0)
            {
                st.CreatorLine = FlareText.Fill("reallife_flare_kit_creator_locked", "tier", FlareText.Tier(FlareTier.Influencer), "count",
                    FlareFeedRules.Count(FlareRules.TierFrom[(int)FlareTier.Influencer]));
            }
            else if (last > 0)
            {
                st.CreatorLine = FlareText.Fill("reallife_flare_kit_creator_paid", "total", FlareText.Money((int)Math.Min(int.MaxValue, total / 100)), "days",
                    in_.ToString(CultureInfo.InvariantCulture));
            }
            else
            {
                st.CreatorLine = FlareText.Fill("reallife_flare_kit_creator_first", "days", in_.ToString(CultureInfo.InvariantCulture));
            }
        }

        private static string Signed(long cents)
        {
            int dollars = (int)Math.Max(int.MinValue, Math.Min(int.MaxValue, cents / 100));
            return (dollars > 0 ? "+" : dollars < 0 ? "-" : string.Empty) + FlareText.Money(Math.Abs(dollars));
        }

        // Sponsorship: the tier, how far to the next, and what each tier opens.
        private static void Tier(FlareFile file, FlareStudioModel st)
        {
            int tier = (int)file.Tier, next = FlareRules.NextTierAt(file.Followers);
            st.TierTitle = FlareText.T("reallife_flare_kit_tier_title");
            st.Tier = FlareText.Tier(file.Tier);
            if (next > 0)
            {
                int from = FlareRules.TierFrom[tier];
                st.TierShare = Math.Max(0f, Math.Min(1f, (file.Followers - from) / (float)Math.Max(1, next - from)));
                st.TierLine = FlareText.Fill("reallife_flare_kit_tier_next", "tier", FlareText.Tier((FlareTier)(tier + 1)), "count", FlareFeedRules.Count(next - file.Followers));
            }
            else
            {
                st.TierShare = 1f;
                st.TierLine = FlareText.T("reallife_flare_kit_tier_top");
            }
            int t = Math.Max(0, Math.Min(tier, FlareUseRules.PercentCap.Length - 1));
            st.Perks = new[]
            {
                Perk(FlareUseRules.Sponsorships(file.Followers), FlareText.Fill("reallife_flare_kit_perk_sponsors", "tier", FlareText.Tier(FlareTier.Known))),
                Perk(true, FlareText.Fill("reallife_flare_kit_perk_cap", "percent", FlareUseRules.PercentCap[t].ToString(CultureInfo.InvariantCulture))),
                Perk(FlareUseRules.JobExtraOf[t] > 0, FlareText.Fill("reallife_flare_kit_perk_jobs", "percent", Percent(FlareUseRules.JobExtraOf[Math.Max(t, 2)], 10))),
                Perk(FlareUseRules.VipOf[t] > 0, FlareText.Fill("reallife_flare_kit_perk_vip", "percent", Percent(FlareUseRules.VipOf[Math.Max(t, 2)], 10))),
                Perk(FlareUseRules.RaiseOf[t] > 0, FlareText.Fill("reallife_flare_kit_perk_raise", "percent", Percent(FlareUseRules.RaiseOf[Math.Max(t, 3)], 100))),
                Perk(FlareUseRules.InvestorOf[t] > 0, FlareText.T("reallife_flare_kit_perk_investors"))
            };
        }

        private static string Perk(bool open, string words) => (open ? "+" : "-") + words;

        private static string Percent(int value, int per) => (value / (float)per).ToString(value % per == 0 ? "0" : "0.#", CultureInfo.InvariantCulture);

        // What the player's posts and campaigns brought: the posts with their boosts, the campaigns running, the ones done.
        private void Results(FlareFile file, long now, int today)
        {
            int minute = (int)Math.Min(int.MaxValue, now);
            bool posts = false;
            for (int i = file.Boosts.Count - 1; i >= 0 && _m.List.Count < ResultsMost; i--)
            {
                FlareBoost b = file.Boosts[i];
                FlarePost p = file.Post(b.Post);
                if (p == null || p.Author != FlareAuthor.Player || b.FromMinute > minute)
                {
                    continue;
                }
                if (!posts)
                {
                    Section(FlareText.T("reallife_flare_kit_results_posts"));
                    posts = true;
                }
                bool on = minute < b.FromMinute + b.Hours * 60;
                ref FlareRow r = ref Add(_m.List, RowAct.Thread, p, "me");
                r.Kind = FlareRowKind.Result;
                r.Icon = b.Paid ? UiIcon.Megaphone : UiIcon.ChartLineUp;
                r.Tint = b.WentViral ? UiRole.Chart2 : UiRole.AccentText;
                string business = p.Name ?? b.Place ?? "?";
                FlareUseRules.Credited(b, minute, out int given, out int givenCents);   // what the hours that ran brought
                if (b.Flopped && given <= 0 && !on)
                {
                    r.Text = FlareText.Fill("reallife_flare_kit_result_flopped", "business", business);
                    r.Flags |= FlareRowFlags.Bad;
                }
                else
                {
                    r.Text = FlareText.Fill(b.WentViral ? "reallife_flare_kit_result_viral" : "reallife_flare_kit_result_post", "business", business, "visitors",
                        FlareText.N(given));
                    r.Value = Signed(givenCents);
                    r.Flags |= givenCents > 0 ? FlareRowFlags.Good : FlareRowFlags.None;
                }
                r.Line = on ? FlareText.Fill("reallife_flare_kit_result_on", "hours", Math.Max(1, (b.FromMinute + b.Hours * 60 - minute + 59) / 60).ToString(CultureInfo.InvariantCulture),
                        "visitors", FlareText.N(b.Visitors))
                    : FlareText.Fill("reallife_flare_kit_result_done", "time", Ago(p.At, now));
            }
            _flare.AdFunnels(_adFunnels, AdsMost);
            for (int i = 0; i < _adFunnels.Count && _m.List.Count < ResultsMost; i++)
            {
                if (i == 0)
                {
                    Section(FlareText.T("reallife_flare_kit_results_ads"));
                }
                FunnelRow(_adFunnels[i], _flare.BusinessNameAt(_adFunnels[i].Place) ?? "?");
            }
            bool campaigns = false;
            for (int i = file.Hires.Count - 1; i >= 0 && _m.List.Count < ResultsMost; i--)
            {
                FlareHire h = file.Hires[i];
                if (h.Ended || today > h.To)
                {
                    continue;
                }
                if (!campaigns)
                {
                    Section(FlareText.T("reallife_flare_kit_results_campaigns"));
                    campaigns = true;
                }
                FlareInfluencer lead = h.Who.Count > 0 ? file.Influencer(h.Who[0]) : null;
                ref FlareRow r = ref Add(_m.List, lead != null ? RowAct.Conversation : RowAct.None, null, lead != null ? "i:" + lead.Id.ToString(CultureInfo.InvariantCulture) : null);
                r.Kind = FlareRowKind.Result;
                r.Icon = UiIcon.Megaphone;
                r.Tint = UiRole.Chart2;
                r.Text = FlareText.Fill("reallife_flare_kit_campaign_running", "handle", lead != null ? lead.Handle : "?", "business", h.Name ?? "?");
                FlareDeskRules.CampaignSoFar(file, h, today, out int so, out long soCents);
                string days = today < h.From
                    ? FlareText.Fill("reallife_flare_kit_campaign_starts", "day", h.From.ToString(CultureInfo.InvariantCulture))
                    : FlareText.Fill("reallife_flare_kit_campaign_day", "day", (today - h.From + 1).ToString(CultureInfo.InvariantCulture), "days",
                        (h.To - h.From + 1).ToString(CultureInfo.InvariantCulture));
                r.Line = today < h.From ? days : days + " · " + FlareText.Fill("reallife_flare_kit_campaign_so_far", "visitors", FlareText.N(so), "money", Signed(soCents));
                r.Value = FlareText.Fill("reallife_flare_kit_campaign_percent", "percent", h.Percent.ToString(CultureInfo.InvariantCulture));
                _refs[_refs.Count - 1] = new RowRef { Act = RowAct.Campaign, Key = _refs[_refs.Count - 1].Key, Id = h.Id };
                bool armed = _kit.EndArmed == h.Id;
                r.Button = FlareText.T(armed ? "reallife_flare_kit_end_sure" : "reallife_flare_kit_end_campaign");
                r.Flags |= armed ? FlareRowFlags.Chosen : FlareRowFlags.None;
            }
            bool done = false;
            for (int i = file.PastHires.Count - 1; i >= 0 && _m.List.Count < ResultsMost; i--)
            {
                FlarePastHire h = file.PastHires[i];
                if (!done)
                {
                    if (!campaigns)
                    {
                        Section(FlareText.T("reallife_flare_kit_results_campaigns"));
                        campaigns = true;
                    }
                    done = true;
                }
                FlareInfluencer inf = file.Influencer(h.Who);
                ref FlareRow r = ref Add(_m.List, inf != null ? RowAct.Conversation : RowAct.None, null, inf != null ? "i:" + inf.Id.ToString(CultureInfo.InvariantCulture) : null);
                r.Kind = FlareRowKind.Result;
                r.Icon = UiIcon.Megaphone;
                r.Tint = h.Flopped ? UiRole.DangerText : UiRole.OkText;
                string handle = inf != null ? inf.Handle : "?";
                r.Text = h.Flopped ? FlareText.Fill("reallife_flare_kit_campaign_flopped", "handle", handle, "business", h.Name ?? "?")
                    : FlareText.Fill("reallife_flare_kit_campaign_brought", "handle", handle, "business", h.Name ?? "?", "visitors", FlareText.N(h.Visitors));
                r.Line = FlareText.Fill("reallife_flare_kit_result_done", "time", Ago(FlareLiveRules.Now(h.Day, FlareLiveRules.NoticeHour, 0), now));
                r.Value = Signed(h.Cents);
                r.Flags |= h.Flopped ? FlareRowFlags.Bad : h.Cents > 0 ? FlareRowFlags.Good : FlareRowFlags.None;
            }
            _m.List.EmptyIcon = UiIcon.ChartLineUp;
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_no_results");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_no_results_body");
        }

        // ------------------------------------------------------------------------------------------------ Hire

        private void Hire(FlareFile file, long now)
        {
            int today = (int)(now / 1440);
            List<FlareShop> shops = _flare.MyShops();
            _m.Head = FlareText.T("reallife_flare_kit_hire_title");
            _hireDistricts.Clear();
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf.Active && inf.District != null && !_hireDistricts.Contains(inf.District))
                {
                    _hireDistricts.Add(inf.District);
                }
            }
            _hireDistricts.Sort(string.CompareOrdinal);
            _district = Math.Max(0, Math.Min(_district, _hireDistricts.Count));
            string[] picks = new string[_hireDistricts.Count + 1];
            picks[0] = FlareText.T("reallife_flare_kit_all_districts");
            for (int i = 0; i < _hireDistricts.Count; i++)
            {
                picks[i + 1] = FlareText.District(_hireDistricts[i]);
            }
            _m.Business.Picker = picks;
            _m.Business.Chosen = _district;
            string district = _district > 0 ? _hireDistricts[_district - 1] : null;
            string place = shops.Count > 0 ? shops[Math.Max(0, Math.Min(_shop, shops.Count - 1))].Place : null;
            string[] niches = new string[FlareDeskRules.NicheCount + 1];
            niches[0] = FlareText.T("reallife_flare_kit_all_niches");
            for (int i = 1; i < niches.Length; i++)
            {
                niches[i] = FlareText.Niche((FlareNiche)(i - 1));
            }
            _m.Business.Picker2 = niches;
            _m.Business.Chosen2 = _kit.Niche;
            _m.Business.Sort = new[] { FlareText.T("reallife_flare_kit_sort_followers"), FlareText.T("reallife_flare_kit_sort_fit") };
            _m.Business.SortChosen = _kit.Sort;
            _hireable.Clear();
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf.Active && (district == null || inf.District == district) && FlareDeskRules.NicheShown(inf, _kit.Niche))
                {
                    _hireable.Add(inf);
                }
            }
            FlareShop chosen = shops.Count > 0 ? shops[Math.Max(0, Math.Min(_shop, shops.Count - 1))] : default(FlareShop);
            FlareDeskRules.Sort(_hireable, _kit.Sort == 1 && shops.Count > 0, chosen);
            // The one the table has chosen (the first until another is pressed, or when the filters took it out): the card under the table is theirs.
            int pick = 0;
            for (int i = 0; i < _hireable.Count && _kit.HireSel >= 0; i++)
            {
                pick = _hireable[i].Id == _kit.HireSel ? i : pick;
            }
            _m.Hire.Shown = false;
            for (int i = 0; i < _hireable.Count && _m.List.Count < InfluencersMost; i++)
            {
                InfluencerRow(_hireable[i], place, today, shops.Count > 0 ? (FlareShop?)chosen : null, i == pick);
                if (i == pick)
                {
                    HireCard(file, _hireable[i], place, shops.Count > 0 ? (FlareShop?)chosen : null, _m.List.Count - 1, today, now);
                }
            }
            _m.HeadLine = shops.Count == 0 ? FlareText.T("reallife_flare_kit_hire_need_business")
                : FlareText.Fill("reallife_flare_kit_hire_line", "count", FlareText.N(_hireable.Count), "business", shops[Math.Max(0, Math.Min(_shop, shops.Count - 1))].Name ?? "?");
            _m.List.EmptyIcon = UiIcon.People;
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_no_influencers");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_no_influencers_body");
        }

        private void InfluencerRow(FlareInfluencer inf, string place, int today, FlareShop? shop, bool picked)
        {
            string key = "i:" + inf.Id.ToString(CultureInfo.InvariantCulture);
            Account(key, out string name, out string handle, out _, out bool verified);
            FlareExpect ex = _flare.HireExpect(inf, place);
            bool booked = inf.BookedUntil >= today && !string.IsNullOrEmpty(inf.BookedBy);
            ref FlareRow r = ref Add(_m.List, RowAct.Hire, null, key, inf.Id);
            r.Kind = FlareRowKind.Influencer;
            r.Name = name;
            r.Handle = FlareText.Fill("reallife_flare_kit_influencer_line", "handle", handle, "niche", FlareText.Niche(inf.Niche), "district", FlareText.District(inf.District));
            r.Hue = UiColour.HueOf(key);
            r.Flags = (verified ? FlareRowFlags.Verified : FlareRowFlags.None) | (picked ? FlareRowFlags.Chosen : FlareRowFlags.None);
            Look(key, ref r);
            int fitPercent = shop.HasValue ? FlareDeskRules.FitPercent(FlareDeskRules.FitOf(inf, shop.Value)) : -1;
            string fit = fitPercent >= 0 ? fitPercent.ToString(CultureInfo.InvariantCulture) + "%" : "-";
            // A bar behind the engagement (against twelve in a hundred, a great deal) and the fit (a whole hundred is a perfect one).
            r.Shares = new[] { -1f, Math.Min(1f, inf.Engagement / EngagementGreat), fitPercent >= 0 ? fitPercent / 100f : -1f, -1f, -1f };
            r.Tags = new[]
            {
                FlareText.T("reallife_flare_kit_fig_followers"), FlareFeedRules.Count(inf.Followers),
                FlareText.T("reallife_flare_kit_fig_engagement"), (inf.Engagement / 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%",
                FlareText.T("reallife_flare_kit_fig_fit"), fit,
                FlareText.T("reallife_flare_kit_fig_price"), FlareText.Money(Prices.At(FlareCast.Price(inf))),
                FlareText.T("reallife_flare_kit_fig_expected"), FlareText.Fill("reallife_flare_kit_visitors_n", "count", FlareText.N(ex.Visitors))
            };
            r.Button = FlareText.T(booked ? "reallife_flare_kit_booked" : "reallife_flare_kit_hire");
            if (booked || place == null)
            {
                r.Flags |= FlareRowFlags.Off;
            }
        }

        private const float EngagementGreat = 1200f;   // hundredths of a per cent: twelve in a hundred answering is the most a bar shows

        // The chosen influencer under the table: the audience in the city, the last campaigns they ran, what hiring them for the business should bring, and the button.
        private void HireCard(FlareFile file, FlareInfluencer inf, string place, FlareShop? shop, int row, int today, long now)
        {
            FlareHireModel d = _m.Hire;
            string key = "i:" + inf.Id.ToString(CultureInfo.InvariantCulture);
            Account(key, out string name, out _, out _, out bool verified);
            d.Shown = true;
            d.Name = name;
            d.Hue = UiColour.HueOf(key);
            d.Ring = UiTokens.Grade(inf.Reliability);
            d.Verified = verified;
            d.Row = row;
            FlareExpect ex = _flare.HireExpect(inf, place);
            // Only what the table above does not say: its row already has the followers, the price of a post and the likely visits.
            d.ForecastLabels = new[] { FlareText.T("reallife_flare_kit_fig_takings") };
            d.ForecastFigures = new[] { Signed(ex.Cents) };
            bool booked = inf.BookedUntil >= today && !string.IsNullOrEmpty(inf.BookedBy);
            d.Hire = place == null || booked ? null : FlareText.Fill("reallife_flare_kit_hire_pay", "money", FlareText.Money(Prices.At(FlareCast.Price(inf))));
            d.HireOff = false;
            bool fits = shop.HasValue && shop.Value.District != null && shop.Value.District == inf.District;
            d.Note = FlareFigures.Bind(FlareText.Fill(fits ? "reallife_flare_kit_hire_card_fits" : "reallife_flare_kit_hire_card_far", "flop", FlareFigures.Odds(ex.Flop), "viral",
                FlareFigures.Odds(ex.Viral)));   // "12 in 100" is not parted from its noun when the note wraps
            d.AudienceTitle = FlareText.T("reallife_flare_kit_audience_title");
            d.AudienceEmpty = FlareText.T("reallife_flare_kit_hire_audience_none");
            if (_flare.AudienceOf(inf.Id, _bandNames, _bandCounts, out int rest, out int total) && _bandCounts.Count > 0)
            {
                FlareFigures.Audience(_bandNames, _bandCounts, total, FlareText.District, out d.AudienceLabels, out d.AudienceFigures, out d.AudienceShares);   // the sheet reads the same
            }
            else
            {
                d.AudienceLabels = d.AudienceFigures = None;
                d.AudienceShares = new float[0];
            }
            d.LastTitle = FlareText.T("reallife_flare_kit_hire_last");
            d.LastEmpty = FlareText.T("reallife_flare_kit_hire_last_none");
            int shown = 0;
            string[] lines = new string[3], figures = new string[3];
            int[] tones = new int[3];
            for (int i = file.PastHires.Count - 1; i >= 0 && shown < 3; i--)
            {
                FlarePastHire h = file.PastHires[i];
                if (h.Who != inf.Id)
                {
                    continue;
                }
                lines[shown] = (h.Name ?? "?") + ", " + Ago(FlareLiveRules.Now(h.Day, FlareLiveRules.NoticeHour, 0), now);
                figures[shown] = h.Flopped ? FlareText.T("reallife_flare_kit_result_flop") : FlareText.Fill("reallife_flare_kit_visitors_n", "count", FlareText.N(h.Visitors));
                tones[shown] = h.Flopped ? -1 : h.Cents > 0 ? 1 : 0;
                shown++;
            }
            d.LastLines = new string[shown];
            d.LastFigures = new string[shown];
            d.LastTones = new int[shown];
            Array.Copy(lines, d.LastLines, shown);
            Array.Copy(figures, d.LastFigures, shown);
            Array.Copy(tones, d.LastTones, shown);
        }

        // ------------------------------------------------------------------------------------------------ in the city

        private void City(FlareFile file, long now)
        {
            int today = (int)(now / 1440);
            _m.Head = FlareText.T("reallife_flare_nav_city");
            _m.HeadLine = FlareText.T("reallife_flare_kit_city_line");
            List<FlareShop> myShops = _flare.MyShops();
            bool mine = myShops.Count > 0;
            CityBand(file, now);
            // Demand: what the residents want, and where.
            FlareCityView trends = _flare.CityOn ? _flare.CityTrendsView() : null;
            if (trends != null && trends.Rows.Count > 0)
            {
                Section(FlareText.T("reallife_flare_kit_city_demand"));
                for (int i = 0; i < trends.Rows.Count && _m.List.Count < CardsMost; i++)
                {
                    FlareViewRow t = trends.Rows[i];
                    ref FlareRow r = ref Add(_m.List, mine ? RowAct.Trend : RowAct.None, null, mine ? FlareCityViews.TrendTag(t.What) : null, mine ? ShopIn(myShops, t.Where) : 0);
                    r.Kind = FlareRowKind.Card;
                    r.Icon = UiIcon.ChartLineUp;
                    r.Tint = UiRole.OkText;
                    r.Name = FlareText.Fill(t.Title, "what", FlareText.T(t.What), "district", t.Where != null ? FlareGame.DistrictName(t.Where) : string.Empty);
                    r.Text = FlareText.Fill("reallife_flare_kit_trend_card", "stage", FlareText.T(t.Subtitle), "moved", FlareText.N((int)t.Value), "days", FlareText.N((int)t.Second));
                    r.Line = t.Where != null ? FlareText.District(t.Where) : FlareText.T("reallife_flare_kit_trend_city");
                    r.Button = mine ? FlareText.T("reallife_flare_kit_promote_there") : null;
                }
            }
            // The city's businesses: what they did on Flare these two days (promoted, ran the player's business down).
            bool rivals = false;
            for (int i = file.Posts.Count - 1; i >= 0 && _m.List.Count < CardsMost; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || today - p.Day > 1 || p.Author != FlareAuthor.Business || p.Source != FlareSource.BusinessPost)
                {
                    continue;
                }
                bool attack = p.Arg == FlareDirector.RivalAttackArg, paid = _flare.RivalPaid(p);
                if (!attack && !paid)
                {
                    continue;
                }
                if (!rivals)
                {
                    Section(FlareText.T("reallife_flare_kit_city_rivals"));
                    rivals = true;
                }
                ref FlareRow r = ref Add(_m.List, attack ? RowAct.Conversation : RowAct.Thread, p, attack ? "k:" + p.Name : FlareLiveRules.KeyOf(p));
                _bodyAccounts.Add("k:" + p.Name);   // the card names the business: the side does not offer it to follow beside it
                r.Kind = FlareRowKind.Card;
                r.Icon = attack ? UiIcon.Warning : UiIcon.Megaphone;
                r.Tint = attack ? UiRole.DangerText : UiRole.Chart2;
                r.Name = p.Name ?? "?";
                r.Time = Ago(p.At, now);
                r.Text = FlareText.Fill(attack ? "reallife_flare_kit_rival_attack" : "reallife_flare_kit_rival_promo", "district", FlareText.District(p.District));
                r.Line = FlareFeedRules.Plain(_flare.WordsFor(p, p.Name));
                r.Button = FlareText.T(attack ? "reallife_flare_kit_answer" : "reallife_flare_kit_view");
            }
            CityMoves(now);
            // Events: what is on, and going.
            bool events = false;
            for (int i = file.Posts.Count - 1; i >= 0 && _m.List.Count < CardsMost; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || today - p.Day > 1 || p.Source != FlareSource.Invite || Hidden(file, p))
                {
                    continue;
                }
                if (!events)
                {
                    Section(FlareText.T("reallife_flare_kit_city_events"));
                    events = true;
                }
                Author(p, out string name, out _, out string key, out _);
                bool going = _flare.IsGoing(p.Id);
                ref FlareRow r = ref Add(_m.List, RowAct.Going, p, key);
                _bodyAccounts.Add(key ?? string.Empty);
                r.Kind = FlareRowKind.Card;
                r.Icon = UiIcon.Calendar;
                r.Tint = UiRole.AccentText;
                r.Name = FlareText.InviteName(p) ?? name;
                r.Time = Ago(p.At, now);
                r.Text = FlareFeedRules.Plain(p.Text ?? _flare.WordsFor(p, name));
                r.Line = name;
                r.Button = FlareText.T(going ? "reallife_flare_kit_going" : "reallife_flare_kit_go");
                r.Flags |= going ? FlareRowFlags.Chosen : FlareRowFlags.None;
            }
            // Property and jobs: the city's offers of paid posts, and the businesses that are hiring.
            bool work = false;
            FlareCityView offers = _flare.CityOn ? _flare.CityOffersView() : null;
            for (int i = 0; offers != null && i < offers.Rows.Count && _m.List.Count < CardsMost; i++)
            {
                FlareOffer o = _flare.OfferOf(offers.Rows[i].Post);
                if (o == null || !o.Open(today))
                {
                    continue;
                }
                if (!work)
                {
                    Section(FlareText.T("reallife_flare_kit_city_work"));
                    work = true;
                }
                ref FlareRow r = ref Add(_m.List, RowAct.Conversation, null, FlareLiveRules.OfferKey(o.Id));
                r.Kind = FlareRowKind.Card;
                r.Icon = UiIcon.Coin;
                r.Tint = UiRole.Gold;
                r.Name = o.Name ?? "?";
                r.Text = FlareText.Fill("reallife_flare_kit_offer_card", "money", FlareText.Money((int)(o.Fee / 100)), "posts", o.Posts.ToString(CultureInfo.InvariantCulture));
                r.Line = FlareText.Fill("reallife_flare_kit_offer_until", "days", Math.Max(0, o.Until - today).ToString(CultureInfo.InvariantCulture));
                r.Button = FlareText.T("reallife_flare_kit_view");
            }
            for (int i = file.Posts.Count - 1; i >= 0 && _m.List.Count < CardsMost; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.At > now || today - p.Day > 2 || Hidden(file, p) || !PropertyOrJob(p))
                {
                    continue;
                }
                if (!work)
                {
                    Section(FlareText.T("reallife_flare_kit_city_work"));
                    work = true;
                }
                Author(p, out string name, out _, out string key, out _);
                ref FlareRow r = ref Add(_m.List, RowAct.Thread, p, key);
                r.Kind = FlareRowKind.Card;
                r.Icon = p.Author == FlareAuthor.Official ? UiIcon.Building : UiIcon.Briefcase;
                r.Tint = UiRole.Text2;
                r.Name = name;
                r.Time = Ago(p.At, now);
                r.Text = FlareFeedRules.Plain(p.Text ?? _flare.WordsFor(p, name));
                r.Button = FlareText.T("reallife_flare_kit_view");
            }
            _m.List.EmptyIcon = UiIcon.Building;
            // Says only what is missing here (the rail's trending tags are another list, and a page that lists them is not "quiet").
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_city_idle");
            _m.List.EmptyBody = FlareText.T(_flare.CityOn ? "reallife_flare_kit_city_idle_body" : "reallife_flare_kit_city_off_body");
        }

        // The followers by district as a donut with its legend, and the week of what the posts did; none without the residents' side.
        private readonly List<string> _bandNames = new List<string>(FlareDeskRules.DistrictsShown);
        private readonly List<int> _bandCounts = new List<int>(FlareDeskRules.DistrictsShown);

        private void CityBand(FlareFile file, long now)
        {
            FlareCityModel c = _m.City;
            c.Shown = _flare.CityOn;
            c.DonutTitle = c.DonutEmpty = c.DonutRest = c.DonutTotal = c.WeekTitle = null;
            c.Names = c.Values = c.WeekLabels = c.WeekFigures = None;
            c.Shares = new float[0];
            c.Rest = 0f;
            if (!c.Shown)
            {
                return;
            }
            bool fresh = FlareFigures.NewAccount(WeekFacts(file, now));   // nothing to count: the page says so once, in the donut's words
            c.DonutTitle = FlareText.T("reallife_flare_kit_donut_title");
            c.DonutEmpty = FlareText.T("reallife_flare_kit_donut_empty");
            if (_flare.FollowersInDistricts("me", _bandNames, _bandCounts, out int rest, out int total))
            {
                int n = _bandNames.Count;
                c.Names = new string[n];
                c.Values = new string[n];
                c.Shares = new float[n];
                for (int i = 0; i < n; i++)
                {
                    c.Names[i] = FlareText.District(_bandNames[i]);
                    c.Values[i] = FlareText.N(_bandCounts[i]);
                    c.Shares[i] = _bandCounts[i];
                }
                c.Rest = rest;
                c.DonutRest = rest > 0 ? FlareText.Fill("reallife_flare_kit_donut_rest", "count", FlareText.N(rest)) : null;
                c.DonutTotal = FlareFigures.DonutTotal(total, file.Followers, Words);   // the slices add up to this, and the rail's followers are the whole
            }
            else if (!fresh)
            {
                // Followers the city has not placed in a district yet: "nobody follows you" would contradict the followers the side counts, so the page draws no donut at all.
                c.DonutTitle = c.DonutEmpty = null;
            }
            if (fresh)
            {
                return;   // no week of zeros beside the sentence
            }
            c.WeekTitle = FlareText.T("reallife_flare_kit_city_week");
            c.WeekLabels = new[] { FlareText.T("reallife_flare_kit_fig_seen"), FlareText.T("reallife_flare_kit_fig_visits"), FlareText.T("reallife_flare_kit_fig_takings") };
            if (_flare.WeekInCity(out int seen, out int came, out long spent) && (seen != 0 || came != 0 || spent != 0))
            {
                c.WeekFigures = new[] { FlareFeedRules.Count(seen), FlareText.N(came), Signed(spent) };
            }
            else
            {
                c.WeekFigures = new[] { FlareText.N(0), FlareText.N(0), FlareText.Money(0) };   // a week with nothing measured is a real zero
            }
        }

        // The rivals' new shops and the premises to let, in cards without a button: they are news, not something to press.
        private void CityMoves(long now)
        {
            List<FlareCityMove> moves = _flare.CityOn ? _flare.CityMoves() : null;
            bool head = false;
            for (int i = 0; moves != null && i < moves.Count && _m.List.Count < CardsMost; i++)
            {
                FlareCityMove mv = moves[i];
                if (!head)
                {
                    Section(FlareText.T("reallife_flare_kit_city_moves"));
                    head = true;
                }
                ref FlareRow r = ref Add(_m.List, RowAct.None);
                r.Kind = FlareRowKind.Card;
                bool outlet = mv.Kind == FlareMoveKind.Outlet;
                r.Icon = outlet ? UiIcon.Store : UiIcon.Key;
                r.Tint = outlet ? UiRole.DangerText : UiRole.AccentText;
                r.Name = outlet ? FlareText.Fill("reallife_flare_kit_move_outlet", "by", mv.By ?? "?", "district", FlareText.District(mv.District))
                    : FlareText.Fill("reallife_flare_kit_move_to_let", "district", FlareText.District(mv.District));
                r.Text = outlet ? FlareText.Fill("reallife_flare_kit_move_outlet_line", "business", mv.Name ?? "?")
                    : FlareText.Fill(mv.Count == 1 ? "reallife_flare_kit_move_to_let_one" : "reallife_flare_kit_move_to_let_line", "count", FlareText.N(mv.Count));
                r.Time = outlet && mv.Day >= 0 ? Ago(FlareLiveRules.Now(mv.Day, 8, 0), now) : null;
            }
        }

        // Which of the player's businesses a trend in a district is about: the one in that district, else the first (the index the trend card's Promote opens with).
        private static int ShopIn(List<FlareShop> shops, string district)
        {
            for (int i = 0; district != null && i < shops.Count; i++)
            {
                if (shops[i].District == district)
                {
                    return i;
                }
            }
            return 0;
        }

        // A post about offers, property or work: the City Office's notices and policies and the bank's rate, a business of the city offering or hiring,
        // the talk of the market and of prices. (A post's Arg is its kind times ten and a variant: the kind is read from there.)
        private static bool PropertyOrJob(FlarePost p)
        {
            if (p.Author == FlareAuthor.Official)
            {
                int kind = p.Arg / 10;
                return p.Source == FlareSource.Official && (kind == (int)FlareOfficialPost.Notice || kind == (int)FlareOfficialPost.Policy || kind == (int)FlareOfficialPost.Rate);
            }
            if (p.Author == FlareAuthor.Business && p.Source == FlareSource.BusinessPost)
            {
                return p.Arg == 0 || p.Arg == 4 || (p.Arg >= FlareLiveRules.OfferFirst && p.Arg < FlareLiveRules.OfferFirst + FlareLiveRules.OfferKinds);
            }
            return p.Source == FlareSource.CityTalk && (p.Arg / 10 == (int)FlareTalk.Market || p.Arg / 10 == (int)FlareTalk.Prices);
        }
    }
}
