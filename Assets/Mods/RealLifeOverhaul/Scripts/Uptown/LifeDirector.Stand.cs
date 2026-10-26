using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.DistrictSave;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>
    /// A building handed to the district stands at once (docs/features/uptown.md, "A building handed over stands at once"; the
    /// owner, 26 Oct 2026: "a finished building must stand the moment it is handed over, as the scaffolding comes down, not
    /// after a reload"). The construction game's finished buildings used to be recorded in the district's line and stand
    /// from the next load, with a look of the design over the site's ground in between (which stood down at a site whose
    /// ground the design did not fit). Now the record is made as before and the building is made in play by the code that
    /// makes it at a load (<see cref="StandRules.Spec"/>, the copy engine's Make, then Enter, Cut, the fillers, the
    /// first load's setting up and the game's lists of buildings): when none of its ground is in the camera's picture and
    /// the player is not on it, else at the next black screen (the game's fade at a door, the loading screen) or the moment
    /// nobody sees it, and the site's props and the look over it come down in the same moment (DistrictBuildings.GroundGiven).
    /// The record is marked set up, so the next load stands the same building from the same record and sets nothing up twice.
    /// Not in a Going Public session (its buildings stand from the next load there). What only a load makes is said in
    /// <see cref="MadeGame.JoinCity"/>. COST: nothing per frame; once a second a box against the camera's planes while a
    /// building waits; one building made, a millisecond or three, once.
    /// </summary>
    public sealed partial class LifeDirector
    {
        private sealed class StandWait
        {
            public string Lot;
            public float X1, Z1, X2, Z2;   // its ground on the world's axes
        }

        private static readonly List<StandWait> Waiting = new List<StandWait>(4);
        private static readonly List<FrontFill> StandFills = new List<FrontFill>(8);
        private static Func<bool> _atBlack;
        private static bool _armed;   // the black-screen piece is handed to ScenePrewarm

        /// <summary>How many finished buildings wait for a moment nobody sees them.</summary>
        public static int StandWaiting => Waiting.Count;

        // The declaration of a finished building, as the load declares it from the district's line and a move in play makes it.
        private static MadeSpec FinishedSpec(BuildingsLot lot, FinishedBuilding f)
        {
            Lot plain = UptownLots.Get(lot.Id);
            return StandRules.Spec(GroundOf(lot, f), f, TrafficOf(lot.Id), () => _known && !_down && UptownLots.Ready && plain != null && UptownLots.Open(plain));
        }

        /// <summary>
        /// A building finished on a lot (DistrictBuildings.Finish) stood in play (DistrictBuildings.StandNow): at once, when none of its
        /// ground is in the camera's picture; else at the next black screen or the moment nobody sees it. False, with why, when it is left
        /// to the next load (a session, the engine not ready, the make refused: Player.log says what).
        /// </summary>
        public static bool StandNow(string lotId, out string why)
        {
            FinishedBuilding f = _state.On(lotId);
            BuildingsLot own = default;
            bool known = f != null && FindLot(lotId, out own);
            var w = new StandWait { Lot = lotId };
            var ask = new StandAsk
            {
                Lives = Lives && Instance != null && !FrameGuard.Off, InSession = GoingPublic.InSession, Recorded = f != null && known, Standing = StandsOn(lotId),
                Settled = f != null && f.Settled, Unread = _stateUnread,
                Ready = NewBuildingsDirector.Instance != null && CopyGame.Ready && CopyGame.DesignCount > 0 && !MadeGame.Loading
            };
            if (known)
            {
                StandRules.Rect(GroundOf(own, f), out w.X1, out w.Z1, out w.X2, out w.Z2);
                ask.Black = LifeGame.Black;
                ask.InView = ask.Lives && LifeGame.InView(w.X1, w.Z1, w.X2, w.Z2);
                ask.OnGround = ask.Lives && LifeGame.PlayerOn(w.X1, w.Z1, w.X2, w.Z2);
            }
            StandCall call = StandRules.Call(ask, out why);
            if (call == StandCall.Load)
            {
                Logger?.Info("Uptown: the building finished on lot " + (lotId ?? "(none)") + " stands from the next load: " + why + ".");
                return false;
            }
            if (call == StandCall.Wait)
            {
                Waiting.Add(w);
                Arm();
                Logger?.Info("Uptown: the building finished on lot " + lotId + " waits to stand until none of its ground is in view or the screen is black"
                             + (ask.OnGround ? " (the player is on it)" : string.Empty) + ".");
                return true;
            }
            return Made(w, out why);
        }

        // Hands the black-screen piece over once: each time the screen goes black outdoors, what waits stands.
        private static void Arm()
        {
            if (_armed)
            {
                return;
            }
            _armed = true;
            ScenePrewarm.Again("uptown: finished buildings stood behind the black screen", _atBlack ??= AtBlack);
        }

        private static bool AtBlack()
        {
            StandPending(true);
            if (Waiting.Count == 0)
            {
                _armed = false;
                return true;
            }
            return false;   // tried again at the next black screen
        }

        // Once a second while a building waits (OnUpdate), and at each black screen (AtBlack): those whose ground nobody sees stand.
        private static void StandPending(bool black)
        {
            if (Waiting.Count == 0)
            {
                return;
            }
            if (!Lives || FrameGuard.Off || _standingDown)
            {
                Waiting.Clear();   // the district is out of the game: its records come back, and stand, at a load where it lives
                return;
            }
            for (int i = Waiting.Count - 1; i >= 0 && i < Waiting.Count; i--)
            {
                StandWait w = Waiting[i];
                FinishedBuilding f = _state.On(w.Lot);
                if (f == null || f.Settled || StandsOn(w.Lot))
                {
                    Waiting.RemoveAt(i);   // taken back, knocked down, or stood meanwhile
                    continue;
                }
                if (LifeGame.PlayerOn(w.X1, w.Z1, w.X2, w.Z2) || (!black && !LifeGame.Black && LifeGame.InView(w.X1, w.Z1, w.X2, w.Z2)))
                {
                    continue;
                }
                Waiting.RemoveAt(i);
                Made(w, out _);
            }
        }

        // One building made in play, every fault caught: it is the next load's if anything fails.
        private static bool Made(StandWait w, out string why)
        {
            try
            {
                return StandOne(w, out why);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                MadeBuildings.Withdraw(w.Lot);
                StandFills.Clear();
                why = "a fault (Player.log has it)";
                Logger?.Warn("Uptown: the building finished on lot " + w.Lot + " could not be stood now (" + why + "); it stands from the next load.");
                return false;
            }
        }

        private static bool StandOne(StandWait w, out string why)
        {
            why = null;
            FinishedBuilding f = _state.On(w.Lot);
            NewBuildingsDirector director = NewBuildingsDirector.Instance;
            BuildingsLot own = default;
            if (f == null || director == null || !FindLot(w.Lot, out own))
            {
                why = "the record or the engine is gone";
                return false;
            }
            long began = Stopwatch.GetTimestamp();
            string lot = w.Lot;
            MadeBuilding knocked = MadeBuildings.Get(lot);
            if (knocked != null && !Standing.Contains(knocked))
            {
                director.Retire(knocked);   // a building knocked down on this lot in this session: its address and ground are free again
            }
            MadeSpec spec = FinishedSpec(own, f);
            MadeBuildings.Declare(spec);
            StandFills.Clear();
            // The streets' arrangement for this declaration alone, by the rules that arrange them all at a load (its look, its place side by side, its fillers).
            FrontageRules.Plan(Plan, id => string.Equals(id, lot, StringComparison.Ordinal) ? spec : null, CopyGame.Catalogue, StandFills);
            MadeBuilding made = director.MakeNow(spec, out why);
            if (made == null)
            {
                MadeBuildings.Withdraw(spec.Id);
                StandFills.Clear();
                Logger?.Warn("Uptown: the building finished on lot " + lot + " could not be stood now (" + why + "); it stands from the next load.");
                return false;
            }
            Declared.Add(spec);
            LifeGame.Enter(made);
            bool cut = LifeGame.Cut(made);
            Standing.Add(made);
            MarkStanding();
            int fillersBefore = LifeGame.FillerCount;
            LifeGame.StandFillers(StandFills);
            int fillers = LifeGame.FillerCount - fillersBefore;
            StandFills.Clear();
            bool listed = MadeGame.JoinCity(made, out string joinWhy);
            string setUp = LifeGame.Settle(made, f.Owner, f.Price, SaveGameManager.Current);
            if (setUp == null)
            {
                StandRules.SetUp(f);
                if (listed)
                {
                    MadeGame.Light(made.Controller);   // a unit the city put up for rent shows its sign
                }
            }
            WriteState();
            _stamp++;
            DistrictPlaces.Changed();
            GroundGiven(w);
            double ms = (Stopwatch.GetTimestamp() - began) * 1000.0 / Stopwatch.Frequency;
            string owner = f.Owner == FinishOwner.Player ? "the player's, deed " + (setUp == null ? "written" : "not yet: " + setUp) : "the city's";
            Logger?.Info("Uptown: the building finished on lot " + lot + " stands now at " + made.Address + " (" + made.Design + ", " + owner + "), "
                         + (cut ? "cut into the walking meshes" : "not cut into the walking meshes") + ", " + fillers + " filler(s), "
                         + (listed ? "in the city's list" : "not in the city's list yet (" + joinWhy + ")") + ", in " + ms.ToString("F1", Plain)
                         + " ms; the next load stands the same building from the same record.");
            if (Meta.Dev.Verbose)
            {
                StandCheck(made, f, listed, ms);
            }
            if (!CompleteOne(made, f, "its stand"))
            {
                LifeGame.SayIncomplete(1);   // a building handed over now that is not yet whole: Player.log names the items
            }
            return true;
        }

        // The site's props and the look over its ground come down in the same moment: whoever keeps the ground is told (Civic, by Wiring).
        private static void GroundGiven(StandWait w)
        {
            Action<string, float, float, float, float> given = DistrictBuildings.GroundGiven;
            if (given == null)
            {
                return;
            }
            try
            {
                given(w.Lot, w.X1, w.Z1, w.X2, w.Z2);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private static void StandCheck(MadeBuilding made, FinishedBuilding f, bool listed, double ms)
        {
            bool lookup = MadeGame.InLookup(made, out bool inSave);
            bool again = StandsOn(made.Spec.Id) && MadeBuildings.Get(made.Spec.Id) == made && Standing.Contains(made);
            bool deed = f.Owner != FinishOwner.Player || LifeGame.Deed(made) != null;
            bool pass = lookup && inSave && again && deed && f.Settled && listed;
            Logger?.Info("Uptown check (a building stood in play, " + made.Address + "): in the game's building lookup " + (lookup ? "YES" : "NO")
                         + ", its registration in the save " + (inSave ? "YES" : "NO") + ", among the district's standing " + (again ? "YES" : "NO")
                         + ", the player's deed " + (f.Owner != FinishOwner.Player ? "not needed" : deed ? "YES" : "NO") + ", set up " + (f.Settled ? "YES" : "NO")
                         + ", in the city's list " + (listed ? "YES" : "NO") + ", " + ms.ToString("F1", Plain) + " ms: " + (pass ? "PASS" : "FAIL"));
        }
    }
}
