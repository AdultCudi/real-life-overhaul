using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, its sheets and its sending: the compose sheet (the words, a photo of the bank, a business tagged,
    /// a promotion priced and forecast by the boost rules, a poll), the hire sheet (a business, the deal, the company's
    /// quote and what it should bring), the reply sheet (an answer to a review and what it does to the rating); a message,
    /// a reply under a post and the short answers of a conversation. Everything done through the director, as the old
    /// panel does it: money through the game's own paths (FlareCityGame.PayAd, the company's campaign booking).
    /// </summary>
    public sealed partial class FlareScreen
    {
        private const int PhotosMost = 5, ShopsMost = 6;
        private const int GroupPhoto = 0, GroupPlace = 1, GroupPromote = 2, GroupDays = 3, GroupPoll = 4, GroupBusiness = 5, GroupDeal = 6;
        private static readonly int[] PromoteDays = { 1, 3, 7 };
        private static readonly FlareDeal[] HireDeals = { FlareDeal.Post, FlareDeal.Series, FlareDeal.Ambassador, FlareDeal.Launch, FlareDeal.Giveaway };

        private readonly FlareCompose _compose = new FlareCompose();
        private readonly List<string> _photos = new List<string>(PhotosMost);
        private readonly List<int> _groups = new List<int>(6);
        private readonly List<string> _quickKeys = new List<string>(4);

        // The compose sheet's choices.
        private int _photo = -1, _place = -1, _promote, _promoteDays;
        private bool _poll;
        private string _composeTag, _sheetWhy;

        // The hire sheet's.
        private int _hireWho = -1, _hireShop, _hireDeal;

        // The reply sheet's.
        private RowRef _replyTo;
        private int _option = -1;

        // ------------------------------------------------------------------------------------------------ opening

        /// <summary>The compose sheet, fresh: a chip of Home's composer starts it with that choice made.</summary>
        private bool OpenCompose(int chip, string tag = null)
        {
            if (_flare == null || !_flare.Running)
            {
                return false;
            }
            _compose.Reset();
            _photo = _place = -1;
            _promote = _promoteDays = 0;
            _poll = false;
            _composeTag = tag;
            _sheetWhy = null;
            List<FlareShop> shops = _flare.MyShops();
            switch (chip)
            {
                case 1:
                    _poll = true;
                    break;
                case 2:
                    _place = shops.Count > 0 ? 0 : -1;
                    _promote = shops.Count > 0 ? 1 : 0;
                    break;
                case 3:
                    _place = shops.Count > 0 ? 0 : -1;
                    break;
            }
            Photos(shops);
            if (chip == 0)
            {
                _photo = _photos.Count > 0 ? 0 : -1;
            }
            _sheet = FlareFace.ComposeSheet;
            _field = FlareFace.ComposeField;
            _dirty = true;
            return true;
        }

        // The photos a post can carry, for the business tagged (its front first) or where the player is.
        private void Photos(List<FlareShop> shops)
        {
            FlareShop? shop = _place >= 0 && _place < shops.Count ? shops[_place] : (FlareShop?)null;
            _flare.BankPhotos(shop?.Place, shop?.District, _photos, PhotosMost);
            if (_photo >= _photos.Count)
            {
                _photo = _photos.Count > 0 ? 0 : -1;
            }
        }

        private bool OpenHire(int influencer)
        {
            FlareFile file = _flare != null && _flare.Running ? _flare.File : null;
            if (file == null || file.Influencer(influencer) == null || _flare.MyShops().Count == 0)
            {
                return false;
            }
            _hireWho = influencer;
            _hireShop = Math.Max(0, Math.Min(_shop, _flare.MyShops().Count - 1));
            _hireDeal = 0;
            _sheetWhy = null;
            _sheet = FlareFace.HireSheet;
            _dirty = true;
            return true;
        }

        private bool OpenReply(RowRef r)
        {
            if ((r.Act == RowAct.Review && r.Post == null) || (r.Act == RowAct.Note && r.Note == null))
            {
                return false;
            }
            if ((r.Act == RowAct.Note && !string.IsNullOrEmpty(r.Note.Answer)) || (r.Act == RowAct.Review && _flare.Answered(r.Post.Id)))
            {
                return false;   // answered once: the row shows the answer
            }
            _replyTo = r;
            _option = -1;
            _sheetWhy = null;
            _sheet = FlareFace.ReplySheet;
            _dirty = true;
            return true;
        }

        private bool CloseSheet()
        {
            if (_sheet == FlareFace.NoSheet)
            {
                return false;
            }
            _sheet = FlareFace.NoSheet;
            _field = -1;
            _replyTo = default(RowRef);
            _dirty = true;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the sheet's words

        private void FillSheet(FlareFile file, long now)
        {
            FlareSheetModel k = _m.Sheet0;
            k.Title = k.Line = k.Words = k.FieldText = k.FieldHint = k.FieldCount = k.Picture = k.Note = k.Confirm = null;
            k.Stars = 0;
            k.Field = k.NoteBad = k.ConfirmOff = false;
            k.Groups = None;
            k.Chips = new string[0][];
            k.Chosen = new int[0];
            k.Figures = k.FigureLabels = k.Options = k.OptionLines = None;
            k.Option = -1;
            k.Cancel = FlareText.T("reallife_flare_kit_cancel");
            _groups.Clear();
            switch (_sheet)
            {
                case FlareFace.ComposeSheet: ComposeSheet(k); break;
                case FlareFace.HireSheet: HireSheet(file, now, k); break;
                case FlareFace.ReplySheet: ReplySheet(file, k); break;
            }
        }

        private void ComposeSheet(FlareSheetModel k)
        {
            List<FlareShop> shops = _flare.MyShops();
            if (_place >= shops.Count)
            {
                _place = -1;
            }
            Apply(shops);
            string first = _flare.MyName;
            int space = first.IndexOf(' ');
            k.Title = FlareText.T("reallife_flare_kit_compose_title");
            k.Line = FlareText.Fill("reallife_flare_kit_compose_as", "name", _flare.MyName, "handle", _flare.MyHandle);
            k.Field = true;
            k.FieldText = _compose.Text;
            k.FieldHint = FlareText.Fill("reallife_flare_kit_composer", "name", space > 0 ? first.Substring(0, space) : first);
            k.FieldCount = FlareText.Fill("reallife_flare_kit_count", "count", (_compose.Text ?? string.Empty).Length.ToString(CultureInfo.InvariantCulture), "most",
                FlareLiveRules.TextMost.ToString(CultureInfo.InvariantCulture));
            k.Picture = _compose.Pic;
            var groups = new List<string>(5);
            var chips = new List<string[]>(5);
            var chosen = new List<int>(5);
            string[] photos = new string[_photos.Count + 1];
            photos[0] = FlareText.T("reallife_flare_kit_no_photo");
            for (int i = 0; i < _photos.Count; i++)
            {
                photos[i + 1] = FlareText.Fill(_photos[i].IndexOf(System.IO.Path.DirectorySeparatorChar) < 0 && _photos[i].IndexOf('/') < 0 ? "reallife_flare_kit_photo_mine"
                    : "reallife_flare_kit_photo_n", "n", (i + 1).ToString(CultureInfo.InvariantCulture));
            }
            Group(groups, chips, chosen, GroupPhoto, FlareText.T("reallife_flare_kit_chip_photo"), photos, _photo + 1);
            if (shops.Count > 0)
            {
                string[] places = new string[Math.Min(shops.Count, ShopsMost) + 1];
                places[0] = FlareText.T("reallife_flare_kit_no_place");
                for (int i = 1; i < places.Length; i++)
                {
                    places[i] = shops[i - 1].Name ?? "?";
                }
                Group(groups, chips, chosen, GroupPlace, FlareText.T("reallife_flare_kit_chip_place"), places, _place + 1);
            }
            if (_place >= 0)
            {
                string[] steps = new string[FlareUseRules.ReachSteps.Length];
                steps[0] = FlareText.T("reallife_flare_kit_free");
                for (int i = 1; i < steps.Length; i++)
                {
                    steps[i] = FlareText.Fill("reallife_flare_kit_reach_step", "count", FlareFeedRules.Count(FlareUseRules.ReachSteps[i]));
                }
                Group(groups, chips, chosen, GroupPromote, FlareText.T("reallife_flare_kit_chip_promote"), steps, _promote);
                if (_promote > 0)
                {
                    string[] days = new string[PromoteDays.Length];
                    for (int i = 0; i < days.Length; i++)
                    {
                        days[i] = PromoteDays[i] == 1 ? FlareText.T("reallife_flare_kit_day_one") : FlareText.Fill("reallife_flare_kit_days_n", "count", PromoteDays[i].ToString(CultureInfo.InvariantCulture));
                    }
                    Group(groups, chips, chosen, GroupDays, FlareText.T("reallife_flare_kit_for_how_long"), days, _promoteDays);
                }
            }
            Group(groups, chips, chosen, GroupPoll, FlareText.T("reallife_flare_kit_chip_poll"), new[] { FlareText.T("reallife_flare_kit_no_poll"), FlareText.T("reallife_flare_kit_poll_yes_no") },
                _poll ? 1 : 0);
            k.Groups = groups.ToArray();
            k.Chips = chips.ToArray();
            k.Chosen = chosen.ToArray();
            int cost = 0;
            if (_compose.Place != null)
            {
                FlareExpect ex = _flare.Preview(_compose);
                cost = ex.Cost;
                k.FigureLabels = new[]
                {
                    FlareText.T("reallife_flare_kit_fig_reach"), FlareText.T("reallife_flare_kit_fig_likely"), FlareText.T("reallife_flare_kit_fig_takings"),
                    FlareText.T("reallife_flare_kit_fig_cost")
                };
                k.Figures = new[]
                {
                    FlareFeedRules.Count(ex.Reach), FlareText.N(ex.Visitors), FlareText.Money(ex.Cents / 100), ex.Cost > 0 ? FlareText.Money(ex.Cost / 100) : FlareText.T("reallife_flare_kit_free")
                };
                k.Note = FlareText.Fill("reallife_flare_kit_forecast", "hours", ex.Hours.ToString(CultureInfo.InvariantCulture), "flop", (ex.Flop / 10).ToString(CultureInfo.InvariantCulture),
                    "viral", (ex.Viral / 10).ToString(CultureInfo.InvariantCulture));
            }
            if (_sheetWhy != null)
            {
                k.Note = FlareText.T(_sheetWhy);
                k.NoteBad = true;
            }
            k.Confirm = cost > 0 ? FlareText.Fill("reallife_flare_kit_post_pay", "money", FlareText.Money(cost / 100)) : FlareText.T("reallife_flare_post");
            k.ConfirmOff = string.IsNullOrWhiteSpace(_compose.Text) && _compose.Pic == null;
        }

        private void Group(List<string> groups, List<string[]> chips, List<int> chosen, int kind, string label, string[] words, int on)
        {
            _groups.Add(kind);
            groups.Add(label);
            chips.Add(words);
            chosen.Add(on);
        }

        // The sheet's choices onto the compose the director posts.
        private void Apply(List<FlareShop> shops)
        {
            _compose.Kind = FlareSource.Life;
            _compose.Pic = _photo >= 0 && _photo < _photos.Count ? _photos[_photo] : null;
            if (_place >= 0 && _place < shops.Count)
            {
                _compose.Place = shops[_place].Place;
                _compose.Name = shops[_place].Name;
                _compose.District = shops[_place].District;
            }
            else
            {
                _compose.Place = _compose.Name = _compose.District = null;
            }
            _compose.PromoteReach = _compose.Place != null ? FlareUseRules.PromoteOf(_promote) : 0;
            _compose.PromoteDays = PromoteDays[Math.Max(0, Math.Min(_promoteDays, PromoteDays.Length - 1))];
            _compose.Poll = _poll;
            _compose.Tags = _poll ? (string.IsNullOrEmpty(_composeTag) ? PollTag : _composeTag + " " + PollTag) : _composeTag;
        }

        private void HireSheet(FlareFile file, long now, FlareSheetModel k)
        {
            FlareInfluencer inf = file.Influencer(_hireWho);
            List<FlareShop> shops = _flare.MyShops();
            if (inf == null || shops.Count == 0)
            {
                _sheet = FlareFace.NoSheet;
                _m.Sheet = FlareFace.NoSheet;
                return;
            }
            _hireShop = Math.Max(0, Math.Min(_hireShop, shops.Count - 1));
            _hireDeal = Math.Max(0, Math.Min(_hireDeal, HireDeals.Length - 1));
            FlareShop shop = shops[_hireShop];
            FlareDeal deal = HireDeals[_hireDeal];
            k.Title = FlareText.Fill("reallife_flare_kit_hire_sheet", "handle", inf.Handle);
            k.Line = FlareText.Fill("reallife_flare_kit_hire_sheet_line", "niche", FlareText.Niche(inf.Niche), "followers", FlareFeedRules.Count(inf.Followers),
                "engagement", (inf.Engagement / 100f).ToString("0.0", CultureInfo.InvariantCulture));
            string[] names = new string[Math.Min(shops.Count, ShopsMost)];
            for (int i = 0; i < names.Length; i++)
            {
                names[i] = shops[i].Name ?? "?";
            }
            string[] deals = new string[HireDeals.Length];
            for (int i = 0; i < deals.Length; i++)
            {
                deals[i] = FlareText.Deal((int)HireDeals[i]);
            }
            var groups = new List<string>(2);
            var chips = new List<string[]>(2);
            var chosen = new List<int>(2);
            Group(groups, chips, chosen, GroupBusiness, FlareText.T("reallife_flare_hire_business"), names, _hireShop);
            Group(groups, chips, chosen, GroupDeal, FlareText.T("reallife_flare_hire_deal"), deals, _hireDeal);
            k.Groups = groups.ToArray();
            k.Chips = chips.ToArray();
            k.Chosen = chosen.ToArray();
            k.Words = FlareText.T(FlareText.DealAboutKeys[(int)deal]);
            bool can = _flare.HireQuote(shop.Place, deal, inf.Id, out int cost, out int percent, out string why);
            FlareExpect ex = _flare.HireExpect(inf, shop.Place);
            int posts = FlareCast.PostsOf(deal);
            bool fits = shop.District != null && shop.District == inf.District;
            k.FigureLabels = new[]
            {
                FlareText.T("reallife_flare_kit_fig_cost"), FlareText.T("reallife_flare_kit_fig_customers"), FlareText.T("reallife_flare_kit_fig_likely"), FlareText.T("reallife_flare_kit_fig_audience")
            };
            k.Figures = new[]
            {
                can ? FlareText.Money(cost) : "-", can ? "+" + percent.ToString(CultureInfo.InvariantCulture) + "%" : "-", FlareText.N(ex.Visitors * posts),
                FlareText.District(inf.District)
            };
            if (!can)
            {
                k.Note = why == FlareDirector.BusyWhy && _flare.CampaignAt(shop.Place, out FlareCampaign busy) ? FlareText.Busy(busy, _flare.CurrentDay)
                    : FlareText.T(why ?? "reallife_flare_hire_no_one");
                k.NoteBad = true;
            }
            else
            {
                k.Note = FlareText.Fill(fits ? "reallife_flare_kit_hire_fits" : "reallife_flare_kit_hire_far", "posts", posts.ToString(CultureInfo.InvariantCulture),
                    "flop", (ex.Flop / 10).ToString(CultureInfo.InvariantCulture), "viral", (ex.Viral / 10).ToString(CultureInfo.InvariantCulture));
            }
            if (_sheetWhy != null)
            {
                k.Note = FlareText.T(_sheetWhy);
                k.NoteBad = true;
            }
            k.Confirm = can ? FlareText.Fill("reallife_flare_kit_hire_pay", "money", FlareText.Money(cost)) : FlareText.T("reallife_flare_kit_hire");
            k.ConfirmOff = !can;
        }

        private void ReplySheet(FlareFile file, FlareSheetModel k)
        {
            RowRef r = _replyTo;
            int stars;
            string name, words, business;
            FlareStarRow row = null;
            if (r.Act == RowAct.Note && r.Note != null)
            {
                stars = FlareRules.Clamp(r.Note.Stars, 1, 5);
                name = _flare.ReviewerName(r.Note.Person);
                FlareShop shop = ShopOfKey(r.Note.Key);
                business = shop.Name ?? "?";
                words = NoteWords(r.Note, business);
                row = shop.Place != null ? _flare.StarsFor(shop.Place) : null;
            }
            else if (r.Act == RowAct.Review && r.Post != null)
            {
                Author(r.Post, out name, out _, out _, out _);
                stars = FlareRules.Clamp(r.Post.Arg, 1, 5);
                business = r.Post.Name ?? "?";
                words = FlareFeedRules.Plain(r.Post.Text ?? _flare.WordsFor(r.Post, name));
            }
            else
            {
                _sheet = FlareFace.NoSheet;
                _m.Sheet = FlareFace.NoSheet;
                return;
            }
            k.Title = FlareText.Fill("reallife_flare_kit_reply_title", "name", name);
            k.Line = FlareText.Fill("reallife_flare_kit_reply_line", "business", business);
            k.Stars = stars;
            k.Words = words;
            k.Options = new string[FlareReplyRules.Options];
            k.OptionLines = new string[FlareReplyRules.Options];
            for (int i = 0; i < FlareReplyRules.Options; i++)
            {
                var option = (FlareReplyOption)i;
                int quality = FlareReplyRules.Quality(option, stars);
                k.Options[i] = FlareText.T("reallife_flare_kit_option_" + i.ToString(CultureInfo.InvariantCulture));
                k.OptionLines[i] = FlareReplyRules.Eases(quality, stars) ? FlareText.T("reallife_flare_kit_option_eases")
                    : FlareReplyRules.Worsens(quality) ? FlareText.T("reallife_flare_kit_option_worsens") : FlareText.T("reallife_flare_kit_option_stands");
            }
            k.Option = _option;
            if (row != null && row.Total > 0)
            {
                int before = FlareReplyRules.Average(row);
                int after = _option >= 0 ? After(row, stars, FlareReplyRules.Quality((FlareReplyOption)_option, stars)) : before;
                k.FigureLabels = new[] { FlareText.T("reallife_flare_kit_fig_rating_now"), FlareText.T("reallife_flare_kit_fig_rating_after") };
                k.Figures = new[] { (before / 1000f).ToString("0.00", CultureInfo.InvariantCulture), (after / 1000f).ToString("0.00", CultureInfo.InvariantCulture) };
            }
            if (_option >= 0)
            {
                k.Note = FlareText.T(FlareDirector.ReplyKeys[_option]);   // the words that go up under the review
            }
            if (_sheetWhy != null)
            {
                k.Note = FlareText.T(_sheetWhy);
                k.NoteBad = true;
            }
            k.Confirm = FlareText.T("reallife_flare_kit_reply");
            k.ConfirmOff = _option < 0;
        }

        // The rating a review's answer would leave: the row as it is, with this answer counted (nothing kept).
        private static int After(FlareStarRow row, int stars, int quality)
        {
            var copy = new FlareStarRow
            {
                Key = row.Key, One = row.One, Two = row.Two, Three = row.Three, Four = row.Four, Five = row.Five, Recent = row.Recent, Earlier = row.Earlier,
                Eased = row.Eased, EasedStars = row.EasedStars, Raised = row.Raised, RaisedStars = row.RaisedStars
            };
            FlareReplyRules.Answered(copy, stars, quality);
            return FlareReplyRules.Average(copy);
        }

        private FlareShop ShopOfKey(string key)
        {
            List<FlareShop> shops = _flare.MyShops();
            for (int i = 0; key != null && i < shops.Count; i++)
            {
                string place = shops[i].Place;
                if (place != null && (FlareCityPass.PlaceToKey(place) == key || place == key))
                {
                    return shops[i];
                }
            }
            return default(FlareShop);
        }

        // ------------------------------------------------------------------------------------------------ the sheet's presses

        private bool SheetChip(int group, int chip)
        {
            if (group < 0 || group >= _groups.Count || chip < 0)
            {
                return false;
            }
            _sheetWhy = null;
            List<FlareShop> shops = _flare.MyShops();
            switch (_groups[group])
            {
                case GroupPhoto:
                    _photo = chip - 1;
                    break;
                case GroupPlace:
                    _place = chip - 1;
                    if (_place < 0)
                    {
                        _promote = 0;
                    }
                    Photos(shops);
                    break;
                case GroupPromote:
                    _promote = Math.Min(chip, FlareUseRules.ReachSteps.Length - 1);
                    break;
                case GroupDays:
                    _promoteDays = Math.Min(chip, PromoteDays.Length - 1);
                    break;
                case GroupPoll:
                    _poll = chip == 1;
                    break;
                case GroupBusiness:
                    _hireShop = chip;
                    break;
                case GroupDeal:
                    _hireDeal = Math.Min(chip, HireDeals.Length - 1);
                    break;
                default:
                    return false;
            }
            _dirty = true;
            return true;
        }

        private bool SheetOption(int option)
        {
            if (_sheet != FlareFace.ReplySheet || option < 0 || option >= FlareReplyRules.Options)
            {
                return false;
            }
            _option = option;
            _sheetWhy = null;
            return true;
        }

        private bool SheetConfirm()
        {
            switch (_sheet)
            {
                case FlareFace.ComposeSheet: return PostNow();
                case FlareFace.HireSheet: return HireNow();
                case FlareFace.ReplySheet: return ReplyNow();
                default: return false;
            }
        }

        // The post goes up: in the feed, the profile and the studio; a promotion is paid at its business first and its boost starts.
        private bool PostNow()
        {
            List<FlareShop> shops = _flare.MyShops();
            Apply(shops);
            if (string.IsNullOrWhiteSpace(_compose.Text) && _compose.Pic == null)
            {
                return false;
            }
            if (!_flare.Compose(_compose, out string why))
            {
                _sheetWhy = why ?? "reallife_flare_kit_post_failed";
                _dirty = true;
                return true;   // the sheet stays open and says why
            }
            CloseSheet();
            _tab = 0;
            Nav(FlareFace.Home);   // the new post at the top of For you
            return true;
        }

        // The influencer is booked and paid as the company's campaign; its lead writes, and the studio shows it running.
        private bool HireNow()
        {
            FlareFile file = _flare.File;
            FlareInfluencer inf = file != null ? file.Influencer(_hireWho) : null;
            List<FlareShop> shops = _flare.MyShops();
            if (inf == null || shops.Count == 0)
            {
                return false;
            }
            FlareShop shop = shops[Math.Max(0, Math.Min(_hireShop, shops.Count - 1))];
            if (!_flare.Hire(shop.Place, HireDeals[Math.Max(0, Math.Min(_hireDeal, HireDeals.Length - 1))], inf.Id))
            {
                _sheetWhy = "reallife_flare_kit_hire_failed";
                _dirty = true;
                return true;
            }
            CloseSheet();
            Nav(FlareFace.Studio);
            return true;
        }

        private bool ReplyNow()
        {
            if (_option < 0)
            {
                return false;
            }
            var option = (FlareReplyOption)_option;
            string key = FlareDirector.ReplyKeys[_option];
            bool done = _replyTo.Act == RowAct.Note ? _flare.ReplyToNote(_replyTo.Note, option, key)   // kept as its key: shown in the reader's language
                : _replyTo.Post != null && _flare.ReplyToReview(_replyTo.Post.Id, option, FlareText.T(key));
            if (!done)
            {
                _sheetWhy = "reallife_flare_kit_reply_failed";
                _dirty = true;
                return true;
            }
            CloseSheet();
            return true;
        }

        // ------------------------------------------------------------------------------------------------ messages and replies

        // The short answers a conversation offers: an offer's two buttons, an answer to a rival, or a few lines for the account.
        private void QuickFor(FlareFile file, string with)
        {
            _quick.Clear();
            _quickKeys.Clear();
            if (with.StartsWith("d:", StringComparison.Ordinal))
            {
                FlareOffer o = _flare.OfferOf(FlareLiveRules.OfferOf(with));
                if (o != null && o.Open(_flare.CurrentDay))
                {
                    QuickAdd("accept", FlareText.Fill("reallife_flare_kit_accept", "money", FlareText.Money((int)(o.Fee * o.Posts / 100))));
                    QuickAdd("decline", FlareText.T("reallife_flare_kit_decline"));
                }
                return;
            }
            if (with.StartsWith("k:", StringComparison.Ordinal) && Attacked(file, with))
            {
                QuickAdd("reallife_flare_kit_rival_calm", FlareText.T("reallife_flare_kit_quick_calm"));
                QuickAdd("reallife_flare_kit_rival_back", FlareText.T("reallife_flare_kit_quick_back"));
                return;
            }
            string kind = with.StartsWith("i:", StringComparison.Ordinal) ? "inf" : with.Length > 1 && with[0] == 'c' && char.IsDigit(with[1]) ? "res"
                : with.StartsWith("k:", StringComparison.Ordinal) || with.StartsWith("b:", StringComparison.Ordinal) ? "biz" : "chat";
            for (int i = 0; i < 3; i++)
            {
                string key = "reallife_flare_kit_quick_" + kind + "_" + i.ToString(CultureInfo.InvariantCulture);
                QuickAdd(key, FlareText.T(key));
            }
        }

        private void QuickAdd(string key, string words)
        {
            _quickKeys.Add(key);
            _quick.Add(words);
        }

        private string[] QuickWords() => _quick.ToArray();

        // Whether a business of the city ran the player's down in this conversation, unanswered.
        private static bool Attacked(FlareFile file, string with)
        {
            bool attacked = false;
            for (int i = 0; i < file.Net.Dms.Count; i++)
            {
                FlareDm d = file.Net.Dms[i];
                if (d.With != with)
                {
                    continue;
                }
                attacked = d.Mine ? false : d.Key == "reallife_flare_dm_rival" || attacked;
            }
            return attacked;
        }

        private bool Quick(int i)
        {
            if (_with == null || i < 0 || i >= _quickKeys.Count)
            {
                return false;
            }
            string key = _quickKeys[i];
            switch (key)
            {
                case "accept": return _flare.TakeOffer(FlareLiveRules.OfferOf(_with));
                case "decline": return _flare.DeclineOffer(FlareLiveRules.OfferOf(_with));
                case "reallife_flare_kit_rival_calm":
                case "reallife_flare_kit_rival_back":
                    // The answer goes up for the city to see (a careful one eases the talk), and into the conversation.
                    List<FlareShop> shops = _flare.MyShops();
                    string words = FlareText.T(key);
                    bool posted = _flare.AnswerRival(shops.Count > 0 ? shops[0].Place : null, words);
                    return _flare.SendDm(_with, words) || posted;
                default:
                    return _flare.SendDm(_with, FlareText.T(key));
            }
        }

        // The field's words sent: a message in a conversation, or a reply under the thread's post.
        private bool Send()
        {
            if (_flare == null || !_flare.Running)
            {
                return false;
            }
            if (_page == FlareFace.Thread)
            {
                string text = _reply.Trim();
                if (text.Length == 0 || _post < 0)
                {
                    return false;
                }
                var c = new FlareCompose { Kind = FlareSource.Reply, ReplyTo = _post, Text = text };
                if (!_flare.Compose(c, out _))
                {
                    return false;
                }
                _reply = string.Empty;
                return true;
            }
            if (_page == FlareFace.Messages && _with != null)
            {
                string text = _message.Trim();
                if (text.Length == 0 || !_flare.SendDm(_with, text))
                {
                    return false;
                }
                _message = string.Empty;
                _m.Detail.Footed = false;   // the conversation opens at its newest again
                return true;
            }
            return false;
        }
    }
}
