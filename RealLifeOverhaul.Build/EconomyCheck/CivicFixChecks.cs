using System;
using System.IO;
using RealLifeOverhaul;
using IoPath = System.IO.Path;

// Faults fixed in Scripts/Civic: the canvasser of the player's own council campaign handed out a mayoral candidate's leaflet,
// a fault in Fame, Justice or the company's raises switched civic life off for the session, and a donation could pass its
// cap. What a rule decides is checked by calling it; what only the game's side can do is checked in its source.
internal static partial class Program
{
    private static void CivicFixes()
    {
        CivicStreetPick();
        CivicBridgesGuarded();
        CivicDonationCap();
    }

    // A file of Scripts/Civic as text, comments and all.
    private static string CivicSource(string file)
    {
        string path = IoPath.Combine(LocalesFolder(), @"..\Scripts\Civic", file);
        return File.Exists(path) ? File.ReadAllText(path) : string.Empty;
    }

    private static void CivicStreetPick()
    {
        float[] polls = { 0.5f, 0.3f, 0.2f, 0f };
        Check(OfficeRules.StreetPick(0.1f, polls, 3) == 0 && OfficeRules.StreetPick(0.6f, polls, 3) == 1 && OfficeRules.StreetPick(0.95f, polls, 3) == 2,
            "civic street: a poster, board or canvasser is a candidate's by their share of the polls");
        Check(OfficeRules.StreetPick(1f, polls, 3) == 2 && OfficeRules.StreetPick(0.5f, polls, 1) == 0 && OfficeRules.StreetPick(0.5f, polls, 0) == 0,
            "civic street: a roll past every share is the last candidate's, and a race of one or none is slot 0's");
        bool inRace = true;
        var random = new Random(1234);
        for (int i = 0; i < 2000; i++)
        {
            int pick = OfficeRules.StreetPick((float)random.NextDouble(), polls, 3);
            inRace &= pick >= 0 && pick < 3 && OfficeRules.StreetPick((float)(i / 2000.0), polls, 3) == OfficeRules.StreetPick((float)(i / 2000.0), polls, 3);
        }
        Check(inRace && OfficeRules.OwnCampaign < -1, "civic street: 2,000 picks are slots of the race, the same for the same roll, and the player's own is no slot nor 'none'");

        string dressing = CivicSource("CampaignWorld.Dressing.cs");
        string draw = MemberText(dressing, "private int DrawCandidate()");
        Check(draw.Contains("return OfficeRules.OwnCampaign;") && !draw.Contains("Colour()") && dressing.Contains("PosterIds[Look(DrawCandidate()) % PosterIds.Length]")
              && dressing.Contains("BoardIds[Look(DrawCandidate()) % BoardIds.Length]"),
            "civic street: the player's own council campaign is drawn as itself, never as its poster colour, and coloured only for its posters and boards");
        string world = CivicSource("CampaignWorld.cs");
        string leaflet = MemberText(world, "public void TakeLeaflet()");
        Check(leaflet.Contains("_canvasserFor == OfficeRules.OwnCampaign ? Own()") && leaflet.Contains("_canvasserFor < state.CandidateCount")
              && world.Contains("BoardIds[Look(candidate) % BoardIds.Length]") && MemberText(world, "public bool CanvasserInReach(").Length > 0,
            "civic street: the player's own canvasser hands out the player's leaflet, and another's names a candidate of this race");
        Check(CivicSource("CivicDirector.cs").Contains("_campaign.OwnCandidate = () => Office.State.Me;"), "civic street: the player's own leaflet names the player as a candidate (CivicDirector)");
    }

    private static void CivicBridgesGuarded()
    {
        string bridges = CivicSource("CivicBridges.cs");
        bool guarded = true;
        foreach (string helper in new[] { "internal static void Moved(", "internal static int Summon(", "internal static float RaiseOf(" })
        {
            string body = MemberText(bridges, helper);
            guarded &= body.Contains("try") && body.Contains("catch (Exception e)") && body.Contains("CivicDirector.Logger?.Error(e);");
        }
        Check(guarded, "civic bridges: Standing, the court and the company's raises are asked through helpers that log a fault and go on");

        string bare = null;
        foreach (string path in Directory.GetFiles(IoPath.Combine(LocalesFolder(), @"..\Scripts\Civic"), "*.cs"))
        {
            string name = IoPath.GetFileName(path);
            string code = File.ReadAllText(path);
            if (name != "CivicBridges.cs" && System.Text.RegularExpressions.Regex.IsMatch(code, @"StandingMoved|CivicBridges\.(FileCase|Raise)\b"))
            {
                bare = (bare == null ? "" : bare + ", ") + name;
            }
        }
        Check(bare == null, "civic bridges: no file of civic life calls Standing, the court or the raises but through the helpers" + (bare != null ? " (" + bare + ")" : ""));

        string government = CivicSource("CivicGovernment.cs");
        int day = government.IndexOf("DayBegins?.Invoke(day);", StringComparison.Ordinal);
        int tried = day > 0 ? government.LastIndexOf("try", day, StringComparison.Ordinal) : -1;
        Check(tried > 0 && day - tried < 60 && government.IndexOf("catch (Exception e)", day, StringComparison.Ordinal) - day < 160,
            "civic bridges: the office's day runs inside a catch, as the count's does: a fault there never switches civic life off");
    }

    private static void CivicDonationCap()
    {
        int step = CivicCatalogue.DonationStep, cap = CivicCatalogue.DonationCap;
        Check(CivicCatalogue.DonationFits(0, step, cap) && CivicCatalogue.DonationFits(cap - step, step, cap), "civic donation: a donation up to the cap is taken");
        Check(!CivicCatalogue.DonationFits(cap, step, cap) && !CivicCatalogue.DonationFits(cap - step + 1, step, cap), "civic donation: one that would pass the cap is refused");

        // Prices that move during the campaign: what was given never passes the cap of the day it was given on.
        var random = new Random(77);
        int worst = 0;
        for (int run = 0; run < 500; run++)
        {
            int donated = 0;
            for (int day = 0; day < 20; day++)
            {
                int level = 900 + random.Next(400);   // the price level in tenths of a point
                int stepNow = PriceRules.At(step, level), capNow = PriceRules.At(cap, level);
                if (CivicCatalogue.DonationFits(donated, stepNow, capNow))
                {
                    donated += stepNow;
                    worst = Math.Max(worst, donated - capNow);
                }
            }
        }
        Check(worst <= 0, "civic donation: 500 campaigns at moving prices never pass the day's cap (" + worst + " over at worst)");
        Check(MemberText(CivicSource("CivicGovernment.Player.cs"), "public string Donate(").Contains("CivicCatalogue.DonationFits(State.Donated[candidate], step, CivicCatalogue.DonationCapNow)"),
            "civic donation: the City Hall line refuses a donation that would pass the cap (CivicGovernment.Donate)");
    }
}
