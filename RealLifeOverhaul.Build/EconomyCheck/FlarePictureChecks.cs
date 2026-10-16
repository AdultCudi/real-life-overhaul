using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;
using System.Text.Json;
using RealLifeOverhaul.Flare;

// Flare's pictures (FlareAvatarRules, FlareAvatars, FlareFacePainter, FlareCityPhotoRules, FlareCityCamera,
// FlareDirector.Photos): the owner's words "nobody has profile pictures, nobody is posting photos, only texts". Held here:
// every kind of account gets a picture of the right kind (a face for a person, a logo for a business, an emblem for an
// office, never a face for a rival tycoon), the same one every time and on every machine; the round picture is made right
// at every edge; the kept textures have a bound and the oldest goes; the city's photos come on the share the option says,
// for the same posts on a host and a guest, in their window, at their pace, a dozen a day at most; a photo goes with its
// post; nothing new is sent in Going Public; nothing runs per frame and every render waits behind its budget (source
// checks); the options and texts are there in three languages.
internal static partial class Program
{
    private static void FlarePictureChecks()
    {
        var watch = Stopwatch.StartNew();
        int before = _run, failedBefore = _failed;
        AvatarKinds();
        AvatarSame();
        AvatarDisc();
        AvatarLru();
        PhotoSubjects();
        PhotoShare();
        PhotoTiming();
        PhotoFiles();
        PicturesShared();
        PicturesSources();
        PicturesTexts();
        PicturesCost();
        Console.WriteLine("  flare pictures: " + (_run - before) + " checks, " + (_failed - failedBefore) + " failed, " + watch.ElapsedMilliseconds + " ms");
    }

    private static FlareAvatarFacts PictureFacts()
    {
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Net.CitySeed = 4242;
        f.Influencers.Add(new FlareInfluencer { Id = 3, Handle = "@niche", Seed = 991 });
        f.Influencers.Add(new FlareInfluencer { Id = 4, Handle = "@friend", Seed = 17, Person = 6 });
        var people = new List<FlarePerson>
        {
            new FlarePerson { Index = 6, First = "Alex", Female = true, Age = 31, LookSeed = 123457 },
            new FlarePerson { Index = 8, First = "Sam" }   // a friend the social life gave no look
        };
        return new FlareAvatarFacts { File = f, People = people, CitySeed = FlareLiveRules.CitySeed(f), MyBusinesses = new List<string> { "Copper Kettle" } };
    }

