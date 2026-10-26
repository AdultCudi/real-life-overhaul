using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Flare;

// Somebody the player swaps numbers with in the street keeps the look the game gave them (People/PersonLook): the look's
// text read back whole, every id with the record's and its own separators in it; its seed the same every time and never 0;
// the social life's save keeping it at the end of the person's record (read back after a load and by the look ahead of
// Flare's faces, a save without it loading as before, an older RLO's 41 fields untouched, a look that cannot be read let go);
// Mom and Alex never anybody's look; the portrait caches naming a kept look's face apart; every place that dresses or draws
// a person from a seed asking for a kept look first. A city person met on Flare keeps their Flare face, and a city person's
// card says the age their face is drawn at (FlareLiveRules.CitizenAge).
internal static partial class Program
{
    private static void StreetLook()
    {
        StreetLookText();
        StreetLookSave();
        StreetLookSources();
        StreetLookFlare();
    }

    private static PersonLook SampleLook()
    {
        var look = new PersonLook { Female = true, Strength = 0.37f, Fatness = 0.81f, Skin = 0xc68e6aff, Eyes = 0x3a5f2bff };
        look.Parts.Add(new LookPart { Type = 1, Variant = "Hair_Long_03", Colour = "hair_brown_2" });
        look.Parts.Add(new LookPart { Type = 3, Variant = "Head_F_07", Colour = null });
        look.Parts.Add(new LookPart { Type = 5, Variant = "Torso|Coat;Wool:Grey+Big%", Colour = "-" });
        look.Parts.Add(new LookPart { Type = 9, Variant = "Feet~Boots,Brown=1\nx", Colour = "shoe_black" });
        look.Shapes.Add(new LookShape { Name = "Nose_Wide", Value = 42.5f, Category = 13, Kind = 0 });
        look.Shapes.Add(new LookShape { Name = "Eyes:Up", Value = 100f, Category = 11, Kind = 1 });
        return look;
    }

    private static bool SameLook(PersonLook a, PersonLook b)
    {
        if (a == null || b == null || a.Female != b.Female || a.Strength != b.Strength || a.Fatness != b.Fatness || a.Skin != b.Skin || a.Eyes != b.Eyes
            || a.Parts.Count != b.Parts.Count || a.Shapes.Count != b.Shapes.Count)
        {
            return false;
        }
        for (int i = 0; i < a.Parts.Count; i++)
        {
            if (a.Parts[i].Type != b.Parts[i].Type || a.Parts[i].Variant != b.Parts[i].Variant || a.Parts[i].Colour != b.Parts[i].Colour)
            {
                return false;
            }
        }
        for (int i = 0; i < a.Shapes.Count; i++)
        {
            if (a.Shapes[i].Name != b.Shapes[i].Name || a.Shapes[i].Value != b.Shapes[i].Value || a.Shapes[i].Category != b.Shapes[i].Category || a.Shapes[i].Kind != b.Shapes[i].Kind)
            {
                return false;
            }
        }
        return true;
    }

    // ---------------------------------------------------------------- the look's text

