using System;
using System.Collections.Generic;
using System.Globalization;
using UI.Notification;

namespace RealLifeOverhaul.Economy
{
    /// <summary>The rivals' holdings as the property business keeps them (RivalHoldingsRules): on the day event and at a start, never per frame.</summary>
    public sealed partial class EstateDirector
    {
        private bool _holdingsDealt;     // this game was dealt by the plan in its first days: it goes on gently
        private bool _holdingsBroken;    // a fault in this part: off for the session, the rest of the property business runs
        // the buildings the rivals' holdings leave alone, made again at each pass
        private readonly HashSet<string> _holdingsKept = new HashSet<string>(StringComparer.Ordinal);

        // The places the holdings must not touch: the buildings the agency's blocks are tied to (offered or held) and every rival's head office, the latter asked of the rivals seen
        // around the city. Made at the pass or the test, never per frame.
        private HashSet<string> KeptBuildings()
        {
            _holdingsKept.Clear();
            List<ManagedBlock> blocks = _state != null && _state.Agency != null ? _state.Agency.Book.Blocks : null;
            for (int i = 0; blocks != null && i < blocks.Count; i++)
            {
                if (blocks[i].Stage != (int)BlockStage.Lost && !string.IsNullOrEmpty(blocks[i].Place))
                {
                    _holdingsKept.Add(blocks[i].Place);
                }
            }
            EstateBridges.HeadOffices?.Invoke(_holdingsKept);
            return _holdingsKept;
        }

        // The game's rivals' buildings brought towards their place in the Rivals app's ranking, as far as the option and the day allow.
        // Rests in a Going Public session (the rivals there are the host's); a fault stops only this part.
        private void HoldingsDay(int day)
        {
            if (_holdingsBroken || GoingPublic.InSession)
            {
                return;
            }
            RivalHoldingsPace pace = RivalHoldingsRules.Pace(Settings.RivalsHoldings, day, _holdingsDealt);
            if (pace == RivalHoldingsPace.None)
            {
                return;
            }
            try
            {
                float started = UnityEngine.Time.realtimeSinceStartup;
                if (!EstateGame.HoldRivals(day, pace, KeptBuildings(), out HoldingsReport r))
                {
                    return;
                }
                _holdingsDealt |= pace == RivalHoldingsPace.AtOnce;
                if (r.Placed > 0 || r.Sold > 0 || r.Bought > 0)
                {
                    string did = pace == RivalHoldingsPace.AtOnce ? r.Placed + " dealt" : r.Sold + " put up for sale and " + r.Bought + " bought";
                    Logger?.Info("Estate: the rivals' buildings by their place in the ranking, day " + day + ": " + did + " among " + r.Rivals + " rivals; "
                                 + r.DistanceBefore + " buildings from their shares before, " + r.DistanceAfter + " after.");
                }
                if (Verbose)
                {
                    Logger?.Info("Estate check (rivals' buildings, day " + day + "): the " + r.Rivals + " rivals hold " + r.Buildings + " building(s) with the market's; first place holds "
                                 + r.FirstHeld
                                 + " of " + r.FirstTarget + ", last " + r.LastHeld + " of " + r.LastTarget + "; " + r.DistanceBefore + " to " + r.DistanceAfter + " from their shares; "
                                 + ((UnityEngine.Time.realtimeSinceStartup - started) * 1000f).ToString("0.00", CultureInfo.InvariantCulture) + " ms: "
                                 + (r.DistanceAfter <= r.DistanceBefore ? "PASS" : "LOOK"));
                }
            }
            catch (Exception e)
            {
                _holdingsBroken = true;
                Logger?.Error(e);
                Logger?.Warn("Estate: the rivals' buildings are left as they are for this session after a fault.");
            }
        }

        /// <summary>
        /// "Test: the rivals' buildings": one gentle day's step now whatever the option says, and a notice of what the first and the last rival
        /// hold against their shares. Puts a few buildings of the rivals up for sale and lets rivals buy others, as the day does; never one of the player's, a rival's head office or a block's building.
        /// </summary>
        public void TestHoldings()
        {
            const string Test = "The rivals' buildings";
            if (!TestReady(Test, EstatePart.Property))
            {
                return;
            }
            if (GoingPublic.InSession || !EstateGame.HoldRivals(Save.Day, RivalHoldingsPace.Gentle, KeptBuildings(), out HoldingsReport r))
            {
                TestRefusals.TellKey(Logger, Test, "reallife_estate_test_rivals_off");
                return;
            }
            Logger?.Warn("Estate test: the rivals' buildings: " + r.Sold + " put up for sale, " + r.Bought + " bought, first place " + r.FirstHeld + " of " + r.FirstTarget + ", last "
                         + r.LastHeld + " of " + r.LastTarget + ".");
            Notifications.Show(NotificationType.Info, "reallife_estate_test_rivals",
                Data("sold", r.Sold.ToString(), "bought", r.Bought.ToString(), "held", r.FirstHeld + " / " + r.LastHeld), 9f, null, null, true, false);
        }
    }
}
