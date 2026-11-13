using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using System.Text;
using BigAmbitions.Rivals;
using Character.Customization;
using Entities;
using Helpers;
using RealLifeOverhaul.GameUi;
using UI.Smartphone.Apps.Rivals;
using UnityEngine;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The game's own picture of each kind of person a screen shows, told to <see cref="UiFaces"/> once a person, by the
    /// name the screen shows them under. This is where "the picture of a person" meets the game, in the order every kind
    /// follows: a sprite the game holds (a special rival's own, the game's cache of a rival's portrait, the HUD's portrait
    /// file of the player, a contact's icon in the game's list), then the game's portrait generator drawing the person
    /// from their data (a rival by the game's own call, a character from their appearance), then, in the view, the painted
    /// face for somebody the game has no picture of. A screen calls these while it reads the game: a call for a person
    /// already told is a dictionary read and nothing else (the sources are made once, so the screen is not laid out again
    /// for it). The mod's people with a look of their own (the cast, a partner, a roommate) are told by ContactPictures,
    /// which draws them through the mod's portrait cache. Cost: nothing a frame; one object a person, once.
    /// </summary>
    internal static class FaceSources
    {
        private static readonly Dictionary<string, FaceAsk> Rivals = new Dictionary<string, FaceAsk>(32, StringComparer.Ordinal);
        private static readonly Dictionary<string, FaceAsk> Characters = new Dictionary<string, FaceAsk>(64, StringComparer.Ordinal);
        private static PlayerFace _player;
        private static GameInstance _save;   // the save the sources were made for: another one starts them again

        /// <summary>The game's picture of a rival on the leaderboard (the player's own line too), told by the name the leaderboard shows.</summary>
        public static void Rival(RivalLeaderboardData line)
        {
            NewSave();
            if (line == null || string.IsNullOrEmpty(line.entryName))
            {
                return;
            }
            if (string.IsNullOrEmpty(line.rivalId))
            {
                Player(line.entryName);   // the player's own line
                return;
            }
            if (!Rivals.TryGetValue(line.rivalId, out FaceAsk ask))
            {
                RivalData rival = RivalsHelper.GetRivalData(line.rivalId);
                ask = new RivalFace(line.rivalId, rival);
                Rivals[line.rivalId] = ask;
            }
            UiFaces.Source(line.entryName, ask);
        }

        /// <summary>The player's own picture, the HUD's, told by the name a screen shows them under.</summary>
        public static void Player(string name)
        {
            NewSave();
            if (string.IsNullOrEmpty(name))
            {
                return;
            }
            UiFaces.Source(name, _player ??= new PlayerFace());
        }

        /// <summary>A character of the game's (an employee, a candidate, a rival's staff): the game's generator draws them from their own data.</summary>
        public static void Character(string name, CharacterData data)
        {
            NewSave();
            if (string.IsNullOrEmpty(name) || data == null)
            {
                return;
            }
            if (!Characters.TryGetValue(name, out FaceAsk ask))
            {
                ask = new CharacterFace(data);
                Characters[name] = ask;
            }
            UiFaces.Source(name, ask);
        }

        /// <summary>
        /// The key a screen draws a hired person's or candidate's face by: their name, and their own id behind it, so two people
        /// of one name (the game's name generator repeats) each keep their own face. Nothing is shown of the id; a person with no
        /// id (a made-up row) is told by the name alone.
        /// </summary>
        public static string Key(string name, EmployeeInstance employee)
        {
            return employee == null || string.IsNullOrEmpty(name) || string.IsNullOrEmpty(employee.id) ? name : name + "\u0001" + employee.id;
        }

        /// <summary>
        /// An employee, a candidate or a rival's staff member by the name a screen shows them under. With a look of their own
        /// (appearance parts) the game's generator draws them; the people the game offers without one (a rival's staff, made
        /// from a name, a sex and an age) have no character to draw, so they are painted from the sex and age the game gives.
        /// </summary>
        public static void Employee(string shown, EmployeeInstance employee) => Employee(shown, shown, employee);

        /// <summary>The same, told under <paramref name="key"/> (see <see cref="Key"/>) where the screen draws the face by that key.</summary>
        public static void Employee(string shown, string key, EmployeeInstance employee)
        {
            CharacterData data = employee != null ? employee.characterData : null;
            if (data == null || string.IsNullOrEmpty(shown) || shown != data.name)
            {
                return;   // a name the screen made up (awaiting replacement) is nobody's face
            }
            if (data.elements != null && data.elements.Count > 0)
            {
                Character(key, data);
            }
            else
            {
                UiFaces.Traits(key, data.gender == BigAmbitions.Characters.Gender.Female, TimeHelper.GetYearsByDays(data.ageInDays));
            }
        }

        /// <summary>The sprite the game's own contacts list holds under a contact's id (GlobalReferences.contactIcons, the list its Contacts app reads), or null.</summary>
        public static Sprite Icon(string id)
        {
            GlobalReferences references = InstanceBehavior<GlobalReferences>.Instance;
            Sprite[] icons = references != null ? references.contactIcons : null;
            for (int i = 0; icons != null && i < icons.Length; i++)
            {
                if (icons[i] != null && icons[i].name == id)
                {
                    return icons[i];
                }
            }
            return null;
        }

        /// <summary>A sprite the game already holds for a person (a contact's icon in its list), told once.</summary>
        public static void Held(string name, Sprite sprite)
        {
            if (string.IsNullOrEmpty(name) || sprite == null || UiFaces.Has(name))
            {
                return;
            }
            UiFaces.Source(name, new HeldFace(sprite));
        }

        // Another save: nobody of the last is kept, so a rival or an employee of the same name there is drawn from this save's own data.
        private static void NewSave()
        {
            GameInstance save = SaveGameManager.Current;
            if (ReferenceEquals(save, _save))
            {
                return;
            }
            _save = save;
            Rivals.Clear();
            Characters.Clear();
            _player = null;
            FaceTurns.Clear();
        }

        // ------------------------------------------------------------------------------------------------- the sources

        // A rival: a special rival's own sprite, else the game's cache of the portrait it draws when a rival is chosen, else
        // that same portrait drawn here by the game's own call and the seed it uses, so it is the face the game's app shows.
        private sealed class RivalFace : FaceAsk
        {
            private readonly string _id;
            private readonly RivalData _rival;

            public RivalFace(string id, RivalData rival)
            {
                _id = id;
                _rival = rival;
            }

            protected override Sprite Have()
            {
                SpecialRival special = RivalsHelper.GetSpecialRival(_id);
                if (special != null && special.rivalAppSprite != null)
                {
                    return special.rivalAppSprite;
                }
                return RivalPortraitHelper.GetPortrait(_id);
            }

            public override bool Start(Action<Sprite> back)
            {
                if (_rival == null || SaveGameManager.Current == null)
                {
                    return false;
                }
                // Exactly what RivalPortraitHelper.CreatePortrait asks, but kept here: the game's own call adds to its cache
                // with a plain Add, which throws if the game draws the same rival it was just asked for.
                PortraitGenerator.Create(_rival.gender, _rival.GetRivalAgeInYears(), _rival.id, Background(_rival.id), null, null, Guarded(back));
                return true;
            }

            // The game calls the answer from its read-back, outside any call of the mod's: so it runs inside the guard.
            private static Action<Sprite> Guarded(Action<Sprite> back)
            {
                return sprite => UiClone.Safe(() => back(sprite));
            }

            // The game's choice of backdrop for a rival (SelectedRivalUI.GetRivalBackgroundIndex): a number from the id.
            private static int Background(string id)
            {
                using (MD5 md5 = MD5.Create())
                {
                    return Mathf.Abs(BitConverter.ToInt32(md5.ComputeHash(Encoding.UTF8.GetBytes(id)), 0));
                }
            }
        }

        // A character of the game's, drawn from their data as the HUD's portrait is.
        private sealed class CharacterFace : FaceAsk
        {
            private readonly CharacterData _data;

            public CharacterFace(CharacterData data)
            {
                _data = data;
            }

            public override bool Start(Action<Sprite> back) => PortraitTurn.Character(_data, back);
        }

        // The player: the portrait file the HUD shows (the game's LoadPlayerPortrait, read once a save), else the generator
        // drawing the player's character as it draws the HUD's portrait when there is no file.
        private sealed class PlayerFace : FaceAsk
        {
            private const float Again = 5f;   // seconds between looks for the file while there is none: the HUD writes it when it first draws it
            private Sprite _file;
            private string _save;
            private float _looked = -Again;

            protected override Sprite Have()
            {
                string save = SaveGameManager.Current != null ? SaveGameManager.Current.characterId : null;
                if (save != _save)
                {
                    _save = save;
                    _file = null;
                    _looked = -Again;
                    Made = null;
                }
                if (_file == null && save != null && Time.realtimeSinceStartup - _looked >= Again)
                {
                    _looked = Time.realtimeSinceStartup;
                    _file = PortraitGenerator.LoadPlayerPortrait();
                }
                return _file;
            }

            public override bool Start(Action<Sprite> back) => PortraitTurn.Character(PlayerHelper.CharacterData, back);
        }

        // A sprite the game holds.
        private sealed class HeldFace : UiFaceSource
        {
            private readonly Sprite _sprite;

            public HeldFace(Sprite sprite)
            {
                _sprite = sprite;
            }

            public override Sprite Get() => _sprite;
        }
    }
}
