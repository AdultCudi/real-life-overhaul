using System;
using System.Collections;
using System.Collections.Generic;
using AI.Customers.CustomerEntries;
using BigAmbitions.Tags;
using Buildings;
using Helpers;
using IngameDebugConsole;
using Streets;
using UI;
using UnityEngine;

namespace RealLifeOverhaul.SelfTest
{
    /// <summary>The step table's world part: its steps, each a nested class (see SelfTestSteps.cs).</summary>
    internal static partial class SelfTestSteps
    {
        /// <summary>A command of the game's own debug console (skiptime, SetMoney, tpb, EnterBuilding, courses, ReceiveMessage, CreateMarketEvent and the rest of its list).</summary>
        internal sealed class ConsoleStep : SelfTestStep
        {
            public override string Name => "console";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the game's commands are a city's");
                    yield break;
                }
                if (!SelfTestWorld.Console(c.Arg("command"), out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                yield return new WaitForSecondsRealtime(c.Number("settle", 1f));
                if (c.Runner.Watch.LogHas("ERROR:", c.Spec.Label, out string refused))
                {
                    c.Fail("the console refused '" + c.Arg("command") + "': " + refused);
                    yield break;
                }
                c.Pass("ran: " + c.Arg("command") + " (the console wrote no refusal); " + SelfTestGame.CityWords());
            }
        }

