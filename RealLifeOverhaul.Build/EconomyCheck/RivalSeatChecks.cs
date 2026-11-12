using System;
using System.Collections.Generic;
using System.Text;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;

// The rivals' businesses stand on the game's own rival businesses (RivalSeatsGame in the game, a fake here): an outlet's crew are the game's people (their id, name, skill, the
// game's wage for the skill, the hours it expects), its rent the building's own, re-read every day, and the game's rival is never changed. Where the game has no rival
// business of the kind in the district the outlet is a seeded stand-in, and says so. (Review 2, must-fix 2: the outlets had anonymous crews, a flat rent and flat pay.)
internal static partial class Program
{
    // What the game holds, as a book of rival businesses by district and type.
    private sealed class FakeSeats : IRivalSeats
    {
        public readonly List<KeyValuePair<string, RivalSeat>> Book = new List<KeyValuePair<string, RivalSeat>>();   // "district|type" and the business
        public int Finds, Reads;

        public RivalSeat Find(string district, string type, IList<string> taken, int pick)
        {
            Finds++;
            var c = new List<RivalSeat>();
            foreach (var one in Book)
            {
                if (one.Key == district + "|" + type && !taken.Contains(one.Value.Key))
                {
                    c.Add(one.Value);
                }
            }
            c.Sort((x, y) => string.CompareOrdinal(x.Key, y.Key));
            return c.Count == 0 ? null : Copy(c[Math.Min(c.Count - 1, pick * c.Count / 1000)]);
        }

        public RivalSeat Read(string key)
        {
            Reads++;
            foreach (var one in Book)
            {
                if (one.Value.Key == key)
                {
                    return Copy(one.Value);
                }
            }
            return null;
        }

        private static RivalSeat Copy(RivalSeat s)
        {
            var c = new RivalSeat { Key = s.Key, Name = s.Name, Rent = s.Rent };
            foreach (RivalHand h in s.Hands)
            {
                c.Hands.Add(new RivalHand { Id = h.Id, Name = h.Name, Skill = h.Skill, Hourly = h.Hourly, WeekHours = h.WeekHours });
            }
            return c;
        }

        public static RivalSeat Make(string key, int rent, int people, string tag)
        {
            var s = new RivalSeat { Key = key, Name = "Rival " + tag, Rent = rent };
            for (int i = 0; i < people; i++)
            {
                s.Hands.Add(new RivalHand { Id = tag + "-" + i + "=", Name = "Hand " + tag + i, Skill = 35 + i * 9, Hourly = 1500 + i * 220, WeekHours = 30 + i * 5 });
            }
            return s;
        }

        public string Snapshot()
        {
            var b = new StringBuilder();
            foreach (var one in Book)
            {
                b.Append(one.Key).Append('/').Append(one.Value.Key).Append('/').Append(one.Value.Rent);
                foreach (RivalHand h in one.Value.Hands)
                {
                    b.Append(',').Append(h.Id).Append(':').Append(h.Name).Append(':').Append(h.Skill).Append(':').Append(h.Hourly).Append(':').Append(h.WeekHours);
                }
                b.Append(';');
            }
            return b.ToString();
        }
    }

