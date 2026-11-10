using System;
using System.Collections.Generic;
using Localizor;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.GameUi;
using RealLifeOverhaul.ParkSpaces;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Parking spaces on the kit's Property app: each building with a car park is a row of the Market tab beside the buildings and
    /// hotels for sale, its page sells or lets a space and lists the player's spaces there with their way out, and every space held
    /// is a row of the rent roll. Every press goes to <see cref="SpaceDirector"/>'s own call, asked first where money moves.
    /// </summary>
    public sealed partial class EstateLive
    {
        private sealed partial class Page
        {
            private const string SpacePick = "spc:";

            private static SpaceDirector SpacesNow => SpaceDirector.Instance != null && SpaceDirector.Instance.Running ? SpaceDirector.Instance : null;

            private static void AskSpace(string titleKey, string body, Action yes, bool negative)
            {
                GameConfirm.Show(titleKey.GetLocalization(), body, () => UiClone.Safe(yes), null, null, null, false, negative);
            }

            // The Market tab's rows: a row a car park, with its price and how many spaces nobody holds.
            private void SpaceRows(List<UiDataRow> rows, List<string> keys, List<int> locate)
            {
                SpaceDirector d = SpacesNow;
                for (int i = 0; d != null && i < d.Parks.Count; i++)
                {
                    SpacePark p = d.Parks[i];
                    SpaceRules.Counts(d.State, p, _day, out int player, out int _, out int _, out int free);
                    string state = p.Owner == SpaceOwner.Player ? SpaceText.T("reallife_space_row_yours")
                        : player > 0 ? SpaceText.Fill("reallife_space_row_held", "count", player.ToString(Inv))
                        : free > 0 ? SpaceText.Fill("reallife_space_row_free", "count", free.ToString(Inv)) : SpaceText.T("reallife_space_row_full");
                    locate.Add(-1);
                    rows.Add(new UiDataRow
                    {
                        Cells = new[] { p.Name, SpaceGame.Money(p.Price), string.Empty, string.Empty, state, string.Empty },
                        Values = new[] { (int)UiIcon.Car, 0, -1, 0, 0, 0 }, Tones = new[] { UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None, UiRole.None },
                        Subs = new[] { EstateText.Pair(SpaceText.T("reallife_space_kind"), p.District), null, null, null, null, null }
                    });
                    keys.Add(SpacePick + p.Number.ToString(Inv));
                }
            }

            // The rent roll's rows: a row a space the player holds, which opens its car park's page.
            private void SpaceHeld(List<UiDataRow> rows, List<string> keys)
            {
                SpaceDirector d = SpacesNow;
                for (int i = 0; d != null && i < d.State.Held.Count; i++)
                {
                    SpaceHolding h = d.State.Held[i];
                    SpacePark p = h.Players ? d.Park(h.Park) : null;
                    if (p == null)
                    {
                        continue;
                    }
                    rows.Add(OtherRow(UiIcon.Car, SpaceText.Fill("reallife_space_row_name", "space", SpaceText.Space(h.Slot), "name", p.Name), SpaceText.Tenure(h), 0,
                        SpaceText.T("reallife_space_kind"), UiRole.None));
                    keys.Add(SpacePick + p.Number.ToString(Inv));
                }
            }

            // A car park's page, if one is picked. True if it was written.
            private bool SpacePage()
            {
                string pick = Picked(_v.Pick, SpacePick);
                SpaceDirector d = SpacesNow;
                SpacePark p = pick != null && d != null && int.TryParse(pick, out int number) ? d.Park(number) : null;
                if (p == null)
                {
                    return false;
                }
                int park = p.Number;
                SpaceRules.Counts(d.State, p, _day, out int player, out int rivals, out int city, out int free);
                _m.Shape = EstateShape.Detail;
                _m.Back = EstateText.T("reallife_co_back");
                _m.DetailIcon = UiIcon.Car;
                _m.DetailTitle = SpaceText.Fill("reallife_space_page_title", "name", p.Name);
                _m.DetailSub = EstateText.Pair(SpaceText.T("reallife_space_kind"), p.District);
                _m.DetailChip = free > 0 ? SpaceText.Fill("reallife_space_row_free", "count", free.ToString(Inv)) : SpaceText.T("reallife_space_row_full");
                _m.DetailChipTone = free > 0 ? UiRole.Ok : UiRole.Warn;
                _m.Strip = new[]
                {
                    new UiFigureModel { Label = SpaceText.T("reallife_space_fig_price"), Value = SpaceGame.Money(p.Price) },
                    new UiFigureModel { Label = SpaceText.Fill("reallife_space_fig_rent", "days", SpaceCatalogue.RentDays.ToString(Inv)), Value = SpaceGame.Money(p.RentMonth) },
                    new UiFigureModel { Label = SpaceText.T("reallife_space_fig_fee"), Value = SpaceGame.Money(SpaceCatalogue.HourFee) },
                    new UiFigureModel { Label = SpaceText.T("reallife_space_fig_spaces"), Value = p.Capacity.ToString(Inv) }
                };
                SpaceRefusal buy = d.MayTake(park, false, out int _), rent = d.MayTake(park, true, out int slot);
                var take = new List<UiFactLine>(5)
                {
                    UiFactLine.Row(SpaceText.T("reallife_space_l_seller"), SpaceText.Owner(p)),
                    UiFactLine.Say(SpaceText.T(p.Owner == SpaceOwner.Player ? "reallife_space_what_yours" : "reallife_space_what"))
                };
                if (buy != SpaceRefusal.None && p.Owner != SpaceOwner.Player)
                {
                    take.Add(UiFactLine.Say(SpaceText.T(SpaceDirector.Why(buy)), UiRole.WarnText));
                }
                var actions = new List<UiActionModel>(2);
                if (p.Owner != SpaceOwner.Player)
                {
                    actions.Add(_runs.Button(SpaceText.T("reallife_space_btn_rent"), UiButtonKind.Secondary, UiIcon.None, UiAction.Option, () => Show(() => AskSpace("reallife_space_q_rent_title",
                        SpaceText.Fill("reallife_space_q_rent", "space", SpaceText.Space(slot), "name", p.Name, "amount", SpaceGame.Money(p.RentMonth),
                            "days", SpaceCatalogue.RentDays.ToString(Inv)), () => d.Rent(park), false)), rent != SpaceRefusal.None));
                    actions.Add(_runs.Button(SpaceText.T("reallife_space_btn_buy"), UiButtonKind.Primary, UiIcon.Check, UiAction.Alt, () => Show(() => AskSpace("reallife_space_q_buy_title",
                        SpaceText.Fill("reallife_space_q_buy", "space", SpaceText.Space(slot), "name", p.Name, "amount", SpaceGame.Money(p.Price), "owner", SpaceText.Owner(p)),
                        () => d.Buy(park), false)), buy != SpaceRefusal.None));
                }
                _m.Left = new[] { new UiFactCardModel { Title = SpaceText.T("reallife_space_sec_take"), Heading = true, Lines = take.ToArray(), Actions = actions.ToArray() } };
                _m.Right = new[]
                {
                    new UiFactCardModel
                    {
                        Title = SpaceText.T("reallife_space_sec_holders"), Heading = true, Lines = new[]
                        {
                            UiFactLine.Row(SpaceText.T("reallife_space_l_you"), player.ToString(Inv)),
                            UiFactLine.Row(SpaceText.T("reallife_space_l_rivals"), rivals.ToString(Inv)),
                            UiFactLine.Row(SpaceText.T("reallife_space_l_city"), city.ToString(Inv)),
                            UiFactLine.Row(SpaceText.T("reallife_space_l_free"), free.ToString(Inv)),
                            UiFactLine.Row(SpaceText.T("reallife_space_l_hours"), d.State.Hours(park).ToString(Inv))
                        }
                    }
                };
                var mine = new List<EstateRow>(SpaceCatalogue.HeldPerPark);
                for (int i = 0; i < d.State.Held.Count; i++)
                {
                    SpaceHolding h = d.State.Held[i];
                    if (!h.Players || h.Park != park)
                    {
                        continue;
                    }
                    int at = h.Slot;
                    long back = d.BackFor(h);
                    bool ending = h.Ending;
                    mine.Add(new EstateRow
                    {
                        Icon = UiIcon.Car, Title = SpaceText.Space(h.Slot), Sub = SpaceText.Tenure(h), Run = -1,
                        Button = SpaceText.T(h.Rented ? (h.Ending ? "reallife_space_btn_keep" : "reallife_space_btn_end") : "reallife_space_btn_sell"),
                        ButtonRun = h.Rented
                            ? _runs.Do(() => Show(() => d.SetEnding(park, at, !ending)))
                            : _runs.Do(() => Show(() => AskSpace("reallife_space_q_sell_title", SpaceText.Fill("reallife_space_q_sell", "space", SpaceText.Space(at), "name", p.Name,
                                "amount", SpaceGame.Money(back), "fee", (SpaceCatalogue.BackFee / 10f).ToString("0.#", Inv)), () => d.Sell(park, at), true))),
                        ButtonOff = !h.Rented && p.Owner == SpaceOwner.Player
                    });
                }
                _m.RelatedTitle = SpaceText.T("reallife_space_sec_mine");
                _m.RelatedCount = mine.Count > 0 ? mine.Count.ToString(Inv) : null;
                _m.Related = mine.ToArray();
                return true;
            }
        }
    }
}
