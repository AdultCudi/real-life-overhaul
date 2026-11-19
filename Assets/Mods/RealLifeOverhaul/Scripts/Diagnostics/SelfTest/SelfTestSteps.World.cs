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
                c.Pass("ran: " + c.Arg("command") + "; " + SelfTestGame.CityWords());
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
                SelfTestWorld.SetMoney(c.Number("amount", 0f));
                yield return new WaitForSecondsRealtime(0.5f);
                c.Pass("money set by the game's own SetMoney: " + SelfTestGame.CityWords());
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
                if (!SelfTestWorld.Teleport(address, business, c.Flag("inside", false), out string why))
                {
                    c.Fail(why);
                    yield break;
                }
                c.Waiting = "the player to arrive";
                yield return new WaitForSecondsRealtime(3f);
                c.Pass("the player was sent to " + (address ?? business) + (c.Flag("inside", false) ? " (inside)" : string.Empty));
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
