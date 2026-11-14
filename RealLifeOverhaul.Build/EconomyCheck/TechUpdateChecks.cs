using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// A player: after every update "all the tech business became unknown" and the AI model had to be launched again, a million dollars each time.
// The tech firms' records were closed with their products the moment the game's list of registrations did not give their business back
// (Tech/Firms/FirmsKeep.cs: a firm goes only when the game shows its business gone; a copy of the last good read is kept in the save;
// FirmsRepair.cs puts right a save already hit). The line formats themselves never changed from 1.0.3 to 1.0.12 (the writers' files are the
// same bytes at every tag), so a line saved by any of those versions reads back whole.
internal static partial class Program
{
    private const string UpA = "Harbour%20Road~12", UpB = "Lantern%20Street~40";

    private static void TechUpdate()
    {
        UpFormat();
        UpKeptLines();
        UpFate();
        UpSweep();
        UpCopy();
        UpRepair();
        UpClean();
        UpSources();
    }

    private static FirmsFile UpFirms(int founded, int stageOfModel, int features)
    {
        var f = new FirmsFile { Seed = 9, FirstDay = 3, LastDay = 40, NextId = 1 };
        var firm = new Firm { Place = UpA, Kind = (int)FirmKind.AiLab, Founded = founded, Reputation = 710, Debt = 90, RegisteredUntil = 500, RnD = 12345 };
        firm.Days[3] = 777;
        f.Firms.Add(firm);
        f.Products.Add(new Product { Id = f.Next(), Place = UpA, Name = "Atlas", Kind = (int)ProductKind.Model, Stage = stageOfModel, Features = features, Launched = 20, LastRelease = 30, Users = 5000, Compute = 4200, Train = 3 });
        f.Products.Add(new Product { Id = f.Next(), Place = UpA, Name = "Beacon", Kind = (int)ProductKind.Saas, Stage = 1, Features = 4, Launched = 25 });
        return f;
    }

    private static TalentFile UpStaff(string place, params int[] joined)
    {
        var t = new TalentFile { Seed = 1 };
        for (int i = 0; i < joined.Length; i++)
        {
            t.People.Add(new Profile { Id = "pro:" + (i + 1), Place = place, Joined = joined[i] });
        }
        return t;
    }

    private static PlaceReading UpRead(bool registered, bool building, bool rented, bool foreign, bool established, int kind) =>
        new PlaceReading { Registered = registered, BuildingFound = building, Rented = rented, Foreign = foreign, Established = established, Kind = kind };

    private static void UpFormat()
    {
        FirmsFile f = UpFirms(10, 1, 6);
        string line = FirmsStore.Write(f);
        FirmsFile back = FirmsStore.Read(line, out int version);
        Check(version == FirmsStore.Version && back.Firms.Count == 1 && back.Products.Count == 2 && back.Firms[0].Kind == (int)FirmKind.AiLab && back.Firms[0].Founded == 10
              && back.Products[0].Stage == 1 && back.Products[0].Launched == 20 && back.Products[0].Features == 6 && back.Products[0].Compute == 4200 && back.Products[0].Name == "Atlas",
            "tech update: the firms' line reads back whole: kind, founding day, each product's launched state, features and training");
        Check(FirmsStore.Write(back) == line, "tech update: a firms' line read and written again is the same line");
        var sector = new SectorFile { Seed = 4 };
        sector.Products.Add(new SectorProduct { Id = 1, Generation = 6, Benchmark = 55, TrainedDay = 31, Data = 2 });
        SectorFile sb = SectorStore.Read(SectorStore.Write(sector), out _);
        Check(sb.Products.Count == 1 && sb.Products[0].Generation == 6 && sb.Products[0].Benchmark == 55, "tech update: the markets' line keeps a model's generation");
        // the writers' own files are the same bytes at every tag from v1.0.3 to v1.0.12 (git rev-parse <tag>:path), so the lines those versions saved are this line
        Check(FirmsStore.Version == 1 && SectorStore.Version == 1 && StudioStore.Version == 1, "tech update: every store of the sector is still at the version 1 every release wrote");
        // a line an older version wrote with fewer fields reads, the rest as they start
        string at = UpA.Replace("~", "%7E");
        string shortLine = "v=1~h:9|3|40|3|3~f:" + at + "|4|10~p:1|" + at + "|Atlas|4|1|1|6";
        FirmsFile older = FirmsStore.Read(shortLine, out _);
        Check(older.Firms.Count == 1 && older.Firms[0].Kind == 4 && older.Products.Count == 1 && older.Products[0].Stage == 1 && older.Products[0].Features == 6,
            "tech update: a line with fewer fields than today's writes reads, and the product stays on sale");
    }

