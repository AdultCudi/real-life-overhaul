using System;
using System.Collections.Generic;
using Characters.EmojiSystem;
using Helpers;
using Streets.Pedestrians;
using UI.Notification;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>
    /// An afternoon's canvassing for a candidate, the player's own short job: they hold a pitch (80 m round where they began)
    /// and offer a leaflet, a key press each, to the game's own pedestrians walking by, each asked once; some take one.
    /// Thirty leaflets, four real minutes or six in the evening, whichever comes first; it ends on every path out.
    /// </summary>
    public sealed class VolunteerShift
    {
        private const int Bundle = 30;   // leaflets
        private const float Seconds = 240f;   // real seconds
        private const float Pitch = 80f;   // metres
        private const float Reach = 3.2f;   // metres
        private const float AskAgain = 90f;   // seconds before a pedestrian may be asked again: the game reuses a pedestrian's object

        private readonly CivicGovernment _government;
        private readonly MonoBehaviour _host;
        private readonly List<int> _asked = new List<int>(48);
        private readonly List<float> _askedAt = new List<float>(48);
        private Vector3 _centre;
        private float _endsAt;
        private int _taken;
        private Pedestrian _near;
        private string _prompt, _status;
        private int _statusTaken = -1, _statusSeconds = -1;

        public VolunteerShift(CivicGovernment government, MonoBehaviour host)
        {
            _government = government;
            _host = host;
        }

        public bool Active { get; private set; }
        public int For { get; private set; } = -1;

        /// <summary>The line to show under the status, or null: the key that offers a leaflet to whoever is in reach.</summary>
        public string Prompt => Active && _near != null ? _prompt : null;

        public string Status => Active ? _status : null;

        /// <summary>Starts a shift where the player stands. Returns null, or the locale key of why not.</summary>
        public string Begin(int candidate, int hour)
        {
            if (Active)
            {
                return "reallife_civic_volunteer_already";
            }
            if (!_government.Campaigning || candidate < 0 || candidate >= _government.State.CandidateCount)
            {
                return "reallife_civic_no_campaign";
            }
            if (_government.State.Volunteered >= 0)
            {
                return "reallife_civic_volunteer_done_before";
            }
            if (hour < 10 || hour >= 18)
            {
                return "reallife_civic_volunteer_hours";
            }
            if (PlayerHelper.PlayerController == null || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || JobDirector.OnShift)
            {
                return "reallife_civic_volunteer_street";
            }
            Active = true;
            For = candidate;
            _centre = PlayerHelper.GetPosition();
            _endsAt = Time.time + Seconds;
            _taken = 0;
            _near = null;
            _asked.Clear();
            _askedAt.Clear();
            _prompt = CivicText.T("reallife_civic_volunteer_prompt", "key", Keys.Label(KeyAction.Interact));
            _statusTaken = -1;
            _statusSeconds = -1;
            return null;
        }

        /// <summary>Five times a second while a shift is on.</summary>
        public void Tick(float now, int hour, bool interact)
        {
            if (!Active)
            {
                return;
            }
            PlayerController player = PlayerHelper.PlayerController;
            if (player == null || BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || !_government.Campaigning
                || CivicCity.Flat(player.transform.position - _centre) > Pitch * Pitch)
            {
                End("reallife_civic_volunteer_left");
                return;
            }
            if (_taken >= Bundle)
            {
                End("reallife_civic_volunteer_done");
                return;
            }
            if (now >= _endsAt || hour >= 18)
            {
                End("reallife_civic_volunteer_time");
                return;
            }
            _near = Find(player.transform.position, now);
            if (interact && _near != null)
            {
                Offer(_near, now);
                _near = null;
            }
            int seconds = Mathf.CeilToInt(_endsAt - now);
            if (_taken != _statusTaken || seconds != _statusSeconds)
            {
                _statusTaken = _taken;
                _statusSeconds = seconds;
                _status = CivicText.T("reallife_civic_volunteer_status", "taken", _taken.ToString(), "bundle", Bundle.ToString(), "seconds", seconds.ToString());
            }
        }

        /// <summary>The shift ends now, whatever was done counts. Safe when none is on.</summary>
        public void Stop()
        {
            if (Active)
            {
                End("reallife_civic_volunteer_left");
            }
        }

        private void End(string key)
        {
            Active = false;
            _near = null;
            int candidate = For;
            For = -1;
            if (_taken > 0 && candidate >= 0 && candidate < _government.State.CandidateCount)
            {
                _government.Volunteered(candidate, _taken);
                ModState.AddModifier(ModState.MoodUp);
                CivicText.Toast(NotificationType.Success, key, "taken", _taken.ToString(), "name", CivicText.Name(_government.State.Candidates[candidate]), 12f);
            }
            else
            {
                CivicText.Toast(NotificationType.Info, "reallife_civic_volunteer_nothing", 9f);
            }
        }

        private void Offer(Pedestrian walker, float now)
        {
            _asked.Add(walker.GetInstanceID());
            _askedAt.Add(now);
            // More people take one from somebody whose candidate speaks to what the city is worried about.
            Candidate candidate = _government.State.Candidates[For];
            float odds = 0.5f + (candidate.Lead == _government.TopIssue() ? 0.15f : 0f) + _government.State.Polls[For] * 0.2f;
            bool took = Random.value < odds;
            if (took)
            {
                _taken++;
            }
            try
            {
                CharacterEmojiName emoji = took ? CharacterEmojiName.CommonGreenHappy : Random.value < 0.3f ? CharacterEmojiName.CommonRedAngry : CharacterEmojiName.CommonYellowNeutral;
                if (walker.tpc != null && walker.tpc.head != null && _host != null && _host.isActiveAndEnabled && GameManager.GetMainCamera() != null
                    && CharacterEmojiSystem.GetCharacterEmojiByName(emoji) != null)
                {
                    _host.StartCoroutine(CharacterEmojiSystem.ShowEmoji(walker.tpc.head, emoji, false, 1.6f));
                }
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
        }

        // The nearest of the game's pedestrians within reach who has not been asked lately.
        private Pedestrian Find(Vector3 player, float now)
        {
            for (int i = _asked.Count - 1; i >= 0; i--)
            {
                if (now - _askedAt[i] > AskAgain)
                {
                    _asked.RemoveAt(i);
                    _askedAt.RemoveAt(i);
                }
            }
            var city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return null;
            }
            Pedestrian best = null;
            float bestSqr = Reach * Reach;
            for (int i = 0; i < walkers.Count; i++)
            {
                Pedestrian walker = walkers[i];
                if (walker == null || !walker.isActiveAndEnabled)
                {
                    continue;
                }
                Vector3 offset = walker.transform.position - player;
                float sqr = CivicCity.Flat(offset);
                if (sqr < bestSqr && Mathf.Abs(offset.y) < 2f && !_asked.Contains(walker.GetInstanceID()))
                {
                    bestSqr = sqr;
                    best = walker;
                }
            }
            return best;
        }
    }
}
