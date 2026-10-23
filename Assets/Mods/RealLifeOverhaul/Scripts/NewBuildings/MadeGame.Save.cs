using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Player.FoodDeliveryJob;
using Player.PlayerMissions;
using UnityEngine;
using Vehicles.DeliveryDriverJob;

namespace RealLifeOverhaul.NewBuildings
{
    /// <summary>What the save guard took out of a save while it was written, to be put back a frame later exactly where it was.</summary>
    internal sealed class SaveTaken
    {
        public bool Current;                 // the player's current address and position stood in for
        public string Street;
        public int Number;
        public Vector3 Position;
        public Quaternion Rotation;
        public Address Destination;          // the player's own destination, taken out
        public PlayerMission Mission;        // the mission under way, taken out whole
        public readonly List<int> DoneAt = new List<int>(4);            // places in the day's delivery addresses
        public readonly List<Address> Done = new List<Address>(4);
        public readonly List<int> OffersAt = new List<int>(4);          // places in the food delivery offers
        public readonly List<FoodDeliveryOffer> Offers = new List<FoodDeliveryOffer>(4);
        public readonly List<int> RegistrationsAt = new List<int>(2);   // places in the save's list of registrations (never any, if all is well)
        public readonly List<BuildingRegistration> Registrations = new List<BuildingRegistration>(2);

        public int Count => (Current ? 1 : 0) + (Destination != null ? 1 : 0) + (Mission != null ? 1 : 0) + Done.Count + Offers.Count + Registrations.Count;

        public void Clear()
        {
            Current = false;
            Street = null;
            Number = 0;
            Destination = null;
            Mission = null;
            DoneAt.Clear();
            Done.Clear();
            OffersAt.Clear();
            Offers.Clear();
            RegistrationsAt.Clear();
            Registrations.Clear();
        }
    }

    /// <summary>What a written save holds of the mod's addresses, as the save check reads it back.</summary>
    internal struct SaveNames
    {
        public int Registrations;    // registrations at an address of the mod's own
        public bool Current;         // the player's current address is one
        public bool CurrentEmpty;    // the player's current address is empty: the save loads with the player outside
        public bool Mission;         // the mission under way names one
        public bool Destination;     // the player's destination is one
        public int Others;           // the day's delivery addresses and the food delivery offers that name one

        public bool None => Registrations == 0 && !Current && !Mission && !Destination && Others == 0;
    }

    /// <summary>
    /// The save guard, small on purpose: a made building is never saved, so only what names a made address while a save is
    /// written needs care. The game serialises right after its save event, so what is taken out is put back a frame later.
    /// A save made inside a made building is written with no current address and the player at its door outside, so it
    /// loads with or without the mod with the player there (the game puts a player with no address at the saved position).
    /// Every address on a street of the mod's own is a made one, standing or not, so the test is the street alone. The
    /// district's own framework (another part) takes out the rest of what names the mod's addresses; each takes out only
    /// what is still there and puts back only what it took, so the two can run in either order.
    /// </summary>
    internal static partial class MadeGame
    {
        private const float OutsideDoor = 1.2f;   // m in front of a door: where a save made inside puts the player

        /// <summary>Takes out of the save about to be written whatever names an address of the mod's own; how many things.</summary>
        public static int Strip(SaveTaken taken)
        {
            taken.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return 0;
            }
            if (MadeStreets.IsMine(save.CurrentStreetName))
            {
                taken.Current = true;
                taken.Street = save.CurrentStreetName;
                taken.Number = save.CurrentStreetNumber;
                taken.Position = save.LastPlayerPosition;
                taken.Rotation = save.LastPlayerRotation;
                save.CurrentStreetName = string.Empty;
                save.CurrentStreetNumber = 0;
                MadeBuilding made = MadeBuildings.At(taken.Street, taken.Number);
                if (made != null)
                {
                    Quaternion outward = Quaternion.Euler(0f, made.DoorFacing, 0f);
                    save.LastPlayerPosition = new Vector3(made.DoorX, made.DoorY, made.DoorZ) + outward * Vector3.forward * OutsideDoor;
                    save.LastPlayerRotation = outward;
                }
            }
            if (Mine(save.customDestination))
            {
                taken.Destination = save.customDestination;
                save.customDestination = null;
            }
            if (save.currentPlayerMission != null && Names(save.currentPlayerMission))
            {
                taken.Mission = save.currentPlayerMission;
                save.currentPlayerMission = null;
            }
            List<Address> done = save.deliveryJobLocationsDoneToday;
            for (int i = done != null ? done.Count - 1 : -1; i >= 0; i--)
            {
                if (Mine(done[i]))
                {
                    taken.DoneAt.Add(i);
                    taken.Done.Add(done[i]);
                    done.RemoveAt(i);
                }
            }
            List<FoodDeliveryOffer> offers = save.foodDeliveryOffers;
            for (int i = offers != null ? offers.Count - 1 : -1; i >= 0; i--)
            {
                if (offers[i] != null && (Mine(offers[i].pickupAddress) || Mine(offers[i].destinationAddress)))
                {
                    taken.OffersAt.Add(i);
                    taken.Offers.Add(offers[i]);
                    offers.RemoveAt(i);
                }
            }
            List<BuildingRegistration> regs = save.BuildingRegistrations;
            for (int i = regs != null ? regs.Count - 1 : -1; i >= 0; i--)
            {
                if (regs[i] != null && MadeStreets.IsMine(regs[i].StreetName))
                {
                    taken.RegistrationsAt.Add(i);
                    taken.Registrations.Add(regs[i]);
                    regs.RemoveAt(i);
                }
            }
            return taken.Count;
        }

