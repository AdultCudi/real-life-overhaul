using System;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul.Leisure
{
    /// <summary>What the player can do for fun. Records are saved by <see cref="LeisureKinds.Id"/>.</summary>
    public enum LeisureKind
    {
        None,
        Chess,
        Fishing,
        Dance,
        Drinks,
        Sightseeing,
        Rack           // the mod's rack in a clothing store: the game's clothes screen as a shop
    }

    /// <summary>The ids leisure's records are kept under in the save.</summary>
    public static class LeisureKinds
    {
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
    /// The door for the rest of the mod, safe at any time: it answers false with a reason rather than throwing. A
    /// <c>place</c> must be within reach; a <c>companion</c> earns the Social mood and is never moved by leisure.
    /// </summary>
    public static class Activities
    {
        /// <summary>An activity ended: its kind, its score (0 if abandoned) and whether it was played out.</summary>
        public static event Action<LeisureKind, int, bool> Finished;

        public static LeisureKind Current => LeisureDirector.Instance != null ? LeisureDirector.Instance.CurrentKind : LeisureKind.None;

        public static bool Start(LeisureKind kind, Vector3? place = null, ThirdPersonCharacter companion = null)
        {
            return Start(kind, place, companion, out _);
        }

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

        public static void Stop()
        {
            LeisureDirector.Instance?.Quit();
        }

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

    /// <summary>Buying by id for the rest of the mod: the mod's garments and shoes, by CharacterCatalogue id.</summary>
    public static class Shopping
    {
        /// <summary>What an item costs, or 0 if nothing of that id is sold.</summary>
        public static int Price(string itemId)
        {
            return ClothesShop.InStock(itemId) ? ClothesShop.Price(itemId) : 0;
        }

        public static bool Owns(string itemId) => LeisureState.Loaded && LeisureState.Owns(itemId);

        /// <summary>True if bought or owned already; false with a locale key if not sold or not affordable.</summary>
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

    /// <summary>Every leisure charge, booked the same way and counted in the save.</summary>
    public static class LeisureMoney
    {
        public static bool CanPay(int amount)
        {
            return SaveGameManager.Current != null && SaveGameManager.Current.Money >= amount;
        }

        /// <summary>Takes the money if the player has it; false, with nothing taken, if not.</summary>
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

        /// <summary>Energy spent on play, never below the level at which the game steps in.</summary>
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
