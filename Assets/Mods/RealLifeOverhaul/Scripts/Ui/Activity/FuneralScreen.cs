using System;
using BigAmbitions.InputSystem;
using Helpers;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's funeral (FuneralUI) as a side card: the late character's initials large, their name, their age and their
    /// days in the city at its left; the words; the game's three ways on, numbered 1, 2 and 3 with the game's own key at
    /// each (load the backup on F, the main menu on P, quit on Escape), the backup's row only when the game found one, and
    /// the main menu and the backup as buttons at its foot. The game's screen stays live underneath and reads its own F,
    /// P and Escape; the kit answers the number keys, the d-pad and the pointer by calling what the game's own keys call.
    /// Offline (Options, Tests) it shows the same card for the player now and every way on only closes it. Game side.
    /// Cost: a layout when it opens and when a pad takes the lead.
    /// </summary>
    public sealed class FuneralScreen : IUiScreen
    {
        private enum Way : byte { Backup, Menu, Quit }

        private readonly FuneralUI _ui;
        private readonly bool _test;
        private readonly Way[] _ways = new Way[3];
        private int _count;
        private bool _gone;          // a way on was taken: no second press starts it again
        private bool _dirty = true;

        /// <summary>The test's way to close it (Options, Tests); null in the game.</summary>
        public Action Close;

        public FuneralScreen(FuneralUI ui, bool test)
        {
            _ui = ui;
            _test = test;
        }

        public string Id => "funeral";

        public bool Dirty => _dirty;

        public void Lay(UiScene s, UiFrame frame, bool dark, IUiKeys keys)
        {
            _dirty = false;
            UiSideCard.Lay(s, Model(), frame, dark, keys, "funeral");
        }

        private UiSideCardModel Model()
        {
            string name = Name();
            int day = SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
            int age = Age();
            // The game's own backup row, shown only when it found the backup it makes at 70 (the test shows it as such a death would).
            var label = GameContract.FuneralBackup.Field != null && _ui != null ? GameContract.FuneralBackup.Field.GetValue(_ui) as Component : null;
            bool hasBackup = (label != null && label.transform.parent != null && label.transform.parent.gameObject.activeSelf) || (_test && day > 1);
            UiStatusModel bar = UiChrome.Bar != null ? UiChrome.Bar() : null;
            string district = bar != null ? bar.District : null;
            var lines = new System.Collections.Generic.List<string>(2);
            if (age > 0)
            {
                lines.Add(UiText.Fill("reallife_ui_funeral_age", "age", UiText.Whole(age)));
            }
            lines.Add(UiText.Fill("reallife_ui_funeral_days", "day", UiText.Whole(Math.Max(1, day))));
            _count = 0;
            var choices = new System.Collections.Generic.List<UiSideChoice>(3);
            if (hasBackup)
            {
                _ways[_count++] = Way.Backup;
                choices.Add(new UiSideChoice
                {
                    Name = UiText.T("reallife_ui_funeral_backup"), Line = UiText.Fill("reallife_ui_funeral_backup_line", "age", UiText.Whole(FuneralHelper.BackUpSaveAge)),
                    GameKey = ActivityRead.Key(PlayerAction.Interact),
                });
            }
            _ways[_count++] = Way.Menu;
            choices.Add(new UiSideChoice { Name = UiText.T("reallife_ui_funeral_menu"), Line = UiText.T("reallife_ui_funeral_menu_line"), GameKey = ActivityRead.Key(PlayerAction.SecondaryInteract) });
            _ways[_count++] = Way.Quit;
            choices.Add(new UiSideChoice { Name = UiText.T("reallife_ui_funeral_quit"), Line = UiText.T("reallife_ui_funeral_quit_line"), GameKey = ActivityRead.Key(PlayerAction.Cancel) });
            var buttons = new System.Collections.Generic.List<UiSideButton>(2)
            {
                new UiSideButton
                {
                    Label = UiText.T("reallife_ui_funeral_menu_short"), Kind = hasBackup ? UiButtonKind.Secondary : UiButtonKind.Primary, Key = UiAction.Count,
                    GameKey = ActivityRead.Key(PlayerAction.SecondaryInteract),
                },
            };
            if (hasBackup)
            {
                buttons.Add(new UiSideButton { Label = UiText.T("reallife_ui_funeral_backup"), Kind = UiButtonKind.Primary, Key = UiAction.Count, GameKey = ActivityRead.Key(PlayerAction.Interact) });
            }
            return new UiSideCardModel
            {
                Portrait = name, SideName = name, SideLines = lines.ToArray(),
                Kicker = string.IsNullOrEmpty(district) ? UiText.T("reallife_ui_funeral_kicker_plain") : UiText.Fill("reallife_ui_funeral_kicker", "place", district),
                Title = UiText.T("reallife_ui_funeral_title"), Body = UiText.T("reallife_ui_funeral_body"), Choices = choices.ToArray(), Selected = 0,
                Note = UiText.T(hasBackup ? "reallife_ui_funeral_note" : "reallife_ui_funeral_note_two"), Buttons = buttons.ToArray(),
                Prompts = new[]
                {
                    new UiPrompt { Action = UiAction.Move, Label = UiText.T("reallife_ui_prompt_move") }, new UiPrompt { Action = UiAction.Confirm, Label = UiText.T("reallife_ui_prompt_choose") },
                    new UiPrompt { Action = UiAction.Back, Label = UiText.T("reallife_ui_funeral_quit_short") },   // the game's own Escape quits, and the bar says so
                },
                PromptCount = 3,
            };
        }

        // The player's name as the game keeps it; their initials stand in the portrait's place.
        private static string Name()
        {
            try
            {
                CharacterData data = PlayerHelper.CharacterData;
                return data != null && !string.IsNullOrEmpty(data.name) ? data.name : UiText.T("reallife_ui_funeral_you");
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return UiText.T("reallife_ui_funeral_you");
            }
        }

        // The player's age in the game's years (its days a year are the save's own); 0 when the game does not say.
        private static int Age()
        {
            try
            {
                CharacterData data = PlayerHelper.CharacterData;
                return data != null ? TimeHelper.GetYearsByDays(data.ageInDays) : 0;
            }
            catch (Exception e) when (Guard.Missing(e) || e is NullReferenceException)
            {
                return 0;
            }
        }

        public bool Press(int command, int arg)
        {
            if (command == UiSideCard.ChoiceCommand && arg >= 0 && arg < _count)
            {
                return Go(_ways[arg]);
            }
            if (command == UiSideCard.ButtonCommand)
            {
                // The foot's buttons: the main menu, then the backup when there is one.
                return Go(arg == 0 ? Way.Menu : Way.Backup);
            }
            return false;
        }

        // A way on, as the game's own key for it calls it; once.
        private bool Go(Way way)
        {
            if (_gone)
            {
                return false;
            }
            if (_test)
            {
                Close?.Invoke();
                return true;
            }
            if (_ui == null)
            {
                return false;
            }
            _gone = true;
            if (GameKeyed())
            {
                return true;   // the game's own key for a way on was pressed this frame: the game takes it itself, and only once
            }
            switch (way)
            {
                case Way.Backup:
                    _ui.LoadBackUpSave();
                    break;
                case Way.Menu:
                    _ui.BackToMainMenu();
                    break;
                default:
                    _ui.ExitToDesktop();
                    break;
            }
            return true;
        }

        // A key the game's own screen reads (F, P, Escape, as bound) pressed this frame: the game acts on it in its own Update.
        private static bool GameKeyed()
        {
            try
            {
                return PlayerAction.Interact.Pressed() || PlayerAction.SecondaryInteract.Pressed() || PlayerAction.Cancel.Pressed();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                return false;
            }
        }

        public void Focused(int command, int arg)
        {
        }

        public bool Back()
        {
            if (_test)
            {
                Close?.Invoke();
                return true;
            }
            return false;   // the game's own Escape quits, as its own screen says: the kit leaves it to the game
        }

        public void Tab(int step)
        {
        }

        public bool Act(UiAction action) => !_test && (action == UiAction.Alt || action == UiAction.Option);   // F and P are the game's: it takes them itself

        public void Reveal(int list, int index)
        {
        }

        public void Wheel(int clip, float notches)
        {
        }
    }
}
