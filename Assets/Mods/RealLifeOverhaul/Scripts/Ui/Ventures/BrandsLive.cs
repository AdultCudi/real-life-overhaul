using System;
using System.Collections.Generic;
using System.Globalization;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Economy.Brands;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Brands pages from the brands' state and the game and carries every button to the director that already owns it, as the old app did: the same methods, the same
    /// refusals as notices, the same question boxes. A page is written when the director's revision moves or the player pressed something; nothing a frame.
    /// </summary>
    public sealed partial class BrandsLive : IVentSource, IBraActions
    {
        public static readonly BrandsLive Instance = new BrandsLive();
        public const string ScreenId = "brands";

        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;
        private static int _wantTab = -1;
        private static string _wantPick;
        private int _local;
        private readonly VentWords _words = new VentWords((key, english) => BrandsText.T(key), x => BrandsText.Money(x));
        private VentView _view;

        private BrandsLive()
        {
        }

        /// <summary>A notice asks for a tab and a row: taken when the screen next opens or is next written.</summary>
        public static void Want(int tab, string pick)
        {
            _wantTab = tab;
            _wantPick = pick;
            Instance._local++;
        }

        public int Generation => (BrandsDirector.Instance != null ? BrandsDirector.Instance.Revision : 0) + _local;

        public void Opened(VentView view)
        {
            BrandsDirector d = BrandsDirector.Instance;
            if (d != null)
            {
                d.AppOpened();
            }
            TakeWant(view);
        }

        private static void TakeWant(VentView view)
        {
            if (_wantTab < 0)
            {
                return;
            }
            view.Tab = Math.Max(0, Math.Min(3, _wantTab));
            view.Pick = _wantPick;
            view.From = null;
            view.Page = 0;
            _wantTab = -1;
            _wantPick = null;
        }

        public bool Back(VentView view) => false;

        public void Fill(VentModel m, VentView v)
        {
            _view = v;
            TakeWant(v);
            BrandsDirector d = BrandsDirector.Instance;
            BraFacts f = d != null && d.Running && d.State != null && SaveGameManager.Current != null ? Facts(d, v) : new BraFacts { Ready = false };
            BraPages.Fill(m, v, f, this, _words);
        }

        // ------------------------------------------------------------------------------------------------- the buttons

        private static void Confirm(string title, string body, object data, Action yes, bool negative)
        {
            GameConfirm.Show(title.Localize(), body.Localize(data), Guard.Wrap(yes), null, null, null, false, negative);
        }

        private void Done()
        {
            _local++;
        }

        public void Rename(int brandId)
        {
            BrandsDirector.Instance?.Rename(brandId);
            Done();
        }

        public void Minimum(int brandId, int step)
        {
            BrandsDirector.Instance?.SetMinimum(brandId, step);
            Done();
        }

        public void Offer(int brandId, bool on)
        {
            BrandsDirector.Instance?.Offer(brandId, on);
            Done();
        }

        public void KeepPrices(int brandId, bool on)
        {
            BrandsDirector.Instance?.KeepBrandPrices(brandId, on);
            Done();
        }

        public void KeepLocal(int brandId, bool on)
        {
            BrandsDirector.Instance?.KeepBrandLocal(brandId, on);
            Done();
        }

        public void SharePrices(int brandId)
        {
            Brand b = BrandOf(brandId);
            BrandsDirector d = BrandsDirector.Instance;
            if (b == null || d == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_prices_title", "reallife_brand_confirm_prices", new { brand = b.Name, business = d.ShopName(b.Flagship) }, () =>
            {
                d.SharePrices(brandId);
                Done();
            }, false);
        }

        public void ShareHours(int brandId)
        {
            Brand b = BrandOf(brandId);
            BrandsDirector d = BrandsDirector.Instance;
            if (b == null || d == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_hours_title", "reallife_brand_confirm_hours", new { brand = b.Name, business = d.ShopName(b.Flagship) }, () =>
            {
                d.ShareHours(brandId);
                Done();
            }, false);
        }

        public void GiveUp(int brandId)
        {
            Brand b = BrandOf(brandId);
            BrandsDirector d = BrandsDirector.Instance;
            if (b == null || d == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_give_up_title", "reallife_brand_confirm_give_up", new { brand = b.Name }, () =>
            {
                _view.Pick = null;
                d.GiveUp(brandId);
                Done();
            }, true);
        }

        public void BookPush(int brandId, int kind, int districtIndex)
        {
            BrandsDirector d = BrandsDirector.Instance;
            Brand b = BrandOf(brandId);
            if (d == null || b == null)
            {
                return;
            }
            string district = null;
            if (kind == BrandCatalogue.PushLocal)
            {
                int at = 0;
                for (int i = 0; i < b.Districts.Count; i++)
                {
                    if (BrandRules.Shops(d.State, b, b.Districts[i].Name) > 0)
                    {
                        if (at++ == districtIndex)
                        {
                            district = b.Districts[i].Name;
                        }
                    }
                }
            }
            d.BookPush(brandId, kind, district);
            Done();
        }

        public void SellBrand(int franchiseId)
        {
            BrandsDirector d = BrandsDirector.Instance;
            Franchise f = d != null && d.State != null ? d.State.Franchise(franchiseId) : null;
            Brand b = f != null ? BrandOf(f.BrandId) : null;
            if (b == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_sell_title", "reallife_brand_confirm_sell", new { brand = b.Name, amount = BrandsText.Money(b.Bid) }, () =>
            {
                d.Agree(franchiseId);
                Done();
            }, true);
        }

        public void RefuseBid(int franchiseId)
        {
            BrandsDirector.Instance?.Decline(franchiseId);
            Done();
        }

        public void Locate(string place)
        {
            if (!EstateGame.Locate(place))
            {
                UI.Notification.Notifications.Show(UI.Notification.NotificationType.Warning, "reallife_ven_note_no_place", null, 6f);
            }
            Done();
        }

        public void Leave(string place)
        {
            BrandsDirector d = BrandsDirector.Instance;
            if (d == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_leave_title", "reallife_brand_confirm_leave", new { business = d.ShopName(place) }, () =>
            {
                d.Leave(place);
                Done();
            }, true);
        }

        public void MakeBrand(string place)
        {
            BrandsDirector.Instance?.CreateBrand(place);
            Done();
        }

        public void JoinBrand(int brandId, string place)
        {
            BrandsDirector.Instance?.Join(brandId, place);
            Done();
        }

        public void TakeChain(string place, int chain)
        {
            BrandsDirector d = BrandsDirector.Instance;
            if (d == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_chain_title", "reallife_brand_confirm_chain",
                new { business = d.ShopName(place), chain = BrandsText.Chain(chain), amount = BrandsText.Money(FranchiseRules.ChainFee(chain, BrandsDirector.Index)) }, () =>
                {
                    d.TakeChain(place, chain);
                    Done();
                }, false);
        }

        public void LeaveChain(string place)
        {
            BrandsDirector d = BrandsDirector.Instance;
            BrandShop shop = d != null ? d.ShopAt(place) : null;
            if (shop == null || shop.Chain == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_chain_leave_title", "reallife_brand_confirm_chain_leave",
                new { business = shop.Name, chain = BrandsText.Chain(shop.Chain.Chain), amount = BrandsText.Money(d.ChainExit(shop.Chain)) }, () =>
                {
                    d.LeaveChain(place);
                    Done();
                }, true);
        }

        public void KeepChain(string place, bool on)
        {
            BrandsDirector.Instance?.KeepChainPrices(place, on);
            Done();
        }

        public void ChainPrices(string place)
        {
            BrandsDirector d = BrandsDirector.Instance;
            BrandShop shop = d != null ? d.ShopAt(place) : null;
            if (shop == null || shop.Chain == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_chain_prices_title", "reallife_brand_confirm_chain_prices", new { business = shop.Name, chain = BrandsText.Chain(shop.Chain.Chain) }, () =>
            {
                d.ChainPrices(place);
                Done();
            }, false);
        }

        public void Sign(int applicantId, int site, int fee, int royalty, int levy, bool exclusive, int length)
        {
            BrandsDirector d = BrandsDirector.Instance;
            BrandsState state = d != null ? d.State : null;
            Applicant a = state != null ? state.Applicant(applicantId) : null;
            Brand b = a != null ? state.Brand(a.BrandId) : null;
            if (b == null)
            {
                return;
            }
            Confirm("reallife_brand_confirm_sign_title", "reallife_brand_confirm_sign",
                new { name = a.Name, brand = b.Name, amount = BrandsText.Money(FranchiseRules.EntryFee(a, fee, BrandsDirector.Index)),
                    royalty = BrandsText.Bp(BrandCatalogue.RoyaltyBp[royalty]) }, () =>
                {
                    d.Sign(applicantId, site, fee, royalty, levy, exclusive, length);
                    Done();
                }, false);
        }

        public void Refuse(int applicantId)
        {
            BrandsDirector.Instance?.Refuse(applicantId);
            Done();
        }

        public void Inspect(int franchiseId)
        {
            BrandsDirector.Instance?.Inspect(franchiseId);
            Done();
        }

        public void Warn(int franchiseId)
        {
            BrandsDirector.Instance?.Warn(franchiseId);
            Done();
        }

        public void Retrain(int franchiseId)
        {
            BrandsDirector.Instance?.Retrain(franchiseId);
            Done();
        }

        public void Agree(int franchiseId)
        {
            BrandsDirector.Instance?.Agree(franchiseId);
            Done();
        }

        public void Waive(int franchiseId)
        {
            BrandsDirector.Instance?.Waive(franchiseId);
            Done();
        }

        public void Decline(int franchiseId)
        {
            BrandsDirector.Instance?.Decline(franchiseId);
            Done();
        }

        public void End(int franchiseId)
        {
            BrandsDirector d = BrandsDirector.Instance;
            Franchise f = d != null && d.State != null ? d.State.Franchise(franchiseId) : null;
            Brand b = f != null ? BrandOf(f.BrandId) : null;
            if (b == null)
            {
                return;
            }
            bool cause = d.EndCost(f) == 0;
            Confirm("reallife_brand_confirm_end_title", cause ? "reallife_brand_confirm_end_cause" : "reallife_brand_confirm_end",
                new { name = f.Name, amount = BrandsText.Money(d.EndCost(f)), points = BrandsText.Lost(BrandCatalogue.UnfairLoss) }, () =>
                {
                    _view.Pick = null;
                    d.End(franchiseId);
                    Done();
                }, true);
        }

        private static Brand BrandOf(int id)
        {
            BrandsDirector d = BrandsDirector.Instance;
            return d != null && d.State != null ? d.State.Brand(id) : null;
        }
    }
}
