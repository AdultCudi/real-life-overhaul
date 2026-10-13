using System.Collections.Generic;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Flare
{
    /// <summary>
    /// The four tabs' pages. Feed: posts newest first, a post's words, likes and replies in the box. Post: the drafts,
    /// and for the one picked its words as they will read, the tone, the tag and the Post button (or when it can be
    /// posted). Me: the account and the player's own posts. Business: the player's businesses and what was posted
    /// about each. The box does not scroll, so a page is a title and a handful of lines.
    /// </summary>
    public sealed partial class FlareApp
    {
        private const int RepliesShown = 4;

        private int _tone = -1;     // the tone picked for the draft that shows; -1 for the one that suits it best
        private bool _untag;        // the draft's friend is not to be tagged

        private void ResetPages()
        {
            _tone = -1;
            _untag = false;
            _hireOpen = false;
            _hireWho = 0;
            _hireDeal = 0;
        }

        // ---------------------------------------------------------------- the Feed

        private TableSpec WriteFeed(InfoBox box, FlareFile file, int day, ref string selected)
        {
            FlarePost picked = null;
            int shown = 0;
            for (int i = file.Posts.Count - 1; i >= 0 && shown < FlareRules.FeedRows; i--)
            {
                FlarePost p = file.Posts[i];
                TableRow row = Row("post:" + p.Id);
                row.Kind = p.Author == FlareAuthor.Player ? FlareTables.IsYours : p.Author == FlareAuthor.Friend ? FlareTables.IsFriends
                    : FlareCast.Followed(file, FlareCast.KeyOf(p)) ? FlareTables.IsFollowed : FlareTables.IsAccounts;
                row.Set(0, AuthorName(p, file));
                row.Set(1, Words(p));
                row.Set(2, FlareText.N(p.Likes), p.Likes);
                row.Set(3, FlareText.N(p.Replies), p.Replies);
                row.Set(4, FlareText.When(p.Day, p.Hour), p.Day * 24 + p.Hour);
                picked = Is(row, ref selected) ? p : picked;
                shown++;
            }
            if (picked != null)
            {
                WritePost(box, file, picked);
            }
            else
            {
                box.Title(FlareText.T("reallife_flare_trending"));
                Trending(box, file, day);
                box.Line(FlareText.T("reallife_flare_intro_what"));
                box.Line(FlareText.T("reallife_flare_intro_post"));
            }
            return FlareTables.Feed;
        }

        // Trending: the day's most liked post, the market's biggest mover, the busiest business.
        private void Trending(InfoBox box, FlareFile file, int day)
        {
            FlarePost top = null;
            for (int i = 0; i < file.Posts.Count; i++)
            {
                FlarePost p = file.Posts[i];
                top = p.Day == day && (top == null || p.Likes > top.Likes) ? p : top;
            }
            int lines = 0;
            if (top != null)
            {
                box.Line(FlareText.Fill("reallife_flare_trend_post", "who", AuthorName(top, file), "likes", FlareText.N(top.Likes)));
                lines++;
            }
            if (_director.MoverName != null)
            {
                box.Line(FlareText.Fill("reallife_flare_trend_mover", "name", _director.MoverName, "change", (_director.MoverBp >= 0 ? "+" : string.Empty) + (_director.MoverBp / 100f).ToString("0.0") + "%"));
                lines++;
            }
            string talked = null;
            int most = 0;
            for (int i = 0; i < file.Buzz.Count; i++)
            {
                int at = file.BuzzAt(file.Buzz[i].Place, day);
                if (at > most)
                {
                    most = at;
                    talked = file.Buzz[i].Place;
                }
            }
            BuildingRegistration reg = talked != null ? FlareGame.Business(talked) : null;
            if (reg != null)
            {
                box.Line(FlareText.Fill("reallife_flare_trend_shop", "name", FlareGame.Name(reg), "percent", most.ToString()));
                lines++;
            }
            if (lines == 0)
            {
                box.Line(FlareText.T("reallife_flare_trend_none"));
            }
        }

        private string Words(FlarePost p)
        {
            FlarePerson who = default(FlarePerson);
            bool friend = p.Author == FlareAuthor.Friend && _director.Person(p.Who, out who);
            FlareInfluencer inf = p.Author == FlareAuthor.Influencer && _director.File != null ? _director.File.Influencer(p.Who) : null;
            if (inf != null)
            {
                return FlareText.Post(p, null, null, FlareGame.DistrictName(inf.District), (int)inf.Niche);
            }
            return FlareText.Post(p, _director.PersonName(p.Tag), friend ? who.Job : string.Empty, friend ? who.District : string.Empty);
        }

        // A post in the box: its words, how far it went and what the friends said.
        private void WritePost(InfoBox box, FlareFile file, FlarePost p)
        {
            box.Title(AuthorName(p, file) + ", " + FlareText.When(p.Day, p.Hour));
            box.Line(Words(p));
            if (p.Source == FlareSource.Photo || p.Source == FlareSource.Selfie)
            {
                Texture2D picture = p.Author == FlareAuthor.Player ? _pictures.Of(file, p) : null;
                if (picture == null || box.Picture(picture, FlareRules.ThumbWidth, _pictures.Height) == null)
                {
                    box.Line(FlareText.Pair("reallife_flare_picture", p.Name ?? string.Empty));   // not one of the newest five, still being read, or the file is gone
                }
            }
            box.Line(FlareText.Fill("reallife_flare_post_figures", "reach", FlareText.N(p.Reach), "likes", FlareText.N(p.Likes), "replies", FlareText.N(p.Replies)));
            if (p.Author == FlareAuthor.Player)
            {
                box.Line(FlareText.Pair("reallife_flare_post_followers", FlareText.Signed(p.Gained)));
                if (p.Viral || p.Bad)
                {
                    box.Line(FlareText.T(p.Viral ? "reallife_flare_post_viral" : "reallife_flare_post_bad"));
                }
                int said = 0;
                IReadOnlyList<FlarePerson> people = _director.People;
                for (int i = 0; i < people.Count && said < RepliesShown; i++)
                {
                    FlarePerson person = people[i];
                    if (person.Index >= 0 && person.Index <= 30 && (p.Repliers & (1 << person.Index)) != 0)
                    {
                        box.Line(FlareText.Fill("reallife_flare_reply_line", "name", person.First, "words", FlareText.Reply(FlareRules.ReplyOf(person, p), p, person.Index)));
                        said++;
                    }
                }
            }
            string key = FlareCast.KeyOf(p);
            if (key != null)
            {
                bool following = FlareCast.Followed(file, key);
                Transform buttons = box.ButtonRow();
                InfoBox.Button(buttons, InfoBox.Positive, following ? "reallife_flare_unfollow" : "reallife_flare_follow", () => _director.Follow(key, !following));
                InfoBox.Button(buttons, InfoBox.Additional, "reallife_flare_back", Back);
                return;
            }
            BackButton(box);
        }

        // ---------------------------------------------------------------- Post

        private TableSpec WriteDrafts(InfoBox box, FlareFile file, int day, ref string selected)
        {
            FlareDraft picked = null;
            for (int i = 0; i < file.Drafts.Count; i++)
            {
                FlareDraft d = file.Drafts[i];
                TableRow row = Row("draft:" + d.Id);
                row.Set(0, FlareText.Draft(d, _director.PersonName(d.Person)));
                row.Set(1, FlareText.When(d.Day, d.Hour), d.Day * 24 + d.Hour);
                int worth = FlareRules.Quality(d.Source, d.Person >= 0, d.Place != null) / 10;
                row.Set(2, worth + "%", worth);
                picked = Is(row, ref selected) ? d : picked;
            }
            if (picked != null)
            {
                WriteDraft(box, file, picked, day);
                return FlareTables.Drafts;
            }
            box.Title(FlareText.T("reallife_flare_tab_post"));
            box.Line(FlareText.T("reallife_flare_post_intro"));
            box.Line(FlareText.Fill("reallife_flare_post_limits", "posts", FlareRules.PostsPerDay.ToString(), "hours", (FlareRules.PostGapMinutes / 60).ToString(), "days", FlareRules.DraftDays.ToString()));
            WaitLine(box, file, day);
            return FlareTables.Drafts;
        }

        private void WriteDraft(InfoBox box, FlareFile file, FlareDraft d, int day)
        {
            string friend = _director.PersonName(d.Person);
            bool canTag = friend != null;
            bool tag = canTag && !_untag;
            int tone = _tone >= 0 ? _tone : (int)FlareRules.BestTone(d.Source);
            box.Title(FlareText.Draft(d, friend));
            Choose(box, FlareText.ToneKeys.Length, tone, i => FlareText.Pair("reallife_flare_tone_label", FlareText.Tone(i)), i => _tone = i);
            if (canTag)
            {
                Choose(box, 2, _untag ? 1 : 0, i => FlareText.Fill(i == 0 ? "reallife_flare_tag_yes" : "reallife_flare_tag_no", "friend", friend), i => _untag = i == 1);
            }
            var preview = new FlarePost { Source = d.Source, Tone = (FlareTone)tone, Name = d.Name, Arg = d.Arg, Seed = file.NextId * 7919 };
            box.Line(FlareText.Pair("reallife_flare_preview", FlareText.Post(preview, tag ? friend : null, null, null)));
            int fit = FlareRules.ToneFit(d.Source, (FlareTone)tone);
            if (fit < FlareRules.BadFit)
            {
                box.Line(FlareText.T("reallife_flare_warn_bad"));
            }
            else
            {
                FlarePost last = file.LastOwn(d.Source);
                int repeat = FlareRules.Repeat(last != null ? last.Day : -1, day);
                int gain = FlareRules.Gain(file.Followers, FlareRules.Quality(d.Source, tag, d.Place != null), fit, file.Reputation, repeat);
                box.Line(FlareText.Fill("reallife_flare_expect", "followers", FlareText.N(gain)) + (repeat < 1000 ? " " + FlareText.T("reallife_flare_repeat") : string.Empty));
            }
            bool wait = WaitLine(box, file, day);
            Transform buttons = box.ButtonRow();
            int id = d.Id;
            FlareTone chosen = (FlareTone)tone;
            Button post = InfoBox.Button(buttons, InfoBox.Positive, "reallife_flare_post_button", () =>
            {
                if (_director.PostDraft(id, chosen, tag))
                {
                    _pick = null;
                    _tab = FlareDirector.TabMe;
                    ResetPages();
                    Refresh();
                }
            });
            if (post != null && wait)
            {
                post.interactable = false;
            }
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_flare_back", Back);
        }

        // When the player can post next, if not now. True if they cannot post now.
        private static bool WaitLine(InfoBox box, FlareFile file, int day)
        {
            FlareRules.Wait wait = FlareRules.CanPost(file, day, FlareGame.MinuteOfDay);
            if (wait == FlareRules.Wait.None)
            {
                return false;
            }
            int next = FlareRules.NextPostMinute(file, day);
            box.Line(wait == FlareRules.Wait.DayFull || next < 0 ? FlareText.T("reallife_flare_wait_day") : FlareText.Fill("reallife_flare_wait_soon", "time", FlareText.Clock(next)));
            return true;
        }

        // ---------------------------------------------------------------- Me

        private TableSpec WriteMe(InfoBox box, FlareFile file, int day, ref string selected)
        {
            FlarePost picked = null;
            for (int i = file.Posts.Count - 1; i >= 0; i--)
            {
                FlarePost p = file.Posts[i];
                if (p.Author != FlareAuthor.Player)
                {
                    continue;
                }
                TableRow row = Row("post:" + p.Id);
                row.Set(0, Words(p));
                row.Set(1, FlareText.When(p.Day, p.Hour), p.Day * 24 + p.Hour);
                row.Set(2, FlareText.N(p.Reach), p.Reach);
                row.Set(3, FlareText.N(p.Likes), p.Likes);
                row.Set(4, FlareText.Signed(p.Gained), p.Gained, p.Gained < 0 ? TableRow.Red : p.Viral ? TableRow.Green : TableRow.Plain);
                picked = Is(row, ref selected) ? p : picked;
            }
            if (picked != null)
            {
                WritePost(box, file, picked);
                return FlareTables.Mine;
            }
            box.Title(FlareText.T("reallife_flare_tab_me"));
            box.Line(FlareText.Pair("reallife_flare_me_followers", FlareText.N(file.Followers)));
            int next = FlareRules.NextTierAt(file.Followers);
            box.Line(FlareText.Pair("reallife_flare_me_tier", FlareText.Tier(file.Tier))
                     + (next > 0 ? " " + FlareText.Fill("reallife_flare_me_next", "tier", FlareText.Tier(FlareRules.TierOf(next)), "followers", FlareText.N(next)) : string.Empty));
            box.Line(FlareText.Pair("reallife_flare_me_reputation", FlareText.Reputation(file.Reputation) + " (" + file.Reputation + ")"));
            box.Line(FlareText.Fill("reallife_flare_me_week", "list", FlareText.History(file.History)));
            box.Line(FlareText.Fill("reallife_flare_me_totals", "posts", FlareText.N(file.TotalPosts), "likes", FlareText.N(file.TotalLikes),
                "friends", FlareRules.Count(file.Following).ToString()));
            box.Line(FlareText.Fill("reallife_flare_me_today", "count", (file.PostsDay == day ? file.PostsToday : 0).ToString(), "most", FlareRules.PostsPerDay.ToString()));
            return FlareTables.Mine;
        }

        // ---------------------------------------------------------------- Business

        private static readonly string[] ReviewGood = { "reallife_flare_review_service_good", "reallife_flare_review_pricing_good", "reallife_flare_review_clean_good", "reallife_flare_review_facility_good" };
        private static readonly string[] ReviewBad = { "reallife_flare_review_service_bad", "reallife_flare_review_pricing_bad", "reallife_flare_review_clean_bad", "reallife_flare_review_facility_bad" };
        private const int ReviewGoodFrom = 70, ReviewBadUnder = 40;   // the game's satisfaction figures, 0 to 100: a part this good or this poor is talked about
        private readonly int[] _parts = new int[4];

        // The game's overall satisfaction as stars, in halves: "4.5".
        private static string Stars(int overall)
        {
            int halves = FlareRules.Clamp((overall + 5) / 10, 0, 10);
            return FlareText.Fill("reallife_flare_stars", "stars", (halves / 2).ToString() + (halves % 2 == 1 ? ".5" : string.Empty));
        }

        private TableSpec WriteBusiness(InfoBox box, FlareFile file, int day, ref string selected)
        {
            List<BuildingRegistration> shops = FlareGame.Businesses();
            string pickedPlace = null, pickedName = null;
            int pickedPosts = 0;
            BuildingRegistration pickedReg = null;
            for (int i = 0; i < shops.Count; i++)
            {
                string place = FlareGame.Place(shops[i]), name = FlareGame.Name(shops[i]);
                int overall;
                bool rated = FlareGame.Satisfaction(shops[i], out overall, _parts);
                int talk = file.BuzzAt(place, day);
                int posts = 0, last = -1;
                for (int k = 0; k < file.Posts.Count; k++)
                {
                    FlarePost p = file.Posts[k];
                    if (p.Author == FlareAuthor.Player && p.Place == place)
                    {
                        posts++;
                        last = p.Day;
                    }
                }
                TableRow row = Row("shop:" + place);
                row.Set(0, name);
                row.Set(1, rated ? Stars(overall) : string.Empty, rated ? overall : double.NaN);
                row.Set(2, talk != 0 ? FlareText.Signed(talk) + "%" : string.Empty, talk, talk > 0 ? TableRow.Green : talk < 0 ? TableRow.Red : TableRow.Plain);
                row.Set(3, posts.ToString(), posts);
                row.Set(4, last >= 0 ? FlareText.Fill("reallife_flare_day", "day", last.ToString()) : FlareText.T("reallife_flare_never"), last);
                if (Is(row, ref selected))
                {
                    pickedPlace = place;
                    pickedName = name;
                    pickedPosts = posts;
                    pickedReg = shops[i];
                }
            }
            FlareHire hire = null;
            HireRows(file, day, ref selected, ref hire);
            if (pickedPlace != null)
            {
                if (_hireOpen)
                {
                    WriteHire(box, file, pickedPlace, pickedName, day);
                }
                else
                {
                    WriteShop(box, file, day, pickedReg, pickedPlace, pickedName, pickedPosts);
                }
                return FlareTables.Business;
            }
            if (hire != null)
            {
                WriteHireRow(box, file, hire, day);
                return FlareTables.Business;
            }
            box.Title(FlareText.T("reallife_flare_tab_business"));
            box.Line(FlareText.T(shops.Count > 0 ? "reallife_flare_business_intro" : "reallife_flare_business_none"));
            box.Line(FlareText.Fill("reallife_flare_business_talk", "most", FlareRules.BuzzMost.ToString(), "days", FlareRules.BuzzDays.ToString()));
            return FlareTables.Business;
        }

        // A business picked: its stars and what its reviews say (the game's own satisfaction, read only), the talk on Flare, and a sponsored post.
        private void WriteShop(InfoBox box, FlareFile file, int day, BuildingRegistration reg, string place, string name, int posts)
        {
            box.Title(name);
            int overall;
            if (FlareGame.Satisfaction(reg, out overall, _parts))
            {
                box.Line(FlareText.Pair("reallife_flare_shop_stars", Stars(overall)));
                int said = 0;
                for (int k = 0; k < _parts.Length && said < 2; k++)
                {
                    string key = _parts[k] >= ReviewGoodFrom ? ReviewGood[k] : _parts[k] < ReviewBadUnder ? ReviewBad[k] : null;
                    if (key != null)
                    {
                        box.Line(FlareText.Fill("reallife_flare_review_line", "words", FlareText.T(key)));
                        said++;
                    }
                }
            }
            int talk = file.BuzzAt(place, day);
            box.Line(talk != 0 ? FlareText.Fill("reallife_flare_shop_talk", "percent", FlareText.Signed(talk)) : FlareText.T("reallife_flare_shop_quiet"));
            box.Line(FlareText.Pair("reallife_flare_shop_posts", posts.ToString()));
            int reach = FlareRules.SponsoredReach(file.Followers);
            box.Line(reach > 0 ? FlareText.Fill("reallife_flare_shop_sponsored", "share", (reach / 10).ToString())
                : FlareText.Fill("reallife_flare_shop_sponsored_later", "followers", FlareText.N(FlareRules.SponsoredFrom)));
            Transform buttons = box.ButtonRow();
            InfoBox.Button(buttons, InfoBox.Positive, "reallife_flare_shop_hire", () =>
            {
                _hireOpen = true;
                Refresh();
            });
            InfoBox.Button(buttons, InfoBox.Additional, "reallife_flare_back", Back);
        }
    }
}
