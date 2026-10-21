using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// A startup's follow-on could be pressed again and again (0.47.0): nothing kept that the round's follow-on was taken, and its
// amount was the last round's money times the player's share after it, so every press bought more and the next press asked
// more, until a player held 95% of a city startup. Now a round keeps the player's shares before it, the follow-on brings them
// back to that share and no further, once a round, within its days, only with pro rata rights, and the save keeps all of it at
// the end of the startup's record. Shares already bought stay as they are.
internal static partial class Program
{
    private static void VentureFollowOns()
    {
        FollowOnOnce();
        FollowOnRights();
        FollowOnValue();
        FollowOnSave();
        FollowOnTexts();
    }

    // A startup the player backed at its seed, then a round a venture firm led without them.
    private static Startup FollowOnStartup(out long mineBefore, out long totalBefore, List<VentureNote> notes)
    {
        var f = new VentureFile { Seed = 9 };
        Startup s = VentureRules.Born(f, 1, 1000);
        s.Ask = new TermSheet { Amount = 10000000, Pre = 500000000, Lapse = 20, ProRata = true };
        VentureRules.Invest(f, s, (int)HolderKind.Angel, false, 11, 1, 1000, null);
        mineBefore = VentureRules.PlayerShares(s);
        totalBefore = s.Total;
        VentureRules.Led(s, VentureRules.AnyLead(s.Stage + 1), s.Post * 2, VentureRules.RoundSize(s.Stage + 1), 50, notes);
        return s;
    }

    // The player's share in millionths, from shares (exact enough to see one share in a few million).
    private static double Ppm(long mine, long total) => total > 0 ? 1e6 * mine / total : 0;

    private static void FollowOnOnce()
    {
        var notes = new List<VentureNote>();
        Startup s = FollowOnStartup(out long mineBefore, out long totalBefore, notes);
        double before = Ppm(mineBefore, totalBefore);
        double afterRound = Ppm(VentureRules.PlayerShares(s), s.Total);
        long amount = VentureRules.FollowOnAmount(s);
        Check(amount > 0 && afterRound < before, "venture follow-on: a round the player stayed out of dilutes them, and offers a follow-on (" + amount + ")");
        bool told = false;
        for (int i = 0; i < notes.Count; i++)
        {
            told |= notes[i].Kind == VentureRules.NoteFollowOn && notes[i].Startup == s.Id;
        }
        Check(told, "venture follow-on: the round tells the player of it");
        Check(VentureRules.FollowOnOpen(s, 50) && VentureRules.FollowOnOpen(s, 50 + VentureCatalogue.ProRataDays)
              && !VentureRules.FollowOnOpen(s, 51 + VentureCatalogue.ProRataDays) && !VentureRules.FollowOnOpen(s, 49),
            "venture follow-on: open from the round's day for its " + VentureCatalogue.ProRataDays + " days, then closed");
        int lines = s.Holders.Count;
        Check(VentureRules.TakeFollowOn(s, (int)HolderKind.Angel, amount) && s.Holders.Count == lines + 1, "venture follow-on: the first press is taken");
        double after = Ppm(VentureRules.PlayerShares(s), s.Total);
        Check(after <= before + 1e-6 && before - after < 1.0, "venture follow-on: one follow-on brings the share back to what it was before the round, never more (" + before.ToString("0.000") + " ppm before, " + after.ToString("0.000") + " after)");
        long second = VentureRules.FollowOnAmount(s);
        Check(second == 0 && !VentureRules.FollowOnOpen(s, 51), "venture follow-on: once taken, the round offers no more (" + second + ")");
        Check(!VentureRules.TakeFollowOn(s, (int)HolderKind.Angel, amount) && !VentureRules.TakeFollowOn(s, (int)HolderKind.Fund, amount) && s.Holders.Count == lines + 1,
            "venture follow-on: a second press in the same round is refused, as an angel or from the fund");
        // the owner's case: pressed again and again
        Startup t = FollowOnStartup(out mineBefore, out totalBefore, null);
        for (int i = 0; i < 40; i++)
        {
            VentureRules.TakeFollowOn(t, (int)HolderKind.Angel, Math.Max(1, VentureRules.FollowOnAmount(t)));
        }
        double pressed = Ppm(VentureRules.PlayerShares(t), t.Total);
        Check(pressed <= Ppm(mineBefore, totalBefore) + 1e-6 && pressed < 100000, "venture follow-on: forty presses leave the share where it was before the round (" + (pressed / 10000).ToString("0.00") + "%)");
        // the next round opens a new one, and its follow-on keeps the share before that round
        long mine2 = VentureRules.PlayerShares(t), total2 = t.Total;
        VentureRules.Led(t, VentureRules.AnyLead(t.Stage + 1), t.Post * 2, VentureRules.RoundSize(t.Stage + 1), 120, null);
        long next = VentureRules.FollowOnAmount(t);
        Check(next > 0 && VentureRules.FollowOnOpen(t, 120) && VentureRules.TakeFollowOn(t, (int)HolderKind.Angel, next)
              && Math.Abs(Ppm(VentureRules.PlayerShares(t), t.Total) - Ppm(mine2, total2)) < 1.0 && VentureRules.FollowOnAmount(t) == 0,
            "venture follow-on: the next round offers its own follow-on, once, back to the share before it");
        Check(FollowOnStartup(out _, out _, null).Post == FollowOnStartup(out _, out _, null).Post
              && VentureRules.FollowOnAmount(FollowOnStartup(out _, out _, null)) == amount, "venture follow-on: the same round asks the same amount on every machine");
    }