        /// <summary>The "set_money" step (SelfTestKinds has its words).</summary>
        internal sealed class SetMoneyStep : SelfTestStep
        {
            public override string Name => "set_money";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: money is a game's");
                    yield break;
                }
                float want = c.Number("amount", 0f);
                string again = string.Empty;
                SelfTestWorld.SetMoney(want);
                yield return null;
                yield return new WaitForSecondsRealtime(0.5f);
                float got = SaveGameManager.Current.Money;
                if (System.Math.Abs(got - want) > 1f)
                {
                    // Something else moved the money in the moment after the set (income, a start grant): set again, once, and say how far off it was.
                    again = "; the first set read " + (long)got + " (" + (long)(got - want) + " from the " + (long)want + " asked: other money landed in the same moment) and was set again";
                    SelfTestWorld.SetMoney(want);
                    yield return null;
                    yield return new WaitForSecondsRealtime(0.5f);
                    got = SaveGameManager.Current.Money;
                }
                if (System.Math.Abs(got - want) > 1f)
                {
                    c.Fail("the game's SetMoney left " + (long)got + ", not " + (long)want + " (set twice)" + again);
                    yield break;
                }
                c.Pass("money set by the game's own SetMoney and read back: " + SelfTestGame.CityWords() + again);
            }
        }

        /// <summary>The player put at an address, at a business type's first building, or inside a building, by the game's own teleport commands.</summary>
        internal sealed class TeleportStep : SelfTestStep
        {
            public override string Name => "teleport";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: the player is a city's");
                    yield break;
                }
                string address = c.Arg("address"), business = c.Arg("business");
                if (address == null && business == null)
                {
                    c.Fail("name an address (12 5th Avenue) or a business type");
                    yield break;
                }
                yield return SelfTestGame.Close();
                Vector3 from = SelfTestWorld.PlayerAt();
                if (!SelfTestWorld.Teleport(address, business, c.Flag("inside", false), out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                float until = Time.realtimeSinceStartup + 15f;
                while ((SelfTestWorld.PlayerAt() - from).sqrMagnitude < 1f && !(c.Flag("inside", false) && BuildingManager.IsInsideBuilding))
                {
                    c.Waiting = "the player to arrive at " + (address ?? business);
                    if (Time.realtimeSinceStartup > until)
                    {
                        c.Fail("the player did not move from " + from + " within 15 s of the teleport to " + (address ?? business));
                        yield break;
                    }
                    yield return new WaitForSecondsRealtime(0.5f);
                }
                c.Pass("the player moved from " + from + " to " + SelfTestWorld.PlayerAt() + " (" + (address ?? business) + (c.Flag("inside", false) ? ", inside" : string.Empty) + ")");
            }
        }

        /// <summary>The "under_lamp" step (SelfTestKinds has its words): the player warped, as the game's own teleport does, to the ground by a lit street lamp.</summary>
        internal sealed class UnderLampStep : SelfTestStep
        {
            public override string Name => "under_lamp";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands");
                    yield break;
                }
                string where = c.Arg("where") ?? "inside";
                if (where != "inside" && where != "front" && where != "behind")
                {
                    c.Fail("where is inside, front or behind, not '" + where + "'");
                    yield break;
                }
                yield return SelfTestGame.Close();
                Vector3 from = SelfTestWorld.PlayerAt();
                var lamps = new List<StreetLamp>(4096);
                PerfGame.Lamps(lamps);
                var lit = new List<StreetLamp>(64);
                foreach (StreetLamp lamp in lamps)
                {
                    GameObject glow = PerfGame.KnowsLamps ? PerfGame.GlowOf(lamp) : null;
                    if (glow != null && glow.activeSelf && (lamp.transform.position - from).magnitude <= 150f)
                    {
                        lit.Add(lamp);
                    }
                }
                if (lit.Count == 0)
                {
                    c.Fail(lamps.Count + " street lamps are loaded, but none within 150 m has its glow switched on (is it night?)");
                    yield break;
                }
                lit.Sort((a, b) => (a.transform.position - from).sqrMagnitude.CompareTo((b.transform.position - from).sqrMagnitude));
                int nth = (int)c.Number("nth", 0f);
                if (nth < 0 || nth >= lit.Count)
                {
                    c.Fail("only " + lit.Count + " lit lamps are within 150 m, so nth " + nth + " is none");
                    yield break;
                }
                StreetLamp pick = lit[nth];
                Vector3 foot = pick.transform.position;
                Camera cam = Camera.main;
                Vector3 toCam = cam != null ? cam.transform.position - foot : from - foot;
                toCam.y = 0f;
                toCam = toCam.sqrMagnitude < 0.01f ? Vector3.back : toCam.normalized;
                Vector3 target = where == "front" ? foot + toCam * 2.5f : where == "behind" ? foot - toCam * 2.5f : foot;
                if (!UnityEngine.AI.NavMesh.SamplePosition(target, out UnityEngine.AI.NavMeshHit hit, 3f, UnityEngine.AI.NavMesh.AllAreas))
                {
                    c.Fail("no walkable ground within 3 m of " + target + " by lamp '" + pick.name + "'");
                    yield break;
                }
                if (!GameManager.IsPlayerWalking)
                {
                    c.Fail("the player is not walking: the game's move needs him on foot");
                    yield break;
                }
                yield return InstanceBehavior<GameManager>.Instance.StartCoroutine(GameManager.SetPlayerPosition(hit.position));
                yield return new WaitForSecondsRealtime(0.5f);
                Vector3 now = SelfTestWorld.PlayerAt();
                if ((now - hit.position).magnitude > 1.5f)
                {
                    c.Fail("the player is at " + now + ", not at " + hit.position + " by lamp '" + pick.name + "'");
                    yield break;
                }
                GameObject glowNow = PerfGame.GlowOf(pick);
                c.Pass(where + ": the player is at " + now + ", " + (now - foot).magnitude.ToString("0.0") + " m from lamp '" + pick.name + "' at " + foot
                    + ", glow " + (glowNow != null && glowNow.activeSelf ? "active" : "not active") + ", lamp " + (nth + 1) + " of " + lit.Count + " lit");
            }
        }

        /// <summary>A business of a type started in a building, as the game's start screen does it (the player's money and the course the type asks for are given first).</summary>
        internal sealed class FoundBusinessStep : SelfTestStep
        {
            public override string Name => "found_business";

            public override IEnumerator Run(SelfTestContext c)
            {
                if (!SelfTestGame.CityUp())
                {
                    c.Fail("no city stands: a business is a city's");
                    yield break;
                }
                string words;
                bool done = SelfTestWorld.FoundBusiness(c.Arg("type"), c.Arg("name"), c.Arg("address"), out words);
                if (!done)
                {
                    c.Fail(words);
                    yield break;
                }
                c.Waiting = "the mod's and the game's directors to take the new business up";
                yield return new WaitForSecondsRealtime(c.Number("settle", 2f));
                c.Pass(words);
            }
        }
    }
}
