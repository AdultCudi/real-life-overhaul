using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using RealLifeOverhaul;

// When a contact of the car the player drives is the car hitting something (Traffic/CarContactRules.cs), and what the
// driving examiner marks for it (Licences/Driving/TestContactRules.cs): the road under a van whose nose dips as it brakes,
// a kerb under the wheels, a load riding in it, and real knocks into a car, a person, a post or a tree; and over the
// sources, every collision of the player's car judged through that one rule.
internal static partial class Program
{
    private static void CarContactChecks()
    {
        CarContactLayers();
        CarContactScrapes();
        CarContactKerbs();
        CarContactLoads();
        CarContactKnocks();
        CarContactFrame();
        CarContactCost();
        CarContactSources();
    }

    // A contact as the game hands it: what was met by its layer, the relative velocity, the normal (made a unit vector),
    // and its rise over the body's underside; the examiner's mark with the car at its own speed, nothing marked for a while.
    private static ContactMark Examined(string layer, float rvx, float rvy, float rvz, float nx, float ny, float nz, float rise, float carSpeed, bool ridesIn = false)
    {
        return TestContactRules.Mark(Contact(layer, rvx, rvy, rvz, nx, ny, nz, rise, ridesIn), carSpeed, 100f);
    }

    private static CarContact Contact(string layer, float rvx, float rvy, float rvz, float nx, float ny, float nz, float rise, bool ridesIn = false, bool trigger = false)
    {
        float length = (float)Math.Sqrt(nx * nx + ny * ny + nz * nz);
        ContactKind kind = CarContactRules.KindOf(CarContactRules.KindOfLayer(layer), trigger, ridesIn);
        return CarContactRules.Judge(kind, rvx, rvy, rvz, nx / length, ny / length, nz / length, rise, 0);
    }

    // The game's layers (its TagManager, Build 3682): the road's, the kerb's and the ground's are where the car drives.
    private static void CarContactLayers()
    {
        bool ground = true;
        foreach (string layer in new[] { "Ground", "GroundUnplacable", "Roads", "Sidewalk", "Crosswalk" })
        {
            ground &= CarContactRules.KindOfLayer(layer) == ContactKind.Ground;
        }
        Check(ground, "car contact: the road, kerb, pavement, crossing and ground layers are where the car drives");
        Check(CarContactRules.KindOfLayer("Vehicles") == ContactKind.Vehicle && CarContactRules.KindOfLayer("ParkedVehicles") == ContactKind.Vehicle
              && CarContactRules.KindOfLayer("AiVehicles") == ContactKind.Vehicle && CarContactRules.KindOfLayer("PlayerVehicles") == ContactKind.Vehicle,
            "car contact: the four vehicle layers are vehicles");
        Check(CarContactRules.KindOfLayer("Human") == ContactKind.Person && CarContactRules.KindOfLayer("StreetProps") == ContactKind.Furniture
              && CarContactRules.KindOfLayer("BuildingWalls") == ContactKind.Building && CarContactRules.KindOfLayer("Buildings") == ContactKind.Building,
            "car contact: people, street props and buildings are what they are");
        Check(CarContactRules.KindOfLayer("Default") == ContactKind.Other && CarContactRules.KindOfLayer("") == ContactKind.Other
              && CarContactRules.KindOfLayer(null) == ContactKind.Other, "car contact: an unknown or unnamed layer is a solid thing");
        Check(CarContactRules.KindOf(ContactKind.Vehicle, true, false) == ContactKind.Trigger && CarContactRules.KindOf(ContactKind.Vehicle, false, true) == ContactKind.Own,
            "car contact: a trigger, or anything riding in the car, is that whatever its layer");
    }

