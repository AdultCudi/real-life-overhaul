using RealLifeOverhaul.GameUi;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// Reads the Garage's list window (the custom shop, the workshop, "My vehicles", a stand's rack) into the garage strip's
    /// model: the headline and its lines as the owner wrote them, each entry and note with its own words, price, state and
    /// button, a paint's colour from the shop. The owner fills the list as it always did and keeps its own rules; this only
    /// reads it, so a press on a card is the list's own pick. Cost: a card a row, once a change of the list.
    /// </summary>
    internal static class GarageShopLive
    {
        /// <summary>Whose list is in the window now.</summary>
        public static UiGarageKind Kind()
        {
            switch (GarageList.Kind)
            {
                case GarageListKind.Shop: return UiGarageKind.Shop;
                case GarageListKind.Vehicles: return UiGarageKind.Vehicles;
                case GarageListKind.Stand: return UiGarageKind.Stand;
                default: return UiGarageKind.Workshop;
            }
        }

        /// <summary>A key of the shop's scroll: it keeps its place within a page and goes back to the top on another.</summary>
        public static int PageKey()
        {
            GarageShop shop = GarageList.Shop;
            return (int)Kind() * 16 + (shop != null ? shop.PageIndex : 0);
        }

        /// <summary>Fills the model and the ids of its cards (the list's id of each) from the list; the scroll and the reveal are left as they stood.</summary>
        public static void Read(TravelList list, UiGarageStripModel m, ref int[] ids)
        {
            UiGarageKind kind = Kind();
            GarageShop shop = GarageList.Shop;
            bool main = shop != null && shop.OnMain;
            m.Title = WorldHost.Plain(list.TitleWords) ?? string.Empty;
            m.Icon = GarageCardRules.IconFor(kind);
            if (m.Info.Length < GarageCardRules.MostInfo)
            {
                m.Info = new string[GarageCardRules.MostInfo];
            }
            m.InfoCount = GarageCardRules.Lines(WorldHost.Plain(list.InfoWords), m.Info);
            m.Leave = UiText.T("common_cancel");
            int n = list.Count;
            if (m.Cards.Length != n)
            {
                m.Cards = new UiGarageCard[n];
                ids = new int[n];
            }
            m.CardCount = n;
            for (int i = 0; i < n; i++)
            {
                list.Read(i, out int id, out string name, out string detail, out string buttonKey, out bool enabled, out bool note);
                ids[i] = id;
                var row = new UiGarageRow
                {
                    Id = id, Name = WorldHost.Plain(name), Detail = WorldHost.Plain(detail), ButtonKey = buttonKey, Enabled = enabled, Note = note,
                    Colour = shop != null && !note ? shop.SwatchOf(id) : -1,
                };
                m.Cards[i] = GarageCardRules.Card(row, kind, main, UiText.T);
            }
            m.Prompts = new[]
            {
                new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") },
                new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_select") },
                new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_prompt_back") },
            };
            m.PromptCount = m.Prompts.Length;
        }
    }
}
