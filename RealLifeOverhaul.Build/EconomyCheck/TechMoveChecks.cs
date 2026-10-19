using System;
using System.Collections.Generic;
using System.Diagnostics;
using RealLifeOverhaul.Tech;

// A tech firm moved by the game's moving company (Tech/Firms/MoveRules.cs, MovesGame.cs, TechDirector.Moves.cs). A player:
// "moving the tech company erases all financial data, including products and sales/revenue". The Tech parts keep a firm's data
// under its building's place key; the moving company moves the business's name, type and staff to another building and shuts
// the old one (BizManTransfer), so the next read of the firms found a firm with no business and closed it with its products,
// takings and work. Now the data follows the move whole, a save that still holds it at the old building gets it back, and
// nothing follows a move with a doubt.
internal static partial class Program
{
    private const string MoveFrom = "Harbour%20Road~12", MoveTo = "Lantern%20Street~40", MoveOther = "Mill%20Lane~7";

    private static void TechMoves()
    {
        MoveTexts();
        MoveReport();
        MoveDoubts();
        MoveOver();
        MoveChain();
        MoveHit();
        MoveParts();
        MoveSave();
        MoveOrder();
        MoveTiming();
        MoveSources();
        MoveWords();
    }

    // A save of the report's kind: one firm with its products, sales, contracts, staff, an ad and investors, at the old building.
    private static void MoveSaveOf(out FirmsFile f, out TalentFile t, out HiringFile h, out VentureFile v)
    {
        f = new FirmsFile { Seed = 5 };
        var firm = new Firm { Place = MoveFrom, Kind = 0, Founded = 10, Debt = 120, Reputation = 640, RegisteredUntil = 400, RnD = 5000, Revenue30 = 0 };
        for (int d = 0; d < firm.Days.Length; d++)
        {
            firm.Days[d] = 1000 + d;
            firm.Revenue30 += firm.Days[d];
        }
        f.Firms.Add(firm);
        for (int i = 0; i < 3; i++)
        {
            f.Products.Add(new Product { Id = f.Next(), Place = MoveFrom, Name = "App " + i, Stage = 1, Users = 400 + i, Revenue = 90000 + i });
        }
        f.Contracts.Add(new Contract { Id = f.Next(), Place = MoveFrom, Stage = (int)WorkStage.Active, Value = 50000, Paid = 10000 });
        f.Contracts.Add(new Contract { Id = f.Next(), Place = MoveFrom, Stage = (int)WorkStage.Done, Value = 20000, Paid = 20000 });
        f.Contracts.Add(new Contract { Id = f.Next(), Place = MoveOther, OwnPlace = MoveFrom, Stage = (int)WorkStage.Active });
        t = new TalentFile { Seed = 3 };
        t.People.Add(new Profile { Id = "employee-1", Place = MoveFrom, Joined = 12 });
        t.People.Add(new Profile { Id = "pro:1", Place = MoveFrom, Joined = 14 });
        t.People.Add(new Profile { Id = "pro:2", Place = MoveOther, Joined = 14 });
        h = new HiringFile { Seed = 4 };
        h.Ads.Add(new JobAd { Id = 1, Place = MoveFrom, From = 18, To = 40 });
        v = new VentureFile { Seed = 6 };
        var cap = new Cap { Place = MoveFrom, Stage = 1, Post = 4000000 };
        cap.Holders.Add(new Holder { Kind = (int)HolderKind.Founders, Shares = 800000 });
        cap.Holders.Add(new Holder { Kind = (int)HolderKind.Vc, Shares = 200000, Invested = 1000000 });
        v.Caps.Add(cap);
    }

    // The player's businesses after the move: the firm at the new building under its name, the old one empty, another firm elsewhere.
    private static List<BusinessNow> MoveNow(string name = "Northwind Labs", int kind = 0)
    {
        return new List<BusinessNow>
        {
            new BusinessNow { Place = MoveTo, Kind = kind, Name = name },
            new BusinessNow { Place = MoveOther, Kind = (int)FirmKind.WebAgency, Name = "Other" }
        };
    }

