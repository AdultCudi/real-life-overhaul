using System;
using System.Reflection;
using BAModAPI;
using BigAmbitions.Items;
using Helpers;
using UnityEngine;
using UnityEngine.Events;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's own mop in the player's hands on a client's floor. The game gives a mop at a cleaning station, and only
    /// in a business the player rents (<c>CleaningStationController.OnCleaningStationClick</c>); what it does then is
    /// <c>PlayerHelper.ItemInstanceInHands = ItemHelper.InitializeNewInstance("ba:itemname_mop")</c>, which puts the mop
    /// object in the hand, turns the dirt highlighting on, sets the cleaning cursor and plays the mopping stance. This
    /// does the same call. The only other difference is the click: the mop listens for a click on the floor only in
    /// the player's own business, so the same mop method is hooked to the floor's click event here.
    ///
    /// Taking it away is the game's own call too (<c>PlayerHelper.RemoveItemsFromHands</c>, as its Escape and its exit
    /// from a building do), which stops a mopping in progress, drops the highlighting and the stance. <see cref="Take"/>
    /// is safe in any state and is what a job calls on every way out.
    /// </summary>
    public sealed class JobMop
    {
        private const string MopItem = "ba:itemname_mop";   // the item the game's cleaning station hands over

        private static readonly MethodInfo Click = typeof(MopController).GetMethod("OnFloorCellClick", BindingFlags.NonPublic | BindingFlags.Instance);

        private readonly IModLogger _logger;
        private UnityAction<DirtSpotObject> _onCell;
        private UnityEvent<DirtSpotObject> _event;

        public JobMop(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>True while the mop is in the player's hands.</summary>
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
                _onCell = cell => mop.UseAtPlayerPosition();   // the game's public call, for the cell under the player
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

        /// <summary>
        /// The game's own item panel shows the cleanliness bar with the mop, but reads 100 outside the player's own
        /// business; this sets the bar to the floor's real figure.
        /// </summary>
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
