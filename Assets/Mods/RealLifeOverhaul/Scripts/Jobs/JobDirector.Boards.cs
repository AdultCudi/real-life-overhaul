using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using NWH.VehiclePhysics2.Damage;
using PlayerActivity;
using UI;
using UI.Guiders;
using UI.Load;
using UI.Notification;
using UI.Overlays;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>Jobs' director: the help-wanted boards and the game's panel over them.</summary>
    public partial class JobDirector
    {
        private readonly GameUi.WorldPanel _boardPanel = new GameUi.WorldPanel();
        private Action _onBoard;
        private int _boardShown = int.MinValue;   // the board and employer the panel was last written for

        // Once a second while no job is on: where the boards stand, and whether the player is at one.
        private void Boards(GameManager gm)
        {
            bool onFoot = Settings.Jobs && JobState.Loaded && !BuildingManager.IsInsideBuilding && gm.selectedVehicle == null && TrafficTools.Street;
            if (!onFoot)
            {
                _boards.Away();
            }
            else
            {
                _boards.Tick(gm.playerController.transform.position);
            }
            _career.Slow(gm.playerController.transform.position, onFoot, false);   // the employers behind the boards: their bosses, the rota
            // At an employer's office the game's panel over the board offers to speak to the boss; at any other board, to read it.
            int employer = CareerDirector.On ? _career.Near : -1;
            int board = employer >= 0 ? _career.DepotOf(employer) : _boards.InReach ? _boards.Near : -1;
            if (board < 0 || board >= _boards.Count || UiOpen())
            {
                HideBoardPanel();
                return;
            }
            int shown = board | ((employer + 1) << 12);
            if (shown != _boardShown)
            {
                _boardShown = shown;
                _boardPanel.Lines(employer >= 0 ? JobText.T(CareerCatalogue.All[employer].NameKey) : JobText.T("reallife_job_board_title"), _boards.Address(board), null,
                    GameUi.WorldPanel.White());
                _boardPanel.Buttons(employer >= 0 ? "reallife_career_board_speak" : "reallife_job_board_read", _onBoard, true, null, null, false);
                _boardPanel.Position = _boards.At(board) + Vector3.up * 1.9f;
                if (Meta.Dev.Verbose)
                {
                    Logger?.Info("Jobs check (board panel): at the board of " + _boards.Address(board) + (employer >= 0 ? " (" + CareerCatalogue.All[employer].Id + "'s office)" : string.Empty)
                                 + ": the game's own panel offers the board on the game's interact key.");
                }
            }
            _boardPanel.Show();
        }

        private void HideBoardPanel()
        {
            if (_boardShown != int.MinValue)
            {
                _boardShown = int.MinValue;
                _boardPanel.Hide();
            }
        }

        // The button of the board's panel: run by the game on its interact key or on a click.
        private void BoardClicked()
        {
            if (_job == null && !_broken)
            {
                _interactFrame = Time.frameCount;   // the key that pressed the button does nothing else this frame
                OpenBoard();
            }
        }

        // The same list the Gig Line reads out, face to face with the board.
        private void OpenBoard()
        {
            try
            {
                if (!Free(out string reason))
                {
                    JobText.Toast(NotificationType.Warning, reason ?? "reallife_job_no_now");
                    return;
                }
                HideBoardPanel();
                int employer = CareerDirector.On ? _career.Near : -1;
                _boards.Away();
                if (employer >= 0)
                {
                    CareerPhone.OpenInPerson(employer);   // the office behind the board: its boss, and through them the board's own list
                }
                else
                {
                    JobPhone.OpenInPerson();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        // A screen of the game's has the keys: its menus, and the face-to-face dialogue a board opens.
        private static bool UiOpen()
        {
            if (ThirdPersonCamera.IsUiOpen())
            {
                return true;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            return uis != null && uis.playerHUD != null && uis.playerHUD.dialogUI != null && uis.playerHUD.dialogUI.isPanelOpen;
        }
    }
}
