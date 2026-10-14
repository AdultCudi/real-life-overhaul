using System;
using System.Collections.Generic;
using RealLifeOverhaul.Tech;

// Checks for startups and venture capital (Scripts/Tech/Venture): the mood and the AI wave, a startup's life over years,
// rounds and dilution, the waterfall of a sale, the player's ask, haggle and follow-on, the player's own raise (term
// sheets, the board, a bridge, options, a sale of shares) and the save line.
internal static partial class Program
{
    private static void Ventures2()
    {
        VentureMood();
        VentureRounds();
        VentureWaterfall();
        VentureLife();
        VenturePlayer();
        VentureOwnFirm();
        VentureSave();
        VentureListing();
        VentureOptions();
    }

    // Share options granted with nobody at the firm (0.12.4): the even split and what waits, and what nobody holds.
    private static void VentureOptions()
    {
        long each = VentureRules.Split(10, 3, out long left);
        Check(each == 3 && left == 1, "options: ten shares among three people are three each, one left to wait (" + each + ", " + left + ")");
        each = VentureRules.Split(500, 0, out left);
        Check(each == 0 && left == 500, "options: with nobody there nothing is given and all of it waits (" + each + ", " + left + ")");
        each = VentureRules.Split(-5, 2, out left);
        Check(each == 0 && left == 0, "options: nothing to split gives nothing and leaves nothing (" + each + ", " + left + ")");
        Cap c = VentureRules.NewCap("Main%20St~9");
        Check(VentureRules.Unheld(c, 0) == 0, "options: a firm with no grant has nothing waiting");
        long shares = VentureRules.Grant(c, VentureCatalogue.OptionGrant);
        Check(shares > 0 && VentureRules.Unheld(c, 0) == shares, "options: a grant that nobody received is all waiting (" + VentureRules.Unheld(c, 0) + " of " + shares + ")");
        Check(VentureRules.Unheld(c, shares / 2) == shares - shares / 2, "options: what the people hold is taken off what waits");
        Check(VentureRules.Unheld(c, shares * 2) == 0 && VentureRules.Unheld(null, 0) == 0, "options: what waits is never below nothing");
    }

    private static void VentureListing()
    {
        var f = new VentureFile { Seed = 8 };
        Startup s = VentureRules.Born(f, 1, 1000);
        var angel = new Holder { Kind = (int)HolderKind.Angel, Invested = 50000000 };
        VentureRules.Round(s, 400000000, new[] { angel }, 10);
        s.Mrr = VentureCatalogue.ListingArr / 12;
        var notes = new List<VentureNote>();
        long[] pay = VentureRules.List(f, s, notes);
        long sum = 0;
        for (int i = 0; i < pay.Length; i++)
        {
            sum += pay[i];
        }
        Check(s.State == (int)StartupState.Listed && !s.Alive && s.Post == VentureCatalogue.ListingArr * VentureCatalogue.ListingMultiple, "ventures: a listed startup is valued at its revenue by the public multiple");
        Check(sum <= s.Post && sum > s.Post * 99 / 100, "ventures: on listing every holder sells at the offer, by shares (" + sum + " of " + s.Post + ")");
        Check(notes.Count == 1 && notes[0].Kind == VentureRules.NoteListing && notes[0].Amount == angel.Paid && angel.Paid > angel.Invested, "ventures: the player's shares at the offer come back to them");
        Startup small = VentureRules.Born(f, 2, 1000);
        VentureRules.Round(small, 900000000, new[] { new Holder { Kind = (int)HolderKind.Vc, Who = 1, Invested = 100000000 } }, 10);
        small.Mrr = 1000;
        VentureRules.List(f, small, null);
        Check(small.Post >= 1000000000, "ventures: a listing is never under the last round");
    }

