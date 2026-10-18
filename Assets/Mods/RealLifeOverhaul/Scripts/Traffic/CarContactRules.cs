using System;

namespace RealLifeOverhaul
{
    /// <summary>What the car the player drives met, read by CarContacts from the other collider's layer and owner.</summary>
    public enum ContactKind
    {
        Ground,      // a layer the car drives on: the road and its kerbs, pavements, crossings, the ground (trees and barriers stand on some)
        Vehicle,     // another car: parked, in the traffic, or a player's
        Person,      // somebody on foot
        Furniture,   // street furniture: lamp posts, bins, benches, fences, bushes
        Building,    // a building or a wall
        Other,       // any other solid thing
        Own,         // the car's own colliders, or something riding in it (its load space)
        Trigger      // a trigger: never solid
    }

    /// <summary>Whether a contact is the car hitting something, and if not, why not.</summary>
    public enum ContactVerdict
    {
        Hit,          // something struck: it counts by its impact speed along the contact's normal
        Trigger,      // a trigger
        Own,          // the car's own colliders or its load
        Underneath,   // a mostly upward normal: the ground, or something the car drives over
        Kerb,         // low on a layer the car drives on: a kerb, a seam or the road's edge under the body or a wheel
        NoPoints      // the physics gave no contact point to judge
    }

    /// <summary>One contact of the car judged: the verdict and, for a hit, how hard it was along the contact's normal.</summary>
    public readonly struct CarContact
    {
        public readonly ContactKind Kind;
        public readonly ContactVerdict Verdict;
        public readonly float Impact;               // m/s: the closing speed along the contact's normal
        public readonly float NormalX, NormalY, NormalZ;
        public readonly float Rise;                 // metres above the body's underside, in the car's own frame
        public readonly int Point;                  // the contact point that decided (-1: none)

        public CarContact(ContactKind kind, ContactVerdict verdict, float impact, float normalX, float normalY, float normalZ, float rise, int point)
        {
            Kind = kind;
            Verdict = verdict;
            Impact = impact;
            NormalX = normalX;
            NormalY = normalY;
            NormalZ = normalZ;
            Rise = rise;
            Point = point;
        }

        /// <summary>True if the car hit something.</summary>
        public bool IsHit => Verdict == ContactVerdict.Hit;

        /// <summary>True if the car hit something at least this hard (m/s along the normal).</summary>
        public bool Counts(float from) => Verdict == ContactVerdict.Hit && Impact >= from;
    }

    /// <summary>
    /// When a contact of the car the player drives is the car hitting something, in plain numbers. Every feature that
    /// judges a collision of that car asks this (the driving test, crashes, jobs, outings; NavCheck's CarContactCheck
    /// holds the sources to it). A contact counts only when the car meets another vehicle, a person, street furniture,
    /// a building or a wall: never the ground it drives on, its own colliders or load, or a trigger. The ground is told
    /// by the contact's normal (mostly upward, whatever the layer, so a body pitched by braking or a softer suspension
    /// still reads its road as road) and, on the layers the car drives on, by height: a contact below kerb height over
    /// the body's own underside is a kerb, a seam or the road's edge, whereas a tree or a barrier set on the road's
    /// layer is met higher up. How hard is the closing speed along the normal, not the size of the relative velocity:
    /// a body sliding over the road at speed closes on it at nothing. Nothing allocated.
    /// </summary>
    public static class CarContactRules
    {
        public const float GroundNormal = 0.6f;   // upward part of a contact's normal from which it is the ground under the car (within 53 degrees of straight up)
        public const float KerbRise = 0.3f;       // metres over the body's underside, in the car's frame, below which a ground layer's contact is a kerb (they stand about 0.15 m)
        public const int Layers = 32;             // the engine's layers

        /// <summary>The kind of thing on a layer of the game, by the layer's name. An unnamed or unknown layer is Other.</summary>
        public static ContactKind KindOfLayer(string name)
        {
            switch (name)
            {
                case "Ground":
                case "GroundUnplacable":
                case "Roads":
                case "Sidewalk":
                case "Crosswalk":
                    return ContactKind.Ground;
                case "Vehicles":
                case "PlayerVehicles":
                case "ParkedVehicles":
                case "AiVehicles":
                    return ContactKind.Vehicle;
                case "Player":
                case "Human":
                    return ContactKind.Person;
                case "StreetProps":
                case "InteractiveItems":
                    return ContactKind.Furniture;
                case "Buildings":
                case "BuildingWalls":
                    return ContactKind.Building;
                default:
                    return ContactKind.Other;
            }
        }

