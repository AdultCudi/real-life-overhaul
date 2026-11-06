using System;
using System.Collections.Generic;
using BigAmbitions.InteriorDesigner;
using BigAmbitions.InteriorDesigner.InteriorElements;
using BigAmbitions.InteriorDesigner.Tools;
using BigAmbitions.Tags;
using Buildings;
using InteriorDesign;
using UI.InteriorDesigner;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The walls and floors on the kit: the materials the game lists for the tool in hand (cheapest first, the ones that can be bought), a tile choosing one through the
    /// game's own choosing (InteriorElementActionPanelUI.SelectButtonByPresetId, which sets what the tool paints with), a shade choosing the colour the same way, and
    /// for a floor one button that lays the chosen material over every piece of floor in a single action of the game's own (InteriorElementRevertibleAction through
    /// ExecuteRevertibleAction), so undo, redo, the cost and the question of money are the game's. The price of each piece is the game's own formula against what the room
    /// began with, kept here as the designer opened. A picture of a material is the game's own preview run through the game's own material, made when a tile first
    /// shows. Cost: nothing while the tool is not a floor or wall; a few reads at the rate the face looks.
    /// </summary>
    internal static partial class DesignerGame
    {
        private struct Began
        {
            public int Index, Colour;
            public string Preset;
        }

        private static readonly Dictionary<string, Began[]> Originals = new Dictionary<string, Began[]>(1024, StringComparer.Ordinal);
        private static readonly List<InteriorMaterialPreset>[] Library = { new List<InteriorMaterialPreset>(64), new List<InteriorMaterialPreset>(64) };
        private static readonly List<InteriorMaterialPreset> Shelf = new List<InteriorMaterialPreset>(64);
        private static readonly Dictionary<string, Sprite> Baked = new Dictionary<string, Sprite>(128, StringComparer.Ordinal);
        private static readonly Dictionary<string, int> Averages = new Dictionary<string, int>(128, StringComparer.Ordinal);
        private static string _materialSearch = string.Empty;
        private static int _materialTool = -1;
        private static bool _libraryBuilt;
        private const int MostShades = 8;
        private static string _scoredKey;
        private static string _scoredWords;

        private static void BeginMaterials()
        {
            ForgetMaterials();
            foreach (KeyValuePair<string, InteriorElement> pair in InteriorElementsHelper.InteriorElementsCache)
            {
                MaterialIndexData[] data = pair.Value != null ? pair.Value.materialData : null;
                if (data == null)
                {
                    continue;
                }
                var snap = new Began[data.Length];
                for (int i = 0; i < data.Length; i++)
                {
                    snap[i] = new Began { Index = data[i].materialIndex, Colour = data[i].colorIndex, Preset = data[i].preset != null ? data[i].preset.uuid : null };
                }
                Originals[pair.Key] = snap;
            }
        }

        private static void EndMaterials()
        {
            _materialSearch = string.Empty;
        }

        private static void ForgetMaterials()
        {
            Originals.Clear();
            Library[0].Clear();
            Library[1].Clear();
            _libraryBuilt = false;
            _materialSearch = string.Empty;
            _scoredKey = null;
            foreach (Sprite sprite in Baked.Values)
            {
                if (sprite != null)
                {
                    UnityEngine.Object.Destroy(sprite.texture);
                    UnityEngine.Object.Destroy(sprite);
                }
            }
            Baked.Clear();
            Averages.Clear();
        }

        // The game's own list for a tool, as its panel makes it: what can be bought and is of the tool's kind, cheapest first.
        private static void BuildLibrary()
        {
            _libraryBuilt = true;
            for (int kind = 0; kind < 2; kind++)
            {
                InteriorMaterialPreset.MaterialType type = kind == 0 ? InteriorMaterialPreset.MaterialType.Floor : InteriorMaterialPreset.MaterialType.Wall;
                foreach (InteriorMaterialPreset p in InteriorElementsHelper.PresetsDictionary.Values)
                {
                    if (p != null && p.canBePurchased && p.type.HasFlag(type))
                    {
                        Library[kind].Add(p);
                    }
                }
                Library[kind].Sort(ByPrice);
            }
        }

        // Cheapest first; pieces of one price keep the order the game lists them in (the sort is made stable by the dictionary's own order).
        private static int ByPrice(InteriorMaterialPreset a, InteriorMaterialPreset b)
        {
            int byPrice = a.price.CompareTo(b.price);
            return byPrice != 0 ? byPrice : string.CompareOrdinal(a.uuid, b.uuid);
        }

        /// <summary>The materials panel is up: the tool in hand is the floor or the wall.</summary>
        private static bool MaterialsUp(out int kind)
        {
            int tool = ToolInHand();
            kind = tool == (int)UiDesignerTool.Floor ? 0 : tool == (int)UiDesignerTool.Wall ? 1 : -1;
            return kind >= 0 && DesignerPanes.Elements != null;
        }

        private static bool FillMaterials(UiDesignerMaterials m)
        {
            if (!MaterialsUp(out int kind))
            {
                return false;
            }
            if (!_libraryBuilt)
            {
                BuildLibrary();
            }
            if (_materialTool != kind)
            {
                _materialTool = kind;
                m.Scroll = 0f;
            }
            m.Tabs[0] = UiText.T("interior_designer_floor");
            m.Tabs[1] = UiText.T("interior_designer_wall");
            m.TabCounts[0] = UiText.Whole(Library[0].Count);
            m.TabCounts[1] = UiText.Whole(Library[1].Count);
            m.Tab = kind;
            m.SearchPlaceholder = UiText.T("common_search");
            m.Search = _materialSearch;
            m.NoneTitle = UiText.T("reallife_ui_designer_none_title");
            m.NoneBody = UiText.T("reallife_ui_designer_none_materials");
            m.PickBody = UiText.T(kind == 0 ? "reallife_ui_designer_pick_floor" : "reallife_ui_designer_pick_wall");
            m.ShadeLabel = UiText.T("reallife_ui_designer_shade");
            Shelf.Clear();
            List<InteriorMaterialPreset> all = Library[kind];
            for (int i = 0; i < all.Count; i++)
            {
                if (DesignerRules.Matches(Say(all[i].localizeKey), _materialSearch))
                {
                    Shelf.Add(all[i]);
                }
            }
            string selected = InteriorElementTool.selectedPresetId;
            int colour = InteriorElementTool.selectedPresetColorIndex;
            int n = Shelf.Count;
            if (m.Cards.Length < n)
            {
                m.Cards = new UiDesignerMaterial[Math.Max(n, m.Cards.Length * 2)];
            }
            int chosen = -1;
            for (int i = 0; i < n; i++)
            {
                InteriorMaterialPreset p = Shelf[i];
                bool is_ = selected != null && string.Equals(p.uuid, selected, StringComparison.Ordinal);
                chosen = is_ ? i : chosen;
                m.Cards[i] = new UiDesignerMaterial
                {
                    Index = i, Name = Say(p.localizeKey), Price = UiText.Money(p.price), Picture = MaterialPictureKey(p.uuid, is_ ? colour : 0), HasShades = p.variants != null && p.variants.Count > 1
                };
            }
            m.CardCount = n;
            m.Chosen = chosen;
            FillMaterialDetail(m, chosen >= 0 ? Shelf[chosen] : null, colour, kind);
            return true;
        }

        private static void FillMaterialDetail(UiDesignerMaterials m, InteriorMaterialPreset p, int colour, int kind)
        {
            m.DetailCount = 0;
            m.ShadeCount = 0;
            m.ShadeChosen = -1;
            m.LayLabel = null;
            m.LayFigure = null;
            m.LayWhy = null;
            m.LayOff = false;
            if (p == null)
            {
                return;
            }
            int shades = p.variants != null ? p.variants.Count : 0;
            m.DetailName = Say(p.localizeKey);
            string name = UiText.T(kind == 0 ? "interior_designer_floor" : "interior_designer_wall");
            m.DetailKind = shades > 1 ? name + ", " + UiText.Fill("reallife_ui_designer_shades", "n", UiText.Whole(shades)) : name;
            m.DetailLabels[0] = UiText.T("reallife_ui_designer_price_piece");
            m.DetailValues[0] = UiText.Money(p.price);
            m.DetailInks[0] = UiRole.Text;
            m.DetailCount = 1;
            if (kind == 0 && ShowsScore(Premises()))
            {
                string words = ScoreIfLaid(p, colour);
                if (words != null)
                {
                    m.DetailLabels[1] = UiText.T("reallife_ui_designer_score_if");
                    m.DetailValues[1] = words;
                    m.DetailInks[1] = words.StartsWith("-", StringComparison.Ordinal) ? UiRole.DangerText : words == "0%" ? UiRole.Text2 : UiRole.OkText;
                    m.DetailCount = 2;
                }
            }
            for (int k = 0; k < shades && k < MostShades; k++)   // eight shades at most, so the panel never holds more targets than a screen should
            {
                m.Shades[k] = UiDraw.SwatchKey(AverageOf(p, k));
                m.ShadeCount = k + 1;
            }
            m.ShadeChosen = Math.Max(0, Math.Min(colour, m.ShadeCount - 1));
            if (kind == 0)
            {
                int pieces = LayPlan(p, colour, null, out float cost);
                m.LayLabel = UiText.T("reallife_ui_designer_lay");
                if (pieces == 0)
                {
                    m.LayOff = true;
                    m.LayWhy = UiText.T("reallife_ui_designer_lay_already");
                }
                else
                {
                    m.LayFigure = UiText.Money(cost);
                    if (cost > _balanceAfter)
                    {
                        m.LayOff = true;
                        m.LayWhy = UiText.T("reallife_ui_designer_lay_short");
                    }
                }
            }
        }

        // ------------------------------------------------------------------ laying a material over the whole floor

        /// <summary>
        /// What laying a material over every piece of floor comes to: how many pieces change, at the game's own price for each (against what the room began
        /// with). Fills the action's arrays when asked. Only pieces of floor that are in the room now are counted.
        /// </summary>
        private static int LayPlan(InteriorMaterialPreset preset, int colour, List<object> into, out float cost)
        {
            cost = 0f;
            int pieces = 0;
            foreach (KeyValuePair<string, InteriorElement> pair in InteriorElementsHelper.InteriorElementsCache)
            {
                InteriorElement e = pair.Value;
                if (e == null || !e.IsFloor || e.materialData == null || !e.gameObject.activeInHierarchy)
                {
                    continue;
                }
                Originals.TryGetValue(pair.Key, out Began[] began);
                for (int i = 0; i < e.materialData.Length; i++)
                {
                    MaterialIndexData md = e.materialData[i];
                    if (md.preset == null || (string.Equals(md.preset.uuid, preset.uuid, StringComparison.Ordinal) && md.colorIndex == colour))
                    {
                        continue;
                    }
                    Began start = FindBegan(began, md.materialIndex);
                    cost += DesignerRules.PieceCost(preset.uuid, colour, start.Preset ?? md.preset.uuid, start.Preset != null ? start.Colour : md.colorIndex, md.preset.uuid, md.colorIndex,
                        md.preset.price, preset.price);
                    pieces++;
                    if (into != null)
                    {
                        into.Add(new LayPiece { Id = pair.Key, Index = md.materialIndex, Preset = md.preset.uuid, Colour = md.colorIndex });
                    }
                }
            }
            return pieces;
        }

        private struct LayPiece
        {
            public string Id, Preset;
            public int Index, Colour;
        }

        private static Began FindBegan(Began[] began, int index)
        {
            for (int i = 0; began != null && i < began.Length; i++)
            {
                if (began[i].Index == index)
                {
                    return began[i];
                }
            }
            return default;
        }

        /// <summary>The whole floor laid with the chosen material as one action of the game's: its cost, its undo and its question of money are the game's.</summary>
        public static bool LayWholeFloor()
        {
            InteriorDesignerUI ui = Ui;
            string id = InteriorElementTool.selectedPresetId;
            if (ui == null || !Open || id == null || ToolInHand() != (int)UiDesignerTool.Floor || !InteriorElementsHelper.PresetsDictionary.TryGetValue(id, out InteriorMaterialPreset preset))
            {
                return false;
            }
            int colour = InteriorElementTool.selectedPresetColorIndex;
            var pieces = new List<object>(256);
            int n = LayPlan(preset, colour, pieces, out float cost);
            if (n == 0)
            {
                return false;
            }
            var ids = new string[n];
            var indices = new int[n];
            var olds = new string[n];
            var oldColours = new int[n];
            for (int i = 0; i < n; i++)
            {
                var piece = (LayPiece)pieces[i];
                ids[i] = piece.Id;
                indices[i] = piece.Index;
                olds[i] = piece.Preset;
                oldColours[i] = piece.Colour;
            }
            ui.ExecuteRevertibleAction(new InteriorElementRevertibleAction(cost, ids, indices, olds, oldColours, id, colour));
            return true;
        }

        // The interior score as it would stand with the chosen material on every piece of floor, as a change against now ("+2%"), by the game's own calculator.
        private static string ScoreIfLaid(InteriorMaterialPreset preset, int colour)
        {
            string key = preset.uuid + ":" + UiText.Whole(colour) + ":" + UiText.Whole(Mathf.RoundToInt(_score * 10f));
            if (string.Equals(key, _scoredKey, StringComparison.Ordinal))
            {
                return _scoredWords;
            }
            _scoredKey = key;
            _scoredWords = null;
            try
            {
                var now = new List<SerializedInteriorDesign>(InteriorElementsHelper.InteriorElementsCache.Count);
                var laid = new List<SerializedInteriorDesign>(InteriorElementsHelper.InteriorElementsCache.Count);
                foreach (KeyValuePair<string, InteriorElement> pair in InteriorElementsHelper.InteriorElementsCache)
                {
                    InteriorElement e = pair.Value;
                    if (e == null)
                    {
                        continue;
                    }
                    SerializedInteriorDesign was = e.Serialize(), will = e.Serialize();
                    if (e.IsFloor && e.gameObject.activeInHierarchy)
                    {
                        for (int i = 0; i < will.materials.Length; i++)
                        {
                            will.materials[i].MaterialID = preset.uuid;
                            will.materials[i].ColorIndex = colour;
                        }
                    }
                    now.Add(was);
                    laid.Add(will);
                }
                if (now.Count == 0)
                {
                    return null;
                }
                int change = InteriorScoreCalculator.GetInteriorScorePercentage(laid) - InteriorScoreCalculator.GetInteriorScorePercentage(now);
                _scoredWords = change == 0 ? "0%" : (change > 0 ? "+" : "-") + Math.Abs(change).ToString(System.Globalization.CultureInfo.InvariantCulture) + "%";
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "the interior score a floor would give in the interior designer");
            }
            return _scoredWords;
        }

        // ------------------------------------------------------------------ the materials' presses

        /// <summary>Floors or Walls: the tool is changed as the game's own button changes it, so its panel lists the other kind.</summary>
        public static bool MaterialTab(int tab)
        {
            int want = tab == 0 ? (int)UiDesignerTool.Floor : (int)UiDesignerTool.Wall;
            return ToolInHand() == want || Tool(want);
        }

        /// <summary>A material chosen as the game's own tile chooses it (the shade stays if it is the one already chosen).</summary>
        public static bool ChooseMaterial(int index)
        {
            if (index < 0 || index >= Shelf.Count || DesignerPanes.Elements == null)
            {
                return false;
            }
            InteriorMaterialPreset p = Shelf[index];
            int colour = string.Equals(p.uuid, InteriorElementTool.selectedPresetId, StringComparison.Ordinal) ? InteriorElementTool.selectedPresetColorIndex : 0;
            DesignerPanes.Elements.SelectButtonByPresetId(p.uuid, colour);
            return true;
        }

        /// <summary>A shade of the chosen material.</summary>
        public static bool ChooseShade(int shade)
        {
            string id = InteriorElementTool.selectedPresetId;
            if (id == null || DesignerPanes.Elements == null || shade < 0)
            {
                return false;
            }
            DesignerPanes.Elements.SelectButtonByPresetId(id, shade);
            return true;
        }

        /// <summary>Words typed in the materials' search.</summary>
        public static void MaterialSearch(string text) => _materialSearch = text ?? string.Empty;

        // ------------------------------------------------------------------ pictures: a material's own preview through the game's material

        private const string ItemPicture = "designer:item:", MaterialPicture = "designer:mat:";
        private static bool _picturesOn;

        // The keys of the designer's pictures answered here first, once a session; any other key, and the swatches of colour (the world's), go to whoever answered before.
        private static void PicturesOn()
        {
            if (_picturesOn)
            {
                return;
            }
            _picturesOn = true;
            WorldPictures.Of(Color.white);   // puts the world's answer for swatches in the chain first, so ours stands over it
            Func<string, Sprite> before = UiPictures.Find;
            UiPictures.Find = key => Picture(key) ?? before?.Invoke(key);
        }

        internal static string MaterialPictureKey(string uuid, int variant) => MaterialPicture + uuid + ":" + UiText.Whole(variant);

        /// <summary>A picture the designer's keys name, or null for a key that is not the designer's.</summary>
        internal static Sprite Picture(string key)
        {
            if (key == null)
            {
                return null;
            }
            if (key.StartsWith(ItemPicture, StringComparison.Ordinal))
            {
                return ItemHelper.GetIconWithFallback(key.Substring(ItemPicture.Length));
            }
            if (!key.StartsWith(MaterialPicture, StringComparison.Ordinal))
            {
                return null;
            }
            if (Baked.TryGetValue(key, out Sprite known) && known != null)
            {
                return known;
            }
            int cut = key.LastIndexOf(':');
            string uuid = key.Substring(MaterialPicture.Length, cut - MaterialPicture.Length);
            if (cut < MaterialPicture.Length || !int.TryParse(key.Substring(cut + 1), out int variant)
                || !InteriorElementsHelper.PresetsDictionary.TryGetValue(uuid, out InteriorMaterialPreset preset))
            {
                return null;
            }
            Sprite sprite = Bake(preset, variant, 96, null);
            Baked[key] = sprite != null ? sprite : preset.preview;
            return Baked[key];
        }

        // The mean colour of a shade, made from the game's own preview of it at a small size, kept for the session.
        private static int AverageOf(InteriorMaterialPreset preset, int variant)
        {
            string key = preset.uuid + ":" + UiText.Whole(variant);
            if (Averages.TryGetValue(key, out int rgb))
            {
                return rgb;
            }
            rgb = 0x808080;
            int made = 0;
            Bake(preset, variant, 16, mean => made = mean);
            Averages[key] = made != 0 ? made : rgb;
            return Averages[key];
        }

        private static Shader _previewShader;

        // The game's preview of a material's shade: its sprite through a material set as the game sets the one on its own tile. The mean colour of what was
        // drawn goes to `mean` when asked (a small bake for a swatch gives no sprite).
        private static Sprite Bake(InteriorMaterialPreset preset, int variant, int size, Action<int> mean)
        {
            Sprite source = preset.preview;
            if (source == null || preset.variants == null || variant < 0 || variant >= preset.variants.Count)
            {
                return null;
            }
            if (_previewShader == null)
            {
                InteriorElementOptionUi any = DesignerPanes.Elements != null ? DesignerPanes.Elements.GetComponentInChildren<InteriorElementOptionUi>(true) : null;
                _previewShader = any != null && any.previewImage != null && any.previewImage.material != null ? any.previewImage.material.shader : null;
            }
            if (_previewShader == null)
            {
                return null;
            }
            var material = new Material(_previewShader) { hideFlags = HideFlags.HideAndDontSave };
            RenderTexture target = RenderTexture.GetTemporary(size, size, 0, RenderTextureFormat.ARGB32);
            RenderTexture before = RenderTexture.active;
            Texture2D picture = null;
            try
            {
                InteriorElementOptionUi.SetMaterialPresetToUIMaterial(material, preset, variant, applyPreviewData: true);
                Graphics.Blit(source.texture, target, material);
                RenderTexture.active = target;
                picture = new Texture2D(size, size, TextureFormat.RGBA32, false) { name = "RealLife designer material", hideFlags = HideFlags.HideAndDontSave, wrapMode = TextureWrapMode.Clamp };
                picture.ReadPixels(new Rect(0f, 0f, size, size), 0, 0);
                picture.Apply(false, mean == null);
            }
            catch (Exception e) when (Guard.Missing(e))
            {
                Guard.DoesWithout(e, "a material's picture in the interior designer");
                UnityEngine.Object.Destroy(picture);
                picture = null;
            }
            finally
            {
                RenderTexture.active = before;
                RenderTexture.ReleaseTemporary(target);
                UnityEngine.Object.Destroy(material);
            }
            if (picture == null)
            {
                return null;
            }
            if (mean != null)
            {
                Color32[] px = picture.GetPixels32();
                long r = 0, g = 0, b = 0, a = 0;
                for (int i = 0; i < px.Length; i++)
                {
                    a += px[i].a;
                    r += px[i].r * px[i].a;
                    g += px[i].g * px[i].a;
                    b += px[i].b * px[i].a;
                }
                mean(a > 0 ? (int)(r / a) << 16 | (int)(g / a) << 8 | (int)(b / a) : 0);
                UnityEngine.Object.Destroy(picture);
                return null;
            }
            var sprite = Sprite.Create(picture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), 100f);
            sprite.hideFlags = HideFlags.HideAndDontSave;
            return sprite;
        }
    }
}