    private static void AvatarKinds()
    {
        FlareAvatarFacts facts = PictureFacts();
        FlareAvatarPlan me = FlareAvatarRules.Plan("me", facts);
        Check(me.Kind == FlareAvatarKind.Player && me.Name == null, "Flare pictures: the player with no selfie posted shows their character's portrait");
        facts.File.Posts.Add(new FlarePost { Id = 1, Author = FlareAuthor.Player, Source = FlareSource.Selfie, Name = "Selfie_1.jpg", Day = 1 });
        facts.File.Posts.Add(new FlarePost { Id = 2, Author = FlareAuthor.Player, Source = FlareSource.Photo, Name = "Photo_2.jpg", Day = 1 });
        facts.File.Posts.Add(new FlarePost { Id = 3, Author = FlareAuthor.Player, Source = FlareSource.Selfie, Pic = "f:Selfie_3.jpg", Day = 2 });
        facts.File.Posts.Add(new FlarePost { Id = 4, Author = FlareAuthor.Player, Source = FlareSource.Selfie, Pic = "f:Selfie_4.jpg", Day = 2, ReplyTo = 1 });
        Check(FlareAvatarRules.Plan("me", facts).Name == "f:Selfie_3.jpg", "Flare pictures: the player shows their latest selfie posted (not a photo, not a reply)");

        FlareAvatarPlan c = FlareAvatarRules.Plan("c7", facts);
        var citizens = new List<FlareCitizen>();
        FlareLiveRules.CitizensOf(facts.CitySeed, null, citizens);
        var women = new HashSet<string> { "Ada", "Bea", "Dina", "Fay", "Hana", "Juno", "Lena", "Nell", "Pia", "Rae", "Tess", "Uma", "Xena", "Yara", "Bree", "Elsa" };
        bool sexes = true;
        for (int i = 0; i < citizens.Count; i++)
        {
            FlareAvatarPlan pi = FlareAvatarRules.Plan("c" + i, facts);
            sexes &= pi.Kind == FlareAvatarKind.Face && pi.Female == women.Contains(citizens[i].First) && pi.Age >= FlareAvatarRules.AgeLeast && pi.Age < FlareAvatarRules.AgeLeast + FlareAvatarRules.AgeSpan && pi.Seed >= 1;
        }
        Check(c.Kind == FlareAvatarKind.Face && sexes, "Flare pictures: each of the city's people is a face of the sex their first name says, of an adult age, from a look's seed");

        FlareAvatarPlan friend = FlareAvatarRules.Plan("f6", facts), partner = FlareAvatarRules.Plan("p6", facts);
        Check(friend.Kind == FlareAvatarKind.Face && friend.Female && friend.Age == 31 && friend.Seed == 123457 && partner.Picture == friend.Picture,
            "Flare pictures: a friend or the partner is the face of the look the social life builds them in the street with");
        FlareAvatarPlan unknown = FlareAvatarRules.Plan("f8", facts);
        Check(unknown.Kind == FlareAvatarKind.Face && unknown.Seed >= 1 && unknown.Picture == FlareAvatarRules.Plan("f8", facts).Picture, "Flare pictures: a friend with no look told is a face of the account's own, the same each time");

        FlareAvatarPlan inf = FlareAvatarRules.Plan("i:3", facts), infFriend = FlareAvatarRules.Plan("i:4", facts);
        Check(inf.Kind == FlareAvatarKind.Face && infFriend.Picture == friend.Picture && FlareAvatarRules.Plan("i:99", facts).Kind == FlareAvatarKind.Initials,
            "Flare pictures: an influencer is a face of their own, or the friend they are; one not in the city keeps the initials");
        FlareAvatarPlan staff = FlareAvatarRules.Plan("s:Robin Hale", facts), boss = FlareAvatarRules.Plan("b", facts);
        Check(staff.Kind == FlareAvatarKind.Staff && staff.Name == "Robin Hale" && boss.Kind == FlareAvatarKind.Face, "Flare pictures: one of the staff is the face the game has of them; the boss a face of the account's own");

        FlareAvatarPlan mine = FlareAvatarRules.Plan("k:Copper Kettle", facts), theirs = FlareAvatarRules.Plan("k:Nook", facts), brand = FlareAvatarRules.Plan("b:Jensen", facts);
        Check(mine.Kind == FlareAvatarKind.Logo && mine.Mine && mine.Name == "Copper Kettle" && theirs.Kind == FlareAvatarKind.Logo && !theirs.Mine && brand.Kind == FlareAvatarKind.Logo && !brand.Mine,
            "Flare pictures: a business or a brand shows the game's logo of it (the player's own from the save's folder)");
        Check(FlareAvatarRules.Plan("o:1", facts).Emblem == FlareAvatarRules.EmblemPolice && FlareAvatarRules.Plan("o:2", facts).Emblem == FlareAvatarRules.EmblemTransit
              && FlareAvatarRules.Plan("o:3", facts).Emblem == FlareAvatarRules.EmblemUtilities && FlareAvatarRules.Plan("o:4", facts).Emblem == FlareAvatarRules.EmblemBank
              && FlareAvatarRules.Plan("o:0", facts).Emblem == FlareAvatarRules.EmblemCity && FlareAvatarRules.Plan("l:ledger", facts).Emblem == FlareAvatarRules.EmblemLedger
              && FlareAvatarRules.Plan("m:Acme", facts).Emblem == FlareAvatarRules.EmblemCompany && FlareAvatarRules.Plan("c:Jo Park", facts).Emblem == FlareAvatarRules.EmblemCandidate,
            "Flare pictures: the official accounts, the paper, a listed company and a candidate's campaign show their emblems");
        FlareAvatarPlan tycoon = FlareAvatarRules.Plan("t:Elon Dusk", facts);
        Check(tycoon.Kind == FlareAvatarKind.Emblem && tycoon.Emblem == FlareAvatarRules.EmblemTycoon && tycoon.Kind != FlareAvatarKind.Face, "Flare pictures: a rival tycoon shows an emblem, never a face (no likeness of anybody)");

        Check(FlareAvatarRules.Plan("p:abc", facts).Kind == FlareAvatarKind.Initials, "Flare pictures: another player with no selfie in the shared feed keeps the initials (no face that is not theirs)");
        facts.File.Posts.Add(new FlarePost { Id = 9, Author = FlareAuthor.Network, AuthorId = "abc", Source = FlareSource.Selfie, Pic = "n:abc/4", Shared = true, Day = 2 });
        facts.File.Posts.Add(new FlarePost { Id = 10, Author = FlareAuthor.Network, AuthorId = "xyz", Source = FlareSource.Selfie, Pic = "n:xyz/1", Shared = true, Day = 2 });
        FlareAvatarPlan net = FlareAvatarRules.Plan("p:abc", facts);
        Check(net.Kind == FlareAvatarKind.Selfie && net.Name == "n:abc/4", "Flare pictures: another player of a session shows their latest selfie the session already shared, and nobody else's");
        Check(FlareAvatarRules.Plan(null, facts).Kind == FlareAvatarKind.Initials && FlareAvatarRules.Plan("", facts).Kind == FlareAvatarKind.Initials && FlareAvatarRules.Plan("z:what", facts).Kind == FlareAvatarKind.Initials
              && FlareAvatarRules.Plan("cx", facts).Kind == FlareAvatarKind.Initials && FlareAvatarRules.Plan("k:", facts).Kind == FlareAvatarKind.Initials && FlareAvatarRules.Plan("me", null).Kind == FlareAvatarKind.Initials,
            "Flare pictures: no key, a key of no kind or a damaged one keeps the initials");

        // Every author of a post has a key the plans give a picture to (another player's: their selfie, which they have here).
        bool every = true;
        string missing = string.Empty;
        foreach (FlareAuthor author in (FlareAuthor[])Enum.GetValues(typeof(FlareAuthor)))
        {
            var p = new FlarePost { Author = author, Who = author == FlareAuthor.Official ? 1 : 3, Name = "Name", AuthorId = "abc" };
            if (author == FlareAuthor.Friend)
            {
                p.Who = 6;
            }
            string key = FlareLiveRules.KeyOf(p);
            FlareAvatarKind kind = FlareAvatarRules.Plan(key, facts).Kind;
            if (kind == FlareAvatarKind.Initials)
            {
                every = false;
                missing += " " + author;
            }
        }
        Check(every, "Flare pictures: every kind of account has a picture, none left with only its initials" + (missing.Length > 0 ? " (not:" + missing + ")" : string.Empty));
    }

