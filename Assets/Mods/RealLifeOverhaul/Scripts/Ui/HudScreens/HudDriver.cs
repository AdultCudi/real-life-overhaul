using System;
using System.Collections.Generic;
using System.Runtime.CompilerServices;
using Helpers;
using Player.HUD.SmartphoneUI;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The private driver as a button in the bar's pill of small tools, for the player who uses the phone for nothing else
    /// (<see cref="HudDriverRules"/>). Its state is read from the game's own (the contract, the cars in the garage, the car
    /// called, the ride); a press is the game's: the phone's own driver button where it shows (its listener opens or shuts the
    /// card the kit draws, PrivateDriverKit), the same method of the card's own window where the phone is folded and the button
    /// is hidden, and the card's own car button for the garage's only car (the fee, the refusals and the notices are the
    /// game's). The Driver key does the same. Cost: four times a second, with the bar, a few field reads; a key test a
    /// frame while the button shows; nothing allocated.
    /// </summary>
    internal static class HudDriver
    {
        private static bool _shown;     // the button is on the bar now: the key answers
        private static int _cars;
        private static VehicleInstance _only;
        private static PrivateDriverVehicle _car;
        private static bool _cardOpen;
        private static DriverState _state = DriverState.None;
        private static int _pressedFrame = -1;   // the frame a key press was last answered: the kit's watch and the old bar's both read the key

        /// <summary>The cars in the garage, as last read.</summary>
        public static int Cars => _cars;

        /// <summary>The called car's name, as last read; empty while none is called.</summary>
        public static string CarName() => _car != null && _car.vehicleInstance != null ? PrivateDriverScreen.CarName(_car.vehicleInstance) : string.Empty;

        /// <summary>The bar's read: whether the button shows, its state, and whether the card is open.</summary>
        public static bool Look(out DriverState state, out bool open)
        {
            state = DriverState.None;
            open = false;
            _shown = false;
            try
            {
                Read(out state, out open);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's private driver button");
                state = DriverState.None;
                open = false;
            }
            _state = state;
            _cardOpen = open;
            _shown = state != DriverState.None;
            return _shown;
        }

        /// <summary>The bar is given back: the key no longer answers, and nothing of the last read is held.</summary>
        public static void Off()
        {
            _shown = false;
            _only = null;
            _car = null;
            _state = DriverState.None;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Read(out DriverState state, out bool open)
        {
            state = DriverState.None;
            open = false;
            UIs uis = InstanceBehavior<UIs>.Instance;
            SmartphoneUI phone = uis != null ? uis.smartphoneUI : null;
            SmartphonePrivateDriverUI card = phone != null ? phone.privateDriverUI : null;
            GameInstance save = SaveGameManager.Current;
            if (card == null || save == null || UI.Load.LoadScene.isLoading)
            {
                return;
            }
            List<VehicleInstance> cars = save.privateDriverVehicleInstances;
            _cars = cars != null ? cars.Count : 0;
            _only = _cars == 1 ? cars[0] : null;
            _car = SmartphonePrivateDriverUI.CurrentVehicle;
            bool riding = InstanceBehavior<TaxiSystem>.Instance != null && TaxiSystem.IsTraveling;
            state = HudDriverRules.State(PrivateDriverHelpers.GetActiveContract() != null, _car != null, riding, Near(_car));
            open = card.gameObject.activeSelf;
        }

        private static bool Near(PrivateDriverVehicle car)
        {
            if (car == null || InstanceBehavior<GameManager>.Instance == null || InstanceBehavior<GameManager>.Instance.playerController == null)
            {
                return false;
            }
            return (car.transform.position - PlayerHelper.GetPosition()).sqrMagnitude <= HudDriverRules.Near * HudDriverRules.Near;
        }

        /// <summary>The words on hover: the state and what a press does, with the key as bound now.</summary>
        public static string Words()
        {
            return UiText.Fill(HudDriverRules.NoteKey(_state, _cars), "key", Keys.Label(KeyAction.Driver), "car", CarName());
        }

        /// <summary>The Driver key, each frame while the interface is on; it answers only while the bar shows the button.</summary>
        internal static void Frame()
        {
            if (!_shown || !UnityEngine.Input.GetKeyDown(Keys.Key(KeyAction.Driver)) || TypingNow.Now || !Keys.Down(KeyAction.Driver) || _pressedFrame == Time.frameCount)
            {
                return;
            }
            _pressedFrame = Time.frameCount;
            if (!Press())
            {
                UiGame.Instance.Play(UiCue.Error);   // the bar's button has the HUD's own cue for a press that did nothing
            }
        }

        /// <summary>The button pressed: the garage's only car called, or the game's own driver button for the card. False when there was nothing to press.</summary>
        public static bool Press()
        {
            if (!_shown)
            {
                return false;
            }
            try
            {
                return Act();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the HUD's private driver button");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool Act()
        {
            UIs uis = InstanceBehavior<UIs>.Instance;
            SmartphoneUI phone = uis != null ? uis.smartphoneUI : null;
            SmartphonePrivateDriverUI card = phone != null ? phone.privateDriverUI : null;
            if (card == null)
            {
                return false;
            }
            if (HudDriverRules.Press(_state, _cars, _cardOpen) == DriverPress.Call && _only != null)
            {
                card.OnClickVehicle(_only);   // as the card's own button for the car: its fee, its refusals and its notices
                return true;
            }
            Button button = GameContract.PhoneDriver.Field?.GetValue(phone) as Button;
            if (UiWidgets.Shows(button) && button.interactable)
            {
                UiWidgets.Hand(button);   // the phone's own button, so what listens to it hears it
            }
            else
            {
                card.OnClickPrivateDriverButton();   // the phone is folded and its button hidden: the same method its listener calls
            }
            return true;
        }
    }
}
