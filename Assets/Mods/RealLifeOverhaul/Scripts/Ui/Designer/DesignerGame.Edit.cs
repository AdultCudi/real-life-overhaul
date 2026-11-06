using System;
using System.Collections;
using System.Collections.Generic;
using BigAmbitions.InteriorDesigner;
using BigAmbitions.InteriorDesigner.InteriorElements;
using BigAmbitions.InteriorDesigner.Tools;
using BigAmbitions.Items;
using BigAmbitions.Tags;
using Buildings;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Buildings.Indoors.InteriorDesign;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.Interiors;
using UI.InteriorDesigner;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    internal static partial class DesignerGame
    {
        private static double _balanceAfter, _costBalance;
        private static float _score, _scoreThen;
        private static int _capacityNow, _capacityThen, _capacityLimit;
        private static bool _capacityStale = true, _baselined;
        private static readonly DesignerRules.Tally Tally = new DesignerRules.Tally();
        private static readonly Dictionary<string, float> Areas = new Dictionary<string, float>(256, StringComparer.Ordinal);
        private static readonly HashSet<string> Laid = new HashSet<string>(StringComparer.Ordinal);   // the pieces of floor this edit has laid, each counted once in the metres
        private static readonly List<DesignerRequirementRules.Placed> Placed = new List<DesignerRequirementRules.Placed>(128);
        private static readonly List<RequireRow> Rows = new List<RequireRow>(12);

        /// <summary>One requirement of the business as the face holds it: the game's own object, the rule that counts its items, and what it came to last time.</summary>
        private sealed class RequireRow
        {
            public BusinessRequirement Need;
            public DesignerRequirementRules.Rule Rule;
            public string Title, Help;
            public bool Met;
            public int Have;
            public bool Counted;
            public bool Items;   // some item of the game's would meet it
        }

        private static void BeginEdit()
        {
            GameInstance save = SaveGameManager.Current;
            _balanceAfter = save != null ? Math.Floor(save.Money) : 0.0;   // as the game's own balance after its changes begins
            _costBalance = 0.0;
            _score = InteriorDesignerController.InteriorScorePercentage;
            _scoreThen = _score;
            _capacityStale = true;
            _baselined = false;
            Tally.Clear();
            Areas.Clear();
        }

        private static void EndEdit()
        {
            Rows.Clear();
            Areas.Clear();
        }

        private static void ForgetEdit()
        {
            EndEdit();
            _baselined = false;
        }

        // ------------------------------------------------------------------ the business

        private static BuildingRegistration Registration()
        {
            BuildingManager bm = InstanceBehavior<BuildingManager>.Instance;
            return bm != null ? bm.buildingRegistration : null;
        }

        private static Building Premises()
        {
            BuildingManager bm = InstanceBehavior<BuildingManager>.Instance;
            return bm != null ? bm.building : null;
        }

        /// <summary>The head of the info panel: the business's name and initials, what it is and where.</summary>
        private static void FillHead(UiDesignerInfo i)
        {
            BuildingRegistration reg = Registration();
            if (reg == null)
            {
                return;
            }
            string kind = reg.businessTypeName == "ba:businesstype_empty" || string.IsNullOrEmpty(reg.businessTypeName) ? null : Say(reg.businessTypeName);
            string place = UiText.Place(BusinessGame.Place(reg));
            string district = GameRegistrations.District(reg);
            string area = string.IsNullOrEmpty(district) ? null : Say(district);
            string name = BusinessGame.Name(reg);
            if (name == "?" || string.IsNullOrWhiteSpace(name))
            {
                name = place;
            }
            i.Name = name;
            i.Mark = DesignerRules.Initials(name);
            string where = string.IsNullOrEmpty(area) ? place : place + ", " + area;
            i.Facts = string.IsNullOrEmpty(kind) ? where : kind + ", " + where;
            i.FloorWords = null;
            int floor = FloorShown();
            if (floor >= 0)
            {
                i.FloorWords = UiText.Fill("reallife_ui_designer_floor", "n", (floor + 1).ToString(System.Globalization.CultureInfo.InvariantCulture));
            }
        }

        /// <summary>A game text by its key as the game says it; the key's own words where it has none.</summary>
        internal static string Say(string key) => string.IsNullOrEmpty(key) ? string.Empty : UiText.T(key);

        // ------------------------------------------------------------------ what this edit costs, by cause

        /// <summary>
        /// This edit's money by cause, from the game's own undo stack (what the designer has done and not undone): a piece bought or copied, a floor or a wall laid, a thing
        /// sold. Each action says its own price; the total the game charges on apply is its own figure, read from its events.
        /// </summary>
        private static void TallyEdit()
        {
            Tally.Clear();
            Laid.Clear();
            InteriorDesignerUI ui = Ui;
            object stack = ui != null && GameContract.DesignerUndoStack.Field != null ? GameContract.DesignerUndoStack.Field.GetValue(ui) : null;
            if (!(stack is IEnumerable actions))
            {
                return;
            }
            foreach (object action in actions)
            {
                Count(action as IRevertibleAction);
            }
        }

        private static void Count(IRevertibleAction action)
        {
            switch (action)
            {
                case null:
                    return;
                case FurnitureRevertibleAction _:
                    Tally.Add(DesignerRules.Cause.Furniture, Price(GameContract.DesignerBoughtPrice, action), 1);
                    return;
                case DuplicateRevertibleAction _:
                    Tally.Add(DesignerRules.Cause.Furniture, Price(GameContract.DesignerCopyPrice, action), 1);
                    return;
                case SellRevertibleAction _:
                    Tally.Add(DesignerRules.Cause.Sold, -Price(GameContract.DesignerSoldPrice, action), 1);
                    return;
                case PackageSellRevertibleAction _:
                    Tally.Add(DesignerRules.Cause.Sold, -Price(GameContract.DesignerSoldPackPrice, action), 1);
                    return;
                case InteriorElementRevertibleAction _:
                    CountLaid(action);
                    return;
                case CompositeRevertibleAction _:
                    if (GameContract.DesignerComposed.Field != null && GameContract.DesignerComposed.Field.GetValue(action) is IRevertibleAction[] parts)
                    {
                        for (int k = 0; k < parts.Length; k++)
                        {
                            Count(parts[k]);
                        }
                    }
                    return;
            }
        }

        private static float Price(GameMember member, object action) => member.Field != null && member.Field.GetValue(action) is float price ? price : 0f;

        // A floor or a wall laid: its cost, and for a floor the metres it covers, each piece's by its own size.
        private static void CountLaid(object action)
        {
            float cost = Price(GameContract.DesignerLaidCost, action);
            var ids = GameContract.DesignerLaidIds.Field != null ? GameContract.DesignerLaidIds.Field.GetValue(action) as string[] : null;
            if (ids == null || ids.Length == 0)
            {
                return;
            }
            bool floor = false;
            float area = 0f;
            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int k = 0; k < ids.Length; k++)
            {
                if (!seen.Add(ids[k]) || !InteriorElementsHelper.InteriorElementsCache.TryGetValue(ids[k], out InteriorElement piece) || piece == null)
                {
                    continue;
                }
                if (piece.IsFloor)
                {
                    floor = true;
                    area += Laid.Add(ids[k]) ? AreaOf(ids[k], piece) : 0f;
                }
            }
            if (floor)
            {
                Tally.Add(DesignerRules.Cause.Floor, cost, seen.Count);
                Tally.Area += area;
            }
            else
            {
                Tally.Add(DesignerRules.Cause.Wall, cost, seen.Count);
            }
        }

        // A piece of floor's size on the ground, from its own drawn bounds, kept for the edit.
        private static float AreaOf(string id, InteriorElement piece)
        {
            if (Areas.TryGetValue(id, out float known))
            {
                return known;
            }
            float area = 0f;
            if (piece.elementRenderer != null)
            {
                Vector3 size = piece.elementRenderer.bounds.size;
                area = size.x * size.z;
            }
            Areas[id] = area;
            return area;
        }

        private static void FillCost(UiDesignerInfo i)
        {
            i.EditTitle = UiText.T("reallife_ui_designer_edit");
            int n = 0;
            string[] causes = { "reallife_ui_designer_cost_furniture", "reallife_ui_designer_cost_floor", "reallife_ui_designer_cost_walls", "reallife_ui_designer_cost_sold" };
            DesignerRules.Cause[] order = { DesignerRules.Cause.Furniture, DesignerRules.Cause.Floor, DesignerRules.Cause.Wall, DesignerRules.Cause.Sold };
            for (int k = 0; k < order.Length; k++)
            {
                int things = Tally.Count[(int)order[k]];
                if (things == 0)
                {
                    continue;
                }
                float money = Tally.Money[(int)order[k]];
                string label = order[k] == DesignerRules.Cause.Floor
                    ? UiText.Fill(causes[k], "area", Math.Max(1, (int)Math.Round(Tally.Area)).ToString(System.Globalization.CultureInfo.InvariantCulture))
                    : UiText.Fill(causes[k], "n", things.ToString(System.Globalization.CultureInfo.InvariantCulture));
                i.Cost[n++] = new UiDesignerCostLine { Label = label, Value = UiText.Money(money), Ink = money < 0f ? UiRole.OkText : UiRole.Text };
            }
            double spent = -_costBalance;
            i.Cost[n++] = new UiDesignerCostLine
            {
                Label = UiText.T("reallife_ui_designer_cost_total"), Value = UiText.Money((float)spent), Total = true,
                Ink = spent > 0.5 ? UiRole.Text : spent < -0.5 ? UiRole.OkText : UiRole.Text2
            };
            i.CostCount = n;
        }

        // ------------------------------------------------------------------ customers an hour and the interior score

        private static void ReadCapacity()
        {
            _capacityStale = false;
            BuildingRegistration reg = Registration();
            Building building = Premises();
            if (reg == null || building == null)
            {
                return;
            }
            int least = 0;
            try
            {
                _capacityLimit = BuildingSizeHelper.GetData(building).GetCustomerCapacity(building.BuildingType, building.BuildingVersion);
                reg.UpdateCachedAvailableProducts();
                foreach (Item.ItemCapacity capacity in reg.itemInstances.Values.GetItemsSortedByCapacity(reg))
                {
                    if (least == 0 || least > capacity.CustomersLimit)
                    {
                        least = capacity.CustomersLimit;
                    }
                }
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the customers an hour in the interior designer");
                return;
            }
            _capacityNow = least;
            if (!_baselined)
            {
                _baselined = true;
                _capacityThen = least;
            }
        }

        private static bool ShowsCapacity(BuildingRegistration reg, Building building)
        {
            return building != null && BuildingTypeHelper.GetData(building).HasTag(TagRef.Buildingtypetag.hasbusinessrequirements) && reg.businessTypeName != "ba:businesstype_headquarters";
        }

        private static bool ShowsScore(Building building) => building != null && BuildingTypeHelper.GetData(building).HasTag(TagRef.Buildingtypetag.hasinteriorscore);

        private static void FillReadings(UiDesignerInfo i)
        {
            BuildingRegistration reg = Registration();
            Building building = Premises();
            i.CapacityShown = reg != null && ShowsCapacity(reg, building);
            if (i.CapacityShown)
            {
                if (_capacityStale)
                {
                    ReadCapacity();
                }
                bool limited = _capacityLimit > 0 && _capacityLimit < 9999;
                i.CapacityTitle = UiText.T("bizman_customers_capacity");
                i.CapacityFigure = _capacityNow.ToString(System.Globalization.CultureInfo.InvariantCulture);
                i.CapacityOf = limited ? UiText.Fill("reallife_ui_designer_of", "n", _capacityLimit.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null;
                i.CapacityFill = DesignerRules.Fill(_capacityNow, _capacityLimit);
                i.CapacityTone = limited && _capacityNow < _capacityLimit ? UiRole.Warn : UiRole.Accent;
                i.CapacityChange = DesignerRules.Change(_capacityNow, _capacityThen, null);
                i.CapacityChangeInk = _capacityNow >= _capacityThen ? UiRole.OkText : UiRole.DangerText;
            }
            i.ScoreShown = ShowsScore(building);
            if (i.ScoreShown)
            {
                int now = Mathf.CeilToInt(_score), then = Mathf.CeilToInt(_scoreThen), want = InteriorGame.Minimum(GameRegistrations.District(reg));
                i.ScoreTitle = UiText.T("reallife_ui_designer_score");
                i.ScoreFigure = now.ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
                i.ScoreWants = want > 0 ? UiText.Fill("reallife_ui_designer_wants", "n", want.ToString(System.Globalization.CultureInfo.InvariantCulture)) : null;
                i.ScoreFill = Mathf.Clamp(now, 0, 100);
                i.ScoreTone = want > 0 && now < want ? UiRole.Warn : UiRole.Ok;
                i.ScoreChange = DesignerRules.Change(now, then, "%");
                i.ScoreChangeInk = now >= then ? UiRole.OkText : UiRole.DangerText;
            }
        }

        // ------------------------------------------------------------------ what the business needs to open

        /// <summary>
        /// The requirements of the business on the designer's live list of what is placed: each line's count of the items that meet it is made from the building
        /// registration's own items (the list the designer's actions change as things are placed, sold and put back), and a line is met by the game's own test.
        /// </summary>
        private static void ReadRequirements()
        {
            Rows.Clear();
            BuildingRegistration reg = Registration();
            BusinessType type = reg != null ? BusinessTypeHelper.GetData(reg) : null;
            if (type == null || type.businessRequirements == null)
            {
                return;
            }
            Placed.Clear();
            if (reg.itemInstances != null)
            {
                foreach (ItemInstance placed in reg.itemInstances.Values)
                {
                    Item item = placed != null ? placed.ItemCached : null;
                    if (item != null)
                    {
                        Placed.Add(new DesignerRequirementRules.Placed { Name = placed.itemName, Mask = (int)item.type });
                    }
                }
            }
            for (int k = 0; k < type.businessRequirements.Count; k++)
            {
                BusinessRequirement need = type.businessRequirements[k];
                if (need == null)
                {
                    continue;
                }
                var row = new RequireRow { Need = need };
                try
                {
                    row.Title = Say(need.GetLocalizeKey());
                    row.Help = need.GetHelpLink(reg);
                    string[] names = need.GetItems();
                    int mask = (int)need.GetItemType();
                    int count = 0;
                    if (need is SpecificItemsInBuildingBySqm bySqm)
                    {
                        count = bySqm.GetRequiredItemCount(reg);
                        row.Counted = true;
                    }
                    else if (need is ItemsOfTypeInBuildingBySqm typeBySqm)
                    {
                        count = typeBySqm.GetRequiredItemCount(reg);
                        row.Counted = true;
                    }
                    row.Rule = new DesignerRequirementRules.Rule { Names = names, Mask = names != null && names.Length > 0 ? 0 : mask, Need = count };
                    row.Items = (names != null && names.Length > 0) || mask != 0;
                    DesignerRequirementRules.Read(row.Rule, Placed, out row.Have, out _);
                    row.Met = need.IsRequirementMet(reg);   // the game's own answer
                }
                catch (Exception e) when (Guard.Missing(e))
                {
                    Guard.DoesWithout(e, "a requirement of a business in the interior designer");
                    continue;
                }
                Rows.Add(row);
            }
        }

        private static void FillRequirements(UiDesignerInfo i)
        {
            BuildingRegistration reg = Registration();
            ReadRequirements();
            i.RequireShown = Rows.Count > 0;
            if (!i.RequireShown)
            {
                i.RequireCount = 0;
                return;
            }
            string kind = reg != null ? Say(reg.businessTypeName) : string.Empty;
            i.RequireTitle = UiText.Fill("reallife_ui_designer_needs", "type", kind);
            i.ShowLabel = UiText.T("reallife_ui_designer_show");
            i.HowLabel = UiText.T("reallife_ui_designer_how");
            int met = 0, n = Math.Min(Rows.Count, UiDesignerInfo.RequireMost);
            for (int k = 0; k < Rows.Count; k++)
            {
                met += Rows[k].Met ? 1 : 0;
            }
            for (int k = 0; k < n; k++)
            {
                RequireRow r = Rows[k];
                i.Require[k] = new UiDesignerRequirement
                {
                    Title = r.Title, Met = r.Met, Count = DesignerRules.CountWords(r.Counted, r.Met, r.Have, r.Rule.Need), CanShow = !r.Met && r.Items, Showing = k == _needFilter,
                    CanHelp = !string.IsNullOrEmpty(r.Help)
                };
            }
            i.RequireCount = n;
            i.RequireFigure = DesignerRules.MetWords(met, Rows.Count);
            i.RequireInk = met == Rows.Count ? UiRole.OkText : UiRole.WarnText;
        }

        /// <summary>The game's own help page for a requirement, opened as its own panel's help button opens it.</summary>
        public static bool Help(int index)
        {
            if (index < 0 || index >= Rows.Count || string.IsNullOrEmpty(Rows[index].Help))
            {
                return false;
            }
            HelpSystem help = InstanceBehavior<HelpSystem>.Instance;
            if (help == null)
            {
                return false;
            }
            help.OpenLink(Rows[index].Help);
            help.Toggle(show: true);
            return true;
        }

        /// <summary>True when an item meets the requirement at a line (it fills what is still wanted).</summary>
        private static bool Fills(int line, Item item)
        {
            return line >= 0 && line < Rows.Count && Rows[line].Rule != null && !Rows[line].Met && DesignerRequirementRules.Meets(Rows[line].Rule, item.itemName, (int)item.type);
        }

        /// <summary>True when any requirement still unmet is filled by this item.</summary>
        private static bool Needed(Item item)
        {
            for (int k = 0; k < Rows.Count; k++)
            {
                if (Rows[k].Rule != null && !Rows[k].Met && Rows[k].Items && DesignerRequirementRules.Meets(Rows[k].Rule, item.itemName, (int)item.type))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