    private static void UpKeptLines()
    {
        var data = new Dictionary<string, string> { { FirmsStore.Key, "a line no version wrote" }, { StudioStore.Key, "v=1~g:cut off" } };
        string original = data[FirmsStore.Key];
        FirmsFile f = FirmsStore.Load(data, out _);
        // the store cannot read it: it is reported as kept, the feature starts afresh, and the save puts the original line back over what the feature wrote
        Check(f == null && global::RealLifeOverhaul.SaveLines.IsKept(data, FirmsStore.Key), "tech update: a firms' line that cannot be read is reported as kept, not dropped");
        data[FirmsStore.Key] = FirmsStore.Write(new FirmsFile { Seed = 1 });
        int put = global::RealLifeOverhaul.SaveLines.PutBack(data);
        Check(put == 1 && data[FirmsStore.Key] == original, "tech update: the unreadable line goes back into the save as it was read after the feature wrote its own");
        // a line this version reads with a later version number is read as far as it goes
        var later = new Dictionary<string, string> { { SectorStore.Key, "v=9~h:1|2|3" } };
        SectorFile s = SectorStore.Load(later, out int version);
        Check(s != null && version == 9, "tech update: a markets' line of a later version is read as far as it goes");
        global::RealLifeOverhaul.SaveLines.Forget();
    }

    private static void UpFate()
    {
        Check(FirmsKeep.Fate(UpRead(false, false, false, false, false, -1)) == PlaceFate.Unsure, "tech update: no registration read at the place: unsure, never gone");
        Check(FirmsKeep.Fate(UpRead(true, false, true, false, true, -1)) == PlaceFate.Unsure, "tech update: a registration whose building the city has not made: unsure");
        Check(FirmsKeep.Fate(UpRead(true, true, false, false, false, -1)) == PlaceFate.Gone, "tech update: a building the player no longer rents: gone");
        Check(FirmsKeep.Fate(UpRead(true, true, true, true, true, 4)) == PlaceFate.Unsure, "tech update: another player's business in a Going Public session: unsure");
        Check(FirmsKeep.Fate(UpRead(true, true, true, false, true, -1)) == PlaceFate.Gone, "tech update: the player's business of another type: gone");
        Check(FirmsKeep.Fate(UpRead(true, true, true, false, false, 4)) == PlaceFate.Unsure, "tech update: a tech business not yet set up: unsure");
        Check(FirmsKeep.Fate(UpRead(true, true, true, false, true, 4)) == PlaceFate.Stands, "tech update: a tech business set up: it stands");
    }

    private static void UpSweep()
    {
        // The report: the day's read found no business at the firm's place (a registration not back, a building not made): the firm, its model and its work stay.
        FirmsFile f = UpFirms(10, 1, 6);
        f.Contracts.Add(new Contract { Id = f.Next(), Place = UpA, Stage = (int)WorkStage.Active, Value = 50000 });
        var held = new HashSet<string>();
        int closed = FirmsKeep.Sweep(f, 50, place => UpRead(false, false, false, false, false, -1), held, null);
        Check(closed == 0 && f.Firms.Count == 1 && f.Products.Count == 2 && f.Products[0].Stage == 1 && f.Contracts[0].Stage == (int)WorkStage.Active && held.Contains(UpA),
            "tech update: a firm whose business was not read keeps its firm, its launched model and its work, and is held");
        // read again: the firm is seen, nothing was lost
        f.Firms[0].Seen = true;
        closed = FirmsKeep.Sweep(f, 51, place => UpRead(false, false, false, false, false, -1), held, null);
        Check(closed == 0 && held.Count == 0 && f.Products[0].Stage == 1, "tech update: a firm read again is no longer held and is as it was");
        // a business the game shows is gone is closed as before
        f.Firms[0].Seen = false;
        var told = new List<string>();
        closed = FirmsKeep.Sweep(f, 52, place => UpRead(true, true, true, false, true, -1), held, (p, fate, n) => told.Add(p + ":" + fate + ":" + n));
        Check(closed == 1 && f.Firms.Count == 0 && f.Products.Count == 0 && told.Count == 1 && told[0] == UpA + ":Gone:2",
            "tech update: a business the game shows is another thing now is closed with its products, as before");
        // an unknown firm is made again from the business: its kind is the business's, never an unknown one
        FirmsFile g = new FirmsFile();
        Check(g.FirmAt(UpA) == null, "tech update: a file with no record for a business has none: the director makes it from the business's own type");
    }

