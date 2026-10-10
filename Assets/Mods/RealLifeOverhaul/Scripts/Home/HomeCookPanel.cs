using System.Collections.Generic;
using System.Text;
using Localizor;
using RealLifeOverhaul.Meta;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The cooking panel: what can be made right now from what is in this home's fridges, and nothing else.
    /// One row per dish (a coloured tile, its name, how many portions, how long, what a portion fills, and
    /// exactly which items it takes), the leftovers put by, then the dishes that are one unit of food short,
    /// greyed, saying what is missing. Three buttons choose the size (normal, double, family size); a dish is
    /// shown in the largest of those the fridges allow. Choosing a dish closes the panel and hands the plan to
    /// the director, which walks the player to the cooker.
    ///
    /// One of the mod's own panels on Meta's canvas (MetaUi), which sees to the rest: the cursor, Esc, the
    /// arrow keys for the list, the game's shortcuts left alone while it is open. Built on first use; its rows
    /// are made when it opens and when the size is changed, never per frame; closed, it does nothing at all.
    /// </summary>
    public sealed class HomeCookPanel : MetaPanel
    {
        private static readonly Color Greyed = new Color(0.55f, 0.58f, 0.63f, 1f);
        private static readonly Color RowPlate = new Color(0.10f, 0.12f, 0.16f, 1f);
        private static readonly Color RowGreyed = new Color(0.08f, 0.09f, 0.11f, 1f);
        private const float RowHeight = 118f;

        private static HomeCookPanel _panel;

        private readonly MetaUi _ui;
        private readonly RectTransform _content;
        private readonly TextMeshProUGUI _status;
        private readonly Image[] _sizeFaces = new Image[3];
        private readonly List<PantryItem> _pantry = new List<PantryItem>(16);
        private readonly StringBuilder _text = new StringBuilder(160);
        private HomeDirector _host;
        private bool _cooker;
        private int _fridges;
        private int _size;

        /// <summary>True while the panel is on screen (the mod's cameras leave the cursor alone: Camera/ThirdPersonCamera.IsUiOpen).</summary>
        public static bool IsOpen => _panel != null && MetaUi.Exists && MetaUi.Instance.IsOpen(_panel);

        /// <summary>
        /// Opens the panel for the home the player is in (HomePlace has just looked it over). Returns what it
        /// found, for the log: fridges, units by kind, dishes possible.
        /// </summary>
        public static string Open(HomeDirector host)
        {
            MetaUi ui = MetaUi.Instance;
            if (_panel == null || _panel.Root == null)
            {
                _panel = new HomeCookPanel(ui);
            }
            _panel._host = host;
            _panel._fridges = HomePlace.Fridges.Count;
            _panel._cooker = HomePlace.Cookers.Count > 0;
            HomePlace.ReadPantry(_panel._pantry);
            string found = _panel.Fill();
            ui.Show(_panel);
            return found;
        }

        public static void CloseIfOpen()
        {
            if (IsOpen)
            {
                MetaUi.Instance.Close(_panel);
            }
        }

        private HomeCookPanel(MetaUi ui)
        {
            _ui = ui;
            Root = ui.Frame("Cooking", new Vector2(1120f, 800f), out RectTransform window);
            ui.TitleBar(window, JobText.T("reallife_home_cookpanel_title"), out _);

            _status = ui.Text("Status", window, 19f, MetaUi.Soft, TextAlignmentOptions.MidlineLeft, false);
            Place(_status.rectTransform, 22f, 66f, 560f, 40f);

            // The three sizes, at the right under the title.
            string[] sizes = { "reallife_home_cookpanel_size_1", "reallife_home_cookpanel_size_2", "reallife_home_cookpanel_size_4" };
            for (int i = 0; i < sizes.Length; i++)
            {
                int size = i;
                Button button = ui.Button("Size " + i, window, JobText.T(sizes[i]), () => Choose(size), 40f, 18f);   // the texts name the number keys ({key1} to {key3})
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 1f);
                rect.pivot = new Vector2(1f, 1f);
                rect.sizeDelta = new Vector2(170f, 40f);
                rect.anchoredPosition = new Vector2(-18f - (sizes.Length - 1 - i) * 178f, -66f);
                _sizeFaces[i] = button.GetComponent<Image>();
            }

            KeyScroll = ui.Scroll("Dishes", window, out _content, 8f);
            MetaUi.Stretch((RectTransform)KeyScroll.transform, 18f, 70f, 18f, 116f);

            TextMeshProUGUI hint = ui.Text("Hint", window, 17f, MetaUi.Soft, TextAlignmentOptions.MidlineLeft, true);
            hint.rectTransform.anchorMin = new Vector2(0f, 0f);
            hint.rectTransform.anchorMax = new Vector2(1f, 0f);
            hint.rectTransform.pivot = new Vector2(0f, 0f);
            hint.rectTransform.offsetMin = new Vector2(22f, 12f);
            hint.rectTransform.offsetMax = new Vector2(-220f, 62f);
            hint.text = JobText.T("reallife_home_cookpanel_hint");

            Button close = ui.Button("Close", window, JobText.T("reallife_meta_close"), () => MetaUi.Instance.Close(this), 46f, 20f);
            var closeRect = (RectTransform)close.transform;
            closeRect.anchorMin = closeRect.anchorMax = new Vector2(1f, 0f);
            closeRect.pivot = new Vector2(1f, 0f);
            closeRect.sizeDelta = new Vector2(180f, 46f);
            closeRect.anchoredPosition = new Vector2(-18f, 14f);
        }

        /// <summary>The number keys choose the size, as the buttons do.</summary>
        public override void Tick()
        {
            int choice = JobKeys.ChoiceDown();
            if (choice >= 0 && choice < HomeBalance.Batches.Length && choice != _size)
            {
                Choose(choice);
            }
        }

        private void Choose(int size)
        {
            _size = size;
            Fill();
        }

        // A rect placed from its parent's top edge: so far from the left, the top and the right, so high.
        private static void Place(RectTransform rect, float left, float top, float right, float height)
        {
            rect.anchorMin = new Vector2(0f, 1f);
            rect.anchorMax = new Vector2(1f, 1f);
            rect.pivot = new Vector2(0f, 1f);
            rect.offsetMin = new Vector2(left, -top - height);
            rect.offsetMax = new Vector2(-right, -top);
        }

        // Makes the rows again from the fridges as they were read when the panel opened. Returns a line for the log.
        private string Fill()
        {
            for (int i = _content.childCount - 1; i >= 0; i--)
            {
                GameObject old = _content.GetChild(i).gameObject;
                old.SetActive(false);
                Object.Destroy(old);
            }
            for (int i = 0; i < _sizeFaces.Length; i++)
            {
                _sizeFaces[i].color = i == _size ? MetaUi.Accent : MetaUi.Plate;
            }

            int units = 0;
            for (int i = 0; i < _pantry.Count; i++)
            {
                units += _pantry[i].Amount;
            }
            _status.text = JobText.T("reallife_home_cookpanel_status", "fridges", _fridges.ToString(), "units", units.ToString());

            int possible = 0;
            int needCooker = 0;
            int oneShort = 0;

            CookPlan reheat = Cooking.Reheat(HomeState.Leftovers, HomeState.LeftoverDish, HomeState.LeftoverFill);
            if (reheat != null)
            {
                string title = JobText.T("reallife_home_cookpanel_leftovers", "meal", JobText.T(reheat.NameKey), "count", HomeState.Leftovers.ToString());
                string detail = JobText.T("reallife_home_cookpanel_reheat_line", "minutes", reheat.Minutes.ToString(), "fill", reheat.Fill.ToString());
                if (_cooker)
                {
                    Row(0x8A8F98, title, detail, false, "reallife_home_cookpanel_reheat", reheat);
                }
                else
                {
                    Row(0x8A8F98, title, detail + "\n" + JobText.T("reallife_home_cookpanel_needs_cooker"), true, null, null);
                }
            }

            Dish[] dishes = HomeBalance.Dishes;
            bool header = false;
            for (int d = 0; d < dishes.Length; d++)
            {
                Dish dish = dishes[d];
                CookPlan plan = null;
                int size = _size;
                for (; size >= 0 && plan == null; size--)
                {
                    plan = Cooking.Plan(dish, size, _pantry);
                }
                if (plan == null)
                {
                    continue;
                }
                if (dish.Cooker && !_cooker)
                {
                    needCooker++;
                    continue;
                }
                possible++;
                if (!header)
                {
                    header = true;
                    Heading("reallife_home_cookpanel_now");
                }
                _text.Length = 0;
                _text.Append(JobText.T("reallife_home_cookpanel_line", "portions", plan.Portions.ToString(), "minutes", plan.Minutes.ToString())
                    .Replace("{fill}", plan.Fill.ToString()));
                if (plan.Size < _size)
                {
                    _text.Append(' ').Append(JobText.T("reallife_home_cookpanel_less"));
                }
                _text.Append('\n').Append(JobText.T("reallife_home_cookpanel_uses", "items", Items(plan)));
                Row(dish.Tile, JobText.T(dish.Name), _text.ToString(), false, dish.Cooker ? "reallife_home_cookpanel_cook" : "reallife_home_cookpanel_make", plan);
            }

            if (_fridges == 0)
            {
                Note("reallife_home_cookpanel_no_fridge");
            }
            else if (units == 0)
            {
                Note("reallife_home_cookpanel_empty");
            }
            else if (possible == 0 && needCooker == 0)
            {
                Note("reallife_home_cookpanel_nothing");
            }

            // One unit short of a dish that takes two or more: a dish of one item that is not there is not "nearly".
            header = false;
            for (int d = 0; d < dishes.Length; d++)
            {
                Dish dish = dishes[d];
                if (dish.Needs.Length < 2 || (dish.Cooker && !_cooker) || Cooking.Short(dish, _pantry, out FoodKind missing) != 1)
                {
                    continue;
                }
                oneShort++;
                if (!header)
                {
                    header = true;
                    Heading("reallife_home_cookpanel_short");
                }
                Row(dish.Tile, JobText.T(dish.Name), JobText.T("reallife_home_cookpanel_missing", "kind", JobText.T(KindKey(missing))), true, null, null);
            }

            if (!_cooker)
            {
                Note(needCooker > 0 ? "reallife_home_cookpanel_no_cooker_more" : "reallife_home_cookpanel_no_cooker");
            }
            KeyScroll.verticalNormalizedPosition = 1f;

            _text.Length = 0;
            _text.Append(_fridges).Append(" fridge(s), ").Append(HomePlace.Cookers.Count).Append(" cooker(s); food by kind:");
            for (FoodKind kind = FoodKind.Meat; kind <= FoodKind.Drink; kind++)
            {
                int count = Cooking.Count(_pantry, kind);
                if (count > 0)
                {
                    _text.Append(' ').Append(kind).Append(' ').Append(count);
                }
            }
            if (units == 0)
            {
                _text.Append(" none");
            }
            _text.Append("; ").Append(possible).Append(" dish(es) possible, ").Append(oneShort).Append(" one short, ").Append(needCooker)
                .Append(" wanting a cooker, leftovers ").Append(HomeState.Leftovers).Append('.');
            return _text.ToString();
        }

        // "Ground Beef (1), Bag of Carrots (2)": the game's own names for its items.
        private static string Items(CookPlan plan)
        {
            var text = new StringBuilder(80);
            for (int i = 0; i < plan.Uses.Count; i++)
            {
                if (i > 0)
                {
                    text.Append(", ");
                }
                text.Append(plan.Uses[i].Id.GetLocalization()).Append(" (").Append(plan.Uses[i].Amount).Append(')');
            }
            return text.ToString();
        }

        private static string KindKey(FoodKind kind)
        {
            switch (kind)
            {
                case FoodKind.Meat: return "reallife_home_food_meat";
                case FoodKind.Vegetable: return "reallife_home_food_vegetable";
                case FoodKind.Dairy: return "reallife_home_food_dairy";
                case FoodKind.Grain: return "reallife_home_food_grain";
                case FoodKind.Fruit: return "reallife_home_food_fruit";
                case FoodKind.Fresh: return "reallife_home_food_fresh";
                case FoodKind.Frozen: return "reallife_home_food_frozen";
                default: return "reallife_home_food_ready";
            }
        }

        private void Heading(string key)
        {
            TextMeshProUGUI text = _ui.Text("Heading", _content, 20f, MetaUi.Accent, TextAlignmentOptions.MidlineLeft, false);
            text.fontStyle = FontStyles.Bold;
            text.text = JobText.T(key);
            var layout = text.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = 34f;
            layout.preferredHeight = 34f;
        }

        private void Note(string key)
        {
            TextMeshProUGUI text = _ui.Text("Note", _content, 20f, MetaUi.Ink, TextAlignmentOptions.TopLeft, true);
            text.text = JobText.T(key);
        }

        // One dish: the tile, the name, two lines about it, and its button if it can be made.
        private void Row(int tile, string title, string detail, bool greyed, string buttonKey, CookPlan plan)
        {
            RectTransform row = _ui.Box("Dish", _content, greyed ? RowGreyed : RowPlate, false);
            var layout = row.gameObject.AddComponent<LayoutElement>();
            layout.minHeight = RowHeight;
            layout.preferredHeight = RowHeight;

            var colour = new Color(((tile >> 16) & 255) / 255f, ((tile >> 8) & 255) / 255f, (tile & 255) / 255f, greyed ? 0.35f : 1f);
            RectTransform square = _ui.Box("Tile", row, colour, false);
            square.anchorMin = square.anchorMax = new Vector2(0f, 0.5f);
            square.pivot = new Vector2(0f, 0.5f);
            square.sizeDelta = new Vector2(64f, 64f);
            square.anchoredPosition = new Vector2(14f, 0f);
            TextMeshProUGUI letter = _ui.Text("Letter", square, 34f, greyed ? Greyed : MetaUi.Dark, TextAlignmentOptions.Center, false);
            MetaUi.Stretch(letter.rectTransform, 0f, 0f, 0f, 0f);
            letter.fontStyle = FontStyles.Bold;
            letter.text = title.Length > 0 ? title.Substring(0, 1).ToUpperInvariant() : string.Empty;

            float right = buttonKey != null ? 196f : 16f;
            TextMeshProUGUI name = _ui.Text("Name", row, 22f, greyed ? Greyed : MetaUi.Ink, TextAlignmentOptions.TopLeft, false);
            name.fontStyle = FontStyles.Bold;
            Place(name.rectTransform, 92f, 8f, right, 28f);
            name.text = title;

            TextMeshProUGUI about = _ui.Text("About", row, 17f, greyed ? Greyed : MetaUi.Soft, TextAlignmentOptions.TopLeft, true);
            Place(about.rectTransform, 92f, 38f, right, 74f);
            about.overflowMode = TextOverflowModes.Truncate;
            about.text = detail;

            if (buttonKey != null && plan != null)
            {
                Button button = _ui.Button("Cook", row, JobText.T(buttonKey), () => Pick(plan), 48f, 20f);
                var rect = (RectTransform)button.transform;
                rect.anchorMin = rect.anchorMax = new Vector2(1f, 0.5f);
                rect.pivot = new Vector2(1f, 0.5f);
                rect.sizeDelta = new Vector2(164f, 48f);
                rect.anchoredPosition = new Vector2(-16f, 0f);
            }
        }

        private void Pick(CookPlan plan)
        {
            HomeDirector host = _host;
            MetaUi.Instance.Close(this);
            if (host != null)
            {
                host.Cook(plan);
            }
        }
    }
}
