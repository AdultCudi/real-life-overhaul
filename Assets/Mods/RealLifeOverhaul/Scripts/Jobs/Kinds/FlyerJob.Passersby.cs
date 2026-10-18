using System.Collections.Generic;
using Characters.EmojiSystem;
using Helpers;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Flyers: who is in reach to be offered one, and whether the street is busy.</summary>
    public sealed partial class FlyerJob
    {
        // The nearest person on foot not asked lately, walkers and those standing or sitting; someone in front counts as nearer.
        private void Find(float reach, out BaseHuman best, out Pedestrian bestWalker)
        {
            best = null;
            bestWalker = null;
            Vector3 player = Ctx.PlayerPosition;
            Vector3 facing = Ctx.Player.transform.forward;
            float bestScore = float.MaxValue;
            float reachSqr = reach * reach;

            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers != null)
            {
                for (int i = 0; i < walkers.Count; i++)
                {
                    Pedestrian walker = walkers[i];
                    if (walker == null || !walker.isActiveAndEnabled || walker.tpc == null)
                    {
                        continue;
                    }
                    float score = Score(walker.tpc.transform.position - player, facing, reachSqr);
                    if (score < bestScore && Eligible(walker.tpc))
                    {
                        bestScore = score;
                        best = walker.tpc;
                        bestWalker = walker;
                    }
                }
            }
            List<BaseHuman> standing = StreetPeople.Standing(Ctx.Logger);
            for (int i = 0; i < standing.Count; i++)
            {
                BaseHuman person = standing[i];
                if (person == null)
                {
                    continue;
                }
                float score = Score(person.transform.position - player, facing, reachSqr);
                if (score < bestScore && Eligible(person))
                {
                    bestScore = score;
                    best = person;
                    bestWalker = null;
                }
            }
        }

        // Squared distance on the flat, shortened for someone in front of the player; float.MaxValue out of reach.
        private static float Score(Vector3 offset, Vector3 facing, float reachSqr)
        {
            if (offset.y > 1.5f || offset.y < -1.5f)
            {
                return float.MaxValue;
            }
            float sqr = offset.x * offset.x + offset.z * offset.z;
            if (sqr >= reachSqr)
            {
                return float.MaxValue;
            }
            float ahead = offset.x * facing.x + offset.z * facing.z;   // metres in front
            return ahead > 0f && ahead * ahead > sqr * 0.5f ? sqr * 0.35f : ahead > 0f ? sqr * 0.7f : sqr;
        }

        // Out on the street, drawn, on their feet or their seat (not lying in the road), and not asked a moment ago.
        private bool Eligible(BaseHuman person)
        {
            if (person == null || !person.gameObject.activeInHierarchy || AskedLately(person.GetInstanceID()))
            {
                return false;
            }
            return !(person is ThirdPersonCharacter body) || (body.visible && !RagdollSystem.Holds(body));
        }

        private bool AskedLately(int id)
        {
            int index = _asked.IndexOf(id);
            return index >= 0 && Time.time - _askedAt[index] < AskAgain;
        }

        private void Remember(int id, float now)
        {
            int index = _asked.IndexOf(id);
            if (index >= 0)
            {
                _askedAt[index] = now;
            }
            else
            {
                _asked.Add(id);
                _askedAt.Add(now);
            }
        }

        // A busy street: enough people about, counted at the moment a flyer changes hands.
        private bool Busy()
        {
            CityManager city = InstanceBehavior<CityManager>.Instance;
            List<Pedestrian> walkers = city != null && city.pedestrianSpawner != null ? city.pedestrianSpawner.activePedestrians : null;
            if (walkers == null)
            {
                return false;
            }
            Vector3 player = Ctx.PlayerPosition;
            int people = 0;
            for (int i = 0; i < walkers.Count && people < BusyPeople; i++)
            {
                if (walkers[i] != null && (walkers[i].transform.position - player).sqrMagnitude < BusyWithin * BusyWithin)
                {
                    people++;
                }
            }
            return people >= BusyPeople;
        }
    }
}
