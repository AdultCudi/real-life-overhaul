using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// Whose a car is, and which car of the traffic pool may be put on the road (Core/WhoseCar.cs): in a Going Public session
// another player's car is never the player's (so never towed, impounded or billed for), a car left behind is brought to the
// player only when it is theirs, and a car of the pool is asked for only when the game would take an idle one of that very
// prefab, never one in use taken by its model.
internal static partial class Program
{
    // A car of a made-up pool, as the game's TrafficManager.LoadVehicle sees it: its prefab (an object, with a name) and
    // whether it is on the road.
    private sealed class Prefab
    {
        public readonly string Name;
        public Prefab(string name) { Name = name; }
    }

    private sealed class Slot
    {
        public Prefab Prefab;
        public bool Active;
        public bool Held;   // driving for a scene of the mod's, a fare, a private driver: in use
    }

    // The game's choice, as LoadVehicle makes it (ExternalPlugins/GleyTrafficSystem/TrafficManager.cs): the first idle car of
    // that very prefab, else the first car whose prefab has that name, whatever it is doing.
    private static int GameLoads(List<Slot> pool, Prefab prefab)
    {
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Prefab == prefab && !pool[i].Active)
            {
                return i;
            }
        }
        for (int i = 0; i < pool.Count; i++)
        {
            if (pool[i].Prefab.Name == prefab.Name)
            {
                return i;
            }
        }
        return -1;
    }

    private static readonly Func<Slot, object> SlotPrefab = s => s.Prefab;
    private static readonly Func<Slot, bool> SlotActive = s => s.Active;

    private static void Cars()
    {
        // ---- whose a car is (GoingPublic.MineVehicle).
        Check(!WhoseCar.Mine(false, false, false, true, true) && !WhoseCar.Mine(false, true, true, true, true), "cars: no vehicle entry, never the player's");
        Check(WhoseCar.Mine(true, false, false, false, false), "cars: alone (or a pretended session) every car of the game's list is the player's");
        Check(WhoseCar.Mine(true, true, false, true, false), "cars: in a session a car on the save's own list is the player's");
        Check(WhoseCar.Mine(true, true, false, false, true), "cars: in a session the player's private driver's car is the player's");
        Check(!WhoseCar.Mine(true, true, false, false, false), "cars: in a session a car on neither list (Going Public's copy of another player's car, 'BAMP_') is not the player's");
        Check(WhoseCar.Mine(true, true, true, false, false), "cars: with no save loaded nothing can be told, and the car is taken as the player's");

        // ---- a car left behind (hospital, custody, prison) is brought round only when it is the player's own.
        int brought = 0;
        foreach (bool has in new[] { false, true })
        {
            foreach (bool carried in new[] { false, true })
            {
                foreach (bool mine in new[] { false, true })
                {
                    if (WhoseCar.BringLater(has, carried, mine))
                    {
                        brought++;
                        Check(has && !carried && mine, "cars: brought round only when there is a car, not carried, and the player's");
                    }
                }
            }
        }
        Check(brought == 1, "cars: exactly one case of eight brings a car round: " + brought);
        bool foreign = WhoseCar.BringLater(true, false, WhoseCar.Mine(true, true, false, false, false));
        Check(!foreign, "cars: another player's car the player crashed or was arrested in is left where it stopped, never towed or billed for");

        // ---- a car of the pool: asked for only when the game would take an idle one of that very prefab.
        var sedan = new Prefab("Sedan");
        var sedanTwin = new Prefab("Sedan");   // another object of the same name (Going Public's pool reference case)
        var ambulance = new Prefab("Ambulance");
        var taxi = new Prefab("Taxi");
        Prefab[] kinds = { sedan, sedanTwin, ambulance, taxi };
        var random = new Random(20261013);
        int asked = 0, refused = 0, wouldYank = 0, wrong = 0, missed = 0;
        var pool = new List<Slot>();
        for (int round = 0; round < 20000; round++)
        {
            pool.Clear();
            int count = random.Next(0, 9);
            for (int i = 0; i < count; i++)
            {
                bool active = random.Next(3) > 0;
                pool.Add(new Slot { Prefab = kinds[random.Next(kinds.Length)], Active = active, Held = active && random.Next(2) == 0 });
            }
            Prefab want = kinds[random.Next(kinds.Length)];
            int free = WhoseCar.FreeSlot(pool, want, SlotPrefab, SlotActive);
            int game = GameLoads(pool, want);
            if (free >= 0)
            {
                asked++;
                // The game then takes an idle car of that very prefab: nothing on the road, nothing in use.
                if (game < 0 || pool[game].Active || pool[game].Prefab != want)
                {
                    wrong++;
                }
            }
            else
            {
                refused++;
                bool anyIdle = false;
                foreach (Slot s in pool)
                {
                    anyIdle |= s.Prefab == want && !s.Active;
                }
                if (anyIdle)
                {
                    missed++;
                }
                if (game >= 0 && pool[game].Active)
                {
                    wouldYank++;   // what the game would have done had it been asked: a car on the road, taken by its name
                }
            }
        }
        Check(wrong == 0, "cars: whenever a car is asked for, the game takes an idle one of that very prefab (" + asked + " asked, " + wrong + " otherwise)");
        Check(missed == 0, "cars: a car is never refused while an idle one of that prefab is in the pool (" + missed + " missed)");
        Check(wouldYank > 0, "cars: the refusals matter: asked anyway, the game would have taken a car on the road by its name " + wouldYank + " times of " + refused);

        // The cases by hand.
        pool.Clear();
        pool.Add(new Slot { Prefab = ambulance, Active = true, Held = true });    // the other unit's, at its scene
        pool.Add(new Slot { Prefab = ambulance, Active = true });                 // driving in view
        Check(WhoseCar.FreeSlot(pool, ambulance, SlotPrefab, SlotActive) < 0 && GameLoads(pool, ambulance) == 0,
            "cars: with every ambulance out, none is asked for (the game would take the other unit's, the first of that name)");
        pool.Add(new Slot { Prefab = ambulance, Active = false });
        Check(WhoseCar.FreeSlot(pool, ambulance, SlotPrefab, SlotActive) == 2 && GameLoads(pool, ambulance) == 2, "cars: with one idle, that one is the one the game takes");
        pool.Clear();
        pool.Add(new Slot { Prefab = sedanTwin, Active = false });
        Check(WhoseCar.FreeSlot(pool, sedan, SlotPrefab, SlotActive) < 0, "cars: an idle car of another prefab of the same name is not that prefab's");
        Check(WhoseCar.FreeSlot(null, sedan, SlotPrefab, SlotActive) < 0 && WhoseCar.FreeSlot(pool, null, SlotPrefab, SlotActive) < 0, "cars: no pool or no prefab, nothing asked for");
    }
}