    private static void VentureMood()
    {
        Check(VentureRules.Mood(0) > VentureRules.Mood(1) && VentureRules.Mood(1) > VentureRules.Mood(3), "ventures: a boom's mood is warmer than a recession's");
        int least = int.MaxValue, most = int.MinValue;
        for (int d = 0; d < 400; d++)
        {
            int h = VentureRules.Hype(7, d, 120);
            least = Math.Min(least, h);
            most = Math.Max(most, h);
        }
        Check(least >= 1000 - VentureCatalogue.HypeSwing && most <= 1000 + VentureCatalogue.HypeSwing && most - least > VentureCatalogue.HypeSwing, "ventures: the AI wave rises and falls within its swing (" + least + " to " + most + ")");
        var f = new VentureFile { Seed = 3 };
        Startup good = VentureRules.Born(f, 1, 1000), copy = VentureRules.Born(new VentureFile { Seed = 3 }, 1, 1000);
        Check(good.Name == copy.Name && good.Quality == copy.Quality && good.Sector == copy.Sector, "ventures: the same seed founds the same startup");
        good.Quality = 900;
        Startup poor = VentureRules.Born(f, 1, 1000);
        poor.Quality = 100;
        poor.Sector = good.Sector = (int)Venture.Software;
        Check(VentureRules.Growth(good, 1000, 1000) > VentureRules.Growth(poor, 1000, 1000) * 3, "ventures: good founders grow faster");
        good.Sector = (int)Venture.Ai;
        Check(VentureRules.Growth(good, 1000, 1500) > VentureRules.Growth(good, 1000, 600), "ventures: an AI startup rides the wave");
        Check(VentureRules.Runway(1000000, 10000, 0) == 100 && VentureRules.Runway(100, 10, 900) == int.MaxValue, "ventures: runway is cash over the net burn, endless when revenue covers it");
    }

    private static void VentureRounds()
    {
        Check(VentureRules.PreMoney(0, 50, 1000, 1) == VentureCatalogue.FloorValue[1], "ventures: a company with no revenue is valued at its stage's floor");
        Check(VentureRules.PreMoney(100000000, 250, 1000, 2) > VentureRules.PreMoney(100000000, 30, 1000, 2), "ventures: faster growth earns a higher multiple");
        Check(VentureRules.PreMoney(100000000, 100, 1250, 2) > VentureRules.PreMoney(100000000, 100, 560, 2), "ventures: a warm market pays more");
        var f = new VentureFile { Seed = 5 };
        Startup s = VentureRules.Born(f, 1, 1000);
        long pre = 500000000;
        var vc = new Holder { Kind = (int)HolderKind.Vc, Who = 1, Invested = 125000000, Board = true, ProRata = true };
        VentureRules.Round(s, pre, new[] { vc }, 10);
        long total = s.Total;
        int vcShare = VentureRules.ShareOf(vc, total);
        Check(s.Stage == (int)RoundStage.Seed && s.Post == pre + 125000000, "ventures: a round moves the stage on and adds its money to the value");
        Check(vcShare >= 170 && vcShare <= 190, "ventures: an investor holds about what they paid for, less the pool (" + vcShare + ")");
        bool pool = false;
        foreach (Holder h in s.Holders)
        {
            pool |= h.Kind == (int)HolderKind.Pool && VentureRules.ShareOf(h, total) >= 90;
        }
        Check(pool, "ventures: the seed round sets a pool aside for staff");
        Check(vc.Preference == vc.Invested, "ventures: an investor's money comes back once over first");
        int founders = VentureRules.ShareOf(s.Holders[0], total);
        Check(founders < 1000 && founders > 600, "ventures: the founders are diluted (" + founders + ")");
    }