    private static void UpCopy()
    {
        FirmsFile good = UpFirms(10, 1, 6);
        string goodLine = FirmsStore.Write(good);
        var loaded = new Dictionary<string, string> { { FirmsStore.Key, goodLine } };
        string held = FirmsKeep.Refresh(null, loaded, 30);
        Check(held != null && FirmsKeep.ReadBackup(held, out int day) != null && day == 30, "tech update: the first good read is kept as the copy");
        Dictionary<string, string> lines = FirmsKeep.ReadBackup(held, out _);
        Check(lines != null && lines[FirmsStore.Key] == goodLine, "tech update: the copy holds the firms' line byte for byte");
        // the next session loads a poorer save (the loss): the richer copy stays for the days it is kept
        FirmsFile poor = new FirmsFile { Seed = 9 };
        poor.Firms.Add(new Firm { Place = UpA, Kind = 4, Founded = 44 });
        var poorLoaded = new Dictionary<string, string> { { FirmsStore.Key, FirmsStore.Write(poor) } };
        Check(FirmsKeep.Refresh(held, poorLoaded, 45) == null, "tech update: a poorer save does not write over the richer copy");
        Check(FirmsKeep.Refresh(held, poorLoaded, 30 + FirmsKeep.BackupDays + 1) != null, "tech update: after the days it is kept the poorer save is the copy");
        string line = "a\nb\\c\r";
        string wrote = FirmsKeep.WriteBackup(1, new Dictionary<string, string> { { SectorStore.Key, line } });
        Check(FirmsKeep.ReadBackup(wrote, out _)[SectorStore.Key] == line, "tech update: a copy's lines come back with their breaks and backslashes");
        Check(FirmsKeep.ReadBackup("not a copy", out _) == null && FirmsKeep.ReadBackup(null, out _) == null, "tech update: a line that is no copy is no copy");
    }

    private static void UpRepair()
    {
        // The hit save: the firm's record was made again on day 44 with no products; its professionals joined on days 12 to 20; the copy holds the firm and its model.
        FirmsFile copy = UpFirms(10, 1, 6);
        FirmsFile now = new FirmsFile { Seed = 9, NextId = 1, LastDay = 50 };
        now.Firms.Add(new Firm { Place = UpA, Kind = (int)FirmKind.AiLab, Founded = 44 });
        TalentFile staff = UpStaff(UpA, 12, 15, 20);
        var standing = new HashSet<string> { UpA };
        var asked = new List<string>();
        RepairReport rep = FirmsRepair.Run(now, copy, staff, standing, (place, since) => { asked.Add(place + "@" + since); return 123456000L; });
        Check(rep.Firms == 1 && rep.Products == 2 && now.FirmAt(UpA).Founded == 10 && now.FirmAt(UpA).Reputation == 710 && now.FirmAt(UpA).Kind == (int)FirmKind.AiLab,
            "tech update repair: the firm gets its founding day and figures back and both products are put back");
        Product atlas = now.Products.Find(p => p.Name == "Atlas");
        Check(atlas != null && atlas.Stage == 1 && atlas.Launched == 20 && atlas.Features == 6 && atlas.Compute == 4200, "tech update repair: the AI model is on sale as it was, with its generation and training");
        Check(rep.RefundCents == 123456000L && asked.Count == 1 && asked[0] == UpA + "@44", "tech update repair: the compute rent booked since the record was made again is paid back, once");
        Check(rep.Steps.Count >= 3, "tech update repair: every step is told for the log");
        // a rerun does nothing new: the firm is no longer made again
        RepairReport again = FirmsRepair.Run(now, copy, staff, standing, (p, s) => 1L);
        Check(!again.Any, "tech update repair: run again on the repaired save it changes nothing");
        // no copy (a save hit before the copy existed): the founding day only, from the staff that outlived the record; no products or money invented
        FirmsFile bare = new FirmsFile { Seed = 9 };
        bare.Firms.Add(new Firm { Place = UpA, Kind = 4, Founded = 44 });
        RepairReport none = FirmsRepair.Run(bare, null, staff, standing, (p, s) => 99L);
        Check(none.Firms == 1 && none.Products == 0 && none.RefundCents == 0 && bare.Firms[0].Founded == 12 && bare.Products.Count == 0,
            "tech update repair: with no copy it gives the founding day back and invents no product and no refund");
        // a firm closed and founded again on purpose: staff hired after the founding, so nothing old comes back
        FirmsFile onPurpose = new FirmsFile { Seed = 9 };
        onPurpose.Firms.Add(new Firm { Place = UpA, Kind = 4, Founded = 44 });
        RepairReport purpose = FirmsRepair.Run(onPurpose, copy, UpStaff(UpA, 45, 46, 47), standing, (p, s) => 5L);
        Check(purpose.Firms == 0 && purpose.Products == 0 && purpose.RefundCents == 0 && onPurpose.Products.Count == 0 && onPurpose.Firms[0].Founded == 44,
            "tech update repair: a firm founded again on purpose gets no old product, no founding day and no refund");
    }