        /// <summary>Puts back what <see cref="Strip"/> took, each thing where it was; true when the save in memory is as before.</summary>
        public static bool PutBack(SaveTaken taken)
        {
            GameInstance save = SaveGameManager.Current;
            if (save == null || taken.Count == 0)
            {
                return taken.Count == 0;
            }
            if (taken.Current)
            {
                save.CurrentStreetName = taken.Street;
                save.CurrentStreetNumber = taken.Number;
                save.LastPlayerPosition = taken.Position;
                save.LastPlayerRotation = taken.Rotation;
            }
            if (taken.Destination != null && save.customDestination == null)
            {
                save.customDestination = taken.Destination;
            }
            if (taken.Mission != null && save.currentPlayerMission == null)
            {
                save.currentPlayerMission = taken.Mission;
            }
            Back(save.deliveryJobLocationsDoneToday, taken.DoneAt, taken.Done);
            Back(save.foodDeliveryOffers, taken.OffersAt, taken.Offers);
            Back(save.BuildingRegistrations, taken.RegistrationsAt, taken.Registrations);
            return (!taken.Current || (save.CurrentStreetName == taken.Street && save.CurrentStreetNumber == taken.Number))
                   && (taken.Destination == null || save.customDestination == taken.Destination) && (taken.Mission == null || save.currentPlayerMission == taken.Mission);
        }

        // Taken from the end, so put back from the start: each goes to the place it had.
        private static void Back<T>(List<T> list, List<int> at, List<T> items)
        {
            if (list == null)
            {
                return;
            }
            for (int i = items.Count - 1; i >= 0; i--)
            {
                list.Insert(Math.Min(at[i], list.Count), items[i]);
            }
        }

        private static bool Mine(Address address)
        {
            return address != null && MadeStreets.IsMine(address.streetName);
        }

        private static bool Names(PlayerMission mission)
        {
            if (mission is DeliveryDriverMission delivery)
            {
                if (Mine(delivery.startAddress) || Mine(delivery.pinnedAddress))
                {
                    return true;
                }
                List<DeliveryJobDestination> stops = delivery.destinations;
                for (int i = 0; stops != null && i < stops.Count; i++)
                {
                    if (stops[i] != null && Mine(stops[i].address))
                    {
                        return true;
                    }
                }
                return false;
            }
            return mission is FoodDeliveryMission food && Mine(food.destinationAddress);
        }

        /// <summary>What a save (read back from its file) holds of the mod's addresses.</summary>
        public static SaveNames NamesIn(GameInstance save)
        {
            var names = new SaveNames();
            if (save == null)
            {
                return names;
            }
            for (int i = 0; save.BuildingRegistrations != null && i < save.BuildingRegistrations.Count; i++)
            {
                names.Registrations += save.BuildingRegistrations[i] != null && MadeStreets.IsMine(save.BuildingRegistrations[i].StreetName) ? 1 : 0;
            }
            names.Current = MadeStreets.IsMine(save.CurrentStreetName);
            names.CurrentEmpty = string.IsNullOrEmpty(save.CurrentStreetName);
            names.Mission = save.currentPlayerMission != null && Names(save.currentPlayerMission);
            names.Destination = Mine(save.customDestination);
            for (int i = 0; save.deliveryJobLocationsDoneToday != null && i < save.deliveryJobLocationsDoneToday.Count; i++)
            {
                names.Others += Mine(save.deliveryJobLocationsDoneToday[i]) ? 1 : 0;
            }
            for (int i = 0; save.foodDeliveryOffers != null && i < save.foodDeliveryOffers.Count; i++)
            {
                FoodDeliveryOffer offer = save.foodDeliveryOffers[i];
                names.Others += offer != null && (Mine(offer.pickupAddress) || Mine(offer.destinationAddress)) ? 1 : 0;
            }
            return names;
        }

        // ---------------------------------------------------------------- the save test: a save written and read back

        /// <summary>
        /// Writes one of the game's own recovery saves now, as its autosave does (the player's own save keeps its name);
        /// the file it writes, or null when the game would not save here.
        /// </summary>
        public static string SaveRecovery(out string name)
        {
            name = null;
            GameInstance save = SaveGameManager.Current;
            GameManager game = InstanceBehavior<GameManager>.Instance;
            if (save == null || game == null)
            {
                return null;
            }
            name = "Recover #" + save.currentAutoSaveNumber;   // the name the game gives it (SaveGameManager.Save, a recovery save)
            string folder = SaveGamePathHelper.GetCharacterFolderPath(save.characterId);
            string path = Path.Combine(folder, name + "." + (game.useSaveGameTypeJson ? "json" : "hsg"));
            return SaveGameManager.Save(SaveGameManager.SaveType.RecoverSave) ? path : null;
        }

        /// <summary>True while the game is still writing a save (its compression runs on a thread of its own).</summary>
        public static bool Saving => SaveGameManager.SavingGameInProgress;

        /// <summary>The save file read back by the game's own reader, off the main thread as the game's own load reads it.</summary>
        public static Task<GameInstance> ReadBack(string path)
        {
            GameManager game = InstanceBehavior<GameManager>.Instance;
            return game != null && game.useSaveGameTypeJson ? SaveGameSerializationHelper.DeserializeJsonDataAsync(path) : SaveGameSerializationHelper.DeserializeBinaryDataAsync(path);
        }
    }
}