    private static void VentureWaterfall()
    {
        var holders = new List<Holder>
        {
            new Holder { Kind = (int)HolderKind.Founders, Shares = 8000000 },
            new Holder { Kind = (int)HolderKind.Vc, Shares = 2000000, Invested = 50000000, Preference = 50000000, Round = 1 }
        };
        long[] small = VentureRules.Waterfall(holders, 40000000);
        Check(small[1] == 40000000 && small[0] == 0, "ventures: a sale for less than the money put in goes to the investor first");
        long[] mid = VentureRules.Waterfall(holders, 100000000);
        Check(mid[1] == 50000000 && mid[0] == 50000000, "ventures: a middling sale pays the preference, the rest to the founders");
        long[] big = VentureRules.Waterfall(holders, 1000000000);
        Check(big[1] == 200000000 && big[0] == 800000000, "ventures: a big sale is shared by the shares, the investor's share being worth more than the preference");
        long sum = 0;
        foreach (long p in VentureRules.Waterfall(holders, 123456789))
        {
            sum += p;
        }
        Check(sum == 123456789, "ventures: a sale's money is shared out to the cent");
        var two = new List<Holder>
        {
            new Holder { Kind = (int)HolderKind.Founders, Shares = 6000000 },
            new Holder { Kind = (int)HolderKind.Vc, Shares = 2000000, Preference = 30000000, Round = 1 },
            new Holder { Kind = (int)HolderKind.Vc, Shares = 2000000, Preference = 60000000, Round = 2 }
        };
        long[] thin = VentureRules.Waterfall(two, 70000000);
        Check(thin[2] == 60000000 && thin[1] == 10000000 && thin[0] == 0, "ventures: the latest round's preference is paid first");
    }

    private static void VentureLife()
    {
        int raised = 0, failed = 0, sold = 0, down = 0, pitches = 0, alive = 0;
        for (int run = 0; run < 6; run++)
        {
            var f = new VentureFile { Seed = 40 + run };
            var notes = new List<VentureNote>();
            for (int i = 0; i < 12; i++)
            {
                f.Startups.Add(VentureRules.Born(f, 0, 1000));
            }
            for (int day = 1; day <= 1500; day++)
            {
                int hype = VentureRules.Hype(f.Seed, day, 120);
                for (int i = 0; i < f.Startups.Count; i++)
                {
                    VentureRules.Day(f, f.Startups[i], day, day / 200 % 4, hype, true, notes);
                }
            }
            foreach (VentureNote n in notes)
            {
                raised += n.Kind == VentureRules.NoteRaised ? 1 : 0;
                failed += n.Kind == VentureRules.NoteFailed || n.Kind == VentureRules.NoteAcquihire ? 1 : 0;
                sold += n.Kind == VentureRules.NoteAcquired ? 1 : 0;
                down += n.Kind == VentureRules.NoteDown ? 1 : 0;
                pitches += n.Kind == VentureRules.NotePitch ? 1 : 0;
            }
            foreach (Startup s in f.Startups)
            {
                alive += s.Alive ? 1 : 0;
                long total = s.Total;
                Check(total > 0 && s.Post >= 0, "ventures: a startup always has shares and a value");
            }
        }
        Check(raised > 20, "ventures: startups raise rounds over the years (" + raised + ")");
        Check(failed > 3, "ventures: some fail (" + failed + ")");
        Check(sold > 0, "ventures: some are bought (" + sold + ")");
        Check(pitches > 5, "ventures: raising startups come to the player (" + pitches + ")");
        Check(alive > 0 && alive < 72, "ventures: some of the 72 are still running after four years (" + alive + ")");
    }

