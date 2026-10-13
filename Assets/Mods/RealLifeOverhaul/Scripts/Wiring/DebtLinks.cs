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
        /// The credit desk with the rest of the mod (Economy/EconomyBridges): what a lender, a collector and the
        /// private lender need from features the economy never calls itself.
        ///
        ///   FileClaim      a lender's civil claim for a debt becomes one of Justice's own civil suits
        ///   ClaimOpen      (Justice/DebtClaims): answered in the Legal file, judged by Justice, collected as its debts are;
        ///   WithdrawClaim  a judgment shows on the credit rating through <see cref="EconomyLinks"/>, as every civil judgment does
        ///   Held           the one question the private lender asks before anything happens in the street: the game's own
        ///                  dialogs, time skips, hospital and pause (LifeEvents), a panel or a test of the mod's (Scenes),
        ///                  a large scene in the street, the police, prison, a bus ride
        ///   Assault        the injury the mod's muggings give (LifeEvents.Injure), never enough to make a tired player faint.
        ///                  Crime has no public way to stage a scene against the player outside its own deck and its test
        ///                  button, so nobody walks up: the consequences are applied and said
        ///   InView         whether the camera looks at a point from near (People/StreetSpots), so a vehicle is never taken in sight
        ///   KerbSpot       a free stretch of kerb for the test tools' car, found the way the driving school finds one
        ///   ContactIcon    a contact's picture, from the game's own assets, as every contact of the mod's (Phone/Contacts/ContactPictures)
        ///
        /// A business damaged by the private lender is a repair bill and nothing else: the business-pressure feature
        /// has no public way to raise an incident from outside (its issues are dealt from its own deck).
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
                // EconomyLinks.Disconnect puts every bridge of the economy back to its default.
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
                    // With Permadeath on, the first beating is a near miss and a later one may end the life (Permadeath/PermadeathRules).
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
                    ContactPictures.Add(id);   // the catalogue's picture (Phone/Contacts): the colour asked for is the old disc's, not used
                }
                catch (Exception e)
                {
                    Fault("DebtLinks.ContactIcon", e);
                }
            }

            // A stretch of lane near a point with nothing standing in it: the kerb side first, then the lane itself (Licences/Driving/TestCar does the same).
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
                                Route.Clear();
                                return true;
                            }
                        }
                    }
                    Route.Clear();
                    return false;
                }
                catch (Exception e)
                {
                    Fault("DebtLinks.KerbSpot", e);
                    return false;
                }
            }
        }
    }
}
