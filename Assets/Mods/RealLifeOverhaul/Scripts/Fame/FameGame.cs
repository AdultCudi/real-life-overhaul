using System;
using System.Collections.Generic;
using Controllers;
using Extensions;
using Helpers;
using Localizor;
using PlayerActivity;
using RealLifeOverhaul.Economy;
using Streets.Pedestrians;
using UI;
using UI.Load;
using UnityEngine;

namespace RealLifeOverhaul.Fame
{
    /// <summary>What Standing asks of the rest of the mod, filled in by Wiring/StandingLinks. Each is optional.</summary>
    public static class FameBridges
    {
        public delegate bool StaffAsk(string place, out int count, out int standing, out int low, out bool strike);

        public delegate bool NeedAsk(out Vector3 where);

        /// <summary>Flare's followers. Default: none.</summary>
        public static Func<int> Followers;

        /// <summary>A business's staff as the company knows them, with a strike now or in the last week. False if it does not know them.</summary>
        public static StaffAsk Staff;

        /// <summary>The buildings the player lets, as property keeps them. Default: none.</summary>
        public static Action<List<FameHolding>> Holdings;

        /// <summary>The best of the player's brands' recognition in each district, 0 to 100. Default: none.</summary>
        public static Action<Dictionary<string, int>> Brand;

        /// <summary>A visit moves each of a business's staff's standing by so many points. Returns how many it moved. Default: nobody.</summary>
        public static Func<string, int, int> Visited;

        /// <summary>Opens the Standing page (a notification was clicked). Default: nothing.</summary>
        public static Action Open;

        /// <summary>Has business pressure write a business's customers again; the flag says it is the new day. Default: nothing.</summary>
        public static Func<BuildingRegistration, bool, bool> Footfall;

        /// <summary>A piece for the morning paper, by locale key. Default: nothing is printed.</summary>
        public static Action<string> Story;

        /// <summary>Somebody in the street needs help now (an accident, a crime's victim, a person down), and where. Default: nobody.</summary>
        public static NeedAsk Need;

        /// <summary>Somebody the player has still to meet in the social life, met at an evening; their name, or null. Default: nobody.</summary>
        public static Func<string> Meet;

        /// <summary>The changes the council could be pushed to make now. Default: none, and no lobbying.</summary>
        public static Action<List<FameLever>> Levers;

        /// <summary>A push won: the lever moves to the position on the day given. False if it could not. Default: never.</summary>
        public static Func<int, int, int, bool> Lobbied;

        /// <summary>A bribe of so much came out: a court case is opened. Its number, or 0 with no court. Default: none.</summary>
        public static Func<int, int> Investigate;

        /// <summary>Points a business's interior moves its customers' figure, by its place (Interiors). Default: nothing.</summary>
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
    }

    /// <summary>
    /// The one place Standing touches the game: the clock, what the player is worth, their businesses and their week
    /// (Economy/Finance/Books), and who is near them. Never asked per frame.
    /// </summary>
    public static class FameGame
    {
        private static readonly List<BuildingRegistration> Regs = new List<BuildingRegistration>(16);

        public static int Day => SaveGameManager.Current != null ? SaveGameManager.Current.Day : 0;
        public static int MinuteOfDay => SaveGameManager.Current != null ? SaveGameManager.Current.Hour * 60 + Mathf.FloorToInt(SaveGameManager.Current.Minute) : 0;

        /// <summary>A business's place as business pressure and the company write it.</summary>
        public static string Place(BuildingRegistration reg) => BusinessGame.Place(reg);

        public static string Name(BuildingRegistration reg) => reg != null && !string.IsNullOrEmpty(reg.BusinessName) ? reg.BusinessName : "?";

        /// <summary>The player's businesses that take money, in a list of Standing's own that each call fills again.</summary>
        public static List<BuildingRegistration> Businesses()
        {
            Books.Businesses(Regs);
            return Regs;
        }