    private static void AvatarSame()
    {
        FlareAvatarFacts a = PictureFacts(), b = PictureFacts();
        bool same = true;
        string[] keys = { "me", "c0", "c39", "f6", "f8", "p6", "i:3", "i:4", "s:Robin", "b", "k:Nook", "b:Jensen", "o:2", "l:ledger", "m:Acme", "c:Jo", "t:Dusk" };
        for (int i = 0; i < keys.Length; i++)
        {
            FlareAvatarPlan x = FlareAvatarRules.Plan(keys[i], a), y = FlareAvatarRules.Plan(keys[i], b);
            same &= x.Kind == y.Kind && x.Female == y.Female && x.Age == y.Age && x.Seed == y.Seed && x.Name == y.Name && x.Emblem == y.Emblem && x.Picture == y.Picture;
        }
        Check(same, "Flare pictures: the same account gets the same picture every time, on every machine with the same city");
        var distinct = new HashSet<string>();
        var seeds = new HashSet<int>();
        for (int i = 0; i < FlareLiveRules.Citizens; i++)
        {
            distinct.Add(FlareAvatarRules.Plan("c" + i, a).Picture);
            seeds.Add(FlareAvatarRules.Plan("c" + i, a).Seed);
        }
        Check(distinct.Count == FlareLiveRules.Citizens && seeds.Count == FlareLiveRules.Citizens, "Flare pictures: each of the city's people has a face of their own (" + distinct.Count + " of " + FlareLiveRules.Citizens + ")");
        FlareAvatarFacts other = PictureFacts();
        other.CitySeed = 99;
        Check(FlareAvatarRules.Plan("c5", other).Picture != FlareAvatarRules.Plan("c5", a).Picture, "Flare pictures: another city (another seed) has other faces");
        Check(FlareAvatarRules.FaceName(true, 30, 5) == "flare96_v1_f30_5.thumb" && FlareAvatarRules.FaceName(true, 30, 5) != FlareAvatarRules.FaceName(false, 30, 5)
              && FlareAvatarRules.StaffName("save", "Robin") == FlareAvatarRules.StaffName("save", "Robin") && FlareAvatarRules.StaffName("save", "Robin") != FlareAvatarRules.StaffName("other", "Robin")
              && FlareAvatarRules.StaffName("save", "Robin").StartsWith(FlareAvatarRules.FilePrefix, StringComparison.Ordinal),
            "Flare pictures: a face's file is named by its look, one of the staff's by the save and the name, the same each time");
    }

    private static void AvatarDisc()
    {
        int s = FlareAvatarRules.Size;
        var into = new byte[s * s * 4];
        var grey = new byte[256 * 256 * 3];
        for (int i = 0; i < grey.Length; i++)
        {
            grey[i] = 100;
        }
        Check(FlareAvatarRules.Disc(grey, 256, 256, 3, false, into), "Flare pictures: a portrait is made round");
        int corner = into[3], middle = into[((s / 2) * s + s / 2) * 4 + 3], edge = into[((s / 2) * s + 0) * 4 + 3];
        Check(corner == 0 && into[((s - 1) * s + s - 1) * 4 + 3] == 0 && middle == 255 && into[((s / 2) * s + s / 2) * 4] == 100 && edge > 0 && edge < 255,
            "Flare pictures: the corners are clear, the middle whole, the edge soft, the colour kept (corner " + corner + ", middle " + middle + ", edge " + edge + ")");
        // A wide picture: its middle square is taken (the outer columns, red, are left out).
        int w = 300, h = 100;
        var wide = new byte[w * h * 3];
        for (int y = 0; y < h; y++)
        {
            for (int x = 0; x < w; x++)
            {
                int o = (y * w + x) * 3;
                wide[o] = (byte)(x < 100 || x >= 200 ? 255 : 0);
                wide[o + 2] = (byte)(x < 100 || x >= 200 ? 0 : 200);
            }
        }
        FlareAvatarRules.Disc(wide, w, h, 3, false, into);
        bool middleOnly = true;
        for (int i = 0; i < s * s; i++)
        {
            middleOnly &= into[i * 4] == 0 && into[i * 4 + 2] == 200;
        }
        Check(middleOnly, "Flare pictures: a wide selfie gives its middle square");
        var argb = new byte[4 * 4 * 4];
        for (int i = 0; i < 16; i++)
        {
            argb[i * 4] = 255;
            argb[i * 4 + 1] = 10;
            argb[i * 4 + 2] = 20;
            argb[i * 4 + 3] = 30;
        }
        FlareAvatarRules.Disc(argb, 4, 4, 4, true, into);
        int mid = ((s / 2) * s + s / 2) * 4;
        Check(into[mid] == 10 && into[mid + 1] == 20 && into[mid + 2] == 30, "Flare pictures: the game's ARGB picture is read in its own order");
        Check(!FlareAvatarRules.Disc(null, 4, 4, 3, false, into) && !FlareAvatarRules.Disc(grey, 0, 4, 3, false, into) && !FlareAvatarRules.Disc(grey, 4, 4, 2, false, into)
              && !FlareAvatarRules.Disc(new byte[10], 4, 4, 3, false, into) && !FlareAvatarRules.Disc(grey, 4, 4, 3, false, new byte[10]) && FlareAvatarRules.Disc(new byte[3], 1, 1, 3, false, into),
            "Flare pictures: a source that is not whole makes no picture; one pixel makes one");
        FlareAvatarRules.Disc(grey, 256, 256, 3, false, into);
        var rgb = new byte[s * s * 3];
        var back = new byte[s * s * 4];
        FlareAvatarRules.ColourOf(into, rgb);
        Check(FlareAvatarRules.FromColour(rgb, s, s, back) && SameBytes(into, back) && !FlareAvatarRules.FromColour(rgb, s - 1, s, back),
            "Flare pictures: a round picture comes back whole from its file's colour rows; rows of another size are not one");
    }