    // The player's report: the van flagged for serious collisions while stopping. The Mersaidi Dash's front springs sit at
    // nine tenths of their travel at rest, so braking bottoms them and NWH turns on the wheels' solid lower halves, which
    // meet the road at the van's own speed. The road is under the car, never a collision.
    private static void CarContactScrapes()
    {
        Check(Examined("Roads", 0f, -0.4f, 7.5f, 0.05f, 0.99f, 0.12f, -0.35f, 7.5f) == ContactMark.None,
            "car contact: a bottomed front wheel meeting the road at 27 km/h under braking is not marked");
        Check(Examined("Roads", 0f, -0.2f, 4f, 0f, 1f, 0f, -0.3f, 4f) == ContactMark.None,
            "car contact: stopping slowly from 14 km/h with the nose dipped onto its stops is not marked");
        Check(Examined("Roads", 0f, -0.3f, 9f, 0f, 1f, 0.05f, 0.02f, 9f) == ContactMark.None,
            "car contact: the body's underside scraping the road is not marked");
        Check(Examined("Default", 0f, -0.5f, 6f, 0f, 1f, 0f, 0f, 6f) == ContactMark.None,
            "car contact: a floor of any layer met from above is the ground, not a collision");
        // A softer suspension: more dip and roll, the road's normal tipped to 40 degrees in the car's view of it.
        Check(Examined("Roads", 0.3f, -1.5f, 9f, 0.2f, 0.77f, 0.6f, -0.1f, 9f) == ContactMark.None,
            "car contact: with a soft suspension's dip and roll the road still reads as the road");
        Check(Examined("Ground", 0f, -2f, 11f, 0f, 1f, 0f, -0.4f, 11f) == ContactMark.None,
            "car contact: landing hard on the ground at 40 km/h is not a collision");
        CarContact scrape = Contact("Roads", 0f, -0.4f, 7.5f, 0f, 1f, 0f, -0.35f);
        Check(scrape.Verdict == ContactVerdict.Underneath && scrape.Impact < 0.5f,
            "car contact: the road scrape is the ground under the car, at under 0.5 m/s along its normal");
    }

    // Kerbs (the game's kerbs are on the road's layer) and the pavement's edge: under the wheels or the body's underside.
    private static void CarContactKerbs()
    {
        Check(Examined("Roads", 0f, 0f, 5f, 0f, 0.3f, -0.95f, -0.2f, 5f) == ContactMark.None,
            "car contact: a wheel against the kerb's face at 18 km/h is not marked");
        Check(Examined("Sidewalk", 0f, 0f, 6f, 0f, 0.45f, -0.89f, 0.05f, 6f) == ContactMark.None,
            "car contact: the body's underside on the pavement's edge is not marked");
        Check(Examined("Crosswalk", 0f, 0f, 8f, 0f, 0.1f, -1f, 0.25f, 8f) == ContactMark.None,
            "car contact: a seam in the road at the height of a kerb is not marked");
        Check(Contact("Roads", 0f, 0f, 5f, 0f, 0.3f, -0.95f, -0.2f).Verdict == ContactVerdict.Kerb,
            "car contact: the kerb under a wheel is a kerb");
    }

    // Something in the load space: a box sliding forward as the van brakes, or a trigger of the car's own.
    private static void CarContactLoads()
    {
        Check(Examined("Default", 0f, 0f, 4f, 0f, 0f, -1f, 0.6f, 6f, ridesIn: true) == ContactMark.None,
            "car contact: a load sliding into the bulkhead at 4 m/s is not marked");
        Check(Examined("InteractiveItems", 0f, 0f, 5f, 0f, 0f, -1f, 0.8f, 6f, ridesIn: true) == ContactMark.None,
            "car contact: a load of any layer riding in the car is never a collision");
        Check(Contact("Default", 0f, 0f, 4f, 0f, 0f, -1f, 0.6f, ridesIn: true).Verdict == ContactVerdict.Own,
            "car contact: the load is the car's own");
        Check(!Contact("ParkedVehicles", 0f, 0f, 9f, 0f, 0f, -1f, 0.6f, trigger: true).IsHit,
            "car contact: a trigger, even round a car, is never hit");
    }