        /// <summary>Everything the daily round reads. A bridge that throws counts as absent.</summary>
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
            try
            {
                into.Followers = FameBridges.Followers != null ? Math.Max(0, FameBridges.Followers()) : 0;
            }
            catch (Exception)
            {
                into.Followers = 0;
            }
            double week = 0.0;
            List<BuildingRegistration> shops = Businesses();
            for (int i = 0; i < shops.Count; i++)
            {
                BuildingRegistration reg = shops[i];
                int onRecord;
                week += Books.AverageProfit(reg.Address, 7, out onRecord) * 7.0;
                into.Shops.Add(Shop(reg, onRecord));
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

        private static FameShop Shop(BuildingRegistration reg, int onRecord)
        {
            var shop = new FameShop { Place = Place(reg), District = GameRegistrations.District(reg), Standing = -1 };
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
                shop.Standing = -1;   // the workers' reputation keeps heading where it was
            }
            return shop;
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

        public static string DistrictName(string district)
        {
            return string.IsNullOrEmpty(district) ? FameText.T("reallife_fame_district_none") : LocalizorManager.IsLocalizedKey(district) ? district.GetLocalization() : district;
        }

        /// <summary>The player's business they are standing in, or null.</summary>
        public static BuildingRegistration Inside()
        {
            if (!BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance == null)
            {
                return null;
            }
            BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
            return here != null && Books.IsBusiness(here) ? here : null;
        }

        /// <summary>
        /// What stops the player joining a video call right now, read from the game. The phone being open and the game paused do not
        /// count (the call is joined from the phone, which pauses the game); see FameJoin. Runs once per press of Join.
        /// </summary>
        public static FameJoinBlock JoinBlock()
        {
            var s = new FameJoinState();
            var uis = InstanceBehavior<UIs>.Instance;
            s.Driving = PlayerHelper.IsUsingVehicle;
            s.Talking = DialogController.current != null || HudConfirm.isOpen;
            s.Activity = PlayerActivityUI.IsPanelOpen;
            s.Subway = SubwaySystem.IsRiding;
            s.Boat = CasinoBoatManager.IsOnCasinoBoat;
            s.Scene = Wiring.Scenes.PlayerInScene;
            s.TripWindow = GameUi.TravelList.AnyOpen;
            s.Other = uis == null || LoadScene.isLoading || GameMembers.PlayerDead || EnergyHelper.goingToHospital || uis.timeMachine.isRunning
                      || Leisure.PhotoMode.Active || Photos.CameraMode.Active || ThirdPersonCamera.AnyWindow(false);
            return FameJoin.Decide(s);
        }

        /// <summary>Takes the game off pause for a scene begun from the phone, which paused it; a call is not held at a standstill.</summary>
        public static void Unpause()
        {
            var uis = InstanceBehavior<UIs>.Instance;
            if (uis != null && uis.gameSpeed != null && uis.gameSpeed.Paused)
            {
                uis.gameSpeed.SetPause(false);
            }
        }

        public static bool OnFootOutside()
        {
            return !BuildingManager.IsInsideBuilding && !PlayerHelper.IsUsingVehicle && LifeEvents.IsSafeForEvent() && !Wiring.Scenes.PlayerHeld;
        }

        public static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
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

        /// <summary>How many of the game's own passers-by are within so many metres: the witnesses to something.</summary>
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

        public static void Face(ThirdPersonCharacter who, CharacterEmojiName emoji) => ActorPool.Instance?.ShowEmoji(who, emoji, 3.5f);

        /// <summary>The player pays for something of Standing's: false, after the game's own "not enough money" notice, if they cannot.</summary>
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

        /// <summary>The district where the player has most businesses, else the one they are in; null if neither is known.</summary>
        public static string BusiestDistrict()
        {
            List<BuildingRegistration> all = Businesses();
            string best = null;
            int most = 0;
            for (int i = 0; i < all.Count; i++)
            {
                string d = GameRegistrations.District(all[i]);
                int n = 0;
                for (int k = 0; k < all.Count; k++)
                {
                    n += GameRegistrations.District(all[k]) == d ? 1 : 0;
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
