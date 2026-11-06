using System;
using System.Collections.Generic;
using GleyTrafficSystem;
using RealLifeOverhaul.Economy;
using UnityEngine;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The credit desk and the private lender (EconomyBridges): a lender's claim as one of Justice's civil suits,
        /// whether the player is held, a mugging's injury (Crime cannot stage one against the player, so the consequences
        /// are applied and told), what the camera sees, a free stretch of kerb and a contact's picture.
        /// </summary>
        public static class DebtLinks
        {
            private const float HalfWidth = 1.05f, HalfLength = 2.7f;   // the test car's footprint, as the driving school's

            private static readonly List<Waypoint> Route = new List<Waypoint>(24);

            internal static void Connect()
            {
                EconomyBridges.FileClaim = amount => DebtClaims.File(amount);
                EconomyBridges.ClaimOpen = number => DebtClaims.Open(number);
                EconomyBridges.WithdrawClaim = number => DebtClaims.Withdraw(number);
                EconomyBridges.Held = Held;
                EconomyBridges.Assault = Assault;
                EconomyBridges.InView = point => StreetSpots.IsInView(point);
                EconomyBridges.KerbSpot = KerbSpot;
                EconomyBridges.ContactIcon = Icon;
            }

            internal static void Disconnect()
            {
                // EconomyLinks.Disconnect puts every bridge of the economy back.
            }

            private static bool Held()
            {
                try
                {
                    return !LifeEvents.IsSafeForEvent() || Scenes.PlayerHeld || Scenes.AnyLargeSceneActive || PoliceAttention.IsBusy || Justice.InPrison || TransitDirector.Riding;
                }
                catch (Exception e)
                {
                    Fault("DebtLinks.Held", e);
                    return true;   // not known: nothing is done
                }
            }

            private static bool Assault(int hours, float energy, string notificationKey)
            {
                try
                {
                    LifeEvents life = LifeEvents.Instance;
                    if (life == null || SaveGameManager.Current == null)
                    {
                        return false;
                    }
                    // With Permadeath on, the first beating is a near miss and a later one may end the life.
                    Permadeath.Verdict fate = Permadeath.Mortality.Judge(Permadeath.DeathCause.Assault);
                    if (fate == Permadeath.Verdict.Death && Permadeath.Mortality.Die(Permadeath.DeathCause.Assault))
                    {
                        return true;
                    }
                    life.Injure(hours, SaveGameManager.Current.Energy > 35f ? energy : 0f, notificationKey);
                    if (fate == Permadeath.Verdict.NearMiss)
                    {
                        Permadeath.Mortality.NearMiss(Permadeath.DeathCause.Assault);
                    }
                    return true;
                }
                catch (Exception e)
                {
                    Fault("DebtLinks.Assault", e);
                    return false;
                }
            }

            private static void Icon(string id, Color colour)
            {
                try
                {
                    ContactPictures.Add(id);   // the catalogue's picture; the colour is not used
                }
                catch (Exception e)
                {
                    Fault("DebtLinks.ContactIcon", e);
                }
            }

            // A stretch of lane near a point with nothing in it, the kerb side first, as the driving school's TestCar finds one.
            private static bool KerbSpot(Vector3 near, out Vector3 spot, out Quaternion heading)
            {
                spot = default(Vector3);
                heading = Quaternion.identity;
                try
                {
                    if (!TrafficTools.Street)
                    {
                        return false;
                    }
                    Waypoint anchor = TrafficTools.ClosestWaypointAlong(near, Vector3.zero, 45f);
                    if (anchor == null)
                    {
                        return false;
                    }
                    TrafficTools.WalkForward(anchor, 70f, 20, Route);
                    for (int i = 0; i + 1 < Route.Count; i++)
                    {
                        Waypoint point = Route[i];
                        if (point == null || point.enter || point.exit || point.temporaryDisabled)
                        {
                            continue;
                        }
                        Vector3 runs = Route[i + 1].position - point.position;
                        runs.y = 0f;
                        if (runs.sqrMagnitude < 0.25f)
                        {
                            continue;
                        }
                        runs.Normalize();
                        Quaternion along = Quaternion.LookRotation(runs);
                        Vector3 kerb = Vector3.Cross(Vector3.up, runs);
                        for (int side = 0; side < 2; side++)
                        {
                            Vector3 at = point.position + kerb * (side == 0 ? 0.8f : 0f);
                            float ground;
                            if (!StreetSpots.Ground(at, out ground) || Mathf.Abs(ground - point.position.y) > 0.6f)
                            {
                                continue;
                            }
                            at.y = ground + 0.05f;
                            Vector3 flat = at - near;
                            flat.y = 0f;
                            if (flat.sqrMagnitude > 25f && SceneParking.IsFree(at, along, HalfWidth, HalfLength, 0.5f))
                            {
                                spot = at;
                                heading = along;
                                return true;
                            }
                        }
                    }
                    return false;
                }
                catch (Exception e)
                {
                    Fault("DebtLinks.KerbSpot", e);
                    return false;
                }
                finally
                {
                    Route.Clear();   // no waypoint of the city kept past the look, a fault or not
                }
            }
        }
    }
}
