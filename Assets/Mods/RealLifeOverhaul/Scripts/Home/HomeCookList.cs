using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The cooking list in the game's own list window (<see cref="TravelList"/>): the size, the leftovers, the dishes the
    /// fridges allow and those one unit short. Filled when it opens or the size changes; closed, it costs nothing.
    /// </summary>
    public static class HomeCookList
    {
        private const int SizeRow = -1;
        private const string ButtonCook = "reallife_home_cookpanel_cook";
        private const string Chosen = "a dish was chosen";
        private const string ButtonMake = "reallife_home_cookpanel_make";
        private const string ButtonReheat = "reallife_home_cookpanel_reheat";
        private const string ButtonChange = "reallife_home_cooklist_change";

        private static readonly string[] SizeNames = { "reallife_home_cooklist_size_1", "reallife_home_cooklist_size_2", "reallife_home_cooklist_size_4" };

        public static IModLogger Logger;

        private static readonly List<PantryItem> Pantry = new List<PantryItem>(16);
        private static readonly List<CookPlan> Plans = new List<CookPlan>(16);
        private static readonly StringBuilder Text = new StringBuilder(240);
        private static readonly Action<int> OnPick = Picked;
        private static readonly Action OnClosed = Closed;
        private static TravelList _list;
        private static HomeDirector _host;
        private static string _closing;
        private static bool _failed;
        private static bool _dark;
        private static bool _cooker;
        private static int _fridges;
        private static int _size;

        public static bool IsOpen => _list != null && _list.IsOpen;

        /// <summary>Opens the list. Returns a line for the log, or null if the game's window could not be copied (the caller then cooks the first dish).</summary>
        public static string Open(HomeDirector host)
        {
            if (_failed)
            {
                return null;
            }
            try
            {
                float began = Time.realtimeSinceStartup;
                if (_list == null || !_list.Alive)
                {
                    _list = TravelList.Make("RealLifeOverhaul Cooking");
                    if (_list == null)
                    {
                        _failed = true;
                        return null;
                    }
                    RectTransform window = _list.Window;
                    window.sizeDelta = new Vector2(1100f, window.sizeDelta.y);   // an entry says what a dish takes: wider than a station's name
                    _list.EntryShape(150f, true);
                }
                _host = host;
                _fridges = HomePlace.Fridges.Count;
                _dark = HomeTidy.Dark(HomeTidy.Home());   // a home whose power is cut has no cooker to cook on (Utilities)
                _cooker = HomePlace.Cookers.Count > 0 && !_dark;
                HomePlace.ReadPantry(Pantry);
                string found = Fill();
                _closing = null;
                _list.Open(OnPick, OnClosed);
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (cooking list): the game's list is open: " + found + " " + _list.Made + " entries made so far; "
                                 + ((Time.realtimeSinceStartup - began) * 1000f).ToString("F1", CultureInfo.InvariantCulture) + " ms: PASS.");
                }
                return found;
            }
            catch (Exception e)
            {
                Fail(e);
                return null;
            }
        }

        public static void Close(string why)
        {
            if (IsOpen)
            {
                _closing = why;
                _list.Close();
            }
        }

        /// <summary>Drops the window with the director: the game's interface is rebuilt with each city.</summary>
        public static void Remove()
        {
            try
            {
                _list?.Destroy();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            _list = null;
            _host = null;
            _failed = false;
            Plans.Clear();
            Pantry.Clear();
        }

        private static void Fail(Exception e)
        {
            _failed = true;
            Logger?.Error(e);
            Logger?.Warn("Home: the cooking list is off for this session after the error above; cooking starts the first dish the fridges allow.");
            try
            {
                _list?.Destroy();
            }
            catch (Exception again)
            {
                Logger?.Error(again);
            }
            _list = null;
        }

        private static void Closed()
        {
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Home check (cooking list): closed (" + (_closing ?? "cancelled by the player") + ").");
            }
            if (_closing != Chosen)
            {
                CookingTogether.Finish(false);   // closed without a dish: no meal together
            }
            _closing = null;
            Plans.Clear();
        }

        private static void Picked(int id)
        {
            try
            {
                if (id == SizeRow)
                {
                    _size = (_size + 1) % HomeBalance.Batches.Length;
                    string found = Fill();
                    _list.Open(OnPick, OnClosed);   // switches off the entries no longer used
                    if (Meta.Dev.Verbose)
                    {
                        Logger?.Info("Home check (choice): the size is now " + HomeBalance.Batches[_size] + " batch(es): " + found);
                    }
                    return;
                }
                if (id < 0 || id >= Plans.Count || Plans[id] == null)
                {
                    return;
                }
                CookPlan plan = Plans[id];
                HomeDirector host = _host;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Home check (choice): " + (plan.Reheat ? "leftovers reheated" : plan.NameKey + ", " + plan.Portions + " portion(s), " + plan.Minutes + " min") + ".");
                }
                Close(Chosen);
                host?.Cook(plan);
            }
            catch (Exception e)
            {
                Fail(e);
            }
        }

        // The entries, from the fridges as read when the list opened. Returns a line for the log.
        private static string Fill()
        {
            Plans.Clear();
            int units = 0;
            for (int i = 0; i < Pantry.Count; i++)
            {
                units += Pantry[i].Amount;
            }

            // What there is, so that the headline can say why little or nothing is listed.
            Dish[] dishes = HomeBalance.Dishes;
            int possible = 0;
            int needCooker = 0;
            for (int d = 0; d < dishes.Length; d++)
            {
                if (Cooking.Plan(dishes[d], 0, Pantry) == null)
                {
                    continue;
                }
                if (dishes[d].Cooker && !_cooker)
                {
                    needCooker++;
                }
                else
                {
                    possible++;
                }
            }
            string note = _dark ? "reallife_home_cookpanel_no_power"
                : _fridges == 0 ? "reallife_home_cookpanel_no_fridge"
                : units == 0 ? "reallife_home_cookpanel_empty"
                : possible == 0 && needCooker == 0 ? "reallife_home_cookpanel_nothing"
                : !_cooker ? (needCooker > 0 ? "reallife_home_cookpanel_no_cooker_more" : "reallife_home_cookpanel_no_cooker")
                : "reallife_home_cookpanel_hint";
            Text.Length = 0;
            Text.Append(JobText.T("reallife_home_cookpanel_status", "fridges", _fridges.ToString(), "units", units.ToString()))
                .Append("\n<size=80%>").Append(JobText.T(note)).Append("</size>");
            _list.Begin(JobText.T("reallife_home_cookpanel_title"), Text.ToString());

            _list.Add(SizeRow, JobText.T("reallife_home_cooklist_size", "size", JobText.T(SizeNames[_size])), JobText.T("reallife_home_cooklist_size_detail"), ButtonChange, true);

            CookPlan reheat = Cooking.Reheat(HomeState.Leftovers, HomeState.LeftoverDish, HomeState.LeftoverFill);
            if (reheat != null)
            {
                string title = JobText.T("reallife_home_cookpanel_leftovers", "meal", JobText.T(reheat.NameKey), "count", HomeState.Leftovers.ToString());
                string detail = JobText.T("reallife_home_cookpanel_reheat_line", "minutes", reheat.Minutes.ToString(), "fill", reheat.Fill.ToString());
                if (_cooker)
                {
                    _list.Add(Keep(reheat), title, detail, ButtonReheat, true);
                }
                else
                {
                    _list.Add(Keep(null), title, detail + " " + JobText.T("reallife_home_cookpanel_needs_cooker"), ButtonReheat, false);
                }
            }

            for (int d = 0; d < dishes.Length; d++)
            {
                Dish dish = dishes[d];
                if (dish.Cooker && !_cooker)
                {
                    continue;
                }
                CookPlan plan = null;
                for (int size = _size; size >= 0 && plan == null; size--)
                {
                    plan = Cooking.Plan(dish, size, Pantry);
                }
                if (plan == null)
                {
                    continue;
                }
                Text.Length = 0;
                Text.Append(JobText.T("reallife_home_cookpanel_line", "portions", plan.Portions.ToString(), "minutes", plan.Minutes.ToString()).Replace("{fill}", plan.Fill.ToString()));
                if (plan.Size < _size)
                {
                    Text.Append(' ').Append(JobText.T("reallife_home_cookpanel_less"));
                }
                Text.Append('\n').Append(JobText.T("reallife_home_cookpanel_uses", "items", Items(plan)));
                _list.Add(Keep(plan), JobText.T(dish.Name), Text.ToString(), dish.Cooker ? ButtonCook : ButtonMake, true);
            }

            // One unit short of a dish that takes two or more: a dish of one item that is not there is not "nearly".
            int oneShort = 0;
            for (int d = 0; d < dishes.Length; d++)
            {
                Dish dish = dishes[d];
                if (dish.Needs.Length < 2 || (dish.Cooker && !_cooker) || Cooking.Short(dish, Pantry, out FoodKind missing) != 1)
                {
                    continue;
                }
                oneShort++;
                _list.Add(Keep(null), JobText.T(dish.Name), JobText.T("reallife_home_cookpanel_missing", "kind", JobText.T(KindKey(missing))), dish.Cooker ? ButtonCook : ButtonMake, false);
            }

            Text.Length = 0;
            Text.Append(_fridges).Append(" fridge(s), ").Append(HomePlace.Cookers.Count).Append(" cooker(s); food by kind:");
            for (FoodKind kind = FoodKind.Meat; kind <= FoodKind.Drink; kind++)
            {
                int count = Cooking.Count(Pantry, kind);
                if (count > 0)
                {
                    Text.Append(' ').Append(kind).Append(' ').Append(count);
                }
            }
            if (units == 0)
            {
                Text.Append(" none");
            }
            Text.Append("; size ").Append(HomeBalance.Batches[_size]).Append("; ").Append(possible).Append(" dish(es) possible, ").Append(oneShort).Append(" one short, ")
                .Append(needCooker).Append(" wanting a cooker, leftovers ").Append(HomeState.Leftovers).Append('.');
            return Text.ToString();
        }

        // An entry's id is its place in the plans; an entry that cannot be picked keeps a null there.
        private static int Keep(CookPlan plan)
        {
            Plans.Add(plan);
            return Plans.Count - 1;
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
    }
}
