using System;
using System.Collections.Generic;
using Buildings.BuildingTypes.Shared.BusinessRequirement;
using Helpers;
using Streets;
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
        // The values BizManInsight.FilterChanged answers to: "1" is yesterday by the hour, "7" the last week by the day
        // (decompiled BizManInsight.cs:206-249; RefreshData starts the chart on "7"); the kit's two tabs, in its order.
        private static readonly string[] PeriodValues = { "1", "7" };

        private bool PressPage(int command, int arg)
        {
            BizManBusiness b = _app.business;
            if (b == null || b.buildingRegistration == null)
            {
                return false;
            }
            if (_m.Ops.Card != null && (command == UiCard.ButtonCommand || command == UiCard.CheckCommand))
            {
                return PressSchedule(b, command, arg);   // a question over the page takes its own buttons, whose numbers the sidebar's share
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
                case BizManPages.Suppliers:
                    return OpenSuppliers(b);
                case BizManPages.FactLink:
                    if (arg == BizManPages.FactSuppliers)
                    {
                        return OpenSuppliers(b);
                    }
                    b.SetDestination();   // the game's own Set destination: its notice, its guide, the menu closed
                    return true;
                case UiFirmHead.First + UiFirmHead.Place:
                    b.SetDestination();
                    return true;
                case UiFirmHead.First + UiFirmHead.Switch:
                    return SetOpen(b);
                case BizManPages.LineAction:
                    return InteriorDo(arg);   // a button of a page written as lines: its own command, so no page action's number is ever taken for one
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
                    if (chart == null || arg < 0 || arg >= PeriodValues.Length)
                    {
                        return false;
                    }
                    chart.SetFilter(PeriodValues[arg]);   // by value, what the game's own period button calls, not by place in its list
                    return true;
                }
                case BizManPages.Goods + UiDataTable.Head:
                    return GoodsHead(b, arg);
                case BizManPages.Goods + UiDataTable.Less:
                case BizManPages.Goods + UiDataTable.More:
                    return StepGood(b, arg, command == BizManPages.Goods + UiDataTable.More);
                case BizManPages.Goods + UiDataTable.Type:
                    return true;
                case BizManPages.StartKinds + UiDataTable.Row:
                    return ChooseKind(arg);
                case BizManPages.StartKinds + UiDataTable.Clear:
                    return ClearKindSearch();
                case BizManPages.Item:
                {
                    List<DeliveryContractEntry> entries = Get<List<DeliveryContractEntry>>(GameContract.DealEntries, PageOf<BizManDeliveries>(b, BizManRules.Deliveries));
                    return entries != null && arg >= 0 && arg < entries.Count && Click(Get<Button>(GameContract.DealButton, entries[arg]), "open a delivery contract");
                }
                case BizManPages.Repeat:
                {
                    BizManContractSettings set = Get<BizManContractSettings>(GameContract.DealSettings, PageOf<BizManDeliveries>(b, BizManRules.Deliveries));
                    return set != null && Flip(set.repeatingOrderToggle);
                }
                case BizManPages.Toggle:
                    return arg >= 0 && arg < _campaigns.Count && Flip(_campaigns[arg]);
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
                    // The uniforms' editor is a canvas of the game's over the full menu (on the kit too, UniformsKit): BizMan's own
                    // view stands under it while it is open, and the kit comes back once it closes.
                    BizManKit.StepAside(UniformsKit.Over());
                    return Click(Calling(Page(b, BizManRules.Settings), "OnConfigureUniformPresetsButtonClick"), "set up uniforms");
                case BizManPages.LogoShape: return PressShape(arg);
                case BizManPages.LogoColour: case BizManPages.LogoColour + 1: case BizManPages.LogoColour + 2:
                    return PressColour(command - BizManPages.LogoColour, arg);
                case BizManPages.LogoColourAdd: return PressAddColour(arg);
                case BizManPages.LogoTab:
                    _logoTab = arg == 1 ? 1 : 0;
                    return true;
                case BizManPages.LogoOwn: return PressOwnShapes(b);
                case BizManPages.Slider:
                    _sliderHeld = !_sliderHeld;
                    return true;
                default:
                    return PressOps(b, command, arg);
            }
        }

        private static int DoOfHead(int index) => index == 0 ? BizManPages.DoEconoView : -1;

        // A toggle of the game's set the other way through itself, as a click sets it. One not there to set says which way, for the
        // page's line: the game holds it, or does not show it.
        private static bool Flip(Toggle t)
        {
            if (UiWidgets.Set(t, t != null && !t.isOn))
            {
                return true;
            }
            _why = t != null && t.gameObject.activeInHierarchy ? BizManRefusal.Held : BizManRefusal.Gone;
            return false;
        }

        // How the open switch reaches the game's toggle now (BizManRules.SwitchWay): by what the game's own objects say. A toggle out
        // of sight is set only where the game's own tab is what hides its header (the building's, the real estate's), and never in
        // a Going Public session: there another player's business stands as rented by the player, and what hides a control may be
        // the session's lock.
        private static BizManSwitchWay SwitchOf(BizManBusiness b, Toggle closed)
        {
            Transform row = closed != null ? closed.transform.parent : null;
            string tab = GameContract.BizTab.Field?.GetValue(b) as string;
            bool byTab = !GoingPublic.InSession && (tab == BizManRules.Presentation || tab == BizManRules.RealEstate);
            return BizManRules.SwitchWay(row != null, row != null && row.gameObject.activeSelf, closed != null && closed.gameObject.activeSelf,
                closed != null && closed.gameObject.activeInHierarchy, closed != null && closed.interactable, byTab);
        }

        // The open switch: the game's own "temporarily closed" toggle set through itself, so its listener (and any other mod's on it)
        // runs the game's opening or closing. The game keeps the toggle in its header, which it switches off on the building's and
        // the real estate's tabs; the head shows the switch there too, and a press there was refused with nothing said. It is set
        // there all the same. Opening is followed by what the opening is worth: the business's own state, and the utilities' look.
        private bool SetOpen(BizManBusiness b)
        {
            Toggle closed = Get<Toggle>(GameContract.BizClosed, b);
            BuildingRegistration reg = b.buildingRegistration;
            BizManSwitchWay way = SwitchOf(b, closed);
            if (closed == null || reg == null || way == BizManSwitchWay.None || way == BizManSwitchWay.Held)
            {
                _why = way == BizManSwitchWay.Held ? BizManRefusal.Held : BizManRefusal.Gone;
                UiDirector.Logger?.Info("BizMan (kit): the open switch " + (way == BizManSwitchWay.Held ? "is held by the game, so nothing was set" : "has no toggle of the game's to set"));
                return false;
            }
            Level(closed, reg);
            bool close = !reg.temporarilyClosed;   // what the business is, the other way
            bool done = way == BizManSwitchWay.Click ? UiWidgets.Set(closed, close) : UiWidgets.SetUnder(closed, close);
            UiDirector.Logger?.Info("BizMan (kit): " + (close ? "closing" : "opening") + " by the game's own toggle"
                + (way == BizManSwitchWay.Under ? ", under its header, which the game switches off on this tab" : string.Empty)
                + (done ? ": handed to the game" : " was refused by the toggle"));
            if (done && !close)
            {
                Opened(b, reg);
                Level(closed, reg);   // closed again by the utilities since: the toggle says so too, in the game's own view as well
            }
            return done;
        }

        // The game writes its toggle from the business when it fills the page, and a closing or opening that is no click on it (the
        // utilities', another player's in Going Public) leaves it saying the old state. Brought level the game's own way, without its
        // listener, so the next set is a change the listener hears and the head never shows one state while the business is in the other.
        private static void Level(Toggle closed, BuildingRegistration reg)
        {
            if (closed.isOn != reg.temporarilyClosed)
            {
                closed.SetIsOnWithoutNotify(reg.temporarilyClosed);
            }
        }

        // A business just opened by its switch: what the opening is worth, said once in Player.log (a warning when it will not trade
        // yet, so a report's log says why "open" is not open: a requirement missing, no opening hours today, the staff the game asks
        // for), and the utilities look at the place at once instead of at the turn of the hour, so one they must close again is
        // closed and told now, on the page the player is looking at.
        private void Opened(BizManBusiness b, BuildingRegistration reg)
        {
            string place = b.address != null ? b.address.ToFormattedString() : "no place";
            if (reg.temporarilyClosed)
            {
                UiDirector.Logger?.Warn("BizMan check (open): " + place + ": the game's toggle was set to open and the business is still closed: FAIL.");
                return;
            }
            string why;
            try
            {
                why = NotTrading(reg);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                why = null;   // a game update took something the look reads: it says nothing, and the opening stands
            }
            if (why != null)
            {
                UiDirector.Logger?.Warn("BizMan check (open): " + place + " is open by its switch and will not trade yet: " + why + ".");
            }
            else if (Meta.Dev.Verbose)
            {
                UiDirector.Logger?.Info("BizMan check (open): " + place + " is open by its switch, every requirement of the game's met, open by its hours and staffed as the game asks: PASS.");
            }
            // Closed again at once for want of power, water or a connection: the page says the utilities' own sentence, which is also their notice.
            _said = Utilities.UtilitiesDirector.Instance != null ? Utilities.UtilitiesDirector.Instance.LookAt(reg) : null;
        }

        // Why a business open by its switch will not trade yet, by the game's own tests: its requirements, its opening hours and its
        // own alert for a station with nobody at it. Null when nothing stands in its way. A method of its own, so a game update that
        // takes one of these leaves the press that called it standing.
        private static string NotTrading(BuildingRegistration reg)
        {
            BusinessType type = BusinessTypeHelper.GetData(reg);
            int needs = 0, missing = 0;
            string first = null;
            for (int i = 0; type != null && type.businessRequirements != null && i < type.businessRequirements.Count; i++)
            {
                BusinessRequirement need = type.businessRequirements[i];
                if (need == null)
                {
                    continue;
                }
                needs++;
                if (!BusinessHelper.IsRequirementMet(reg, need))
                {
                    missing++;
                    first = first ?? need.businessRequirementName;
                }
            }
            bool anyDay = false;
            for (int d = 0; reg.scheduleDays != null && d < reg.scheduleDays.Count; d++)
            {
                anyDay |= reg.scheduleDays[d] != null && reg.scheduleDays[d].isOpen;
            }
            ScheduleDay today = BuildingHelper.GetTodaySchedule(reg);
            return BizManRules.NotTrading(needs, missing, first, anyDay, today != null && today.isOpen, BusinessHelper.IsMissingEmployeeAlertNeeded(reg));
        }

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
            int page = index - _tabs.Count - (_extras.Count > 0 ? 1 : 0);
            if (page >= 0 && page < KitForOtherMods.PagesOfBizMan.Count)
            {
                using (HotSpots.Game())
                {
                    KitForOtherMods.PagesOfBizMan[page].Open();   // the mod's own page: its fault is blamed on it by the host (UiForeignGame.Caught)
                }
                return true;
            }
            if (index < 0 || index >= _tabs.Count)
            {
                return false;
            }
            string name = _tabs[index].name;
            if (!BizManRules.Drawn(name, Ops))
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

        // An action of a page: each the game's own button, found by the method the game wired it to. Every action has its own case
        // and nothing is asked before the cases (EconomyCheck holds both): a range asked first once took Start business for a button
        // of the Interior tab, which had none of that number, and the press came to nothing.
        private bool Do(BizManBusiness b, int what)
        {
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
                case BizManPages.DoRent:
                {
                    int heard = ShellNotices.Heard;
                    bool handed = Click(Calling(pres, "RentBuilding"), "rent the building");
                    if (handed)
                    {
                        Outcome("rent", b, b.buildingRegistration != null && b.buildingRegistration.RentedByPlayer, heard);
                    }
                    return handed;
                }
                case BizManPages.DoStart: return Click(Calling(pres, "SetStartBusiness"), "start a business");
                case BizManPages.DoTerminate:
                    return Click(Calling(_shown == BizManRules.Settings ? Page(b, BizManRules.Settings) : pres, "TerminateContract"), "terminate the lease (the game asks first)");
                case BizManPages.DoPreview: return Click(Calling(pres, "PreviewBuilding"), "preview the building");
                case BizManPages.DoBuy:
                {
                    HamptonsPurchaseBoxUI villa = Get<HamptonsPurchaseBoxUI>(GameContract.ShowVilla, pres);
                    Button send = On(villa) ? Calling(villa, "SendBuyBuildingOffer") : null;
                    return pres != null && Send(send ?? Calling(pres, "SendBuyBuildingOffer"), pres.SendBuyBuildingOffer, "send an offer for the building");
                }
                case BizManPages.DoTakeover:
                    return pres != null && Send(Calling(pres, "SendOvertakeOffer"), pres.SendOvertakeOffer, "send an offer to take the business over");
                case BizManPages.DoBrowse: return Click(Calling(pres, "BrowseInventory"), "browse the store's goods");
                case BizManPages.DoEmployees:
                    return pres != null && pres.showEmployeesButton != null && Click(pres.showEmployeesButton.GetComponentInChildren<Button>(true), "show the business's employees");
                case BizManPages.DoStartHere: return SetUpBusiness(b);
                case BizManPages.DoRandomName:
                    return Click(Calling(Get<StartBusinessUI>(GameContract.ShowStart, pres), "RandomizeName"), "a name from the game's own generator");
                case BizManPages.DoRandom: return Click(Calling(b.bizManSettings, "RandomizeBusinessName"), "a name from the game's own generator");
                case BizManPages.DoSave: return Click(Get<Button>(GameContract.SetSave, b.bizManSettings), "save the business's settings");
                case BizManPages.DoShutdown: return Click(Calling(b.bizManSettings, "ShutdownBusiness"), "shut down the business (the game asks first)");
                case BizManPages.DoSaveLogo: return Click(Get<Button>(GameContract.LogoSave, Get<LogoCustomizer>(GameContract.SetLogo, b.bizManSettings)), "save the logo");
                case BizManPages.DoLogoEdit: return OpenLogoEditor(b);   // the shapes and colours are the kit's page; each pick presses the game's own entry
                case BizManPages.DoLogoDone:
                    _logoEdit = false;
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

        // The Suppliers app, on the business this page shows (Scripts/Suppliers).
        private static bool OpenSuppliers(BizManBusiness b)
        {
            BuildingRegistration reg = b != null ? b.buildingRegistration : null;
            return reg != null && reg.Address != null && Suppliers.SupplierDirector.OpenApp(Suppliers.SupplierGame.Key(reg.Address));
        }

        // An offer's Send: the game's button while it shows, else the public method that button calls. The game keeps the box that
        // holds Send shut until its make-an-offer button is pressed, and the kit's amount is its own field already; the method
        // runs the game's checks and notices either way, and the patches of other mods (Going Public) hear it as they would the click.
        private static bool Send(Button shown, Action call, string what)
        {
            if (shown != null)
            {
                return Click(shown, what);
            }
            using (HotSpots.Game())
            {
                call();
            }
            UiDirector.Logger?.Info("BizMan (kit): " + what + ": handed to the game's own method");
            return true;
        }

        private BizManContractSettings Contract(BizManBusiness b) => Get<BizManContractSettings>(GameContract.DealSettings, PageOf<BizManDeliveries>(b, BizManRules.Deliveries));

        private static bool Pick(UI.Elements.Dropdown d, int option)
        {
            if (d == null)
            {
                _why = BizManRefusal.Gone;
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
                _why = f == null ? BizManRefusal.Gone : BizManRefusal.Held;
                return false;
            }
            double.TryParse(f.text, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.CurrentCulture, out double amount);
            double next = BizManRules.Stepped(amount, direction, 1.0);
            return UiWidgets.Type(f, next.ToString("F0", System.Globalization.CultureInfo.CurrentCulture), false);
        }

        // ------------------------------------------------------------------------------------------------- the goods' rows

        private bool GoodsHead(BizManBusiness b, int column)
        {
            if (_shown != BizManRules.Pricing || column < 0 || BizManRules.HeadOf(BizManRules.PriceHeads, column) == null)
            {
                return false;
            }
            InventoryProductsScrollerController table = b.bizManInventoryPricing != null ? b.bizManInventoryPricing.inventoryProductsScrollerController : null;
            Transform heads = table != null ? table.transform.Find("Headers") : null;
            Transform head = heads != null ? heads.Find(BizManRules.PriceHeads[column]) : null;
            return head != null && Click(head.GetComponent<Button>(), "sort the goods by " + BizManRules.PriceHeads[column]);
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
            bool any = _filterOpen >= 0 || _listOpen || _todoOpen || _kindOpen || _fontOpen || _sliderHeld || _pagesOpen || _logoEdit;
            _filterOpen = -1;
            _listOpen = _todoOpen = _kindOpen = _fontOpen = _sliderHeld = _pagesOpen = _logoEdit = false;   // Back from the logo editor lands on the settings
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
