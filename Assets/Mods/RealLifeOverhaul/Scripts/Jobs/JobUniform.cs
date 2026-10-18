using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters.Appearance;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Work clothes over the player's own, drawn as the game draws its swimwear (<c>AppearanceSetter.UpdateVisuals(list)</c>), so
    /// nothing is written into their appearance data and no save holds the uniform. Owned by <see cref="JobDirector"/>, which
    /// takes it off on every way a job stops; one mesh merge each way.
    /// </summary>
    public sealed class JobUniform
    {
        private readonly IModLogger _logger;
        private readonly List<AppearanceElementData> _worn = new List<AppearanceElementData>(8);
        private readonly List<AppearanceElementData> _onDouble = new List<AppearanceElementData>(8);
        private readonly List<CityOutfits.Dye> _dyes = new List<CityOutfits.Dye>(4);
        private readonly List<CityOutfits.Dye> _doubleDyes = new List<CityOutfits.Dye>(4);
        private readonly Action<AppearanceSetter, List<AppearanceElementData>> _overDouble;
        private AppearanceSetter _look;
        private CityLook _outfit;
        private bool _on;
        private bool _failed;

        public JobUniform(IModLogger logger)
        {
            _logger = logger;
            _overDouble = OverDouble;
        }

        /// <summary>Puts one of the living city's outfits on over the player's own. False, nothing changed, if they cannot be dressed now.</summary>
        public bool Wear(PlayerController player, CityLook outfit)
        {
            if (_on && _outfit == outfit)
            {
                return true;
            }
            if (_failed || player == null || player.Character == null || player.Character.appearanceSetter == null)
            {
                return false;
            }
            Off();
            AppearanceSetter look = player.Character.appearanceSetter;
            if (look.data == null || look.data.elements == null || look.data.elements.Count == 0)
            {
                return false;
            }
            try
            {
                _worn.Clear();
                CityOutfits.Wear(look, outfit, _dyes, _worn);
                if (_worn.Count == 0)
                {
                    CityOutfits.Restore(_dyes);
                    return false;
                }
                _look = look;
                _outfit = outfit;
                _on = true;
                PlayerDouble.Workwear = _overDouble;
                Looks.Count("jobs: the player's uniform put on");
                look.UpdateVisuals(_worn);   // their own data untouched; the double is dressed through OverDouble
                Looks.Done();
                if (Meta.Dev.Verbose)
                {
                    _logger?.Info("Jobs: uniform on (" + outfit + ", " + _worn.Count + " pieces over the player's own; worn before: " + Describe(look, look.data.elements)
                                  + "). The player's own appearance data is not changed.");
                }
                return true;
            }
            catch (Exception e)
            {
                _failed = true;
                _logger?.Error(e);
                _logger?.Warn("Jobs: the uniform could not be put on; shifts are worked in the player's own clothes for this session.");
                Off();
                return false;
            }
        }

        /// <summary>The player's own clothes again. Safe at any time and in any state; never throws.</summary>
        public void Off()
        {
            if (!_on && _dyes.Count == 0 && _doubleDyes.Count == 0)
            {
                return;
            }
            bool was = _on;
            _on = false;
            if (ReferenceEquals(PlayerDouble.Workwear, _overDouble))
            {
                PlayerDouble.Workwear = null;
            }
            try
            {
                CityOutfits.Restore(_doubleDyes);
                CityOutfits.Restore(_dyes);
                AppearanceSetter look = _look;
                _look = null;
                if (was && look != null)   // gone with the city: nothing to draw, the next player is built from their own data
                {
                    Looks.Count("jobs: the player's uniform taken off");
                    look.UpdateVisuals();
                    Looks.Done();
                    if (Meta.Dev.Verbose)
                    {
                        _logger?.Info("Jobs: uniform off; the player is drawn in their own clothes again (" + Describe(look, look.data.elements) + ").");
                    }
                }
            }
            catch (Exception e)
            {
                _dyes.Clear();
                _doubleDyes.Clear();
                _logger?.Error(e);
            }
        }

        /// <summary>The city is going: nothing is drawn again, the records of this city's objects are dropped.</summary>
        public void Forget()
        {
            if (ReferenceEquals(PlayerDouble.Workwear, _overDouble))
            {
                PlayerDouble.Workwear = null;
            }
            _on = false;
            _look = null;
            _dyes.Clear();
            _doubleDyes.Clear();
            _worn.Clear();
            _onDouble.Clear();
        }

        // The detailed double is dressed from a copy of the player's data, with its own garments and colours.
        private void OverDouble(AppearanceSetter body, List<AppearanceElementData> elements)
        {
            if (!_on || body == null || elements == null)
            {
                return;
            }
            try
            {
                CityOutfits.Restore(_doubleDyes);
                _onDouble.Clear();
                CityOutfits.Wear(body, _outfit, _doubleDyes, _onDouble);
                for (int i = 0; i < _onDouble.Count; i++)
                {
                    AppearanceElementData piece = _onDouble[i];
                    for (int e = elements.Count - 1; e >= 0; e--)
                    {
                        if (elements[e] != null && elements[e].type == piece.type)
                        {
                            elements.RemoveAt(e);
                        }
                    }
                    elements.Add(piece);
                }
            }
            catch (Exception e)
            {
                _logger?.Error(e);
            }
        }

        // The torso, legs, feet and hat of a look by the game's piece names, for the log.
        private static string Describe(AppearanceSetter look, List<AppearanceElementData> elements)
        {
            var text = new System.Text.StringBuilder(96);
            for (int i = 0; i < elements.Count; i++)
            {
                AppearanceElementData piece = elements[i];
                if (piece == null || (piece.type != AppearanceElementType.Torso && piece.type != AppearanceElementType.Legs
                                      && piece.type != AppearanceElementType.Feet && piece.type != AppearanceElementType.HeadAccessory))
                {
                    continue;
                }
                string name = piece.variantId;
                List<AppearanceElementVariant> variants = look.GetElementVariants(piece.type);
                for (int v = 0; variants != null && v < variants.Count; v++)
                {
                    if (variants[v].id == piece.variantId)
                    {
                        name = variants[v].name;
                        break;
                    }
                }
                text.Append(text.Length > 0 ? ", " : string.Empty).Append(piece.type).Append(" '").Append(name).Append('\'');
            }
            return text.Length > 0 ? text.ToString() : "nothing listed";
        }
    }
}