        /// <summary>The kind of what was met: a trigger, or something of the car's own or riding in it, whatever its layer.</summary>
        public static ContactKind KindOf(ContactKind layerKind, bool trigger, bool ridesInCar)
        {
            return trigger ? ContactKind.Trigger : ridesInCar ? ContactKind.Own : layerKind;
        }

        /// <summary>The closing speed along a normal (m/s): how hard the two met, whichever way the normal points.</summary>
        public static float Closing(float rvx, float rvy, float rvz, float nx, float ny, float nz)
        {
            return Math.Abs(rvx * nx + rvy * ny + rvz * nz);
        }

        /// <summary>
        /// Metres a point stands above the body's underside, along the car's own up: <paramref name="floor"/> is the
        /// underside's height in the car's frame, so a body pitched or lowered by its suspension measures from itself.
        /// </summary>
        public static float Rise(float px, float py, float pz, float ox, float oy, float oz, float ux, float uy, float uz, float floor)
        {
            return (px - ox) * ux + (py - oy) * uy + (pz - oz) * uz - floor;
        }

        /// <summary>
        /// One contact point judged: what was met, the relative velocity of the two (m/s), the point's normal (a unit
        /// vector, world up being +y) and its rise over the body's underside (metres).
        /// </summary>
        public static CarContact Judge(ContactKind kind, float rvx, float rvy, float rvz, float nx, float ny, float nz, float rise, int point)
        {
            float impact = Closing(rvx, rvy, rvz, nx, ny, nz);
            ContactVerdict verdict = kind == ContactKind.Trigger ? ContactVerdict.Trigger
                : kind == ContactKind.Own ? ContactVerdict.Own
                : ny >= GroundNormal ? ContactVerdict.Underneath
                : kind == ContactKind.Ground && rise < KerbRise ? ContactVerdict.Kerb
                : ContactVerdict.Hit;
            return new CarContact(kind, verdict, impact, nx, ny, nz, rise, point);
        }

        /// <summary>A contact the physics gave no point for: a trigger or the car's own if it is one, else nothing to judge.</summary>
        public static CarContact Unpointed(ContactKind kind)
        {
            ContactVerdict verdict = kind == ContactKind.Trigger ? ContactVerdict.Trigger : kind == ContactKind.Own ? ContactVerdict.Own : ContactVerdict.NoPoints;
            return new CarContact(kind, verdict, 0f, 0f, 0f, 0f, 0f, -1);
        }

        /// <summary>The contact that speaks for a collision: a hit over anything else, the harder of two hits, else the first.</summary>
        public static CarContact Stronger(in CarContact a, in CarContact b)
        {
            if (a.IsHit != b.IsHit)
            {
                return a.IsHit ? a : b;
            }
            if (a.IsHit)
            {
                return b.Impact > a.Impact ? b : a;
            }
            return a.Verdict == ContactVerdict.NoPoints ? b : a;
        }

        /// <summary>Why a contact did not count, for the log.</summary>
        public static string Why(ContactVerdict verdict)
        {
            switch (verdict)
            {
                case ContactVerdict.Hit: return "something struck";
                case ContactVerdict.Trigger: return "a trigger";
                case ContactVerdict.Own: return "the car's own collider or its load";
                case ContactVerdict.Underneath: return "the ground under the car (an upward normal)";
                case ContactVerdict.Kerb: return "a kerb, a seam or the road's edge, below kerb height";
                default: return "no contact point to judge";
            }
        }

        /// <summary>What a kind is, for the log.</summary>
        public static string Name(ContactKind kind)
        {
            switch (kind)
            {
                case ContactKind.Ground: return "a layer the car drives on";
                case ContactKind.Vehicle: return "a vehicle";
                case ContactKind.Person: return "a person";
                case ContactKind.Furniture: return "street furniture";
                case ContactKind.Building: return "a building";
                case ContactKind.Own: return "the car's own";
                case ContactKind.Trigger: return "a trigger";
                default: return "a solid thing";
            }
        }
    }
}
