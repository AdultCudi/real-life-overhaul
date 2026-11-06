using System;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using Buildings.Outdoors;
using Enums;
using Helpers;
using Streets;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The fronts of the mod's buildings (<see cref="SpecialBuildings"/>) on the game's own sign holders. The game holds
    /// them as to let, so its CityBuildingController.UpdateSign puts "For rent" on their signs; each shows instead what
    /// FrontSignRules.Look says: a name and logo drawn by the game's logo maker on a sign configured as a business's
    /// (lit after dark by the game's own switch), the game's signs off, or the game's own signs again.
    /// Nothing is saved: the sign is configured from a registration of the mod's own, never in the save, and logos are
    /// kept in the game's temporary folder. Every Going Public machine has the same claims; nothing is sent.
    /// COST: once a second, a few flag reads per building in the book and a texture comparison per sign shown (the game
    /// rewrites a building's signs when its registration changes); sign holders are found only when the book changes.
    /// </summary>
    internal static class FrontSigns
    {
        private const string Folder = "RealLifeOverhaul/Signs1";   // under the game's temporary folder; the number changes if the logos' look does
        private const string NoBusiness = "ba:businesstype_empty";

        private static readonly int BaseColorMap = Shader.PropertyToID("_BaseColorMap");   // the face texture of the game's signs
        private static readonly string WideFile = LogoSize.WideSign + ".jpg";
        private static readonly string SquareFile = LogoSize.SquareSign + ".jpg";

        private sealed class Front
        {
            public SpecialEntry Entry;
            public CityBuildingController Building;
            public BuildingSignController[] Wide;
            public BuildingLogoSignController[] Square;
            public Renderer[] SquareFaces;
            public bool[] WideOn;                      // which the mod has put its sign on; the others are off
            public bool[] SquareOn;
            public FrontLook Shown = FrontLook.Game;
            public Logo Logo;                          // shown, while named
            public Logo Wanted;                        // to be shown, worked out when the book changes
            public bool Named;                         // Wanted has been worked out
            public bool Seen;
        }

        private sealed class Logo
        {
            public string Name;
            public string Path;
            public FrontStyle Style;
            public Texture2D Wide;
            public Texture2D Square;
            public bool Asked;
            public bool Failed;
        }

        private static readonly Dictionary<CityBuildingController, Front> Fronts = new Dictionary<CityBuildingController, Front>(40);
        private static readonly Dictionary<string, Logo> Logos = new Dictionary<string, Logo>(40, StringComparer.Ordinal);
        private static readonly List<SpecialEntry> Entries = new List<SpecialEntry>(40);
        private static readonly List<Front> Gone = new List<Front>(8);
        private static readonly List<Material> Faces = new List<Material>(4);
        private static readonly int[] Lit = new int[32];
        private static MaterialPropertyBlock _block;
        private static BuildingRegistration _stand;
        private static FieldInfo _types, _face;
        private static CityManager _city;
        private static int _stamp = int.MinValue;
        private static bool _failed;

        internal static int Count => Fronts.Count;

        /// <summary>Called once a second by SpecialNames: reads the book if it changed, then sets or puts back each front.</summary>
        internal static void Keep()
        {
            if (_failed)
            {
                return;
            }
            try
            {
                CityManager city = InstanceBehavior<CityManager>.Instance;
                if (city == null || city.cityBuildingControllers == null || city.cityBuildingControllers.Length == 0 || GameManager.isCitySceneBeingUnloaded)
                {
                    return;
                }
                if (!ReferenceEquals(city, _city))
                {
                    Fronts.Clear();   // a city just loaded, with the game's own signs
                    _city = city;
                    _stamp = int.MinValue;
                }
                if (_stamp != SpecialBuildings.Stamp)
                {
                    _stamp = SpecialBuildings.Stamp;
                    Read(city);
                }
                foreach (Front front in Fronts.Values)
                {
                    Look(front);
                }
            }
            catch (Exception e) when (Guard.StandsDown(e, "the signs on the mod's buildings"))
            {
                _failed = true;
            }
            catch (Exception e)
            {
                _failed = true;
                SpecialBuildings.Logger?.Error(e);
                SpecialBuildings.Logger?.Warn("Signs: the mod's buildings' fronts could not be kept after the error above; they are given back to the game's own signs.");
                GiveBack();
            }
        }

        /// <summary>The city is unloading: nothing is put back (the next city draws its own signs); the logos are kept.</summary>
        internal static void Forget()
        {
            Fronts.Clear();
            _city = null;
            _stamp = int.MinValue;
        }

        /// <summary>
        /// A feature that switched a building's signs off for a while gives them back: the game's own call, then the
        /// mod's front again at once where it has one.
        /// </summary>
        internal static void Refresh(CityBuildingController building)
        {
            if (building == null || building.buildingRegistration == null)
            {
                return;
            }
            building.UpdateSign();
            if (!_failed && Fronts.TryGetValue(building, out Front front) && front.Shown != FrontLook.Game)
            {
                front.Shown = FrontLook.Game;   // the game's call has just drawn its own
                front.Logo = null;
                Look(front);
            }
        }

        /// <summary>
        /// After the city map has looked at a building (through <see cref="ClaimedMap"/>): one of the mod's that the game
        /// lit only as to let or for sale is put out before the frame is drawn. A dictionary look for any other.
        /// </summary>
        internal static void Unlit(CityBuildingController building)
        {
            if (Fronts.Count == 0 || building == null || !Fronts.TryGetValue(building, out Front front))
            {
                return;
            }
            BuildingRegistration reg = building.buildingRegistration;
            PointOfInterest poi = building.poi;
            if (reg == null || poi == null || poi.Permanent || !poi.gameObject.activeSelf)
            {
                return;   // not lit; a building the game always marks is the player's
            }
            bool players = reg.RentedByPlayer || reg.BuildingOwnedByPlayer;
            bool business = !string.IsNullOrEmpty(reg.businessTypeName) && reg.businessTypeName != NoBusiness;
            bool refused = SpecialNameBook.RefusesEntry(front.Entry, reg.RentedByPlayer, reg.BuildingOwnedByPlayer);
            if (FrontSignRules.PutOut(refused, players, business, reg.AvailableForRent, !reg.AvailableForRent && refused && !business && reg.IsOnSale()))
            {
                building.SetHighlight(false);
            }
        }

        // A building that has left the book is given back to the game's own signs.
        private static void Read(CityManager city)
        {
            foreach (Front front in Fronts.Values)
            {
                front.Seen = false;
            }
            SpecialBuildings.Entries(Entries);
            for (int i = 0; i < Entries.Count; i++)
            {
                SpecialEntry entry = Entries[i];
                CityBuildingController building = city.FindCityBuildingController(new Address(entry.Street, entry.Number));
                if (building == null)
                {
                    continue;
                }
                if (!Fronts.TryGetValue(building, out Front front))
                {
                    front = new Front { Building = building };
                    Fronts[building] = front;
                }
                front.Entry = entry;
                front.Seen = true;
                front.Named = false;   // worked out again: the language may have changed
            }
            Entries.Clear();
            Gone.Clear();
            foreach (Front front in Fronts.Values)
            {
                if (!front.Seen)
                {
                    Gone.Add(front);
                }
            }
            for (int i = 0; i < Gone.Count; i++)
            {
                Front front = Gone[i];
                Fronts.Remove(front.Building);
                if (front.Building != null)
                {
                    Show(front, FrontLook.Game, null);
                }
            }
            Gone.Clear();
        }

        private static void GiveBack()
        {
            try
            {
                foreach (Front front in Fronts.Values)
                {
                    if (front.Building != null && !GameManager.isCitySceneBeingUnloaded)
                    {
                        Show(front, FrontLook.Game, null);
                    }
                }
            }
            catch (Exception e)
            {
                SpecialBuildings.Logger?.Error(e);
            }
            Fronts.Clear();
        }

        private static void Look(Front front)
        {
            CityBuildingController building = front.Building;
            BuildingRegistration reg = building != null ? building.buildingRegistration : null;
            if (reg == null || front.Entry == null)
            {
                return;
            }
            bool refused = SpecialNameBook.RefusesEntry(front.Entry, reg.RentedByPlayer, reg.BuildingOwnedByPlayer);
            FrontLook want = FrontSignRules.Look(front.Entry.Kind, refused, reg.AvailableForRent, front.Entry.Board, Settings.SpecialNames);
            Logo logo = null;
            if (want == FrontLook.Named)
            {
                if (!front.Named)
                {
                    front.Named = true;
                    string name = SpecialBuildings.NameOf(front.Entry);
                    front.Wanted = string.IsNullOrEmpty(name) ? null : LogoFor(name, FrontSignRules.Style(front.Entry.Kind, front.Entry.Id));
                }
                logo = front.Wanted;
                if (logo != null && logo.Wide == null && !logo.Failed)
                {
                    Make(logo);   // until it is made the signs are off: never "For rent"
                }
                if (logo == null || logo.Wide == null)
                {
                    want = FrontLook.Off;
                    logo = null;
                }
            }
            if (want != front.Shown || !ReferenceEquals(logo, front.Logo))
            {
                Show(front, want, logo);
            }
            else if (want != FrontLook.Game && !Holds(front))
            {
                Show(front, want, logo);   // the game has rewritten its signs
            }
        }

        private static void Show(Front front, FrontLook look, Logo logo)
        {
            CityBuildingController building = front.Building;
            if (look == FrontLook.Game)
            {
                BuildingRegistration reg = building.buildingRegistration;
                bool players = reg == null || reg.RentedByPlayer || reg.BuildingOwnedByPlayer;
                if (front.Shown != FrontLook.Game && !players)
                {
                    building.UpdateSign();   // a building that became a player's has them from the game already
                }
                if (front.Shown != FrontLook.Game)
                {
                    SpecialBuildings.Logger?.Info("Signs: " + building.building.Address.ToFormattedString() + " has the game's own signs again.");
                }
                front.Shown = FrontLook.Game;
                front.Logo = null;
                return;
            }
            if (front.Wide == null)
            {
                front.Wide = building.GetComponentsInChildren<BuildingSignController>(true);
                front.Square = building.GetComponentsInChildren<BuildingLogoSignController>(true);
                front.SquareFaces = new Renderer[front.Square.Length];
                front.WideOn = new bool[front.Wide.Length];
                front.SquareOn = new bool[front.Square.Length];
                for (int i = 0; i < front.Square.Length; i++)
                {
                    front.SquareFaces[i] = Face(front.Square[i]);
                }
            }
            bool named = look == FrontLook.Named && logo != null;
            int shown = 0;
            for (int i = 0; i < front.Wide.Length; i++)
            {
                BuildingSignController sign = front.Wide[i];
                front.WideOn[i] = false;
                if (sign == null)
                {
                    continue;
                }
                if (named && Name(sign, logo))
                {
                    front.WideOn[i] = true;
                    shown++;
                }
                else if (sign.gameObject.activeSelf)
                {
                    sign.gameObject.SetActive(false);
                }
            }
            for (int i = 0; i < front.Square.Length; i++)
            {
                BuildingLogoSignController sign = front.Square[i];
                Renderer face = front.SquareFaces[i];
                front.SquareOn[i] = false;
                if (sign == null)
                {
                    continue;
                }
                if (named && face != null && logo.Square != null)
                {
                    front.SquareOn[i] = true;
                    if (!sign.gameObject.activeSelf)
                    {
                        sign.gameObject.SetActive(true);
                    }
                    Block().Clear();
                    face.GetPropertyBlock(Block(), 1);
                    Block().SetTexture(BaseColorMap, logo.Square);
                    face.SetPropertyBlock(Block(), 1);   // as the game sets its own (BuildingLogoSignController.SetSignTexture)
                    shown++;
                }
                else if (sign.gameObject.activeSelf)
                {
                    sign.gameObject.SetActive(false);
                }
            }
            if (front.Shown != look || !ReferenceEquals(front.Logo, logo))
            {
                SpecialBuildings.Logger?.Info("Signs: " + building.building.Address.ToFormattedString() + " (" + front.Entry.Id + ") "
                                              + (named ? "shows " + logo.Name + " on " + shown + " of the game's sign holders" : "has the game's signs off (" + Why(front) + ")")
                                              + "; " + front.Wide.Length + " wide, " + front.Square.Length + " square, nothing of it saved.");
            }
            front.Shown = look;
            front.Logo = named ? logo : null;
        }

        private static string Why(Front front)
        {
            if (front.Entry.Kind == SpecialKind.Police || front.Entry.Kind == SpecialKind.Hotel)
            {
                return "its own dressing is its sign";
            }
            if (front.Entry.Board && front.Entry.Kind != SpecialKind.Campus)
            {
                return "its board is its sign";
            }
            if (!Settings.SpecialNames)
            {
                return "the special buildings' names are off";
            }
            return front.Wanted != null && front.Wanted.Failed ? "its logo could not be made" : "its logo is being made";
        }

        // True while every sign holder still shows what the mod gave it.
        private static bool Holds(Front front)
        {
            if (front.Wide == null)
            {
                return true;
            }
            bool named = front.Shown == FrontLook.Named && front.Logo != null;
            for (int i = 0; i < front.Wide.Length; i++)
            {
                BuildingSignController sign = front.Wide[i];
                if (sign == null)
                {
                    continue;
                }
                bool on = named && front.WideOn[i];
                if (sign.gameObject.activeSelf != on)
                {
                    return false;
                }
                if (on && sign.signMeshRenderer != null)
                {
                    sign.signMeshRenderer.GetSharedMaterials(Faces);
                    Material face = Faces.Count > 1 ? Faces[1] : null;
                    bool ours = face != null && face.HasProperty(BaseColorMap) && ReferenceEquals(face.GetTexture(BaseColorMap), front.Logo.Wide);
                    Faces.Clear();
                    if (!ours)
                    {
                        return false;
                    }
                }
            }
            for (int i = 0; i < front.Square.Length; i++)
            {
                BuildingLogoSignController sign = front.Square[i];
                if (sign == null)
                {
                    continue;
                }
                Renderer face = front.SquareFaces[i];
                bool wanted = named && front.SquareOn[i];
                if (sign.gameObject.activeSelf != wanted)
                {
                    return false;
                }
                if (wanted)
                {
                    face.GetPropertyBlock(Block(), 1);
                    if (!ReferenceEquals(Block().GetTexture(BaseColorMap), front.Logo.Square))
                    {
                        return false;
                    }
                }
            }
            return true;
        }

        // A wide sign configured as a business's (sign type, neon and bulbs), with the logo on its face.
        private static bool Name(BuildingSignController sign, Logo logo)
        {
            if (sign.signMeshRenderer == null)
            {
                return false;
            }
            BuildingRegistration stand = Stand();
            FrontStyle style = logo.Style;
            stand.signAppearanceSettings.signType = (SignType)Type(sign, style.Preferred);
            stand.signAppearanceSettings.signLight = new Color(style.NeonR, style.NeonG, style.NeonB);
            stand.signAppearanceSettings.lamp = new Color(style.LampR, style.LampG, style.LampB);
            if (!sign.gameObject.activeSelf)
            {
                sign.gameObject.SetActive(true);
            }
            sign.ConfigureSign(stand);
            Material[] faces = sign.signMeshRenderer.materials;   // as BuildingSignController.SetSignTexture reaches its face
            if (faces.Length < 2 || faces[1] == null || !faces[1].HasProperty(BaseColorMap))
            {
                sign.gameObject.SetActive(false);
                return false;
            }
            faces[1].SetTexture(BaseColorMap, logo.Wide);
            return true;
        }

        // The mod's own registration, never in the save. An empty name: the game looks up no logo by name; not to let
        // and nobody's: the sign is a business's.
        private static BuildingRegistration Stand()
        {
            if (_stand == null)
            {
                _stand = new BuildingRegistration();
            }
            _stand.BusinessName = string.Empty;
            _stand.AvailableForRent = false;
            _stand.RentedByPlayer = false;
            return _stand;
        }

        private static int Type(BuildingSignController sign, int preferred)
        {
            if (_types == null)
            {
                _types = GameContract.SignTypes.Field;
            }
            var types = _types != null ? _types.GetValue(sign) as WideSignTypes : null;
            WideSignType[] all = types != null ? types.signTypes : null;
            int lit = 0, first = -1;
            for (int i = 0; all != null && i < all.Length; i++)
            {
                WideSignType type = all[i];
                if (type == null || type.signMesh == null)
                {
                    continue;
                }
                int value = (int)type.type;
                if (first < 0 || value < first)
                {
                    first = value;
                }
                if (type.lightMesh != null && lit < Lit.Length)
                {
                    Lit[lit++] = value;
                }
            }
            Array.Sort(Lit, 0, lit);
            return FrontSignRules.SignType(preferred, Lit, lit, first);
        }

        private static Renderer Face(BuildingLogoSignController sign)
        {
            if (sign == null)
            {
                return null;
            }
            if (_face == null)
            {
                _face = GameContract.LogoFace.Field;
            }
            Renderer face = _face != null ? _face.GetValue(sign) as Renderer : null;
            return face != null ? face : sign.GetComponentInChildren<Renderer>(true);
        }

        private static MaterialPropertyBlock Block()
        {
            return _block ?? (_block = new MaterialPropertyBlock());
        }

        // Made once by the game's logo maker into the game's temporary folder, and kept for the next session.
        private static Logo LogoFor(string name, FrontStyle style)
        {
            uint mark = FrontSignRules.Mark(name, style);
            string key = name + "|" + mark.ToString("x8", System.Globalization.CultureInfo.InvariantCulture);
            if (Logos.TryGetValue(key, out Logo logo))
            {
                return logo;
            }
            logo = new Logo
            {
                Name = name,
                Style = style,
                Path = Path.Combine(Application.temporaryCachePath, Folder, LogoHelper.GetBusinessNamePathSafe(name) + "-" + mark.ToString("x8", System.Globalization.CultureInfo.InvariantCulture))
            };
            Logos[key] = logo;
            Load(logo);
            return logo;
        }

        private static void Make(Logo logo)
        {
            if (logo.Asked || BusinessLogoGenerator.Instance == null)
            {
                return;   // being made, or the logo maker is not there yet: asked again in a second
            }
            logo.Asked = true;
            FrontStyle s = logo.Style;
            var settings = new LogoSettings
            {
                logoShape = s.Shape,
                font = (FontFace)s.Font,
                backgroundColor = new Color(s.BackR, s.BackG, s.BackB),
                fontColor = new Color(s.TextR, s.TextG, s.TextB),
                logoColor = new Color(s.MarkR, s.MarkG, s.MarkB)
            };
            // Not a player's business: the game caches nothing of it and writes only into the folder given.
            BusinessLogoGenerator.Create(logo.Name, settings, logo.Path, false, Guard.Listener(() => Made(logo)));
        }

        private static void Made(Logo logo)
        {
            try
            {
                Load(logo);
                if (logo.Wide == null)
                {
                    logo.Failed = true;
                    SpecialBuildings.Logger?.Warn("Signs: the game's logo maker gave no picture for " + logo.Name + "; its building has the game's signs off instead.");
                }
                else
                {
                    SpecialBuildings.Logger?.Info("Signs: the logo of " + logo.Name + " was made by the game's logo maker (" + logo.Wide.width + " x " + logo.Wide.height + ").");
                }
            }
            catch (Exception e)
            {
                logo.Failed = true;
                SpecialBuildings.Logger?.Error(e);
            }
        }

        private static void Load(Logo logo)
        {
            string wide = Path.Combine(logo.Path, WideFile);
            string square = Path.Combine(logo.Path, SquareFile);
            if (!File.Exists(wide) || !File.Exists(square))
            {
                return;
            }
            Texture2D w = LogoHelper.GetLogoShapeTexture(wide);
            Texture2D q = LogoHelper.GetLogoShapeTexture(square);
            Pack(w);
            Pack(q);
            logo.Wide = w;
            logo.Square = q;
        }

        // As LogoHelper.TryToCompress packs the logos the game loads.
        private static void Pack(Texture2D texture)
        {
            if (texture != null && texture.width % 16 == 0 && texture.height % 16 == 0)
            {
                texture.Compress(false);
            }
        }
    }
}
