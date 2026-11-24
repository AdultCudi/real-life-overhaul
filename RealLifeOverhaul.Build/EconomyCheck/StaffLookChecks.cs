using System;
using System.Collections.Generic;
using RealLifeOverhaul;

// The people of the pool who stand for somebody who works at a place of the player's (a Car Showroom's salesperson, a data
// centre's technicians, the people at a fitted floor's desks) are the same people at every visit (People/StaffLookRules): a
// player wrote that their employees changed weight, skin, hair and sex every time they went in and out (24 Nov 2026). Each
// had been whoever the pool had free, in a look of the free roll that the shelf changes after three uses. Checked here: the
// seed (the same on every machine, by what the person is), the shelf's order (their own look kept and handed back as it is,
// anybody's taken first for anybody), a shelf played through thirty visits, and the sources: who is asked for by seed, the
// roll from it with the game's own random sequence put back, and nothing of a look chosen by the mod.
internal static partial class Program
{
    private static void StaffLooks()
    {
        StaffLookSeeds();
        StaffLookShelf();
        StaffLookVisits();
        StaffLookSources();
    }

    // FNV-1a as People/PersonLook.SeedOf writes it, worked here apart: a change of the seed's arithmetic would give every
    // save's staff another face.
    private static int StaffFnv(string text)
    {
        unchecked
        {
            uint hash = 2166136261u;
            for (int i = 0; i < text.Length; i++)
            {
                hash = (hash ^ text[i]) * 16777619u;
            }
            int seed = (int)(hash & 0x7fffffffu);
            return seed == 0 ? 1 : seed;
        }
    }

    private static void StaffLookSeeds()
    {
        const string id = "Qm9iIHRoZSBzYWxlc21hbg==";   // an id as the game writes one (a base64 uuid)
        int seed = StaffLookRules.Of(StaffLookRules.Employee, id);
        Check(seed == StaffFnv("employee|" + id) && seed == StaffLookRules.Of(StaffLookRules.Employee, id) && seed > 0,
            "staff looks: an employee's seed is FNV-1a over their id, the same every time and on every machine, never 0 or negative");
        Check(StaffLookRules.Of(StaffLookRules.Employee, id + "x") != seed && StaffLookRules.Of(StaffLookRules.Sales, id) != seed
              && StaffLookRules.Of(StaffLookRules.Technician, "5th Ave~12#0") != StaffLookRules.Of(StaffLookRules.Technician, "5th Ave~12#1")
              && StaffLookRules.Of(StaffLookRules.Technician, "5th Ave~12#0") != StaffLookRules.Of(StaffLookRules.Technician, "5th Ave~13#0")
              && StaffLookRules.Of(StaffLookRules.Desk, "77#0") != StaffLookRules.Of(StaffLookRules.Desk, "77#1")
              && StaffLookRules.Of(StaffLookRules.Desk, "77#0") != StaffLookRules.Of(StaffLookRules.Desk, "78#0"),
            "staff looks: another employee, another kind, another place, another number or another floor is another person");
        var seen = new HashSet<int>();
        int women = 0, bad = 0;
        for (int i = 0; i < 600; i++)
        {
            int s = StaffLookRules.Of(StaffLookRules.Technician, "Place " + (i / 2) + "#" + (i % 2));
            bad += s <= 0 ? 1 : 0;
            seen.Add(s);
            women += StaffLookRules.Female(s) ? 1 : 0;
            bad += StaffLookRules.Female(s) != StaffLookRules.Female(s) ? 1 : 0;
        }
        Check(bad == 0 && seen.Count >= 599, "staff looks: six hundred technicians have six hundred seeds, each above 0 (" + seen.Count + " apart)");
        Check(women >= 240 && women <= 360, "staff looks: somebody with no sex from the game has one from their seed, theirs every time, men and women about alike (" + women + " women of 600)");
    }