    private static void VenturePlayer()
    {
        var f = new VentureFile { Seed = 9 };
        Startup s = VentureRules.Born(f, 1, 1000);
        s.Ask = new TermSheet { Amount = 10000000, Pre = 500000000, Lapse = 10, ProRata = true };
        Check(!VentureRules.Invest(f, s, (int)HolderKind.Angel, false, 11, 1, 1000, null), "ventures: an ask that has lapsed cannot be taken");
        s.Ask.Lapse = 20;
        Check(VentureRules.Invest(f, s, (int)HolderKind.Angel, false, 11, 1, 1000, null) && s.Ask == null, "ventures: taking the ask closes the round");
        int share;
        long put;
        long value = VentureRules.PlayerValue(s, out share, out put);
        Check(put == 10000000 && share > 0 && value >= put * 9 / 10, "ventures: the player holds their share at the round's value (" + share + ")");
        int yes = 0;
        for (int i = 0; i < 200; i++)
        {
            Startup t = VentureRules.Born(new VentureFile { Seed = 100 + i }, 1, 1000);
            t.Ask = new TermSheet { Amount = 1, Pre = 1000, Lapse = 99 };
            yes += VentureRules.Haggle(new VentureFile { Seed = 100 + i }, t, 3, 2) && t.Ask.Pre == 850 ? 1 : 0;
        }
        Check(yes > 40 && yes < 190, "ventures: some founders take a lower value and some hold firm (" + yes + " of 200)");
        // a later round: a follow-on keeps the player's share
        var vc = new Holder { Kind = (int)HolderKind.Vc, Who = 3, Invested = 300000000 };
        VentureRules.Round(s, s.Post * 2, new[] { vc }, 50);
        long follow = VentureRules.FollowOnAmount(s);
        int before;
        VentureRules.PlayerValue(s, out before, out put);
        VentureRules.TakeFollowOn(s, (int)HolderKind.Angel, follow);
        int after;
        VentureRules.PlayerValue(s, out after, out put);
        Check(follow > 0 && after > before, "ventures: a follow-on buys more of the company (" + before + " to " + after + ")");
        Check(VentureRules.Claimed(new Startup { Mrr = 1000, ClaimBp = 5000 }) == 1500, "ventures: a deck may claim half again over the truth");
    }

    private static void VentureOwnFirm()
    {
        Cap c = VentureRules.NewCap("p");
        Check(VentureRules.PlayerShare(c) == 1000, "ventures: a firm that has raised nothing is all the player's");
        TermSheet t = VentureRules.Sheet(2, 400000000, 100000000, 800, 1000, 5);
        TermSheet weak = VentureRules.Sheet(2, 400000000, 100000000, 200, 1000, 5);
        Check(t.Pre > weak.Pre && t.Board && t.Lapse == 5 + VentureCatalogue.TermSheetDays, "ventures: a better meeting earns a higher value, and a lead a board seat");
        long money = VentureRules.Accept(c, t, 1000000, 10);
        int mine = VentureRules.PlayerShare(c);
        Check(money == t.Amount && mine < 1000 && Math.Abs(mine - (1000 - t.EquityMille)) <= 2, "ventures: taking a term sheet dilutes the player by what was sold (" + mine + ")");
        Check(c.BoardSeats == 2 && c.VcSeats == 1 && c.TargetMrr == 1000000 * (1000 + VentureCatalogue.TargetGrowth) / 1000 && c.TargetDay == 10 + VentureCatalogue.TargetDays, "ventures: the investor joins the board, which sets a target");
        Check(VentureRules.Review(c, 99999999, c.TargetDay - 1) == 0, "ventures: the board waits for its day");
        Check(VentureRules.Review(c, 99999999, c.TargetDay) == 1 && c.Misses == 0, "ventures: a target met is a new target");
        Check(VentureRules.Review(c, 0, c.TargetDay) == 2 && c.Pressure == 1, "ventures: a target missed is a warning");
        Check(VentureRules.Review(c, 0, c.TargetDay) == 2, "ventures: without a majority the board only warns");
        TermSheet second = VentureRules.Sheet(3, 1500000000, 300000000, 700, 1000, 200);
        VentureRules.Accept(c, second, 1000000, 200);
        Check(c.VcSeats * 2 > c.BoardSeats, "ventures: two leads hold most of a board of three");
        VentureRules.Review(c, 0, c.TargetDay);
        Check(VentureRules.Review(c, 0, c.TargetDay) == 3 && c.Pressure == 2, "ventures: missed twice with a majority, the board replaces the chief executive");
        c.Bridge = VentureRules.BridgeFor(100000);
        Check(c.Bridge == 100000L * 30 * VentureCatalogue.BridgeMonths && VentureRules.BridgeInterest(36500000) == 36500000L * VentureCatalogue.BridgeInterestBp / 10000 / 365, "ventures: a bridge is six months of burn at its interest");
        int bridgeBefore = c.Holders.Count;
        VentureRules.Accept(c, VentureRules.Sheet(4, 3000000000, 500000000, 600, 1000, 300), 1000000, 300);
        Check(c.Bridge == 0 && c.Holders.Count == bridgeBefore + 2, "ventures: a bridge turns into shares at the next round");
        int shareBefore = VentureRules.PlayerShare(c);
        VentureRules.Grant(c, VentureCatalogue.OptionGrant);
        Check(VentureRules.PlayerShare(c) < shareBefore && c.OptionsGranted == VentureCatalogue.OptionGrant, "ventures: share options dilute the owners");
        Check(VentureRules.Vested(0, 50, 120) == 0 && VentureRules.Vested(0, 240, 120) == 500 && VentureRules.Vested(0, 999, 120) == 1000, "ventures: options vest over four years after a one-year cliff");
        long sold = VentureRules.SellShares(c, 100, 4);
        Check(sold > 0 && VentureRules.PlayerShare(c) < shareBefore, "ventures: the player can sell some of their shares");
    }

