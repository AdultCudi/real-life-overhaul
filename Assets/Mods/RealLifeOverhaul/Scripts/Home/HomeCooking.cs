using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>One kind of item in the home's fridges, all fridges together.</summary>
    public sealed class PantryItem
    {
        public string Id;            // the game's item name, which is also the key of its text
        public FoodKind Kind;
        public int Saturation;       // what one unit fills when the game's own eating eats it
        public int Amount;
    }

    /// <summary>So many units of one item, as a dish takes them.</summary>
    public struct CookUse
    {
        public string Id;
        public int Amount;
        public int Saturation;
    }

    /// <summary>
    /// A dish as it would be made right now: which items leave the fridges, how many portions come of it, how
    /// long it takes and what a portion fills. Reheating leftovers is a plan too (no dish, nothing taken).
    /// </summary>
    public sealed class CookPlan
    {
        public Dish Dish;            // null: leftovers reheated
        public int Size;             // index into HomeBalance.Batches
        public readonly List<CookUse> Uses = new List<CookUse>(6);
        public int Portions;
        public int Minutes;
        public int Fill;             // what one portion fills, of the game's 100
        public string NameKey;       // the dish's name, or what the leftovers were

        public bool Reheat => Dish == null;
        public bool Cooker => Dish == null || Dish.Cooker;
    }

    /// <summary>
    /// What can be made from what is in the fridges. Game-free: it is given the fridges' contents as a list
    /// and answers with plans. An item is of one kind only, so a dish can be made when, kind by kind, the
    /// fridges hold as many units as it needs; the most filling items of a kind are used first, and what the
    /// dish fills is worked out from the items actually used.
    /// </summary>
    public static class Cooking
    {
        public static FoodKind KindOf(string itemId)
        {
            return itemId != null && HomeBalance.Foods.TryGetValue(itemId, out FoodKind kind) ? kind : FoodKind.None;
        }

        public static Dish DishByNumber(int number)
        {
            Dish[] dishes = HomeBalance.Dishes;
            for (int i = 0; i < dishes.Length; i++)
            {
                if (dishes[i].Number == number)
                {
                    return dishes[i];
                }
            }
            return null;
        }

        /// <summary>How many units of a kind the fridges hold.</summary>
        public static int Count(List<PantryItem> pantry, FoodKind kind)
        {
            int count = 0;
            for (int i = 0; i < pantry.Count; i++)
            {
                if (pantry[i].Kind == kind)
                {
                    count += pantry[i].Amount;
                }
            }
            return count;
        }

        private static int Needed(Dish dish, FoodKind kind)
        {
            int count = 0;
            for (int i = 0; i < dish.Needs.Length; i++)
            {
                if (dish.Needs[i] == kind)
                {
                    count++;
                }
            }
            return count;
        }

        /// <summary>
        /// How many units one batch of the dish is short of, all kinds together (0: it can be made). When it is
        /// short of one kind only, that kind is given, for "one ingredient short".
        /// </summary>
        public static int Short(Dish dish, List<PantryItem> pantry, out FoodKind missing)
        {
            missing = FoodKind.None;
            int shortBy = 0;
            for (int i = 0; i < dish.Needs.Length; i++)
            {
                FoodKind kind = dish.Needs[i];
                bool seen = false;
                for (int k = 0; k < i; k++)
                {
                    seen |= dish.Needs[k] == kind;
                }
                if (seen)
                {
                    continue;
                }
                int lack = Needed(dish, kind) - Count(pantry, kind);
                if (lack > 0)
                {
                    shortBy += lack;
                    missing = kind;
                }
            }
            return shortBy;
        }

        /// <summary>The dish in a size (an index into HomeBalance.Batches) from these fridges; null if they do not hold enough.</summary>
        public static CookPlan Plan(Dish dish, int size, List<PantryItem> pantry)
        {
            if (dish == null || size < 0 || size >= HomeBalance.Batches.Length)
            {
                return null;
            }
            int batches = HomeBalance.Batches[size];
            var plan = new CookPlan { Dish = dish, Size = size, NameKey = dish.Name };
            int total = 0;
            for (int i = 0; i < dish.Needs.Length; i++)
            {
                FoodKind kind = dish.Needs[i];
                bool seen = false;
                for (int k = 0; k < i; k++)
                {
                    seen |= dish.Needs[k] == kind;
                }
                if (seen)
                {
                    continue;
                }
                int need = Needed(dish, kind) * batches;
                // The most filling first; a fridge holds a handful of kinds of item, so this is a short loop.
                while (need > 0)
                {
                    PantryItem best = null;
                    for (int p = 0; p < pantry.Count; p++)
                    {
                        PantryItem item = pantry[p];
                        if (item.Kind == kind && item.Amount > 0 && !Used(plan, item.Id) && (best == null || item.Saturation > best.Saturation))
                        {
                            best = item;
                        }
                    }
                    if (best == null)
                    {
                        return null;
                    }
                    int take = best.Amount < need ? best.Amount : need;
                    plan.Uses.Add(new CookUse { Id = best.Id, Amount = take, Saturation = best.Saturation });
                    total += take * best.Saturation;
                    need -= take;
                }
            }
            plan.Portions = dish.Portions * batches;
            plan.Minutes = RoundToFive(dish.Minutes * HomeBalance.BatchMinutes[size]);
            float fill = total * dish.Yield / plan.Portions;
            plan.Fill = fill > 100f ? 100 : (int)(fill + 0.5f);
            return plan;
        }

        /// <summary>One of the portions put by, warmed up. Null when there are none.</summary>
        public static CookPlan Reheat(int leftovers, int dishNumber, int fill)
        {
            if (leftovers <= 0)
            {
                return null;
            }
            Dish was = DishByNumber(dishNumber);
            return new CookPlan
            {
                Dish = null,
                Size = 0,
                Portions = 1,
                Minutes = HomeBalance.ReheatMinutes,
                Fill = fill > 0 ? (fill > 100 ? 100 : fill) : HomeBalance.LeftoverSaturation,
                NameKey = was != null ? was.Name : "reallife_home_dish_leftovers"
            };
        }

        /// <summary>The chance that this plan burns, before "easy needs".</summary>
        public static float BurnChance(CookPlan plan, bool worn)
        {
            if (plan.Reheat || !plan.Dish.Cooker)
            {
                return 0f;
            }
            return plan.Dish.Burn + HomeBalance.BatchBurn[plan.Size] + (worn ? HomeBalance.BurnWhenWorn : 0f);
        }

        private static bool Used(CookPlan plan, string id)
        {
            for (int i = 0; i < plan.Uses.Count; i++)
            {
                if (plan.Uses[i].Id == id)
                {
                    return true;
                }
            }
            return false;
        }

        private static int RoundToFive(float minutes)
        {
            int rounded = (int)(minutes / 5f + 0.5f) * 5;
            return rounded < 5 ? 5 : rounded;
        }
    }
}
