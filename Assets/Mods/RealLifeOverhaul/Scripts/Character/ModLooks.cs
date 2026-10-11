using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text;
using BAModAPI;
using BigAmbitions.Characters.Appearance;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Puts the mod's own hair, beards and clothes (<see cref="ModAssets"/>) on the detailed characters
    /// that show the player: the one in character creation, the one in the shop screens and the
    /// player's double in the city. Nobody else is touched.
    ///
    /// For each kind of piece the player has picked a new look for, the game's own piece is told
    /// not to draw (it stays selected and "enabled", so the game's logic is undisturbed) and a
    /// renderer of ours is added beside it, skinned to the same bones. Its materials are copies
    /// of materials the game already draws these characters with, so every shader variant is
    /// certain to be in the build; only textures, colours and a few numbers differ, never keywords.
    ///  - Hair and beards take the colour the game has put on its own hair, on every change.
    ///  - Clothes come with a second renderer for the skin they leave bare: the skin part of one
    ///    of the game's own pieces, cut out of its mesh at run time and drawn with the game's
    ///    own skin material.
    ///  - Body sliders and face shapes are copied to our meshes' blendshapes.
    ///
    /// All of it happens when a look changes; nothing runs per frame. Everything changed on the
    /// game's objects is recorded and put back. If anything is not as expected this logs once,
    /// puts the game's look back and stays off until the next scene.
    /// </summary>
    internal static class ModLooks
    {
        private const int Kinds = 5;
        private const int BodyShapes = 3;           // Fat, Slim, Musc: the game sets these by position, not by name
        private const int SmallTexture = 64;        // the game's flat placeholder textures are 64 px
        private const float BulkAlphaClip = 0.35f;  // strands thinner than this do not write depth
        private const float UnderlayShade = 0.72f;  // the scalp underlay's colour, as a share of the hair colour

        private const string OurPrefix = "RealLifeOverhaul ";
        private const string BootSuffix = "_boot";   // the mesh of a tall shoe of ours (feet_m_boot): trousers of ours tuck into it
        private const string BootShape = "Boots";     // the shape the game's trousers, and ours, have for that
        private const string ClothesShader = "Shader Graphs/SH_CharacterClothes";
        private const string TransparentKeyword = "_SURFACE_TYPE_TRANSPARENT";
        private const string AlphaTestKeyword = "_ALPHATEST_ON";
        private const string MaskMapKeyword = "_MASKMAP";
        private const string NormalMapKeyword = "_NORMALMAP";

        private static readonly Color DarkBrown = new Color(0.16f, 0.11f, 0.08f, 1f);
        // In the space of the hips bone; generous, so the piece is never culled while the character is on screen.
        private static readonly Bounds WideBounds = new Bounds(Vector3.zero, new Vector3(2.6f, 2.6f, 2.6f));

        private static readonly AppearanceElementType[] ElementTypes =
        {
            AppearanceElementType.Hair, AppearanceElementType.Beard, AppearanceElementType.Torso, AppearanceElementType.Legs, AppearanceElementType.Feet
        };

        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int TransmissionColorId = Shader.PropertyToID("_TransmissionColor");
        private static readonly int BaseColorMapId = Shader.PropertyToID("_BaseColorMap");
        private static readonly int NormalMapId = Shader.PropertyToID("_NormalMap");
        private static readonly int NormalScaleId = Shader.PropertyToID("_NormalScale");
        private static readonly int MaskMapId = Shader.PropertyToID("_MaskMap");
        private static readonly int MaskColorRedId = Shader.PropertyToID("_MaskColorRed");
        private static readonly int DepthPrepassId = Shader.PropertyToID("_TransparentDepthPrepassEnable");
        private static readonly int DepthPrepassClipId = Shader.PropertyToID("_AlphaClipThresholdDepthPrepass");
        private static readonly int SortPriorityId = Shader.PropertyToID("_TransparentSortPriority");
        private static readonly int DoubleSidedEnableId = Shader.PropertyToID("_DoubleSidedEnable");
        private static readonly int[] CullIds =
        {
            Shader.PropertyToID("_CullMode"), Shader.PropertyToID("_CullModeForward"), Shader.PropertyToID("_TransparentCullMode")
        };
        private static readonly int ColourMaskMapId = Shader.PropertyToID("_ColorMaskMap");
        private static readonly int[] MaskColourIds =
        {
            Shader.PropertyToID("_MaskColorRed"), Shader.PropertyToID("_MaskColorGreen"), Shader.PropertyToID("_MaskColorBlue"), Shader.PropertyToID("_MaskColorAlpha")
        };
        private static readonly int[] SheenColourIds =
        {
            Shader.PropertyToID("_SpecularColorRed"), Shader.PropertyToID("_SpecularColorGreen"), Shader.PropertyToID("_SpecularColorBlue"), Shader.PropertyToID("_SpecularColorAlpha")
        };
        // SH_CharacterClothes as the game sets it on its own clothes: smoothness straight from the mask map, normal map at
        // full strength, the three fresnel terms at 1. Its sheen colour per mask channel is about half the cloth's colour.
        private static readonly int[] ClothIds =
        {
            Shader.PropertyToID("_SmoothnessMin"), Shader.PropertyToID("_SmoothnessMax"), Shader.PropertyToID("_NormalStrength"),
            Shader.PropertyToID("_NormalFresnelSmooth"), Shader.PropertyToID("_SpecularFresnelSmooth"), Shader.PropertyToID("_SpecularFresnelExterior")
        };
        private static readonly float[] ClothValues = { 0f, 1f, 1f, 1f, 1f, 1f };
        private const float ClothSheen = 0.5f;
        private static readonly int[] ColourMaskSwitches =
        {
            Shader.PropertyToID("_ColorMaskMapHasRedChannel"), Shader.PropertyToID("_ColorMaskMapHasGreenChannel"),
            Shader.PropertyToID("_ColorMaskMapHasBlueChannel"), Shader.PropertyToID("_ColorMaskMapHasAlphaChannel")
        };

        /// <summary>One of our pieces on one character.</summary>
        private sealed class Piece
        {
            public LookItem Item;
            public SkinnedMeshRenderer Head;        // whose bones we are skinned to
            public SkinnedMeshRenderer Hidden;      // the game's piece we stopped from drawing
            public bool HiddenWasOff;
            public GameObject Object;
            public SkinnedMeshRenderer Renderer;
            public Material[] Materials;            // ours, destroyed with the piece
            public GameObject SkinObject;
            public SkinnedMeshRenderer Skin;
            public Material OwnedSkin;              // only when the game's own skin material could not be shared
        }

        private sealed class Dressed
        {
            public AppearanceSetter Character;
            public readonly Piece[] Pieces = new Piece[Kinds];
        }

        public static IModLogger Logger;

        private static readonly List<Dressed> All = new List<Dressed>(3);
        private static readonly List<Material> Scratch = new List<Material>(8);
        private static readonly Dictionary<long, Mesh> SkinMeshes = new Dictionary<long, Mesh>(8);
        private static readonly HashSet<string> Warned = new HashSet<string>();
        private static readonly Material[] Templates = new Material[4];   // by LayerRole
        private static Texture _flatNormal;
        private static Texture _flatMask;
        private static bool _templatesSearched;
        private static FieldInfo _selectedField;
        private static bool _failed;
        private static string _failure;

        // The player's double draws no shadows of its own and is lit like the player (see PlayerDouble.Dress).
        private static AppearanceSetter _double;
        private static uint _doubleLightLayers;

        // ---------------------------------------------------------------- entry points

        /// <summary>
        /// Brings this character in line with the picks. Safe to call again and again; called from inside
        /// the game's own appearance update, so nothing may escape from it.
        /// </summary>
        public static void Apply(AppearanceSetter character, bool showsPlayer)
        {
            if (_failed || character == null)
            {
                return;
            }
            try
            {
                Dressed dressed = Find(character);
                SkinnedMeshRenderer head = character.HeadMesh;
                bool on = Settings.CharacterNewLooks && showsPlayer && ModAssets.Ready && head != null && character.data != null
                          && character.skipMeshMerging && character.GetSelectedMeshRenderers().Count > 0 && CharacterTools.IsHighDetail(character);
                if (!on)
                {
                    if (dressed != null)
                    {
                        Undress(dressed);
                        All.Remove(dressed);
                        ModAssets.Sweep();
                    }
                    return;
                }
                ModAssets.Arrived = Arrived;
                if (dressed == null)
                {
                    dressed = new Dressed { Character = character };
                    All.Add(dressed);
                }
                // A piece of the game's own that the player has just picked in one of the game's screens takes the mod's
                // look of that kind off: the game announces every such click, and this runs inside that announcement.
                CharacterCatalogue.Observe(character);
                Dictionary<AppearanceElementType, SkinnedMeshRenderer> selected = SelectedPieces(character);
                Transform[] bones = null;
                for (int kind = 0; kind < Kinds; kind++)
                {
                    LookItem item = Picked((LookKind)kind, character, out string waiting);
                    if (waiting != null && dressed.Pieces[kind] != null)
                    {
                        Logger?.Info("Looks: " + (LookKind)kind + " on '" + character.name + "' shows the game's own piece for now (one being tried in a game screen, or bare skin); the mod's '"
                                     + waiting + "' is back when the piece it was picked over is.");
                    }
                    selected.TryGetValue(ElementTypes[kind], out SkinnedMeshRenderer game);
                    if (item == null && kind == (int)LookKind.Hair && game != null && game.sharedMesh != null)
                    {
                        // Nothing picked: the game's own hair stays, with a hair-coloured scalp put under it.
                        item = ModAssets.FindUnderlay(game.sharedMesh.name, character.data.gender == BigAmbitions.Characters.Gender.Female);
                    }
                    if (item == null)
                    {
                        Remove(dressed, kind);
                        continue;
                    }
                    if (!item.Loaded && !ModAssets.Request(item))
                    {
                        // On its way in from the bundle: what is worn now (the game's own piece, or the look picked before)
                        // stays until it is there, and Arrived brings us back here.
                        continue;
                    }
                    if (bones == null)
                    {
                        bones = head.bones;
                        if (bones.Length != ModAssets.BoneCount)
                        {
                            throw new InvalidOperationException("the character's head has " + bones.Length + " bones, the looks were made for " + ModAssets.BoneCount);
                        }
                    }
                    Wear(dressed, kind, item, game, head, bones, selected);
                }
                ModAssets.Sweep();
            }
            catch (Exception e)
            {
                Fail(e.Message, e);
            }
        }

        // The mod's look of this kind for this character: what is picked, if this character is wearing the piece of
        // the game's own that it was picked over. Otherwise nothing (the game's piece shows) and the pick that waits.
        private static LookItem Picked(LookKind kind, AppearanceSetter character, out string waiting)
        {
            waiting = null;
            string id = CharacterCatalogue.Selected(kind, character.data.gender);
            if (id == null)
            {
                return null;
            }
            if (!CharacterCatalogue.Shows(kind, character.data))
            {
                waiting = id;
                return null;
            }
            LookItem item = ModAssets.Find(id);
            return item != null && item.Kind == kind ? item : null;
        }

        // A look that was asked for has come in from the bundle (or failed): dress everyone again. Not per frame:
        // once per asset read, a frame or a few after a pick changed.
        private static void Arrived()
        {
            if (!_failed)
            {
                for (int i = All.Count - 1; i >= 0; i--)
                {
                    if (i < All.Count && All[i].Character != null)
                    {
                        Apply(All[i].Character, true);
                    }
                }
            }
            ModAssets.Sweep();
        }

        /// <summary>The player's double: its pieces cast no shadows and take the player's light layers, ours included.</summary>
        public static void SetDouble(AppearanceSetter character, uint lightLayers)
        {
            _double = character;
            _doubleLightLayers = lightLayers;
            if (_failed)
            {
                return;
            }
            try
            {
                Dressed dressed = Find(character);
                if (dressed == null)
                {
                    return;
                }
                for (int kind = 0; kind < Kinds; kind++)
                {
                    Piece piece = dressed.Pieces[kind];
                    if (piece == null)
                    {
                        continue;
                    }
                    // The double's own dressing has just switched every selected piece back on.
                    if (piece.Hidden != null)
                    {
                        piece.Hidden.forceRenderingOff = true;
                    }
                    Light(piece, (LookKind)kind, character);
                }
            }
            catch (Exception e)
            {
                Fail(e.Message, e);
            }
        }

        /// <summary>This character is going away or is no longer looked after: the game's look back, ours removed.</summary>
        public static void Forget(AppearanceSetter character)
        {
            try
            {
                if (character == _double)
                {
                    _double = null;
                }
                Dressed dressed = Find(character);
                if (dressed != null)
                {
                    Undress(dressed);
                    All.Remove(dressed);
                }
                Prune();
                ModAssets.Sweep();
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        /// <summary>The game's look back on every character.</summary>
        public static void RestoreAll()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                try
                {
                    Undress(All[i]);
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
            All.Clear();
            ModAssets.Sweep();
        }

        /// <summary>End of the scene: undo everything and forget what was found in it.</summary>
        public static void Shutdown()
        {
            RestoreAll();
            foreach (Mesh mesh in SkinMeshes.Values)
            {
                if (mesh != null)
                {
                    UnityEngine.Object.Destroy(mesh);
                }
            }
            SkinMeshes.Clear();
            Array.Clear(Templates, 0, Templates.Length);
            _flatNormal = null;
            _flatMask = null;
            _templatesSearched = false;
            _double = null;
            _failed = false;
            _failure = null;
            Warned.Clear();
        }

        // Characters destroyed under us (the double with its holder): their objects went with them, the materials did not.
        private static void Prune()
        {
            for (int i = All.Count - 1; i >= 0; i--)
            {
                if (All[i].Character == null)
                {
                    Undress(All[i]);
                    All.RemoveAt(i);
                }
            }
        }

        private static Dressed Find(AppearanceSetter character)
        {
            for (int i = 0; i < All.Count; i++)
            {
                if (All[i].Character == character)
                {
                    return All[i];
                }
            }
            return null;
        }

        // The game keeps which renderer is worn per element type in a private dictionary.
        private static Dictionary<AppearanceElementType, SkinnedMeshRenderer> SelectedPieces(AppearanceSetter character)
        {
            if (_selectedField == null)
            {
                _selectedField = typeof(AppearanceSetter).GetField("_selectedMeshRenderers", BindingFlags.NonPublic | BindingFlags.Instance);
            }
            var selected = _selectedField?.GetValue(character) as Dictionary<AppearanceElementType, SkinnedMeshRenderer>;
            if (selected == null)
            {
                throw new InvalidOperationException("the game's record of worn pieces was not found");
            }
            return selected;
        }

        // ---------------------------------------------------------------- one piece

        private static void Wear(Dressed dressed, int kind, LookItem item, SkinnedMeshRenderer game, SkinnedMeshRenderer head, Transform[] bones,
            Dictionary<AppearanceElementType, SkinnedMeshRenderer> selected)
        {
            AppearanceSetter character = dressed.Character;
            Piece piece = dressed.Pieces[kind];
            if (piece != null && (piece.Item != item || piece.Head != head || piece.Object == null))
            {
                Remove(dressed, kind);
                piece = null;
            }
            if (piece == null)
            {
                string problem = ModAssets.Prepare(item, character);
                if (problem != null)
                {
                    throw new InvalidOperationException(problem);
                }
                Material[] materials = MakeMaterials(item, character);
                if (materials == null)
                {
                    return;   // logged; the game's piece stays
                }
                piece = new Piece { Item = item, Head = head, Materials = materials };
                piece.Renderer = MakeRenderer(OurPrefix + item.Id, item.Mesh, head, bones, out piece.Object);
                piece.Renderer.sharedMaterials = materials;
                item.Wearers++;
                dressed.Pieces[kind] = piece;
            }

            bool underlay = item.Under != null;
            if (underlay)
            {
                Unhide(piece);   // worn beneath the game's piece, not instead of it
            }
            else
            {
                Hide(piece, game);
            }
            bool hairLike = kind == (int)LookKind.Hair || kind == (int)LookKind.Beard;
            if (hairLike)
            {
                Color colour = HairColour(game, kind == (int)LookKind.Beard, selected);
                if (underlay)
                {
                    // Roots in the shade of the hair above them: a little darker than the strands.
                    colour = new Color(colour.r * UnderlayShade, colour.g * UnderlayShade, colour.b * UnderlayShade, colour.a);
                }
                for (int i = 0; i < piece.Materials.Length; i++)
                {
                    Material material = piece.Materials[i];
                    if (material == null || !material.HasProperty(BaseColorId))
                    {
                        continue;
                    }
                    if (underlay)
                    {
                        // A surface on the skull, not strands: drawn before every layer of the game's hair, matt, and
                        // letting no light through (HairFinish.Underlay; numbers only, no keyword is touched).
                        HairFinish.Underlay(material, colour);
                    }
                    else
                    {
                        material.SetColor(BaseColorId, colour);
                        // The template's back-light is the game's orange for every hair colour: ours follows the colour.
                        HairFinish.Through(material, colour, i < piece.Item.Layers.Length && piece.Item.Layers[i].Role == LayerRole.Scalp);
                    }
                }
                ShapeByName(piece.Renderer, head);
                if (kind == (int)LookKind.Hair)
                {
                    ShapeUnderHat(piece.Renderer, game, character);
                }
            }
            else
            {
                Skin(piece, kind, game, head, bones, character);
                // Trousers follow the game's trousers in their shoe shapes ("Boots": the hem drawn in to go into the shaft),
                // unless shoes of ours are worn: low ones are cut to our hems (no shape), and our boots always want the tuck.
                LookItem ownShoes = kind == (int)LookKind.Legs ? Picked(LookKind.Feet, character, out string _) : null;
                bool tuck = ownShoes != null && ownShoes.MeshName.EndsWith(BootSuffix, StringComparison.Ordinal);
                ShapeBody(piece.Renderer, head, ownShoes != null ? null : game, tuck ? BootShape : null);
                ShapeBody(piece.Skin, head, null);
            }
            Light(piece, (LookKind)kind, character);
        }

        private static SkinnedMeshRenderer MakeRenderer(string name, Mesh mesh, SkinnedMeshRenderer head, Transform[] bones, out GameObject holder)
        {
            holder = new GameObject(name);
            holder.layer = head.gameObject.layer;
            holder.transform.SetParent(head.transform.parent, false);   // the gender's model: shown and hidden with it
            var renderer = holder.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = mesh;
            renderer.bones = bones;
            renderer.rootBone = head.rootBone;
            renderer.localBounds = WideBounds;
            renderer.updateWhenOffscreen = false;
            renderer.quality = head.quality;
            renderer.skinnedMotionVectors = head.skinnedMotionVectors;
            renderer.lightProbeUsage = head.lightProbeUsage;
            renderer.reflectionProbeUsage = head.reflectionProbeUsage;
            return renderer;
        }

        private static void Hide(Piece piece, SkinnedMeshRenderer game)
        {
            if (piece.Hidden != game)
            {
                Unhide(piece);
                if (game != null)
                {
                    piece.Hidden = game;
                    piece.HiddenWasOff = game.forceRenderingOff;
                }
            }
            if (game != null)
            {
                game.forceRenderingOff = true;
            }
        }

        private static void Unhide(Piece piece)
        {
            if (piece.Hidden != null)
            {
                piece.Hidden.forceRenderingOff = piece.HiddenWasOff;
            }
            piece.Hidden = null;
        }

        private static void Light(Piece piece, LookKind kind, AppearanceSetter character)
        {
            bool isDouble = character == _double;
            uint layers = isDouble ? _doubleLightLayers : piece.Head.renderingLayerMask;
            // Hair and beards cast no shadows in the game either; clothes do what the piece they replace does.
            ShadowCastingMode shadows = isDouble || kind == LookKind.Hair || kind == LookKind.Beard
                ? ShadowCastingMode.Off
                : piece.Hidden != null ? piece.Hidden.shadowCastingMode : ShadowCastingMode.On;
            piece.Renderer.renderingLayerMask = layers;
            piece.Renderer.shadowCastingMode = shadows;
            if (piece.Skin != null)
            {
                piece.Skin.renderingLayerMask = layers;
                piece.Skin.shadowCastingMode = shadows;
            }
        }

        private static void Remove(Dressed dressed, int kind)
        {
            Piece piece = dressed.Pieces[kind];
            if (piece == null)
            {
                return;
            }
            dressed.Pieces[kind] = null;
            Unhide(piece);
            ModAssets.Released(piece.Item);   // its mesh and textures leave memory at the next sweep, if nobody else wears them
            if (piece.Renderer != null)
            {
                piece.Renderer.enabled = false;
                piece.Renderer.sharedMesh = null;
            }
            if (piece.Object != null)
            {
                UnityEngine.Object.Destroy(piece.Object);
            }
            if (piece.SkinObject != null)
            {
                UnityEngine.Object.Destroy(piece.SkinObject);
            }
            if (piece.Materials != null)
            {
                for (int i = 0; i < piece.Materials.Length; i++)
                {
                    if (piece.Materials[i] != null)
                    {
                        UnityEngine.Object.Destroy(piece.Materials[i]);
                    }
                }
            }
            if (piece.OwnedSkin != null)
            {
                UnityEngine.Object.Destroy(piece.OwnedSkin);
            }
        }

        private static void Undress(Dressed dressed)
        {
            for (int kind = 0; kind < Kinds; kind++)
            {
                Remove(dressed, kind);
            }
        }

        // ---------------------------------------------------------------- materials

        private static Material[] MakeMaterials(LookItem item, AppearanceSetter character)
        {
            FindTemplates(character);
            var materials = new Material[item.Layers.Length];
            for (int i = 0; i < materials.Length; i++)
            {
                LookItem.Layer layer = item.Layers[i];
                Material template = Templates[(int)layer.Role];
                if (template == null)
                {
                    for (int k = 0; k < i; k++)
                    {
                        UnityEngine.Object.Destroy(materials[k]);
                    }
                    WarnOnce("template " + layer.Role, "New looks: the game has no material on this character to build a '" + layer.Role + "' layer from; '" + item.Id + "' is not shown.");
                    return null;
                }
                var material = new Material(template) { name = OurPrefix + item.Id + " " + layer.Role };
                SetTexture(material, BaseColorMapId, layer.Texture);
                if (layer.Role == LayerRole.Cloth)
                {
                    FinishCloth(material, layer);
                }
                else
                {
                    FinishHair(material, layer);
                }
                materials[i] = material;
            }
            return materials;
        }

        private static void FinishHair(Material material, LookItem.Layer layer)
        {
            // The template's normal map belongs to another mesh's layout: ours, or none.
            if (layer.Normal != null && material.HasProperty(NormalMapId))
            {
                SetTexture(material, NormalMapId, layer.Normal);
            }
            else if (material.HasProperty(NormalScaleId))
            {
                material.SetFloat(NormalScaleId, 0f);
            }
            if (layer.Mask != null && material.HasProperty(MaskMapId))
            {
                SetTexture(material, MaskMapId, layer.Mask);
            }
            if (material.IsKeywordEnabled(TransparentKeyword))
            {
                // Scalp first, then the body of the hair, then the loose strands over it.
                int order = layer.Role == LayerRole.Scalp ? 0 : layer.Role == LayerRole.Bulk ? 1 : 2;
                material.renderQueue = (int)RenderQueue.Transparent + order;
                if (material.HasProperty(SortPriorityId))
                {
                    material.SetFloat(SortPriorityId, order);
                }
            }
            if (layer.Role == LayerRole.Bulk && material.HasProperty(DepthPrepassClipId))
            {
                material.SetFloat(DepthPrepassClipId, BulkAlphaClip);
            }
            // Strips are seen from both sides; the scalp cap from outside only, or the far side of the head would
            // show through the near one. Culling is render state read from these numbers; no keyword is touched.
            bool twoSided = layer.Role != LayerRole.Scalp;
            for (int i = 0; i < CullIds.Length; i++)
            {
                if (material.HasProperty(CullIds[i]))
                {
                    material.SetFloat(CullIds[i], (float)(twoSided ? CullMode.Off : CullMode.Back));
                }
            }
            if (material.HasProperty(DoubleSidedEnableId))
            {
                material.SetFloat(DoubleSidedEnableId, twoSided ? 1f : 0f);
            }
        }

        private static void FinishCloth(Material material, LookItem.Layer layer)
        {
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, Color.white);
            }
            // The numbers every cloth material of the game has (F9 reports: all but three of its clothes), set here so
            // that ours do not depend on which of the game's materials happened to be the template.
            for (int i = 0; i < ClothIds.Length; i++)
            {
                if (material.HasProperty(ClothIds[i]))
                {
                    material.SetFloat(ClothIds[i], ClothValues[i]);
                }
            }
            // Colour: either all in our texture, or (a tint mask) as the game colours its own clothes: a pale base map
            // and a mask whose red, green and blue areas each take one colour.
            bool tinted = layer.TintMask != null && layer.Tints != null && material.HasProperty(ColourMaskMapId);
            for (int i = 0; i < ColourMaskSwitches.Length; i++)
            {
                bool channel = tinted && i < layer.Tints.Length && material.HasProperty(MaskColourIds[i]);
                if (material.HasProperty(ColourMaskSwitches[i]))
                {
                    material.SetFloat(ColourMaskSwitches[i], channel ? 1f : 0f);
                }
                if (channel)
                {
                    Color tint = layer.Tints[i];
                    material.SetColor(MaskColourIds[i], new Color(tint.r, tint.g, tint.b, 0f));
                    if (material.HasProperty(SheenColourIds[i]))
                    {
                        material.SetColor(SheenColourIds[i], new Color(tint.r * ClothSheen, tint.g * ClothSheen, tint.b * ClothSheen, 0f));
                    }
                }
            }
            if (tinted)
            {
                SetTexture(material, ColourMaskMapId, layer.TintMask);
            }
            SetTexture(material, NormalMapId, layer.Normal != null ? layer.Normal : _flatNormal);
            SetTexture(material, MaskMapId, layer.Mask != null ? layer.Mask : _flatMask);
        }

        private static void SetTexture(Material material, int property, Texture texture)
        {
            if (texture != null && material.HasProperty(property))
            {
                material.SetTexture(property, texture);
                material.SetTextureScale(property, Vector2.one);
                material.SetTextureOffset(property, Vector2.zero);
            }
        }

        /// <summary>
        /// Picks, once per scene, the game materials our layers are copied from. Every renderer of the
        /// character is looked at, worn or not, both genders: the materials on pieces not being worn
        /// are the game's untouched originals.
        /// </summary>
        private static void FindTemplates(AppearanceSetter character)
        {
            // Again if one has gone: a copied material dies with the character that owned it.
            if (_templatesSearched && Templates[(int)LayerRole.Scalp] != null && Templates[(int)LayerRole.Bulk] != null
                && Templates[(int)LayerRole.Wisps] != null && Templates[(int)LayerRole.Cloth] != null)
            {
                return;
            }
            _templatesSearched = true;
            Array.Clear(Templates, 0, Templates.Length);
            int bulkScore = 0, wispsScore = 0, scalpScore = 0, clothScore = 0;
            foreach (SkinnedMeshRenderer renderer in character.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if (renderer.name.StartsWith(OurPrefix, StringComparison.Ordinal))
                {
                    continue;
                }
                renderer.GetSharedMaterials(Scratch);
                for (int i = 0; i < Scratch.Count; i++)
                {
                    Material material = Scratch[i];
                    // Never one of the mod's own copies (looks, finished hair layers): those die with what they were made for.
                    if (material == null || material.shader == null || material.name.IndexOf("RealLifeOverhaul", StringComparison.Ordinal) >= 0)
                    {
                        continue;
                    }
                    string shader = material.shader.name;
                    if (shader == CharacterTools.HairShader)
                    {
                        if (!material.IsKeywordEnabled(TransparentKeyword) || !material.HasProperty(BaseColorMapId))
                        {
                            continue;
                        }
                        bool alphaTest = material.IsKeywordEnabled(AlphaTestKeyword);
                        bool prepass = material.HasProperty(DepthPrepassId) && material.GetFloat(DepthPrepassId) > 0.5f;
                        bool scalp = material.name.IndexOf("Scalp", StringComparison.OrdinalIgnoreCase) >= 0;
                        bool soft = !alphaTest && !prepass;
                        // Bulk: a layer that writes depth where the strands are solid, so the hair is a mass and sorts properly.
                        Consider(LayerRole.Bulk, material, alphaTest && prepass ? 3 : alphaTest && !scalp ? 2 : !scalp ? 1 : 0, ref bulkScore);
                        Consider(LayerRole.Wisps, material, soft && !scalp ? 2 : !scalp ? 1 : 0, ref wispsScore);
                        Consider(LayerRole.Scalp, material, soft && scalp ? 3 : soft ? 2 : scalp ? 1 : 0, ref scalpScore);
                    }
                    else if (shader == ClothesShader)
                    {
                        if (material.HasProperty(BaseColorMapId))
                        {
                            Consider(LayerRole.Cloth, material, material.IsKeywordEnabled(MaskMapKeyword) && material.IsKeywordEnabled(NormalMapKeyword) ? 2 : 1, ref clothScore);
                        }
                    }
                    if (_flatNormal == null)
                    {
                        _flatNormal = Flat(material, NormalMapId);
                    }
                    if (_flatMask == null)
                    {
                        _flatMask = Flat(material, MaskMapId);
                    }
                }
            }
            Scratch.Clear();
            if (Templates[(int)LayerRole.Wisps] == null)
            {
                Templates[(int)LayerRole.Wisps] = Templates[(int)LayerRole.Bulk];
            }
            if (Templates[(int)LayerRole.Scalp] == null)
            {
                Templates[(int)LayerRole.Scalp] = Templates[(int)LayerRole.Wisps];
            }
            if (Warned.Add("templates"))
            {
                Logger?.Info("New looks: materials are copied from the game's " + TemplateNames() + ".");
            }
        }

        private static void Consider(LayerRole role, Material material, int score, ref int best)
        {
            if (score > best)
            {
                best = score;
                Templates[(int)role] = material;
            }
        }

        private static Texture Flat(Material material, int property)
        {
            Texture texture = material.HasProperty(property) ? material.GetTexture(property) : null;
            return texture != null && texture.width <= SmallTexture ? texture : null;
        }

        private static string TemplateNames()
        {
            var text = new StringBuilder(160);
            for (int role = 0; role < Templates.Length; role++)
            {
                Material template = Templates[role];
                text.Append(role > 0 ? ", " : "").Append((LayerRole)role).Append(" '").Append(template != null ? template.name : "none").Append('\'');
                if (template != null)
                {
                    text.Append(" (queue ").Append(template.renderQueue).Append(template.IsKeywordEnabled(AlphaTestKeyword) ? ", alpha test" : "")
                        .Append(template.HasProperty(DepthPrepassId) && template.GetFloat(DepthPrepassId) > 0.5f ? ", depth prepass" : "").Append(')');
                }
            }
            return text.ToString();
        }

        // ---------------------------------------------------------------- hair colour and shapes

        // The game writes the chosen colour onto its own hair on every update; that is where it is read from.
        private static Color HairColour(SkinnedMeshRenderer game, bool beard, Dictionary<AppearanceElementType, SkinnedMeshRenderer> selected)
        {
            if (TryColour(game, out Color colour))
            {
                return colour;
            }
            if (beard && selected.TryGetValue(AppearanceElementType.Hair, out SkinnedMeshRenderer hair) && TryColour(hair, out colour))
            {
                return colour;
            }
            if (selected.TryGetValue(AppearanceElementType.Eyebrows, out SkinnedMeshRenderer brows) && TryColour(brows, out colour))
            {
                return colour;
            }
            return DarkBrown;
        }

        private static bool TryColour(SkinnedMeshRenderer renderer, out Color colour)
        {
            colour = DarkBrown;
            if (renderer == null || renderer.sharedMesh == null)
            {
                return false;   // the game's "None"
            }
            renderer.GetSharedMaterials(Scratch);
            bool found = false;
            for (int i = 0; i < Scratch.Count && !found; i++)
            {
                if (Scratch[i] != null && Scratch[i].HasProperty(BaseColorId))
                {
                    colour = Scratch[i].GetColor(BaseColorId);
                    found = true;
                }
            }
            Scratch.Clear();
            return found;
        }

        // Hair and beards: every shape of ours that the head also has (body sliders, mouth shapes) takes the head's value.
        private static void ShapeByName(SkinnedMeshRenderer ours, SkinnedMeshRenderer head)
        {
            Mesh mesh = ours.sharedMesh;
            Mesh headMesh = head.sharedMesh;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                int match = headMesh != null ? headMesh.GetBlendShapeIndex(mesh.GetBlendShapeName(i)) : -1;
                ours.SetBlendShapeWeight(i, match >= 0 ? head.GetBlendShapeWeight(match) : 0f);
            }
        }

        // Hats press hair down through a shape on the hair that the hat names. Taken from the game's own hair
        // when it has the shape, and from the hat itself otherwise (the game's hair may be "None").
        private static void ShapeUnderHat(SkinnedMeshRenderer ours, SkinnedMeshRenderer game, AppearanceSetter character)
        {
            Mesh mesh = ours.sharedMesh;
            Mesh gameMesh = game != null ? game.sharedMesh : null;
            if (gameMesh != null)
            {
                for (int i = 0; i < gameMesh.blendShapeCount; i++)
                {
                    int match = mesh.GetBlendShapeIndex(gameMesh.GetBlendShapeName(i));
                    if (match >= 0)
                    {
                        ours.SetBlendShapeWeight(match, game.GetBlendShapeWeight(i));
                    }
                }
            }
            List<AppearanceElementVariant> hats = character.GetElementVariants(AppearanceElementType.HeadAccessory);
            for (int i = 0; hats != null && i < hats.Count; i++)
            {
                AppearanceElementVariant hat = hats[i];
                if (hat.gameObject.activeSelf && hat.hasCustomBlendshapeModifier && hat.elementTypeThatModifies == AppearanceElementType.Hair
                    && !string.IsNullOrEmpty(hat.customBlendshapeModifier))
                {
                    int match = mesh.GetBlendShapeIndex(hat.customBlendshapeModifier);
                    if (match >= 0)
                    {
                        ours.SetBlendShapeWeight(match, 100f);
                    }
                }
            }
        }

        // Clothes: the first three shapes are fat, slim and muscular, as on every clothing mesh of the game
        // (which sets them by position). Any further shape of ours follows the game's piece by name.
        private static void ShapeBody(SkinnedMeshRenderer ours, SkinnedMeshRenderer head, SkinnedMeshRenderer game, string full = null)
        {
            if (ours == null || ours.sharedMesh == null)
            {
                return;
            }
            Mesh mesh = ours.sharedMesh;
            Mesh gameMesh = game != null ? game.sharedMesh : null;
            for (int i = 0; i < mesh.blendShapeCount; i++)
            {
                float weight = 0f;
                if (i < BodyShapes)
                {
                    weight = head.GetBlendShapeWeight(i);   // the game sets the same three on the head
                }
                else if (gameMesh != null)
                {
                    int match = gameMesh.GetBlendShapeIndex(mesh.GetBlendShapeName(i));
                    weight = match >= BodyShapes ? game.GetBlendShapeWeight(match) : 0f;
                }
                else if (full != null && mesh.GetBlendShapeName(i) == full)
                {
                    weight = 100f;   // our own boots are worn: the hem goes into the shaft
                }
                ours.SetBlendShapeWeight(i, weight);
            }
        }

        // ---------------------------------------------------------------- the skin a garment leaves bare

        private static void Skin(Piece piece, int kind, SkinnedMeshRenderer game, SkinnedMeshRenderer head, Transform[] bones, AppearanceSetter character)
        {
            if (piece.Item.SkinFrom == null)
            {
                return;
            }
            if (piece.Skin == null)
            {
                Mesh mesh = SkinMesh(piece.Item, ElementTypes[kind], character, out Material sourceSkin);
                if (mesh == null)
                {
                    return;   // logged; the garment is shown without bare skin
                }
                piece.Skin = MakeRenderer(OurPrefix + piece.Item.Id + " skin", mesh, head, bones, out piece.SkinObject);
                piece.Skin.sharedMaterial = sourceSkin;   // until the wearer's own is found below
            }

            // Best: the very material the game tints for the piece we are hiding, so skin tone changes keep arriving.
            Material shared = null;
            if (game != null)
            {
                var variant = game.GetComponent<AppearanceElementVariant>();
                if (variant != null && variant.skinMaterialIndex >= 0)
                {
                    game.GetSharedMaterials(Scratch);
                    shared = variant.skinMaterialIndex < Scratch.Count ? Scratch[variant.skinMaterialIndex] : null;
                    Scratch.Clear();
                }
            }
            if (shared != null)
            {
                piece.Skin.sharedMaterial = shared;
                if (piece.OwnedSkin != null)
                {
                    UnityEngine.Object.Destroy(piece.OwnedSkin);
                    piece.OwnedSkin = null;
                }
                return;
            }
            // The piece being hidden shows no skin (long trousers): a copy of the source's skin material, tinted here.
            if (piece.OwnedSkin == null)
            {
                Material source = piece.Skin.sharedMaterial;
                if (source == null)
                {
                    return;
                }
                piece.OwnedSkin = new Material(source) { name = OurPrefix + piece.Item.Id + " skin" };
            }
            if (piece.OwnedSkin.HasProperty(MaskColorRedId))
            {
                piece.OwnedSkin.SetColor(MaskColorRedId, character.data.color);
            }
            piece.Skin.sharedMaterial = piece.OwnedSkin;
        }

        /// <summary>
        /// The skin part of one of the game's own pieces, as a mesh of its own. Made once per source
        /// mesh from the game's mesh in memory (nothing of the game's is shipped with the mod).
        /// </summary>
        private static Mesh SkinMesh(LookItem item, AppearanceElementType type, AppearanceSetter character, out Material skin)
        {
            skin = null;
            AppearanceElementVariant source = null;
            List<AppearanceElementVariant> variants = character.GetElementVariants(type);
            for (int i = 0; variants != null && i < variants.Count; i++)
            {
                if (variants[i].name == item.SkinFrom)
                {
                    source = variants[i];
                    break;
                }
            }
            SkinnedMeshRenderer renderer = source != null ? source.GetComponent<SkinnedMeshRenderer>() : null;
            Mesh mesh = renderer != null ? renderer.sharedMesh : null;
            int slot = source != null ? source.skinMaterialIndex : -1;
            if (mesh == null || slot < 0 || slot >= mesh.subMeshCount || !mesh.isReadable)
            {
                WarnOnce("skin " + item.Id, "New looks: the game's '" + item.SkinFrom + "' has no usable skin part for '" + item.Id + "' ("
                                             + (source == null ? "no such piece" : mesh == null ? "no mesh" : !mesh.isReadable ? "mesh not readable" : "skin slot " + slot + " of " + mesh.subMeshCount + " parts")
                                             + "); it is shown without bare skin.");
                return null;
            }
            renderer.GetSharedMaterials(Scratch);
            skin = slot < Scratch.Count ? Scratch[slot] : null;
            Scratch.Clear();

            // A garment worn over bare skin names the triangles of that skin that lie under its cloth: those are left
            // out, so nothing can come through the cloth. Only if the game's mesh is still the one they were counted on.
            int[] triangles = mesh.GetTriangles(slot);
            bool hide = item.SkinHidden != null && item.SkinTriangles * 3 == triangles.Length;
            if (item.SkinHidden != null && !hide)
            {
                WarnOnce("skin hide " + item.MeshName, "New looks: the game's '" + item.SkinFrom + "' has " + (triangles.Length / 3) + " skin triangles, '" + item.Id
                                                       + "' was made for " + item.SkinTriangles + "; the skin under its cloth is drawn too.");
            }
            long key = ((long)mesh.GetInstanceID() << 8) | (uint)slot;
            if (hide)
            {
                key ^= (long)item.MeshName.GetHashCode() << 24;
            }
            if (SkinMeshes.TryGetValue(key, out Mesh made) && made != null)
            {
                return made;
            }
            made = UnityEngine.Object.Instantiate(mesh);
            made.name = OurPrefix + mesh.name + " skin" + (hide ? " under " + item.MeshName : "");
            if (hide)
            {
                var drop = new bool[item.SkinTriangles];
                int dropped = 0;
                for (int i = 0; i < item.SkinHidden.Length; i++)
                {
                    int index = item.SkinHidden[i];
                    if (index >= 0 && index < drop.Length && !drop[index])
                    {
                        drop[index] = true;
                        dropped++;
                    }
                }
                var kept = new int[(drop.Length - dropped) * 3];
                for (int i = 0, k = 0; i < drop.Length; i++)
                {
                    if (!drop[i])
                    {
                        kept[k++] = triangles[i * 3];
                        kept[k++] = triangles[i * 3 + 1];
                        kept[k++] = triangles[i * 3 + 2];
                    }
                }
                triangles = kept;
            }
            made.subMeshCount = 1;
            made.SetTriangles(triangles, 0, false);
            SkinMeshes[key] = made;
            return made;
        }

        // ---------------------------------------------------------------- failure and the F9 report

        private static void WarnOnce(string what, string message)
        {
            if (Warned.Add(what))
            {
                Logger?.Warn(message + " (Logged once.)");
            }
        }

        private static void Fail(string why, Exception error)
        {
            if (!_failed)
            {
                _failed = true;
                _failure = why;
                Logger?.Warn("New looks: switched off until the next load, " + why + ". The game's own hair and clothes are shown.");
                if (error != null)
                {
                    Logger?.Error(error);
                }
            }
            RestoreAll();
        }

        private static string Tints(Material material)
        {
            string text = "";
            string[] names = { "red", "green", "blue" };
            for (int i = 0; i < names.Length; i++)
            {
                if (material.HasProperty(ColourMaskSwitches[i]) && material.GetFloat(ColourMaskSwitches[i]) > 0.5f && material.HasProperty(MaskColourIds[i]))
                {
                    text += " " + names[i] + "=" + ColorUtility.ToHtmlStringRGB(material.GetColor(MaskColourIds[i]));
                }
            }
            return text.Length > 0 ? ", tint mask" + text : "";
        }

        private static string Maps(Material material)
        {
            string text = "";
            int[] ids = { BaseColorMapId, NormalMapId, MaskMapId, ColourMaskMapId };
            for (int i = 0; i < ids.Length; i++)
            {
                Texture texture = material.HasProperty(ids[i]) ? material.GetTexture(ids[i]) : null;
                text += (i == 0 ? ", maps " : " | ") + (texture != null ? texture.name + " " + texture.width : "none");
            }
            return text;
        }

        /// <summary>The scalp underlay this character wears under the game's hair, for the hair check's line; "none" without one.</summary>
        public static string DescribeUnderlay(AppearanceSetter character)
        {
            for (int i = 0; i < All.Count; i++)
            {
                Piece piece = All[i].Character == character ? All[i].Pieces[(int)LookKind.Hair] : null;
                if (piece == null || piece.Item == null || piece.Item.Under == null || piece.Materials == null || piece.Materials.Length == 0 || piece.Materials[0] == null)
                {
                    continue;
                }
                Material material = piece.Materials[0];
                bool drawn = piece.Renderer != null && piece.Renderer.enabled && !piece.Renderer.forceRenderingOff;
                return "'" + piece.Item.Id + "' (a cap on the skull under '" + piece.Item.Under + "', " + (drawn ? "drawn" : "NOT drawn") + ", queue " + material.renderQueue
                       + (material.IsKeywordEnabled(TransparentKeyword) ? ", blended" : ", solid") + (material.IsKeywordEnabled(AlphaTestKeyword) ? ", alpha-tested" : "")
                       + ", one-sided, colour " + ColorUtility.ToHtmlStringRGB(material.GetColor(BaseColorId))
                       + (material.HasProperty(TransmissionColorId) ? ", back-light " + ColorUtility.ToHtmlStringRGB(material.GetColor(TransmissionColorId)) : "") + ")";
            }
            return "none";
        }

        public static void Describe(StringBuilder text)
        {
            text.AppendLine("new looks: option " + Settings.CharacterNewLooks + ", assets " + ModAssets.Describe() + (_failed ? ", FAILED this scene: " + _failure : ""));
            text.Append("  picks (" + CharacterCatalogue.Context + "):");
            foreach (CharacterCatalogue.Slot slot in CharacterCatalogue.Slots)
            {
                text.Append(' ').Append(slot.OptionId).Append('=').Append(slot.Ids[slot.Index] ?? "game");
            }
            text.AppendLine();
            text.AppendLine("  templates: " + (_templatesSearched ? TemplateNames() : "not looked for yet")
                            + ", flat normal '" + (_flatNormal != null ? _flatNormal.name : "none") + "', flat mask '" + (_flatMask != null ? _flatMask.name : "none") + "'");
            for (int i = 0; i < All.Count; i++)
            {
                Dressed dressed = All[i];
                text.AppendLine("  on '" + (dressed.Character != null ? dressed.Character.name : "destroyed") + "'" + (dressed.Character == _double ? " (the player's double)" : "") + ":");
                for (int kind = 0; kind < Kinds; kind++)
                {
                    Piece piece = dressed.Pieces[kind];
                    if (piece == null || piece.Renderer == null)
                    {
                        continue;
                    }
                    text.Append("    ").Append((LookKind)kind).Append(" '").Append(piece.Item.Id).Append("': ").Append(piece.Item.Vertices).Append(" vertices, ")
                        .Append(piece.Item.Triangles).Append(" triangles, ").Append(piece.Materials.Length).Append(" materials, shadows ").Append(piece.Renderer.shadowCastingMode)
                        .Append(", replaces '").Append(piece.Hidden != null ? piece.Hidden.name : "nothing").Append('\'');
                    if (piece.Skin != null)
                    {
                        text.Append(", skin '").Append(piece.Skin.sharedMesh != null ? piece.Skin.sharedMesh.name : "none").Append("' with ")
                            .Append(piece.OwnedSkin != null ? "a tinted copy of the skin material" : "the game's own skin material");
                    }
                    text.AppendLine();
                    for (int m = 0; m < piece.Materials.Length; m++)
                    {
                        Material material = piece.Materials[m];
                        if (material != null)
                        {
                            text.AppendLine("      " + material.name + ": shader '" + material.shader.name + "' queue " + material.renderQueue
                                            + (material.HasProperty(BaseColorId) ? " colour " + ColorUtility.ToHtmlStringRGBA(material.GetColor(BaseColorId)) : "")
                                            + Tints(material) + Maps(material));
                        }
                    }
                }
            }
        }
    }
}
