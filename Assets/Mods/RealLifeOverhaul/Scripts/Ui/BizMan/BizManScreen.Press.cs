using System;
using System.Collections.Generic;
using TMPro;
using UI.Smartphone.Apps.BizMan;
using UI.Smartphone.Apps.BizMan.StartBusiness;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// What a press on a place's page does: always the game's own button, or the public method a button of the game's calls
    /// where the game wired it in the Inspector, so its questions, its refusals and other mods' patches run as for a click.
    /// A row of a game table is brought into the game's own scroller first, so its own cell's buttons are the ones pressed.
    /// Cost: what each press does, only when pressed.
    /// </summary>
    public sealed partial class BizManScreen
    {
        private bool PressPage(int command, int arg)
        {
            BizManBusiness b = _app.business;
            if (b == null || b.buildingRegistration == null)
            {
                return false;
            }
            switch (command)
            {
                case BizManPages.Section: return Section(b, arg);
                case BizManPages.Classic: return ToClassic(b);
                case BizManPages.Page: return InteriorPage(arg);
                case BizManPages.Pages:
                    _pagesOpen = !_pagesOpen;
                    return true;
                case BizManPages.Heading: return true;   // a stop for the pad on a long page, nothing to do
                case UiFirmHead.First + UiFirmHead.Name:
                    _listOpen = !_listOpen;
                    _todoOpen = false;
                    return true;
                case UiFirmHead.First + UiFirmHead.Option:
                {
                    UI.Elements.Dropdown names = Get<UI.Elements.Dropdown>(GameContract.BizSwitch, b);
                    _listOpen = false;
                    if (names == null)
                    {
                        return false;
                    }
                    names.SelectOption(arg);   // the game's own choice: it refuses a nameless business and moves to the other place
                    UiDirector.Logger?.Info("BizMan (kit): another business chosen in the game's own list");
                    return true;
                }
                case UiFirmHead.First + UiFirmHead.Place:
                case BizManPages.FactLink:
                    b.SetDestination();   // the game's own Set destination: its notice, its guide, the menu closed
                    return true;
                case UiFirmHead.First + UiFirmHead.Switch:
                {
                    Toggle closed = Get<Toggle>(GameContract.BizClosed, b);
                    bool done = UiWidgets.Set(closed, closed != null && !closed.isOn);
                    UiDirector.Logger?.Info("BizMan (kit): temporarily closed set through the game's own toggle" + (done ? string.Empty : " was refused by the game"));
                    return done;
                }
                case UiFirmHead.First + UiFirmHead.Todo:
                    _todoOpen = !_todoOpen;
                    _listOpen = false;
                    return true;
                case UiFirmHead.First + UiFirmHead.Action:
                case BizManPages.Action:
                    return Do(b, command == BizManPages.Action ? arg : DoOfHead(arg));
                case BizManPages.Type:
                    return true;
                case BizManPages.Less:
                case BizManPages.More:
                    return StepAmount(arg, command == BizManPages.More ? 1 : -1);
                case BizManPages.Period:
                {
                    Extensions.ChartController chart = Get<Extensions.ChartController>(GameContract.SeeChart, b.bizManInsight);
                    if (chart == null || chart.filterOptions == null || arg < 0 || arg >= chart.filterOptions.Count)
                    {
                        return false;
                    }
                    chart.SetFilter(chart.filterOptions[arg].value);   // what the game's own period button calls
                    return true;
                }
                case BizManPages.Goods + UiDataTable.Head:
                    return GoodsHead(b, arg);
                case BizManPages.Goods + UiDataTable.Less:
                case BizManPages.Goods + UiDataTable.More:
                    return StepGood(b, arg, command == BizManPages.Goods + UiDataTable.More);
                case BizManPages.Goods + UiDataTable.Type:
                    return true;
                case UiCatalogue.First + UiCatalogue.Card:
                    return arg >= 0 && arg < _kindCards.Count && Click(Get<Button>(GameContract.KindSelect, _kindCards[arg].GetComponent<StartBusinessTypeUI>()), "choose " + _kindCards[arg].name);
                case UiCatalogue.First + UiCatalogue.Action:
                {
                    StartBusinessUI start = Get<StartBusinessUI>(GameContract.ShowStart, PageOf<BizManPresentation>(b, BizManRules.Presentation));
                    return Click(Calling(start, "SetUpBusiness"), "set up the business");
                }
                case BizManPages.Item:
                {
                    List<DeliveryContractEntry> entries = Get<List<DeliveryContractEntry>>(GameContract.DealEntries, PageOf<BizManDeliveries>(b, BizManRules.Deliveries));
                    return entries != null && arg >= 0 && arg < entries.Count && Click(Get<Button>(GameContract.DealButton, entries[arg]), "open a delivery contract");
                }
                case BizManPages.Repeat:
                {
                    BizManContractSettings set = Get<BizManContractSettings>(GameContract.DealSettings, PageOf<BizManDeliveries>(b, BizManRules.Deliveries));
                    return set != null && UiWidgets.Set(set.repeatingOrderToggle, !set.repeatingOrderToggle.isOn);
                }
                case BizManPages.Toggle:
                    return arg >= 0 && arg < _campaigns.Count && UiWidgets.Set(_campaigns[arg], !_campaigns[arg].isOn);
                case BizManPages.Cancel:
                {
                    Transform cancel = arg >= 0 && arg < _agencies.Count ? _agencies[arg].Find("CancelCampaign") : null;
                    return cancel != null && Click(cancel.GetComponent<Button>(), "cancel a campaign (the game asks first)");
                }
                case BizManPages.ItemLink:
                {
                    Transform call = arg >= 0 && arg < _contacts.Count ? _contacts[arg].Find("CallButton") : null;
                    return call != null && Click(call.GetComponent<Button>(), "call a marketing agency");
                }
                case BizManPages.Kind:
                    _kindOpen = !_kindOpen;
                    _fontOpen = false;
                    return true;
                case BizManPages.KindOption:
                    _kindOpen = false;
                    return Pick(Get<UI.Elements.Dropdown>(GameContract.SetKind, b.bizManSettings), arg);
                case BizManPages.Font:
                    _fontOpen = !_fontOpen;
                    _kindOpen = false;
                    return true;
                case BizManPages.FontOption:
                    _fontOpen = false;
                    return Pick(Get<UI.Elements.Dropdown>(GameContract.LogoFont, Get<LogoCustomizer>(GameContract.SetLogo, b.bizManSettings)), arg);
                case BizManPages.Uniforms:
                    // The uniforms' editor stands over BizMan in the game's own view: the classic view until the app closes.
                    BizManKit.Classic();
                    return Click(Calling(Page(b, BizManRules.Settings), "OnConfigureUniformPresetsButtonClick"), "set up uniforms");
                case BizManPages.Slider:
                    _sliderHeld = !_sliderHeld;
                    return true;
                default:
                    return false;
            }
        }

        private static int DoOfHead(int index) => index == 0 ? BizManPages.DoEconoView : -1;

        // A section chosen: a tab the kit draws is the game's own tab button pressed (its question about unsaved settings
        // first); a page only the game's view shows is its handover, the game's tab left as it is until it is opened there.
        private bool Section(BizManBusiness b, int index)
        {
            CloseLists();
            if (index == _tabs.Count && _extras.Count > 0)
            {
                _shown = ExtrasName;
                return true;
            }
            if (index < 0 || index >= _tabs.Count)
            {
                return false;
            }
            string name = _tabs[index].name;
            if (!BizManRules.Drawn(name))
            {
                _shown = name;
                return true;
            }
            bool pressed = PressTab(b, name);
            string now = GameContract.BizTab.Field?.GetValue(b) as string;
            _gameTab = now;
            _shown = now;
            return pressed;
        }

        private bool PressTab(BizManBusiness b, string name)
        {
            Transform menu = Menu(b);
            Transform tab = menu != null ? menu.Find(name) : null;
            return tab != null && Click(tab.GetComponent<Button>(), "the " + name + " tab");
        }

        // The game's own view of the page shown: its tab button pressed first where it has one, then the kit steps aside
        // until the app closes.
        private bool ToClassic(BizManBusiness b)
        {
            if (_shown != ExtrasName)
            {
                PressTab(b, _shown);
            }
            UiDirector.Logger?.Info("BizMan (kit): the game's own view for " + _shown + " until the app closes");
            BizManKit.Classic();
            return true;
        }

        // An action of a page: each the game's own button, found by the method the game wired it to.
        private bool Do(BizManBusiness b, int what)
        {
            if (what >= BizManPages.DoLine)
            {
                return InteriorDo(what - BizManPages.DoLine);
            }
            BizManPresentation pres = PageOf<BizManPresentation>(b, BizManRules.Presentation);
            switch (what)
            {
                case BizManPages.DoEconoView:
                {
                    Button own = Calling(b, "OpenInEconoView");
                    if (own != null)
                    {
                        return Click(own, "open in EconoView");
                    }
                    b.OpenInEconoView();   // the game's own path to EconoView on this business
                    return true;
                }
                case BizManPages.DoRent: return Click(Calling(pres, "RentBuilding"), "rent the building");
                case BizManPages.DoStart: return Click(Calling(pres, "SetStartBusiness"), "start a business");
                case BizManPages.DoTerminate:
                    return Click(Calling(_shown == BizManRules.Settings ? Page(b, BizManRules.Settings) : pres, "TerminateContract"), "terminate the lease (the game asks first)");
                case BizManPages.DoPreview: return Click(Calling(pres, "PreviewBuilding"), "preview the building");
                case BizManPages.DoBuy:
                {
                    HamptonsPurchaseBoxUI villa = Get<HamptonsPurchaseBoxUI>(GameContract.ShowVilla, pres);
                    Button send = On(villa) ? Calling(villa, "SendBuyBuildingOffer") : null;
                    return Click(send ?? Calling(pres, "SendBuyBuildingOffer"), "send an offer for the building");
                }
                case BizManPages.DoTakeover: return Click(Calling(pres, "SendOvertakeOffer"), "send an offer to take the business over");
                case BizManPages.DoBrowse: return Click(Calling(pres, "BrowseInventory"), "browse the store's goods");
                case BizManPages.DoEmployees:
                    return pres != null && pres.showEmployeesButton != null && Click(pres.showEmployeesButton.GetComponentInChildren<Button>(true), "show the business's employees");
                case BizManPages.DoRandomName:
                    return Click(Calling(Get<StartBusinessUI>(GameContract.ShowStart, pres), "RandomizeName"), "a name from the game's own generator");
                case BizManPages.DoRandom: return Click(Calling(b.bizManSettings, "RandomizeBusinessName"), "a name from the game's own generator");
                case BizManPages.DoSave: return Click(Get<Button>(GameContract.SetSave, b.bizManSettings), "save the business's settings");
                case BizManPages.DoShutdown: return Click(Calling(b.bizManSettings, "ShutdownBusiness"), "shut down the business (the game asks first)");
                case BizManPages.DoSaveLogo: return Click(Get<Button>(GameContract.LogoSave, Get<LogoCustomizer>(GameContract.SetLogo, b.bizManSettings)), "save the logo");
                case BizManPages.DoLogoClassic:
                    // The logo's shapes and colours are the game's own pickers: its own view until the app closes.
                    UiDirector.Logger?.Info("BizMan (kit): the game's own view for the logo's shapes and colours until the app closes");
                    BizManKit.Classic();
                    return true;
                case BizManPages.DoApplyRent: return Click(Get<Button>(GameContract.EstateApply, PageOf<RealEstateSettings>(b, BizManRules.RealEstate)), "apply the new rent");
                case BizManPages.DoTakeoverUnit: return Click(Get<Button>(GameContract.EstateTakeover, PageOf<RealEstateSettings>(b, BizManRules.RealEstate)), "take over the unit");
                case BizManPages.DoMarkSale: return Click(Calling(PageOf<RealEstateSettings>(b, BizManRules.RealEstate), "SetForSale"), "put the building up for sale (the game asks first)");
                case BizManPages.DoCancelSale: return Click(Calling(PageOf<RealEstateSettings>(b, BizManRules.RealEstate), "CancelForSale"), "take the building off the market");
                case BizManPages.DoOrder: return Click(Contract(b)?.orderButton, "start the deliveries");
                case BizManPages.DoUrgent: return Click(Contract(b)?.urgentOrderButton, "an urgent order (the game asks first)");
                case BizManPages.DoCancelContract: return Click(Contract(b)?.cancelContractButton, "stop the deliveries");
                case BizManPages.DoEndContract: return Click(Contract(b)?.endContractButton, "end the contract (the game asks first)");
                case BizManPages.DoClassic: return ToClassic(b);
                default: return false;
            }
        }

        private BizManContractSettings Contract(BizManBusiness b) => Get<BizManContractSettings>(GameContract.DealSettings, PageOf<BizManDeliveries>(b, BizManRules.Deliveries));

        private static bool Pick(UI.Elements.Dropdown d, int option)
        {
            if (d == null)
            {
                return false;
            }
            d.SelectOption(option);   // what a click on the game's own option does: its listeners hear it
            return true;
        }

        // ------------------------------------------------------------------------------------------------- amounts

        // The field a typed command types into: the list's search, a name, an offer, a price for the building, a sale price,
        // or a row's price or amount in the game's own cell.
        private TMP_InputField Field(int command, int arg)
        {
            BizManBusiness b = _app.business;
            if (command == UiDataTable.First + UiDataTable.Search)
            {
                return SearchField();
            }
            if (b == null || !b.gameObject.activeSelf)
            {
                return null;
            }
            BizManPresentation pres = PageOf<BizManPresentation>(b, BizManRules.Presentation);
            if (command == BizManPages.Goods + UiDataTable.Type)
            {
                return GoodField(b, arg);
            }
            switch (arg)
            {
                case BizManPages.FieldName:
                    return _shown == BizManRules.Settings ? Get<TMP_InputField>(GameContract.SetName, b.bizManSettings)
                        : Get<TMP_InputField>(GameContract.StartName, Get<StartBusinessUI>(GameContract.ShowStart, pres));
                case BizManPages.FieldOffer:
                    return pres != null ? pres.offerAmountInputField : null;
                case BizManPages.FieldBuy:
                {
                    HamptonsPurchaseBoxUI villa = Get<HamptonsPurchaseBoxUI>(GameContract.ShowVilla, pres);
                    return On(villa) ? villa.offerInputField : pres != null ? pres.buyBuildingAmountInputField : null;
                }
                case BizManPages.FieldSale:
                    return Get<TMP_InputField>(GameContract.EstatePrice, PageOf<RealEstateSettings>(b, BizManRules.RealEstate));
                default:
                    return null;
            }
        }

        // An offer or a sale price stepped by a round hundredth of itself, written into the game's own field as typing would.
        private bool StepAmount(int field, int direction)
        {
            TMP_InputField f = Field(BizManPages.Type, field);
            if (f == null || !f.interactable)
            {
                return false;
            }
            double.TryParse(f.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double amount);
            double next = BizManRules.Stepped(amount, direction, 1.0);
            return UiWidgets.Type(f, next.ToString("F0", System.Globalization.CultureInfo.CurrentCulture), false);
        }

        // ------------------------------------------------------------------------------------------------- the goods' rows

        private bool GoodsHead(BizManBusiness b, int column)
        {
            if (_shown != BizManRules.Pricing || column < 0 || column >= PriceHeads.Length)
            {
                return false;
            }
            InventoryProductsScrollerController table = b.bizManInventoryPricing != null ? b.bizManInventoryPricing.inventoryProductsScrollerController : null;
            Transform heads = table != null ? table.transform.Find("Headers") : null;
            Transform head = heads != null ? heads.Find(PriceHeads[column]) : null;
            return head != null && Click(head.GetComponent<Button>(), "sort the goods by " + PriceHeads[column]);
        }

        // A row's minus or plus, pressed on the game's own cell for that row once its scroller has brought it in.
        private bool StepGood(BizManBusiness b, int row, bool more)
        {
            if (_shown == BizManRules.Pricing && Cell(b, row) is InventoryProductCellView price)
            {
                return Click(more ? price.retailPricePlusButton : price.retailPriceMinusButton, "a price stepped");
            }
            if (_shown == BizManRules.Deliveries && Cell(b, row) is BizManDeliveriesProductCellView amount)
            {
                return Click(more ? amount.targetPlusButton : amount.targetMinusButton, "an amount stepped");
            }
            return false;
        }

        private TMP_InputField GoodField(BizManBusiness b, int row)
        {
            if (_shown == BizManRules.Pricing && Cell(b, row) is InventoryProductCellView price && price.retailPrice != null)
            {
                return price.retailPrice.tmpInputField;
            }
            if (_shown == BizManRules.Deliveries && Cell(b, row) is BizManDeliveriesProductCellView amount && amount.deliveryAmount != null)
            {
                return amount.deliveryAmount.tmpInputField;
            }
            return null;
        }

        // The game's own cell for a row of the shown table, its scroller jumped to it so the cell is laid and live.
        private Component Cell(BizManBusiness b, int row)
        {
            EnhancedUI.EnhancedScroller.EnhancedScroller scroller = null;
            if (_shown == BizManRules.Pricing)
            {
                InventoryProductsScrollerController table = b.bizManInventoryPricing != null ? b.bizManInventoryPricing.inventoryProductsScrollerController : null;
                scroller = table != null && row >= 0 && row < table.data.Count ? table.scroller : null;
            }
            else if (_shown == BizManRules.Deliveries)
            {
                BizManDeliveriesProductsScrollerController table = Contract(b)?.bizManDeliveriesProductsScrollerController;
                scroller = table != null && row >= 0 && row < table.data.Count ? table.scroller : null;
            }
            if (scroller == null)
            {
                return null;
            }
            scroller.JumpToDataIndex(row);
            return scroller.GetCellViewAtDataIndex(row);
        }

        // ------------------------------------------------------------------------------------------------- back

        private bool CloseLists()
        {
            bool any = _filterOpen >= 0 || _listOpen || _todoOpen || _kindOpen || _fontOpen || _sliderHeld || _pagesOpen;
            _filterOpen = -1;
            _listOpen = _todoOpen = _kindOpen = _fontOpen = _sliderHeld = _pagesOpen = false;
            return any;
        }

        // From a place back to the portfolio, as the game's own back does: settings not saved are asked about first, and a
        // business without a name is refused, as the game's tab buttons do.
        private void BackToList()
        {
            BizManBusiness b = _app.business;
            BizManSettings set = b != null ? b.bizManSettings : null;
            if (set != null && set.HasUnsavedChanges)
            {
                if (b.buildingRegistration != null && string.IsNullOrEmpty(b.buildingRegistration.BusinessName))
                {
                    UI.Notification.Notifications.ShowError("bizman_settings_notification_name_empty");
                    return;
                }
                GameConfirm.Show(null, "change_character_clothes_unsaved_changes_warning", Guard.Wrap(OpenList), null, null, null, true, true);
                return;
            }
            OpenList();
        }

        private void OpenList()
        {
            _app.Open();   // the game's own way to its list
            _dirty = true;
        }

        // The new rent's slider stepped a twentieth of its way, through the game's own slider.
        private void StepRent(int step)
        {
            BizManBusiness b = _app.business;
            Slider slider = b != null ? Get<Slider>(GameContract.EstateSlider, PageOf<RealEstateSettings>(b, BizManRules.RealEstate)) : null;
            if (slider != null)
            {
                UiWidgets.Slide(slider, Mathf.Clamp(slider.value + step * (slider.maxValue - slider.minValue) / 20f, slider.minValue, slider.maxValue));
                _dirty = true;
            }
        }
    }
}