    private static void StreetLookText()
    {
        PersonLook look = SampleLook();
        string text = look.Encode();
        PersonLook back = PersonLook.Decode(text);
        Check(SameLook(look, back) && back.Encode() == text,
            "street look: a look's text is read back whole (sex, build, skin, eyes, every part's variant and colour, none and '-' told apart, the face's shapes)");
        Check(text.IndexOf('|') < 0 && text.IndexOf('\n') < 0 && text.IndexOf('\r') < 0 && text.IndexOf(',') < 0 && text.IndexOf('~') < 0,
            "street look: its text holds none of the social record's separators, whatever the game's ids hold (" + text + ")");
        int seed = PersonLook.SeedOf(text);
        PersonLook other = SampleLook();
        other.Parts[0] = new LookPart { Type = 1, Variant = "Hair_Long_04", Colour = "hair_brown_2" };
        Check(seed > 0 && seed == PersonLook.SeedOf(text) && seed == PersonLook.SeedOf(back.Encode()) && PersonLook.SeedOf(other.Encode()) != seed && PersonLook.SeedOf(null) > 0,
            "street look: a kept look's seed is its text's, the same every time and on every machine, another for another look, never 0 or negative");
        Check(PersonLook.Decode(null) == null && PersonLook.Decode("") == null && PersonLook.Decode("2" + text.Substring(1)) == null
              && PersonLook.Decode(text.Replace(";f;", ";x;")) == null && PersonLook.Decode(text + ";more") == null
              && PersonLook.Decode(text.Replace("%7C", "%G1")) == null && PersonLook.Decode(text.Replace(";0.37;", ";NaN;")) == null
              && PersonLook.Decode("1;f;0.5;0.5;ffffffff;ffffffff;;") == null && PersonLook.Decode(new string('a', 5000)) == null,
            "street look: anything that is not a whole look of this version is not read as one (another version, a wrong sex, a field more, a broken escape, no number, no parts, too long)");
        var plain = new PersonLook { Female = false, Strength = 0f, Fatness = 1f };
        plain.Parts.Add(new LookPart { Type = 0, Variant = "Male" });
        string plainText = plain.Encode();
        Check(SameLook(plain, PersonLook.Decode(plainText)) && PersonLook.Decode(plainText).Shapes.Count == 0,
            "street look: one part and no shapes, the least a look can be, reads back");

        PersonLooks.Forget();
        Check(PersonLooks.Remember(text) == seed && PersonLooks.Has(seed) && PersonLooks.TryGet(seed, out PersonLook known) && SameLook(known, look)
              && PersonLooks.Remember("not a look") == 0 && PersonLooks.Remember(null) == 0 && PersonLooks.Count == 1 && !PersonLooks.Has(seed + 1),
            "street look: a kept look is known by its seed; a text that is not a look is never kept");
        Check(ContactCatalogue.CacheName(true, 34, seed) == "v1_f34_" + seed + "s.jpg" && ContactCatalogue.CacheName(true, 34, seed + 1) == "v1_f34_" + (seed + 1) + ".jpg"
              && FlareAvatarRules.FaceName(true, 34, seed) == FlareAvatarRules.FilePrefix + "v1_f34_" + seed + "s" + FlareAvatarRules.FileExtension
              && FlareAvatarRules.FaceName(false, 34, 77171) == FlareAvatarRules.FilePrefix + "v1_m34_77171" + FlareAvatarRules.FileExtension,
            "street look: the portrait caches name a kept look's face apart from a rolled face of the same number; every other face keeps its name");
        PersonLooks.Forget();
        Check(PersonLooks.Count == 0 && !PersonLooks.Has(seed) && ContactCatalogue.CacheName(true, 34, seed) == "v1_f34_" + seed + ".jpg",
            "street look: forgotten with the social life's state");
    }

    // ---------------------------------------------------------------- the save

