using System;
using System.Reflection;
using BigAmbitions.Characters;
using BigAmbitions.Characters.Appearance;
using Character;
using Character.Customization;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The game's portrait camera (Character.Customization.PortraitGenerator, as the Rivals app draws a rival), shared by
    /// every painter of faces of the mod: the contacts' (ContactPainter) and Flare's (Social/Flare/FlareFacePainter). It
    /// draws one face at a time: the game reads each back into one texture of its own and lets that go when it is done, so
    /// two at once would spoil each other. A painter takes the turn (<see cref="Take"/>), asks for the face, and gives the
    /// turn back when the face comes (<see cref="Give"/>); a turn not given back in <see cref="Timeout"/> seconds lapses.
    /// After each face the camera rests <see cref="PaceSeconds"/> in play, <see cref="HiddenPaceSeconds"/> behind the game's
    /// black screen, and not at all behind the loading screen while it is held for the faces (Core/LoadHold: the next face
    /// at once, the load's time being the only cost).
    ///
    /// A FACE OF A LOOK (<see cref="Face"/>): built exactly as the mod builds the person in the city, AppearanceSetter.
    /// RandomizeElements with the person's sex, the skin of their seed, casual clothes and their seed, their age, and the
    /// build capped as the mod's own people are (Social/SocialCast.Dress); the beard's colour rolled from the same seed with
    /// the engine's dice put back. A FACE OF A CHARACTER (<see cref="Character"/>): one the game has (one of the player's
    /// staff), drawn from its own data.
    ///
    /// COST: nothing at rest. A face is the game's portrait render with its read-back (some 10 to 30 ms of one frame, once
    /// per look, ever, for a face kept by its painter), told to the session watch first by the painter.
    /// </summary>
    internal static class PortraitTurn
    {
        public const float PaceSeconds = 2f;          // real seconds after a face in play
        public const float HiddenPaceSeconds = 0.25f; // ...and behind the game's black screen
        public const float LoadPaceSeconds = 0f;      // ...and behind the loading screen held for the faces (Core/LoadHold): no frame of play to spare
        public const float Timeout = 15f;             // seconds to wait for the camera's read-back before the turn lapses
        private const float BuildMost = 0.55f;        // the build cap the mod's people are made with (Social/SocialCast.MaxBuild)
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

        /// <summary>The turn given back with a rest of its own (one of the paces above).</summary>
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
                return _generator == null || _generator.GetValue(null) as PortraitGenerator != null;   // (not known: the game's public call is tried, and the time-out covers it)
            }
        }

        /// <summary>
        /// A face of a look asked of the game's portrait camera; 'back' gets the game's sprite (its texture readable, 256
        /// pixels square) or null. False, with nothing asked, while the camera is not there.
        /// </summary>
        public static bool Face(bool female, int age, int seed, Action<Sprite> back)
        {
            PortraitGenerator generator = Generator();
            if (generator == null && _generator != null)
            {
                return false;   // the city's portrait camera is not there yet (the main menu, a load)
            }
            int days = age * GameCalendar.DaysPerYear;
            BigAmbitions.Characters.Gender gender = female ? BigAmbitions.Characters.Gender.Female : BigAmbitions.Characters.Gender.Male;
            AppearanceSetter setter = generator != null ? Setter(generator) : null;
            if (setter == null)
            {
                // Without the camera's own appearance the game's public way is used: the same person each time, though not
                // dressed as the mod dresses them in the city.
                if (!_toldFallback)
                {
                    _toldFallback = true;
                    ContactPictures.Logger?.Warn("Portraits: the portrait camera is built another way in this version of the game: faces are drawn with the game's own random look for each person.");
                }
                PortraitGenerator.Create(gender, days, seed.ToString(), seed, null, null, Safe(back));
                return true;
            }
            UnityEngine.Random.State dice = UnityEngine.Random.state;
            UnityEngine.Random.InitState(seed);
            try
            {
                setter.data.ageInDays = days;
                setter.RandomizeElements(gender, SkinColorHelper.GetRandom(seed), Casual, seed);
                setter.data.fatness = Mathf.Min(setter.data.fatness, BuildMost);
                PortraitGenerator.Create(setter.data, null, null, Safe(back));
            }
            finally
            {
                UnityEngine.Random.state = dice;
            }
            return true;
        }

        /// <summary>A face of one of the game's own characters asked of the portrait camera. False while the camera is not there or with no data.</summary>
        public static bool Character(CharacterData data, Action<Sprite> back)
        {
            if (data == null || (Generator() == null && _generator != null))
            {
                return false;
            }
            PortraitGenerator.Create(data, null, null, Safe(back));
            return true;
        }

        // The face's answer run inside the guard (UiClone.Safe): the game calls it from its read-back, outside any call of the mod's.
        private static Action<Sprite> Safe(Action<Sprite> back)
        {
            return sprite => UiClone.Safe(() => back?.Invoke(sprite));
        }

        // The game's portrait camera of this scene, and the appearance it draws (both private: read once).
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
            _generator = GameContract.PortraitInstance.Field;   // (resolved once: Core/GameContract)
            _setter = GameContract.PortraitCharacter.Field;
            if (_setter != null && _setter.FieldType != typeof(AppearanceSetter))
            {
                _setter = null;
            }
        }
    }
}
