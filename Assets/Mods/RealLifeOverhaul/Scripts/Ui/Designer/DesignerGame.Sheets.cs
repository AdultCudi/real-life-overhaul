using System;
using System.Collections.Generic;
using BigAmbitions.InteriorDesigner;
using BigAmbitions.Items;
using BigAmbitions.ModsInternal;
using BigAmbitions.SaveSystem;
using Blueprints;
using Buildings;
using Buildings.Indoors.InteriorDesign;
using Controllers;
using Helpers;
using TMPro;
using UI.Elements;
using UI.InteriorDesigner;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The smaller tools on the kit, each mirrored from the game's own panel and pressed through it: the lists of producers, counters and cameras (the game's buttons, one
    /// press each), the time of day (the game's slider and its two buttons; the clock of the game never moves), the blueprint's name and its Save (the game's field and its
    /// own method, which keeps its checks and its questions), and the game's question of what kind of business a blueprint is for (its list, its Continue and its Cancel).
    /// </summary>
    internal static partial class DesignerGame
    {
        private static DropdownSelectorUI _asker;
        private static readonly int[] MarkMinutes = new int[12];

        private static void BeginSheets()
        {
            _asker = null;
        }

        private static void EndSheets()
        {
        }

        private static void ForgetSheets()
        {
            _asker = null;
        }

        // ------------------------------------------------------------------ the lists of things

        private static bool FillItems(UiDesignerItems m, UiDesignerTool tool)
        {
            ItemActionPanelUI panel = DesignerPanes.ForList(tool);
            if (panel == null)
            {
                return false;
            }
            var controllers = BuyKits.Get<List<ItemController>>(GameContract.DesignerListControllers, panel);
            int selected = BuyKits.Value(GameContract.DesignerListSelected, panel, -1);
            bool security = tool == UiDesignerTool.Security;
            m.Title = UiText.T(tool == UiDesignerTool.Producer ? "interior_designer_producer_panel"
                : tool == UiDesignerTool.Queue ? "interior_designer_queue_designer" : "interior_designer_security_coverage");
            m.Icon = DesignerGlyphs.ToolIcons[(int)tool];
            m.Note = UiText.T(tool == UiDesignerTool.Producer ? "reallife_ui_designer_note_producer"
                : tool == UiDesignerTool.Queue ? "reallife_ui_designer_note_queue" : "reallife_ui_designer_note_security");
            m.NoteInk = UiRole.Text2;
            m.EmptyTitle = UiText.T("reallife_ui_designer_none_title");
            m.EmptyBody = UiText.T("reallife_ui_designer_none_things");
            m.Meter = -1;
            int n = controllers != null ? controllers.Count : 0;
            if (m.Items.Length < n)
            {
                m.Items = new UiDesignerItem[Math.Max(n, m.Items.Length * 2)];
            }
            int shown = 0;
            for (int i = 0; i < n; i++)
            {
                ItemController c = controllers[i];
                if (c == null || c.ItemInstance == null)
                {
                    continue;
                }
                UiIcon glyph = UiIcon.None;
                UiRole tone = UiRole.Ok;
                string line = null;
                if (security)
                {
                    bool covered = c.ItemInstance.isSecured;
                    glyph = covered ? UiIcon.Check : UiIcon.Warning;
                    tone = covered ? UiRole.Ok : UiRole.Warn;
                    line = UiText.T(covered ? "reallife_ui_designer_secured" : "reallife_ui_designer_not_covered");
                }
                else if (tool == UiDesignerTool.Producer)
                {
                    line = StockLine(c);
                }
                m.Items[shown] = new UiDesignerItem
                {
                    Index = i, Name = NameOf(c.itemName), Line = line, Picture = ItemPictureKey(c.itemName), Glyph = glyph, GlyphTone = tone,
                    Chosen = selected != -1 && c.GetHashCode() == selected
                };
                shown++;
            }
            m.Count = shown;
            if (security)
            {
                BuildingRegistration reg = Registration();
                BusinessType type = reg != null ? BusinessTypeHelper.GetData(reg) : null;
                if (type != null && type.HasTag(BigAmbitions.Tags.TagRef.Businesstag.allowtheft))
                {
                    int percent = Mathf.FloorToInt(reg.securityLevelPercentage);
                    m.Meter = Mathf.Clamp(percent, 0, 100);
                    m.MeterWords = UiText.Fill("interior_designer_security_rating", "amount", UiText.Whole(percent) + "%");
                    m.MeterTone = percent >= 90 ? UiRole.Ok : percent >= 50 ? UiRole.Accent : percent >= 30 ? UiRole.Warn : UiRole.Danger;
                }
            }
            var none = BuyKits.Get<GameObject>(GameContract.DesignerListNone, panel);
            string own = none != null ? UiWidgets.Label(none.transform) : null;
            if (!string.IsNullOrEmpty(own))
            {
                m.EmptyBody = own;
            }
            return true;
        }

        // What a shelf stocks, or the words on a sign: what the game's own overlay for that item would set.
        private static string StockLine(ItemController c)
        {
            if (c is ItemWithTextController)
            {
                string text = c.ItemInstance.worldSpaceTextValue;
                return string.IsNullOrEmpty(text) ? UiText.T("reallife_ui_designer_not_set") : text;
            }
            if (c.StockTypes.Count > 1)
            {
                CargoInstance stock = c.ItemInstance.GetStockInstance();
                return stock == null || string.IsNullOrEmpty(stock.itemName) ? UiText.T("reallife_ui_designer_not_set") : NameOf(stock.itemName);
            }
            return null;
        }

        /// <summary>A thing of a list chosen: the game's own button for it is pressed, which focuses the camera on it and opens its overlay.</summary>
        public static bool ChooseThing(UiDesignerTool tool, int index)
        {
            ItemActionPanelUI panel = DesignerPanes.ForList(tool);
            var controllers = panel != null ? BuyKits.Get<List<ItemController>>(GameContract.DesignerListControllers, panel) : null;
            var container = panel != null ? BuyKits.Get<Transform>(GameContract.DesignerListContainer, panel) : null;
            if (controllers == null || container == null || index < 0 || index >= controllers.Count || controllers[index] == null)
            {
                return false;
            }
            int hash = controllers[index].GetHashCode();
            for (int i = 0; i < container.childCount; i++)
            {
                IDOtherItemTemplate t = container.GetChild(i).GetComponent<IDOtherItemTemplate>();
                if (t != null && t.gameObject.activeSelf && t.itemHash == hash)
                {
                    return UiWidgets.Press(BuyKits.Get<Button>(GameContract.DesignerListFocus, t));
                }
            }
            return false;
        }

        // ------------------------------------------------------------------ the time of day

        private static bool FillTime(UiDesignerTime t)
        {
            TimeOfDayActionPanelUI panel = DesignerPanes.Daylight;
            var slider = panel != null ? BuyKits.Get<Slider>(GameContract.DesignerSlider, panel) : null;
            if (slider == null)
            {
                return false;
            }
            float min = slider.minValue, max = slider.maxValue, value = slider.value;
            t.Title = UiText.T("reallife_ui_designer_time_title");
            t.Sub = UiText.T("reallife_ui_designer_time_sub");
            t.Time = DesignerRules.Clock(value);
            t.Figure = t.Time;
            t.Share = DesignerRules.Share(value, min, max);
            GameInstance save = SaveGameManager.Current;
            string clock = save != null ? DesignerRules.Clock(save.Hour * 60 + save.Minute) : null;
            t.Clock = clock != null ? UiText.Fill("reallife_ui_designer_clock", "time", clock) : null;
            int n = DesignerRules.Marks(min, max, MarkMinutes);
            if (t.Marks.Length != n)
            {
                t.Marks = new string[n];
                t.MarkShares = new float[n];
            }
            for (int i = 0; i < n; i++)
            {
                t.Marks[i] = DesignerRules.Clock(MarkMinutes[i]);
                t.MarkShares[i] = DesignerRules.Share(MarkMinutes[i], min, max);
            }
            t.LessLabel = "-10";
            t.MoreLabel = "+10";
            t.LessOff = value <= min;
            t.MoreOff = value >= max;
            t.Note = UiText.T("reallife_ui_designer_time_note");
            return true;
        }

        /// <summary>The slider's track: the width of the layout's slider from the pointer's share across it, to a value in tens of minutes.</summary>
        public static bool SlideTime(float share)
        {
            TimeOfDayActionPanelUI panel = DesignerPanes.Daylight;
            var slider = panel != null ? BuyKits.Get<Slider>(GameContract.DesignerSlider, panel) : null;
            if (slider == null)
            {
                return false;
            }
            float value = DesignerRules.Value(share, slider.minValue, slider.maxValue);
            return Math.Abs(value - slider.value) < 0.5f || UiWidgets.Slide(slider, value);
        }

        /// <summary>Ten minutes on or back, by the game's own two buttons' methods.</summary>
        public static bool StepTime(int minutes)
        {
            TimeOfDayActionPanelUI panel = DesignerPanes.Daylight;
            if (panel == null)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                if (minutes > 0)
                {
                    panel.IncreaseTimeButton();
                }
                else
                {
                    panel.DecreaseTimeButton();
                }
            }
            return true;
        }

        // ------------------------------------------------------------------ the blueprint

        private static SaveBlueprintUI BlueprintUi()
        {
            return DesignerPanes.Blueprint != null ? BuyKits.Get<SaveBlueprintUI>(GameContract.DesignerBlueprintUi, DesignerPanes.Blueprint) : null;
        }

        private static bool BlueprintOpen()
        {
            SaveBlueprintUI ui = BlueprintUi();
            return ui != null && ui.IsOpen;
        }

        private static bool FillBlueprint(UiDesignerBlueprint b)
        {
            SaveBlueprintUI ui = BlueprintUi();
            if (ui == null || !ui.IsOpen)
            {
                return false;
            }
            BuildingRegistration reg = Registration();
            var field = BuyKits.Get<TMP_InputField>(GameContract.DesignerBlueprintName, ui);
            string name = field != null ? field.text ?? string.Empty : string.Empty;
            b.Title = UiText.T("reallife_ui_designer_bp_title");
            b.Sub = reg != null ? BusinessGame.Name(reg) + ", " + UiText.Place(BusinessGame.Place(reg)) : null;
            b.NameLabel = UiText.T("reallife_ui_designer_bp_name");
            b.Name = name;
            b.NamePlaceholder = UiText.T("reallife_ui_designer_bp_placeholder");
            int problem = DesignerRules.NameProblem(name);
            b.NameBad = problem == 2;
            b.NameHint = UiText.T(problem == 2 ? "reallife_ui_designer_bp_bad" : "reallife_ui_designer_bp_hint");
            b.FactsTitle = UiText.T("reallife_ui_designer_bp_saved");
            FillFacts(b, reg);
            // A name that is empty, or has a character the game refuses, is not "free": the row says what is wrong with it, as the line under the box does.
            bool taken = problem == 0 && BlueprintsFolderLoader.IsBlueprintNameInUse(name);
            if (problem == 1)
            {
                b.CheckWords[0] = UiText.T("reallife_ui_designer_bp_empty");
                b.CheckSide[0] = UiText.T("reallife_ui_designer_bp_empty_side");
            }
            else if (problem == 2)
            {
                b.CheckWords[0] = UiText.T("reallife_ui_designer_bp_unfit");
                b.CheckSide[0] = UiText.T("reallife_ui_designer_bp_unfit_side");
            }
            else
            {
                b.CheckWords[0] = UiText.T(taken ? "reallife_ui_designer_bp_used" : "reallife_ui_designer_bp_free");
                b.CheckSide[0] = UiText.T(taken ? "reallife_ui_designer_bp_used_side" : "reallife_ui_designer_bp_free_side");
            }
            b.CheckMet[0] = !taken && problem == 0;
            bool gift = HasGift();
            b.CheckWords[1] = UiText.T(gift ? "reallife_ui_designer_bp_gifts_in" : "reallife_ui_designer_bp_gifts");
            b.CheckSide[1] = UiText.T("reallife_ui_designer_bp_gifts_side");
            b.CheckMet[1] = !gift;
            b.CheckCount = 2;
            b.Foot = UiText.T("reallife_ui_designer_bp_foot");
            b.SaveLabel = UiText.T("reallife_ui_designer_bp_save");
            b.SaveOff = problem != 0 || gift;
            return true;
        }

        private static void FillFacts(UiDesignerBlueprint b, BuildingRegistration reg)
        {
            int n = 0;
            if (reg != null && !string.IsNullOrEmpty(reg.businessTypeName) && reg.businessTypeName != "ba:businesstype_empty")
            {
                b.FactLabels[n] = UiText.T("reallife_ui_designer_bp_business");
                b.FactValues[n++] = Say(reg.businessTypeName);
            }
            if (reg != null)
            {
                b.FactLabels[n] = UiText.T("reallife_ui_designer_bp_floor");
                b.FactValues[n++] = UiText.Fill("reallife_ui_designer_bp_area", "n", UiText.Whole(BuildingSizeHelper.GetData(reg).squareMeters));
                b.FactLabels[n] = UiText.T("reallife_ui_designer_bp_furniture");
                b.FactValues[n++] = UiText.Fill("reallife_ui_designer_bp_items", "n", UiText.Whole(reg.itemInstances != null ? reg.itemInstances.Count : 0));
            }
            int mods = ModsNeeded();
            if (mods > 0 && n < UiDesignerBlueprint.FactsMost)
            {
                b.FactLabels[n] = UiText.T("reallife_ui_designer_bp_mods");
                b.FactValues[n++] = UiText.Fill("reallife_ui_designer_bp_mods_n", "n", UiText.Whole(mods));
            }
            b.FactCount = n;
        }

        private static bool AllDigits(string id)
        {
            for (int i = 0; i < id.Length; i++)
            {
                if (!char.IsDigit(id[i]))
                {
                    return false;
                }
            }
            return true;
        }

        // The mods a saved layout would name as needed, as the game writes them (the ones with a workshop id; the others as "unknown").
        private static int ModsNeeded()
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            try
            {
                foreach (HashSet<string> set in ModLifecycleLoader.ActiveModIdsByScope.Values)
                {
                    foreach (string id in set)
                    {
                        ids.Add(AllDigits(id) ? id : "unknown");
                    }
                }
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the mods a blueprint needs in the interior designer");
            }
            return ids.Count;
        }

        private static bool HasGift()
        {
            BuildingManager bm = InstanceBehavior<BuildingManager>.Instance;
            if (bm == null || bm.allItemControllers == null)
            {
                return false;
            }
            foreach (ItemController c in bm.allItemControllers)
            {
                Item item = c != null ? ItemsGetter.GetByName(c.itemName) : null;
                if (item != null && item.isSpecialGift)
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>The words typed in the blueprint's name go into the game's own field, as if typed there.</summary>
        public static bool NameBlueprint(string text)
        {
            SaveBlueprintUI ui = BlueprintUi();
            return ui != null && UiWidgets.Type(BuyKits.Get<TMP_InputField>(GameContract.DesignerBlueprintName, ui), text ?? string.Empty, false);
        }

        /// <summary>The name in the game's field now.</summary>
        public static string BlueprintName()
        {
            SaveBlueprintUI ui = BlueprintUi();
            var field = ui != null ? BuyKits.Get<TMP_InputField>(GameContract.DesignerBlueprintName, ui) : null;
            return field != null ? field.text ?? string.Empty : string.Empty;
        }

        /// <summary>Save: the game's own method for it, which checks the name and the gifts and asks what it asks.</summary>
        public static bool SaveBlueprint()
        {
            SaveBlueprintUI ui = BlueprintUi();
            if (ui == null || !ui.IsOpen)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                ui.OnSaveLayoutAsBlueprint();
            }
            return true;
        }

        // ------------------------------------------------------------------ the question of what kind of business

        // The game's question box, found from the delegate the game hangs on its static: the box subscribes itself as it starts, so no search of the scene is made.
        private static DropdownSelectorUI Asker()
        {
            if (_asker != null)
            {
                return _asker;
            }
            Delegate subscribed = DropdownSelector.onShow;
            Delegate[] list = subscribed != null ? subscribed.GetInvocationList() : null;
            for (int i = 0; list != null && i < list.Length; i++)
            {
                if (list[i].Target is DropdownSelectorUI box)
                {
                    _asker = box;
                    break;
                }
            }
            return _asker;
        }

        private static bool Asking() => DropdownSelector.isOpen && Asker() != null;

        private static bool FillQuestion(UiDesignerBusinessType t)
        {
            DropdownSelectorUI box = Asker();
            if (box == null || !DropdownSelector.isOpen)
            {
                return false;
            }
            var list = BuyKits.Get<global::UI.Elements.Dropdown>(GameContract.DesignerAskList, box);
            var options = list != null ? BuyKits.Get<List<string>>(GameContract.DesignerAskOptions, list) : null;
            if (options == null)
            {
                return false;
            }
            string header = UiWidgets.Label(BuyKits.Get<Component>(GameContract.DesignerAskHeader, box));
            t.Title = string.IsNullOrEmpty(header) ? UiText.T("dropdown_selector_header") : header;
            t.Body = UiWidgets.Label(BuyKits.Get<Component>(GameContract.DesignerAskBody, box));
            if (t.Options.Length != options.Count)
            {
                t.Options = new string[options.Count];
            }
            for (int i = 0; i < options.Count; i++)
            {
                t.Options[i] = options[i];
            }
            t.Count = options.Count;
            t.Chosen = list.SelectedOptionIndex;
            string confirm = UiWidgets.Label(BuyKits.Get<Button>(GameContract.DesignerAskConfirm, box));
            t.ContinueLabel = string.IsNullOrEmpty(confirm) ? UiText.T("common_ok") : confirm;
            t.CancelLabel = UiText.T("common_cancel");
            return true;
        }

        /// <summary>An option of the question chosen in the game's own list.</summary>
        public static bool AnswerOption(int index)
        {
            DropdownSelectorUI box = Asker();
            var list = box != null ? BuyKits.Get<global::UI.Elements.Dropdown>(GameContract.DesignerAskList, box) : null;
            if (list == null)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                list.SelectOption(index);
            }
            return true;
        }

        /// <summary>Continue: the game's own button.</summary>
        public static bool AnswerContinue()
        {
            DropdownSelectorUI box = Asker();
            return box != null && UiWidgets.Press(BuyKits.Get<Button>(GameContract.DesignerAskConfirm, box));
        }

        /// <summary>Cancel: the game's own method, which puts the question away.</summary>
        public static bool AnswerCancel()
        {
            DropdownSelectorUI box = Asker();
            if (box == null)
            {
                return false;
            }
            using (HotSpots.Game())
            {
                box.OnCancel();
            }
            return true;
        }
    }
}
