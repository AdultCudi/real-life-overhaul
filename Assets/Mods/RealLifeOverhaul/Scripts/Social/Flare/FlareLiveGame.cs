using System;
using System.Collections.Generic;
using System.Reflection;
using BAModAPI;
using Helpers;
using Unity.Collections;
using UnityEngine;
using UnityEngine.Experimental.Rendering;

namespace RealLifeOverhaul.Flare
{
    /// <summary>Where the living network touches the game beside FlareGame. Asked on a post, a click, the hour and the day; never per frame.</summary>
    public static class FlareLiveGame
    {
        public static IModLogger Logger;

        private const string EmptyBusiness = "ba:businesstype_empty";
        private const int ShopsMost = 24;            // the city's businesses the day's posts pick from

        private static bool _filterTried, _filterOn;
        private static object _chat, _nobody;
        private static FieldInfo _steamIdValue;

        /// <summary>Any business at a place, the player's, another player's or the city's; null for none.</summary>
        public static BuildingRegistration AnyBusiness(string place)
        {
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            if (all == null || string.IsNullOrEmpty(place))
            {
                return null;
            }
            for (int i = 0; i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && reg.HasValidAddress && !string.IsNullOrEmpty(reg.BusinessName) && FlareGame.Place(reg) == place)
                {
                    return reg;
                }
            }
            return null;
        }

        /// <summary>True for a business of the city's: nobody's, neither the player's nor another player's of a session.</summary>
        public static bool City(BuildingRegistration reg)
        {
            return reg != null && !reg.RentedByPlayer;
        }

        /// <summary>The business the player stands in, whoever's it is; null outside one.</summary>
        public static BuildingRegistration InsideAny()
        {
            if (!BuildingManager.IsInsideBuilding || InstanceBehavior<BuildingManager>.Instance == null)
            {
                return null;
            }
            BuildingRegistration here = InstanceBehavior<BuildingManager>.Instance.buildingRegistration;
            return here != null && !string.IsNullOrEmpty(here.BusinessName) && here.businessTypeName != EmptyBusiness ? here : null;
        }

        /// <summary>A sample of the city's businesses (named, not empty, nobody's) spread over the save's list for the day's posts; one pass a day.</summary>
        public static void CityShops(List<FlareBiz> into)
        {
            into.Clear();
            List<BuildingRegistration> all = SaveGameManager.Current != null ? SaveGameManager.Current.BuildingRegistrations : null;
            if (all == null)
            {
                return;
            }
            int named = 0;
            for (int i = 0; i < all.Count; i++)
            {
                named += Shop(all[i]) ? 1 : 0;
            }
            int step = Math.Max(1, named / ShopsMost), k = 0;
            for (int i = 0; i < all.Count && into.Count < ShopsMost; i++)
            {
                BuildingRegistration reg = all[i];
                if (!Shop(reg) || k++ % step != 0)
                {
                    continue;
                }
                into.Add(Biz(reg, false));
            }
        }

        private static bool Shop(BuildingRegistration reg)
        {
            return reg != null && !reg.RentedByPlayer && reg.HasValidAddress && !string.IsNullOrEmpty(reg.BusinessName) && reg.businessTypeName != EmptyBusiness
                   && GameRegistrations.Found(reg);
        }

        public static FlareBiz Biz(BuildingRegistration reg, bool mine)
        {
            Satisfaction s = reg.satisfaction;
            return new FlareBiz
            {
                Place = FlareGame.Place(reg), Name = FlareGame.Name(reg), District = GameRegistrations.District(reg), Niche = FlareCast.NicheOf(reg.businessTypeName),
                Stars = s != null ? s.overall : -1, Mine = mine, Open = OpenHours(reg)
            };
        }

        // The hours of today the game has a business open (bit h for hour h): its own opening hours, none while it is
        // temporarily closed (BusinessHelper.IsBusinessOpen reads both). Once a day for each business the day's posts see.
        private static int OpenHours(BuildingRegistration reg)
        {
            int open = 0;
            for (int h = 0; h < 24 && !reg.temporarilyClosed; h++)
            {
                open |= BusinessHelper.IsBusinessOpen(reg, h) ? 1 << h : 0;
            }
            return open;
        }

