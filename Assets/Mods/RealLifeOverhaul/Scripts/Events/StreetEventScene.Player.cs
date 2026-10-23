using System.Collections.Generic;
using Extensions;
using Helpers;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul.Events
{
    /// <summary>What the player can do at a street event: buy a snack, sign a petition, cheer, watch a take.</summary>
    internal sealed partial class StreetEventScene
    {
        public string Prompt(Vector3 player)
        {
            if (_phase != Phase.Running || PlayerHelper.IsUsingVehicle)
            {
                return null;
            }
            switch (_kind)
            {
                case EventKind.Market:
                case EventKind.FarmersMarket:
                    return StallNear(player) >= 0 ? "reallife_event_prompt_snack" : null;
                case EventKind.Protest:
                    return !_thanked && NearJob(player, Job.Organiser, 2.6f) ? "reallife_event_prompt_petition" : null;
                case EventKind.Parade:
                case EventKind.FunRun:
                    return Time.time >= _nextCheer && Beside(player, 7f) && (Has(Job.Runner) > 0 || Has(Job.Marcher) > 0) ? "reallife_event_prompt_cheer" : null;
                case EventKind.FilmShoot:
                    return !_thanked && _taking && Beside(player, 9f) ? "reallife_event_prompt_watch" : null;
                default:
                    return null;
            }
        }

        public void Interact(float now, Vector3 player)
        {
            string prompt = Prompt(player);
            if (prompt == null)
            {
                return;
            }
            switch (_kind)
            {
                case EventKind.Market:
                case EventKind.FarmersMarket:
                    Buy(now, StallNear(player));
                    break;
                case EventKind.Protest:
                    Thank(Random.value < 0.5f ? "reallife_event_petition_signed_a" : "reallife_event_petition_signed_b", ModState.Helped);
                    break;
                case EventKind.Parade:
                case EventKind.FunRun:
                    _nextCheer = now + 25f;
                    Wave(player);
                    if (!_thanked)
                    {
                        Thank(_kind == EventKind.FunRun ? "reallife_event_cheer_run" : "reallife_event_cheer_parade", ModState.MoodUp);
                    }
                    else
                    {
                        Notifications.Show(NotificationType.Info, "reallife_event_cheer_again", null, 4f);
                    }
                    break;
                case EventKind.FilmShoot:
                    Thank("reallife_event_film_watched", ModState.MoodUp);
                    break;
            }
        }

        public bool Report(float now)
        {
            return false;   // nothing here is an emergency
        }

        private void Thank(string key, string mood)
        {
            _thanked = true;
            ModState.AddModifier(mood);
            Notifications.Show(NotificationType.Success, key, null, 7f);
        }

        private void Buy(float now, int slot)
        {
            if (slot < 0)
            {
                return;
            }
            var save = SaveGameManager.Current;
            if (save == null || save.Money < SnackPrice)
            {
                Notifications.Show(NotificationType.Warning, "reallife_event_snack_broke",
                    new Dictionary<string, string>(1) { { "price", ((float)SnackPrice).ToCurrencyFormat() } }, 5f);
                return;
            }
            try
            {
                GameManager.ChangeMoneySafe(-SnackPrice, new TransactionInfo("reallife_transaction_snack"), null, null, force: true);
                EnergyHelper.GenerateEnergy(SnackEnergy);
            }
            catch (System.Exception e)
            {
                EventDirector.Logger?.Error(e);
                return;
            }
            _stalls[slot].ServeAt = now;
            Crime.ShowAmount(NotificationType.Success, Random.value < 0.5f ? "reallife_event_snack_bought_a" : "reallife_event_snack_bought_b", SnackPrice, 5f);
            if (!_thanked)
            {
                _thanked = true;
                ModState.AddModifier(ModState.MoodUp);
            }
        }

        // The stall whose customer side the player stands at, with its vendor in and not serving; -1 if none.
        private int StallNear(Vector3 player)
        {
            for (int slot = 0; slot < _stallCount; slot++)
            {
                if (CitySpots.Flat(player - _stalls[slot].Customer) < 1.9f * 1.9f && VendorAt(slot) && _stalls[slot].ServeAt <= 0f)
                {
                    return slot;
                }
            }
            return -1;
        }

        private bool NearJob(Vector3 player, Job job, float within)
        {
            for (int i = 0; i < _count; i++)
            {
                if (_cast[i].Job == job && _cast[i].Step != 0 && CitySpots.Flat(player - _cast[i].Who.Position) < within * within)
                {
                    return true;
                }
            }
            return false;
        }

        // Within so many metres of the closed stretch's sides.
        private bool Beside(Vector3 player, float within)
        {
            Vector3 offset = player - _centre;
            return Mathf.Abs(Vector3.Dot(offset, _along)) < _half + 2f && Mathf.Abs(Vector3.Dot(offset, _right)) < _width + within;
        }

        // The nearest runner or marcher lifts a hand to whoever cheered.
        private void Wave(Vector3 player)
        {
            int best = -1;
            float nearest = 14f * 14f;
            for (int i = 0; i < _count; i++)
            {
                float sqr = CitySpots.Flat(_cast[i].Who.Position - player);
                if ((_cast[i].Job == Job.Runner || _cast[i].Job == Job.Marcher) && _cast[i].Step != 0 && sqr < nearest)
                {
                    nearest = sqr;
                    best = i;
                }
            }
            if (best >= 0)
            {
                // A marcher's right arm has the banner: they wave with the left; a runner with whatever the clip uses.
                _cast[best].Who.WorkOver("wave_hello", _director.People.Props.Shelf, 0.2f, ClipEnd.Return, _cast[best].Job == Job.Marcher ? BodyMask.LeftArm : BodyMask.Upper);
            }
        }
    }
}