    private static List<MoveSeen> MoveSeenOf(int day, string from = MoveFrom, string to = MoveTo, string name = "Northwind Labs")
    {
        return new List<MoveSeen> { new MoveSeen { From = from, To = to, Name = name, Day = day, Hour = 13 } };
    }

    // The order of TechDirector.ReadFirms with the buildings as a list: the moves followed first (FollowMoves), then a new firm for
    // a tech business with none, then a firm with no tech firm of the player's at its building closed. follow false: before the fix.
    private static int MoveRead(FirmsFile f, TalentFile t, HiringFile h, VentureFile v, List<BusinessNow> now, List<MoveSeen> seen, int day, bool follow)
    {
        int followed = 0;
        if (follow)
        {
            var loose = new List<string>();
            MoveRules.Loose(f, t, h, v, now, day, loose);
            foreach (string from in loose)
            {
                if (MoveRules.Judge(from, seen, f, t, now, out MoveSeen move, out string to) == MoveVerdict.Yes)
                {
                    MoveCount n = MoveRules.Follow(from, to, f, t, h, v, day);
                    MoveRules.Record(t, from, to, move.Day, move.Hour, MoveRules.PartsAll, n.Firm > 0, n.Closed > 0);
                    followed++;
                }
            }
        }
        foreach (BusinessNow b in now)
        {
            if (b.Kind >= 0 && f.FirmAt(b.Place) == null)
            {
                f.Firms.Add(new Firm { Place = b.Place, Kind = b.Kind, Founded = day });
            }
        }
        for (int i = f.Firms.Count - 1; i >= 0; i--)
        {
            int at = MoveRules.IndexOf(now, f.Firms[i].Place);
            if (at < 0 || now[at].Kind < 0)
            {
                FirmsRules.Close(f, f.Firms[i], day);
            }
        }
        return followed;
    }

    private static int CountAt<T>(List<T> list, Func<T, string> place, string at)
    {
        int n = 0;
        foreach (T x in list)
        {
            n += place(x) == at ? 1 : 0;
        }
        return n;
    }

    private static void MoveTexts()
    {
        Check(MoveRules.Composed("Northwind Labs (40 Lantern Street)", out string name, out string address) && name == "Northwind Labs" && address == "40 Lantern Street",
            "moves: the new building is read from the business's composed name, \"name (address)\"");
        Check(MoveRules.Composed("Bits (and) Bytes (40 Lantern Street)", out name, out address) && name == "Bits (and) Bytes" && address == "40 Lantern Street",
            "moves: a name with brackets of its own keeps them; the address is the last brackets");
        Check(!MoveRules.Composed("12 Harbour Road", out _, out _) && !MoveRules.Composed(null, out _, out _) && !MoveRules.Composed("(x)", out _, out _),
            "moves: a bare address (the old building once its business has gone) is no composed name");
        Check(MoveRules.IsDone(MoveRules.DoneKey) && MoveRules.IsDone(MoveRules.DoneSoldKey) && !MoveRules.IsDone("ba:messagetype_dialog_moving_service_contract_set_manager") && !MoveRules.IsDone(null),
            "moves: only the moving company's 'move done' messages count, not the booking");
        string key = PlaceRules.Key("Rue de l'Été 5/B", 31);
        Check(PlaceRules.Street(key, out string street) && street == "Rue de l'Été 5/B" && PlaceRules.Number(key, out int number) && number == 31,
            "moves: a place key gives back its street and number as the game names them (" + street + ")");
        Check(!PlaceRules.Street("~4", out _) && !PlaceRules.Street(null, out _) && !PlaceRules.Street("nonumber", out _), "moves: a place key that cannot be read gives no street");
    }

