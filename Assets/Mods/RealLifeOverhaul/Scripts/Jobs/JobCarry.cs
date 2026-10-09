using System;
using BAModAPI;
using BigAmbitions.Items;
using Helpers;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A box in the player's arms, carried with the game's own two-handed carry: the sealed
    /// cardboard box its van job delivers, which cannot be opened, shelved or sold. The game
    /// animates the carry, stows the box if the player gets into a car, and clears sealed boxes
    /// away with its own call; nothing here draws or poses anything. One per job that carries;
    /// <see cref="Take"/> is safe in any state and is what a job calls on every way out.
    /// </summary>
    public sealed class JobCarry
    {
        private const string SealedBox = "ba:itemname_sealedcardboardbox";

        private readonly IModLogger _logger;
        private ItemInstance _box;

        public JobCarry(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>True while the box given here is still the thing in the player's hands.</summary>
        public bool Holding => _box != null && PlayerHelper.ItemInstanceInHands == _box;

        /// <summary>True if a box was given and has left the player's hands by some other way than <see cref="Take"/> (the game stowed it).</summary>
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
                _box = box;   // known before it is in the hands, so it is taken back even if putting it there faults half way
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

        /// <summary>
        /// A box the game stowed in one of the player's vehicles is taken out again, with the game's own
        /// clean-up for sealed boxes. Never while one of the game's own deliveries is on: those boxes are its.
        /// </summary>
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
