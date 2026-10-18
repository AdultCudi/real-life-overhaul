using System;
using BAModAPI;
using BigAmbitions.Items;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>The game's sealed box in the player's arms with its own carry; the game stows it in a car and clears it. <see cref="Take"/> is always safe.</summary>
    public sealed class JobCarry
    {
        private const string SealedBox = "ba:itemname_sealedcardboardbox";

        private readonly IModLogger _logger;
        private ItemInstance _box;

        public JobCarry(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>True if a box was given and has left the player's hands other than by <see cref="Take"/> (the game stowed it).</summary>
        public bool Lost => _box != null && PlayerHelper.ItemInstanceInHands != _box;

        /// <summary>Puts a box in the player's empty hands. False if their hands are full or the game refuses.</summary>
        public bool Give()
        {
            if (PlayerHelper.IsHoldingItem)
            {
                return false;
            }
            try
            {
                ItemInstance box = ItemHelper.InitializeItemInHandsWithCargo(new CargoInstance(SealedBox, 1, 0f));
                _box = box;   // known before it is in the hands, so it is taken back even if putting it there faults
                PlayerHelper.ItemInstanceInHands = box;
                return true;
            }
            catch (Exception e)
            {
                Take();
                _logger?.Error(e);
                _logger?.Warn("Jobs: the game would not put a box in the player's hands.");
                return false;
            }
        }

        /// <summary>Out of the player's hands, if it is still there. Never throws.</summary>
        public void Take()
        {
            ItemInstance box = _box;
            _box = null;
            try
            {
                if (box != null && PlayerHelper.ItemInstanceInHands == box)
                {
                    PlayerHelper.ItemInstanceInHands = null;
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        /// <summary>A box the game stowed in a vehicle is taken out with the game's clean-up for sealed boxes; never during a delivery of the game's own.</summary>
        public void ClearStowed()
        {
            _box = null;
            try
            {
                if (SaveGameManager.Current != null && SaveGameManager.Current.currentPlayerMission == null)
                {
                    Vehicles.DeliveryDriverJob.DeliveryJobHelper.DiscardSealedBoxes();
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }
    }
}
