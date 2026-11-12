using System.Collections.Generic;
using BigAmbitions.SaveSystem;
using Buildings;
using Buildings.BuildingTypes.Special.PrivateDriverService;
using Helpers;
using Player.HUD.SmartphoneUI;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The phone's private driver (Player.HUD.SmartphoneUI.SmartphonePrivateDriverUI, opened by the phone's Private driver
    /// button) drawn as the kit's small panel beside the phone while it is open: over the world, taking no key of its own,
    /// so the phone's button still opens and closes it. Its pieces under the kit's are hidden by name, never switched off,
    /// so the game's own rules run as they are. Game side.
    /// </summary>
    public sealed class PrivateDriverKit : IUiGameScreen
    {
        public string Id => UiWorld.DriverId;

        public string Name => "the private driver";

        public string Title => UiText.T("ba:private_driver");

        public UiIcon Icon => UiIcon.Car;

        public UiGameKind Kind => UiGameKind.Overlay;

        public Component Find()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.smartphoneUI != null ? uis.smartphoneUI.privateDriverUI : null;
        }

        public string Check(Component found)
        {
            if (GameContract.UiWorldDriver.Off)
            {
                return GameContract.UiWorldDriver.Why;
            }
            return found.transform.Find(PrivateDriverScreen.Body) == null || found.transform.Find(PrivateDriverScreen.Headline) == null ? "its body or its headline is not there" : null;
        }

        public void Conceal(Component found, UiConceal conceal)
        {
            conceal.Add(found.transform.Find(PrivateDriverScreen.Body));
            conceal.Add(found.transform.Find(PrivateDriverScreen.Headline));
        }

        public IUiScreen Make(Component found) => new PrivateDriverScreen((SmartphonePrivateDriverUI)found);
    }

    /// <summary>
    /// The private driver as the kit's tool panel beside the phone, in one of three states as the game has them: nothing to
    /// call yet (no contract, or no car left in the firm's garage) with the way to the firm on the map; the cars to call,
    /// under the contract that drives them, the day's fee at its foot; or the car called, on its way or waiting, with the
    /// game's Dismiss. A call and a dismissal go through the game's own window, so its rules (one car at a time, the fee
    /// first, never from the subway) are its own. NPCs have no private driver of the game's, so there is no other side.
    /// Cost: a layout as something changes; four times a second a few reads.
    /// </summary>
    public sealed class PrivateDriverScreen : WorldToolScreen, IUiScreen
    {
        internal const string Body = "Body", Headline = "Headline";
        private const float Near = 20f;           // m: the car is "waiting" this close to the player
        private const int MostCars = UiToolPanel.MostLines - 3;

        private readonly SmartphonePrivateDriverUI _ui;
        private readonly int[] _carAt = new int[UiToolPanel.MostLines];   // a line's car in the save's list, or -1
        private int _dismissLine = -1, _locateLine = -1;

        public PrivateDriverScreen(SmartphonePrivateDriverUI ui)
        {
            _ui = ui;
        }

        public override string Id => UiWorld.DriverId;

        protected override string Scene => "private driver";

        /// <summary>What it shows now, for the tests: "empty", "list" or "called".</summary>
        public string State { get; private set; } = "empty";

        protected override long Signature()
        {
            PrivateDriverVehicle car = SmartphonePrivateDriverUI.CurrentVehicle;
            List<VehicleInstance> cars = SaveGameManager.Current != null ? SaveGameManager.Current.privateDriverVehicleInstances : null;
            long seen = car != null ? car.GetInstanceID() : 0;
            seen = Mix(seen, cars != null ? cars.Count : -1);
            seen = Mix(seen, SaveGameManager.Current != null && SaveGameManager.Current.activePrivateDriverContractUnpaid ? 1 : 0);
            seen = Mix(seen, SaveGameManager.Current != null && SaveGameManager.Current.activePrivateDriverContract != null ? SaveGameManager.Current.activePrivateDriverContract.GetHashCode() : 0);
            seen = Mix(seen, Riding() ? 1 : 0);
            seen = Mix(seen, car != null && Waiting(car) ? 1 : 0);
            seen = Mix(seen, PhoneKit.Rect != null ? 1 : 0);
            return seen;
        }

        protected override void Read()
        {
            for (int i = 0; i < _carAt.Length; i++)
            {
                _carAt[i] = -1;
            }
            _dismissLine = _locateLine = -1;
            M.Title = UiText.T("ba:private_driver");
            M.Icon = UiIcon.Car;
            M.Close = false;
            PrivateDriverVehicle car = SmartphonePrivateDriverUI.CurrentVehicle;
            List<VehicleInstance> cars = SaveGameManager.Current != null ? SaveGameManager.Current.privateDriverVehicleInstances : null;
            PrivateDriverContract contract = PrivateDriverHelpers.GetActiveContract();
            string firm = Firm();
            if (car != null)
            {
                Called(car);
            }
            else if (cars != null && cars.Count > 0)
            {
                List(cars, contract, firm);
            }
            else
            {
                Empty(contract, firm);
            }
        }

        // The car called: its picture and name, on its way or waiting, what the driver does, and the game's Dismiss.
        private void Called(PrivateDriverVehicle car)
        {
            State = "called";
            bool riding = Riding(), waiting = !riding && Waiting(car);
            string name = CarName(car.vehicleInstance);
            M.State = UiText.T(riding ? "reallife_ui_world_driver_state_riding" : waiting ? "reallife_ui_world_driver_state_waiting" : "reallife_ui_world_driver_state_coming");
            ref UiToolLine entry = ref Add(UiToolKind.Entry);
            entry.Label = name;
            entry.Value = UiText.T(riding ? "reallife_ui_world_driver_riding_line" : waiting ? "reallife_ui_world_driver_waiting_line" : "reallife_ui_world_driver_coming_line");
            entry.Picture = CarPicture(car.vehicleInstance);
            entry.Icon = UiIcon.Car;
            ref UiToolLine said = ref Add(UiToolKind.Text);
            said.Label = riding ? UiText.T("reallife_ui_world_driver_riding")
                : waiting ? WorldHost.Plain(UiText.T("ba:private_driver_waiting").Replace("{vehicleName}", name))
                : UiText.Fill("reallife_ui_world_driver_coming", "vehicle", name);
            _dismissLine = M.LineCount;
            ref UiToolLine dismiss = ref Add(UiToolKind.Buttons);
            dismiss.Label = UiText.Fill("reallife_ui_world_driver_dismiss", "vehicle", name);
            dismiss.Icon = UiIcon.Close;
            dismiss.Disabled = riding;   // the game sends no car away while the player rides in it
        }

        // The cars to call under the contract that drives them, the day's fee at the foot.
        private void List(List<VehicleInstance> cars, PrivateDriverContract contract, string firm)
        {
            State = "list";
            M.State = UiText.T("reallife_ui_world_driver_state_cars");
            if (contract != null)
            {
                ref UiToolLine head = ref Add(UiToolKind.Entry);
                head.Label = UiText.T(contract.key);
                head.Value = UiText.Fill("reallife_ui_world_driver_contract", "business", firm, "cars", UiText.Whole(cars.Count), "most", UiText.Whole(contract.maxCars),
                    "fee", UiText.Money(contract.costPerDay));
                head.Icon = UiIcon.Person;
            }
            bool riding = Riding();
            for (int i = 0; i < cars.Count && i < MostCars; i++)
            {
                VehicleInstance v = cars[i];
                if (v == null)
                {
                    continue;
                }
                _carAt[M.LineCount] = i;
                ref UiToolLine row = ref Add(UiToolKind.Entry);
                row.Label = CarName(v);
                row.Value = UiText.T("reallife_ui_world_driver_ready");
                row.Picture = CarPicture(v);
                row.Icon = UiIcon.Car;
                row.Action = UiText.T("reallife_ui_world_driver_call");
                row.Main = i == 0;
                row.Disabled = riding;
            }
            if (contract != null)
            {
                bool unpaid = SaveGameManager.Current != null && SaveGameManager.Current.activePrivateDriverContractUnpaid;
                ref UiToolLine foot = ref Add(UiToolKind.Foot);
                foot.Label = UiText.T(unpaid ? "reallife_ui_world_driver_fee_due" : "reallife_ui_world_driver_fee_paid");
                foot.Value = UiText.Money(contract.costPerDay);
            }
        }

        // Nothing to call: no contract yet, or no car left in the firm's garage; the way to the firm on the map.
        private void Empty(PrivateDriverContract contract, string firm)
        {
            State = "empty";
            M.State = UiText.T(contract == null ? "reallife_ui_world_driver_state_none" : "reallife_ui_world_driver_state_no_cars");
            ref UiToolLine said = ref Add(UiToolKind.Text);
            said.Label = contract == null ? UiText.Fill("reallife_ui_world_driver_none", "business", firm)
                : WorldHost.Plain(UiText.T("ba:private_driver_no_vehicles").Replace("{businessName}", firm));
            if (Service() != null)
            {
                _locateLine = M.LineCount;
                ref UiToolLine link = ref Add(UiToolKind.Link);
                link.Label = UiText.Fill("reallife_ui_world_driver_locate", "business", firm);
            }
        }

        // Beside the phone at its left, its top on the phone's; where the game's own window stands while the phone is the game's.
        protected override void Place(UiFrame frame, float w, float h, out float x, out float y)
        {
            if (Rect(PhoneKit.Rect, frame, out float px, out float py, out _, out _))
            {
                x = px - UiTokens.S3 - w;
                y = py;
            }
            else if (Rect(_ui.transform.Find(Body) as RectTransform, frame, out float bx, out float by, out float bw, out _))
            {
                x = bx + bw - w;
                y = by - UiFrame.Strip;
            }
            else
            {
                x = frame.Width - w - UiTokens.S8 - UiPhone.Width;
                y = UiTokens.S8;
            }
            x = Mathf.Clamp(x, UiTokens.S3, Mathf.Max(UiTokens.S3, frame.Width - w - UiTokens.S3));
            y = Mathf.Clamp(y, UiTokens.S3, Mathf.Max(UiTokens.S3, frame.BandY - h - UiTokens.S3));
        }

        protected override bool Line(int line)
        {
            if (line == _dismissLine)
            {
                _ui.DismissPrivateDriver();
                Touch();
                return true;
            }
            if (line == _locateLine)
            {
                return Locate();
            }
            int car = line >= 0 && line < _carAt.Length ? _carAt[line] : -1;
            List<VehicleInstance> cars = SaveGameManager.Current != null ? SaveGameManager.Current.privateDriverVehicleInstances : null;
            if (car < 0 || cars == null || car >= cars.Count || cars[car] == null)
            {
                return false;
            }
            _ui.OnClickVehicle(cars[car]);   // the game's own call: its fee, its refusals and its notices
            Touch();
            return true;
        }

        // ------------------------------------------------------------------------------------------------- reads

        private static bool Riding() => InstanceBehavior<TaxiSystem>.Instance != null && TaxiSystem.IsTraveling;

        private static bool Waiting(PrivateDriverVehicle car)
        {
            if (car == null || InstanceBehavior<GameManager>.Instance == null || InstanceBehavior<GameManager>.Instance.playerController == null)
            {
                return false;
            }
            return (car.transform.position - PlayerHelper.GetPosition()).sqrMagnitude <= Near * Near;
        }

        private SpecialService Service() => GameContract.DriverService.Field != null ? GameContract.DriverService.Field.GetValue(_ui) as SpecialService : null;

        private string Firm()
        {
            SpecialService service = Service();
            return service != null && !string.IsNullOrEmpty(service.businessName) ? service.businessName : UiText.T("ba:private_driver");
        }

        private static string CarName(VehicleInstance v) => v == null ? string.Empty : WorldHost.Plain(UiText.T(v.vehicleTypeName)) ?? v.vehicleTypeName;

        // The car's own picture, as BizMan shows a dealer's cars, or the game's car marker where it has none.
        private static string CarPicture(VehicleInstance v)
        {
            if (v == null || string.IsNullOrEmpty(v.vehicleTypeName))
            {
                return null;
            }
            Sprite sprite = AddressableResolver.IsValidAddressableKey(PlayerHelper.GetItemNameIconPath(v.vehicleTypeName)) ? ItemHelper.GetIcon(v.vehicleTypeName)
                : InstanceBehavior<GlobalReferences>.Instance != null ? InstanceBehavior<GlobalReferences>.Instance.vehiclePOIIcon : null;
            return WorldPictures.Of(sprite);
        }

        // The firm's building on the city map, as the game focuses one (CityMap.FocusOnBuilding).
        private bool Locate()
        {
            SpecialService service = Service();
            CityManager city = InstanceBehavior<CityManager>.Instance;
            CityBuildingController[] all = GameDlc.Open(city);
            if (service == null || all == null || city.cityMap == null || !CityMap.CanOpenMap())
            {
                return false;
            }
            for (int i = 0; i < all.Length; i++)
            {
                CityBuildingController cbc = all[i];
                if (cbc != null && cbc.building != null && ReferenceEquals(cbc.building.SpecialService, service))
                {
                    city.cityMap.FocusOnBuilding(cbc);
                    return true;
                }
            }
            return false;
        }
    }
}
