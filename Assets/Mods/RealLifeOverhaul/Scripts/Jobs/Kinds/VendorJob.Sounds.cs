using BigAmbitions.Characters;
using BigAmbitions.SoundSystem;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The drinks cart heard, with the game's own sounds as its counters make them (BarmanEmployee, FullServiceEmployee, Order):
    /// the customer's voice for their gender as their order starts, once more if an untouched order nears its end
    /// (<see cref="CartSoundRules.Remind"/>), and the till when a drink is paid for. A player looking away hears that an order
    /// has started. Nothing per frame: each sound is one call on its event, and the game's presets are looked at once for each
    /// sound manager the game makes.
    /// </summary>
    public sealed partial class VendorJob
    {
        private static SfxManager _soundsSeen;   // the sound manager whose presets were looked at
        private static bool _voicesHere;         // ...it has both customer voices, every one with a clip
        private static bool _tillHere;           // ...and the till

        private bool _remindDue;      // this order's one reminder is still to come
        private bool _tried;          // a drink was tried for this order: they heard it
        private bool _soundSaid;      // the missing sound was logged, once a shift
        private int _cueWanted;       // order cues wanted this shift (the option on at the time), and played
        private int _cues;
        private int _remindWanted;
        private int _reminds;
        private int _tillWanted;
        private int _tills;

        private bool Audible => CartSoundRules.Audible(Settings.JobCartSounds, Time.timeScale, _skip != null, _ending);

        // The customer is at the counter and their order's clock has started.
        private void OrderHeard(StreetActor person)
        {
            _tried = false;
            _remindDue = Audible;
            if (!_remindDue)
            {
                return;
            }
            _cueWanted++;
            if (Voice(person))
            {
                _cues++;
            }
        }

        // On each tick of an order still waiting: the one reminder, if it is due.
        private void OrderWaiting(StreetActor person, float now)
        {
            if (!CartSoundRules.Remind(_remindDue, _tried, now, _orderedAt, Patience, _orderEnds))
            {
                return;
            }
            _remindDue = false;
            if (!Audible)
            {
                return;
            }
            _remindWanted++;
            if (Voice(person))
            {
                _reminds++;
            }
        }

        // A drink was paid for at the counter.
        private void TillHeard()
        {
            if (!Audible)
            {
                return;
            }
            _tillWanted++;
            if (SoundsReady(out SfxManager sounds) && _tillHere)
            {
                // The player's own sale, as the game's petrol pumps and boat dealer play it.
                sounds.PlayAudio(SoundType.PurchaseSuccess, _cartAt + Vector3.up * CounterHeight, 1f, isPlayerCreatedSound: true);
                _tills++;
            }
            else
            {
                SoundMissing("the till");
            }
        }

        private bool Voice(StreetActor person)
        {
            if (person == null || !person.IsValid || !SoundsReady(out SfxManager sounds) || !_voicesHere)
            {
                SoundMissing("a customer's voice");
                return false;
            }
            // A customer of the street, as the game's barman plays them: not the player's own sound.
            sounds.PlayAudio(Female(person) ? SoundType.NpcFemaleProducerInteraction : SoundType.NpcMaleProducerInteraction, person.Position);
            return true;
        }

        private static bool Female(StreetActor person)
        {
            var look = person.Character.appearanceSetter;
            if (look == null || look.data == null)
            {
                return CartSoundRules.FemaleBySeed(person.Lease);
            }
            return look.data.gender == Gender.Female;
        }

        // The game's sound manager, with its presets looked at once: PlayAudio warns for a preset it lacks and throws on one with an empty clip.
        private static bool SoundsReady(out SfxManager sounds)
        {
            sounds = InstanceBehavior<SfxManager>.Instance;
            if (sounds == null)
            {
                return false;
            }
            if (sounds != _soundsSeen)
            {
                _soundsSeen = sounds;
                _voicesHere = Playable(sounds, SoundType.NpcMaleProducerInteraction) && Playable(sounds, SoundType.NpcFemaleProducerInteraction);
                _tillHere = Playable(sounds, SoundType.PurchaseSuccess);
            }
            return true;
        }

        private static bool Playable(SfxManager sounds, SoundType type)
        {
            SfxManager.SoundPreset[] presets = sounds.soundPresets;
            if (presets == null)
            {
                return false;
            }
            for (int i = 0; i < presets.Length; i++)
            {
                SfxManager.SoundPreset preset = presets[i];
                if (preset == null || preset.type != type)
                {
                    continue;
                }
                if (preset.ClipSettings == null || preset.ClipSettings.Length == 0)
                {
                    return false;
                }
                for (int c = 0; c < preset.ClipSettings.Length; c++)
                {
                    if (preset.ClipSettings[c] == null || preset.ClipSettings[c].audioClip == null)
                    {
                        return false;
                    }
                }
                return true;
            }
            return false;
        }

        private void SoundMissing(string what)
        {
            if (_soundSaid)
            {
                return;
            }
            _soundSaid = true;
            Ctx.Logger?.Warn("Jobs: vendor: " + what + " was not played: the game's sound manager or its preset is missing; the cart stays silent where it would be heard (said once a shift).");
        }

        // One line a shift at its end: every cue wanted was played.
        private void SoundCheck()
        {
            if (!Meta.Dev.Verbose)
            {
                return;
            }
            bool pass = CartSoundRules.AllPlayed(_cueWanted, _cues) && CartSoundRules.AllPlayed(_remindWanted, _reminds) && CartSoundRules.AllPlayed(_tillWanted, _tills);
            Ctx.Logger?.Info("Jobs: vendor check (cart sounds): order cues " + _cues + " of " + _cueWanted + " customers who reached the counter with the sounds on, reminders "
                             + _reminds + " of " + _remindWanted + ", tills " + _tills + " of " + _tillWanted + " drinks paid for"
                             + (Settings.JobCartSounds ? string.Empty : " (Drinks cart sounds is off)") + ": "
                             + (pass ? "PASS." : "the game's sound manager or a preset was missing for the rest."));
        }
    }
}
