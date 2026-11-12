using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy.Brands;
using RealLifeOverhaul.Loyalty;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The loyalty cards with the rest of the mod:
        ///
        ///   BusinessGame.Ambient   a scheme's lift on top of the earlier pairings' part of a business's footfall (the city's, the
        ///                          company's, the brands', Flare's, the faults' and the rivals'); business pressure bounds the sum,
        ///                          and the scheme is told what of its lift the bounds carried
        ///   Footfall               business pressure's one writer of a business's figure
        ///   Chain                  a shop's brand (Economy/Brands): the brand's shops share one card's terms
        ///   Outlets                the rival tycoons' outlets that stand on no business of the game's: they run a card by the same rules
        ///
        /// Connected after the brands', Flare's, the interiors' and the rivals' and disconnected before them, so the footfall it
        /// wrapped goes back as found. Asked when a figure is written, at a day's turn or a press, never per frame; a fault is
        /// said once (<see cref="Fault"/>) and the bridge's default answered.
        /// </summary>
        public static class LoyaltyLinks
        {
            private const int OutletQualityLeast = 55, OutletQualitySpan = 31;   // satisfaction (0 to 100) a tycoon's own outlet is taken to have, from its seed

            private static Func<BuildingRegistration, int> _ambientBefore;

            internal static void Connect()
            {
                LoyaltyBridges.Footfall = Footfall;
                LoyaltyBridges.Chain = Chain;
                LoyaltyBridges.Outlets = Outlets;
                _ambientBefore = BusinessGame.Ambient;
                BusinessGame.Ambient = Ambient;
            }

            internal static void Disconnect()
            {
                LoyaltyBridges.Reset();
                BusinessGame.Ambient = _ambientBefore;   // the rivals', the interiors', Flare's and the others' pairings take theirs off next
                _ambientBefore = null;
            }

            // The earlier pairings' part with the scheme's lift on top; business pressure bounds the sum.
            private static int Ambient(BuildingRegistration reg)
            {
                int before = 0;
                try
                {
                    before = _ambientBefore != null ? _ambientBefore(reg) : 0;
                }
                catch (Exception e)
                {
                    Fault("LoyaltyLinks.Ambient (the pairings before)", e);
                }
                try
                {
                    int lift = LoyaltyDirector.FootfallOf(reg);
                    if (lift != 0)
                    {
                        LoyaltyDirector.Carried(reg, BusinessCatalogue.ClampAmbient(before + lift) - BusinessCatalogue.ClampAmbient(before));
                    }
                    return before + lift;
                }
                catch (Exception e)
                {
                    Fault("LoyaltyLinks.Ambient", e);
                    return before;
                }
            }

            private static bool Footfall(BuildingRegistration reg, bool redraw, bool newDay, out int own, out int written)
            {
                own = written = 0;
                try
                {
                    return BusinessDirector.Footfall(reg, redraw, newDay, out own, out written);
                }
                catch (Exception e)
                {
                    Fault("LoyaltyLinks.Footfall", e);
                    return false;
                }
            }

            // A shop under a brand of the player's shares the brand's card: the key is the brand's own number.
            private static string Chain(string place, out string name)
            {
                name = null;
                try
                {
                    BrandsDirector brands = BrandsDirector.Instance;
                    BrandsState state = brands != null ? brands.State : null;
                    Member member = state != null ? state.Member(place) : null;
                    Brand brand = member != null ? state.Brand(member.BrandId) : null;
                    if (brand == null)
                    {
                        return null;
                    }
                    name = brand.Name;
                    return "brand" + brand.Id;
                }
                catch (Exception e)
                {
                    Fault("LoyaltyLinks.Chain", e);
                    return null;
                }
            }

            // The tycoons rest in a shared city; an outlet standing on one of the game's rival businesses is read with the city's shops.
            private static void Outlets(string district, string type, int today, List<LoyaltyRival> into)
            {
                try
                {
                    RivalFile f = RivalDirector.Instance != null && !GoingPublic.InSession ? RivalDirector.Instance.File : null;
                    for (int i = 0; f != null && i < f.Outlets.Count; i++)
                    {
                        RivalOutlet o = f.Outlets[i];
                        Tycoon t = o != null && o.Tycoon >= 0 && o.Tycoon < f.Tycoons.Length ? f.Tycoons[o.Tycoon] : null;
                        if (t == null || !t.Active || !string.IsNullOrEmpty(o.Seat) || !string.Equals(o.District, district, StringComparison.Ordinal)
                            || !string.Equals(o.Type, type, StringComparison.Ordinal))
                        {
                            continue;
                        }
                        RivalMove move = f.Move(o.Move);
                        int seed = LoyaltyRules.Hash(RivalFirms.Key(o));
                        into.Add(new LoyaltyRival
                        {
                            Seed = seed, Quality = OutletQualityLeast + LoyaltyRules.Roll(seed, 4, 0) % OutletQualitySpan, Days = move != null ? Math.Max(0, today - move.Land) : 0, Tycoon = true
                        });
                    }
                }
                catch (Exception e)
                {
                    Fault("LoyaltyLinks.Outlets", e);
                }
            }
        }
    }
}
