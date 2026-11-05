using System;
using System.Collections.Generic;
using RealLifeOverhaul.Rivals;
using UI.Notification;

namespace RealLifeOverhaul.Economy.Brands
{
    /// <summary>
    /// The director's side of what Brands keeps besides the brands: recognition week by week beside a rival's brand, royalties paid on time month by month and the rebate claim of a
    /// franchisee whose exclusive area a rival opened in. It runs on the day turn and only does work on a new week or month.
    /// </summary>
    public sealed partial class BrandsDirector
    {
        private BrandsExtra _extra = new BrandsExtra();
        private readonly List<RivalOutletFact> _outlets = new List<RivalOutletFact>(16);

        /// <summary>The extras of the save; null while Brands is not running.</summary>
        public BrandsExtra Extra => _running ? _extra : null;

        /// <summary>Moves with every change the kit's pages show.</summary>
        public int Revision { get; private set; }

        private static int MonthLength => Math.Max(1, GameCalendar.MonthDays);

        private void ExtraStart()
        {
            _extra = BrandExtraStore.Load(Save.modData);
            _extra.Prune(_state);
        }

        private void ExtraSave()
        {
            if (_extra != null)
            {
                BrandExtraStore.Save(Save.modData, _extra);
            }
        }

        /// <summary>The rivals' outlets as brands see them, read from the rivals' own file.</summary>
        internal void RivalOutlets(List<RivalOutletFact> into)
        {
            into.Clear();
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            for (int i = 0; file != null && i < file.Outlets.Count; i++)
            {
                RivalOutlet o = file.Outlets[i];
                into.Add(new RivalOutletFact { Move = o.Move, Tycoon = o.Tycoon, District = o.District, Type = o.Type });
            }
        }

        /// <summary>The rival that has most outlets of a brand's kind, and how many: the rival brand beside it.</summary>
        internal int RivalOf(string type, List<RivalOutletFact> outlets, out int count)
        {
            var counts = new Dictionary<int, int>(4);
            for (int i = 0; i < outlets.Count; i++)
            {
                if (string.Equals(outlets[i].Type, type, StringComparison.Ordinal))
                {
                    int n;
                    counts.TryGetValue(outlets[i].Tycoon, out n);
                    counts[outlets[i].Tycoon] = n + 1;
                }
            }
            int best = -1;
            count = 0;
            foreach (KeyValuePair<int, int> pair in counts)
            {
                if (pair.Value > count || (pair.Value == count && pair.Key < best))
                {
                    best = pair.Key;
                    count = pair.Value;
                }
            }
            return best;
        }

        private void ExtraDay(int day)
        {
            RivalOutlets(_outlets);
            bool changed = false;
            for (int i = 0; i < _state.Brands.Count; i++)
            {
                Brand b = _state.Brands[i];
                int count;
                int rival = RivalOf(b.Type, _outlets, out count);
                changed |= BrandExtraRules.Week(_extra, b, _state.Seed, Math.Max(0, rival), count, day);
            }
            for (int i = 0; i < _state.Franchises.Count; i++)
            {
                Franchise f = _state.Franchises[i];
                changed |= BrandExtraRules.Month(_extra, f, day, MonthLength);
                Rebate claim = BrandExtraRules.Inside(_extra, _state, f, _outlets, day);
                Brand b = _state.Brand(f.BrandId);
                if (claim != null && b != null)
                {
                    BrandExtraRules.File(b, f, day);
                    changed = true;
                    Tell(NotificationType.Warning, "reallife_ui_brand_note_rebate", new Dictionary<string, string>
                    {
                        { "name", f.Name }, { "brand", b.Name }, { "type", BrandsText.Type(b.Type) }, { "district", BrandsText.District(claim.District) }, { "amount",
                            BrandsText.Money(f.AskAmount) }
                    }, TabAgreements, "fr:" + f.Id);
                }
            }
            if (changed)
            {
                Revision++;
            }
        }
    }
}
