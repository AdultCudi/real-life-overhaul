using System;
using BigAmbitions.Characters;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>What the player can do for fun. The order is of no meaning; the ids used in the save are in LeisureKinds.</summary>
    public enum LeisureKind
    {
        None,
        Chess,         // street chess at the mod's table, against whoever sits there
        Fishing,       // a line in the water at the waterfront
        Dance,         // the dance floor of a nightclub
        Drinks,        // the bar of a nightclub
        Sightseeing,   // taking in the view at a landmark
        Rack           // the Real Life Overhaul rack in a clothing store: the game's clothes screen as a shop (ClothesScreen)
    }

    public static class LeisureKinds
    {
        /// <summary>The id a kind's records are kept under in the save.</summary>
        public static string Id(LeisureKind kind)
        {
            switch (kind)
            {
                case LeisureKind.Chess: return "chess";
                case LeisureKind.Fishing: return "fishing";
                case LeisureKind.Dance: return "dance";
                case LeisureKind.Drinks: return "drinks";
                case LeisureKind.Sightseeing: return "sights";
                case LeisureKind.Rack: return "rack";
                default: return "none";
            }
        }
    }

    /// <summary>
    /// The door for the rest of the mod (friends, dates, life events). Everything here is safe to
    /// call at any time and from anywhere: it answers false with a reason instead of throwing.
    ///
    ///   Leisure.Activities.Start(LeisureKind.Chess, null, friend)
    ///
    /// starts the activity where the player stands if it can be played there (the same test the
    /// options-page test buttons make); <c>place</c>, when given, is where it should be played
    /// and must be within reach of the player (it is not walked or driven to: bring the player
    /// there first, then call). <c>companion</c> is somebody who came along: the player is given
    /// the "good company" mood at the end, and the companion is left alone otherwise (whoever
    /// brought them keeps them: leisure never moves, animates or releases a companion).
    /// <see cref="Finished"/> fires once per activity with its kind and score.
    /// </summary>
    public static class Activities
    {
        /// <summary>An activity ended: its kind, its score (0 when it was abandoned) and whether it was played to the end.</summary>
        public static event Action<LeisureKind, int, bool> Finished;

        /// <summary>The activity being played, or None.</summary>
        public static LeisureKind Current => LeisureDirector.Instance != null ? LeisureDirector.Instance.CurrentKind : LeisureKind.None;

        public static bool Busy => Current != LeisureKind.None;

        public static bool Start(LeisureKind kind, Vector3? place = null, ThirdPersonCharacter companion = null)
        {
            return Start(kind, place, companion, out _);
        }

        /// <summary>The same, with the reason it did not start (a locale key) when it answers false.</summary>
        public static bool Start(LeisureKind kind, Vector3? place, ThirdPersonCharacter companion, out string refusalKey)
        {
            LeisureDirector director = LeisureDirector.Instance;
            if (director == null)
            {
                refusalKey = "reallife_leisure_no_now";
                return false;
            }
            return director.StartFromApi(kind, place, companion, out refusalKey);
        }

        /// <summary>Ends whatever is being played, as if the player had walked away. Safe when nothing is.</summary>
        public static void Stop()
        {
            LeisureDirector.Instance?.Quit();
        }

        public static int Best(LeisureKind kind) => LeisureState.BestOf(LeisureKinds.Id(kind));

        internal static void Raise(LeisureKind kind, int score, bool played)
        {
            try
            {
                Finished?.Invoke(kind, score, played);
            }
            catch (Exception e)
            {
                LeisureDirector.Logger?.Error(e);
            }
        }
    }

    /// <summary>
    /// Buying by id, for the rest of the mod (a gift from a friend, a life event that ends in a shop). Today the only
    /// things sold are the mod's own garments and shoes (the ids of Character/CharacterCatalogue): groceries and gifts
    /// were to be added only when another feature needs them, and there is no second inventory here.
    /// </summary>
    public static class Shopping
    {
        /// <summary>What an item costs, or 0 if nothing of that id is sold.</summary>
        public static int Price(string itemId)
        {
            return ClothesShop.InStock(itemId) ? ClothesShop.Price(itemId) : 0;
        }

        public static bool Owns(string itemId) => LeisureState.Loaded && LeisureState.Owns(itemId);

        /// <summary>Buys a garment for this save: charged if <paramref name="free"/> is false. False with a reason (a locale key) if it is not sold or cannot be paid for; true if it was bought or was theirs already.</summary>
        public static bool Buy(string itemId, out string refusalKey, bool free = false)
        {
            refusalKey = null;
            if (!LeisureState.Loaded || !ClothesShop.InStock(itemId))
            {
                refusalKey = "reallife_leisure_clothes_looks_off";
                return false;
            }
            if (LeisureState.Owns(itemId))
            {
                return true;
            }
            if (!free && !LeisureMoney.Charge(ClothesShop.Price(itemId), "reallife_transaction_clothes"))
            {
                refusalKey = "reallife_leisure_no_money";
                return false;
            }
            LeisureState.Own(itemId);
            return true;
        }

        public static bool Buy(string itemId) => Buy(itemId, out _);
    }

    /// <summary>Paying for leisure: one place, so every charge is booked the same way and counted in the save.</summary>
    public static class LeisureMoney
    {
        public static bool CanPay(int amount)
        {
            return SaveGameManager.Current != null && SaveGameManager.Current.Money >= amount;
        }

        /// <summary>Takes the money if the player has it. False (and nothing taken) if not, or if the game's own call failed.</summary>
        public static bool Charge(int amount, string transactionKey)
        {
            if (amount <= 0)
            {
                return true;
            }
            try
            {
                if (!CanPay(amount) || !GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transactionKey), null, null, force: false))
                {
                    return false;
                }
                LeisureState.Spent += amount;
                return true;
            }
            catch (Exception e)
            {
                LeisureDirector.Logger?.Error(e);
                return false;
            }
        }

        public static void Give(int amount, string transactionKey)
        {
            if (amount <= 0)
            {
                return;
            }
            try
            {
                GameManager.ChangeMoneySafe(amount, new TransactionInfo(transactionKey), null, null, force: true);
            }
            catch (Exception e)
            {
                LeisureDirector.Logger?.Error(e);
            }
        }

        /// <summary>Energy spent on play: never below the level at which the game takes over.</summary>
        public static void Tire(float amount)
        {
            try
            {
                float room = SaveGameManager.Current.Energy - 12f;
                if (amount > 0f && room > 0f)
                {
                    EnergyHelper.SpentEnergyOnce(Mathf.Min(amount, room));
                }
            }
            catch (Exception e)
            {
                LeisureDirector.Logger?.Error(e);
            }
        }
    }
}