    private static void StaffLookShelf()
    {
        const int me = 4711, other = 9001;
        Check(StaffLookRules.Rank(0, false, 0, false, false) == 0 && StaffLookRules.Rank(0, false, 0, true, true) == 1 && StaffLookRules.Rank(0, false, me, true, false) == 2
              && StaffLookRules.Rank(0, false, me, false, true) == 2,
            "staff looks: for anybody the shelf gives a look of the free roll not yet due a change, then one due a change, and somebody's own look last");
        Check(StaffLookRules.Rank(me, true, me, true, false) == StaffLookRules.Kept && StaffLookRules.Rank(me, true, me, true, true) == StaffLookRules.Kept
              && StaffLookRules.Kept < StaffLookRules.Rank(me, true, 0, false, true) && StaffLookRules.Rank(me, true, 0, false, true) < StaffLookRules.Rank(me, true, 0, false, false)
              && StaffLookRules.Rank(me, true, 0, false, false) < StaffLookRules.Rank(me, true, other, true, false) && StaffLookRules.Rank(me, true, other, true, false) == StaffLookRules.Other
              && StaffLookRules.Rank(me, true, me, false, false) == StaffLookRules.Other,
            "staff looks: for one person the shelf gives that person as they are, then a free roll due a change, then any free roll, and another's own look last");
        Check(!StaffLookRules.Redress(me, true, me, true) && StaffLookRules.Redress(me, true, 0, true) && StaffLookRules.Redress(me, true, other, true)
              && StaffLookRules.Redress(me, true, me, false) && !StaffLookRules.Redress(0, false, me, true) && !StaffLookRules.Redress(0, false, 0, false),
            "staff looks: only one person asked for and found in another look (or of the other sex) is dressed; anybody is taken as they are");
        Check(StaffLookRules.Ages(0) && !StaffLookRules.Ages(me), "staff looks: a look of the free roll is due a change by its use, somebody's own never: it waits for them");
    }

    // A shelf of bodies played as People/ActorPool plays it, by the same rules: who is taken, who is dressed, what a
    // return leaves.
    private sealed class StaffBody
    {
        public int Seed;
        public bool Female, Out, Due;
        public int Back;      // when they came back, for the tie
        public int Uses;
    }

    private static StaffBody StaffTake(List<StaffBody> shelf, int seed, bool female, ref int dressed)
    {
        StaffBody ready = null;
        int rank = 0;
        for (int i = shelf.Count - 1; i >= 0; i--)
        {
            StaffBody b = shelf[i];
            if (b.Out)
            {
                continue;
            }
            int r = StaffLookRules.Rank(seed, female, b.Seed, b.Female, b.Due);
            if (ready == null || r < rank || (r == rank && b.Back < ready.Back))
            {
                ready = b;
                rank = r;
            }
        }
        if (ready == null)
        {
            return null;
        }
        if (StaffLookRules.Redress(seed, female, ready.Seed, ready.Female))
        {
            dressed++;
            ready.Seed = seed;
            ready.Female = female;
            ready.Uses = 0;
            ready.Due = false;
        }
        else if (seed == 0)
        {
            ready.Seed = 0;
        }
        ready.Out = true;
        return ready;
    }

    private static void StaffBack(StaffBody b, int clock)
    {
        b.Out = false;
        b.Back = clock;
        b.Uses++;
        b.Due = StaffLookRules.Ages(b.Seed) && b.Uses >= 3;   // (People/Looks.UsesBeforeChange: a look handed out this often is due a change)
    }