    private static void VentureSave()
    {
        var f = new VentureFile { Seed = 21, FirstDay = 3, LastDay = 80 };
        f.Fund.Open = true;
        f.Fund.Committed = 5000000;
        f.Fund.Cash = 1200000;
        f.ClosedUntil[3] = 140;
        Startup s = VentureRules.Born(f, 4, 1000);
        s.Founder = "Pat Rowe";
        s.Ask = new TermSheet { Amount = 3000000, Pre = 600000000, Lapse = 90, ProRata = true };
        VentureRules.Round(s, 500000000, new[] { new Holder { Kind = (int)HolderKind.Angel, Invested = 2000000, ProRata = true } }, 50);
        s.Ask = new TermSheet { Amount = 3000000, Pre = 600000000, Lapse = 90, ProRata = true };
        f.Startups.Add(s);
        Cap c = VentureRules.NewCap("Main%20St~9");
        VentureRules.Accept(c, VentureRules.Sheet(1, 400000000, 100000000, 700, 1000, 10), 500000, 10);
        c.Sheets.Add(VentureRules.Sheet(2, 400000000, 80000000, 500, 1000, 12));
        c.Ceo = "pro:3";
        c.Unassigned = 1234;
        c.OptionsChecked = 1;
        f.Caps.Add(c);
        int version;
        VentureFile back = VentureStore.Read(VentureStore.Write(f), out version);
        Startup b = back.StartupOf(s.Id);
        Check(back.Seed == 21 && back.Fund.Open && back.Fund.Cash == 1200000 && back.ClosedUntil[3] == 140, "ventures: the fund and the closed doors come back");
        Check(b != null && b.Founder == "Pat Rowe" && b.Holders.Count == s.Holders.Count && b.Total == s.Total && b.Ask != null && b.Ask.Pre == 600000000, "ventures: a startup comes back with its cap table and ask");
        Cap bc = back.CapOf("Main%20St~9");
        Check(bc != null && bc.Holders.Count == c.Holders.Count && bc.Sheets.Count == 1 && bc.Ceo == "pro:3" && bc.TargetMrr == c.TargetMrr && VentureRules.PlayerShare(bc) == VentureRules.PlayerShare(c),
            "ventures: a firm's cap table, board and term sheets come back");
        Check(bc != null && bc.Unassigned == 1234 && bc.OptionsChecked == 1, "ventures: a firm's options waiting for somebody, and the look over them, come back");
    }
}
