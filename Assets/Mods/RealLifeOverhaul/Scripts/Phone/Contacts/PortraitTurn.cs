using System;
using System.Reflection;
using BigAmbitions.Characters.Appearance;
using Character;
using Character.Customization;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's portrait camera, shared by the contacts' and Flare's face painters. The game reads each face back into one
    /// texture of its own, so a painter takes the turn (<see cref="Take"/>), asks, and gives it back when the face comes; a
    /// turn not given back lapses after <see cref="Timeout"/>. Cost: nothing at rest; a face is some 10 to 30 ms of one
    /// frame, once per look ever.
    /// </summary>
    internal static class PortraitTurn
    {
        public const float PaceSeconds = 2f;          // real seconds after a face in play
        public const float HiddenPaceSeconds = 0.25f; // real seconds after a face behind the game's black screen
        public const float LoadPaceSeconds = 0f;      // behind the loading screen held for the faces: no frame of play to spare
        public const float Timeout = 15f;             // seconds to wait for the camera's read-back before the turn lapses
        private const float BuildMost = 0.55f;        // the build cap the mod's people are made with (SocialCast.MaxBuild)
        private static readonly AppearanceTag[] Casual = { AppearanceTag.Casual };

        private static object _holder;
        private static float _since, _next;
        private static FieldInfo _generator, _setter;
        private static bool _reflected, _toldFallback;

        /// <summary>True if a painter may take the turn now.</summary>
        public static bool Free(float now)
        {
            return (_holder == null || now - _since > Timeout) && now >= _next;
        }

        /// <summary>The turn taken by a painter; false if another has it or the camera rests.</summary>
        public static bool Take(object who, float now)
        {
            if (!Free(now))
            {
                return _holder == who && now - _since <= Timeout;
            }
            _holder = who;
            _since = now;
            return true;
        }

        /// <summary>The turn given back: the camera rests before the next face.</summary>
        public static void Give(object who, float now, bool hidden)
        {
            Give(who, now, hidden ? HiddenPaceSeconds : PaceSeconds);
        }

        /// <summary>The turn given back with a rest of its own, in seconds.</summary>
        public static void Give(object who, float now, float rest)
        {
            if (_holder != who)
            {
                return;
            }
            _holder = null;
            _next = now + Math.Max(0f, rest);
        }

        /// <summary>The city's portrait camera is there (not on the main menu or during a load).</summary>
        public static bool Ready
        {
            get
            {
                Reflect();
                return _generator == null || _generator.GetValue(null) as PortraitGenerator != null;   // not known: the game's public call is tried
            }
        }

        /// <summary>Asks for a face of a look; 'back' gets the game's readable sprite or null. False, nothing asked, without the camera.</summary>
        public static bool Face(bool female, int age, int seed, Action<Sprite> back)
        {
            PortraitGenerator generator = Generator();
            if (generator == null && _generator != null)
            {
                return false;   // not there yet: the main menu, a load
            }
            int days = age * GameCalendar.DaysPerYear;
            BigAmbitions.Characters.Gender gender = female ? BigAmbitions.Characters.Gender.Female : BigAmbitions.Characters.Gender.Male;
            AppearanceSetter setter = generator != null ? Setter(generator) : null;
            if (setter == null)
            {
                // Without the camera's own appearance the game's public way: the same person, not dressed as in the city.
                if (!_toldFallback)
                {
                    _toldFallback = true;
                    ContactPictures.Logger?.Warn("Portraits: the portrait camera is built another way in this version of the game: faces are drawn with the game's own random look for each person.");
                }
                CharacterData kept = PersonLookGame.Data(seed, days);
                if (kept != null)
                {
                    PortraitGenerator.Create(kept, null, null, Safe(back));   // a kept look is whole
                    return true;
                }
                PortraitGenerator.Create(gender, days, seed.ToString(), seed, null, null, Safe(back));
                return true;
            }
            UnityEngine.Random.State dice = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            try
            {
                setter.data.ageInDays = days;
                if (!PersonLookGame.Dress(setter, seed))   // a kept look: somebody met in the street, as they were there
                {
                    setter.RandomizeElements(gender, SkinColorHelper.GetRandom(seed), Casual, seed);
                }
                setter.data.fatness = Mathf.Min(setter.data.fatness, BuildMost);
                PortraitGenerator.Create(setter.data, null, null, Safe(back));
            }
            finally
            {
                UnityEngine.Random.state = dice;
            }
            return true;
        }

        /// <summary>A face of one of the game's own characters. False while the camera is not there, or with no data.</summary>
        public static bool Character(CharacterData data, Action<Sprite> back)
        {
            if (data == null || (Generator() == null && _generator != null))
            {
                return false;
            }
            PortraitGenerator.Create(data, null, null, Safe(back));
            return true;
        }

        // The game calls the answer from its read-back, outside any call of the mod's: so it runs inside the guard.
        private static Action<Sprite> Safe(Action<Sprite> back)
        {
            return sprite => UiClone.Safe(() => back?.Invoke(sprite));
        }

        // The scene's portrait camera and the appearance it draws, both private fields.
        private static PortraitGenerator Generator()
        {
            Reflect();
            return _generator != null ? _generator.GetValue(null) as PortraitGenerator : null;
        }

        private static AppearanceSetter Setter(PortraitGenerator generator)
        {
            Reflect();
            return _setter != null ? _setter.GetValue(generator) as AppearanceSetter : null;
        }

        private static void Reflect()
        {
            if (_reflected)
            {
                return;
            }
            _reflected = true;
            _generator = GameContract.PortraitInstance.Field;
            _setter = GameContract.PortraitCharacter.Field;
            if (_setter != null && _setter.FieldType != typeof(AppearanceSetter))
            {
                _setter = null;
            }
        }
    }
}
