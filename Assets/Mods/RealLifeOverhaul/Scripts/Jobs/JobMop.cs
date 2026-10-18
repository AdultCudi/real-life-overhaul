using System;
using System.Reflection;
using BAModAPI;
using Helpers;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own mop in the player's hands on a client's floor, given with the call its cleaning station makes
    /// (<c>CleaningStationController.OnCleaningStationClick</c>), which brings the highlighting, the cursor and the stance.
    /// The mop listens for floor clicks only in the player's own business, so its click method is hooked to the floor's
    /// click event here. It is taken away with the game's own <c>PlayerHelper.RemoveItemsFromHands</c>; <see cref="Take"/>
    /// is safe in any state, for every way out.
    /// </summary>
    public sealed class JobMop
    {
        private const string MopItem = "ba:itemname_mop";   // what the game's cleaning station hands over

        private static readonly MethodInfo Click = GameContract.MopClick.Method;   // resolved once (Core/GameContract)

        private readonly IModLogger _logger;
        private UnityAction<DirtSpotObject> _onCell;
        private UnityEvent<DirtSpotObject> _event;

        public JobMop(IModLogger logger)
        {
            _logger = logger;
        }

        public static bool Holding => PlayerHelper.IsHoldingAMop;

        /// <summary>True while the game's mop is cleaning a spot.</summary>
        public static bool Cleaning => MopController.currentCleaningMop != null;

        /// <summary>True if the player's hands hold something that is not a mop.</summary>
        public static bool HandsFull => PlayerHelper.IsHoldingItem && !PlayerHelper.IsHoldingAMop;

        /// <summary>Puts the mop in empty hands, ready to click on the floor. False if the hands are full or the game refuses.</summary>
        public bool Give()
        {
            if (Holding)
            {
                return true;
            }
            if (PlayerHelper.IsHoldingItem || PlayerHelper.IsUsingVehicle || !BuildingManager.IsInsideBuilding)
            {
                return false;
            }
            try
            {
                PlayerHelper.ItemInstanceInHands = ItemHelper.InitializeNewInstance(MopItem);
                MopController mop = PlayerHelper.PlayerController.Character.rightHand.GetComponentInChildren<MopController>();
                if (mop == null)
                {
                    Take();
                    _logger?.Warn("Jobs: the game would not put a mop in the player's hands.");
                    return false;
                }
                Hook(mop);
                return true;
            }
            catch (Exception e)
            {
                Take();
                _logger?.Error(e);
                return false;
            }
        }

        // The click on a dirt spot starts the mop's own cleaning of the spot and the cells round it.
        private void Hook(MopController mop)
        {
            Unhook();
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null)
            {
                return;
            }
            _event = manager.onFloorCellClick;
            if (Click != null)
            {
                _onCell = (UnityAction<DirtSpotObject>)Delegate.CreateDelegate(typeof(UnityAction<DirtSpotObject>), mop, Click);
            }
            else
            {
                _onCell = Guard.Listener<DirtSpotObject>(cell => mop.UseAtPlayerPosition());   // the game's public call, for the cell under the player
                _logger?.Warn("Jobs: the mop's click method was not found; the mop cleans the cell under the player instead.");
            }
            _event.AddListener(_onCell);
        }

        private void Unhook()
        {
            if (_event != null && _onCell != null)
            {
                _event.RemoveListener(_onCell);
            }
            _event = null;
            _onCell = null;
        }

        /// <summary>The mop out of the player's hands, if it is there, and nothing of it left listening. Never throws.</summary>
        public void Take()
        {
            try
            {
                Unhook();
                if (Holding)
                {
                    MopController.SetOnStopCleaningAction(null);
                    PlayerHelper.RemoveItemsFromHands();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>Sets the game's cleanliness bar, shown with the mop, to this floor's figure: outside the player's own business it reads 100.</summary>
        public void Show(float percent)
        {
            try
            {
                var panel = InstanceBehavior<UI.UIs>.Instance != null ? InstanceBehavior<UI.UIs>.Instance.playerHUD.itemPanelUI : null;
                if (panel != null && panel.maintenanceProgress != null && Holding)
                {
                    panel.maintenanceProgress.SetValue(percent);
                }
            }
            catch (Exception)
            {
                // a bar that cannot be set is only a bar
            }
        }
    }
}
