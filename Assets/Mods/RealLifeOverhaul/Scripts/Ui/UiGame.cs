using System;
using System.Globalization;
using System.Runtime.CompilerServices;
using BigAmbitions.DayNightCycle;
using BigAmbitions.InputSystem;
using Extensions;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.DualShock;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The one file of the kit that meets the game: the game's own bindings and a pad read into the kit's actions (and
    /// Back taken from the game when a screen uses it), the keys' names for the prompt bar, the game's UI sounds, and the
    /// game's font as the faces' last fallback. Each game step sits in a small method of its own, so a game without a
    /// member costs that step, not the screen. Called on a host's frame only while a kit screen shows (its input), else
    /// on events. Cost: a few input reads a frame while a kit screen shows; nothing otherwise.
    /// </summary>
    public sealed class UiGame : IUiInput, IUiSound, IUiKeys
    {
        private const float StickDead = 0.55f;   // of a stick's travel before it counts as a direction, so a resting stick never moves the focus

        /// <summary>The one instance the director's hosts share.</summary>
        public static readonly UiGame Instance = new UiGame();

        private UiPad _pad;
        private bool _failed;

        public IUiKeys Keys => this;

        public UiPad Pad => _pad;

        public void Read(ref UiInputState state)
        {
            if (_failed)
            {
                return;
            }
            try
            {
                if (QuestionUp())
                {
                    return;   // the game's own question box is over the screen: its keys are its own
                }
                ReadKeys(ref state);
                ReadPad(ref state);
                state.Pad = _pad;
            }
            catch (Exception e)
            {
                _failed = true;   // a game without these members: the kit reads nothing, the pointer still works
                Guard.DoesWithout(e, "the keyboard and a pad on the RLO interface's screens");
            }
        }

        public void TakeBack()
        {
            try
            {
                ResetCancel();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "holding the game's own Escape back while a screen of the RLO interface closes an overlay");
            }
        }

        // ------------------------------------------------------------------------------------------------- keys

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReadKeys(ref UiInputState state)
        {
            Vector2 move = PlayerAction.Move.Vector();
            state.MoveX = move.x > 0.5f ? 1 : move.x < -0.5f ? -1 : 0;
            state.MoveY = move.y > 0.5f ? -1 : move.y < -0.5f ? 1 : 0;   // the game's up is +y; the kit's rows run down
            state.Confirm = PlayerAction.Confirm.Pressed();
            state.Back = PlayerAction.Cancel.Pressed();
            state.TabPrev = PlayerAction.RotateLeft.Pressed();
            state.TabNext = PlayerAction.RotateRight.Pressed();
            state.Alt = PlayerAction.Interact.Pressed();
            state.Option = PlayerAction.SecondaryInteract.Pressed();
            Keyboard keyboard = Keyboard.current;
            Mouse mouse = Mouse.current;
            for (int i = 0; keyboard != null && i < 9; i++)
            {
                if (keyboard[Key.Digit1 + i].wasPressedThisFrame || keyboard[Key.Numpad1 + i].wasPressedThisFrame)
                {
                    state.Pick = i + 1;   // the number keys are no binding of the game's: a choice's number is the same on every keyboard
                    break;
                }
            }
            if ((keyboard != null && keyboard.anyKey.wasPressedThisFrame) || (mouse != null && (mouse.leftButton.wasPressedThisFrame || mouse.delta.ReadValue().sqrMagnitude > 4f)))
            {
                _pad = UiPad.None;   // the keyboard or the mouse was used last: key caps again
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private void ReadPad(ref UiInputState state)
        {
            Gamepad pad = Gamepad.current;
            if (pad == null)
            {
                return;
            }
            Vector2 stick = pad.leftStick.ReadValue();
            int x = pad.dpad.right.isPressed || stick.x > StickDead ? 1 : pad.dpad.left.isPressed || stick.x < -StickDead ? -1 : 0;
            int y = pad.dpad.down.isPressed || stick.y < -StickDead ? 1 : pad.dpad.up.isPressed || stick.y > StickDead ? -1 : 0;
            bool used = x != 0 || y != 0 || pad.buttonSouth.wasPressedThisFrame || pad.buttonEast.wasPressedThisFrame || pad.buttonWest.wasPressedThisFrame
                        || pad.buttonNorth.wasPressedThisFrame || pad.leftShoulder.wasPressedThisFrame || pad.rightShoulder.wasPressedThisFrame;
            if (!used)
            {
                return;
            }
            _pad = pad is DualShockGamepad ? UiPad.PlayStation : UiPad.Xbox;
            if (state.MoveX == 0 && state.MoveY == 0)
            {
                state.MoveX = x;
                state.MoveY = y;
            }
            state.Confirm |= pad.buttonSouth.wasPressedThisFrame;
            state.Back |= pad.buttonEast.wasPressedThisFrame;
            state.Alt |= pad.buttonWest.wasPressedThisFrame;
            state.Option |= pad.buttonNorth.wasPressedThisFrame;
            state.TabPrev |= pad.leftShoulder.wasPressedThisFrame;
            state.TabNext |= pad.rightShoulder.wasPressedThisFrame;
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void ResetCancel() => PlayerAction.Cancel.Reset();

        private static bool QuestionUp() => GameConfirm.IsOpen;

        public string Name(UiAction action)
        {
            if (_pad != UiPad.None)
            {
                return null;
            }
            string number = UiKeyGlyphs.Number(action);
            if (number != null)
            {
                return number;
            }
            try
            {
                return KeyName(action);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return UiDefaultKeys.Keyboard.Name(action);   // a game without its bindings' names: the defaults
            }
        }

        public UiIcon Glyph(UiAction action) => UiKeyGlyphs.For(action, _pad);

        // The game's own label for the key bound to an action, as its windows write it (" [Esc]" without the brackets).
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string KeyName(UiAction action)
        {
            switch (action)
            {
                case UiAction.Confirm: return PlayerAction.Confirm.GetDisplayName();
                case UiAction.Back: return PlayerAction.Cancel.GetDisplayName();
                case UiAction.TabPrev: return PlayerAction.RotateLeft.GetDisplayName();
                case UiAction.TabNext: return PlayerAction.RotateRight.GetDisplayName();
                case UiAction.Alt: return PlayerAction.Interact.GetDisplayName();
                case UiAction.Option: return PlayerAction.SecondaryInteract.GetDisplayName();
                default: return null;
            }
        }

        // ------------------------------------------------------------------------------------------------- sounds

        public void Play(UiCue cue)
        {
            try
            {
                Sound(cue);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the game's own sounds on the RLO interface's screens");
            }
        }

        // The game's own UI sounds, as its buttons play them: its hover tick on a move, its click on a press.
        [MethodImpl(MethodImplOptions.NoInlining)]
        private static void Sound(UiCue cue)
        {
            switch (cue)
            {
                case UiCue.Move: UiSoundHelper.Play(UiSound.Hover); break;
                case UiCue.Back: UiSoundHelper.Play(UiSound.Back); break;
                case UiCue.Error: UiSoundHelper.Play(UiSound.Error); break;
                default: UiSoundHelper.Play(UiSound.Click); break;
            }
        }

        // ------------------------------------------------------------------------------------------------- the masthead

        /// <summary>The masthead's right side, as the game's own bar writes it: the money, then the day and the time.</summary>
        public static string Status()
        {
            try
            {
                return StatusNow();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the money and the time in the RLO interface's masthead");
                UiChrome.Status = null;
                return null;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static string StatusNow()
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return null;
            }
            Timestamp now = TimeHelper.Now();
            string time = now.Hour.ToString("00", CultureInfo.InvariantCulture) + ":" + ((int)now.Minute).ToString("00", CultureInfo.InvariantCulture);
            return save.Money.ToCurrencyFormat() + "     " + LocalizorManager.GetLocalization("timestamp_full", new { day = now.Day, time });
        }

        // ------------------------------------------------------------------------------------------------- fonts

        /// <summary>The game's own font as the faces' last fallback, once it is known (behind a black screen); true when set.</summary>
        public static bool FallbackFont()
        {
            TMPro.TMP_FontAsset font = GameFont.Asset;
            if (font == null)
            {
                return false;
            }
            UiFonts.SetFallback(font);
            return true;
        }
    }
}