    private static bool SameBytes(byte[] a, byte[] b)
    {
        if (a.Length != b.Length)
        {
            return false;
        }
        for (int i = 0; i < a.Length; i++)
        {
            if (a[i] != b[i])
            {
                return false;
            }
        }
        return true;
    }

    private static void AvatarLru()
    {
        var lru = new FlareAvatarLru(3);
        Check(lru.Use("a") == null && lru.Use("b") == null && lru.Use("c") == null && lru.Count == 3, "Flare pictures: the kept textures fill up to their bound");
        Check(lru.Use("a") == null && lru.Use("d") == "b" && lru.Count == 3 && lru.Contains("a") && !lru.Contains("b"), "Flare pictures: a texture used again stays; the one used longest ago goes first");
        lru.Remove("c");
        Check(lru.Count == 2 && lru.Use("e") == null && lru.Use(null) == null, "Flare pictures: one taken out leaves room");
        var big = new FlareAvatarLru(FlareAvatarRules.Most);
        int gone = 0;
        for (int i = 0; i < 1000; i++)
        {
            gone += big.Use("k" + (i % 200)) != null ? 1 : 0;
        }
        Check(big.Count == FlareAvatarRules.Most && gone == 1000 - FlareAvatarRules.Most && FlareAvatarRules.Most * FlareAvatarRules.Size * FlareAvatarRules.Size * 4 <= 2500 * 1024,
            "Flare pictures: never more than " + FlareAvatarRules.Most + " textures, 2.3 MB at most, however many accounts are scrolled past");
        Check(new FlareAvatarLru(0).Most == 1, "Flare pictures: a bound of none is one");
    }

    // ---------------------------------------------------------------- the city's photos

    private static FlarePost TalkPost(int id, RealLifeOverhaul.Flare.FlareTalk topic, string district = "ba:district_a", string place = null)
    {
        return new FlarePost { Id = id, Author = FlareAuthor.Citizen, Who = 2, Source = FlareSource.CityTalk, Arg = (int)topic * 10, Day = 3, Hour = 10, District = district, Place = place, Seed = id * 7919 + 13 };
    }