    private static void FollowOnRights()
    {
        // no pro rata rights: nothing offered
        Startup s = FollowOnStartup(out _, out _, null);
        Startup r = FollowOnStartup(out _, out _, null);
        for (int i = 0; i < r.Holders.Count; i++)
        {
            if (r.Holders[i].Players)
            {
                r.Holders[i].ProRata = false;
            }
        }
        Check(VentureRules.FollowOnAmount(s) > 0 && VentureRules.FollowOnAmount(r) == 0 && !VentureRules.FollowOnOpen(r, 50) && !VentureRules.TakeFollowOn(r, (int)HolderKind.Angel, 1),
            "venture follow-on: a player without pro rata rights is offered none");
        // the round the player first came in at: they hold what they bought, and no follow-on
        var f = new VentureFile { Seed = 9 };
        Startup n = VentureRules.Born(f, 1, 1000);
        n.Ask = new TermSheet { Amount = 10000000, Pre = 500000000, Lapse = 20, ProRata = true };
        VentureRules.Invest(f, n, (int)HolderKind.Angel, false, 11, 1, 1000, null);
        Check(VentureRules.PlayerShares(n) > 0 && VentureRules.FollowOnAmount(n) == 0, "venture follow-on: the round the player first came in at offers no follow-on");
        // a round the player joined at more than their share: nothing to keep
        Startup j = FollowOnStartup(out _, out _, null);
        VentureRules.TakeFollowOn(j, (int)HolderKind.Angel, VentureRules.FollowOnAmount(j));
        long before = VentureRules.PlayerShares(j), total = j.Total;
        j.Ask = new TermSheet { Amount = VentureRules.RoundSize(j.Stage + 1), Pre = j.Post, Lapse = 200, ProRata = true };
        VentureRules.Invest(new VentureFile { Seed = 9 }, j, (int)HolderKind.Angel, false, 150, 1, 1000, null);
        Check(Ppm(VentureRules.PlayerShares(j), j.Total) > Ppm(before, total) && VentureRules.FollowOnAmount(j) == 0, "venture follow-on: a round the player took more of than their share offers none");
        // a down round the player stayed out of: offered too, once
        Startup d = FollowOnStartup(out _, out _, null);
        VentureRules.TakeFollowOn(d, (int)HolderKind.Angel, VentureRules.FollowOnAmount(d));
        long dMine = VentureRules.PlayerShares(d), dTotal = d.Total;
        int stage = d.Stage;
        VentureRules.Round(d, d.Post / 2, new[] { new Holder { Kind = (int)HolderKind.Vc, Who = 2, Invested = VentureRules.RoundSize(stage) / 2 } }, 200);
        d.Stage = stage;
        long down = VentureRules.FollowOnAmount(d);
        Check(down > 0 && VentureRules.TakeFollowOn(d, (int)HolderKind.Fund, down) && Math.Abs(Ppm(VentureRules.PlayerShares(d), d.Total) - Ppm(dMine, dTotal)) < 1.0
              && VentureRules.FollowOnAmount(d) == 0, "venture follow-on: a down round's follow-on keeps the share before it, once");
        Check(!VentureRules.TakeFollowOn(FollowOnStartup(out _, out _, null), (int)HolderKind.Angel, VentureRules.FollowOnAmount(FollowOnStartup(out _, out _, null)) + 1),
            "venture follow-on: more than the amount is refused");
        Startup dead = FollowOnStartup(out _, out _, null);
        dead.State = (int)StartupState.Failed;
        Check(!VentureRules.FollowOnOpen(dead, 50), "venture follow-on: a startup that has failed offers none");
    }