    private static void StreetLookSave()
    {
        var data = new Dictionary<string, string>();
        SocialState.Load(data, 0, 112, 9, 5150);
        SocialPerson met = null;
        for (int i = 2; i < SocialRules.BaseCast && met == null; i++)
        {
            SocialPerson p = SocialState.Get(i);
            if (p != null && !p.Met && !p.IsFamily && p.Origin != SocialOrigin.OldFriend)
            {
                met = p;
            }
        }
        if (met == null)
        {
            Check(false, "street look: the cast has somebody still to meet");
            return;
        }
        int index = met.Index, seatSeed = met.Seed;
        string text = SampleLook().Encode();
        int seed = PersonLooks.Remember(text);
        met.Name = "Rosa Lindqvist";
        met.Female = true;
        met.Age = 47;
        met.Origin = SocialOrigin.Street;
        met.Met = true;
        met.Look = text;
        met.Seed = seed;
        SocialPerson mom = SocialState.ByOrigin(SocialOrigin.Family);
        mom.Look = text;   // (never anybody's: put right as the save is read)
        SocialState.Save(data);
        string people = data["reallife:social_people"];
        string record = null;
        foreach (string r in people.Split('\n'))
        {
            if (r.StartsWith(index + "|", StringComparison.Ordinal))
            {
                record = r;
            }
        }
        string[] f = record != null ? record.Split('|') : new string[0];
        Check(f.Length >= 43 && f[41] == text && f[4] == seed.ToString() && f[1] == "Rosa Lindqvist",
            "street look: the look is saved in field 41 of the person's record, after the 41 fields an older RLO reads (which then has the look of the seed), "
            + "with the age's day and later fields after it");
        Check(seatSeed != seed, "street look: the person met has the look's seed, not the seat's");

        SocialState.Load(data, 3, 112, 9, 999);   // (a load: the state reset, the looks forgotten, then read)
        SocialPerson back = SocialState.Get(index);
        Check(back != null && back.Look == text && back.Seed == seed && back.Name == "Rosa Lindqvist" && back.Female && back.Age == 47 && back.Met
              && PersonLooks.TryGet(seed, out PersonLook known) && SameLook(known, SampleLook()),
            "street look: after a save and a load the person met in the street has the same look and seed, known again to everything that draws them");
        SocialPerson momBack = SocialState.ByOrigin(SocialOrigin.Family);
        Check(momBack.Look == null && momBack.Seed == FixedPeople.MomSeed && SocialState.Get(1).Look == null && SocialState.Get(1).Seed == FixedPeople.AlexSeed,
            "street look: Mom and Alex keep their one look of every game (a look set on them is let go)");

        PersonLooks.Forget();
        var peeked = new List<SocialPerson>();
        SocialState.PeekMet(data, peeked);
        SocialPerson ahead = null;
        foreach (SocialPerson p in peeked)
        {
            if (p.Index == index)
            {
                ahead = p;
            }
        }
        Check(ahead != null && ahead.Seed == seed && PersonLooks.Has(seed), "street look: the look ahead of Flare's faces, behind the loading screen, knows the kept look before the social life reads its save");

        // A save from before 0.29.0 (41 fields): loads as it did, with the seed it has.
        var old = new Dictionary<string, string>(data);
        string[] records = old["reallife:social_people"].Split('\n');
        for (int i = 0; i < records.Length; i++)
        {
            records[i] = string.Join("|", records[i].Split('|'), 0, 41);   // the look, the age's day and every later field taken off
        }
        old["reallife:social_people"] = string.Join("\n", records);
        SocialState.Load(old, 3, 112, 9, 999);
        SocialPerson before = SocialState.Get(index);
        Check(before != null && before.Look == null && before.Seed == seed && before.Name == "Rosa Lindqvist" && before.Met && !PersonLooks.Has(seed),
            "street look: a save without the field (41 fields, as an older RLO writes it) loads whole, the person with the look of their seed");
        // A look that cannot be read is let go; the person keeps the seed they were saved with.
        var broken = new Dictionary<string, string>(data);
        broken["reallife:social_people"] = data["reallife:social_people"].Replace(text, "9;f;garbage");
        SocialState.Load(broken, 3, 112, 9, 999);
        SocialPerson mended = SocialState.Get(index);
        Check(mended != null && mended.Look == null && mended.Seed == seed && mended.Name == "Rosa Lindqvist" && PersonLooks.Count == 0,
            "street look: a look this version cannot read is let go, the person kept with their saved seed");
        SocialState.Reset();
        PersonLooks.Forget();
    }

    // ---------------------------------------------------------------- the code

