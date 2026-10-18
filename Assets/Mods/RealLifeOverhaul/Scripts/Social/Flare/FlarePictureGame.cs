using System;
using System.Collections.Generic;
using System.IO;
using BigAmbitions.DayNightCycle;
using Character.Customization;
using Entities;
using Extensions;
using Helpers;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Flare
{
    /// <summary>A business's front as the bank of city photos knows it: its place, district, kind and where its door is.</summary>
    public struct FlareFrontSpot
    {
        public string Place;
        public string District;
        public int Niche;
        public float X, Z;
    }

    /// <summary>Flare's pictures' one door to the game, asked when a picture is made or on the director's second; each says "none" rather than throw.</summary>
    public static class FlarePictureGame
    {
        private const string EmptyBusiness = "ba:businesstype_empty";
        private static readonly List<EmployeeInstance> Staffed = new List<EmployeeInstance>(16);
        private static readonly string Square = LogoSize.SquareSign.ToStringFast();

        /// <summary>The save's own id (the game's character id), or null.</summary>
        public static string SaveId => SaveGameManager.Current != null ? SaveGameManager.Current.characterId : null;

        /// <summary>The folder of the round pictures, among the contacts' faces.</summary>
        public static string FaceFolder => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Portraits");

        /// <summary>The game's logo of a business or brand, loaded without waiting into <paramref name="back"/> (null for none); the game's texture, never destroyed.</summary>
        public static void Logo(string name, bool mine, Action<Texture2D> back)
        {
            try
            {
                if (string.IsNullOrEmpty(name))
                {
                    back(null);
                    return;
                }
                if (mine)
                {
                    back(LogoHelper.GetBusinessLogoTexture(name, LogoSize.SquareSign, true));
                    return;
                }
                string safe = LogoHelper.GetBusinessNamePathSafe(name);
                string key = "BusinessLogos/" + safe + "/" + Square;
                bool modded = File.Exists(Path.Combine(LogoHelper.GetModdedBusinessLogosFolder(), safe, Square + ".jpg"));
                if (!modded && !AddressableChecksHelper.IsValidAddressableKey(key + ".jpg") && !AddressableChecksHelper.IsValidAddressableKey(key + ".png"))
                {
                    back(null);
                    return;
                }
                // The game may answer later, from its loader: inside the guard.
                LogoHelper.LoadBusinessLogoTextureAsync(name, LogoSize.SquareSign,
                    texture => UiClone.Safe(() => back(texture != null && texture != LogoHelper.GetNullTexture() ? texture : null)));
            }
            catch (Exception e)
            {
                FlareDirector.Logger?.Warn("Flare: the game's logo of '" + name + "' could not be had (" + e.Message + "); its emblem is shown.");
                back(null);
            }
        }

        /// <summary>The names of the player's own businesses, into the list given (emptied first).</summary>
        public static void MyBusinesses(List<string> into)
        {
            into.Clear();
            List<BuildingRegistration> mine = FlareGame.Businesses();
            for (int i = 0; i < mine.Count; i++)
            {
                if (mine[i] != null && !string.IsNullOrEmpty(mine[i].BusinessName))
                {
                    into.Add(mine[i].BusinessName);
                }
            }
        }

        /// <summary>The game's portrait of the player's character, as its file; null if the game has not made one.</summary>
        public static string PlayerPortrait()
        {
            try
            {
                GameInstance save = SaveGameManager.Current;
                string path = save != null ? PortraitGenerator.GetCharacterPortraitPath(save) : null;
                return path != null && File.Exists(path) ? path : null;
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>One of the player's staff by name, as the game has them (for their face); null if none of that name.</summary>
        public static CharacterData Staff(string name)
        {
            try
            {
                List<EmployeeInstance> all = EmployeeHelper.GetEmployeeInstances();
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    CharacterData data = all[i] != null ? all[i].characterData : null;
                    if (data != null && data.name == name)
                    {
                        return data;
                    }
                }
            }
            catch (Exception)
            {
                // no face: the initials stay
            }
            return null;
        }

        /// <summary>Where the player stands and which way they face; false indoors, on the main menu or during a load.</summary>
        public static bool Outdoors(out Vector3 position, out Vector3 forward)
        {
            position = forward = Vector3.zero;
            try
            {
                if (BuildingManager.IsInsideBuilding || PlayerHelper.PlayerController == null)
                {
                    return false;
                }
                Transform t = PlayerHelper.PlayerController.transform;
                position = t.position;
                forward = t.forward;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True while the player is in a vehicle: a city photo waits, as its renders (a frame of 10 to 30 ms) are felt most at the wheel.</summary>
        public static bool Driving()
        {
            try
            {
                return PlayerHelper.IsUsingVehicle;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>True while it rains as the game shows it (wet streets, the rain falling or the mist after it).</summary>
        public static bool Raining()
        {
            try
            {
                return RainHelper.isRaining;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>The next slice of the businesses' fronts until <paramref name="budgetTicks"/> Stopwatch ticks pass; true once the register is done.</summary>
        public static bool FrontSlice(ref int cursor, List<FlareFrontSpot> into, long budgetTicks)
        {
            try
            {
                List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                if (all == null || city == null)
                {
                    return false;
                }
                long until = System.Diagnostics.Stopwatch.GetTimestamp() + budgetTicks;
                for (; cursor < all.Count; cursor++)
                {
                    if ((cursor & 7) == 0 && System.Diagnostics.Stopwatch.GetTimestamp() > until)
                    {
                        return false;
                    }
                    BuildingRegistration reg = all[cursor];
                    if (reg == null || !reg.HasValidAddress || string.IsNullOrEmpty(reg.BusinessName) || reg.businessTypeName == EmptyBusiness || reg.Address == null)
                    {
                        continue;
                    }
                    Transform door = DoorOf(city.FindCityBuildingController(reg.Address));
                    if (door == null)
                    {
                        continue;
                    }
                    Vector3 at = door.position;
                    into.Add(new FlareFrontSpot { Place = FlareGame.Place(reg), District = reg.Neighborhood, Niche = (int)FlareCast.NicheOf(reg.businessTypeName), X = at.x, Z = at.z });
                }
                return true;
            }
            catch (Exception)
            {
                cursor = int.MaxValue;   // the register could not be read: no fronts this day, the streets still
                return true;
            }
        }

        /// <summary>A business's front by its place: its first entrance door and the way out, level; false for no building or door.</summary>
        public static bool Front(string place, out Vector3 door, out Vector3 outward)
        {
            door = outward = Vector3.zero;
            try
            {
                BuildingRegistration reg = FlareLiveGame.AnyBusiness(place);
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                CityBuildingController controller = city != null && reg != null && reg.Address != null ? city.FindCityBuildingController(reg.Address) : null;
                Transform doorAt = DoorOf(controller);
                if (doorAt == null)
                {
                    return false;
                }
                door = doorAt.position;
                Vector3 away = door - controller.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.25f)
                {
                    away = -doorAt.forward;
                    away.y = 0f;
                }
                outward = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        // The first entrance door of a building, or null.
        private static Transform DoorOf(CityBuildingController controller)
        {
            return controller != null && controller.entranceDoors != null && controller.entranceDoors.Length > 0 && controller.entranceDoors[0] != null
                ? controller.entranceDoors[0].doorTransform
                : null;
        }
    }
}