    private static void FollowOnValue()
    {
        Startup s = FollowOnStartup(out _, out _, null);
        long post = s.Post, cash = s.Cash, total = s.Total;
        double price = (double)post / total;
        long amount = VentureRules.FollowOnAmount(s);
        VentureRules.TakeFollowOn(s, (int)HolderKind.Angel, amount);
        Holder line = s.Holders[s.Holders.Count - 1];
        double priceAfter = (double)s.Post / s.Total;
        Check(s.Post == post + amount && s.Cash == cash + amount && Math.Abs(priceAfter - price) / price < 1e-6,
            "venture follow-on: the money is added to the value and the cash, and a share is worth what the round priced it at (" + price.ToString("0.0000") + ", " + priceAfter.ToString("0.0000") + ")");
        Check(Math.Abs((double)line.Shares * s.Post / s.Total - amount) <= 1 + price, "venture follow-on: the new shares are worth what was put in");
        Check(line.Invested == amount && line.Preference == amount * VentureCatalogue.LiquidationPreference / 1000 && line.ProRata && line.Players,
            "venture follow-on: the new line has its money, its preference and pro rata rights");
        long value = VentureRules.PlayerValue(s, out int share, out long invested);
        Check(Math.Abs(value - (double)s.Post * VentureRules.PlayerShares(s) / s.Total) <= 1, "venture follow-on: the player's value is their part of the value after it");
        long[] prices = { 1, s.Holders[s.Holders.Count - 2].Preference / 2, VentureRules.Invested(s), s.Post, s.Post * 10 };
        bool sums = true, honest = true;
        foreach (long p in prices)
        {
            long[] pay = VentureRules.Waterfall(s.Holders, p);
            long paid = 0;
            for (int i = 0; i < pay.Length; i++)
            {
                paid += pay[i];
                honest &= pay[i] >= 0;
            }
            sums &= paid == p;
        }
        Check(sums && honest, "venture follow-on: after a follow-on a sale's waterfall pays out exactly the price, never below nothing");
        long big = s.Post * 10;
        long[] all = VentureRules.Waterfall(s.Holders, big);
        long mine = 0;
        for (int i = 0; i < all.Length; i++)
        {
            mine += s.Holders[i].Players ? all[i] : 0;
        }
        double fair = (double)big * VentureRules.PlayerShares(s) / s.Total;
        Check(Math.Abs(mine - fair) / fair < 1e-6, "venture follow-on: a sale far above the preferences pays the player their share of it");
    }

    // A startup's record without the fields 0.47.1 added at its end: the line an older RLO wrote.
    private static string WithoutFollowOnFields(string line)
    {
        string[] parts = line.Split('~');
        for (int p = 0; p < parts.Length; p++)
        {
            if (!parts[p].StartsWith("s:", StringComparison.Ordinal))
            {
                continue;
            }
            string[] records = parts[p].Substring(2).Split(';');
            for (int r = 0; r < records.Length; r++)
            {
                string[] fields = records[r].Split('|');
                records[r] = string.Join("|", fields, 0, Math.Max(0, fields.Length - 4));
            }
            parts[p] = "s:" + string.Join(";", records);
        }
        return string.Join("~", parts);
    }

