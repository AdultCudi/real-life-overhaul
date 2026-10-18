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
    /// <summary>The campaign's posters and boards, and the polling place's sign.</summary>
    public sealed partial class CampaignWorld
    {
        private void Dress(float now, Vector3 player, bool campaigning)
        {
            // What is far behind, or (once the campaign is over) not being looked at, comes down.
            for (int i = _posters.Count - 1; i >= 0; i--)
            {
                Transform poster = _posters[i];
                if (poster == null || CivicCity.Flat(poster.position - player) > TooFar * TooFar || (!campaigning && !CitySpots.InView(poster.position)))
                {
                    _props.Return(poster);
                    _posters.RemoveAt(i);
                }
            }
            for (int i = _boards.Count - 1; i >= 0; i--)
            {
                Transform board = _boards[i];
                if (board == null || CivicCity.Flat(board.position - player) > TooFar * TooFar || (!campaigning && !CitySpots.InView(board.position)))
                {
                    _props.Return(board);
                    _boards.RemoveAt(i);
                }
            }
            if (!campaigning || now < _nextDressing)
            {
                return;
            }
            _nextDressing = now + 4f;
            if (_posters.Count < MaxPosters)
            {
                TryPoster(player, true, 25f, 90f);
            }
            else if (_boards.Count < MaxBoards)
            {
                TryBoard(player, true, 25f, 90f);
            }
        }

        // A candidate, more often one who is ahead.
        private int DrawCandidate()
        {
            GovState state = _government.State;
            float own = 0f;
            try
            {
                own = OwnShare != null ? Mathf.Clamp01(OwnShare()) : 0f;
            }
            catch (Exception e)
            {
                CivicDirector.Logger?.Error(e);
            }
            if (own > 0f && Random.value < own)
            {
                return Colour();   // the player's own, in the council race
            }
            float roll = Random.value, sum = 0f;
            for (int c = 0; c < state.CandidateCount; c++)
            {
                sum += state.Polls[c];
                if (roll < sum)
                {
                    return c;
                }
            }
            return Mathf.Max(0, state.CandidateCount - 1);
        }

        private void TryPoster(Vector3 player, bool unseen, float near, float far)
        {
            if (!CitySpots.PavementRing(player, near, far, unseen, out Vector3 spot) || !CitySpots.Shopfront(spot, 0.9f, 0.35f, out Vector3 stand, out Vector3 outward))
            {
                return;
            }
            // The wall exactly, at the poster's height: the middle, top, bottom and both edges must be one flat surface.
            if (!CivicSpots.Wall(stand, -outward, 1.55f, 1.8f, out RaycastHit middle) || Vector3.Dot(middle.normal, outward) < 0.97f)
            {
                return;
            }
            Vector3 across = Vector3.Cross(Vector3.up, outward) * 0.27f;
            if (!Agrees(stand, outward, 1.95f, middle.distance) || !Agrees(stand, outward, 1.15f, middle.distance)
                || !Agrees(stand + across, outward, 1.55f, middle.distance) || !Agrees(stand - across, outward, 1.55f, middle.distance))
            {
                return;
            }
            Vector3 at = middle.point + middle.normal * 0.012f;
            for (int i = 0; i < _posters.Count; i++)
            {
                if (_posters[i] != null && (_posters[i].position - at).sqrMagnitude < PosterGap * PosterGap)
                {
                    return;
                }
            }
            if (unseen && CitySpots.InView(at))
            {
                return;
            }
            Transform poster = _props.Stand(PosterIds[DrawCandidate() % PosterIds.Length], at, middle.normal);
            if (poster != null)
            {
                _posters.Add(poster);
            }
        }

        private static bool Agrees(Vector3 from, Vector3 outward, float height, float distance)
        {
            return CivicSpots.Wall(from, -outward, height, 1.8f, out RaycastHit hit) && Mathf.Abs(hit.distance - distance) < 0.03f && Vector3.Dot(hit.normal, outward) > 0.97f;
        }

        private void TryBoard(Vector3 player, bool unseen, float near, float far)
        {
            // At the foot of a wall, out of the way of people walking.
            if (!CitySpots.PavementRing(player, near, far, unseen, out Vector3 spot) || !CitySpots.Shopfront(spot, 0.55f, 0.45f, out Vector3 stand, out Vector3 outward)
                || (unseen && CitySpots.InView(stand)) || !CitySpots.OnPavement(stand + outward * 1.2f, out _))
            {
                return;
            }
            for (int i = 0; i < _boards.Count; i++)
            {
                if (_boards[i] != null && (_boards[i].position - stand).sqrMagnitude < PosterGap * PosterGap)
                {
                    return;
                }
            }
            Transform board = _props.Stand(BoardIds[DrawCandidate() % BoardIds.Length], stand, Vector3.Cross(Vector3.up, outward));
            if (board != null)
            {
                _boards.Add(board);
            }
        }

        private static readonly string[] PosterIds = { "campaign_poster_0", "campaign_poster_1", "campaign_poster_2", "campaign_poster_3" };
        private static readonly string[] BoardIds = { "campaign_board_0", "campaign_board_1", "campaign_board_2", "campaign_board_3" };

        private void Polling(float now, Vector3 player, int day, bool campaigning)
        {
            bool open = campaigning && day == _government.State.ElectionDay;
            if (_pollSign != null && (!open || CivicCity.Flat(_pollStand - player) > TooFar * TooFar) && (!CitySpots.InView(_pollStand) || !open))
            {
                _props.Return(_pollSign);
                _pollSign = null;
            }
            if (!open || _pollSign != null || now < _nextPollLook)
            {
                return;
            }
            _nextPollLook = now + 5f;
            District district = _city.NearestPolling(player, out float distance);
            if (district == null || distance > 130f)
            {
                return;
            }
            // Beside the door, a few steps along the wall, where the sign has room and the voter a place to stand in front of it.
            if (!CitySpots.Pavement(district.Door, 6f, out Vector3 front) || !CitySpots.Along(front, out Vector3 along))
            {
                return;
            }
            for (int side = -1; side <= 1; side += 2)
            {
                if (CitySpots.Stand(front + along * (side * 2.8f), 0.5f, out Vector3 at) && CitySpots.Stand(at - along * (side * 1.1f), 0.4f, out Vector3 stand))
                {
                    _pollSign = _props.Stand("polling_sign", at, -along * side);
                    if (_pollSign != null)
                    {
                        _pollStand = stand;
                        CivicDirector.Logger?.Info("Civic: the polling place of " + district.Id + " is open at " + district.PollingAddress + ".");
                    }
                    return;
                }
            }
        }
    }
}
