using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, what the compose sheet writes besides a moment of now: a friend's moment from the phone's drafts, a report to the city, a
    /// review of a business, an announcement of the player's own, the tone and a mention. Each is the same compose the director posts
    /// (FlareDirector.Compose: Built, Report), the sheet's chips only filling it. The sheet's groups are laid out by FlareFace.Sheet with their
    /// label beside them, so they fit the window. Cost: the chips are written when the sheet changes.
    /// </summary>
    public sealed partial class FlareScreen
    {
        private const int KindLife = 0, KindMoment = 1, KindReport = 2, KindReview = 3, KindAnnounce = 4;
        private const int GroupKind = 7, GroupMoment = 8, GroupTag = 9, GroupReport = 10, GroupReviewShop = 11, GroupStars = 12, GroupAnnounceKind = 13, GroupWhen = 14,
            GroupTone = 15, GroupMention = 16, GroupAnnouncePlace = 17, GroupAdTarget = 18, GroupAdSize = 19, GroupAdDays = 20, GroupBanner = 21;
        private const int MomentsMost = 4, ReviewShopsMost = 5, MentionMost = 5;

        private readonly List<int> _kindChips = new List<int>(5);
        private readonly List<int> _momentChips = new List<int>(MomentsMost);
        private readonly List<string> _reviewPlaces = new List<string>(ReviewShopsMost + 1);
        private readonly List<string> _reviewNames = new List<string>(ReviewShopsMost + 1);
        private readonly List<int> _whenChips = new List<int>(4);
        private readonly List<string> _mentionKeys = new List<string>(MentionMost);

        // What the kinds of post need chosen.
        private int _kind, _draftId = -1, _reportArg, _stars, _announceArg, _when = -1, _tone = -1;
        private bool _tagFriend = true;
        private string _reviewPlace, _reviewName, _mention, _standPlace, _standName;

        private void ResetKinds()
        {
            _kind = KindLife;
            _draftId = -1;
            _tagFriend = true;
            _reportArg = _stars = _announceArg = 0;
            _when = -1;
            _tone = -1;
            _reviewPlace = _reviewName = _mention = null;
        }

        private void ComposeSheet(FlareSheetModel k)
        {
            FlareFile file = _flare.File;
            List<FlareShop> shops = _flare.MyShops();
            if (_place >= shops.Count)
            {
                _place = -1;
            }
            if (_kind == KindAnnounce && _place < 0 && shops.Count > 0)
            {
                _place = 0;
            }
            Apply(shops);
            string first = _flare.MyName;
            int space = first.IndexOf(' ');
            k.Title = FlareText.T("reallife_flare_kit_compose_title");
            k.Line = FlareText.Fill("reallife_flare_kit_compose_as", "name", _flare.MyName, "handle", _flare.MyHandle);
            k.Field = true;
            k.FieldArg = FlareFace.ComposeField;
            k.FieldText = _compose.Text;
            k.FieldHint = FlareText.Fill("reallife_flare_kit_composer", "name", space > 0 ? first.Substring(0, space) : first);
            k.FieldCount = FlareText.Fill("reallife_flare_kit_count", "count", (_compose.Text ?? string.Empty).Length.ToString(CultureInfo.InvariantCulture), "most",
                FlareLiveRules.TextMost.ToString(CultureInfo.InvariantCulture));
            k.Picture = _compose.Pic;
            var groups = new List<string>(8);
            var chips = new List<string[]>(8);
            var chosen = new List<int>(8);
            // What it is about.
            _kindChips.Clear();
            var about = new List<string>(5);
            _kindChips.Add(KindLife);
            about.Add(FlareText.T("reallife_flare_kind_now"));
            if (file.Drafts.Count > 0)
            {
                _kindChips.Add(KindMoment);
                about.Add(FlareText.T("reallife_flare_kind_moment"));
            }
            _kindChips.Add(KindReport);
            about.Add(FlareText.T("reallife_flare_kind_report"));
            _kindChips.Add(KindReview);
            about.Add(FlareText.T("reallife_flare_kind_review"));
            if (shops.Count > 0)
            {
                _kindChips.Add(KindAnnounce);
                about.Add(FlareText.T("reallife_flare_kind_announce"));
            }
            int aboutAt = _kindChips.IndexOf(_kind);
            if (aboutAt < 0)
            {
                _kind = KindLife;
                aboutAt = 0;
            }
            Group(groups, chips, chosen, GroupKind, FlareText.T("reallife_flare_kit_group_about"), about.ToArray(), aboutAt);
            switch (_kind)
            {
                case KindMoment: MomentGroups(file, groups, chips, chosen); break;
                case KindReport: ReportGroups(groups, chips, chosen); break;
                case KindReview: ReviewGroups(groups, chips, chosen); break;
                default: PostGroups(shops, groups, chips, chosen); break;
            }
            if (_kind != KindReport)
            {
                var tones = new string[FlareText.ToneKeys.Length];
                for (int t = 0; t < tones.Length; t++)
                {
                    tones[t] = FlareText.Tone(t);
                }
                FlareSource source = _kind == KindMoment && file.Draft(_draftId) != null ? file.Draft(_draftId).Source : _compose.Kind;
                Group(groups, chips, chosen, GroupTone, FlareText.T("reallife_flare_kit_group_tone"), tones, _tone >= 0 ? _tone : (int)FlareRules.BestTone(source));
            }
            MentionGroup(groups, chips, chosen);
            k.Groups = groups.ToArray();
            k.Chips = chips.ToArray();
            k.Chosen = chosen.ToArray();
            int cost = 0;
            if (_compose.Place != null && (_kind == KindLife || _kind == KindAnnounce))
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
                k.Note = FlareFigures.Bind(FlareText.Fill("reallife_flare_kit_forecast", "hours", ex.Hours.ToString(CultureInfo.InvariantCulture), "flop", FlareFigures.Odds(ex.Flop),
                    "viral", FlareFigures.Odds(ex.Viral)));
                if (_promote > 0 && _flare.SharedAdWith(_compose.Place, out string other))
                {
                    k.Note += " " + FlareText.Fill("reallife_flare_kit_reach_shared", "business", other);   // the pools are shared: the rival's ad takes part of this reach
                }
            }
            string why = ComposeNeeds(file);
            if (why != null && _kind != KindLife)
            {
                k.Note = FlareText.T(why);
                k.NoteBad = true;
            }
            if (_sheetWhy != null)
            {
                k.Note = FlareText.T(_sheetWhy);
                k.NoteBad = true;
            }
            k.Confirm = cost > 0 ? FlareText.Fill("reallife_flare_kit_post_pay", "money", FlareText.Money(cost / 100)) : FlareText.T("reallife_flare_post");
            k.ConfirmOff = _kind == KindLife ? string.IsNullOrWhiteSpace(_compose.Text) && _compose.Pic == null : why != null;
        }

        // The photo, the business tagged, and the reach bought, for a moment of now and for an announcement; the poll only for a moment of now.
        private void PostGroups(List<FlareShop> shops, List<string> groups, List<string[]> chips, List<int> chosen)
        {
            string[] photos = new string[_photos.Count + 2];
            photos[0] = FlareText.T("reallife_flare_kit_no_photo");
            for (int i = 0; i < _photos.Count; i++)
            {
                photos[i + 1] = FlareText.Fill(_photos[i].IndexOf(System.IO.Path.DirectorySeparatorChar) < 0 && _photos[i].IndexOf('/') < 0 ? "reallife_flare_kit_photo_mine"
                    : "reallife_flare_kit_photo_n", "n", (i + 1).ToString(CultureInfo.InvariantCulture));
            }
            photos[_photos.Count + 1] = FlareText.T("reallife_flare_kit_photo_gallery");   // the whole Gallery, in the picker
            Group(groups, chips, chosen, GroupPhoto, FlareText.T("reallife_flare_kit_chip_photo"), photos, _photo == GalleryPhoto ? _photos.Count + 1 : _photo + 1);
            if (shops.Count > 0)
            {
                bool announce = _kind == KindAnnounce;
                string[] places = new string[Math.Min(shops.Count, ShopsMost) + (announce ? 0 : 1)];
                if (!announce)
                {
                    places[0] = FlareText.T("reallife_flare_kit_no_place");
                }
                for (int i = announce ? 0 : 1; i < places.Length; i++)
                {
                    places[i] = shops[announce ? i : i - 1].Name ?? "?";
                }
                Group(groups, chips, chosen, announce ? GroupAnnouncePlace : GroupPlace, FlareText.T("reallife_flare_kit_chip_place"), places, announce ? _place : _place + 1);
            }
            if (_kind == KindAnnounce)
            {
                var kinds = new string[FlareText.AnnounceKeys.Length];
                for (int k = 0; k < kinds.Length; k++)
                {
                    kinds[k] = FlareText.T(FlareText.AnnounceKeys[k]);
                }
                Group(groups, chips, chosen, GroupAnnounceKind, FlareText.T("reallife_flare_kit_group_announce"), kinds, _announceArg);
                if (_announceArg == (int)FlareAnnounce.Event)
                {
                    int day = _flare.CurrentDay, nowHour = day * 24 + _flare.HourNow;
                    int[] hours = { day * 24 + 19, (day + 1) * 24 + 12, (day + 1) * 24 + 19, (day + 2) * 24 + 19 };
                    _whenChips.Clear();
                    var labels = new List<string>(4);
                    for (int i = 0; i < hours.Length; i++)
                    {
                        if (hours[i] > nowHour)
                        {
                            _whenChips.Add(hours[i]);
                            labels.Add(FlareText.Hour(hours[i]));
                        }
                    }
                    int at = _whenChips.IndexOf(_when);
                    if (at < 0 && _whenChips.Count > 0)
                    {
                        _when = _whenChips[0];
                        at = 0;
                    }
                    Group(groups, chips, chosen, GroupWhen, FlareText.T("reallife_flare_kit_group_when"), labels.ToArray(), at);
                }
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
                        days[i] = PromoteDays[i] == 1
                            ? FlareText.T("reallife_flare_kit_day_one")
                            : FlareText.Fill("reallife_flare_kit_days_n", "count", PromoteDays[i].ToString(CultureInfo.InvariantCulture));
                    }
                    Group(groups, chips, chosen, GroupDays, FlareText.T("reallife_flare_kit_for_how_long"), days, _promoteDays);
                }
            }
            if (_kind == KindLife)
            {
                Group(groups, chips, chosen, GroupPoll, FlareText.T("reallife_flare_kit_chip_poll"), new[] { FlareText.T("reallife_flare_kit_no_poll"), FlareText.T("reallife_flare_kit_poll_yes_no") },
                    _poll ? 1 : 0);
            }
        }

        // A friend's moment: the drafts the phone's life made (the newest first), and the friend in it tagged or not.
        private void MomentGroups(FlareFile file, List<string> groups, List<string[]> chips, List<int> chosen)
        {
            _momentChips.Clear();
            var labels = new List<string>(MomentsMost);
            for (int i = file.Drafts.Count - 1; i >= 0 && _momentChips.Count < MomentsMost; i--)
            {
                FlareDraft d = file.Drafts[i];
                _momentChips.Add(d.Id);
                labels.Add(FlareText.Draft(d, _flare.PersonName(d.Person)));
            }
            int at = _momentChips.IndexOf(_draftId);
            if (at < 0 && _momentChips.Count > 0)
            {
                _draftId = _momentChips[0];
                at = 0;
            }
            Group(groups, chips, chosen, GroupMoment, FlareText.T("reallife_flare_kit_group_moment"), labels.ToArray(), at);
            FlareDraft pick = file.Draft(_draftId);
            string friend = pick != null && pick.Person >= 0 ? _flare.PersonName(pick.Person) : null;
            if (friend != null)
            {
                Group(groups, chips, chosen, GroupTag, FlareText.T("reallife_flare_kit_group_friend"),
                    new[] { FlareText.T("reallife_flare_kit_no_tag"), FlareText.Fill("reallife_flare_tag_yes", "friend", friend) }, _tagFriend ? 1 : 0);
            }
        }

        // A report to the city: what is wrong; the district is where the player stands, and a business reported is the one they stand in.
        private void ReportGroups(List<string> groups, List<string[]> chips, List<int> chosen)
        {
            var kinds = new string[FlareText.ReportKeys.Length];
            for (int k = 0; k < kinds.Length; k++)
            {
                kinds[k] = FlareText.T(FlareText.ReportKeys[k]);
            }
            Group(groups, chips, chosen, GroupReport, FlareText.T("reallife_flare_kit_group_report"), kinds, _reportArg);
        }

        // A review of a business of the city's: which one (the one the player stands in first) and how many stars.
        private void ReviewGroups(List<string> groups, List<string[]> chips, List<int> chosen)
        {
            _reviewPlaces.Clear();
            _reviewNames.Clear();
            bool here = _flare.StandingIn(out string place, out string name);
            if (here)
            {
                _reviewPlaces.Add(place);
                _reviewNames.Add(name);
            }
            IReadOnlyList<FlareBiz> city = _flare.CityShops;
            for (int i = 0; i < city.Count && _reviewPlaces.Count < ReviewShopsMost; i++)
            {
                if (!_reviewPlaces.Contains(city[i].Place))
                {
                    _reviewPlaces.Add(city[i].Place);
                    _reviewNames.Add(city[i].Name);
                }
            }
            var shopLabels = _reviewNames.ToArray();
            if (here)
            {
                shopLabels[0] = FlareText.Fill("reallife_flare_compose_here", "name", name);
            }
            Group(groups, chips, chosen, GroupReviewShop, FlareText.T("reallife_flare_kit_group_shop"), shopLabels, _reviewPlaces.IndexOf(_reviewPlace));
            var stars = new string[5];
            for (int s = 0; s < stars.Length; s++)
            {
                stars[s] = FlareText.Fill("reallife_flare_stars", "stars", (s + 1).ToString(CultureInfo.InvariantCulture));
            }
            Group(groups, chips, chosen, GroupStars, FlareText.T("reallife_flare_kit_group_stars"), stars, _stars - 1);
        }

        // An account to call on, or none: the City Office and the other officials, one at most.
        private void MentionGroup(List<string> groups, List<string[]> chips, List<int> chosen)
        {
            _mentionKeys.Clear();
            var labels = new List<string>(MentionMost) { FlareText.T("reallife_flare_kit_no_mention") };
            _mentionKeys.Add(null);
            for (int i = 0; i <= (int)FlareOfficial.Bank && _mentionKeys.Count < MentionMost; i++)
            {
                _mentionKeys.Add("o:" + i.ToString(CultureInfo.InvariantCulture));
                labels.Add(FlareText.OfficialHandle(i));
            }
            Group(groups, chips, chosen, GroupMention, FlareText.T("reallife_flare_kit_group_mention"), labels.ToArray(), Math.Max(0, _mentionKeys.IndexOf(_mention)));
        }

        // What the chosen kind still needs, as a locale key; null when it can go.
        private string ComposeNeeds(FlareFile file)
        {
            switch (_kind)
            {
                case KindMoment:
                    return file.Draft(_draftId) == null ? "reallife_flare_kit_no_moments" : null;
                case KindReport:
                    if (_flare.PlayerDistrictNow() == null)
                    {
                        return "reallife_flare_compose_need_district";
                    }
                    return _reportArg == (int)FlareReport.Business && !_flare.StandingIn(out _, out _) ? "reallife_flare_compose_stand_in" : null;
                case KindReview:
                    return _reviewPlace == null || _stars < 1 ? "reallife_flare_compose_need_place" : null;
                case KindAnnounce:
                    return _compose.Place == null ? "reallife_flare_compose_need_mine" : null;
                default:
                    return null;
            }
        }

        // The sheet's choices onto the compose the director posts.
        private void Apply(List<FlareShop> shops)
        {
            FlareFile file = _flare.File;
            _compose.Kind = FlareSource.Life;
            _compose.Draft = -1;
            _compose.Arg = 0;
            _compose.When = -1;
            _compose.Tag = true;
            _compose.Pic = _photo == GalleryPhoto ? _chosenPhoto : _photo >= 0 && _photo < _photos.Count ? _photos[_photo] : null;
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
            bool paid = _kind == KindLife || _kind == KindAnnounce;
            switch (_kind)
            {
                case KindMoment:
                    FlareDraft d = file != null ? file.Draft(_draftId) : null;
                    _compose.Place = _compose.Name = _compose.District = null;
                    if (d != null)
                    {
                        _compose.Kind = d.Source;
                        _compose.Draft = d.Id;
                        _compose.Tag = _tagFriend;
                        _compose.Pic = (d.Source == FlareSource.Photo || d.Source == FlareSource.Selfie) && !string.IsNullOrEmpty(d.Name) ? "f:" + d.Name : _compose.Pic;
                    }
                    break;
                case KindReport:
                    _compose.Kind = FlareSource.Report;
                    _compose.Arg = _reportArg;
                    _compose.Place = _compose.Name = _compose.District = null;
                    if (_reportArg == (int)FlareReport.Business && _flare.StandingIn(out _standPlace, out _standName))
                    {
                        _compose.Place = _standPlace;
                        _compose.Name = _standName;
                    }
                    break;
                case KindReview:
                    _compose.Kind = FlareSource.Review;
                    _compose.Arg = _stars;
                    _compose.Place = _reviewPlace;
                    _compose.Name = _reviewName;
                    _compose.District = null;
                    break;
                case KindAnnounce:
                    _compose.Kind = FlareSource.Announce;
                    _compose.Arg = _announceArg;
                    _compose.When = _announceArg == (int)FlareAnnounce.Event ? _when : -1;
                    break;
            }
            _compose.PromoteReach = paid && _compose.Place != null ? FlareUseRules.PromoteOf(_promote) : 0;
            _compose.PromoteDays = PromoteDays[Math.Max(0, Math.Min(_promoteDays, PromoteDays.Length - 1))];
            _compose.Poll = _poll && _kind == KindLife;
            _compose.Tags = _compose.Poll ? (string.IsNullOrEmpty(_composeTag) ? FlareFeedRules.PollTag : _composeTag + " " + FlareFeedRules.PollTag) : _composeTag;
            _compose.ToneChosen = _tone >= 0;
            _compose.Tone = (FlareTone)Math.Max(0, _tone);
            _compose.Mention = _mention;
        }

        // A chip of the compose sheet by its group's kind.
        private bool ComposeChip(int kind, int chip)
        {
            switch (kind)
            {
                case GroupKind:
                    if (chip >= _kindChips.Count) { return false; }
                    _kind = _kindChips[chip];
                    _sheetWhy = null;
                    Photos(_flare.MyShops());
                    return true;
                case GroupMoment:
                    if (chip >= _momentChips.Count) { return false; }
                    _draftId = _momentChips[chip];
                    return true;
                case GroupTag:
                    _tagFriend = chip == 1;
                    return true;
                case GroupReport:
                    if (chip >= FlareText.ReportKeys.Length) { return false; }
                    _reportArg = chip;
                    return true;
                case GroupReviewShop:
                    if (chip >= _reviewPlaces.Count) { return false; }
                    _reviewPlace = _reviewPlaces[chip];
                    _reviewName = _reviewNames[chip];
                    return true;
                case GroupStars:
                    if (chip >= 5) { return false; }
                    _stars = chip + 1;
                    return true;
                case GroupAnnouncePlace:
                    _place = chip;
                    Photos(_flare.MyShops());
                    return true;
                case GroupAnnounceKind:
                    if (chip >= FlareText.AnnounceKeys.Length) { return false; }
                    _announceArg = chip;
                    _when = -1;
                    return true;
                case GroupWhen:
                    if (chip >= _whenChips.Count) { return false; }
                    _when = _whenChips[chip];
                    return true;
                case GroupTone:
                    if (chip >= FlareText.ToneKeys.Length) { return false; }
                    _tone = chip;
                    return true;
                case GroupMention:
                    if (chip >= _mentionKeys.Count) { return false; }
                    _mention = _mentionKeys[chip];
                    return true;
                default:
                    return false;
            }
        }
    }
}
