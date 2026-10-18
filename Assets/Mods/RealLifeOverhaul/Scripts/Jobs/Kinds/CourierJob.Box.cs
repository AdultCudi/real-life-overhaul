using System;
using BigAmbitions.Items;
using Helpers;
using UnityEngine;
using Vehicles.DeliveryDriverJob;

namespace RealLifeOverhaul
{
    /// <summary>Courier: the game's own sealed box in the player's hands, and what the game stows of it.</summary>
    public sealed partial class CourierJob
    {
        private const string SealedBox = "ba:itemname_sealedcardboardbox";

        // The game's own carried box: the sealed one of its delivery job, which cannot be opened, shelved or sold.
        private bool GiveBox()
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
                TakeBox();
                Ctx.Logger?.Error(e);
                Ctx.Logger?.Warn("Jobs: the game would not put a parcel in the player's hands; the courier round is called off.");
                Ctx.Say("reallife_job_courier_no_box", 9f);
                Finish(JobEnd.Interrupted);
                return false;
            }
        }

        private void TakeBox()
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
                Ctx.Logger?.Error(e);
            }
        }

        // The parcels in the car are a count, not cargo: a box the game stowed is cleared, never during a delivery of the game's own.
        private void ClearStowed()
        {
            try
            {
                if (SaveGameManager.Current != null && SaveGameManager.Current.currentPlayerMission == null)
                {
                    DeliveryJobHelper.DiscardSealedBoxes();
                }
            }
            catch (Exception e)
            {
                Ctx.Logger?.Error(e);
            }
        }

        /// <summary>After a load: a sealed parcel a saved round left in the player's hands or cars is taken away, never during a game delivery.</summary>
        public static void ClearLeftovers(BAModAPI.IModLogger logger)
        {
            try
            {
                if (SaveGameManager.Current == null || SaveGameManager.Current.currentPlayerMission != null)
                {
                    return;
                }
                ItemInstance held = PlayerHelper.ItemInstanceInHands;
                if (held != null)
                {
                    System.Collections.Generic.List<CargoInstance> cargo = held.GetCargoInstances();
                    for (int i = 0; cargo != null && i < cargo.Count; i++)
                    {
                        if (cargo[i] != null && cargo[i].IsSealed)
                        {
                            PlayerHelper.ItemInstanceInHands = null;
                            logger?.Info("Jobs: a parcel left in the player's hands by a saved courier round was taken away.");
                            break;
                        }
                    }
                }
                DeliveryJobHelper.DiscardSealedBoxes();
            }
            catch (Exception e)
            {
                logger?.Error(e);
            }
        }
    }
}
