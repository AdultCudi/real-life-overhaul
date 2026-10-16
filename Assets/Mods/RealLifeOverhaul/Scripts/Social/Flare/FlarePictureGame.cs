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
    /// <summary>A business's front as the bank of city photos knows it: its place, district, kind and where its door is on the ground.</summary>
    public struct FlareFrontSpot
    {
        public string Place;
        public string District;
        public int Niche;
        public float X, Z;
    }

    /// <summary>
    /// Flare's pictures' one door to the game (FlareAvatars, FlareFacePainter, FlareCityCamera, the bank of city photos):
    /// the game's own logos of its businesses, the game's portrait of the player's character, one of the player's staff as
    /// the game has them, where the player stands and whether outdoors, whether it rains, a business's front door, and the
    /// fronts of the city's businesses read a slice at a time. Each is asked when a picture is made or on the director's
    /// second, never per frame, and each says "none" rather than throw: a picture that cannot be had leaves its stand-in.
    /// </summary>
    public static class FlarePictureGame
    {
        private const string EmptyBusiness = "ba:businesstype_empty";
        private static readonly List<EmployeeInstance> Staffed = new List<EmployeeInstance>(16);
        private static readonly string Square = LogoSize.SquareSign.ToStringFast();

        /// <summary>The save's own id (the game's character id), or null.</summary>
        public static string SaveId => SaveGameManager.Current != null ? SaveGameManager.Current.characterId : null;

        /// <summary>The folder of the round pictures, among the contacts' faces (persistentDataPath/RealLifeOverhaul/Portraits).</summary>
        public static string FaceFolder => Path.Combine(Application.persistentDataPath, "RealLifeOverhaul", "Portraits");

        // ---------------------------------------------------------------- logos

        /// <summary>
        /// The game's own logo of a business or a brand, as its signs and BizMan show it: a business of the player's from the
        /// save's folder (read once, then the game keeps it), any other from the game's own logos, loaded the game's way
        /// without waiting. 'back' gets the texture, or null when the game has no logo of that name (asked first, so the
        /// game's warning for a missing logo is never written). The texture is the game's: shown, never changed or destroyed.
        /// </summary>
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
                LogoHelper.LoadBusinessLogoTextureAsync(name, LogoSize.SquareSign, texture => UiClone.Safe(() => back(texture != null && texture != LogoHelper.GetNullTexture() ? texture : null)));   // (the game may answer later, from its loader: inside the guard)
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

        // ---------------------------------------------------------------- people

        /// <summary>The game's own portrait of the player's character (the picture at the top of the screen), as its file; null if the game has not made one.</summary>
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
                // (no face: the initials stay)
            }
            return null;
        }

        // ---------------------------------------------------------------- the city photos' places

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

        /// <summary>
        /// The next slice of the city's businesses' fronts, from 'cursor' on, into 'into', until 'budgetTicks' of the
        /// Stopwatch have passed (the director gives a millisecond a second): each business of the game's register with a
        /// name and a door (the city's, the player's and another player's alike: a photo is only looked at). True once the
        /// register is done.
        /// </summary>
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
                    CityBuildingController controller = city.FindCityBuildingController(reg.Address);
                    if (controller == null || controller.entranceDoors == null || controller.entranceDoors.Length == 0 || controller.entranceDoors[0] == null
                        || controller.entranceDoors[0].doorTransform == null)
                    {
                        continue;
                    }
                    Vector3 door = controller.entranceDoors[0].doorTransform.position;
                    into.Add(new FlareFrontSpot { Place = FlareGame.Place(reg), District = reg.Neighborhood, Niche = (int)FlareCast.NicheOf(reg.businessTypeName), X = door.x, Z = door.z });
                }
                return true;
            }
            catch (Exception)
            {
                cursor = int.MaxValue;   // (the register could not be read: no fronts this day, the streets still)
                return true;
            }
        }

        /// <summary>
        /// The front of a business by its place ("street~number"): its first entrance door, and the way out of it (from the
        /// building's middle to the door, level). False if the city has no such building or door to ask.
        /// </summary>
        public static bool Front(string place, out Vector3 door, out Vector3 outward)
        {
            door = outward = Vector3.zero;
            try
            {
                BuildingRegistration reg = FlareLiveGame.AnyBusiness(place);
                CityManager city = InstanceBehavior<CityManager>.IsInitialized ? InstanceBehavior<CityManager>.Instance : null;
                CityBuildingController controller = city != null && reg != null && reg.Address != null ? city.FindCityBuildingController(reg.Address) : null;
                if (controller == null || controller.entranceDoors == null || controller.entranceDoors.Length == 0 || controller.entranceDoors[0] == null || controller.entranceDoors[0].doorTransform == null)
                {
                    return false;
                }
                door = controller.entranceDoors[0].doorTransform.position;
                Vector3 away = door - controller.transform.position;
                away.y = 0f;
                if (away.sqrMagnitude < 0.25f)
                {
                    away = -controller.entranceDoors[0].doorTransform.forward;
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
    }
}