    // The report: the firm moved to another building, then the Tech app opened (a read of the firms).
    private static void MoveReport()
    {
        MoveSaveOf(out FirmsFile before, out TalentFile tb, out HiringFile hb, out VentureFile vb);
        MoveRead(before, tb, hb, vb, MoveNow(), MoveSeenOf(20), 21, false);
        Firm fresh = before.FirmAt(MoveTo);
        Check(before.FirmAt(MoveFrom) == null && before.Products.Count == 0 && fresh != null && fresh.Revenue30 == 0 && before.Contracts[0].Stage == (int)WorkStage.Failed,
            "moves: before the fix, the read after the move closed the firm with its 3 products and its work, and a firm with no takings stood at the new building (the report reproduced)");

        MoveSaveOf(out FirmsFile f, out TalentFile t, out HiringFile h, out VentureFile v);
        Firm firm = f.FirmAt(MoveFrom);
        long revenue = firm.Revenue30, day7 = firm.Days[7];
        int followed = MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 21, true);
        Firm moved = f.FirmAt(MoveTo);
        Check(followed == 1 && moved == firm && f.FirmAt(MoveFrom) == null && f.Firms.Count == 2, "moves: the firm's own record is the one at the new building, and no second one was made there");
        Check(moved != null && moved.Founded == 10 && moved.Debt == 120 && moved.Reputation == 640 && moved.RegisteredUntil == 400 && moved.RnD == 5000 && moved.Revenue30 == revenue && moved.Days[7] == day7,
            "moves: the firm keeps its founding day, debt, reputation, register, research costs and 30 days of takings");
        Check(f.Products.Count == 3 && CountAt(f.Products, p => p.Place, MoveTo) == 3 && f.Products[2].Revenue == 90002 && f.Products[1].Users == 401,
            "moves: its 3 products go with it, each with its users and all its takings");
        Check(f.Contracts[0].Place == MoveTo && f.Contracts[0].Stage == (int)WorkStage.Active && f.Contracts[0].Paid == 10000 && f.Contracts[1].Place == MoveTo && f.Contracts[1].Stage == (int)WorkStage.Done,
            "moves: the work under way goes on at the new building, and its finished work stays in its history");
        Check(f.Contracts[2].OwnPlace == MoveTo && f.Contracts[2].Place == MoveOther, "moves: a contract another firm does for it as a client bills it at its new building");
        Check(t.Of("employee-1").Place == MoveTo && t.Of("pro:1").Place == MoveTo && t.Of("pro:2").Place == MoveOther, "moves: its professionals' files go with it, nobody else's");
        Check(h.Ads[0].Place == MoveTo, "moves: its job ad goes with it, so a hire starts at the new building");
        Check(v.Caps.Count == 1 && v.Caps[0].Place == MoveTo && v.Caps[0].Holders.Count == 2 && v.Caps[0].Post == 4000000, "moves: its cap table, the investors and their shares, goes with it");
        Check(t.Moves.Count == 1 && t.Moves[0].From == MoveFrom && t.Moves[0].To == MoveTo && t.Moves[0].Day == 20 && t.Moves[0].Whole && !t.Moves[0].Replaced,
            "moves: the move is kept as followed, by all the parts that ran");
        var loose = new List<string>();
        MoveRules.Loose(f, t, h, v, MoveNow(), 22, loose);
        Check(loose.Count == 0, "moves: nothing is left at the old building to ask about again (" + string.Join(", ", loose) + ")");
        Check(MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 22, true) == 0 && f.Products.Count == 3 && f.Firms.Count == 2, "moves: the next day's read follows nothing twice and closes nothing");
    }

    private static MoveVerdict MoveJudge(FirmsFile f, TalentFile t, List<BusinessNow> now, List<MoveSeen> seen, out string to)
    {
        return MoveRules.Judge(MoveFrom, seen, f, t, now, out _, out to);
    }

    // Nothing follows a move with a doubt: the firm is then closed as before.
    private static void MoveDoubts()
    {
        MoveSaveOf(out FirmsFile f, out TalentFile t, out _, out _);
        Check(MoveJudge(f, t, MoveNow(), new List<MoveSeen>(), out _) == MoveVerdict.No, "moves: with no move in the moving company's messages, nothing follows");
        Check(MoveJudge(f, t, MoveNow(), MoveSeenOf(5), out _) == MoveVerdict.No, "moves: a move from the building before the firm was founded there is not its move");
        Check(MoveJudge(f, t, MoveNow("Renamed"), MoveSeenOf(20), out _) == MoveVerdict.No, "moves: a business at the new building by another name is not the one that moved");
        Check(MoveJudge(f, t, MoveNow("Northwind Labs", (int)FirmKind.WebAgency), MoveSeenOf(20), out _) == MoveVerdict.No, "moves: a business of another kind at the new building is not the firm");
        var gone = new List<BusinessNow> { new BusinessNow { Place = MoveOther, Kind = 1, Name = "Other" } };
        Check(MoveJudge(f, t, gone, MoveSeenOf(20), out _) == MoveVerdict.No, "moves: a move to a building that holds no business of the player's now is not followed");
        var back = MoveNow();
        back.Add(new BusinessNow { Place = MoveFrom, Kind = 0, Name = "Northwind Labs" });
        var loose = new List<string>();
        MoveRules.Loose(f, t, null, null, back, 21, loose);
        Check(!loose.Contains(MoveFrom), "moves: a firm whose building holds a tech firm of the player's is never asked about");
        f.Firms.Add(new Firm { Place = MoveTo, Kind = 0, Founded = 25 });
        Check(MoveJudge(f, t, MoveNow(), MoveSeenOf(20), out _) == MoveVerdict.No, "moves: a firm at the new building founded after the move leaves a doubt: nothing follows");
        MoveSaveOf(out f, out t, out HiringFile h, out VentureFile v);
        int read = MoveRead(f, t, h, v, MoveNow("Renamed"), MoveSeenOf(20), 21, true);
        Check(read == 0 && f.FirmAt(MoveFrom) == null && f.Products.Count == 0, "moves: with a doubt the firm is closed with its products, as a firm whose business went");
        MoveSaveOf(out f, out t, out _, out _);
        var other = MoveSeenOf(20, MoveOther, MoveTo);
        Check(MoveJudge(f, t, MoveNow(), other, out _) == MoveVerdict.No, "moves: a move from another building is not this firm's");
    }

    // The firm moved into a building where another firm of the player's stood: the game replaced that business, so it closes.
    private static void MoveOver()
    {
        MoveSaveOf(out FirmsFile f, out TalentFile t, out HiringFile h, out VentureFile v);
        var d = new Firm { Place = MoveTo, Kind = 0, Founded = 3, Revenue30 = 777 };
        f.Firms.Add(d);
        f.Products.Add(new Product { Id = f.Next(), Place = MoveTo, Name = "Old", Stage = 1 });
        f.Contracts.Add(new Contract { Id = f.Next(), Place = MoveTo, Stage = (int)WorkStage.Active });
        v.Caps.Add(new Cap { Place = MoveTo, Stage = 0 });
        MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 21, true);
        Check(f.FirmAt(MoveTo) != null && f.FirmAt(MoveTo).Founded == 10 && !f.Firms.Contains(d), "moves: over another firm of the player's, the moved firm stands at the building and the replaced one is closed");
        Check(f.Products.Count == 3 && CountAt(f.Products, p => p.Name, "Old") == 0 && f.Contracts[f.Contracts.Count - 1].Stage == (int)WorkStage.Failed,
            "moves: the replaced firm's product goes and its work fails, as when a business goes; the moved firm's 3 stay");
        Check(v.Caps.Count == 1 && v.Caps[0].Stage == 1 && v.Caps[0].Place == MoveTo && t.Moves[0].Replaced, "moves: the moved firm's investors hold the building's cap table; the replaced firm's goes with it");
    }

    // Moved, then moved again before the parts looked: followed to where it stands now.
    private static void MoveChain()
    {
        MoveSaveOf(out FirmsFile f, out TalentFile t, out HiringFile h, out VentureFile v);
        var seen = new List<MoveSeen>
        {
            new MoveSeen { From = MoveTo, To = MoveOther, Name = "Northwind Labs", Day = 25, Hour = 11 },
            new MoveSeen { From = MoveFrom, To = MoveTo, Name = "Northwind Labs", Day = 20, Hour = 13 }
        };
        var now = new List<BusinessNow> { new BusinessNow { Place = MoveOther, Kind = 0, Name = "Northwind Labs" } };
        Check(MoveRules.Judge(MoveFrom, seen, f, t, now, out MoveSeen move, out string to) == MoveVerdict.Yes && to == MoveOther && move.Day == 25,
            "moves: a firm moved twice is followed to the building it stands in now");
        seen.Reverse();
        Check(MoveRules.Judge(MoveFrom, seen, f, t, now, out _, out string again) == MoveVerdict.Yes && again == MoveOther, "moves: the order the messages are read in changes nothing");
    }

    // Saves already hit, read by the fixed version.
    private static void MoveHit()
    {
        // Saved after the move and before the firms were read: the data is still at the old building, and is put back.
        MoveSaveOf(out FirmsFile f, out TalentFile t, out HiringFile h, out VentureFile v);
        Check(MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 30, true) == 1 && f.FirmAt(MoveTo) != null && f.FirmAt(MoveTo).Founded == 10 && CountAt(f.Products, p => p.Place, MoveTo) == 3,
            "moves: a save that still holds the firm at its old building gets it back whole when loaded, ten days on");
        // Read before the fix: the firm and its products were closed; a firm with nothing stands at the new building.
        MoveSaveOf(out f, out t, out h, out v);
        MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 21, false);
        Check(f.Products.Count == 0 && f.FirmAt(MoveFrom) == null, "moves: in a save read before the fix the products and the firm's takings are gone, and nothing can bring them back");
        int read = MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 31, true);
        Check(read == 1 && t.Of("pro:1").Place == MoveTo && h.Ads[0].Place == MoveTo && v.CapOf(MoveTo) != null && v.CapOf(MoveFrom) == null,
            "moves: what the old read left at the old building, the professionals, the ad and the cap table, is put back with the firm");
        Check(f.Contracts[0].Place == MoveTo && f.Contracts[0].Stage == (int)WorkStage.Failed && f.FirmAt(MoveTo).Founded == 21, "moves: the work the old read failed stays failed, in the firm's history");
        // The new firm raised its own money since: its cap table stays, the old one is left as it was.
        MoveSaveOf(out f, out t, out h, out v);
        MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 21, false);
        v.Caps.Add(new Cap { Place = MoveTo, Stage = 0 });
        MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 31, true);
        Check(v.Caps.Count == 2 && v.CapOf(MoveTo).Stage == 0 && v.CapOf(MoveFrom) != null, "moves: a cap table at the new building is kept; the old one is not put over it");
        // Doubts in such a save.
        MoveSaveOf(out f, out t, out h, out v);
        MoveRead(f, t, h, v, MoveNow(), MoveSeenOf(20), 21, false);
        var twice = MoveSeenOf(20);
        twice.Add(new MoveSeen { From = MoveFrom, To = MoveOther, Name = "Other", Day = 15, Hour = 10 });
        Check(MoveJudge(f, t, MoveNow(), twice, out _) == MoveVerdict.No, "moves: data left at a building two businesses moved away from cannot be told apart: nothing follows");
        var standing = MoveNow();
        standing.Add(new BusinessNow { Place = MoveFrom, Kind = -1, Name = "Cafe" });
        Check(MoveJudge(f, t, standing, MoveSeenOf(20), out _) == MoveVerdict.No, "moves: data left at a building where a business of the player's stands again is not taken from it");
        Check(MoveJudge(null, t, MoveNow(), MoveSeenOf(20), out _) == MoveVerdict.Later, "moves: while the firms part is off, data left for a tech firm waits for it");
        MoveRules.Record(t, MoveFrom, MoveTo, 20, 13, MoveRules.PartsAll, true, false);
        Check(MoveJudge(f, t, MoveNow(), MoveSeenOf(20), out _) == MoveVerdict.No, "moves: data at a building after its move was followed is newer than the move: nothing follows it");
        // A business that is no tech firm (a client with its professionals).
        var cafe = new List<BusinessNow> { new BusinessNow { Place = MoveTo, Kind = -1, Name = "Cafe" } };
        var t2 = new TalentFile();
        t2.People.Add(new Profile { Id = "pro:9", Place = MoveFrom });
        var c = new FirmsFile();
        c.Contracts.Add(new Contract { Id = 1, Place = MoveOther, OwnPlace = MoveFrom, Stage = (int)WorkStage.Active });
        var loose = new List<string>();
        MoveRules.Loose(c, t2, null, null, cafe, 21, loose);
        Check(loose.Count == 1 && MoveRules.Judge(MoveFrom, MoveSeenOf(20, MoveFrom, MoveTo, "Cafe"), null, t2, cafe, out _, out string to) == MoveVerdict.Yes && to == MoveTo,
            "moves: a client business that moved, with the mod's professionals, is followed too, without the firms part");
    }

    // A part not running at the move follows it when it starts.
    private static void MoveParts()
    {
        MoveSaveOf(out FirmsFile f, out TalentFile t, out HiringFile h, out VentureFile v);
        MoveCount n = MoveRules.Follow(MoveFrom, MoveTo, f, t, h, null, 21);
        MoveRules.Record(t, MoveFrom, MoveTo, 20, 13, MoveRules.PartPeople | MoveRules.PartHiring | MoveRules.PartFirms, n.Firm > 0, n.Closed > 0);
        Check(v.Caps[0].Place == MoveFrom, "moves: a part that is off is not touched");
        var c = new MoveCount();
        Check(MoveRules.CatchUp(t, MoveRules.PartVenture, null, null, v, 30, ref c) == 1 && v.Caps[0].Place == MoveTo && c.Caps == 1 && (t.Moves[0].Parts & MoveRules.PartVenture) != 0,
            "moves: the startups part, switched on later, follows the move it missed");
        c = new MoveCount();
        Check(MoveRules.CatchUp(t, MoveRules.PartVenture, null, null, v, 31, ref c) == 0 && c.Total == 0, "moves: and follows it once");

        MoveSaveOf(out f, out t, out h, out v);
        n = MoveRules.Follow(MoveFrom, MoveTo, null, t, null, null, 21);
        MoveRules.Record(t, MoveFrom, MoveTo, 20, 13, MoveRules.PartPeople, false, false);
        c = new MoveCount();
        MoveRules.CatchUp(t, MoveRules.PartHiring, null, h, null, 30, ref c);
        MoveRules.CatchUp(t, MoveRules.PartFirms, f, null, null, 30, ref c);
        MoveRules.CatchUp(t, MoveRules.PartVenture, null, null, v, 30, ref c);
        Check(n.People == 2 && h.Ads[0].Place == MoveTo && f.FirmAt(MoveTo) != null && f.FirmAt(MoveTo).Founded == 10 && CountAt(f.Products, p => p.Place, MoveTo) == 3 && v.CapOf(MoveTo) != null
              && t.Moves[0].Whole && t.Moves[0].Parts == MoveRules.PartsAll,
            "moves: with only the people part running at the move, the firms, ads and cap table follow as each part starts");

        var r = new TalentFile();
        for (int i = 0; i < MoveRules.RecordsMost + 3; i++)
        {
            MoveRules.Record(r, "a~" + i, "b~" + i, i, 10, MoveRules.PartPeople, false, false);
        }
        MoveRules.Record(r, "a~5", "b~5", 5, 10, MoveRules.PartFirms, true, false);
        Check(r.Moves.Count == MoveRules.RecordsMost && r.Moves[0].From == "a~3" && r.Moves[2].Parts == (MoveRules.PartPeople | MoveRules.PartFirms) && r.Moves[2].Whole,
            "moves: at most " + MoveRules.RecordsMost + " moves are kept, the oldest going first, and a move kept twice is one record");
    }

    private static void MoveSave()
    {
        var t = new TalentFile { Seed = 9 };
        t.People.Add(new Profile { Id = "pro:1", Place = MoveTo });
        MoveRules.Record(t, MoveFrom, MoveTo, 20, 13, MoveRules.PartPeople | MoveRules.PartFirms, true, false);
        MoveRules.Record(t, MoveTo, MoveOther, 25, 9, MoveRules.PartsAll, false, true);
        TalentFile back = TalentStore.Read(TalentStore.Write(t), out int version);
        Check(version == TalentStore.Version && back.Moves.Count == 2 && back.Moves[0].From == MoveFrom && back.Moves[0].To == MoveTo && back.Moves[0].Day == 20 && back.Moves[0].Hour == 13
              && back.Moves[0].Parts == (MoveRules.PartPeople | MoveRules.PartFirms) && back.Moves[0].Whole && !back.Moves[0].Replaced && back.Moves[1].Replaced && back.People[0].Place == MoveTo,
            "moves: the moves followed come back from the save with their places, time, parts and what they took");
        string line = TalentStore.Write(new TalentFile { Seed = 9 });
        TalentFile old = TalentStore.Read(line.Replace("~m:", "~z:"), out _);
        Check(old.Moves.Count == 0 && TalentStore.Read("v=1~h:9|0|-1|1~p:pro:1|1", out _).People.Count == 1, "moves: a save from before the moves loads with none");
        string withMoves = TalentStore.Write(t);
        Check(withMoves.IndexOf("~m:", StringComparison.Ordinal) > withMoves.IndexOf("~p:", StringComparison.Ordinal) && new TechReader(withMoves).One('h').Int() == 9,
            "moves: the moves are a part of their own after the files, which an older version of the mod reads past");
        var bad = TalentStore.Read("v=1~h:9~m:" + MoveFrom.Replace("~", "%7E") + "|" + MoveFrom.Replace("~", "%7E") + "|1|1|99", out _);
        Check(bad.Moves.Count == 0, "moves: a move from a building to itself is not read back");
    }

    // Deterministic, and the same on every machine: nothing random, the result does not hang on the order things are read in.
    private static void MoveOrder()
    {
        string a = null, b = null;
        for (int k = 0; k < 2; k++)
        {
            MoveSaveOf(out FirmsFile f, out TalentFile t, out HiringFile h, out VentureFile v);
            var now = MoveNow();
            if (k == 1)
            {
                now.Reverse();
                f.Products.Reverse();
                t.People.Reverse();
            }
            MoveRead(f, t, h, v, now, MoveSeenOf(20), 21, true);
            string s = TalentStore.Write(t);
            int at = s.IndexOf("~m:", StringComparison.Ordinal);
            string moves = at >= 0 ? s.Substring(at) : string.Empty;
            string sum = f.FirmAt(MoveTo).Founded + "|" + CountAt(f.Products, p => p.Place, MoveTo) + "|" + moves;
            if (k == 0)
            {
                a = sum;
            }
            else
            {
                b = sum;
            }
        }
        Check(a != null && a == b, "moves: the same save gives the same result whatever order its lists are in (" + a + " / " + b + ")");
    }

    // Asked on each read of the firms and at the start of a day: a full save, 600 professionals, 60 products, 40 ads, 50 businesses.
    private static void MoveTiming()
    {
        var f = new FirmsFile();
        var t = new TalentFile();
        var h = new HiringFile();
        var v = new VentureFile();
        var now = new List<BusinessNow>();
        for (int i = 0; i < 50; i++)
        {
            now.Add(new BusinessNow { Place = "Street%20" + i + "~" + i, Kind = i % 3 == 0 ? 0 : -1, Name = "B" + i });
        }
        for (int i = 0; i < 600; i++)
        {
            t.People.Add(new Profile { Id = "pro:" + i, Place = now[i % 50].Place });
        }
        for (int i = 0; i < 16; i++)
        {
            f.Firms.Add(new Firm { Place = now[i * 3].Place });
            v.Caps.Add(new Cap { Place = now[i * 3].Place });
        }
        for (int i = 0; i < 60; i++)
        {
            f.Products.Add(new Product { Place = now[(i % 16) * 3].Place });
            f.Contracts.Add(new Contract { Place = now[(i % 16) * 3].Place, OwnPlace = now[i % 50].Place, Stage = (int)WorkStage.Active });
        }
        for (int i = 0; i < 40; i++)
        {
            h.Ads.Add(new JobAd { Place = now[i].Place, To = 100 });
        }
        var loose = new List<string>();
        MoveRules.Loose(f, t, h, v, now, 10, loose);
        const int runs = 200;
        var watch = Stopwatch.StartNew();
        for (int i = 0; i < runs; i++)
        {
            MoveRules.Loose(f, t, h, v, now, 10, loose);
        }
        double ms = watch.Elapsed.TotalMilliseconds / runs;
        Check(loose.Count == 0 && ms < 2.0, "moves: asking a full save for data with no business takes " + ms.ToString("0.000", System.Globalization.CultureInfo.InvariantCulture) + " ms (wanted under 2)");
    }

    // The director asks before every read of the firms and every day, follows a move on its hour, and each part catches up when it starts.
    private static void MoveSources()
    {
        string firms = ModSource(@"Tech\TechDirector.Firms.cs");
        string read = MemberText(firms, "private void ReadFirms(int day)");
        int follow = read.IndexOf("FollowMoves();", StringComparison.Ordinal), regs = read.IndexOf("FirmsGame.Firms(_firmRegs);", StringComparison.Ordinal),
            close = read.IndexOf("FirmsRules.Close(", StringComparison.Ordinal);
        Check(follow > 0 && regs > follow && close > regs && read.IndexOf("RemoveAll", StringComparison.Ordinal) < 0,
            "moves: a read of the firms follows any move before it closes a firm whose business has gone");
        string start = MemberText(firms, "private void StartFirms()");
        Check(start.IndexOf("CatchUpMoves(MoveRules.PartFirms);", StringComparison.Ordinal) > 0
              && start.IndexOf("CatchUpMoves(MoveRules.PartFirms);", StringComparison.Ordinal) < start.IndexOf("ReadFirms(save.Day);", StringComparison.Ordinal),
            "moves: the firms part follows the moves it missed before its first read");
        string director = ModSource(@"Tech\TechDirector.cs");
        string day = MemberText(director, "private void OnDay()"), hour = MemberText(director, "private void OnHour()");
        Check(day.IndexOf("FollowMoves();", StringComparison.Ordinal) > 0 && day.IndexOf("FollowMoves();", StringComparison.Ordinal) < day.IndexOf("HiringDay(", StringComparison.Ordinal)
              && hour.IndexOf("MovesHour();", StringComparison.Ordinal) > 0, "moves: a day follows any move before it is worked, and the hour after a booked move follows it");
        Check(ModSource(@"Tech\TechDirector.Hiring.cs").Contains("CatchUpMoves(MoveRules.PartHiring);") && ModSource(@"Tech\TechDirector.Venture.cs").Contains("CatchUpMoves(MoveRules.PartVenture);")
              && MemberText(ModSource(@"Tech\TechDirector.People.cs"), "private void StartPeople()").Contains("FollowMoves();"),
            "moves: the people, jobs and startups parts each follow the moves when they start");
        string game = ModSource(@"Tech\Firms\MovesGame.cs");
        Check(Occurs(game, "GoingPublic.Mine(") == 3 && !game.Contains("Foreign(") && !game.Contains("Random"),
            "moves: only the player's own buildings are read as businesses and moved (Going Public: another player's things are never touched), nothing random");
        string moves = ModSource(@"Tech\TechDirector.Moves.cs");
        Check(!moves.Contains("Send") && !moves.Contains("Network") && !moves.Contains("Update()"), "moves: nothing is sent to other players and nothing runs each frame");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains(".AddButton(\"reallife_tech_test_move\", () => Tech.TechDirector.Instance?.TestMove())"),
            "moves: the options page has the test that makes a booked move now");
    }

    private static readonly string[] MoveKeys = { "reallife_tech_test_move", "reallife_tech_test_no_move" };

    private static void MoveWords()
    {
        var langs = LanguageWords(out string[] codes);
        if (!LanguagesRead("moves", langs, codes))
        {
            return;
        }
        var missing = new List<string>();
        foreach (string key in MoveKeys)
        {
            string lacking = LanguagesLacking(langs, codes, key);
            if (lacking.Length > 0)
            {
                missing.Add(key + " (" + lacking + ")");
            }
        }
        Check(missing.Count == 0, "moves: every text is in every language (" + string.Join(", ", missing) + ")");
        bool line = false;
        foreach (KeyValuePair<string, string> row in langs[0])
        {
            line |= row.Key.StartsWith("reallife_changes_", StringComparison.Ordinal) && row.Value.Contains("Fixed: a tech firm moved to another building by a moving company");
        }
        Check(line, "moves: What's new says the fix");
    }
}
