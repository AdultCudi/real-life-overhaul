using System;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// The director's side of what a web shop keeps beyond its old settings: the delivery fee and free-over amount, the area served, how long packing is told to take, night orders,
    /// prices that follow the lowest rival, the finished orders and the thirty days of sales. The rules ask these through the hooks of OnlineRules as orders come and arrive.
    /// </summary>
    public sealed partial class OnlineDirector
    {
        private OnlineExtra _extra = new OnlineExtra();

        /// <summary>The extras of the save; null while the shops do not run.</summary>
        public OnlineExtra Extra => _running ? _extra : null;

        /// <summary>Moves with every change the kit pages show.</summary>
        public int Revision { get; private set; }

        private void ExtraStart()
        {
            _extra = OnlineExtraStore.Load(Save.modData);
            OnlineRules.Gate = (shop, district, hour) => OnlineExtraRules.Takes(_extra.Of(shop.Place, true), district, hour);
            OnlineRules.PriceHook = (shop, line, ask) =>
            {
                ShopExtra x = _extra.Of(shop.Place, false);
                if (x == null || !x.Match || line < 0 || line >= shop.Lines.Count)
                {
                    return ask;
                }
                // Following the rivals only ever lowers a price, and never under seven tenths of it.
                int rival = OnlineExtraRules.LowestRival(_state.Seed, shop.Lines[line].Item, ask);
                return Math.Max(ask * 7 / 10, Math.Min(ask, rival));
            };
            OnlineRules.ArrivedHook = (shop, order, late) =>
            {
                ShopExtra x = _extra.Of(shop.Place, true);
                int day = order.Arrive / 24;
                OnlineExtraRules.Sold(x, day, order.Total);
                return OnlineExtraRules.Delivered(x, _state.Seed, order.Id, day, order.District, order.Count, order.Total, late);
            };
        }

        private void ExtraSave()
        {
            if (_extra != null && Save != null)
            {
                OnlineExtraStore.Save(Save.modData, _extra);
            }
        }

        private ShopExtra ExtraOf(string place) => _running ? _extra.Of(place, true) : null;

        /// <summary>The delivery fee a customer pays, in cents (0 for none).</summary>
        public void SetFee(string place, int cents)
        {
            ShopExtra x = ExtraOf(place);
            if (x != null)
            {
                x.FeeCents = Math.Max(0, Math.Min(5000, cents));
                Revision++;
            }
        }

        /// <summary>An order of this much or more is delivered free, in cents (0 for never).</summary>
        public void SetFreeOver(string place, int cents)
        {
            ShopExtra x = ExtraOf(place);
            if (x != null)
            {
                x.FreeOverCents = Math.Max(0, Math.Min(1000000, cents));
                Revision++;
            }
        }

        /// <summary>How long packing is told to take: 30 minutes, an hour or the day.</summary>
        public void SetPacking(string place, int packing)
        {
            ShopExtra x = ExtraOf(place);
            WebShop shop = _state != null ? _state.Shop(place) : null;
            if (x != null)
            {
                x.Packing = Math.Max(0, Math.Min(2, packing));
                if (shop != null && x.Packing != ShopExtra.PackingDay)
                {
                    shop.Promise = 0;   // a packing time under a day is a same day promise
                }
                Revision++;
            }
        }

        /// <summary>One of the shop's switches: night orders, matching the lowest rival, showing what is in stock.</summary>
        public void Toggle(string place, int which)
        {
            ShopExtra x = ExtraOf(place);
            if (x == null)
            {
                return;
            }
            x.Night ^= which == 0;
            x.Match ^= which == 1;
            x.ShowStock ^= which == 2;
            Revision++;
        }

        /// <summary>A district is served or not; the first press from "all" leaves out that one.</summary>
        public void ToggleDistrict(string place, int district)
        {
            ShopExtra x = ExtraOf(place);
            if (x == null || district < 0 || district >= Math.Min(31, _state.Districts.Count))
            {
                return;
            }
            int all = (int)((1L << Math.Min(31, _state.Districts.Count)) - 1);
            int mask = x.DistrictMask == 0 ? all : x.DistrictMask;
            mask ^= 1 << district;
            x.DistrictMask = mask == 0 || mask == all ? 0 : mask;
            Revision++;
        }
    }
}