    private static int StartupFields(string line)
    {
        foreach (string part in line.Split('~'))
        {
            if (part.StartsWith("s:", StringComparison.Ordinal))
            {
                return part.Substring(2).Split(';')[0].Split('|').Length;
            }
        }
        return -1;
    }

    private static void FollowOnSave()
    {
        var f = new VentureFile { Seed = 9 };
        Startup open = FollowOnStartup(out _, out _, null);
        Startup taken = FollowOnStartup(out _, out _, null);
        taken.Id = open.Id + 1;
        VentureRules.TakeFollowOn(taken, (int)HolderKind.Angel, VentureRules.FollowOnAmount(taken));
        f.Startups.Add(open);
        f.Startups.Add(taken);
        string line = VentureStore.Write(f);
        VentureFile back = VentureStore.Read(line, out int version);
        Startup bo = back.StartupOf(open.Id), bt = back.StartupOf(taken.Id);
        Check(version == VentureStore.Version && bo != null && bt != null && bo.RoundDay == 50 && bo.BeforeMine == open.BeforeMine && bo.BeforeTotal == open.BeforeTotal && !bo.Followed && bt.Followed,
            "venture follow-on: the last round's day, the shares before it and the follow-on taken come back from the save");
        Check(VentureRules.FollowOnAmount(bo) == VentureRules.FollowOnAmount(open) && VentureRules.FollowOnAmount(bt) == 0 && !VentureRules.TakeFollowOn(bt, (int)HolderKind.Angel, 1),
            "venture follow-on: a follow-on taken before a save is not offered again after it");
        string old = WithoutFollowOnFields(line);
        Check(StartupFields(line) == StartupFields(old) + 4 && StartupFields(old) == 23, "venture follow-on: the four fields are at the end of the startup's record, after the 23 an older RLO reads");
        // an older save, with a stake already pressed up: it loads as it was and offers no more
        Startup grown = FollowOnStartup(out _, out _, null);
        long grownMine = VentureRules.PlayerShares(grown);
        grown.Holders.Add(new Holder { Kind = (int)HolderKind.Angel, Shares = grown.Total * 18, Invested = 900000000, Round = grown.Stage, ProRata = true });
        var g = new VentureFile { Seed = 9 };
        g.Startups.Add(grown);
        VentureFile oldBack = VentureStore.Read(WithoutFollowOnFields(VentureStore.Write(g)), out version);
        Startup ob = oldBack.StartupOf(grown.Id);
        Check(ob != null && ob.Holders.Count == grown.Holders.Count && ob.Total == grown.Total && ob.Post == grown.Post && VentureRules.PlayerShares(ob) == VentureRules.PlayerShares(grown)
              && VentureRules.PlayerShares(ob) > grownMine * 10, "venture follow-on: a save from before loads with every share the player bought, none taken away");
        Check(ob != null && ob.RoundDay == -1 && ob.BeforeTotal == 0 && !ob.Followed && VentureRules.FollowOnAmount(ob) == 0 && !VentureRules.FollowOnOpen(ob, ob.LastRound),
            "venture follow-on: a save from before offers no more follow-on of the round it was in");
    }

    private static void FollowOnTexts()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(DesignLocales, "en.json"));
        Dictionary<string, string> pt = LoadWords(System.IO.Path.Combine(DesignLocales, "pt.json"));
        Dictionary<string, string> zh = LoadWords(System.IO.Path.Combine(DesignLocales, "zh-cn.json"));
        Check(en != null && pt != null && zh != null && en.ContainsKey("reallife_vc_test_round") && pt.ContainsKey("reallife_vc_test_round") && zh.ContainsKey("reallife_vc_test_round"),
            "venture follow-on: the test of a round is named in en, pt and zh-cn");
        Check(en != null && en.TryGetValue("reallife_changes_next", out string next) && next.Contains("Fixed: a startup's follow-on"), "venture follow-on: What's new says so");
    }
}
