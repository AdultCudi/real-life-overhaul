using System;
using System.Collections.Generic;
using System.Globalization;
using RealLifeOverhaul.ParkSpaces;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a car park's page asks for when a button is pressed; the live page asks the player first and calls the director, the checks and previews record it.</summary>
    public interface ISpaceActions
    {
        void Buy(int park, int slot);
        void Rent(int park, int slot);
        void Sell(int park, int slot, long back);
        void SetEnding(int park, int slot, bool ending);
    }

    /// <summary>
    /// The words of the parking spaces that are built from the state, game-free: a space's number, a building's owner, how a space is
    /// held, how a car park's row reads, why something is refused. The words come through <see cref="UiWords"/> (the game's
    /// localisation in play, a locale file in the checks and previews), each with the English it stands for.
    /// </summary>
    public static class SpaceWords
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        private static readonly string[] RefusalKeys =
        {
            null, "reallife_space_refuse_off", "reallife_space_refuse_session", "reallife_space_refuse_no_park", "reallife_space_refuse_full", "reallife_space_refuse_limit",
            "reallife_space_refuse_park_limit", "reallife_space_refuse_cash", "reallife_space_refuse_not_held", "reallife_space_refuse_own_building", "reallife_space_refuse_rented",
            "reallife_space_refuse_owned"
        };

        private static readonly string[] RefusalEnglish =
        {
            string.Empty, "Parking spaces are switched off or not ready yet.", "Parking spaces are not bought, rented or sold in a shared city.", "That car park is not there.",
            "Every space in this car park is taken. One comes free when somebody gives theirs up.", "You hold as many spaces as one person may.",
            "You hold as many spaces in this car park as one person may.", "Not enough cash for this space.", "That space is not yours.",
            "This building is yours. Its free spaces are yours already, and there is nobody to buy from or sell to.", "A rented space is not sold. End the rental instead.",
            "A bought space has no rental to end. Sell it back instead."
        };

        public static string Fill(string text, params string[] pairs)
        {
            for (int i = 0; i + 1 < pairs.Length; i += 2)
            {
                text = text.Replace("{" + pairs[i] + "}", pairs[i + 1] ?? string.Empty);
            }
            return text;
        }

        /// <summary>The text key that says why something was refused; null for nothing refused.</summary>
        public static string WhyKey(SpaceRefusal r) => RefusalKeys[SpaceCatalogue.Clamp((int)r, 0, RefusalKeys.Length - 1)];

        public static string Why(UiWords t, SpaceRefusal r)
        {
            int at = SpaceCatalogue.Clamp((int)r, 0, RefusalKeys.Length - 1);
            return at == 0 ? string.Empty : t(RefusalKeys[at], RefusalEnglish[at]);
        }

        public static string Pair(UiWords t, string first, string second) => Fill(t("reallife_ui_estate_pair", "{first}, {second}"), "first", first, "second", second);

        public static string Space(UiWords t, int slot) => Fill(t("reallife_space_number", "Space {number}"), "number", slot.ToString(Inv));

        /// <summary>Who sells a car park's spaces: the rival that owns the building by name, the player, or the building's landlord.</summary>
        public static string Owner(UiWords t, SpacePark p)
        {
            return p.Owner == SpaceOwner.Player ? t("reallife_space_owner_you", "You") : p.Owner == SpaceOwner.Rival && !string.IsNullOrEmpty(p.OwnerName) ? p.OwnerName
                : t("reallife_space_owner_landlord", "The building's landlord");
        }

        /// <summary>How a space is held: bought, rented until a day, or rented and given up.</summary>
        public static string Tenure(UiWords t, SpaceHolding h, Func<int, string> dayText)
        {
            if (!h.Rented)
            {
                return t("reallife_space_tenure_owned", "Bought");
            }
            return Fill(h.Ending ? t("reallife_space_tenure_ending", "Rented, ends on {day}") : t("reallife_space_tenure_rented", "Rented, next rent on {day}"), "day", dayText(h.PaidTo + 1));
        }

        /// <summary>A car park's state on its row: the player's own building, how many spaces they hold there, how many are free, or full.</summary>
        public static string RowState(UiWords t, SpacePark p, int player, int free)
        {
            return p.Owner == SpaceOwner.Player ? t("reallife_space_row_yours", "Your building")
                : player > 0 ? Fill(t("reallife_space_row_held", "You hold {count}"), "count", player.ToString(Inv))
                : free > 0 ? Fill(t("reallife_space_row_free", "{count} free"), "count", free.ToString(Inv)) : t("reallife_space_row_full", "Full");
        }
    }

    /// <summary>
    /// A building's car park on the kit's Property app, game-free so the checks and previews lay out exactly what ships: its row on
    /// the Market tab, and its page with what a space costs, who holds the spaces, buying and renting one, and the player's spaces
    /// there with their way out. Every button runs one call of <see cref="ISpaceActions"/>.
    /// </summary>
    public static class SpacePages
    {
        private static readonly CultureInfo Inv = CultureInfo.InvariantCulture;

        /// <summary>Every key the row and the page label with: the Property screen's entry names them, so the texts checks see each one translated.</summary>
        public static readonly string[] Keys =
        {
            "reallife_co_back", "reallife_ui_estate_pair", "reallife_econ_day", "reallife_space_kind", "reallife_space_number", "reallife_space_page_title", "reallife_space_row_free",
            "reallife_space_row_full", "reallife_space_row_held", "reallife_space_row_yours", "reallife_space_fig_price", "reallife_space_fig_rent", "reallife_space_fig_fee",
            "reallife_space_fig_spaces", "reallife_space_sec_take", "reallife_space_sec_holders", "reallife_space_sec_mine", "reallife_space_l_seller", "reallife_space_l_you",
            "reallife_space_l_rivals", "reallife_space_l_city", "reallife_space_l_free", "reallife_space_l_hours", "reallife_space_what", "reallife_space_what_yours",
            "reallife_space_owner_you", "reallife_space_owner_landlord", "reallife_space_tenure_owned", "reallife_space_tenure_rented", "reallife_space_tenure_ending",
            "reallife_space_btn_buy", "reallife_space_btn_rent", "reallife_space_btn_sell", "reallife_space_btn_end", "reallife_space_btn_keep", "reallife_space_refuse_off",
            "reallife_space_refuse_session", "reallife_space_refuse_no_park", "reallife_space_refuse_full", "reallife_space_refuse_limit", "reallife_space_refuse_park_limit",
            "reallife_space_refuse_cash", "reallife_space_refuse_not_held", "reallife_space_refuse_own_building", "reallife_space_refuse_rented", "reallife_space_refuse_owned"
        };

        /// <summary>A car park's row on the Market tab, in that table's columns: the building, the price, no condition, no rent a month, its state.</summary>
        public static UiDataRow MarketRow(UiWords t, SpaceState s, SpacePark p, int day, Func<long, string> money)
        {
            SpaceRules.Counts(s, p, day, out int player, out int _, out int _, out int free);
            return new UiDataRow
            {
                Cells = new[] { p.Name, money(p.Price), string.Empty, string.Empty, SpaceWords.RowState(t, p, player, free), string.Empty },
                Values = new[] { (int)UiIcon.Car, 0, -1, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                Subs = new[] { SpaceWords.Pair(t, t("reallife_space_kind", "parking space"), p.District), null, null, null, null, null }
            };
        }

        public static void Fill(EstateModel m, EstateRuns runs, UiWords t, SpaceState s, SpacePark p, int day, SpaceRefusal buy, SpaceRefusal rent, int slot, Func<long, string> money,
            Func<int, string> dayText, ISpaceActions act)
        {
            int park = p.Number;
            bool own = p.Owner == SpaceOwner.Player;
            SpaceRules.Counts(s, p, day, out int player, out int rivals, out int city, out int free);
            m.Shape = EstateShape.Detail;
            m.Back = t("reallife_co_back", "Back");
            m.DetailIcon = UiIcon.Car;
            m.DetailTitle = SpaceWords.Fill(t("reallife_space_page_title", "Car park, {name}"), "name", p.Name);
            m.DetailSub = SpaceWords.Pair(t, t("reallife_space_kind", "parking space"), p.District);
            m.DetailChip = free > 0 ? SpaceWords.Fill(t("reallife_space_row_free", "{count} free"), "count", free.ToString(Inv)) : t("reallife_space_row_full", "Full");
            m.DetailChipTone = free > 0 ? UiRole.Ok : UiRole.Warn;
            m.Strip = new[]
            {
                new UiFigureModel { Label = t("reallife_space_fig_price", "Price of a space"), Value = money(p.Price) },
                new UiFigureModel { Label = SpaceWords.Fill(t("reallife_space_fig_rent", "Rent for {days} days"), "days", SpaceCatalogue.RentDays.ToString(Inv)), Value = money(p.RentMonth) },
                new UiFigureModel { Label = t("reallife_space_fig_fee", "The hourly charge"), Value = money(SpaceCatalogue.HourFee) },
                new UiFigureModel { Label = t("reallife_space_fig_spaces", "Spaces"), Value = p.Capacity.ToString(Inv) }
            };
            var take = new List<UiFactLine>(4)
            {
                UiFactLine.Row(t("reallife_space_l_seller", "Sold by"), SpaceWords.Owner(t, p)),
                UiFactLine.Say(own
                    ? t("reallife_space_what_yours", "This building is yours, so every space nobody else holds is yours too. The hourly charge for your cars here is paid back at the day's end.")
                    : t("reallife_space_what", "For each space you hold here, one car you leave in this car park has its hourly charge paid back at the day's end."))
            };
            if (!own && buy != SpaceRefusal.None)
            {
                take.Add(UiFactLine.Say(SpaceWords.Why(t, buy), UiRole.WarnText));   // why no space can be bought now (the rent may still be within the cash)
            }
            var actions = new List<UiActionModel>(2);
            if (!own)
            {
                actions.Add(runs.Button(t("reallife_space_btn_rent", "Rent a space"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => act.Rent(park, slot), rent != SpaceRefusal.None));
                actions.Add(runs.Button(t("reallife_space_btn_buy", "Buy a space"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => act.Buy(park, slot), buy != SpaceRefusal.None));
            }
            m.Left = new[] { new UiFactCardModel { Title = t("reallife_space_sec_take", "A space of your own"), Heading = true, Lines = take.ToArray(), Actions = actions.ToArray() } };
            m.Right = new[]
            {
                new UiFactCardModel
                {
                    Title = t("reallife_space_sec_holders", "Who holds the spaces"), Heading = true, Lines = new[]
                    {
                        UiFactLine.Row(t("reallife_space_l_you", "You"), player.ToString(Inv)),
                        UiFactLine.Row(t("reallife_space_l_rivals", "Rival tycoons"), rivals.ToString(Inv)),
                        UiFactLine.Row(t("reallife_space_l_city", "The city's people"), city.ToString(Inv)),
                        UiFactLine.Row(t("reallife_space_l_free", "Free"), free.ToString(Inv)),
                        UiFactLine.Row(t("reallife_space_l_hours", "Car hours to pay back tonight"), s.Hours(park).ToString(Inv))
                    }
                }
            };
            var mine = new List<EstateRow>(SpaceCatalogue.HeldPerPark);
            for (int i = 0; i < s.Held.Count; i++)
            {
                SpaceHolding h = s.Held[i];
                if (!h.Players || h.Park != park)
                {
                    continue;
                }
                int at = h.Slot;
                long back = h.Rented ? 0L : SpaceRules.Back(h.Paid, p.Price);
                bool ending = h.Ending;
                mine.Add(new EstateRow
                {
                    Icon = UiIcon.Car, Title = SpaceWords.Space(t, h.Slot), Sub = SpaceWords.Tenure(t, h, dayText), Run = -1,
                    Button = h.Rented ? (ending ? t("reallife_space_btn_keep", "Keep the rental") : t("reallife_space_btn_end", "End the rental")) : t("reallife_space_btn_sell", "Sell it back"),
                    ButtonRun = h.Rented ? runs.Do(() => act.SetEnding(park, at, !ending)) : runs.Do(() => act.Sell(park, at, back)),
                    ButtonOff = !h.Rented && own
                });
            }
            m.RelatedTitle = t("reallife_space_sec_mine", "Your spaces here");
            m.RelatedCount = mine.Count > 0 ? mine.Count.ToString(Inv) : null;
            m.Related = mine.ToArray();
        }
    }
}
