using System.Collections.Generic;
using RealLifeOverhaul.Flare;

internal static partial class Program
{
    private static void FlareUseChecks()
    {
        var small = new FlareBrief
        {
            Followers = 34, Hour = 12, Photo = true, PhotoHere = true, SameDistrict = 1, DayCustomers = 160, BasketCents = 2500,
            Districts = 1, PriceLevel = 1000, Reliability = -1, Seed = 3, RollKey = 1
        };
        FlareExpect a = FlareUseRules.Expect(in small);
        FlareExpect again = FlareUseRules.Expect(in small);
        Check(a.Visitors == 8 && a.Cents == 20000 && a.Percent == 5 && a.Hours == 6 && a.Cost == 0 && a.Gain == 20000 && a.Peak
              && a.Visitors == again.Visitors && a.Cents == again.Cents && a.Reach == again.Reach,
            "Flare use: a pictured post at midday from a new account brings visitors and takings, and the dry run saves nothing (" + a.Visitors + ", " + a.Cents + ")");
        var known = small;
        known.Followers = 5000;
        FlareExpect b = FlareUseRules.Expect(in known);
        Check(b.Visitors == 25 && b.Cents == 62500 && b.Percent == 16 && b.Hours == 8 && b.Visitors > a.Visitors,
            "Flare use: a larger account brings more, inside the tier's cap (" + b.Visitors + ")");
        var guest = small;
        guest.Guest = true;
        FlareExpect none = FlareUseRules.Expect(in guest);
        Check(none.Visitors == 0 && none.Cents == 0 && none.Reach == 0, "Flare use: another player's business is left alone");
        Check(FlareUseRules.JobExtra(0) == 0 && FlareUseRules.JobExtra(5000) == 40 && FlareUseRules.RaiseBp(25000) == 150
              && FlareUseRules.InvestorChance(100000) == 12 && FlareUseRules.VipOff(5000) == 30
              && FlareUseRules.Sponsorships(5000) && !FlareUseRules.Sponsorships(499)
              && FlareUseRules.Verified(100, true) && !FlareUseRules.Verified(499, false)
              && FlareUseRules.NextPerkAt(0) == 500 && FlareUseRules.NextPerkAt(100000) == -1
              && FlareRules.TierFrom[1] == 500 && FlareRules.TierFrom[2] == 5000 && FlareRules.TierFrom[3] == 25000 && FlareRules.TierFrom[4] == 100000,
            "Flare use: perks follow the account's tiers, 500, 5,000, 25,000 and 100,000");
        Check(FlareUseRules.AnswerQuality("Thanks, we were short staffed at lunch and the wait is shorter now.") >= FlareUseRules.GoodAnswer
              && FlareUseRules.AnswerQuality("ok") < FlareUseRules.GoodAnswer
              && FlareUseRules.Soften(1000, 800) == 500 && FlareUseRules.Soften(1000, 200) == 1000,
            "Flare use: a careful answer softens a poor review, a short one does not");
        var notes = new List<FlareReviewNote>();
        var stars = new List<FlareStarRow>();
        Check(FlareUseRules.Reason(40, 80, 90, 85) == FlareReviewWhy.Price && FlareUseRules.WaitMinutes(50) == 15, "Flare use: a review names its weakest point");
        bool turned = false;
        for (int i = 0; i < 6; i++)
        {
            turned |= FlareUseRules.NoteReview(notes, stars, "cafe", i, i < 3 ? 5 : 2, i, (int)FlareReviewWhy.Wait);
        }
        FlareStarRow row = FlareUseRules.StarsOf(stars, "cafe");
        Check(row != null && row.Five == 3 && row.Two == 3 && row.Total == 6 && (turned || row.Earlier >= 0), "Flare use: stars are kept past the ring of six (" + (row != null ? row.Total : 0) + ", turned " + turned + ")");
        Check(FlareUseRules.TrendDemand(0) == 8 && FlareUseRules.TrendDemand(40) == 20 && FlareUseRules.PollYes(4, 2, 1) >= 50 && FlareUseRules.PollYes(4, 2, 1) <= 950,
            "Flare use: a trend is demand you can act on, and a poll has two sides");
        Check(FlareUseRules.MaskCount(7) == 3 && FlareUseRules.InMask(1, 0) && !FlareUseRules.InMask(1, 2), "Flare use: a promotion can name several districts");
        int move = FlareUseRules.RivalMove(9, 3);
        Check(move >= 0 && move <= 2 && FlareUseRules.RivalMove(9, 3) == move, "Flare use: a rival's move is seeded");
        var boosts = new List<FlareBoost> { new FlareBoost { Place = "a~1", FromMinute = 0, Hours = 6, Visitors = 8, Cents = 20000, Percent = 5 } };
        Check(FlareUseRules.Marketing(boosts, "a~1", 30) == 5 && FlareUseRules.Marketing(boosts, "other", 30) == 0, "Flare use: the extra customers last for the post's hours");
        int visitors, cents;
        FlareUseRules.Credit(boosts[0], 60, out visitors, out cents);
        Check(visitors > 0 && cents > 0 && boosts[0].GivenVisitors == visitors, "Flare use: an hour credits its share once (" + visitors + ")");
        FlareUseRules.Credit(boosts[0], 60, out visitors, out cents);
        Check(visitors == 0 && cents == 0, "Flare use: the same hour is not credited twice");
        var file = FlareRules.Open(5, 1, 0, 50);
        file.Boosts.Add(boosts[0]);
        file.Bio = "Cafe on the corner";
        file.ReachDay = 4;
        file.ReachDays[29] = 120;
        string account = FlareStore.WriteAccount(file);
        FlareFile back = FlareStore.Read(account, null);
        Check(back != null && back.Boosts.Count == 1 && back.Boosts[0].Visitors == 8 && back.Bio == "Cafe on the corner" && back.ReachDays[29] == 120
              && FlareStore.WriteAccount(back) == account,
            "Flare use: the post's result and the reach series survive the save");
        Check(FlareUseRules.PhotoPlace(null, "shop") == "shop" && FlareUseRules.PhotoPlace("sidecar", "shop") == "sidecar",
            "Flare use: a picture's place is Flare's until Life's sidecar is there");
        bool accepted;
        long fee = FlareUseRules.CounterFee(10000, 5000, 60, out accepted);
        Check(fee >= 10000, "Flare use: a counter asks at least the offer (" + fee + ", " + accepted + ")");
        Check(FlareBrand.JobExtra() == 0 && FlareBrand.InvestorExtra() == 0, "Flare use: with the account unset, a job and an investor are unchanged");
    }
}
