using System;
using RealLifeOverhaul.Economy;

// The staff rota planner worked out a step at a time, as the Company director does it a few steps a frame, over the problems
// with nothing to plan: no stations, no staff, both, a station and no hours wanted, a shop with customers or without. It must
// finish without a fault and give the plan the whole solve gives (Scripts/Economy/Company/RotaFit.cs, RotaLadder*.cs, RotaAdvisor*.cs).
internal static partial class Program
{
    private static void RotaDegenerateChecks()
    {
        int faults = 0, differ = 0, cases = 0;
        string firstFault = null, firstDiffer = null;
        for (int tills = 0; tills <= 1; tills++)
        {
            for (int cleaners = 0; cleaners <= 1; cleaners++)
            {
                for (int workers = 0; workers <= 2; workers++)
                {
                    for (int mask = 0; mask < 4; mask++)
                    {
                        string name = tills + " tills, " + cleaners + " cleaning, " + workers + " staff, " + ((mask & 1) != 0 ? "customers" : "no customers") + ", " + ((mask & 2) != 0 ? "hours wanted" : "no hours wanted");
                        cases++;
                        RotaDegenerateRun(name, tills, cleaners, workers, (mask & 1) != 0, (mask & 2) != 0, 0, ref faults, ref differ, ref firstFault, ref firstDiffer);
                    }
                }
            }
        }
        Check(faults == 0, "rota degenerate: " + cases + " problems with no stations, no staff or no hours wanted all finish a step at a time" + (firstFault != null ? " (" + firstFault + ")" : ""));
        Check(differ == 0, "rota degenerate: and each gives the ladder the whole solve gives" + (firstDiffer != null ? " (" + firstDiffer + ")" : ""));

        faults = differ = 0;
        firstFault = firstDiffer = null;
        for (int seed = 1; seed <= 150; seed++)
        {
            _rotaSeed = (uint)(seed * 104729 + 3);
            int tills = RotaNext(3) == 0 ? 0 : 1 + RotaNext(2), cleaners = RotaNext(3) == 0 ? 1 : 0, workers = RotaNext(4);
            RotaDegenerateRun("seed " + seed + " (" + tills + " tills, " + cleaners + " cleaning, " + workers + " staff)", tills, cleaners, workers, RotaNext(2) == 0, RotaNext(3) != 0,
                seed, ref faults, ref differ, ref firstFault, ref firstDiffer);
        }
        Check(faults == 0, "rota degenerate: 150 random small problems, many with nothing to plan, all finish a step at a time" + (firstFault != null ? " (" + firstFault + ")" : ""));
        Check(differ == 0, "rota degenerate: and each gives the ladder the whole solve gives" + (firstDiffer != null ? " (" + firstDiffer + ")" : ""));

        // The solver and the advice alone, a step at a time against the whole, with no stations.
        var solver = new RotaSolver();
        RotaProblem p = RotaMake(0, 2);
        RotaPlan whole = solver.Solve(p, new RotaPlan());
        var steppedSolver = new RotaSolver();
        RotaPlan stepped = steppedSolver.Start(p, new RotaPlan());
        while (!steppedSolver.Step(1))
        {
        }
        RotaDegenerateSources();
        Check(whole.Shifts == stepped.Shifts && whole.Wage == stepped.Wage && whole.Full == stepped.Full && stepped.WantedHours == 0, "rota degenerate: the solver with no stations, a step at a time, is the whole solve");
    }

    // The director cannot run offline: its source says a planner's fault is not told as the game's figures failing, and that it is tried again.
    private static void RotaDegenerateSources()
    {
        string jobs = CompanySource("CompanyDirector.RotaJobs.cs"), rota = CompanySource("CompanyDirector.Rota.cs"), fit = CompanySource("RotaFit.cs");
        string slice = CompanyMethod(jobs, "private bool RotaSlice()");
        Check(InOrder(slice, "else if (_rotaStage == StageRead)", "RotaUnreadable(at, e)", "RotaPlannerFailed(at, e)"),
            "rota degenerate sources: only a fault while the game is read is told as the game's figures being unreadable; any other is the planner's own");
        string failed = CompanyMethod(rota, "private void RotaPlannerFailed(RotaPlace place, Exception e)");
        Check(InOrder(failed, "RotaGame.Stamp(place, RotaDays(place))", "_rotaFailed[place.Place] = stamp") && !failed.Contains("_rotaUnread.Add") && failed.Contains("a fault of the planner, not of the game's figures"),
            "rota degenerate sources: a planner's fault keeps the business out only at the stamp it failed at, and the log line says it was the planner's");
        Check(InOrder(CompanyMethod(rota, "internal RotaSheet RotaSheetFor(RotaPlace place)"), "if (RotaFailed(place))", "if (RotaFresh(place))")
              && InOrder(CompanyMethod(rota, "internal bool RotaFailed(RotaPlace place)"), "failedAt == RotaGame.Stamp(place, RotaDays(place))", "_rotaFailed.Remove(place.Place)"),
            "rota degenerate sources: a business the planner failed on is planned again as soon as its stamp changes (its stations or staff, or the next day)");
        Check(fit.Contains("if (_slid || !_shop.HasCustomers || _s == 0)") && !fit.Contains("Math.Max(1, _s)"),
            "rota degenerate sources: the stepped fit asks no station of a group when the business has none");
    }

