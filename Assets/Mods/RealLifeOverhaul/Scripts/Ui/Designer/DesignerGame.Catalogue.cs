using System;
using System.Collections.Generic;
using BigAmbitions.Items;
using Extensions;
using Helpers;
using TMPro;
using UI.InteriorDesigner;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The furniture catalogue on the kit, mirrored from the game's furniture panel and pressed through it. What the panel lists is what the game's own filters left
    /// (its categories, its favourites, its search, whatever other mods added to the items it offers), so the kit never filters again: a tab is the switch the
    /// game made for that category, the search is the game's search field, the tile's place is the call the game's own tile makes. Only two things are the kit's: the
    /// count of each category (worked out with the game's own tag test) and narrowing the list to what one unmet requirement of the business takes.
    /// </summary>
    internal static partial class DesignerGame
    {
        private static FurnitureCategoryToggle[] _cats = Array.Empty<FurnitureCategoryToggle>();
        private static readonly string[] CatNames = new string[UiDesignerCatalogue.TabsMost];
        private static readonly string[] CatCounts = new string[UiDesignerCatalogue.TabsMost];
        private static readonly Dictionary<string, int> CatOf = new Dictionary<string, int>(512, StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Named = new Dictionary<string, string>(512, StringComparer.Ordinal);
        private static readonly Dictionary<string, string> Priced = new Dictionary<string, string>(512, StringComparer.Ordinal);
        private static readonly List<IDItemUiTemplateData> Listed = new List<IDItemUiTemplateData>(256);
        private static readonly List<IDItemUiTemplateData> Everything = new List<IDItemUiTemplateData>(512);
        private static readonly List<IDItemUiTemplateData> View = new List<IDItemUiTemplateData>(256);
        private static readonly HashSet<string> Included = new HashSet<string>(StringComparer.Ordinal);
        private static readonly HashSet<string> Excluded = new HashSet<string>(StringComparer.Ordinal);
        private static bool _catBuilt;
        private static int _needFilter = -1;   // the requirement the catalogue is narrowed to, or -1
        private static string _chosenItem;

        private static void BeginCatalogue()
        {
            ForgetCatalogue();
        }

        private static void EndCatalogue()
        {
            _needFilter = -1;
        }

        private static void ForgetCatalogue()
        {
            _catBuilt = false;
            _needFilter = -1;
            _chosenItem = null;
            _cats = Array.Empty<FurnitureCategoryToggle>();
            CatOf.Clear();
            Named.Clear();
            Priced.Clear();
            Listed.Clear();
            Everything.Clear();
            View.Clear();
        }

        /// <summary>The item at a tile of the catalogue as last laid out, or null.</summary>
        public static Item ItemAt(int index) => index >= 0 && index < View.Count ? ItemsGetter.GetByName(View[index].itemName) : null;

        /// <summary>The name of the item at a tile of the catalogue as last laid out, or null.</summary>
        public static string ItemNameAt(int index) => index >= 0 && index < View.Count ? View[index].itemName : null;

        // The game's panel builds its list as the Furniture tool first opens; until then there is nothing to count.
        private static void BuildTabs(FurnitureActionPanelUi panel)
        {
            var names = BuyKits.Get<List<string>>(GameContract.DesignerAvailable, panel);
            if (names == null || names.Count == 0)
            {
                return;
            }
            _cats = panel.GetComponentsInChildren<FurnitureCategoryToggle>(true);
            Everything.Clear();
            for (int i = 0; i < names.Count; i++)
            {
                Everything.Add(new IDItemUiTemplateData(names[i], 0f, null));
            }
            CatOf.Clear();
            var inside = new List<IDItemUiTemplateData>(Everything.Count);
            int most = Math.Min(_cats.Length, UiDesignerCatalogue.TabsMost - 1);
            for (int k = 0; k < most; k++)
            {
                Included.Clear();
                Excluded.Clear();
                if (_cats[k].includedTags != null)
                {
                    Included.UnionWith(_cats[k].includedTags);
                }
                if (_cats[k].excludedTags != null)
                {
                    Excluded.UnionWith(_cats[k].excludedTags);
                }
                inside.Clear();
                FurnitureTagMatcher.FilterItemsByTagSets(Everything, Included, Excluded, ref inside);
                CatCounts[k + 1] = UiText.Whole(inside.Count);
                for (int i = 0; i < inside.Count; i++)
                {
                    if (!CatOf.ContainsKey(inside[i].itemName))
                    {
                        CatOf[inside[i].itemName] = k + 1;
                    }
                }
            }
            CatCounts[0] = UiText.Whole(Everything.Count);
            _catBuilt = true;
        }

        private static Toggle SwitchOf(int tab)
        {
            if (tab == 0)
            {
                return BuyKits.Get<Toggle>(GameContract.DesignerAllToggle, DesignerPanes.Furniture);
            }
            return tab - 1 < _cats.Length ? BuyKits.Get<Toggle>(GameContract.DesignerCategoryToggle, _cats[tab - 1]) : null;
        }

        private static TMP_InputField SearchField()
        {
            var field = BuyKits.Get<global::UI.Components.InputField>(GameContract.DesignerSearch, DesignerPanes.Furniture);
            return field != null ? field.tmpInputField : null;
        }

        /// <summary>Fills the catalogue from the game's furniture panel; false while the game has not built it.</summary>
        private static bool FillCatalogue(UiDesignerCatalogue c)
        {
            FurnitureActionPanelUi panel = DesignerPanes.Furniture;
            var scroller = panel != null ? BuyKits.Get<IDItemTemplateScrollingController>(GameContract.DesignerScroller, panel) : null;
            if (scroller == null)
            {
                return false;
            }
            if (!_catBuilt)
            {
                BuildTabs(panel);
            }
            c.NeededLabel = UiText.T("reallife_ui_designer_needed");
            c.SearchPlaceholder = UiText.T("common_search");
            c.FavouritesLabel = UiText.T("interior_designer_furniture_panel_category_favorites");
            c.ColoursLabel = UiText.T("reallife_ui_designer_colours");
            c.PlaceLabel = UiText.T("reallife_ui_designer_place");
            c.FavouriteLabel = UiText.T("reallife_ui_designer_favourite");
            c.ClearLabel = UiText.T("reallife_ui_designer_clear");
            c.TabCount = 0;
            Toggle fav = BuyKits.Get<Toggle>(GameContract.DesignerFavToggle, panel);
            c.FavouritesOnly = UiWidgets.On(fav);
            if (_catBuilt)
            {
                Toggle all = SwitchOf(0);
                string first = UiWidgets.Label(all);
                CatNames[0] = string.IsNullOrEmpty(first) ? UiText.T("interior_designer_furniture_panel_category_allitems") : first;
                int most = Math.Min(_cats.Length, UiDesignerCatalogue.TabsMost - 1);
                for (int k = 0; k < most; k++)
                {
                    string words = UiWidgets.Label(_cats[k]);   // the category's own label, wherever the game put it under its object
                    if (string.IsNullOrEmpty(words))
                    {
                        words = UiWidgets.Label(SwitchOf(k + 1));
                    }
                    CatNames[k + 1] = string.IsNullOrEmpty(words) ? _cats[k].name : words;
                }
                for (int k = 0; k <= most; k++)
                {
                    c.Tabs[k] = CatNames[k];
                    c.TabCounts[k] = CatCounts[k];
                }
                c.TabCount = most + 1;
                int active = Array.IndexOf(_cats, FurnitureCategoryToggle.CurrentActiveToggle);
                c.Tab = c.FavouritesOnly ? -1 : active >= 0 && active < most ? active + 1 : 0;   // the favourites stand in the place of a category
            }
            TMP_InputField search = SearchField();
            c.Search = search != null ? search.text ?? string.Empty : string.Empty;
            Listed.Clear();
            List<IDItemTemplatesModel> rows = scroller.data;
            for (int r = 0; rows != null && r < rows.Count; r++)
            {
                Listed.AddRange(rows[r].itemTemplates);
            }
            View.Clear();
            if (_needFilter >= Rows.Count || (_needFilter >= 0 && Rows[_needFilter].Met))
            {
                _needFilter = -1;   // the need is met (the player placed what it wanted) or gone: the catalogue is whole again
            }
            bool narrowed = _needFilter >= 0;
            for (int i = 0; i < Listed.Count; i++)
            {
                Item item = ItemsGetter.GetByName(Listed[i].itemName);
                if (item != null && (!narrowed || Fills(_needFilter, item)))
                {
                    View.Add(Listed[i]);
                }
            }
            c.Filtered = narrowed;
            c.FilterNote = narrowed ? UiText.Fill("reallife_ui_designer_showing", "need", Rows[_needFilter].Title ?? string.Empty) : null;
            FillCards(c);
            c.NoneTitle = UiText.T("reallife_ui_designer_none_title");
            bool noFavourites = c.FavouritesOnly && PlayerSettingsHelper.GetIDFurnitureFavorites().Count == 0;
            c.NoneBody = UiText.T(noFavourites ? "interior_designer_no_favorites" : narrowed ? "reallife_ui_designer_none_needed" : "reallife_ui_designer_none_body");
            FillDetail(c);
            return true;
        }

        private static void FillCards(UiDesignerCatalogue c)
        {
            int n = View.Count;
            if (c.Cards.Length < n)
            {
                c.Cards = new UiDesignerCard[Math.Max(n, c.Cards.Length * 2)];
            }
            for (int i = 0; i < n; i++)
            {
                string name = View[i].itemName;
                Item item = ItemsGetter.GetByName(name);
                c.Cards[i] = new UiDesignerCard
                {
                    Index = i, Name = NameOf(name), Price = PriceOf(name, item), Kind = KindOf(item), Picture = ItemPictureKey(name), Needed = Needed(item),
                    Favourite = PlayerSettingsHelper.IsFurnitureFavorite(name)
                };
            }
            c.CardCount = n;
            int chosen = -1;
            for (int i = 0; i < n && _chosenItem != null; i++)
            {
                if (string.Equals(View[i].itemName, _chosenItem, StringComparison.Ordinal))
                {
                    chosen = i;
                    break;
                }
            }
            if (chosen < 0 && n > 0)
            {
                chosen = 0;
                _chosenItem = View[0].itemName;
            }
            c.Chosen = chosen;
        }

        private static string NameOf(string itemName)
        {
            if (!Named.TryGetValue(itemName, out string words))
            {
                words = Say(itemName);
                Named[itemName] = words;
            }
            return words;
        }

        private static string PriceOf(string itemName, Item item)
        {
            if (!Priced.TryGetValue(itemName, out string words))
            {
                words = item != null ? item.DefaultMarketPrice.ToShortCurrencyFormat() : string.Empty;
                Priced[itemName] = words;
            }
            return words;
        }

        // What the piece is for the room: what it holds, or that it hangs from the ceiling or the wall.
        private static string KindOf(Item item)
        {
            if (item == null)
            {
                return null;
            }
            if (item.cargoCapacity > 0)
            {
                return UiText.Fill("reallife_ui_designer_holds", "n", UiText.Whole(item.cargoCapacity));
            }
            return item.snapToCeiling ? UiText.T("reallife_ui_designer_ceiling") : item.wallMounted ? UiText.T("reallife_ui_designer_wall") : null;
        }

        /// <summary>A picture key for an item's icon, answered by <see cref="Picture"/>.</summary>
        internal static string ItemPictureKey(string itemName) => ItemPicture + itemName;

        private static void FillDetail(UiDesignerCatalogue c)
        {
            c.DetailCount = 0;
            c.SwatchCount = 0;
            if (c.Chosen < 0 || c.Chosen >= c.CardCount)
            {
                return;
            }
            string name = View[c.Chosen].itemName;
            Item item = ItemsGetter.GetByName(name);
            if (item == null)
            {
                return;
            }
            c.DetailName = NameOf(name);
            string kind = KindOf(item);
            string category = CatOf.TryGetValue(name, out int at) && at < c.TabCount ? CatNames[at] : null;
            c.DetailKind = category != null && kind != null ? category + ", " + kind : category ?? kind;
            Row(c, UiText.T("common_price"), PriceOf(name, item), UiRole.Text);
            if (item.addedCustomersPerHour > 0)
            {
                Row(c, UiText.T("bizman_customers_capacity"), "+" + UiText.Whole(item.addedCustomersPerHour), UiRole.OkText);
            }
            for (int k = 0; k < Rows.Count && c.DetailCount < UiDesignerCatalogue.RowsMost; k++)
            {
                if (Fills(k, item))
                {
                    string count = Rows[k].Counted && Rows[k].Rule.Need > 0 ? DesignerRules.CountWords(true, false, Rows[k].Have, Rows[k].Rule.Need, UiText.T("reallife_ui_designer_count_of")) : null;
                    Row(c, UiText.T("reallife_ui_designer_fills"), count != null ? Rows[k].Title + ", " + count : Rows[k].Title, UiRole.WarnText);
                    break;
                }
            }
            Color[] colours = item.customizationColors;
            for (int k = 0; colours != null && k < colours.Length && k < c.Swatches.Length; k++)
            {
                Color32 rgb = colours[k];
                c.Swatches[k] = UiDraw.SwatchKey((rgb.r << 16) | (rgb.g << 8) | rgb.b);
                c.SwatchCount = k + 1;
            }
        }

        private static void Row(UiDesignerCatalogue c, string label, string value, UiRole ink)
        {
            if (c.DetailCount >= UiDesignerCatalogue.RowsMost)
            {
                return;
            }
            c.DetailLabels[c.DetailCount] = label;
            c.DetailValues[c.DetailCount] = value;
            c.DetailInks[c.DetailCount] = ink;
            c.DetailCount++;
        }

        // ------------------------------------------------------------------ the catalogue's presses

        /// <summary>A category (or All items at 0) taken as the game's own switch for it is: the filter it runs is the game's.</summary>
        public static bool Tab(int tab)
        {
            Toggle t = SwitchOf(tab);
            _needFilter = -1;
            return UiWidgets.Set(t, true);
        }

        /// <summary>The favourites' switch: on shows the favourites, off goes back to all items, each through the game's own switches.</summary>
        public static bool Favourites(bool on)
        {
            _needFilter = -1;
            Toggle fav = BuyKits.Get<Toggle>(GameContract.DesignerFavToggle, DesignerPanes.Furniture);
            if (on)
            {
                return UiWidgets.Set(fav, true);
            }
            UiWidgets.Set(fav, false);
            return UiWidgets.Set(SwitchOf(0), true);
        }

        /// <summary>A tile chosen: its detail shows.</summary>
        public static bool Choose(int index)
        {
            string name = ItemNameAt(index);
            if (name == null)
            {
                return false;
            }
            _chosenItem = name;
            return true;
        }

        /// <summary>The star on a tile, through the game's own favourites.</summary>
        public static bool Star(int index)
        {
            string name = ItemNameAt(index);
            if (name == null)
            {
                return false;
            }
            PlayerSettingsHelper.ToggleFurnitureFavorite(name);
            return true;
        }

        /// <summary>The chosen piece taken in hand to place, as the game's own tile does it.</summary>
        public static bool PlaceItem(int index)
        {
            if (index < 0 || index >= View.Count)
            {
                return false;
            }
            IDItemUiTemplateData data = View[index];
            if (data.onClickItemName == null)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                data.onClickItemName(null, data.itemName);
            }
            return true;
        }

        /// <summary>Typed into the game's search field, which filters as it does when the player types there.</summary>
        public static bool SearchFor(string text)
        {
            return UiWidgets.Type(SearchField(), text ?? string.Empty, false);
        }

        /// <summary>The catalogue narrowed to what a requirement still takes; the game's list is first put back to all items with no search.</summary>
        public static bool Narrow(int line)
        {
            if (line < 0 || line >= Rows.Count || !Rows[line].Items)
            {
                return false;
            }
            if (ToolInHand() != (int)UiDesignerTool.Furniture)
            {
                Tool((int)UiDesignerTool.Furniture);
            }
            _needFilter = line;
            UiWidgets.Type(SearchField(), string.Empty, false);
            UiWidgets.Set(BuyKits.Get<Toggle>(GameContract.DesignerFavToggle, DesignerPanes.Furniture), false);
            UiWidgets.Set(SwitchOf(0), true);
            return true;
        }

        /// <summary>The narrowing taken off.</summary>
        public static void Unnarrow() => _needFilter = -1;

        /// <summary>The line the catalogue is narrowed to, or -1.</summary>
        public static int NarrowedLine => _needFilter;
    }
}
