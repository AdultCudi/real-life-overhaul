using System;
using BigAmbitions.InteriorDesigner;
using BigAmbitions.InteriorDesigner.Tools;
using Buildings.Indoors;
using Extensions;
using UI.InteriorDesigner;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What the designer's face shows, read from the game into one model: the bar (the tools, the steps, the time, the money after this edit), the info panel (the business,
    /// what this edit costs, the customers an hour, the score, what the business needs), and the panel that rises over the tool in hand. Nothing is kept here that the game
    /// does not say; the same reads give the signature the face looks at ten times a second.
    /// </summary>
    internal static partial class DesignerGame
    {
        private static bool _infoFolded;
        private static int _itemCount = -1;

        /// <summary>The info panel folded to its head, as the player left it.</summary>
        public static bool InfoFolded
        {
            get => _infoFolded;
            set => _infoFolded = value;
        }

        /// <summary>Fills the model from the game's designer as it stands now.</summary>
        public static void Fill(UiDesignerModel m)
        {
            int tool = ToolInHand();
            FillBar(m, tool);
            FillInfo(m.Info);
            FillPanel(m, tool);
        }

        // ------------------------------------------------------------------ the bar

        private static void FillBar(UiDesignerModel m, int tool)
        {
            int known = (int)UiDesignerTool.Count, all = known + ExtraCount;
            if (m.Tools.Tools.Length != all)
            {
                m.Tools.Tools = new UiTool[all];
                m.ToolOff = new bool[all];
            }
            for (int i = 0; i < known; i++)
            {
                m.Tools.Tools[i] = new UiTool { Icon = DesignerGlyphs.ToolIcons[i], Name = UiText.T(DesignerGlyphs.ToolKeys[i]) };
                m.ToolOff[i] = !Offers(i);
            }
            for (int i = known; i < all; i++)
            {
                m.Tools.Tools[i] = new UiTool { Icon = UiIcon.Wrench, Name = ExtraName(i - known) };   // the kit knows no glyph of another mod's tool
                m.ToolOff[i] = ExtraOff(i - known);
            }
            m.Tools.Tool = tool;
            InteriorDesignerUI ui = Ui;
            int floor = FloorShown();
            for (int i = 0; i < (int)UiDesignerStep.Count; i++)
            {
                m.StepIcons[i] = DesignerGlyphs.StepIcons[i];
                m.StepNames[i] = UiText.T(DesignerGlyphs.StepKeys[i]);
                m.StepShown[i] = true;
                m.StepOff[i] = false;
            }
            m.StepShown[(int)UiDesignerStep.FloorDown] = m.StepShown[(int)UiDesignerStep.FloorUp] = floor >= 0;
            m.StepOff[(int)UiDesignerStep.Undo] = ui == null || !ui.HasChanges;
            m.StepOff[(int)UiDesignerStep.Redo] = !CanRedo();
            m.StepOff[(int)UiDesignerStep.FloorDown] = floor <= 0;
            m.StepOff[(int)UiDesignerStep.FloorUp] = floor >= 2;
            m.StepIcons[(int)UiDesignerStep.Walls] = DesignerGlyphs.WallsIcon(WallsLook());
            FillTimePill(m);
            m.Balance = _balanceAfter < double.MaxValue / 2.0 ? _balanceAfter.ToShortCurrencyFormat(abbreviated: true) : null;
            double spent = _costBalance;
            m.OnApply = Math.Abs(spent) >= 1.0 ? UiText.Fill("reallife_ui_designer_on_apply", "amount", UiText.Money((float)spent)) : null;
            m.OnApplyTrend = spent <= -1.0 ? -1 : spent >= 1.0 ? 1 : 0;
            m.ApplyLabel = UiText.T("common_apply");
            m.CloseLabel = UiText.T("common_close");
            m.ApplyOff = false;
        }

        // The walls' look as the game holds it: all shown (0), all hidden (1), some lowered (2).
        private static int WallsLook()
        {
            switch (WallsVisibilityHelper.currentWallsVisibility)
            {
                case WallsVisibility.AllVisible:
                    return 0;
                case WallsVisibility.AllHidden:
                    return 1;
                default:
                    return 2;
            }
        }

        // The time on the left of the bar and what the game's speed controller says of it, as the HUD's bar writes them.
        private static void FillTimePill(UiDesignerModel m)
        {
            UiStatusModel hud = HudStatusGame.Bar();
            if (hud == null)
            {
                return;
            }
            UiStatusModel bar = m.Bar;
            bar.Time = hud.Time;
            bar.Day = hud.Day;
            bar.DayWidest = hud.DayWidest;
            bar.Paused = hud.Paused;
            bar.Speed = hud.Speed;
            bar.TimeLocked = hud.TimeLocked;
            bar.FastOffered = hud.FastOffered;
            bar.Money = null;
            bar.Apps = null;
        }

        // ------------------------------------------------------------------ the info panel

        private static void FillInfo(UiDesignerInfo i)
        {
            i.Title = UiText.T("interior_designer");
            i.Folded = _infoFolded;
            i.CanFold = true;
            i.MarkTone = UiRole.Accent;
            FillHead(i);
            TallyEdit();
            FillCost(i);
            FillReadings(i);
            FillRequirements(i);
        }

        // ------------------------------------------------------------------ the panel over the tool

        private static void FillPanel(UiDesignerModel m, int tool)
        {
            m.Panel = UiDesignerPanelKind.None;
            if (FillQuestion(m.BusinessType))
            {
                m.Panel = UiDesignerPanelKind.BusinessType;
                return;
            }
            if (tool < 0)
            {
                return;
            }
            var which = (UiDesignerTool)tool;
            switch (which)
            {
                case UiDesignerTool.Furniture:
                    m.Panel = FillCatalogue(m.Catalogue) ? UiDesignerPanelKind.Furniture : UiDesignerPanelKind.None;
                    break;
                case UiDesignerTool.Floor:
                case UiDesignerTool.Wall:
                    m.Panel = FillMaterials(m.Materials) ? UiDesignerPanelKind.Materials : UiDesignerPanelKind.None;
                    break;
                case UiDesignerTool.Producer:
                case UiDesignerTool.Queue:
                case UiDesignerTool.Security:
                    m.Panel = FillItems(m.Items, which) ? UiDesignerPanelKind.Items : UiDesignerPanelKind.None;
                    break;
                case UiDesignerTool.TimeOfDay:
                    m.Panel = FillTime(m.Time) ? UiDesignerPanelKind.Time : UiDesignerPanelKind.None;
                    break;
                case UiDesignerTool.SaveBlueprint:
                    m.Panel = FillBlueprint(m.Blueprint) ? UiDesignerPanelKind.Blueprint : UiDesignerPanelKind.None;
                    break;
                case UiDesignerTool.Duplicate:
                case UiDesignerTool.Package:
                case UiDesignerTool.Palette:
                case UiDesignerTool.Eyedropper:
                case UiDesignerTool.Sell:
                    m.Note.Title = UiText.T(DesignerGlyphs.ToolKeys[tool]);
                    m.Note.Icon = DesignerGlyphs.ToolIcons[tool];
                    m.Note.Body = UiText.T(NoteKeys[tool]);
                    m.Panel = UiDesignerPanelKind.Note;
                    break;
            }
        }

        // What the mouse does with a tool that has no panel of its own, in the tool row's order.
        private static readonly string[] NoteKeys =
        {
            null, null, "reallife_ui_designer_note_duplicate", "reallife_ui_designer_note_package", null, null, "reallife_ui_designer_note_palette", "reallife_ui_designer_note_eyedropper",
            null, null, null, "reallife_ui_designer_note_sell", null, null
        };

        // ------------------------------------------------------------------ the signature

        /// <summary>
        /// A number that changes when anything the face shows changes: the tool, the steps, the money, the score, the items placed and what each panel holds. A few
        /// reads of the game's own state and no allocation; asked ten times a second while the designer shows.
        /// </summary>
        public static long Signature()
        {
            InteriorDesignerUI ui = Ui;
            if (ui == null)
            {
                return 0;
            }
            unchecked
            {
                long h = ToolInHand() + 2;
                h = h * 31 + (ui.HasChanges ? 1 : 0);
                h = h * 31 + (CanRedo() ? 1 : 0);
                h = h * 31 + FloorShown();
                h = h * 31 + WallsLook();
                h = h * 31 + (long)_balanceAfter;
                h = h * 31 + (long)_costBalance;
                h = h * 31 + Mathf.RoundToInt(_score * 10f);
                h = h * 31 + _capacityNow;
                BuildingRegistration reg = Registration();
                int count = reg != null && reg.itemInstances != null ? reg.itemInstances.Count : -1;
                if (count != _itemCount)
                {
                    _itemCount = count;
                    _capacityStale = true;   // a piece placed, sold or put back that no event told of: the readings are worked out again
                }
                h = h * 31 + count;
                h = h * 31 + (DropdownSelectorOpen() ? 1 : 0);
                h = h * 31 + (BlueprintOpen() ? 1 : 0);
                h = h * 31 + (InteriorElementTool.selectedPresetId != null ? InteriorElementTool.selectedPresetId.GetHashCode() : 0);
                h = h * 31 + InteriorElementTool.selectedPresetColorIndex;
                h = h * 31 + SheetsSignature();
                return h;
            }
        }

        private static bool DropdownSelectorOpen() => global::UI.InteriorDesigner.DropdownSelector.isOpen;

        // What the panels the kit mirrors hold: the catalogue's rows and switches, the lists, the time, the blueprint's name.
        private static long SheetsSignature()
        {
            long h = 0;
            unchecked
            {
                if (DesignerPanes.Furniture != null && DesignerPanes.Furniture.gameObject.activeInHierarchy)
                {
                    var scroller = BuyKits.Get<IDItemTemplateScrollingController>(GameContract.DesignerScroller, DesignerPanes.Furniture);
                    if (scroller != null && scroller.data != null)
                    {
                        h = h * 31 + scroller.data.Count;
                        if (scroller.data.Count > 0 && scroller.data[0].itemTemplates.Count > 0)
                        {
                            h = h * 31 + scroller.data[0].itemTemplates[0].itemName.GetHashCode();
                        }
                        if (scroller.data.Count > 0)
                        {
                            h = h * 31 + scroller.data[scroller.data.Count - 1].itemTemplates.Count;
                        }
                    }
                    var search = SearchField();
                    h = h * 31 + (search != null && search.text != null ? search.text.GetHashCode() : 0);
                    h = h * 31 + (FurnitureCategoryToggle.CurrentActiveToggle != null ? FurnitureCategoryToggle.CurrentActiveToggle.GetInstanceID() : 0);
                    h = h * 31 + (UiWidgets.On(BuyKits.Get<UnityEngine.UI.Toggle>(GameContract.DesignerFavToggle, DesignerPanes.Furniture)) ? 1 : 0);
                    h = h * 31 + _needFilter;
                    h = h * 31 + CatalogueSignature();
                }
                for (int t = (int)UiDesignerTool.Producer; t <= (int)UiDesignerTool.Security; t++)
                {
                    ItemActionPanelUI list = DesignerPanes.ForList((UiDesignerTool)t);
                    if (list == null || !list.gameObject.activeInHierarchy)
                    {
                        continue;
                    }
                    var controllers = BuyKits.Get<System.Collections.Generic.List<ItemController>>(GameContract.DesignerListControllers, list);
                    h = h * 31 + (controllers != null ? controllers.Count : 0);
                    h = h * 31 + BuyKits.Value(GameContract.DesignerListSelected, list, 0);
                }
                var slider = DesignerPanes.Daylight != null ? BuyKits.Get<UnityEngine.UI.Slider>(GameContract.DesignerSlider, DesignerPanes.Daylight) : null;
                h = h * 31 + (slider != null ? (long)slider.value : 0);
                if (BlueprintOpen())
                {
                    h = h * 31 + BlueprintName().GetHashCode();
                }
            }
            return h;
        }
    }
}
