using System;
using System.Collections.Generic;
using RealLifeOverhaul.NewBuildings;

namespace RealLifeOverhaul.Uptown
{
    /// <summary>What the district does with a building handed to it: stand it now, wait for a moment nobody sees it, or leave it to the next load.</summary>
    public enum StandCall
    {
        Load,   // from the next load, as every finished building stood before (the reason is said)
        Wait,   // not now: it is in view, or the player stands on its ground; asked again each second and at each black screen
        Now     // at once: made in play by the code that makes it at a load
    }

    /// <summary>What is known of a finished building and the player's view when it is handed to the district (<see cref="StandRules.Call"/>).</summary>
    public struct StandAsk
    {
        public bool Lives;       // Uptown is a district of the game with its buildings standing at this load
        public bool Ready;       // the made buildings' engine can make one in play: the city's designs measured, no load under way
        public bool InSession;   // a Going Public session: the building stands as on every machine of it, from the next load
        public bool Recorded;    // the district's line holds the finished building
        public bool Standing;    // a district building stands on its lot at this load already
        public bool Settled;     // the record has stood at a load before: its first load's setting up is done
        public bool Unread;      // the district's line in this save could not be read: kept as it is
        public bool InView;      // any of its ground is in the camera's picture, or the city map is open
        public bool Black;       // the loading screen or the game's black screen at full strength
        public bool OnGround;    // the player stands on its ground
    }

    /// <summary>
    /// When a building handed to the district stands, and how (docs/features/uptown.md, "A building handed over stands at once"),
    /// game-free so EconomyCheck holds it: the rule that decides (<see cref="Call"/>), the declaration that makes it, the one a
    /// load makes from the district's line and a move in play makes from the same record (<see cref="Spec"/>), the ground it
    /// takes (<see cref="Rect"/>) and the first load's setting up that a move in play has already done (<see cref="FirstLoadDue"/>).
    /// COST: nothing per frame; a few comparisons a call.
    /// </summary>
    public static class StandRules
    {
        public const float BoxHeight = 40f;   // m: the height of the box the camera's picture is tested against, from the ground up
        public const float OnGroundMargin = 2f;   // m round its ground that the player standing in stops it standing

        /// <summary>
        /// Stand now, wait or leave it to the next load. A building in view waits for a black screen (the game's fade at a door,
        /// the loading screen) or for the moment nobody sees its ground; one the player stands on waits for them to step off.
        /// </summary>
        public static StandCall Call(StandAsk a, out string why)
        {
            why = null;
            if (!a.Lives)
            {
                why = "Uptown is not a district of the game at this load";
            }
            else if (!a.Ready)
            {
                why = "the made buildings' engine cannot make one now (the city's designs are not measured, or a load is under way)";
            }
            else if (a.InSession)
            {
                why = "a Going Public session: a finished building stands from the next load there, as on every machine of it";
            }
            else if (a.Unread)
            {
                why = "the district's own line in this save cannot be read, and is kept as it is";
            }
            else if (!a.Recorded)
            {
                why = "the district's line holds no building finished on that lot";
            }
            else if (a.Standing)
            {
                why = "a building stands there already";
            }
            else if (a.Settled)
            {
                why = "it has stood at a load, and the game's records name it";
            }
            if (why != null)
            {
                return StandCall.Load;
            }
            if (a.OnGround)
            {
                return StandCall.Wait;
            }
            return a.Black || !a.InView ? StandCall.Now : StandCall.Wait;
        }

        /// <summary>
        /// The declaration of a finished building: its structure, design, storeys, ground and traffic as the district's line
        /// has them, filed under Uptown. The load declares it from the line and a move in play makes it from the same record,
        /// so the building stood now is the one the next load would stand. <paramref name="others"/> is null or empty for a
        /// finished building (it keeps the structure and design the construction game gave it).
        /// </summary>
        public static MadeSpec Spec(BuildingsLot ground, BuildingsStructure structure, IList<BuildingsStructure> others, string design, int storeys, int traffic, Func<bool> wanted)
        {
            MadeStructure[] instead = null;
            if (others != null && others.Count > 0)
            {
                instead = new MadeStructure[others.Count];
                for (int i = 0; i < others.Count; i++)
                {
                    instead[i] = new MadeStructure { Size = others[i].Size, Version = others[i].Version, Type = others[i].Type };
                }
            }
            return new MadeSpec
            {
                Id = ground.Id, Street = ground.Street, Number = ground.Number, Size = structure.Size, Version = structure.Version, Type = structure.Type,
                District = DistrictSplit.IdOfColumn(ground.Block % 100), Design = string.IsNullOrEmpty(design) ? null : design,
                Lots = new[] { new MadeLot(ground.X, ground.Z, ground.Facing, ground.Width, ground.Depth) }, Preferred = 0,
                Traffic = traffic, Floors = Math.Max(1, storeys), DesignArea = storeys <= 0, Life = true, Wanted = wanted, Others = instead
            };
        }

        /// <summary>The declaration of a building the district's line records (<paramref name="ground"/>: its own lot's, or the rectangle round every lot it stands over).</summary>
        public static MadeSpec Spec(BuildingsLot ground, FinishedBuilding f, int traffic, Func<bool> wanted)
        {
            var structure = new BuildingsStructure { Size = f.Size, Version = f.Version, Type = f.Type };
            return Spec(ground, structure, null, f.Design, f.Storeys, traffic, wanted);
        }

        /// <summary>The ground a building takes on the world's axes, for the camera's picture and the player's feet: x and z from, to.</summary>
        public static void Rect(BuildingsLot ground, out float x1, out float z1, out float x2, out float z2)
        {
            bool avenue = BuildingsRules.FrontsAvenue(ground.Facing);
            float halfX = (avenue ? ground.Width : ground.Depth) * 0.5f, halfZ = (avenue ? ground.Depth : ground.Width) * 0.5f;
            x1 = ground.X - halfX;
            x2 = ground.X + halfX;
            z1 = ground.Z - halfZ;
            z2 = ground.Z + halfZ;
        }

        /// <summary>True when a point on the ground (x, z) is on the rectangle, grown by <see cref="OnGroundMargin"/>.</summary>
        public static bool OnIt(float x1, float z1, float x2, float z2, float x, float z)
        {
            return x >= x1 - OnGroundMargin && x <= x2 + OnGroundMargin && z >= z1 - OnGroundMargin && z <= z2 + OnGroundMargin;
        }

        /// <summary>
        /// True when the record's first load setting up (the player's deed, or the city's letting) is still to be done: a building
        /// stood in play was set up as it stood and is marked, so the next load stands it and does not set it up twice.
        /// </summary>
        public static bool FirstLoadDue(FinishedBuilding f)
        {
            return f != null && !f.Settled;
        }

        /// <summary>The record marked set up: its first load's work is done (at its first load, or as it stood in play).</summary>
        public static void SetUp(FinishedBuilding f)
        {
            if (f != null)
            {
                f.Settled = true;
            }
        }
    }
}
