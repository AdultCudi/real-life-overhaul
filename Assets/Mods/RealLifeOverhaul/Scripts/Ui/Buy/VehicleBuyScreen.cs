using System.Collections.Generic;
using System.Text;
using TMPro;
using UI;
using UI.PurchaseVehicle;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The vehicle purchase as the kit's info panel: the name and price, the specs, the colours, the delivery, the buttons.</summary>
    internal sealed class VehicleBuyScreen : IUiScreen
    {
        private const float Every = 0.25f;
        private readonly PurchaseVehicleUI _ui;
        private readonly UiVehicleBuyModel _m = new UiVehicleBuyModel();
        private readonly List<Transform> _specs = new List<Transform>(8), _colours = new List<Transform>(16);
        private readonly StringBuilder _sb = new StringBuilder(256);
        private string _seen;
        private float _next;
        private bool _dirty = true;

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
                    // The game's list cycles; with no warehouse its own notice says why, as a click on its list does.
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
                    list.SelectNextOption();
                    return Again(true);
                }
                case UiVehicleBuy.BuyCommand:
                    return Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarBuy, _ui)));
                case UiVehicleBuy.CancelCommand:
                    return Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarCancel, _ui)));
            }
            return false;
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back() => Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarCancel, _ui)));

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => action == UiAction.Alt && Again(UiWidgets.Press(BuyKits.Get<Button>(GameContract.CarBuy, _ui)));

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
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
            BuyKits.Copies(BuyKits.Get<Transform>(GameContract.CarSpecTemplate, _ui), _specs);
            if (_m.Specs.Length != _specs.Count)
            {
                _m.Specs = new UiInfoRow[_specs.Count];
            }
            for (int i = 0; i < _specs.Count; i++)
            {
                _m.Specs[i] = new UiInfoRow { Label = BuyKits.WordsAt(_specs[i], "Label"), Value = BuyKits.WordsAt(_specs[i], "Value") };
                _sb.Append(_m.Specs[i].Label).Append('=').Append(_m.Specs[i].Value).Append(';');
            }
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
            _m.DeliveryWords = words != null ? words.Replace("\n", " ") : null;
            _m.WarehouseChosen = list != null && list.SelectedOptionIndex >= 0;
            _m.Warehouse = _m.WarehouseChosen ? list.SelectedOption : UiText.T("dialog_select_address");
            _sb.Append(_m.Delivery).Append(_m.DeliveryOn).Append(_m.DeliveryWords).Append(_m.Warehouse).Append('|');
            Button buy = BuyKits.Get<Button>(GameContract.CarBuy, _ui), cancel = BuyKits.Get<Button>(GameContract.CarCancel, _ui);
            _m.Buy = buy != null && buy.gameObject.activeSelf ? BuyKits.Words(buy) : null;
            _m.BuyEnabled = buy != null && buy.interactable;
            _m.Cancel = cancel != null && cancel.gameObject.activeSelf ? BuyKits.Words(cancel) : null;
            _m.CancelEnabled = cancel != null && cancel.interactable;
            _sb.Append(_m.Buy).Append(_m.BuyEnabled).Append(_m.Cancel).Append(_m.CancelEnabled);
            return _sb.ToString();
        }
    }
}