    private static void StreetLookSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts"));
        string Read(params string[] parts) => File.ReadAllText(System.IO.Path.Combine(scripts, System.IO.Path.Combine(parts)));
        string links = Read("Wiring", "StreetLinks.cs"), talk = Read("Street", "StreetDirector.Talk.cs") + Read("Street", "StreetDirector.Outcomes.cs"), game = Read("Street", "StreetGame.cs");
        Check(links.Contains("int seed = PersonLooks.Remember(m.Look);") && links.Contains("p.Look = m.Look;") && links.Contains("p.Seed = seed;")
              && links.IndexOf("p.Seed = seed;", StringComparison.Ordinal) < links.IndexOf("ContactPictures.Person(SocialPhone.IdOf(p), p.Female, p.Age, p.Seed);", StringComparison.Ordinal)
              && talk.Contains("Look = _hold.Held && !_hold.Gone ? StreetGame.Look(_hold.Body) : null") && game.Contains("PersonLookGame.Of(p.appearanceSetter.data)"),
            "street look: the number swapped takes the figure's own look as the game made it, before the contact's picture is asked for (Street, Wiring/StreetLinks)");
        bool dressers = Read("Social", "SocialCast.cs").Contains("if (!PersonLookGame.Dress(appearance, who.Seed))")
                        && Read("Family", "FamilyCast.cs").Contains("if (!PersonLookGame.Dress(appearance, seed))")
                        && Read("Home", "Roommates", "RoommateCast.cs").Contains("if (!PersonLookGame.Dress(appearance, seed))")
                        && Read("Phone", "Contacts", "PortraitTurn.cs").Contains("if (!PersonLookGame.Dress(setter, seed))")
                        && Read("Phone", "Contacts", "PortraitTurn.cs").Contains("CharacterData kept = PersonLookGame.Data(seed, days);");
        Check(dressers, "street look: everything that dresses or draws a person from a seed (the street character, Family, the roommate, the portrait camera for the contacts and Flare) asks for a kept look first");
        int rolls = 0;
        foreach (string file in Directory.GetFiles(scripts, "*.cs", SearchOption.AllDirectories))
        {
            string text = File.ReadAllText(file);
            if (text.Contains("RandomizeElements(") && text.Contains("who.Seed") && !text.Contains("PersonLookGame.Dress("))
            {
                rolls++;
            }
        }
        Check(rolls == 0, "street look: no look of a person of the social life is rolled from their seed without asking for a kept look");
        Check(Read("Social", "SocialState.cs").Contains("PersonLooks.Forget();") && Read("People", "PersonLookGame.cs").Contains("Dressed.Remove(id);"),
            "street look: the kept looks are forgotten with the social life's state, and a character dressed in one is put back for the next rolled look");
        string dress = Read("People", "PersonLookGame.cs");
        int forget = dress.IndexOf("private static void ForgetGone()", StringComparison.Ordinal);
        string gone = forget >= 0 ? dress.Substring(forget, Math.Min(600, dress.Length - forget)) : "";
        Check(!dress.Contains("Dressed.Clear()") && dress.Contains("Owner = appearance") && gone.Contains("record.Value.Owner == null") && gone.Contains("Dressed.Remove(Gone[i]);"),
            "street look: past 16 characters only the records of characters no longer there are let go, so a rolled face never depends on what was dressed before");
    }

    // ---------------------------------------------------------------- Flare: the face and the age

    private static void StreetLookFlare()
    {
        var districts = new List<string> { "Midtown", "Garment District", "Hell's Kitchen", "Murray Hill", "Bushwick" };
        var citizens = new List<FlareCitizen>();
        bool same = true;
        int checkedCount = 0;
        foreach (int seed in new[] { 77, 4242, 9001 })
        {
            FlareLiveRules.CitizensOf(seed, districts, citizens);
            for (int i = 0; i < citizens.Count; i++)
            {
                FlareCitizen c = citizens[i];
                int card = FlareTextsMap.Age(c, seed);
                FlareAvatarPlan face = FlareAvatarRules.Citizen(seed, c.Index);
                same &= card == FlareLiveRules.CitizenAge(seed, c.Index) && card == c.Age && face.Age == Math.Max(18, Math.Min(85, card)) && face.Female == c.Female;
                checkedCount++;
            }
        }
        Check(same && checkedCount >= 1200, "street look: a city person's card says the age their face is drawn at and their profile has (FlareLiveRules.CitizenAge), for every one of " + checkedCount);
        var who = new RealLifeOverhaul.Texts.TextWho();
        FlareTextsMap.Fill(who, citizens[123], "Midtown", 9001, "Monday 15:10");
        Check(who.Age == FlareLiveRules.CitizenAge(9001, citizens[123].Index), "street look: and the card's speaker is filled with it");

        // A yes on Flare keeps the city person's face for when they are met.
        FlareCitizen hana = citizens[57];
        int faceSeed = FlareAvatarRules.Citizen(9001, hana.Index).Seed;
        FlareInviteAsk a = InviteStranger(5, 60, 100);
        a.Age = FlareTextsMap.Age(hana, 9001);
        a.Face = faceSeed;
        string data = FlareInviteRules.Data(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes);
        FlareInviteBook book = FlareInviteRules.BookOf(new FlareDm { Data = data });
        a.Face = 0;
        FlareInviteBook older = FlareInviteRules.BookOf(new FlareDm { Data = FlareInviteRules.Data(a, FlareInviteAnswer.Yes, FlareInviteWhy.Yes) });
        Check(book != null && book.Face == faceSeed && faceSeed > 0 && book.Age == a.Age && older != null && older.Face == 0,
            "street look: a yes on Flare keeps the city person's face's seed in its message (a yes saved before has none, and keeps the seat's)");
        string links = File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")), "Wiring", "FlareInviteLinks.cs"));
        string invites = File.ReadAllText(System.IO.Path.Combine(System.IO.Path.GetFullPath(System.IO.Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul\Scripts")), "Social", "Flare", "FlareDirector.Invites.cs"));
        Check(links.Contains("p.Seed = b.Face;") && links.Contains("p.Age = Mathf.Clamp(b.Age, 18, 85);") && invites.Contains("a.Face = FlareAvatarRules.Citizen(citySeed, number).Seed;"),
            "street look: somebody met on Flare takes that face and their own age (a retiree of 82 too)");
    }
}