    // Real knocks: the car into another car, a person, a post, a wall, and a tree that stands on the road's layer.
    private static void CarContactKnocks()
    {
        Check(Examined("ParkedVehicles", 0f, 0f, -6f, 0f, 0.05f, -1f, 0.5f, 6f) == ContactMark.Collision,
            "car contact: into a parked car at 22 km/h is a collision");
        Check(Examined("AiVehicles", 1f, 0f, -5f, 0.2f, 0f, -1f, 0.4f, 5f) == ContactMark.Collision,
            "car contact: into a car of the traffic at 18 km/h is a collision");
        Check(Examined("Human", 0f, 0f, 3f, 0f, 0.1f, -1f, 0.7f, 3f) == ContactMark.Touched,
            "car contact: a person knocked at 11 km/h is a touch");
        Check(Examined("Human", 0f, 0f, 5f, 0f, 0.1f, -1f, 0.7f, 5f) == ContactMark.Collision,
            "car contact: a person knocked at 18 km/h is a collision");
        Check(Examined("StreetProps", 0f, 0f, 4.5f, 0f, 0f, -1f, 0.2f, 4.5f) == ContactMark.Collision,
            "car contact: a lamp post or a bollard hit low on the bumper at 16 km/h is a collision");
        Check(Examined("StreetProps", 0f, 0f, 1.5f, 0f, 0f, -1f, 0.4f, 1.6f) == ContactMark.Touched,
            "car contact: nudging a post at walking pace is a touch");
        Check(Examined("BuildingWalls", 0f, 0f, 7f, 0f, 0f, -1f, 0.6f, 7f) == ContactMark.Collision,
            "car contact: into a wall at 25 km/h is a collision");
        Check(Examined("Roads", 0f, 0f, 6f, 0f, 0f, -1f, 0.9f, 6f) == ContactMark.Collision,
            "car contact: a tree or a barrier standing on the road's layer, met at the body's face, is a collision");
        Check(Examined("BuildingWalls", 0f, 0f, 10f, -0.995f, 0f, 0.15f, 0.6f, 10f) == ContactMark.Touched,
            "car contact: a wall grazed along its length at 36 km/h is a touch, by the speed into it");
        Check(Examined("ParkedVehicles", 0f, 0f, 0.8f, 0f, 0f, -1f, 0.5f, 3f) == ContactMark.None,
            "car contact: under 1 m/s into anything is too soft to mark");
        Check(Examined("AiVehicles", 0f, 0f, 4f, 0f, 0f, 1f, 0.5f, 0.6f) == ContactMark.RunInto,
            "car contact: run into while standing is not the driver's fault");
        Check(TestContactRules.Mark(Contact("ParkedVehicles", 0f, 0f, 6f, 0f, 0f, -1f, 0.5f), 6f, 1f) == ContactMark.Same,
            "car contact: a second contact a second after a marked one is the same knock");

        // One collision's points: the hit speaks for it, the harder of two hits, else the first.
        CarContact under = Contact("ParkedVehicles", 0f, -0.2f, 6f, 0f, 1f, 0f, 0f);
        CarContact soft = Contact("ParkedVehicles", 0f, 0f, 2f, 0f, 0f, -1f, 0.5f);
        CarContact hard = Contact("ParkedVehicles", 0f, 0f, 6f, 0f, 0f, -1f, 0.5f);
        Check(CarContactRules.Stronger(under, soft).Impact == soft.Impact && CarContactRules.Stronger(soft, hard).Impact == hard.Impact
              && CarContactRules.Stronger(hard, soft).Impact == hard.Impact, "car contact: a collision's strongest hit speaks for it");
        CarContact none = CarContactRules.Unpointed(ContactKind.Vehicle);
        Check(none.Verdict == ContactVerdict.NoPoints && CarContactRules.Stronger(none, under).Verdict == ContactVerdict.Underneath
              && CarContactRules.Unpointed(ContactKind.Own).Verdict == ContactVerdict.Own, "car contact: a collision without points counts for nothing");
        Check(CarContactRules.Closing(0f, 0f, 6f, 0f, 0f, -1f) == 6f && CarContactRules.Closing(0f, 0f, -6f, 0f, 0f, -1f) == 6f,
            "car contact: the impact is the closing speed along the normal, whichever way either points");
    }