    private static void RotaDegenerateRun(string name, int tills, int cleaners, int workers, bool customers, bool wanted, int seed, ref int faults, ref int differ, ref string firstFault, ref string firstDiffer)
    {
        long whole = 0, stepped = 0;
        try
        {
            whole = RotaLadderHash(RotaClimb(RotaDegenerateCase(tills, cleaners, workers, customers, wanted, seed)));
        }
        catch (Exception e)
        {
            faults++;
            firstFault = firstFault ?? name + " whole: " + e.GetType().Name + " " + e.StackTrace.Split((char)10)[0].Trim();
            return;
        }
        try
        {
            stepped = RotaLadderHash(RotaClimbSteps(RotaDegenerateCase(tills, cleaners, workers, customers, wanted, seed), RotaMode.Auto));
        }
        catch (Exception e)
        {
            faults++;
            firstFault = firstFault ?? name + " stepped: " + e.GetType().Name + " " + e.StackTrace.Split('\n')[0].Trim();
            return;
        }
        if (whole != stepped)
        {
            differ++;
            firstDiffer = firstDiffer ?? name;
        }
        for (int mode = RotaMode.StaffFirst; mode <= RotaMode.Lean; mode++)
        {
            try
            {
                long a = RotaLadderHash(RotaClimb(RotaDegenerateCase(tills, cleaners, workers, customers, wanted, seed), mode));
                long b = RotaLadderHash(RotaClimbSteps(RotaDegenerateCase(tills, cleaners, workers, customers, wanted, seed), mode));
                if (a != b)
                {
                    differ++;
                    firstDiffer = firstDiffer ?? name + " mode " + mode;
                }
            }
            catch (Exception e)
            {
                faults++;
                firstFault = firstFault ?? name + " mode " + mode + ": " + e.GetType().Name + " " + e.StackTrace.Split('\n')[0].Trim();
            }
        }
    }

    // As the director's job does it: the advice, the rating and the ladder each a couple of steps at a time.
    private static RotaLadderResult RotaClimbSteps(RotaCase c, int mode)
    {
        c.Advisor.Begin(c.Shop, c.Problem, c.Solver, c.Now.At, c.Barred, c.Outlook);
        while (!c.Advisor.Step(2))
        {
        }
        c.Advisor.Grid(c.Shop, c.Shop.Open, c.Problem);
        c.Solver.Rate(c.Problem, c.Now);
        c.Maker.Begin(c.Shop, c.Problem, c.Solver, c.Advisor, c.Outlook, c.Now, c.Barred, mode, c.Ladder);
        while (!c.Maker.Step(2))
        {
        }
        return c.Ladder;
    }

    // A shop as a gift shop is before it has any furniture, up to a small staffed one. Seed 0 is a plain case; others vary hours, skills and demands.
    private static RotaCase RotaDegenerateCase(int tills, int cleaners, int workers, bool customers, bool wanted, int seed)
    {
        RotaCase c = RotaShopCase(tills, cleaners, workers);
        int stations = tills + cleaners;
        RotaProblem p = c.Problem;
        if (seed > 0)
        {
            _rotaSeed = (uint)(seed * 7907 + 11);
        }
        c.Shop.HasCustomers = customers;
        for (int cell = 0; cell < RotaCatalogue.Cells; cell++)
        {
            if (!customers)
            {
                c.Shop.Customers[cell] = c.Shop.Wanting[cell] = 0;
            }
        }
        if (wanted)
        {
            RotaOpenHours(c, seed == 0 ? 0x7F : RotaNext(128) | 1, 9, 17);
            for (int s = 0; s < stations; s++)
            {
                RotaWant(p, s, 0x7F, 9, 17);
            }
        }
        for (int w = 0; seed > 0 && w < workers; w++)
        {
            p.Worker[w].Wage = 1200 + RotaNext(8) * 100;
            for (int s = 0; s < stations; s++)
            {
                p.Can[w * stations + s] = (byte)(RotaNext(4) == 0 ? 0 : 30 + RotaNext(70));
            }
            if (RotaNext(2) == 0)
            {
                int least = RotaNext(30);
                p.Worker[w].Demands = new[] { RotaAsk(RotaDemandKind.Hours, RotaNext(3), least, least + RotaNext(20)) };
            }
        }
        return c;
    }
}
