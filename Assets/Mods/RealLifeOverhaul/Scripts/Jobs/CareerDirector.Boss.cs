using System.Collections.Generic;
using System.Globalization;
using System.Text;
using BAModAPI;
using Entities;
using Helpers;
using UI.Guiders;
using UI.Notification;
using UnityEngine;
using Vehicles.VehicleTypes;

namespace RealLifeOverhaul
{
    /// <summary>Careers: the boss and the colleague who stand by the office's board.</summary>
    public sealed partial class CareerDirector
    {
        private const float BossAppears = 75f;      // metres from a depot at which its boss is put out, if unseen
        private const float BossNotNearer = 22f;    // ...and never in view nearer than this
        private const float BossGoes = 100f;        // metres
        private const float BossReach = 2.4f;       // metres

        private StreetActor _boss;
        private int _bossOf = -1;
        private Vector3 _bossAt;
        private StreetActor _mate;         // on a rota day: whoever is on the same shift, waiting at the office
        private int _mateIndex;
        private bool _mateGreeted;
        private bool _greeted;
        private int _noRoomAt = -1;        // a depot whose boss could not be stood anywhere: not asked again this visit

        // The boss of the nearest depot stands by its board while the player is about; nobody is ever put down in view close by.
        private void Presence(Vector3 player, bool onFoot, JobBoards boards)
        {
            int nearest = -1;
            float best = BossGoes * BossGoes;
            for (int e = 0; e < _depot.Length; e++)
            {
                if (_depot[e] < 0 || _depot[e] >= boards.Count)
                {
                    continue;
                }
                float sqr = FlatSqr(boards.Front(_depot[e]) - player);
                if (sqr < best)
                {
                    best = sqr;
                    nearest = e;
                }
            }
            if (_boss != null && (nearest != _bossOf || !_boss.IsValid))
            {
                PutAway();
            }
            if (nearest != _noRoomAt)
            {
                _noRoomAt = -1;
            }
            if (nearest >= 0 && _boss == null && onFoot && nearest != _noRoomAt && best < BossAppears * BossAppears)
            {
                PutBoss(nearest, boards, best);
            }

            // The colleague says hello when the player walks up: a wave at somebody who is there to be waved at.
            if (_mate != null && _mate.IsValid && !_mateGreeted && onFoot && nearest >= 0 && FlatSqr(_mate.Position - player) < 3.2f * 3.2f)
            {
                _mateGreeted = true;
                _mate.Face(player);
                _director.Animations.Play(_mate.Character, JobClip.Thank);
                Colleague(CareerCatalogue.All[nearest], _mateIndex);
            }

            // At the board, or beside the boss: the key speaks to the office.
            Near = -1;
            if (nearest >= 0 && onFoot)
            {
                bool atBoard = boards.Near == _depot[nearest];
                bool atBoss = _boss != null && _boss.IsValid && FlatSqr(_boss.Position - player) < BossReach * BossReach;
                if (atBoard || atBoss)
                {
                    Near = nearest;
                    if (_boss != null && _boss.IsValid && !_greeted)
                    {
                        _greeted = true;
                        _boss.Face(player);
                        _director.Animations.Play(_boss.Character, JobClip.Thank);   // a nod and a wave: there is somebody in front of them
                        CareerLinks.TellMet(CareerCatalogue.All[nearest], -1);
                    }
                }
            }
        }

        private void PutBoss(int employer, JobBoards boards, float distanceSqr)
        {
            int board = _depot[employer];
            Vector3 outward = boards.Outward(board);
            Vector3 along = Vector3.Cross(Vector3.up, outward);
            Vector3 front = boards.Front(board);
            for (int side = -1; side <= 1; side += 2)
            {
                // A step and a half along the wall from the board, clear of it and of the door on its other side.
                if (!CitySpots.Stand(front + along * (side * 1.7f) - outward * 0.2f, 0.45f, out Vector3 spot))
                {
                    continue;
                }
                if (CitySpots.InView(spot) && distanceSqr < BossAppears * BossAppears * 0.36f && FlatSqr(spot - PlayerPosition()) < BossNotNearer * BossNotNearer * 4f)
                {
                    return;   // they would be seen appearing: wait until the player looks away or comes from further off
                }
                Employer who = CareerCatalogue.All[employer];
                StreetActor boss = _cast.Put(spot, false, who.Seed, who.BossFemale);
                if (boss == null)
                {
                    break;
                }
                boss.StandAt(spot, spot + outward);
                if (!boss.Work(employer % 2 == 0 ? "clipboard_write" : "arms_crossed"))
                {
                    boss.Work("arms_crossed");
                }
                _boss = boss;
                _bossOf = employer;
                _bossAt = spot;
                _greeted = false;
                PutMate(employer, who, front - along * (side * 1.9f) + outward * 0.5f, outward);
                _logger?.Info("Careers: " + who.Id + "'s boss is at the office (" + boards.Address(board) + ")" + (_mate != null ? ", and a colleague with them." : "."));
                return;
            }
            _noRoomAt = employer;
            _logger?.Info("Careers: no room to stand " + CareerCatalogue.All[employer].Id + "'s boss by the board; the board itself is the office.");
        }

        // On the player's rota day a colleague waits at the office too, across the board from the boss, with the same face every time.
        private void PutMate(int employer, Employer who, Vector3 place, Vector3 outward)
        {
            int day = TimeHelper.CurrentDay;
            if (!CareerState.Employed || CareerState.EmployerIndex != employer || !CareerState.ShiftOpen(day)
                || !CitySpots.Stand(place, 0.45f, out Vector3 spot) || FlatSqr(spot - _bossAt) < 1.5f * 1.5f)
            {
                return;
            }
            int mate = day % who.Colleagues.Length;
            StreetActor person = _cast.Put(spot, false, who.Seed + 11 * (mate + 1), (mate + who.Seed) % 2 == 0, false);
            if (person == null)
            {
                return;
            }
            person.StandAt(spot, spot + outward);
            person.Work("talk_b");   // listening, a nod, a look up the street: somebody waiting for the shift to start
            _mate = person;
            _mateIndex = mate;
            _mateGreeted = false;
        }

        private static Vector3 PlayerPosition()
        {
            PlayerController player = PlayerHelper.PlayerController;
            return player != null ? player.transform.position : Vector3.zero;
        }

        private void PutAway()
        {
            if (_boss != null)
            {
                _cast.Release(_boss);
                _boss = null;
            }
            if (_mate != null)
            {
                _cast.Release(_mate);
                _mate = null;
            }
            _bossOf = -1;
        }

        private static float FlatSqr(Vector3 offset)
        {
            return offset.x * offset.x + offset.z * offset.z;
        }
    }
}