    private static void UpClean()
    {
        // a save never hit: the record's founding day is the day it was made, the professionals joined on or after it
        FirmsFile clean = UpFirms(10, 1, 6);
        string before = FirmsStore.Write(clean);
        RepairReport rep = FirmsRepair.Run(clean, UpFirms(10, 1, 6), UpStaff(UpA, 10, 11, 14), new HashSet<string> { UpA }, (p, s) => 1000L);
        Check(!rep.Any && FirmsStore.Write(clean) == before, "tech update repair: a save that was never hit is left exactly as it is");
        // one professional moved in from another business, joined long before: no evidence
        RepairReport one = FirmsRepair.Run(clean, null, UpStaff(UpA, 2, 12, 15), new HashSet<string> { UpA }, (p, s) => 1000L);
        Check(!one.Any, "tech update repair: one professional who joined long ago is no sign of a lost record");
        // a firm whose business is not standing is not touched
        RepairReport held = FirmsRepair.Run(clean, UpFirms(1, 1, 6), UpStaff(UpA, 1, 2, 3), new HashSet<string>(), (p, s) => 1000L);
        Check(!held.Any, "tech update repair: a firm held for want of a reading is left alone");
        Check(FirmsRepair.Done(FirmsRepair.Mark(rep, 60)) && !FirmsRepair.Done(null) && !FirmsRepair.Done(string.Empty), "tech update repair: the mark in the save says it ran");
    }

    private static void UpSources()
    {
        string firms = ModSource(@"Tech\TechDirector.Firms.cs"), director = ModSource(@"Tech\TechDirector.cs"), infra = ModSource(@"Tech\TechDirector.Infra.cs");
        string read = MemberText(firms, "private void ReadFirms(int day)");
        Check(read.Contains("FirmsKeep.Sweep(") && !read.Contains("FirmsRules.Close("), "tech update: the day's read closes a firm only through the sweep that asks the game");
        Check(MemberText(firms, "private void StartFirms()").Contains("RepairFirms(save)") && firms.Contains("FirmsRepair.Key"), "tech update: the repair runs when the firms start, once by its mark");
        Check(MemberText(firms, "private void WriteFirms(").Contains("KeepCopy(data)"), "tech update: the copy of the last good read is written with the firms' line");
        string save = MemberText(director, "private void OnSave()");
        Check(save.Contains("if (_firmsOn)") && save.Contains("if (_studioOn)") && save.Contains("if (_sectorOn)"), "tech update: a part that is off or stood down writes nothing, so its line stays as it was loaded");
        Check(MemberText(infra, "private void ReadInfra()").Contains("FirmsKeep.Fate("), "tech update: a data centre whose warehouse was not read keeps its racks");
        string game = ModSource(@"Tech\Firms\FirmsGame.cs");
        Check(game.Contains("RefundKey") && game.Contains("RentSince"), "tech update: the refund is a line of its own in the bank statement, read from the ledger");
    }
}