    // A body pitched by braking measures a kerb from its own underside, not from the world's level.
    private static void CarContactFrame()
    {
        double pitch = 7.0 * Math.PI / 180.0;   // a nose dive deeper than the game's springs give today
        float upY = (float)Math.Cos(pitch), upZ = (float)Math.Sin(pitch);
        float floor = 0.27f;                    // the Mersaidi Dash's body collider's underside in its own frame
        float localY = 0.29f, localZ = 3.2f;    // a kerb's top 2 cm into the underside under the nose
        float worldY = localY * upY - localZ * upZ, worldZ = localY * upZ + localZ * upY;
        float rise = CarContactRules.Rise(0f, worldY, worldZ, 0f, 0f, 0f, 0f, upY, upZ, floor);
        Check(Math.Abs(rise - 0.02f) < 0.001f, $"car contact: under a dipped nose the kerb stands {rise:0.000} m over the body's underside (0.020)");
        Check(Examined("Roads", 0f, 0f, 5f, 0f, 0.2f, -0.98f, rise, 5f) == ContactMark.None, "car contact: so the dipped nose on a kerb is not marked");
        Check(CarContactRules.KerbRise >= 0.2f && CarContactRules.KerbRise <= 0.4f && CarContactRules.GroundNormal >= 0.5f && CarContactRules.GroundNormal <= 0.75f,
            "car contact: kerb height 0.2 to 0.4 m over the underside, the ground's normal within 40 to 60 degrees of straight up");
    }

    private static void CarContactCost()
    {
        long bytes = CarContactAsks(out long ms, out int hits);
        Check(bytes == 0, $"car contact: judging 1,000,000 contact points allocates nothing ({bytes} bytes)");
        Check(ms < 100, $"car contact: judging 1,000,000 contact points takes {ms} ms (100 at the most; a collision has a few)");
        Check(hits > 0 && hits < 1000000, $"car contact: the timed points were judged both ways ({hits} hits)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static long CarContactAsks(out long ms, out int hits)
    {
        Stopwatch watch = Stopwatch.StartNew();
        long before = GC.GetAllocatedBytesForCurrentThread();
        hits = 0;
        CarContact best = CarContactRules.Unpointed(ContactKind.Other);
        for (int n = 0; n < 1000000; n++)
        {
            float ny = (n % 11) * 0.1f;
            ContactKind kind = (ContactKind)(n % 6);
            CarContact one = CarContactRules.Judge(kind, 0f, -0.3f, 5f + (n % 7), 0f, ny, -(1f - ny), (n % 9) * 0.1f - 0.3f, n % 4);
            best = CarContactRules.Stronger(best, one);
            hits += TestContactRules.Mark(one, 5f, 10f) != ContactMark.None ? 1 : 0;
        }
        watch.Stop();
        long allocated = GC.GetAllocatedBytesForCurrentThread() - before;
        ms = watch.ElapsedMilliseconds;
        hits += best.IsHit ? 0 : 1;
        return allocated;
    }

    // Over the sources as they ship: every listener to a vehicle's collisions judges through CarContacts, and nothing
    // else in the mod reads a collision's relative velocity or the game's own ground-layer test.
    private static void CarContactSources()
    {
        string scripts = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        if (!Directory.Exists(scripts))
        {
            Check(false, "car contact: the mod's sources are at " + scripts);
            return;
        }
        int listeners = 0;
        string unjudged = null, raw = null;
        foreach (string path in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string name = Path.GetFileName(path);
            string code = Code(File.ReadAllText(path));
            bool listens = code.Contains("onCollision.AddListener(") || code.Contains("void OnCollisionEnter(") || code.Contains("void OnCollisionStay(");
            listeners += listens ? 1 : 0;
            if (listens && !code.Contains("CarContacts.Judge("))
            {
                unjudged = (unjudged == null ? "" : unjudged + ", ") + name;
            }
            if (name != "CarContacts.cs" && (code.Contains("relativeVelocity") || code.Contains("IsCollisionValid") || code.Contains(".impulse")))
            {
                raw = (raw == null ? "" : raw + ", ") + name;
            }
        }
        Check(listeners >= 4, $"car contact: the driving test, crashes, jobs and outings listen to the car's collisions ({listeners} found)");
        Check(unjudged == null, "car contact: every collision of the player's car is judged through CarContacts.Judge" + (unjudged != null ? " (not: " + unjudged + ")" : ""));
        Check(raw == null, "car contact: no file but CarContacts reads a collision's speed or the game's ground test" + (raw != null ? " (" + raw + ")" : ""));
    }
}