    private static void StaffLookVisits()
    {
        var shelf = new List<StaffBody>();
        for (int i = 0; i < 6; i++)
        {
            shelf.Add(new StaffBody { Back = i });
        }
        int sales = StaffLookRules.Of(StaffLookRules.Employee, "the one on shift");
        int clock = 10, dressed = 0, wrong = 0, afterFirst = 0, kept = 0;
        for (int visit = 0; visit < 30; visit++)
        {
            // In the showroom: the salesperson and three visitors, anybody.
            int before = dressed;
            StaffBody s = StaffTake(shelf, sales, true, ref dressed);
            var visitors = new List<StaffBody>();
            for (int v = 0; v < 3; v++)
            {
                visitors.Add(StaffTake(shelf, 0, false, ref dressed));
            }
            wrong += s == null || s.Seed != sales || !s.Female ? 1 : 0;
            foreach (StaffBody v in visitors)
            {
                wrong += v == null || v.Seed != 0 || ReferenceEquals(v, s) ? 1 : 0;
            }
            afterFirst += visit > 0 ? dressed - before : 0;
            StaffBack(s, clock++);
            foreach (StaffBody v in visitors)
            {
                StaffBack(v, clock++);
            }
            // Out in the street between two visits: a scene of four, anybody; the shelf's tending gives those due a change a new free roll.
            var scene = new List<StaffBody>();
            for (int k = 0; k < 4; k++)
            {
                scene.Add(StaffTake(shelf, 0, false, ref dressed));
            }
            foreach (StaffBody b in scene)
            {
                wrong += b == null ? 1 : 0;
                StaffBack(b, clock++);
            }
            foreach (StaffBody b in shelf)
            {
                if (b.Due && !b.Out)
                {
                    b.Due = false;
                    b.Uses = 0;
                    b.Seed = 0;   // (ActorPool.Redress: a new look of the free roll)
                }
                kept += b.Seed == sales ? 1 : 0;
            }
        }
        Check(wrong == 0 && dressed == 1 && afterFirst == 0 && kept == 30,
            "staff looks: over thirty visits with street scenes between, the salesperson is dressed once, is that person at every visit, waits on the shelf between them and is never "
            + "handed out as a visitor or to a scene while anybody else is free (" + dressed + " dressing(s), " + afterFirst + " after the first visit, " + wrong + " wrong)");

        // A scene that needs every body takes the one waiting as they are: anybody from then on, and the person is dressed once more at the next visit.
        dressed = 0;
        var all = new List<StaffBody>();
        for (int k = 0; k < 6; k++)
        {
            all.Add(StaffTake(shelf, 0, false, ref dressed));
        }
        int still = 0;
        foreach (StaffBody b in all)
        {
            still += b != null && b.Seed == sales ? 1 : 0;
            StaffBack(b, clock++);
        }
        StaffBody again = StaffTake(shelf, sales, true, ref dressed);
        Check(still == 0 && dressed == 1 && again != null && again.Seed == sales,
            "staff looks: a scene that needs every body takes the one waiting too, as anybody and with no look built; the person is dressed once more when next asked for");
    }

