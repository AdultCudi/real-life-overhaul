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
    /// hotels for sale, its page (<see cref="SpacePages"/>, the same page the checks and previews lay out) sells or lets a space and
    /// lists the player's spaces there with their way out, and every space held is a row of the rent roll. Every press goes to
    /// <see cref="SpaceDirector"/>'s own call, asked first in the game's question box where money moves.
    /// </summary>
    public sealed partial class EstateLive
    {
        private sealed partial class Page : ISpaceActions
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
                IReadOnlyList<SpacePark> parks = d != null ? d.Open() : null;   // read again as the page is built
                for (int i = 0; parks != null && i < parks.Count; i++)
                {
                    locate.Add(-1);
                    rows.Add(SpacePages.MarketRow(SpaceText.Words, d.State, parks[i], _day, SpaceGame.Money));
                    keys.Add(SpacePick + parks[i].Number.ToString(Inv));
                }
            }

            // The rent roll's rows: a row a space the player holds, which opens its car park's page.
            private void SpaceHeld(List<UiDataRow> rows, List<string> keys)
            {
                SpaceDirector d = SpacesNow;
                if (d != null)
                {
                    d.Open();
                }
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
                if (d != null && pick != null)
                {
                    d.Open();
                }
                SpacePark p = pick != null && d != null && int.TryParse(pick, out int number) ? d.Park(number) : null;
                if (p == null)
                {
                    return false;
                }
                SpaceRefusal buy = d.MayTake(p.Number, false, out int _), rent = d.MayTake(p.Number, true, out int slot);
                SpacePages.Fill(_m, _runs, SpaceText.Words, d.State, p, _day, buy, rent, slot, SpaceGame.Money, FinanceText.Day, this);
                return true;
            }

            void ISpaceActions.Buy(int park, int slot)
            {
                SpaceDirector d = SpacesNow;
                SpacePark p = d != null ? d.Park(park) : null;
                if (p != null)
                {
                    long shown = p.Price;   // what the question box asks about is what is charged, or nothing is
                    Show(() => AskSpace("reallife_space_q_buy_title", SpaceText.Fill("reallife_space_q_buy", "space", SpaceText.Space(slot), "name", p.Name,
                        "amount", SpaceGame.Money(shown), "owner", SpaceText.Owner(p)), () => d.Buy(park, shown, slot), false));
                }
            }

            void ISpaceActions.Rent(int park, int slot)
            {
                SpaceDirector d = SpacesNow;
                SpacePark p = d != null ? d.Park(park) : null;
                if (p != null)
                {
                    long shown = p.RentMonth;
                    Show(() => AskSpace("reallife_space_q_rent_title", SpaceText.Fill("reallife_space_q_rent", "space", SpaceText.Space(slot), "name", p.Name,
                        "amount", SpaceGame.Money(shown), "days", SpaceCatalogue.RentDays.ToString(Inv)), () => d.Rent(park, shown, slot), false));
                }
            }

            void ISpaceActions.Sell(int park, int slot, long back)
            {
                SpaceDirector d = SpacesNow;
                SpacePark p = d != null ? d.Park(park) : null;
                if (p != null)
                {
                    Show(() => AskSpace("reallife_space_q_sell_title", SpaceText.Fill("reallife_space_q_sell", "space", SpaceText.Space(slot), "name", p.Name,
                        "amount", SpaceGame.Money(back), "fee", (SpaceCatalogue.BackFee / 10f).ToString("0.#", Inv)), () => d.Sell(park, slot, back), true));
                }
            }

            void ISpaceActions.SetEnding(int park, int slot, bool ending)
            {
                SpaceDirector d = SpacesNow;
                if (d != null)
                {
                    Show(() => d.SetEnding(park, slot, ending));
                }
            }
        }
    }
}
