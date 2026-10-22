using System;
using System.Runtime.CompilerServices;
using BigAmbitions.InputSystem;
using UI;
using UI.Smartphone;
using UnityEngine;
using UnityEngine.EventSystems;

namespace RealLifeOverhaul.GameUi
{
    /// <summary>
    /// On the HUD phone's grid, on only while one of its folders is open (PhoneGridPager.Keys): the game's cancel key goes
    /// back to the home screen, the press taken so the game's menu does not open too (PlayerAction.Reset, as TravelListKeys
    /// does). Only while nothing the game's key would close first is up (a question, a menu, the map, the options, an
    /// activity's panel, a dialogue, a building's card, a preview or the designer, a list window, a field being typed in)
    /// and the phone is not folded away. Runs before the game's own key handling. Costs nothing while no folder is open;
    /// while one is, one look at the key a frame.
    /// </summary>
    [DefaultExecutionOrder(-31000)]
    public sealed class PhoneFolderKeys : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PhoneFolderKeys", typeof(PhoneFolderKeys));   // its Update
        private static readonly int HotUpdate = HotSpots.Slot("Core: PhoneFolderKeys.Update");

        internal PhoneGridPager Pager;
        private static bool _without;   // this game lacks a member the check needs: the key is left to the game, the Back tile still works

        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (Pager == null || Pager.Folder < 0)
            {
                enabled = false;
                return;
            }
            if (!PlayerAction.Cancel.Pressed() || !KeyIsFree())
            {
                return;
            }
            PlayerAction.Cancel.Reset();   // taken, so the game's own handling does not open its menu too
            Pager.CloseFolder();
        }

        // Nothing of the game's that the key closes first is up, in the order of the game's CancelButtonHandler.
        private static bool KeyIsFree()
        {
            if (_without || TravelList.AnyOpen)
            {
                return false;
            }
            try
            {
                return GameLeavesKey();
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                _without = true;
                Guard.DoesWithout(e, "the cancel key closing a folder of the phone", "its Back tile closes it");
                return false;
            }
        }

        [MethodImpl(MethodImplOptions.NoInlining)]
        private static bool GameLeavesKey()
        {
            if (GameManager.HasInputSelected() || HudConfirm.isOpen || UI.MiniMenu.MiniMenu.IsOpen || FullMenu.IsOpen || CityMap.IsOpen || Scenes.MainMenu.Options.IsVisible
                || PlayerActivity.PlayerActivityUI.IsPanelOpen || BuildingPreview.isPreviewing || UI.InteriorDesigner.InteriorDesignerUI.IsOpen)
            {
                return false;
            }
            UIs uis = InstanceBehavior<UIs>.Instance;
            if (uis == null)
            {
                return false;
            }
            if (uis.smartphoneCollapsibleWindow != null && uis.smartphoneCollapsibleWindow.IsCollapsed)
            {
                return false;   // the phone folded away: the key is the game's, the folder stays
            }
            if (uis.buildingResume != null && GameMembers.TargetOf(uis.buildingResume) != null)
            {
                return false;
            }
            return uis.playerHUD == null || uis.playerHUD.dialogUI == null || !uis.playerHUD.dialogUI.isPanelOpen;
        }
    }

    /// <summary>
    /// On each mod app button on the HUD phone while it has folders: a right click opens the list to move the app
    /// (PhoneMove), as a long press does on a real phone; a left click is the button's own. Costs nothing a frame.
    /// </summary>
    public sealed class PhoneAppPress : MonoBehaviour, IPointerClickHandler
    {
        private static readonly FeatureGuard FrameGuard = Guard.For(null, "PhoneAppPress", typeof(PhoneAppPress));   // the game's calls of it

        internal PhoneGridPager Pager;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (FrameGuard.Off || eventData == null || eventData.button != PointerEventData.InputButton.Right)
            {
                return;
            }
            try
            {
                if (Pager != null && Pager.Foldered)
                {
                    PhoneMove.Open(transform, Pager);
                }
            }
            catch (Exception fault)
            {
                Guard.Fail(FrameGuard, fault);
            }
        }
    }
}