    private static void StaffLookSources()
    {
        string pool = ModSource(@"People\ActorPool.cs"), dress = ModSource(@"People\ActorPool.Dress.cs"), role = ModSource(@"People\ActorRole.cs");
        string take = MemberText(pool, "private StreetActor Take(ActorLook look, Vector3 position, bool mayReclaim, int seed = 0, bool female = false)");
        Check(take.Contains("StaffLookRules.Rank(seed, female, actor.Seed, actor.Female, Due(actor))") && take.Contains("StaffLookRules.Redress(seed, female, ready.Seed, ready.Female)")
              && take.Contains("Dress(ready, look, seed, female);") && take.Contains("Dress(created, look, seed, female);") && take.Contains("Dress(spare, look, seed, female);")
              && take.Contains("StaffLookRules.Redress(seed, female, yielding.Seed, yielding.Female)") && Occurs(take, "Seed = 0;") == 2,
            "staff looks: the pool takes a civilian off its shelf by the rules, dresses the one taken only when another person is wanted, and on every way to a body (the shelf, a new "
            + "one, a spare, one taken back from the street) dresses for the seed asked");
        Check(MemberText(pool, "public void Release(StreetActor actor)").Contains("actor.Stale = StaffLookRules.Ages(actor.Seed) && Looks.Worn(actor.Uses, actor.DressedAt);"),
            "staff looks: somebody's own look is not worn out on the shelf: it waits for them");
        string ready = MemberText(pool, "public bool ReadyAs(int seed, bool female)");
        Check(ready.Contains("!StaffLookRules.Redress(seed, female, actor.Seed, actor.Female)") && ready.Contains("return Looks.Free;"),
            "staff looks: a person can be put out without a frame that shows when they wait on the shelf as they are, or when a look may be built");
        string dressing = MemberText(dress, "private static void Dress(StreetActor actor, ActorLook look, int seed = 0, bool female = false)");
        Check(dressing.Contains("actor.Seed = look == ActorLook.Civilian ? seed : 0;") && dressing.Contains("actor.Female = female;") && dressing.Contains("else if (seed != 0)")
              && dressing.Contains("OwnLook(appearance, seed, female);") && dressing.Contains("appearance.RandomizeElements(gender, SkinColorHelper.GetRandom(), CasualWear);"),
            "staff looks: a body remembers whose look it wears; anybody's look is the free roll it was");
        string own = MemberText(dress, "private static void OwnLook(AppearanceSetter appearance, int seed, bool female)");
        int kept = own.IndexOf("Random.State dice = Random.state;", StringComparison.Ordinal), seeded = own.IndexOf("Random.InitState(seed);", StringComparison.Ordinal);
        int rolled = own.IndexOf("appearance.RandomizeElements(female ? Gender.Female : Gender.Male, SkinColorHelper.GetRandom(seed), CasualWear, seed);", StringComparison.Ordinal);
        int last = own.IndexOf("finally", StringComparison.Ordinal), back = own.IndexOf("Random.state = dice;", StringComparison.Ordinal);
        int fresh = own.IndexOf("AsNew(appearance);", StringComparison.Ordinal);
        Check(fresh >= 0 && fresh < kept && kept < seeded && seeded < rolled && rolled < last && last < back,
            "staff looks: one person's look is the game's own roll, every draw of it from their seed, on a body put back as the game makes one, and the game's random sequence is put back "
            + "as it was whatever happens");
        string asNew = MemberText(dress, "private static void AsNew(AppearanceSetter appearance)") + MemberText(dress, "private static void AtRest(BlendshapeWrapper shapes)");
        Check(asNew.Contains("data.elements.Clear();") && Occurs(asNew, "AtRest(appearance.Blendshapes);") == 2 && asNew.Contains("shapes.ResetAllBlendShapes();"),
            "staff looks: before one person's look a body has no piece and no shape of a face left from whoever it was, by the game's own reset for either sex");
        string chosen = own + asNew;
        Check(chosen.Length > 0 && !chosen.Contains("variantId") && !chosen.Contains("colorId") && !chosen.Contains(".fatness") && !chosen.Contains(".strength") && !chosen.Contains(".color")
              && !chosen.Contains("GetElementVariants(") && !chosen.Contains("RandomizeElement(") && !chosen.Contains("sharedMesh") && !chosen.Contains("Material"),
            "staff looks: nothing of a look is chosen by the mod: no piece, colour, build, skin, mesh or material is named where a person's own look is made");
        Check(role.Contains("public bool EnterAs(Vector3 near, int seed, bool female, float radius, bool onRoad)")
              && role.Contains("public bool EnterPresenceAs(Vector3 near, int seed, bool female, float radius, bool onRoad)")
              && role.Contains("pool.SpawnPresenceAs(near, seed, female, radius, onRoad) : pool.SpawnAs(near, seed, female, radius, onRoad)"),
            "staff looks: a scene's role asks the pool for one person by their seed");

        string floor = ModSource(@"Economy\Dealer\Floor\ShowroomFloor.cs"), director = ModSource(@"Economy\Dealer\DealerDirector.Floor.cs"), game = ModSource(@"Economy\Dealer\DealerGame.cs");
        string staff = MemberText(floor, "private void Staff(bool staffed)");
        Check(staff.Contains("director.FloorSales(_yard, out int seed, out bool female);") && staff.Contains("_sales.EnterPresenceAs(_home, seed, female, 1.5f, false)")
              && staff.Contains("if (_sales.Present && seed != _salesSeed)") && staff.Contains("_salesSeed = seed;") && !floor.Contains("_sales.EnterPresence(") && !floor.Contains("_sales.Enter("),
            "staff looks: the salesperson on a Car Showroom's floor is asked for by seed on every way onto it, and changes only when the shift is another's");
        string who = MemberText(director, "internal void FloorSales(DealerYard y, out int seed, out bool female)");
        Check(who.Contains("DealerGame.OnShift(reg, out string id, out female)") && who.Contains("seed = StaffLookRules.Of(StaffLookRules.Employee, id);")
              && who.Contains("seed = StaffLookRules.Of(StaffLookRules.Sales, y.Place);") && who.Contains("female = StaffLookRules.Female(seed);"),
            "staff looks: they are the employee on shift, by the game's own id and of the game's own sex for them; the showroom's own person when nobody is on shift this moment");
        string shift = MemberText(game, "public static bool OnShift(BuildingRegistration reg, out string id, out bool female)");
        Check(shift.Contains("Selling(e, reg)") && shift.Contains("id = e.id;") && shift.Contains("e.characterData.gender == BigAmbitions.Characters.Gender.Female")
              && MemberText(game, "private static void Staff(BuildingRegistration reg, out int count, out int skill)").Contains("if (!Selling(e, reg))") && !shift.Contains(" = new ")
              && !shift.Contains("characterData.gender = "),
            "staff looks: the one on shift is the first, in the save's order, of the very salespeople the hour counts, read and never written");

        string room = ModSource(@"Tech\Sites\ServerRoomView.cs");
        string cast = MemberText(room, "private void Cast(float now)");
        Check(room.Contains("_technicians[i].Seed = StaffLookRules.Of(StaffLookRules.Technician, Place + \"#\" + i.ToString(CultureInfo.InvariantCulture));") && cast.Contains("int seed = t.Seed;")
              && cast.Contains("pool.ReadyAs(seed, female)") && cast.Contains("t.Role.EnterPresenceAs(at, seed, female, 0.5f, onRoad: false)") && !room.Contains("t.Role.EnterPresence(")
              && !room.Contains("t.Role.Enter("),
            "staff looks: a data centre's technicians are asked for by its place and their number, when they wait as they are or a look may be built");

        string fit = ModSource(@"FitOut\FitCast.cs"), view = ModSource(@"FitOut\FitView.cs");
        Check(fit.Contains("Add(Crew.AtDesks, FitUse.Desk, false, seed);") && fit.Contains("Add(Crew.InMeeting, FitUse.Meeting, false, seed);") && fit.Contains("Add(Crew.InLounge, FitUse.Lounge, true, 0);")
              && fit.Contains("Add(Crew.Waiting, FitUse.Waiting, true, 0);") && fit.Contains("Add(Crew.Walking, FitUse.None, true, 0);")
              && fit.Contains("int who = presence ? 0 : StaffLookRules.Of(StaffLookRules.Desk,") && fit.Contains("return m.Role.EnterAs(near, m.Seed, StaffLookRules.Female(m.Seed), 0.8f, onRoad: false);")
              && fit.Contains("if (hidden && Ready(m))") && fit.Contains("&& DoorClear() && Ready(m))"),
            "staff looks: those at a fitted floor's desks and meeting tables are its own people by the floor's seed and their number; its visitors are anybody");
        Check(view.Contains("_cast.Begin(r.Kind, _seatsView, StopsView(r), r.Door, r.Seed);") && view.Contains("_cast.Begin(_request.Kind, _seatsView, _stopsView, _request.Door, _request.Seed);")
              && Occurs(view, "_cast.Begin(") == 2,
            "staff looks: the floor hands its seed to its people each time they are put out (as it stands, and when it is shown again)");
    }
}
