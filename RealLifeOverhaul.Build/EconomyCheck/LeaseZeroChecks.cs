using System;
using System.Collections.Generic;
using RealLifeOverhaul.Economy;
using RealLifeOverhaul.Rivals;

// A landlord's offer that comes to a rise of nothing (the rent stays) is an offer like any other: it is made, it is within any manager's limit, it renews and it is told as such.
// Before, "the offer is made" was read from the offer being over nothing, so such a lease was never answered, never renewed and never offered again (review 2, must-fix 1).
// These drive seeds whose offer is nothing through the real day entries: the shared lease step (CompanyDay.LeaseShop) the director and the run both take, the rivals' own
// day (RivalFirms.Day), CompanyDay.Offer by itself with a falling market, and a long jump of days.
internal static partial class Program
{
    private static void LeaseZeroOffers()
    {
        int before = _run, failedBefore = _failed;

        // Seeds whose first offer to a lease with no rise yet comes to nothing: the city's trend is nothing and the landlord's own draw is at or under it.
        var zero = new List<int>();
        for (int seed = 1; seed <= 600 && zero.Count < 40; seed++)
        {
            LeaseFile probe = LeaseRules.Begin(seed, "Elm~7", 180, Epoch, RunMonth);
            if (LeaseRules.Offer(probe, seed, 0, 0, 0) == 0)
            {
                zero.Add(seed);
            }
        }
        Check(zero.Count >= 30, "zero offers: a landlord's offer comes to nothing for a good share of seeds (" + zero.Count + " of the first 600 seeds had one)");

        // The offer by itself: made, within the narrowest limit, answerable, and it renews at the same rent.
        LeaseFile a = LeaseRules.Begin(zero[0], "Elm~7", 180, 100, RunMonth);
        a.Offer = LeaseRules.Offer(a, zero[0], 0, 0, 0);
        a.Asked = a.End - LeaseRules.NoticeDays;
        int early = a.End - 5;
        Check(a.Offer == 0 && LeaseRules.Made(a) && LeaseRules.Waiting(a, early) && LeaseRules.Within(a, 0) && !LeaseRules.OfferDue(a, early) && LeaseRules.OfferOver(a) == 0,
            "zero offers: an offer of nothing is made (Asked, not the sum, says so): it waits for its answer, is inside the narrowest limit and is not offered again");
        Check(LeaseRules.Respond(a, early, true) && LeaseRules.Settle(a, a.End, zero[0], RunMonth) && a.Rise == 0 && a.Renewals == 1 && a.Last == 0 && !a.Hold && a.End > early && !LeaseRules.Made(a),
            "zero offers: accepted, it renews at the same rent for a new term and the lease is ready for the next offer");
        LeaseFile b = LeaseRules.Begin(zero[1], "Elm~7", 180, 100, RunMonth);
        LeaseRules.Make(b, 0, b.End - LeaseRules.NoticeDays);
        Check(LeaseRules.Settle(b, b.End, zero[1], RunMonth) && b.Renewals == 1 && b.Rise == 0, "zero offers: unanswered, an offer of nothing is the landlord's figure and renews at the end like any other");
        LeaseFile c = new LeaseFile { Place = "Dock~4", Start = 10, Term = 100, Rent = 150, Offer = 0, Asked = 0 };
        Check(LeaseRules.OfferDue(c, 100) && !LeaseRules.Made(c), "zero offers: a lease with no offer made is due one at the notice");
        LeaseRules.Make(c, 0, 0);
        Check(LeaseRules.Made(c) && c.Asked == 1 && !LeaseRules.OfferDue(c, 100), "zero offers: an offer made on day 0 is still made (the day is never read as none)");

        // A lease saved with an offer and no day (an older line) is an offer made.
        string record = "10|100|150|0|75|0|0|0|0|Dock~4|0|0|0";
        LeaseFile old = LeaseStore.Parse(record.Split('|'));
        Check(old != null && old.Offer == 75 && LeaseRules.Made(old), "zero offers: a saved offer with no day loads as an offer made");

        // The director's and the run's one lease step, over 420 days, for 30 seeds: a lease is never left past its end, and every one is offered again.
        int stuck = 0, offers = 0, nothing = 0, renewals = 0, pending = 0;
        foreach (int seed in zero.GetRange(0, 30))
        {
            CoWorld w = NewCoWorld(seed);
            w.L.TrendDay = Epoch + 100000;   // the city's month never turns: the trend stays nothing, so the offers of nothing are the landlords' own draws
            var world = new CoDayWorld(w);
            var seen = new HashSet<string>();
            for (int day = Epoch + 1; day <= Epoch + 420; day++)
            {
                w.Day = day;
                w.Hour = 9;
                CoLeases(w, world, day);
                foreach (LeaseFile l in w.L.Leases)
                {
                    stuck += l.End <= day ? 1 : 0;
                    if (LeaseRules.Made(l) && l.Offer == 0 && seen.Add(l.Place + "@" + l.Asked))
                    {
                        nothing++;
                    }
                }
            }
            offers += w.Offers;
            renewals += w.Renewals;
            foreach (LeaseFile l in w.L.Leases)
            {
                stuck += l.Renewals == 0 && l.End <= Epoch + 420 ? 1 : 0;
                pending += LeaseRules.Made(l) ? 1 : 0;
            }
        }
        Check(nothing >= 30 && stuck == 0 && renewals + pending == offers,
            "zero offers: through the day step both the director and the run take, " + nothing + " offers of nothing over 30 seeds and 420 days were all answered or renewed, none left past its end (" + stuck + " stuck, " + offers + " offers, "
            + renewals + " renewals, " + pending + " still waiting at the end)");

        // With the whole day (the managers' hours, the owner's answers, the hierarchy): the same, for a few seeds.
        int stuckAll = 0;
        foreach (int seed in zero.GetRange(0, 6))
        {
            CoWorld w = NewCoWorld(seed);
            for (int day = Epoch + 1; day <= Epoch + 420; day++)
            {
                CoDay(w, day);
                foreach (LeaseFile l in w.L.Leases)
                {
                    stuckAll += l.End <= day ? 1 : 0;
                }
            }
        }
        Check(stuckAll == 0, "zero offers: and through the whole day of the run (managers at their hour, the owner's answers) no lease is left past its end");

        // A long jump of days over a lease whose offer is nothing: renewed on the first day back, once, and told as a renewal at the same rent.
        CoWorld j = NewCoWorld(zero[2]);
        j.L.TrendDay = Epoch + 100000;
        var jw = new CoDayWorld(j);
        LeaseFile elm = j.L.Of("Elm~7");
        int jump = elm.End + 25;
        j.Day = jump;
        CoLeases(j, jw, jump);
        LeaseFile after = j.L.Of("Elm~7");
        Check(after.Renewals == 1 && after.Rise == 0 && after.Last == 0 && after.End > jump && j.Skipped >= 1, "zero offers: a lease whose notice was skipped renews on the first day back at the same rent (" + after.Renewals + " renewal, rise " + after.Rise + ")");
        Check(LeaseRules.RenewedNote(after) == "reallife_co_note_lease_renewed_same" && LeaseRules.OfferNote(after) == "reallife_co_note_lease_offer_same",
            "zero offers: the notices say the rent stays the same, not that it is up by nothing");
        var rise = new LeaseFile { Place = "x", Start = 1, Term = 100, Rent = 100, Offer = 80, Asked = 90 };
        rise.Last = 80;
        Check(LeaseRules.OfferNote(rise) == "reallife_co_note_lease_offer" && LeaseRules.RenewedNote(rise) == "reallife_co_note_lease_renewed", "zero offers: an offer with a rise is told with its share as before");

        // CompanyDay.Offer by itself with a falling market (the property feature will feed it): the ask is pulled to nothing and the offer is still made.
        var lm = new LeaseFile { Place = "Mill~9", Start = 100, Term = 360, Rent = 200, Rise = 0 };
        CoWorld mw = NewCoWorld(zero[3]);
        var fall = new CoDayWorld(mw);
        OfferWas was = CompanyDay.Offer(fall, "Mill~9", lm, true, 0, 0, 90, RunMonth, lm.End - LeaseRules.NoticeDays);
        Check(was == OfferWas.Manager && lm.Offer == 0 && LeaseRules.Made(lm) && !LeaseRules.OfferDue(lm, lm.End - 10), "zero offers: a market under the rent pulls the ask to nothing and the offer is made all the same (" + was + ")");
        var lm2 = new LeaseFile { Place = "Mill~9", Start = 100, Term = 360, Rent = 200, Rise = 0 };
        OfferWas skipped = CompanyDay.Offer(fall, "Mill~9", lm2, true, 0, 0, 90, RunMonth, lm2.End + 3);
        Check(skipped == OfferWas.Renewed && lm2.Renewals == 1 && lm2.Rise == 0 && lm2.End > lm2.Start + 360, "zero offers: made after the lease had ended, an offer of nothing renews the lease (" + skipped + ")");

        // A rent that goes down is told as a cut, with its figure, not as "the rent stays": a hold-over's premium dropped at the renewal, and a market under the rent that pulls the ask below it.
        var cut = new LeaseFile { Place = "Mill~9", Start = 100, Term = 360, Rent = 200, Rise = 0, Hold = true };
        LeaseRules.Make(cut, 0, cut.End - LeaseRules.NoticeDays);
        bool offerLess = LeaseRules.OfferOver(cut) < 0 && LeaseRules.OfferNote(cut) == "reallife_co_note_lease_offer_less";
        Check(offerLess && LeaseRules.Within(cut, 0), "rent cut: an offer under the rent paid now (the premium of a hold-over dropped) is told as the rent going down (" + LeaseRules.OfferOver(cut) + " per mille)");
        Check(LeaseRules.Settle(cut, cut.End, zero[4], RunMonth) && cut.Last < 0 && !cut.Hold && LeaseRules.RenewedNote(cut) == "reallife_co_note_lease_renewed_less",
            "rent cut: and the renewal that follows says the rent went down (" + cut.Last + " per mille)");
        var drop = new LeaseFile { Place = "Mill~9", Start = 100, Term = 360, Rent = 200, Rise = 150, Renewals = 1, Last = 0 };
        LeaseRules.Make(drop, LeaseRules.Offer(drop, zero[5], 0, 0, 60), drop.End - LeaseRules.NoticeDays);   // a market of 60 under a rent of 200 pulls the ask under the rent now
        Check(drop.Offer < drop.Rise && LeaseRules.OfferOver(drop) < 0 && LeaseRules.OfferNote(drop) == "reallife_co_note_lease_offer_less",
            "rent cut: a falling market pulls an ask under the rent paid now, and it is told as a cut (" + LeaseRules.OfferOver(drop) + " per mille)");

        // The manager with the Leases task answers it without asking anyone, at any limit.
        var ml = new ManagerFile { Place = "Mill~9", Id = "mz", Name = "Mia Cole", Role = ManagerCatalogue.RolePlace, From = 1, Skill = 90, Seen = true, LeaseStep = 0, Tasks = ManagerRules.Bit(ManagerTask.Leases) };
        mw.M.Managers.Add(ml);
        var lm3 = new LeaseFile { Place = "Mill~9", Start = 100, Term = 360, Rent = 200, Rise = 0 };
        LeaseRules.Make(lm3, 0, lm3.End - LeaseRules.NoticeDays);
        LeaseHour hour = CompanyDay.Lease(fall, "Mill~9", ml, lm3, 90, 0, ml.Name, lm3.End - 10, 9);
        Check((hour == LeaseHour.Accepted || hour == LeaseHour.Declined) && lm3.Answer != 0 && mw.LeaseNotices == 0, "zero offers: a manager answers an offer of nothing inside any limit, the owner is not asked (" + hour + ")");

        // The rivals' own leases: the outlet's lease is offered, answered and renewed on the same terms.
        int rivalZero = 0, rivalStuck = 0, rivalRenewed = 0, tried = 0;
        for (int seed = 1; seed <= 400 && tried < 25; seed++)
        {
            LeaseFile probe = LeaseRules.Begin(seed, "rival0#1", RivalFirms.RentBase, 100, RunMonth);
            if (LeaseRules.Offer(probe, seed, 0, 0, 0) != 0)
            {
                continue;
            }
            tried++;
            RivalFile f = RivalFirmFile(seed, 0, 1);
            RivalFirms.Sync(f, seed, 100, RunMonth, RunIndex);
            RivalOutlet o = f.Outlets[0];
            bool sawZero = false;
            for (int day = 100; day < 100 + 1200; day++)
            {
                RivalFirms.Day(f, seed, day, RunMonth, 0, RunIndex, null);
                sawZero |= LeaseRules.Made(o.Lease) && o.Lease.Offer == 0;
                rivalStuck += o.Lease.End < day ? 1 : 0;   // (a lease offered on the day it ended is settled on the next)
            }
            rivalZero += sawZero ? 1 : 0;
            rivalRenewed += o.Renewals >= 1 ? 1 : 0;
        }
        Check(tried >= 20 && rivalZero == tried && rivalStuck == 0 && rivalRenewed == tried, "zero offers: a rival's lease whose offer is nothing is answered and renews like the rest (" + tried + " seeds, " + rivalZero + " with an offer of nothing, " + rivalStuck + " stuck, " + rivalRenewed + " renewed)");

        Console.WriteLine("  an offer of nothing: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed");
    }
}
