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
            _m.HeadLine = FlareText.Fill("reallife_flare_kit_business_line", "kind", FlareText.Niche(shop.Niche), "district", FlareText.District(shop.District));
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
            r.Stars = FlareRules.Clamp(p.Arg, 1, 5);
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
            _m.HeadLine = FlareText.Fill("reallife_flare_kit_studio_line", "tier", FlareText.Tier(file.Tier), "followers", FlareFeedRules.Count(file.Followers));
            int gain = FlareFeedRules.WeekGain(file.History, file.Followers);
            FlareFeedRules.Reach(file.Posts, today, out long week, out long before);
            int change = FlareFeedRules.Change(week, before);
            Brought(file, null, now, out int visitors, out long cents, out int visitorsBefore, out long centsBefore);
            st.Labels = new[]
            {
                FlareText.T("reallife_flare_kit_fig_followers"), FlareText.T("reallife_flare_kit_fig_reach"), FlareText.T("reallife_flare_kit_fig_visitors"),
                FlareText.T("reallife_flare_kit_fig_money")
            };
            st.Figures = new[] { FlareFeedRules.Count(file.Followers), FlareFeedRules.Count(week), FlareText.N(visitors), FlareText.Money(cents / 100) };
            st.Deltas = new[]
            {
                FlareText.Fill("reallife_flare_kit_week_followers", "change", FlareText.Signed(gain)),
                FlareText.Fill("reallife_flare_kit_week_reach", "change", FlareText.Signed(change)),
                FlareText.Fill("reallife_flare_kit_week_before", "change", FlareText.Signed(visitors - visitorsBefore)),
                FlareText.Fill("reallife_flare_kit_week_before", "change", Signed(cents - centsBefore))
            };
            st.Tones = new[] { Math.Sign(gain), Math.Sign(change), Math.Sign(visitors - visitorsBefore), Math.Sign(cents - centsBefore) };
            st.LineTitle = FlareText.T("reallife_flare_kit_line_title");
            st.Line = FlareFeedRules.Line(file.History, file.Followers);
            Tier(file, st);
            Results(file, now, today);
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
                r.Line = today < h.From
                    ? FlareText.Fill("reallife_flare_kit_campaign_starts", "day", h.From.ToString(CultureInfo.InvariantCulture))
                    : FlareText.Fill("reallife_flare_kit_campaign_day", "day", (today - h.From + 1).ToString(CultureInfo.InvariantCulture), "days",
                        (h.To - h.From + 1).ToString(CultureInfo.InvariantCulture));
                r.Value = FlareText.Fill("reallife_flare_kit_campaign_percent", "percent", h.Percent.ToString(CultureInfo.InvariantCulture));
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
            _hireable.Clear();
            for (int i = 0; i < file.Influencers.Count; i++)
            {
                FlareInfluencer inf = file.Influencers[i];
                if (inf.Active && (district == null || inf.District == district))
                {
                    _hireable.Add(inf);
                }
            }
            _hireable.Sort((a, b) => b.Followers != a.Followers ? b.Followers.CompareTo(a.Followers) : a.Id.CompareTo(b.Id));
            for (int i = 0; i < _hireable.Count && _m.List.Count < InfluencersMost; i++)
            {
                InfluencerRow(_hireable[i], place, today);
            }
            _m.HeadLine = shops.Count == 0 ? FlareText.T("reallife_flare_kit_hire_need_business")
                : FlareText.Fill("reallife_flare_kit_hire_line", "count", FlareText.N(_hireable.Count), "business", shops[Math.Max(0, Math.Min(_shop, shops.Count - 1))].Name ?? "?");
            _m.List.EmptyIcon = UiIcon.People;
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_no_influencers");
            _m.List.EmptyBody = FlareText.T("reallife_flare_kit_no_influencers_body");
        }

        private void InfluencerRow(FlareInfluencer inf, string place, int today)
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
            r.Flags = verified ? FlareRowFlags.Verified : FlareRowFlags.None;
            r.Tags = new[]
            {
                FlareText.T("reallife_flare_kit_fig_followers"), FlareFeedRules.Count(inf.Followers),
                FlareText.T("reallife_flare_kit_fig_engagement"), (inf.Engagement / 100f).ToString("0.0", CultureInfo.InvariantCulture) + "%",
                FlareText.T("reallife_flare_kit_fig_price"), FlareText.Money(Prices.At(FlareCast.Price(inf))),
                FlareText.T("reallife_flare_kit_fig_expected"), FlareText.Fill("reallife_flare_kit_visitors_n", "count", FlareText.N(ex.Visitors))
            };
            r.Button = FlareText.T(booked ? "reallife_flare_kit_booked" : "reallife_flare_kit_hire");
            if (booked || place == null)
            {
                r.Flags |= FlareRowFlags.Off;
            }
        }

        // ------------------------------------------------------------------------------------------------ in the city

        private void City(FlareFile file, long now)
        {
            int today = (int)(now / 1440);
            _m.Head = FlareText.T("reallife_flare_nav_city");
            _m.HeadLine = FlareText.T("reallife_flare_kit_city_line");
            bool mine = _flare.MyShops().Count > 0;
            // Demand: what the residents want, and where.
            FlareCityView trends = _flare.CityOn ? _flare.CityTrendsView() : null;
            if (trends != null && trends.Rows.Count > 0)
            {
                Section(FlareText.T("reallife_flare_kit_city_demand"));
                for (int i = 0; i < trends.Rows.Count && _m.List.Count < CardsMost; i++)
                {
                    FlareViewRow t = trends.Rows[i];
                    ref FlareRow r = ref Add(_m.List, mine ? RowAct.Trend : RowAct.None, null, null);
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
                r.Kind = FlareRowKind.Card;
                r.Icon = attack ? UiIcon.Warning : UiIcon.Megaphone;
                r.Tint = attack ? UiRole.DangerText : UiRole.Chart2;
                r.Name = p.Name ?? "?";
                r.Time = Ago(p.At, now);
                r.Text = FlareText.Fill(attack ? "reallife_flare_kit_rival_attack" : "reallife_flare_kit_rival_promo", "district", FlareText.District(p.District));
                r.Line = FlareFeedRules.Plain(_flare.WordsFor(p, p.Name));
                r.Button = FlareText.T(attack ? "reallife_flare_kit_answer" : "reallife_flare_kit_view");
            }
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
            _m.List.EmptyTitle = FlareText.T("reallife_flare_kit_city_empty");
            _m.List.EmptyBody = FlareText.T(_flare.CityOn ? "reallife_flare_kit_city_empty_body" : "reallife_flare_kit_city_off_body");
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