    private static void PhotoSubjects()
    {
        Check(FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Weather)) == FlarePhotoSubject.Sky && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Accident)) == FlarePhotoSubject.Street
              && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Fire)) == FlarePhotoSubject.Street && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Event)) == FlarePhotoSubject.Street
              && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Food, place: "Main~3")) == FlarePhotoSubject.Front && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Night)) == FlarePhotoSubject.Street,
            "Flare city photos: the weather is the street and the sky, an accident, a fire or an event the street, food where it is eaten");
        Check(FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Prices)) == FlarePhotoSubject.None && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Market)) == FlarePhotoSubject.None
              && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Policy)) == FlarePhotoSubject.None && FlareCityPhotoRules.Subject(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Election)) == FlarePhotoSubject.None,
            "Flare city photos: prices, the market, a policy or the election have nothing to see");
        var shop = new FlarePost { Id = 2, Author = FlareAuthor.Business, Source = FlareSource.BusinessPost, Place = "Main~3", Name = "Nook" };
        var niche = new FlarePost { Id = 3, Author = FlareAuthor.Influencer, Source = FlareSource.Niche };
        var tycoon = new FlarePost { Id = 4, Author = FlareAuthor.Tycoon, Source = FlareSource.CityNews, Place = "High~9" };
        Check(FlareCityPhotoRules.Subject(shop) == FlarePhotoSubject.Front && FlareCityPhotoRules.Subject(niche) == FlarePhotoSubject.Street && FlareCityPhotoRules.Subject(tycoon) == FlarePhotoSubject.Front,
            "Flare city photos: a business's shop front, an influencer's niche, a tycoon's new building");
        var own = new FlarePost { Id = 5, Author = FlareAuthor.Player, Source = FlareSource.Photo };
        var reply = TalkPost(6, RealLifeOverhaul.Flare.FlareTalk.Weather);
        reply.ReplyTo = 1;
        var report = new FlarePost { Id = 7, Author = FlareAuthor.Citizen, Source = FlareSource.Report };
        var official = new FlarePost { Id = 8, Author = FlareAuthor.Official, Source = FlareSource.Official };
        var shopNoPlace = new FlarePost { Id = 9, Author = FlareAuthor.Business, Source = FlareSource.BusinessPost };
        Check(FlareCityPhotoRules.Subject(own) == FlarePhotoSubject.None && FlareCityPhotoRules.Subject(reply) == FlarePhotoSubject.None && FlareCityPhotoRules.Subject(report) == FlarePhotoSubject.None
              && FlareCityPhotoRules.Subject(official) == FlarePhotoSubject.None && FlareCityPhotoRules.Subject(shopNoPlace) == FlarePhotoSubject.None && FlareCityPhotoRules.Subject(null) == FlarePhotoSubject.None,
            "Flare city photos: never the player's own post, a reply, a report, an official notice or a business with no place");
    }

    private static void PhotoShare()
    {
        int seed = 4242;
        int[] got = new int[FlareCityPhotoRules.Options];
        const int Posts = 20000;
        for (int option = 0; option < FlareCityPhotoRules.Options; option++)
        {
            for (int i = 0; i < Posts; i++)
            {
                got[option] += FlareCityPhotoRules.Wants(TalkPost(i + 1, RealLifeOverhaul.Flare.FlareTalk.Weather), seed, option) ? 1 : 0;
            }
        }
        bool near = true;
        for (int option = 0; option < FlareCityPhotoRules.Options; option++)
        {
            int want = FlareCityPhotoRules.Share(option) * Posts / 1000;
            near &= Math.Abs(got[option] - want) <= Posts / 50;
        }
        Check(got[0] == 0 && near && got[1] < got[2] && got[2] < got[3], "Flare city photos: the option's share of the posts carry a photo (none " + got[0] + ", a few " + got[1] + ", some " + got[2] + ", many " + got[3] + " of " + Posts + ")");
        Check(FlareCityPhotoRules.Share(-5) == 0 && FlareCityPhotoRules.Share(99) == FlareCityPhotoRules.Share(FlareCityPhotoRules.Options - 1) && FlareCityPhotoRules.OptionDefault == 1
              && FlareCityPhotoRules.Share(FlareCityPhotoRules.OptionDefault) <= 200,
            "Flare city photos: a damaged option is read within range, and the default is a modest share");
        bool same = true;
        for (int i = 0; i < 500; i++)
        {
            same &= FlareCityPhotoRules.Wants(TalkPost(i, RealLifeOverhaul.Flare.FlareTalk.Event), seed, 2) == FlareCityPhotoRules.Wants(TalkPost(i, RealLifeOverhaul.Flare.FlareTalk.Event), seed, 2);
        }
        Check(same && !FlareCityPhotoRules.Wants(TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Prices), seed, 3), "Flare city photos: the same post always carries a photo or never; a post with nothing to see never");
    }

    private static void PhotoTiming()
    {
        FlarePost p = TalkPost(1, RealLifeOverhaul.Flare.FlareTalk.Weather);
        long at = p.At;
        Check(!FlareCityPhotoRules.Due(p, at - 1) && FlareCityPhotoRules.Due(p, at) && FlareCityPhotoRules.Due(p, at + FlareCityPhotoRules.WindowMinutes) && !FlareCityPhotoRules.Due(p, at + FlareCityPhotoRules.WindowMinutes + 1) && !FlareCityPhotoRules.Due(null, at),
            "Flare city photos: a photo is taken at the time of its post, within its window, never before and never long after");
        Check(FlareCityPhotoRules.InRange(0f, 0f) && FlareCityPhotoRules.InRange(FlareCityPhotoRules.FrontRange, 0f) && !FlareCityPhotoRules.InRange(FlareCityPhotoRules.FrontRange + 1f, 0f)
              && !FlareCityPhotoRules.InRange(200f, 200f), "Flare city photos: a business's front only within reach of the player");
        Check(FlareCityPhotoRules.SameDistrict(p, "ba:district_a") && !FlareCityPhotoRules.SameDistrict(p, "ba:district_b") && !FlareCityPhotoRules.SameDistrict(p, null)
              && FlareCityPhotoRules.SameDistrict(TalkPost(2, RealLifeOverhaul.Flare.FlareTalk.Weather, null), null), "Flare city photos: a street only in the district the player is in");
        float pace = FlareCityPhotoRules.PaceLeast;
        pace = FlareCityPhotoRules.NextPace(pace, FlareCityPhotoRules.SlowMs + 1);
        float slower = pace;
        for (int i = 0; i < 10; i++)
        {
            pace = FlareCityPhotoRules.NextPace(pace, 80.0);
        }
        Check(slower == FlareCityPhotoRules.PaceLeast * 2f && pace == FlareCityPhotoRules.PaceMost && FlareCityPhotoRules.NextPace(pace, 3.0) == FlareCityPhotoRules.PaceLeast && FlareCityPhotoRules.PaceLeast >= 10f,
            "Flare city photos: the pace doubles after a slow photo, up to " + FlareCityPhotoRules.PaceMost + " s, and is back to " + FlareCityPhotoRules.PaceLeast + " s after a quick one");
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Net.CitySeed = 4242;
        int seed = FlareLiveRules.CitySeed(f);
        int wanted = -1;
        for (int i = 1; i < 400 && wanted < 0; i++)
        {
            wanted = FlareCityPhotoRules.Wants(TalkPost(i, RealLifeOverhaul.Flare.FlareTalk.Weather), seed, 3) ? i : -1;
        }
        f.Posts.Add(TalkPost(wanted, RealLifeOverhaul.Flare.FlareTalk.Weather));
        f.Posts.Add(TalkPost(1000, RealLifeOverhaul.Flare.FlareTalk.Prices));
        var tried = new HashSet<int>();
        long now = f.Posts[0].At + 10;
        FlarePost next = FlareCityPhotoRules.Next(f, seed, 3, now, tried);
        tried.Add(wanted);
        Check(next != null && next.Id == wanted && FlareCityPhotoRules.Next(f, seed, 3, now, tried) == null && FlareCityPhotoRules.Next(f, seed, 0, now, new HashSet<int>()) == null
              && FlareCityPhotoRules.Next(f, seed, 3, now + 500, new HashSet<int>()) == null, "Flare city photos: the next photo is a post due, wanted and not tried; none with the option off or past its window");
        Check(FlareCityPhotoRules.DayMost <= 12 && FlareCityPhotoRules.Width * FlareCityPhotoRules.Height * 3 <= 200 * 1024, "Flare city photos: a dozen a game day at most, each small copy under 200 KB");
    }

    private static void PhotoFiles()
    {
        Check(FlareCityPhotoRules.IdOf(FlareCityPhotoRules.FileName(42) + ".thumb") == 42 && FlareCityPhotoRules.IdOf("City_.png.thumb") == -1 && FlareCityPhotoRules.IdOf("City_x.png.thumb") == -1
              && FlareCityPhotoRules.IdOf("City_-3.png.thumb") == -1 && FlareCityPhotoRules.IdOf("Photo_1.png.thumb") == -1 && FlareCityPhotoRules.IdOf(null) == -1 && FlareCityPhotoRules.IdOf("City_5.png") == -1,
            "Flare city photos: a photo's file names its post, and nothing else is read as one");
        FlareFile f = FlareRules.Open(5, 1, 0, 50);
        f.Posts.Add(new FlarePost { Id = 3 });
        f.Posts.Add(new FlarePost { Id = 8 });
        var gone = new List<int>();
        Check(FlareCityPhotoRules.Stale(new List<int> { 3, 5, 8, 11 }, f, gone) == 2 && gone.Contains(5) && gone.Contains(11) && !gone.Contains(3), "Flare city photos: a photo goes with its post");
        FlareFile many = FlareRules.Open(5, 1, 0, 50);
        var files = new List<int>();
        for (int i = 0; i < FlareCityPhotoRules.FilesMost + 30; i++)
        {
            many.Posts.Add(new FlarePost { Id = 1000 - i });
            files.Add(1000 - i);
        }
        FlareCityPhotoRules.Stale(files, many, gone);
        bool oldest = gone.Count == 30;
        for (int i = 0; i < gone.Count; i++)
        {
            oldest &= gone[i] <= 1000 - FlareCityPhotoRules.FilesMost;
        }
        Check(oldest && FlareCityPhotoRules.Stale(new List<int>(), many, gone) == 0, "Flare city photos: never more than " + FlareCityPhotoRules.FilesMost + " kept for a save; the oldest go first");
        string photos = ReviewSource("Social/Flare/FlareDirector.Photos.cs"), director = ReviewSource("Social/Flare/FlareDirector.cs");
        Check(FlareMethod(director, "private void PlayDay").Contains("            PhotosLook();   // (the photos of posts gone") && FlareMethod(director, "private void StandDown").Contains("PhotosStop()") && FlareMethod(director, "private void StartUp").Contains("PhotosStart()")
              && FlareMethod(photos, "private void PhotosStart").Contains("PhotosLook()"), "Flare city photos: the photos of gone posts are deleted when Flare starts and each game day");
        Check(!photos.Contains(".Pic =") && !photos.Contains("FlareStore"), "Flare city photos: nothing is written into a post or the save (which post has one is worked out again)");
        string links = ReviewSource("Wiring/PhotosLinks.cs");
        Check(links.Contains("FlareBridges.PictureFolder = () => PhotoStore.Folder(") && links.Contains("PhotoStore.MarkSave(") && FlareMethod(photos, "private IEnumerator PhotoTake").Contains("MarkPictureFolder"),
            "Flare city photos: they are kept in the save's photos' folder, marked as the save's, so a deleted save's go with its photos");
    }

    private static void PicturesShared()
    {
        // A post of the city's on the host (its own number) and the guest's copy of it (another number of the guest's, the host's as its session number).
        bool same = true;
        for (int i = 1; i < 400; i++)
        {
            var host = new FlarePost { Id = i, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Arg = (int)RealLifeOverhaul.Flare.FlareTalk.Weather * 10, Seed = 11 * i };
            var guest = new FlarePost { Id = 5000 + i, NetId = i, Shared = true, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk, Arg = (int)RealLifeOverhaul.Flare.FlareTalk.Weather * 10, Seed = 987 * i };
            same &= FlareCityPhotoRules.Wants(host, 4242, 2) == FlareCityPhotoRules.Wants(guest, 4242, 2);
        }
        Check(same, "Flare city photos: a host and a guest agree which shared posts carry a photo (the city's seed and the post's session number)");
        string net = ReviewSource("Social/Flare/FlareNetShare.cs"), share = ReviewSource("Social/Flare/FlareDirector.Share.cs");
        Check(!net.Contains("CityPhoto") && !net.Contains("Avatar") && !share.Contains("CityPhoto") && !share.Contains("Avatar") && !net.Contains("City_"),
            "Flare pictures: nothing about faces or city photos is sent in a session (each machine makes its own)");
        string photos = ReviewSource("Social/Flare/FlareDirector.Photos.cs"), avatars = ReviewSource("Social/Flare/FlareAvatars.cs");
        Check(!photos.Contains("Shared_") && !avatars.Contains("Shared_") && !photos.Contains("SendPicture") && !avatars.Contains("SendPicture"),
            "Flare pictures: no picture of these is handed to the session's sharing");
        string rules = ReviewSource("Social/Flare/FlareAvatarRules.cs");
        Check(FlareMethod(rules, "public static string LatestSelfie").Contains("\"n:\"") && FlareMethod(rules, "public static FlareAvatarPlan Citizen").Contains("citySeed"),
            "Flare pictures: another player's picture is the selfie the session already shares; the city's faces come from the city's seed");
    }

    private static void PicturesSources()
    {
        string avatars = ReviewSource("Social/Flare/FlareAvatars.cs"), painter = ReviewSource("Social/Flare/FlareFacePainter.cs"), camera = ReviewSource("Social/Flare/FlareCityCamera.cs"),
            photos = ReviewSource("Social/Flare/FlareDirector.Photos.cs"), cells = ReviewSource("Social/Flare/FlareApp.Cells.cs"), turn = ReviewSource("Phone/Contacts/PortraitTurn.cs"),
            contacts = ReviewSource("Phone/Contacts/ContactPainter.cs");
        Check(!avatars.Contains("void Update(") && !camera.Contains("void Update(") && !photos.Contains("void Update(") && !avatars.Contains("MonoBehaviour") && !camera.Contains("MonoBehaviour"),
            "Flare pictures: the avatars, the city camera and the photos' pacing have no Update (nothing per frame)");
        string update = FlareMethod(painter, "private void Update()"), on = FlareMethod(painter, "private void OnUpdate()");
        int idle = on.IndexOf("Waiting.Count == 0", StringComparison.Ordinal), wait = on.IndexOf("!AppShowing", StringComparison.Ordinal), loop = on.IndexOf("for (", StringComparison.Ordinal);
        Check(update.Contains("HotSpots.Time(HotUpdate)") && update.Contains("FrameGuard.Off") && on.Contains("enabled = false") && idle >= 0 && wait > idle && loop > wait,
            "Flare pictures: the face painter is timed and guarded, switches itself off with nothing to draw, and waits for its moment before any search");
        string show = cells.Substring(Math.Max(0, cells.IndexOf("public void Show(FlareApp app", StringComparison.Ordinal)));
        show = show.Substring(0, Math.Max(0, show.IndexOf("private static int OfficialOf", StringComparison.Ordinal)));
        Check(show.Length > 0 && !show.Contains("File.") && !show.Contains("new Texture2D") && !show.Contains("LoadImage") && show.Contains("app._avatars.Of(key)"),
            "Flare pictures: showing an avatar reads no file and makes no texture (a look in what is kept)");
        int renders = 0;
        for (int at = camera.IndexOf(".Render()", StringComparison.Ordinal); at >= 0; at = camera.IndexOf(".Render()", at + 1, StringComparison.Ordinal))
        {
            renders++;
        }
        string render = FlareMethod(camera, "private static bool Render("), take = FlareMethod(camera, "public static IEnumerator Take(");
        Check(renders == 1 && render.Contains("_camera.Render()") && render.Contains("AsyncGPUReadback.Request(") && !camera.Contains("ReadPixels") && take.Contains("Render(from, at, target") && take.Contains("yield return null"),
            "Flare city photos: the city is rendered in one place, read back without waiting for the graphics card, the pose a frame before");
        string second = FlareMethod(photos, "private void PhotosSecond()");
        int paced = second.IndexOf("now < _photoNext", StringComparison.Ordinal), started = second.IndexOf("PhotoTake(", StringComparison.Ordinal);
        Check(paced >= 0 && started > paced && second.Contains("_photoToday >= FlareCityPhotoRules.DayMost") && second.Contains("_photoBusy") && FlareMethod(photos, "private IEnumerator PhotoTake").Contains("FlareCityPhotoRules.NextPace(")
              && FlareMethod(photos, "private IEnumerator PhotoTake").Contains("FlareCityCamera.Take("),
            "Flare city photos: a photo is taken only after its pace and under the day's most, one at a time, and the pace follows its measured cost");
        int takes = 0;
        for (int at = photos.IndexOf("Guard.Routine(PhotoTake(", StringComparison.Ordinal); at >= 0; at = photos.IndexOf("Guard.Routine(PhotoTake(", at + 1, StringComparison.Ordinal))
        {
            takes++;
        }
        Check(takes == 2 && FlareMethod(photos, "public void TestPictures").Contains("_photoBusy"), "Flare city photos: a photo is started only by the paced second and the test");
        string draw = FlareMethod(painter, "private void Draw(");
        Check(draw.Contains("PortraitTurn.Take(this, now)") && draw.IndexOf("PortraitTurn.Take(", StringComparison.Ordinal) < draw.IndexOf("PortraitTurn.Face(", StringComparison.Ordinal)
              && draw.Contains("SessionWatch.Piece(") && FlareMethod(contacts, "private void PaintFace(").Contains("PortraitTurn.Take(this, now)") && !contacts.Contains("PortraitGenerator.Create("),
            "Flare pictures: every face asks the game's portrait camera only with the shared turn, one at a time with the contacts' faces");
        Check(FlareMethod(turn, "public static void Give(").Contains("HiddenPaceSeconds") && turn.Contains("PaceSeconds = 2f") && FlareMethod(turn, "public static bool Free(").Contains("Timeout"),
            "Flare pictures: the portrait camera rests between faces, and a turn not given back lapses");
        string done = FlareMethod(avatars, "private void Done(");
        int destroyed = 0;
        for (int at = done.IndexOf("UnityEngine.Object.Destroy(before)", StringComparison.Ordinal); at >= 0; at = done.IndexOf("UnityEngine.Object.Destroy(before)", at + 1, StringComparison.Ordinal))
        {
            destroyed++;
        }
        Check(avatars.Contains("FlareAvatarRules.Most") && done.Contains("_used.Use(picture)") && destroyed == 2 && done.IndexOf("string gone = _used.Use(picture)", StringComparison.Ordinal) < done.LastIndexOf("UnityEngine.Object.Destroy(before)", StringComparison.Ordinal)
              && FlareMethod(avatars, "public void Clear()").Contains("UnityEngine.Object.Destroy(pair.Value)") && !FlareMethod(avatars, "public void Clear()").Contains("_logos[") ,
            "Flare pictures: the textures are bound, the one that goes is destroyed, all of ours go when the city unloads, and the game's logos are never destroyed");
    }

    private static void PicturesTexts()
    {
        string folder = LocalesFolder();
        var words = new Dictionary<string, string>[3];
        string[] codes = { "en", "pt", "zh-cn" };
        for (int i = 0; i < 3; i++)
        {
            words[i] = JsonSerializer.Deserialize<Dictionary<string, string>>(File.ReadAllText(System.IO.Path.Combine(folder, codes[i] + ".json")));
        }
        string[] keys =
        {
            "reallife_flare_opt_pictures", "reallife_opt_tip_flare_pictures", "reallife_flare_opt_city_photos", "reallife_flare_city_photos_0", "reallife_flare_city_photos_1",
            "reallife_flare_city_photos_2", "reallife_flare_city_photos_3", "reallife_opt_tip_flare_city_photos", "reallife_flare_test_pictures", "reallife_flare_test_photo_taken",
            "reallife_flare_test_photo_not", "reallife_flare_test_photo_busy", "reallife_flare_test_photo_none"
        };
        string lacking = string.Empty;
        for (int k = 0; k < keys.Length; k++)
        {
            for (int i = 0; i < 3; i++)
            {
                if (!words[i].TryGetValue(keys[k], out string text) || string.IsNullOrEmpty(text) || text.Contains("{ms}") != words[0][keys[k]].Contains("{ms}"))
                {
                    lacking += " " + codes[i] + ":" + keys[k];
                }
            }
        }
        Check(lacking.Length == 0, "Flare pictures: every text is in en, pt and zh-cn with the same names in curly brackets" + lacking);
        Check(words[0]["reallife_guide_flare"].Contains("# Pictures") && words[1]["reallife_guide_flare"].Contains("# Fotos") && words[2]["reallife_guide_flare"].Contains("# 头像与照片")
              && words[0]["reallife_guide_flare"].Contains("never indoors"), "Flare pictures: the Guide's Flare page says what the pictures are, in three languages");
        string mod = ReviewSource("Core/RealLifeOverhaulMod.cs"), features = ReviewSource("Meta/FeatureList.cs"), tree = ReviewSource("Meta/OptionsTree.cs"), settings = ReviewSource("Core/Settings.cs");
        Check(mod.Contains("Settings.FlarePicturesId, \"reallife_flare_opt_pictures\"") && mod.Contains("Settings.FlareCityPhotosId, \"reallife_flare_opt_city_photos\"")
              && features.Contains("Settings.FlarePicturesId, Settings.FlareCityPhotosId") && tree.Contains("\"flare_pictures\", \"flare_city_photos\"")
              && settings.Contains("public static bool FlarePictures = true;") && settings.Contains("public static int FlareCityPhotos = 1;"),
            "Flare pictures: both options are on the options page, Flare's, on by default with a modest share of photos");
        Check(ReviewSource("Social/Flare/FlareAvatars.cs").Contains("!Settings.FlarePictures") && ReviewSource("Social/Flare/FlareDirector.Photos.cs").Contains("Settings.FlareCityPhotos <= 0"),
            "Flare pictures: with the options off, the initials as before and no photo taken or shown");
    }

    private static void PicturesCost()
    {
        var into = new byte[FlareAvatarRules.Size * FlareAvatarRules.Size * 4];
        var src = new byte[256 * 256 * 3];
        DiscMany(src, into, 5);
        double ms = DiscTime(src, into, 50) / 50.0;
        Check(ms < 5.0, "Flare pictures: a portrait made round in " + ms.ToString("0.00") + " ms (on a worker thread, once per picture)");
        FlareAvatarFacts facts = PictureFacts();
        for (int i = 0; i < 200; i++)
        {
            facts.File.Posts.Add(new FlarePost { Id = 100 + i, Author = FlareAuthor.Citizen, Source = FlareSource.CityTalk });
        }
        PlansMany(facts, 10);
        double plans = PlansTime(facts, 2000);
        Check(plans < 100.0, "Flare pictures: 2,000 plans of every kind of account over 200 posts take " + plans.ToString("0.0") + " ms (a card's is asked when it is bound)");
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void DiscMany(byte[] src, byte[] into, int n)
    {
        for (int i = 0; i < n; i++)
        {
            FlareAvatarRules.Disc(src, 256, 256, 3, false, into);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double DiscTime(byte[] src, byte[] into, int n)
    {
        var watch = Stopwatch.StartNew();
        DiscMany(src, into, n);
        return watch.Elapsed.TotalMilliseconds;
    }

    private static readonly string[] PlanKeys = { "me", "c3", "f6", "i:3", "s:Robin", "b", "k:Nook", "o:1", "t:Dusk", "p:abc" };

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static void PlansMany(FlareAvatarFacts facts, int n)
    {
        for (int i = 0; i < n; i++)
        {
            FlareAvatarRules.Plan(PlanKeys[i % PlanKeys.Length], facts);
        }
    }

    [MethodImpl(MethodImplOptions.AggressiveOptimization | MethodImplOptions.NoInlining)]
    private static double PlansTime(FlareAvatarFacts facts, int n)
    {
        var watch = Stopwatch.StartNew();
        PlansMany(facts, n);
        return watch.Elapsed.TotalMilliseconds;
    }
}
