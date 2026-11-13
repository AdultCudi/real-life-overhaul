using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Controllers;
using Localizor;
using TMPro;
using UI;
using UI.PurchaseVehicle;
using UnityEngine;
using UnityEngine.UI;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The vehicle purchase as the kit's info panel: the name and price, the specs, the colours, the delivery, the buttons.</summary>
    internal sealed class VehicleBuyScreen : IUiScreen
    {
        private const float Every = 0.25f;
        private readonly PurchaseVehicleUI _ui;
        private readonly UiVehicleBuyModel _m = new UiVehicleBuyModel();
        private readonly List<Transform> _specs = new List<Transform>(8), _colours = new List<Transform>(16);
        private readonly List<UiInfoRow> _rows = new List<UiInfoRow>(8);
        private readonly List<ShowcaseVehicleController> _lot = new List<ShowcaseVehicleController>(16);
        private static readonly Comparison<ShowcaseVehicleController> Cheapest = (a, b) =>
        {
            int c = a.GetPurchasePrice().CompareTo(b.GetPurchasePrice());
            return c != 0 ? c : string.CompareOrdinal(a.vehicleName, b.vehicleName);
        };
        private static readonly string[] KindKeys =
        {
            "reallife_ui_buy_carkind_1", "reallife_ui_buy_carkind_2", "reallife_ui_buy_carkind_3", "reallife_ui_buy_carkind_4", "reallife_ui_buy_carkind_5",
            "reallife_ui_buy_carkind_6", "reallife_ui_buy_carkind_7"
        };
        private IPurchasableAsset _of;            // the vehicle the mod's figures and the lot were read for
        private string _kind, _top, _to100, _insurance, _fuel, _seats, _topWords, _lotWords;
        private int _lotAt = -1;
        private readonly StringBuilder _sb = new StringBuilder(256);
        private string _seen;
        private float _next;
        private bool _dirty = true, _warehouseOpen;
        private float _warehouseOffset;

        public VehicleBuyScreen(PurchaseVehicleUI ui)
        {
            _ui = ui;
            _m.Title = UiText.T("common_vehicle");
            _m.SpecsLabel = UiText.T("purchasecar_specs");
            _m.ColourLabel = UiText.T("reallife_ui_buy_colour");
            _m.Prompts = BuyKits.Prompts();
            _m.PromptCount = _m.Prompts.Length;
        }

        public string Id => BuyKits.VehicleId;

        public bool Dirty
        {
            get
            {
                if (Time.unscaledTime >= _next)
                {
                    _next = Time.unscaledTime + Every;
                    string now = Read();
                    _dirty |= now != _seen;
                    _seen = now;
                }
                return _dirty;
            }
        }

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            if (_seen == null)
            {
                _seen = Read();
            }
            UiVehicleBuy.Lay(s, _m, frame, dark, keys);
            _dirty = false;
        }

        public bool Press(int command, int arg)
        {
            switch (command)
            {
                case UiVehicleBuy.SwatchCommand:
                    return arg >= 0 && arg < _colours.Count && _colours[arg] != null && Again(UiWidgets.Press(_colours[arg].GetComponent<Button>()));
                case UiVehicleBuy.DeliveryCommand:
                {
                    Toggle toggle = BuyKits.Get<Toggle>(GameContract.CarDelivery, _ui);
                    return toggle != null && Again(UiWidgets.Set(toggle, !toggle.isOn));
                }
                case UiVehicleBuy.WarehouseCommand:
                {
                    // The game's list opens under its field; with no warehouse its own notice says why, as a click on its list does.
                    var list = BuyKits.Get<UI.Elements.Dropdown>(GameContract.CarWarehouse, _ui);
                    var options = BuyKits.Get<List<string>>(GameContract.CarWarehouses, _ui);
                    if (list == null)
                    {
                        return false;
                    }
                    if (options == null || options.Count == 0)
                    {
                        list.onMiddleBoxClicked?.Invoke();
                        return false;
                    }
                    _warehouseOpen = !_warehouseOpen;
                    _warehouseOffset = 0f;
                    return Again(true);
                }
                case UiVehicleBuy.WarehouseOptionCommand:
                {
                    var list = BuyKits.Get<UI.Elements.Dropdown>(GameContract.CarWarehouse, _ui);
                    var options = BuyKits.Get<List<string>>(GameContract.CarWarehouses, _ui);
                    _warehouseOpen = false;
                    if (list == null || options == null || arg < 0 || arg >= options.Count)
                    {
                        return Again(true);
                    }
                    list.SelectOption(arg);   // what a click on the game's own option does: its listener prices the delivery
                    return Again(true);
                }
                case UiVehicleBuy.BuyCommand:
                    return Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarBuy, _ui)));
                case UiVehicleBuy.CancelCommand:
                    return Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarCancel, _ui)));
                case UiVehicleBuy.LotCommand:
                    return Walk(arg);
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_warehouseOpen)
            {
                _warehouseOpen = false;
                return Again(true);
            }
            return Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarCancel, _ui)));
        }

        public void Tab(int step) => Walk(step);

        public bool Act(UiAction action) => action == UiAction.Alt && Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarBuy, _ui)));

        public void Reveal(int list, int index)
        {
            if (_warehouseOpen)
            {
                float top = index * UiTokens.ControlSmall;
                _warehouseOffset = Math.Min(_warehouseOffset, top);
                _warehouseOffset = Math.Max(_warehouseOffset, top + UiTokens.ControlSmall - UiTokens.ControlSmall * 8f);
                _dirty = true;
            }
        }

        public void Wheel(int clip, float notches)
        {
            if (_warehouseOpen)
            {
                _warehouseOffset = Math.Max(0f, _warehouseOffset - notches * UiTokens.ControlSmall);
                _dirty = true;
            }
        }

        private bool Again(bool done)
        {
            if (done)
            {
                _next = 0f;
                _dirty = true;
            }
            return done;
        }

        // The panel as the game filled it for the vehicle shown. Returns what the screen shows, to see a change by.
        private string Read()
        {
            _sb.Length = 0;
            _m.Name = BuyKits.Words(BuyKits.Get<Component>(GameContract.CarName, _ui));
            _m.Price = UiWidgets.Text(BuyKits.Get<TMP_Text>(GameContract.CarPrice, _ui));
            _sb.Append(_m.Name).Append('|').Append(_m.Price).Append('|');
            IPurchasableAsset asset = BuyKits.Get<IPurchasableAsset>(GameContract.CarAsset, _ui);
            if (!ReferenceEquals(asset, _of))
            {
                _of = asset;
                Figures(asset as ShowcaseVehicleController);
                ReadLot(asset as ShowcaseVehicleController);
            }
            _m.Facts = _kind;
            _m.Lot = _lotWords;
            BuyKits.Copies(BuyKits.Get<Transform>(GameContract.CarSpecTemplate, _ui), _specs);
            _rows.Clear();
            for (int i = 0; i < _specs.Count; i++)
            {
                string label = BuyKits.WordsAt(_specs[i], "Label");
                bool top = _top != null && label != null && label == _topWords;   // the game's tier, the mod's top speed in its place
                _rows.Add(new UiInfoRow { Label = label, Value = top ? _top : BuyKits.WordsAt(_specs[i], "Value") });
                if (top && _to100 != null)
                {
                    _rows.Add(new UiInfoRow { Label = UiText.T("reallife_tuning_fig_to100"), Value = _to100 });
                }
            }
            if (_fuel != null && _specs.Count > 0)
            {
                _rows.Add(new UiInfoRow { Label = UiText.T("reallife_ui_buy_fuel_use"), Value = _fuel });
            }
            if (_seats != null && _specs.Count > 0)
            {
                _rows.Add(new UiInfoRow { Label = UiText.T("reallife_ui_buy_seats"), Value = _seats });
            }
            if (_insurance != null && _specs.Count > 0)
            {
                _rows.Add(new UiInfoRow { Label = UiText.T("reallife_ui_buy_insurance"), Value = _insurance });
            }
            if (_m.Specs.Length != _rows.Count)
            {
                _m.Specs = new UiInfoRow[_rows.Count];
            }
            _rows.CopyTo(_m.Specs);
            for (int i = 0; i < _rows.Count; i++)
            {
                _sb.Append(_rows[i].Label).Append('=').Append(_rows[i].Value).Append(';');
            }
            _sb.Append(_m.Facts).Append('|').Append(_m.Lot).Append('|');
            BuyKits.Copies(BuyKits.Get<Transform>(GameContract.CarColourTemplate, _ui), _colours);
            if (_m.Swatches.Length != _colours.Count)
            {
                _m.Swatches = new string[_colours.Count];
            }
            _m.Colour = -1;
            for (int i = 0; i < _colours.Count; i++)
            {
                var image = _colours[i].GetComponent<Image>();
                _m.Swatches[i] = UiDraw.SwatchKey(image != null ? BuyKits.Rgb(image.color) : 0x808080);
                Transform chosen = _colours[i].Find("Selected");
                if (chosen != null && chosen.gameObject.activeSelf)
                {
                    _m.Colour = i;
                }
                _sb.Append(_m.Swatches[i]);
            }
            _sb.Append(_m.Colour).Append('|');
            GameObject delivery = BuyKits.Get<GameObject>(GameContract.CarDeliveryPart, _ui);
            Toggle toggle = BuyKits.Get<Toggle>(GameContract.CarDelivery, _ui);
            var list = BuyKits.Get<UI.Elements.Dropdown>(GameContract.CarWarehouse, _ui);
            _m.Delivery = delivery != null && delivery.activeSelf;
            _m.DeliveryOn = toggle != null && toggle.isOn;
            string words = UiWidgets.Text(BuyKits.Get<TMP_Text>(GameContract.CarDeliveryPrice, _ui));
            int cut = words != null ? words.IndexOf('\n') : -1;
            _m.DeliveryWords = cut >= 0 ? words.Substring(0, cut) : words;   // the game writes "Deliver to Warehouse" and, under it, "(fee)": two lines, as it shows them
            _m.DeliveryDetail = cut >= 0 ? words.Substring(cut + 1).Trim() : null;
            _m.DeliveryState = UiText.T(_m.DeliveryOn ? "reallife_meta_on" : "reallife_meta_off");
            _m.WarehouseChosen = list != null && list.SelectedOptionIndex >= 0;
            _m.Warehouse = _m.WarehouseChosen ? list.SelectedOption : UiText.T("dialog_select_address");
            var warehouses = BuyKits.Get<List<string>>(GameContract.CarWarehouses, _ui);
            int count = warehouses != null ? warehouses.Count : 0;
            if (_m.WarehouseOptions.Length != count)
            {
                _m.WarehouseOptions = new string[count];
            }
            for (int i = 0; i < count; i++)
            {
                _m.WarehouseOptions[i] = warehouses[i];
            }
            _m.WarehouseAt = list != null ? list.SelectedOptionIndex : -1;
            _warehouseOpen &= _m.Delivery && count > 0;   // the list goes with the delivery it belongs to
            _m.WarehouseOpen = _warehouseOpen;
            _m.WarehouseOffset = _warehouseOffset;
            _sb.Append(_m.Delivery).Append(_m.DeliveryOn).Append(_m.DeliveryWords).Append(_m.DeliveryDetail).Append(_m.Warehouse).Append(_m.WarehouseAt).Append(count).Append(_warehouseOpen)
                .Append(_warehouseOffset).Append('|');
            Button buy = BuyKits.Get<Button>(GameContract.CarBuy, _ui), cancel = BuyKits.Get<Button>(GameContract.CarCancel, _ui);
            _m.Buy = buy != null && buy.gameObject.activeSelf ? BuyKits.Words(buy) : null;
            _m.BuyEnabled = buy != null && buy.interactable;
            _m.Cancel = cancel != null && cancel.gameObject.activeSelf ? BuyKits.Words(cancel) : null;
            _m.CancelEnabled = cancel != null && cancel.interactable;
            _sb.Append(_m.Buy).Append(_m.BuyEnabled).Append(_m.Cancel).Append(_m.CancelEnabled);
            return _sb.ToString();
        }

        // The mod's figures for the car on sale, once a car: its kind (Driving/CarCatalogue, as the dealers and the mod's NPC
        // buyers read it), the top speed and 0 to 100 km/h the mod's tuning gives a new one of its kind (Driving/CarTuning,
        // before any custom shop's parts) and a month of the mod's insurance at its list price (Transport/CarPhone). None for
        // a boat or what the mod leaves as the game made it; each with its option off.
        private void Figures(ShowcaseVehicleController car)
        {
            _kind = _top = _to100 = _insurance = _fuel = _seats = null;
            VehicleType t = car != null && !string.IsNullOrEmpty(car.vehicleName) ? VehicleTypeHelper.GetVehicleType(car.vehicleName) : null;
            if (t == null)
            {
                return;
            }
            if (Settings.CarOwnership && t.IsMotorVehicle)
            {
                _insurance = UiText.T("reallife_ui_buy_a_month").Replace("{price}", UiText.Money(CarPhone.PremiumOf(t.price, 0, 0f)));
            }
            CarSpec spec = CarCatalogue.For(t.vehicleTypeName, new CarTraits(t.IsMotorVehicle, t.spawnInPlayerObject, t.maxSpeed, t.maxCargoCapacity, t.isLuxuryCar, 1200f));
            int kind = (int)spec.Kind;
            if (kind < 1 || kind > KindKeys.Length)
            {
                return;
            }
            _kind = UiText.T(KindKeys[kind - 1]);
            _seats = CarCatalogue.SeatsKnown(t.vehicleTypeName) ? UiText.Whole(CarCatalogue.Seats(t.vehicleTypeName, spec.Kind)) : null;   // a guess by kind is not shown
            float per100 = t.IsMotorVehicle ? FuelUseRules.Per100(t.enginePower, t.maxSpeed) : 0f;   // the game's own reckoning, as the item panel's range
            if (per100 > 0f)
            {
                bool us = PlayerPrefSettings.useImperial;
                _fuel = UiText.T(us ? "reallife_ui_buy_mpg" : "reallife_ui_buy_l100").Replace("{value}", (us ? FuelUseRules.MilesAGallon(per100) :
                    per100).ToString(us ? "0" : "0.0", CultureInfo.InvariantCulture));
            }
            if (!Settings.CarTuning)
            {
                return;
            }
            CarTuning.Targets(spec, out float seconds, out float topKmh);
            bool imperial = PlayerPrefSettings.useImperial;
            _top = Mathf.RoundToInt(imperial ? topKmh * BuyKits.MilesPerKm : topKmh).ToString(CultureInfo.InvariantCulture) + (imperial ? " mph" : " km/h");
            _to100 = UiText.T("reallife_ui_buy_seconds").Replace("{seconds}", seconds.ToString("0.0", CultureInfo.InvariantCulture));
            _topWords = "vehicle_max_speed".GetLocalization();   // the game's row for its own tier, as its label reads
        }

        // The cars the player can buy where this one stands (the showcase cars the game offers for purchase), cheapest first,
        // and this one's place among them; no lot for a boat or a car alone.
        private void ReadLot(ShowcaseVehicleController car)
        {
            _lot.Clear();
            _lotAt = -1;
            _lotWords = null;
            List<ItemController> items = car != null && InstanceBehavior<BuildingManager>.IsInitialized ? InstanceBehavior<BuildingManager>.Instance.allItemControllers : null;
            for (int i = 0; items != null && i < items.Count; i++)
            {
                if (items[i] is ShowcaseVehicleController other && other != null && other.playerItemPurchaserSettings != null && other.playerItemPurchaserSettings.enabled
                    && !string.IsNullOrEmpty(other.vehicleName) && VehicleTypeHelper.GetVehicleType(other.vehicleName) != null)
                {
                    _lot.Add(other);
                }
            }
            _lot.Sort(Cheapest);
            _lotAt = _lot.IndexOf(car);
            if (_lot.Count > 1 && _lotAt >= 0)
            {
                _lotWords = UiText.T("reallife_ui_buy_lot").Replace("{place}", (_lotAt + 1).ToString(CultureInfo.InvariantCulture))
                    .Replace("{count}", _lot.Count.ToString(CultureInfo.InvariantCulture));
            }
        }

        // The next car on the lot either way, as a click on it does: this panel closes (the game puts back a colour not
        // bought), the player walks to that car and its purchase opens. Not while the game's showcase animation runs.
        private bool Walk(int step)
        {
            if (_lotWords == null || step == 0 || PurchaseVehicleUI.IsShowcaseAnimationRunning)
            {
                return false;
            }
            ShowcaseVehicleController next = _lot[((_lotAt + (step > 0 ? 1 : -1)) % _lot.Count + _lot.Count) % _lot.Count];
            if (next == null)
            {
                return false;
            }
            _ui.Close();
            next.MoveTowardsEntity(next.ShowPurchaseUi);
            return true;
        }
    }
}