    private static void RivalSeatChecks()
    {
        int before = _run, failedBefore = _failed;
        var seats = new FakeSeats();
        seats.Book.Add(new KeyValuePair<string, RivalSeat>("Midtown|restaurant", FakeSeats.Make("Main Street#12", 85, 4, "a")));
        seats.Book.Add(new KeyValuePair<string, RivalSeat>("Midtown|restaurant", FakeSeats.Make("Oak Road#3", 0, 3, "b")));
        seats.Book.Add(new KeyValuePair<string, RivalSeat>("Harbour|restaurant", FakeSeats.Make("Quay#1", 60, 2, "c")));
        string untouched = seats.Snapshot();

        // An outlet opens on a game rival's business of its kind in its district; two outlets never share one.
        RivalFile f = RivalFirmFile(901, 0, 2);
        f.Moves[1].Type = "bakery";   // (the game has no rival bakery in Midtown: a stand-in)
        RivalFirms.Sync(f, f.Seed, 100, 30, RunIndex, seats);
        RivalOutlet o = f.Outlets.Count > 0 ? f.Outlets[0] : null, stand = f.Outlets.Count > 1 ? f.Outlets[1] : null;
        RivalSeat book = o != null ? seats.Read(o.Seat) : null;
        Check(o != null && book != null && o.Seat.Length > 0 && o.Crew.Count == book.Hands.Count && o.Crew.TrueForAll(p => book.Hands.Exists(h => h.Id == p.Id)),
            "rival seats: an outlet stands on a game rival's business of its kind in its district, and its crew are that business's own people, by the game's ids (" + (o != null ? o.Seat : "none") + ")");
        Check(o != null && book != null && o.Crew.TrueForAll(p => p.Name.StartsWith("Hand ", StringComparison.Ordinal) && !p.Id.StartsWith("rival", StringComparison.Ordinal)),
            "rival seats: they have the game's names, not anonymous ids");
        bool pay = o != null && book != null;
        for (int i = 0; pay && i < o.Crew.Count; i++)
        {
            RivalHand h = book.Hands.Find(x => x.Id == o.Crew[i].Id);
            pay &= o.Crew[i].Base == h.Hourly && o.Crew[i].Wage == PaidWage.Cents(h.Hourly, RunIndex, 0) && o.Crew[i].Skill == h.Skill && o.Crew[i].DayHours == Math.Max(400, Math.Min(1000, h.WeekHours * 100 / 5));
        }
        Check(pay, "rival seats: their skill, their pay (the game's wage for the skill, at today's prices) and their hours are the game's");
        Check(o != null && book != null && (book.Rent > 0 ? o.Lease.Rent == book.Rent : o.Lease.Rent == CompanyCatalogue.AtPrices(RivalFirms.RentBase, RunIndex)),
            "rival seats: the outlet's rent is the building's own where the game gives one, and the seeded rent where it does not (" + (o != null ? o.Lease.Rent.ToString() : "-") + " a day)");
        Check(stand != null && stand.Seat.Length == 0 && stand.Crew.Count >= RivalFirms.CrewStart && stand.Crew.TrueForAll(p => p.Id.StartsWith("rival", StringComparison.Ordinal)),
            "rival seats: where the game has no rival business of the kind there the outlet is a seeded stand-in, and says so (no seat, anonymous ids)");
        RivalFile two = RivalFirmFile(902, 1, 2);
        RivalFirms.Sync(two, two.Seed, 100, 30, RunIndex, seats);
        Check(two.Outlets.Count == 2 && two.Outlets[0].Seat != two.Outlets[1].Seat && two.Outlets[0].Seat.Length > 0 && two.Outlets[1].Seat.Length > 0, "rival seats: two outlets in a district take two businesses, never the same one");

        // Twelve hundred days: the game's crew read every day; the mod's lateness, raises and let-go run on them; the game's own rival is never changed.
        var feed = new List<string>();
        long paid = 0, worth = f.Tycoons[0].Worth;
        for (int day = 100; day < 1300; day++)
        {
            paid += RivalFirms.Day(f, f.Seed, day, 30, 20, RunIndex + (day > 400 ? 12 : 0), (d, r) => feed.Add(d + ":" + r), seats);
            RivalSeat now = seats.Read(o.Seat);
            if (now != null)
            {
                foreach (StaffFile p in o.Crew)
                {
                    if (!now.Hands.Exists(h => h.Id == p.Id) || o.Out.Contains(p.Id))
                    {
                        Check(false, "rival seats: the crew is only the game's people, and none the firm let go (" + p.Id + " on day " + day + ")");
                        day = 1300;
                        break;
                    }
                }
            }
        }
        Check(seats.Snapshot() == untouched, "rival seats: the game's rival business is read and never changed (its people, wages and rent are as they were)");
        Check(o.LetGo >= 1 && o.Out.Count >= 1 && o.Out.Count <= RivalFirms.OutMost && o.Crew.Count == book.Hands.Count - o.Out.Count && o.Crew.TrueForAll(p => !o.Out.Contains(p.Id)),
            "rival seats: somebody late three days running is let go in the firm's books only, and is not read back from the game's crew (" + o.LetGo + " let go, " + o.Out.Count + " remembered, " + o.Crew.Count + " on the crew)");
        Check(o.Raises >= 0 && o.Hires == book.Hands.Count && o.Wages > 0 && o.Rent > 0 && o.Renewals >= 1 && stand.Hires - stand.LetGo == stand.Crew.Count,
            "rival seats: the lease runs and renews over the building's rent, the crew's wages are paid, and the stand-in still fills its place (" + o.Hires + " hired, " + o.Renewals + " renewals)");
        Check(o.Rent > 0 && paid == f.Outlets[0].Rent + f.Outlets[0].Wages + f.Outlets[1].Rent + f.Outlets[1].Wages && f.Tycoons[0].Worth == worth - paid,
            "rival seats: what they pay comes off the tycoon's worth as before (" + paid + " in rent and wages)");

        // The game's crew changes: a person who leaves it (poached by the player, replaced by the game) leaves the outlet without being let go; a new one is hired; the rent follows.
        RivalFile g = RivalFirmFile(903, 0, 1);
        RivalFirms.Sync(g, g.Seed, 100, 30, RunIndex, seats);
        RivalOutlet og = g.Outlets[0];
        string gone = og.Crew[0].Id;
        int letGo = og.LetGo, hires = og.Hires;
        RivalSeat live = seats.Book.Find(x => x.Value.Key == og.Seat).Value;
        live.Hands.RemoveAt(live.Hands.FindIndex(h => h.Id == gone));
        live.Hands.Add(new RivalHand { Id = "new=", Name = "Newcomer", Skill = 50, Hourly = 1800, WeekHours = 40 });
        live.Rent = 120;
        RivalFirms.Day(g, g.Seed, 101, 30, 0, RunIndex, null, seats);
        Check(og.Crew.TrueForAll(p => p.Id != gone) && og.Crew.Exists(p => p.Id == "new=") && og.LetGo == letGo && og.Hires == hires + 1 && !og.Out.Contains(gone) && og.Lease.Rent == 120,
            "rival seats: a person who has left the game's crew leaves the outlet without being let go, a new one is hired by their own id, and the rent follows the building");
        // Somebody let go stays out while the game's crew holds them, however many are let go: the ids are forgotten only when the game's crew no longer has the person.
        Check(RivalFirms.OutMost >= RivalFirms.CrewMost * 2, "rival seats: the let-go ids are bounded well past the crew, so that a ninth let go cannot bring the oldest back");
        og.Out.Clear();
        og.Crew.Clear();   // (each of them let go: off the crew and into the book of those kept out)
        foreach (RivalHand h in live.Hands)
        {
            og.Out.Add(h.Id);
        }
        int heldOut = live.Hands.Count;
        og.Out.Add("someone-the-game-lost=");
        for (int n = 0; n < 12; n++)
        {
            live.Hands.Add(new RivalHand { Id = "extra" + n + "=", Name = "Extra" + n, Skill = 40, Hourly = 1500, WeekHours = 40 });
        }
        RivalFirms.Day(g, g.Seed, 102, 30, 0, RunIndex, null, seats);
        Check(!og.Out.Contains("someone-the-game-lost=") && live.Hands.Count > RivalFirms.CrewMost && og.Crew.TrueForAll(p => !og.Out.Contains(p.Id)) && og.Out.Count == heldOut,
            "rival seats: a let-go person the game no longer holds is forgotten, those it still holds stay out of the book (" + og.Out.Count + " kept, held " + heldOut + ", lost " + og.Out.Contains("someone-the-game-lost=") + ", hands " + live.Hands.Count + ", crewOk " + og.Crew.TrueForAll(p => !og.Out.Contains(p.Id)) + ")");
        seats.Book.RemoveAll(x => x.Value.Key == og.Seat);
        RivalFirms.Day(g, g.Seed, 102, 30, 0, RunIndex, null, seats);
        Check(og.Seat.Length == 0 && og.Crew.Count >= RivalFirms.CrewStart, "rival seats: when the game's business is gone the outlet goes on as a seeded stand-in with the crew and rent it has");

        // Same seed, same days; the save keeps the seat and those let go; an older line loads as a stand-in.
        seats.Book.Clear();
        seats.Book.Add(new KeyValuePair<string, RivalSeat>("Midtown|restaurant", FakeSeats.Make("Main Street#12", 85, 4, "a")));
        RivalFile h1 = RivalFirmFile(777, 0, 1), h2 = RivalFirmFile(777, 0, 1);
        RivalFirms.Sync(h1, h1.Seed, 100, 30, RunIndex, seats);
        RivalFirms.Sync(h2, h2.Seed, 100, 30, RunIndex, seats);
        for (int day = 100; day < 900; day++)
        {
            RivalFirms.Day(h1, h1.Seed, day, 30, 20, RunIndex, null, seats);
            RivalFirms.Day(h2, h2.Seed, day, 30, 20, RunIndex, null, seats);
        }
        string line = RivalStore.Write(h1);
        RivalFile back = RivalStore.Read(line, out int version);
        Check(RivalStore.Write(h2) == line && back != null && back.Outlets.Count == 1 && back.Outlets[0].Seat == h1.Outlets[0].Seat && back.Outlets[0].Out.Count == h1.Outlets[0].Out.Count && RivalStore.Write(back) == line,
            "rival seats: the same seed lives the same days on the same game, and the save keeps the seat and those let go, and writes the same line back");
        // The days after a load go as the days without one: the save and a fresh read of the game give the same crew.
        for (int day = 900; day < 1000; day++)
        {
            RivalFirms.Day(h1, h1.Seed, day, 30, 20, RunIndex, null, seats);
            RivalFirms.Day(back, back.Seed, day, 30, 20, RunIndex, null, seats);
        }
        Check(back.Outlets[0].Crew.Count == h1.Outlets[0].Crew.Count && back.Outlets[0].Lease.End == h1.Outlets[0].Lease.End, "rival seats: a save written and read back lives the next hundred days as the one that was not saved (" + back.Outlets[0].Crew.Count + " on the crew)");
        string[] fields = line.Substring(line.IndexOf("\nO|", StringComparison.Ordinal) + 1).Split('\n')[0].Split('|');
        string oldLine = line.Substring(0, line.IndexOf("\nO|", StringComparison.Ordinal)) + "\n" + string.Join("|", fields, 0, 15);
        RivalFile oldFile = RivalStore.Read(oldLine, out version);
        Check(oldFile != null && oldFile.Outlets.Count == 1 && oldFile.Outlets[0].Seat.Length == 0 && oldFile.Outlets[0].Out.Count == 0 && oldFile.Outlets[0].Crew.Count == h1.Outlets[0].Crew.Count,
            "rival seats: an outlet saved before seats loads as a stand-in with its crew, and plays on");
        // No seats at all (the bridge not wired, an old game): every outlet is a stand-in.
        RivalFile none = RivalFirmFile(777, 0, 1);
        RivalFirms.Sync(none, none.Seed, 100, 30, RunIndex, null);
        Check(none.Outlets.Count == 1 && none.Outlets[0].Seat.Length == 0 && none.Outlets[0].Crew.Count >= RivalFirms.CrewStart, "rival seats: with nothing handed in the outlet is a seeded stand-in, as before");
        Console.WriteLine("  the rivals' businesses on the game's own: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }
}
