using System;
using System.Collections.Generic;
using Controllers;
using Helpers;
using Localizor;
using RealLifeOverhaul.Economy;
using Streets.Pedestrians;
using UnityEngine;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// What Standing asks of the rest of the mod. Each is optional and has a default, so Scripts/Fame works alone (wealth
    /// from the game's own figures, the customers' reputation from the game's satisfaction, the street); Scripts/Wiring/
    /// StandingLinks.cs fills them in. Standing never calls another feature directly, and what happens elsewhere that
    /// moves it (a good deed, a strike, a complaint) is told to it from there.
    /// </summary>
    public static class FameBridges
    {
        /// <summary>Flare's followers. Default: none.</summary>
        public static Func<int> Followers;

        /// <summary>A business's staff as the company knows them: how many, their standing on average, how many stand low, and a strike now or in the last week. False if the company does not know them. Default: never.</summary>
        public static StaffAsk Staff;

        public delegate bool StaffAsk(string place, out int count, out int standing, out int low, out bool strike);

        /// <summary>The buildings the player lets, as property keeps them: district, let or empty, the rent asked against the market's and the rent a day. Default: none.</summary>
        public static Action<List<FameHolding>> Holdings;

        /// <summary>The best of the player's brands' recognition in each district, 0 to 100, into the dictionary given. Default: none.</summary>
        public static Action<Dictionary<string, int>> Brand;

        /// <summary>A visit to a business: each of its staff's standing moved by so many points through the company. Returns how many it moved. Default: nobody.</summary>
        public static Func<string, int, int> Visited;

        /// <summary>The Standing page is opened (a notification was clicked). Default: nothing.</summary>
        public static Action Open;

        /// <summary>Has a business's customers written again now that Standing's part of them has changed (business pressure's own writer), and whether it is the new day. Default: nothing is written.</summary>
        public static Func<BuildingRegistration, bool, bool> Footfall;

        /// <summary>A finished piece for the morning paper (one locale key). Default: nothing is printed.</summary>
        public static Action<string> Story;

        /// <summary>Somebody in the street needs help now (an accident, a crime's victim, a person down) and where. Default: nobody.</summary>
        public static NeedAsk Need;

        public delegate bool NeedAsk(out UnityEngine.Vector3 where);

        /// <summary>Somebody the player has still to meet in the social life is met at an evening (a place of meeting of its own). Their name, or null. Default: nobody.</summary>
        public static Func<string> Meet;

        /// <summary>The changes the council could be pushed to make now, for a business owner, into the list given. Default: none (no lobbying).</summary>
        public static Action<List<FameLever>> Levers;

        /// <summary>A push on the council won: the lever is to move to the position on the day given. False if it could not. Default: never.</summary>
        public static Func<int, int, int, bool> Lobbied;

        /// <summary>A bribe of so much came out: a court case is opened. Its number, or 0 with no court. Default: none.</summary>
        public static Func<int, int> Investigate;

        /// <summary>Points a business's interior moves its figure in the customers' reputation, by its place (Scripts/Interiors). Default: nothing.</summary>
        public static Func<string, int> Interior;

        public static void Reset()
        {
            Meet = null;
            Levers = null;
            Lobbied = null;
            Investigate = null;
            Interior = null;
            Footfall = null;
            Story = null;
            Need = null;
            Followers = null;
            Staff = null;
            Holdings = null;
            Brand = null;
            Visited = null;
            Open = null;
        }

        internal static T Ask<T>(Func<T> bridge, T otherwise)
        {
            try
            {
                return bridge != null ? bridge() : otherwise;
            }
            catch (Exception)
            {
                return otherwise;
            }
        }
    }

    /// <summary>
    /// The one place Standing touches the game: the clock, what the player is worth (the game's own figure, as Persona and
    /// Finance show it), their businesses with the game's own satisfaction figures and their week from the game's daily
    /// statements (through Economy/Finance/Books), the buildings they own, the district they are in, who is near them in
    /// the street and in their own shop. Asked on the day, the hour and the street's look; never per frame.
    /// </summary>
    public static class FameGame
    {
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        public static int Hour => SaveGameManager.Current != null ? SaveGameManager.Current.Hour : 0;
        public static int MinuteOfDay => SaveGameManager.Current != null ? SaveGameManager.Current.Hour * 60 + Mathf.FloorToInt(SaveGameManager.Current.Minute) : 0;

        /// <summary>A business's place as business pressure and the company write it.</summary>
        public static string Place(BuildingRegistration reg)
        {
            return reg != null ? BusinessGame.Place(reg) : null;
        }

        public static string Name(BuildingRegistration reg)
        {
            return reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : "?";
        }

        /// <summary>The player's businesses that take money, into a list of Standing's own (filled again on each call).</summary>
        public static List<BuildingRegistration> Businesses()
        {
            Books.Businesses(Regs);
            return Regs;
        }

        /// <summary>Everything the daily round reads, into the inputs given.</summary>
        public static void Read(FameInputs into)
        {
            into.Clear();
            GameInstance save = SaveGameManager.Current;
            if (save == null)
            {
                return;
            }
            try
            {
                into.NetWorth = (long)PlayerHelper.GetPersonalWealth().CurrentWealth;
            }
            catch (Exception)
            {
                into.NetWorth = (long)save.Money;
            }
            into.Properties = save.realEstate != null ? save.realEstate.Count : 0;
            into.Followers = Math.Max(0, FameBridges.Ask(FameBridges.Followers, 0));
            double week = 0.0;
            List<BuildingRegistration> shops = Businesses();
            for (int i = 0; i < shops.Count; i++)
            {
                BuildingRegistration reg = shops[i];
                int onRecord;
                week += Books.AverageProfit(reg.Address, 7, out onRecord) * 7.0;
                var shop = new FameShop { Place = Place(reg), Name = Name(reg), District = reg.Neighborhood, Standing = -1 };
                Satisfaction s = reg.satisfaction;
                if (s != null && onRecord > 0)
                {
                    shop.Figures = true;
                    shop.Satisfaction = s.overall;
                    shop.Pricing = s.pricing;
                    try
                    {
                        shop.Interior = FameBridges.Interior != null ? FameBridges.Interior(shop.Place) : 0;
                    }
                    catch (Exception)
                    {
                        shop.Interior = 0;
                    }
                }
                int count, standing, low;
                bool strike;
                try
                {
                    if (FameBridges.Staff != null && FameBridges.Staff(shop.Place, out count, out standing, out low, out strike))
                    {
                        shop.Staff = count;
                        shop.Standing = standing;
                        shop.Low = low;
                        shop.Strike = strike;
                    }
                }
                catch (Exception)
                {
                    shop.Standing = -1;   // the company could not be asked: the workers' reputation is left where it is heading
                }
                into.Shops.Add(shop);
            }
            try
            {
                FameBridges.Holdings?.Invoke(into.Holdings);
                FameBridges.Brand?.Invoke(into.Brand);
            }
            catch (Exception)
            {
                into.Holdings.Clear();
                into.Brand.Clear();
            }
            for (int i = 0; i < into.Holdings.Count; i++)
            {
                week += Math.Max(0, into.Holdings[i].Rent) * 7.0;
            }
            into.Weekly = (long)Math.Max(0.0, week);
        }

        /// <summary>The district the player is in, by the game's own name; null if it is not known.</summary>
        public static string DistrictHere()
        {
            try
            {
                string here = NeighborhoodHelper.CurrentNeighborhood;
                return string.IsNullOrEmpty(here) ? null : here;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>A district by the game's own name, in words.</summary>
        public static string DistrictName(string district)
        {
            return string.IsNullOrEmpty(district) ? FameText.T("reallife_fame_district_none") : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>The business of the player's they are standing in, or null.</summary>
        public static BuildingRegistration Inside()
        {
            if (!BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance == null)
            {
                return null;
            }
            BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
            return here != null && Books.IsBusiness(here) ? here : null;
        }

        /// <summary>True out of doors, on foot, with nothing of the game's or the mod's holding the player.</summary>
        public static bool OnFootOutside()
        {
            return !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle && LifeEvents.IsSafeForEvent() && !Wiring.Scenes.PlayerHeld;
        }

        /// <summary>The nearest of the game's own passers-by within so many metres who can be seen, or null.</summary>
        public static ThirdPersonCharacter PasserBy(float within)
        {
            List<Pedestrian> walkers = PerfGame.Pedestrians;
            if (walkers == null || PlayerHelper.PlayerController == null)
            {
                return null;
            }
            Vector3 player = PlayerHelper.GetPosition();
            ThirdPersonCharacter who = null;
            float best = within * within;
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || walker.tpc == null || !walker.tpc.visible || !walker.gameObject.activeInHierarchy || RagdollSystem.Holds(walker.tpc))
                {
                    continue;
                }
                Vector3 offset = walker.transform.position - player;
                float sqr = offset.x * offset.x + offset.z * offset.z;
                if (sqr < best && Mathf.Abs(offset.y) < 2f)
                {
                    best = sqr;
                    who = walker.tpc;
                }
            }
            return who;
        }

        /// <summary>How many of the game's own passers-by are within so many metres (the witnesses to something).</summary>
        public static int PassersBy(Vector3 where, float within)
        {
            List<Pedestrian> walkers = PerfGame.Pedestrians;
            int n = 0;
            for (int i = 0; walkers != null && i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker != null && walker.tpc != null && walker.gameObject.activeInHierarchy && !RagdollSystem.Holds(walker.tpc))
                {
                    Vector3 offset = walker.transform.position - where;
                    n += offset.x * offset.x + offset.z * offset.z < within * within ? 1 : 0;
                }
            }
            return n;
        }

        /// <summary>A face over up to so many of the customers in the shop the player stands in. How many were shown.</summary>
        public static int Customers(CharacterEmojiName emoji, int atMost)
        {
            ActorPool pool = ActorPool.Instance;
            List<Customer> customers = IndoorCustomerSpawner.Customers;
            int shown = 0;
            for (int i = 0; pool != null && customers != null && i < customers.Count && shown < atMost; i++)
            {
                Customer customer = customers[i];
                if (customer != null && !customer.isPlayer && customer.tpc != null && customer.isActiveAndEnabled && customer.tpc.visible)
                {
                    pool.ShowEmoji(customer.tpc, emoji, 4f);
                    shown++;
                }
            }
            return shown;
        }

        /// <summary>A face over somebody in the street.</summary>
        public static void Face(ThirdPersonCharacter who, CharacterEmojiName emoji)
        {
            ActorPool.Instance?.ShowEmoji(who, emoji, 3.5f);
        }

        /// <summary>The player pays for something of Standing's: false (with the game's own "not enough money" notice) if they cannot.</summary>
        public static bool Pay(int amount, string transaction)
        {
            return amount <= 0 || GameManager.ChangeMoneySafe(-amount, new TransactionInfo(transaction), null, null, false, true);
        }

        /// <summary>One of the game's own rivals by name, for the other side of a push on the council; null while the game's rivals are off.</summary>
        public static string Rival(int pick)
        {
            try
            {
                if (SaveGameManager.Current == null || !BigAmbitions.Rivals.RivalsHelper.IsFeatureEnabled)
                {
                    return null;
                }
                var rivals = BigAmbitions.Rivals.RivalsHelper.GetSpecialRivals();
                var names = new List<string>(8);
                if (rivals != null)
                {
                    foreach (var rival in rivals)
                    {
                        string name = rival != null && rival.rivalData != null ? rival.rivalData.rivalName : null;
                        if (!string.IsNullOrEmpty(name) && !names.Contains(name))
                        {
                            names.Add(name);
                        }
                    }
                }
                return names.Count > 0 ? names[Math.Abs(pick) % names.Count] : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>The district where the player has most businesses, or the one they are in; null if neither is known.</summary>
        public static string BusiestDistrict()
        {
            List<BuildingRegistration> all = Businesses();
            string best = null;
            int most = 0;
            for (int i = 0; i < all.Count; i++)
            {
                string d = all[i].Neighborhood;
                int n = 0;
                for (int k = 0; k < all.Count; k++)
                {
                    n += all[k].Neighborhood == d ? 1 : 0;
                }
                if (!string.IsNullOrEmpty(d) && n > most)
                {
                    most = n;
                    best = d;
                }
            }
            return best ?? DistrictHere();
        }
    }
}