        /// <summary>
        /// Whether the game has the business at a place open at <paramref name="hour"/>: the visit a city review is of. Asked
        /// as the review's hour comes; with <paramref name="today"/> false (the hour's step ran after midnight) only the
        /// temporary closing is read, the day's hours having been read when the review was picked. A business no longer there is closed.
        /// </summary>
        public static bool OpenAt(string place, int hour, bool today)
        {
            BuildingRegistration reg = AnyBusiness(place);
            return reg != null && !reg.temporarilyClosed && (!today || BusinessHelper.IsBusinessOpen(reg, hour));
        }

        /// <summary>Where the player is in the city, on the ground (at the door of a building they are in).</summary>
        public static bool PlayerPoint(out float x, out float z)
        {
            x = z = 0f;
            try
            {
                if (PlayerHelper.PlayerController == null)
                {
                    return false;
                }
                Vector3 at = PlayerHelper.GetCityPosition();
                x = at.x;
                z = at.z;
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        /// <summary>What another player typed through Steam's text filter (Core/GameContract); null when the filter is not there.</summary>
        public static string SteamFilter(string text, string author = null)
        {
            if (string.IsNullOrEmpty(text))
            {
                return text;
            }
            if (!_filterTried)
            {
                _filterTried = true;
                try
                {
                    MethodInfo init = GameContract.SteamFilterInit.Method, filter = GameContract.SteamFilterText.Method;
                    if (init != null && filter != null)
                    {
                        ParameterInfo[] ps = filter.GetParameters();
                        _chat = Enum.ToObject(ps[0].ParameterType, 2);   // TextFilteringContext.Chat
                        _nobody = Activator.CreateInstance(ps[1].ParameterType);   // a SteamId of 0: nobody in particular
                        _steamIdValue = ps[1].ParameterType.GetField("Value", BindingFlags.Public | BindingFlags.Instance);
                        _filterOn = init.Invoke(null, null) is bool on && on;
                    }
                }
                catch (Exception e)
                {
                    _filterOn = false;
                    Logger?.Info("Flare: Steam's own text filter is not there (" + (e.InnerException ?? e).GetType().Name + "); what other players type goes through the mod's own word list.");
                }
                Logger?.Info("Flare: what other players type is filtered by " + (_filterOn ? "Steam's own text filter" : "the mod's own word list") + ".");
            }
            if (!_filterOn)
            {
                return null;
            }
            try
            {
                object who = _nobody;
                ulong id = FlareFilter.SteamIdOf(author);
                if (id != 0UL && _steamIdValue != null && _steamIdValue.FieldType == typeof(ulong))
                {
                    who = Activator.CreateInstance(_nobody.GetType());
                    _steamIdValue.SetValue(who, id);   // a boxed copy: set in place
                }
                return GameContract.SteamFilterText.Method.Invoke(null, new[] { _chat, who, text }) as string;
            }
            catch (Exception e)
            {
                _filterOn = false;
                Logger?.Warn("Flare: Steam's text filter failed (" + (e.InnerException ?? e).GetType().Name + "); the mod's own word list is used from now on.");
                return null;
            }
        }

        /// <summary>The phone is put away (the game's own close of the full menu), for a shot of the screen as it is.</summary>
        public static void CloseMenu()
        {
            UI.UIs uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis != null && uis.fullMenu != null && UI.Smartphone.FullMenu.IsOpen)
            {
                uis.fullMenu.Toggle(false);
            }
        }

        /// <summary>The screen's pixels as the engine reads them, outside the managed heap (a 4K screen is 33 MB), for a worker to make small.</summary>
        public sealed class Shot
        {
            public NativeArray<byte> Pixels;
            public int Width, Height, Bytes;
            public bool Bgr;
        }

        /// <summary>The screen's pixels let go, whatever became of them.</summary>
        public static void Drop(Shot shot)
        {
            if (shot != null && shot.Pixels.IsCreated)
            {
                shot.Pixels.Dispose();
            }
        }

        /// <summary>On a worker: the shot shrunk by averaging to RGB24 rows bottom row first (Core/PhotoThumbs' format); its pixels let go.</summary>
        public static byte[] Shrink(Shot shot, int width, int tallMost, out int outW, out int outH)
        {
            outW = outH = 0;
            try
            {
                int w = shot.Width, h = shot.Height, bytes = shot.Bytes;
                NativeArray<byte> src = shot.Pixels;
                if (w <= 0 || h <= 0 || src.Length < w * h * bytes)
                {
                    return null;
                }
                outW = Math.Max(1, Math.Min(width, w));
                outH = Math.Max(1, (int)Math.Round((double)h * outW / w));
                if (outH > tallMost)
                {
                    outH = tallMost;
                    outW = Math.Max(1, (int)Math.Round((double)w * outH / h));
                }
                var dst = new byte[outW * outH * 3];
                int r0 = shot.Bgr ? 2 : 0, b0 = shot.Bgr ? 0 : 2;
                for (int y = 0; y < outH; y++)
                {
                    int ya = (int)((long)y * h / outH), yb = Math.Max(ya + 1, (int)((long)(y + 1) * h / outH));
                    for (int x = 0; x < outW; x++)
                    {
                        int xa = (int)((long)x * w / outW), xb = Math.Max(xa + 1, (int)((long)(x + 1) * w / outW));
                        long r = 0, g = 0, b = 0;
                        int n = 0;
                        for (int sy = ya; sy < yb && sy < h; sy++)
                        {
                            int row = sy * w * bytes;
                            for (int sx = xa; sx < xb && sx < w; sx++)
                            {
                                int at = row + sx * bytes;
                                r += src[at + r0];
                                g += src[at + 1];
                                b += src[at + b0];
                                n++;
                            }
                        }
                        int o = (y * outW + x) * 3;
                        n = Math.Max(1, n);
                        dst[o] = (byte)(r / n);
                        dst[o + 1] = (byte)(g / n);
                        dst[o + 2] = (byte)(b / n);
                    }
                }
                return dst;
            }
            finally
            {
                Drop(shot);
            }
        }

        /// <summary>The screen as drawn (after WaitForEndOfFrame), copied out and the texture let go at once; null when it cannot be read.</summary>
        public static Shot Capture()
        {
            Texture2D texture = null;
            try
            {
                texture = ScreenCapture.CaptureScreenshotAsTexture();
                if (texture == null)
                {
                    return null;
                }
                int bytes;
                bool bgr;
                if (!Layout(texture.graphicsFormat, out bytes, out bgr))
                {
                    Logger?.Warn("Flare: the screen came as " + texture.graphicsFormat + ", which the camera does not read; no picture.");
                    return null;
                }
                return new Shot
                {
                    Pixels = new NativeArray<byte>(texture.GetRawTextureData<byte>(), Allocator.Persistent), Width = texture.width, Height = texture.height, Bytes = bytes, Bgr = bgr
                };
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
            finally
            {
                if (texture != null)
                {
                    UnityEngine.Object.Destroy(texture);
                }
            }
        }

        private static bool Layout(GraphicsFormat format, out int bytes, out bool bgr)
        {
            bytes = 4;
            bgr = false;
            switch (format)
            {
                case GraphicsFormat.R8G8B8A8_SRGB:
                case GraphicsFormat.R8G8B8A8_UNorm:
                    return true;
                case GraphicsFormat.B8G8R8A8_SRGB:
                case GraphicsFormat.B8G8R8A8_UNorm:
                    bgr = true;
                    return true;
                case GraphicsFormat.R8G8B8_SRGB:
                case GraphicsFormat.R8G8B8_UNorm:
                    bytes = 3;
                    return true;
                default:
                    return false;
            }
        }
    }
}
