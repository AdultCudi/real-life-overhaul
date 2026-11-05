using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Flare's kit face, what the audit added to its working pages: an ad run for one of the player's businesses (from the Business page, from a
    /// trend, in the director's own ad path), the player's profile written (the words and the picture across its head), a campaign ended, the
    /// notifications marked read, and the hire sheet's audience by district. The choices live in FlareKitPress (game-free, pressed by EconomyCheck);
    /// this part is the face's side of them: the sheets' words and the calls to the director. Cost: written when a sheet changes, nothing per frame.
    /// </summary>
    public sealed partial class FlareScreen
    {
        private int _pickFrom = FlareFace.ComposeSheet;   // the sheet the photo picker was opened from
        private int _bannerChip;                          // the profile sheet's picture: 0 the newest, then the bank's photos, then the Gallery's

        private bool KitPress(int command, int arg)
        {
            bool done = _kit.Press(command, arg, FlareDeskRules.NicheCount);
            if (done)
            {
                _m.List.Offset = 0f;
            }
            return done;
        }

        // ------------------------------------------------------------------------------------------------ the host's calls

        bool IFlareKitHost.EndHire(int id)
        {
            FlareFile file = _flare != null && _flare.Running ? _flare.File : null;
            if (file == null)
            {
                return false;
            }
            _flare.EndHire(id);
            for (int i = 0; i < file.Hires.Count; i++)
            {
                if (file.Hires[i].Id == id)
                {
                    return file.Hires[i].Ended;
                }
            }
            return false;
        }

        bool IFlareKitHost.BuyAd(string place, int target, int size, int days, out string why)
        {
            FlareAd ad = _flare.AdOf(place, target, size, days);
            return _flare.AdBuy(ad, out why);
        }

        bool IFlareKitHost.SaveProfile(string bio, string banner)
        {
            return _flare != null && _flare.SetProfile(bio, banner);
        }

        bool IFlareKitHost.OpenHire(int influencer) => OpenHire(influencer);

        void IFlareKitHost.MarkNoticesSeen()
        {
            if (_flare == null || !_flare.Running)
            {
                return;
            }
            _flare.NoticesSeen();
            _seenBefore = _flare.Now;   // what was new stops being marked while the page shows
        }

        // ------------------------------------------------------------------------------------------------ an ad

        /// <summary>The ad sheet for a business of the player's, by its place in the list; false without the residents' side.</summary>
        private bool OpenAd(int shop)
        {
            List<FlareShop> shops = _flare != null && _flare.Running ? _flare.MyShops() : null;
            if (shops == null || !_flare.CityOn || shop < 0 || shop >= shops.Count)
            {
                return false;
            }
            _kit.BeginAd(shops[shop].Place, shop);
            _sheet = FlareFace.AdSheet;
            _field = -1;
            _sheetWhy = null;
            _dirty = true;
            return true;
        }

        // A trend card's Promote: the compose sheet about the business in the trend's district, its promotion already chosen.
        private bool PromoteTrend(int shop, string tag)
        {
            List<FlareShop> shops = _flare != null && _flare.Running ? _flare.MyShops() : null;
            if (shops == null || shop < 0 || shop >= shops.Count || !OpenCompose(2))
            {
                return false;
            }
            _place = shop;
            _promote = 1;
            _composeTag = tag;   // the post is about the trend: its subject is the tag
            Photos(shops);
            return true;
        }

        private void AdSheet(FlareSheetModel k)
        {
            List<FlareShop> shops = _flare.MyShops();
            if (string.IsNullOrEmpty(_kit.AdPlace) || _kit.AdShop >= shops.Count || !_flare.CityOn)
            {
                _sheet = FlareFace.NoSheet;
                _m.Sheet = FlareFace.NoSheet;
                return;
            }
            FlareShop shop = shops[_kit.AdShop];
            k.Title = FlareText.Fill("reallife_flare_ad_title", "business", shop.Name ?? "?");
            k.Line = FlareText.T("reallife_flare_kit_ad_line");
            var groups = new List<string>(3);
            var chips = new List<string[]>(3);
            var chosen = new List<int>(3);
            var targets = new string[FlareCityCatalogue.AdTargets];
            for (int i = 0; i < targets.Length; i++)
            {
                targets[i] = FlareText.T(FlareCityViews.TargetKey(i));
            }
            var sizes = new string[FlareCityCatalogue.AdReach.Length];
            for (int i = 0; i < sizes.Length; i++)
            {
                sizes[i] = FlareText.N(FlareCityCatalogue.AdReach[i]);
            }
            var days = new string[FlareCityCatalogue.AdDays.Length];
            for (int i = 0; i < days.Length; i++)
            {
                days[i] = FlareCityCatalogue.AdDays[i] == 1 ? FlareText.T("reallife_flare_kit_day_one")
                    : FlareText.Fill("reallife_flare_kit_days_n", "count", FlareCityCatalogue.AdDays[i].ToString(CultureInfo.InvariantCulture));
            }
            Group(groups, chips, chosen, GroupAdTarget, FlareText.T("reallife_flare_ad_label_target"), targets, _kit.AdTarget);
            Group(groups, chips, chosen, GroupAdSize, FlareText.T("reallife_flare_ad_label_size"), sizes, _kit.AdSize);
            Group(groups, chips, chosen, GroupAdDays, FlareText.T("reallife_flare_ad_label_days"), days, _kit.AdDay);
            k.Groups = groups.ToArray();
            k.Chips = chips.ToArray();
            k.Chosen = chosen.ToArray();
            FlareAd ad = _flare.AdOf(_kit.AdPlace, _kit.AdTarget, _kit.AdSize, _kit.AdDay);
            bool can = _flare.AdQuote(ad, out long cost, out int target, out string why);
            if (can)
            {
                k.FigureLabels = new[] { FlareText.T("reallife_flare_kit_fig_reach_day"), FlareText.T("reallife_flare_kit_fig_holds"), FlareText.T("reallife_flare_kit_fig_cost") };
                k.Figures = new[] { FlareFeedRules.Count(Math.Min(ad.Reach, target)), FlareFeedRules.Count(target), FlareText.Money((int)(cost / 100)) };
                k.Note = _flare.SharedAdWith(_kit.AdPlace, out string other) ? FlareText.Fill("reallife_flare_kit_reach_shared", "business", other) : null;
            }
            else
            {
                k.Note = FlareText.T(why ?? "reallife_flare_ad_why_off");
                k.NoteBad = true;
            }
            if (_kit.AdWhy != null)
            {
                k.Note = FlareText.T(_kit.AdWhy);
                k.NoteBad = true;
            }
            k.Confirm = can ? FlareText.Fill("reallife_flare_kit_ad_pay", "money", FlareText.Money((int)(cost / 100))) : FlareText.T("reallife_flare_city_action_run");
            k.ConfirmOff = !can;
        }

        private bool AdNow()
        {
            if (_kit.AdConfirm())
            {
                CloseSheet();   // the ad ran: paid at its business, shown to the residents today, measured from tomorrow
            }
            _dirty = true;
            return true;
        }

        // ------------------------------------------------------------------------------------------------ the profile

        private bool OpenEdit()
        {
            FlareFile file = _flare != null && _flare.Running ? _flare.File : null;
            if (file == null)
            {
                return false;
            }
            _kit.BeginEdit(file.Bio, file.Banner);
            _flare.BankPhotos(null, null, _photos, PhotosMost);
            _bannerChip = string.IsNullOrEmpty(file.Banner) ? 0 : _photos.IndexOf(file.Banner) >= 0 ? _photos.IndexOf(file.Banner) + 1 : _photos.Count + 1;
            _chosenPhoto = _bannerChip > _photos.Count ? file.Banner : null;
            _sheet = FlareFace.EditSheet;
            _field = FlareFace.BioField;
            _sheetWhy = null;
            _dirty = true;
            return true;
        }

        private void EditSheet(FlareSheetModel k)
        {
            k.Title = FlareText.T("reallife_flare_kit_edit_profile");
            k.Line = null;
            k.Field = true;
            k.FieldArg = FlareFace.BioField;
            k.FieldText = _kit.Bio;
            k.FieldHint = FlareText.T("reallife_flare_kit_bio_hint");
            k.FieldCount = FlareText.Fill("reallife_flare_kit_count", "count", (_kit.Bio ?? string.Empty).Length.ToString(CultureInfo.InvariantCulture), "most",
                FlareDeskRules.BioMost.ToString(CultureInfo.InvariantCulture));
            k.Picture = _kit.Banner;
            var banners = new string[_photos.Count + 2];
            banners[0] = FlareText.T("reallife_flare_kit_banner_newest");
            for (int i = 0; i < _photos.Count; i++)
            {
                banners[i + 1] = FlareText.Fill("reallife_flare_kit_photo_n", "n", (i + 1).ToString(CultureInfo.InvariantCulture));
            }
            banners[_photos.Count + 1] = FlareText.T("reallife_flare_kit_photo_gallery");
            var groups = new List<string>(1);
            var chips = new List<string[]>(1);
            var chosen = new List<int>(1);
            Group(groups, chips, chosen, GroupBanner, FlareText.T("reallife_flare_kit_group_banner"), banners, Math.Min(_bannerChip, banners.Length - 1));
            k.Groups = groups.ToArray();
            k.Chips = chips.ToArray();
            k.Chosen = chosen.ToArray();
            if (_sheetWhy != null)
            {
                k.Note = FlareText.T(_sheetWhy);
                k.NoteBad = true;
            }
            k.Confirm = FlareText.T("reallife_flare_kit_save");
        }

        // A chip of the profile sheet's picture group: the newest picture, a photo of the bank, or one from the Gallery.
        private bool BannerChip(int chip)
        {
            if (chip < 0 || chip > _photos.Count + 1)
            {
                return false;
            }
            if (chip == _photos.Count + 1)
            {
                return OpenPicker();
            }
            _bannerChip = chip;
            _kit.Banner = chip == 0 ? null : _photos[chip - 1];
            return true;
        }

        private bool EditNow()
        {
            if (_kit.EditConfirm())
            {
                CloseSheet();
            }
            else
            {
                _sheetWhy = "reallife_flare_kit_edit_failed";
            }
            _dirty = true;
            return true;
        }
    }
}
