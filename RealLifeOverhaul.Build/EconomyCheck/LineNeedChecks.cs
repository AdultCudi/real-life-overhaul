using System;
using System.Collections.Generic;
using System.IO;
using IoPath = System.IO.Path;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// A player got a bill for repairing their fridge, and they owned no fridge: the life event hm12 needed only a home. Every
// random line whose words take something of the player's for granted (a fridge, a sink, a bed, a computer, a car, a flat
// rather than a house, a landlord) now needs it through one rule, Core/LineNeeds, read from the game by LineNeedsGame and
// asked wherever such lines are drawn. These checks read every random line's English for the things it names, so a new
// line that names one and is not listed with it fails here.
internal static partial class Program
{
    // The things a line can name, by the words that name them as the player's. A line in a random family whose English
    // holds one needs that thing in the table (InCar does for a car), unless it is listed below as about somebody else's.
    private static readonly KeyValuePair<LineNeed, Regex>[] ThingWords =
    {
        Words(LineNeed.Fridge, @"\bfridges?\b|\bfreezer"),
        Words(LineNeed.Sink, @"\bkitchen tap\b|\bthe sink\b|\byour sink\b"),
        Words(LineNeed.Bath, @"\ba bath\b|\bthe bath\b|\bbathtub"),
        Words(LineNeed.Bed, @"\bthe bed\b|\byour bed\b"),
        Words(LineNeed.Computer, @"\blaptop\b|\bcomputer\b"),
        Words(LineNeed.Rented, @"\blandlord\b|\btenants?\b|\bthe super\b"),
        Words(LineNeed.Flat, @"\bthe flat\b|\byour flat\b|\bapartment\b|\bupstairs\b|\bacross the hall\b|\bfloors up\b|\bwhole floor\b|\bbuilding's\b|\bthe lobby\b"),
        Words(LineNeed.Car, @"\byour car\b|\byour parked\b|\bwiper|\bwindscreen\b|\ba lift\b|\bdrive me\b|\bregistration sticker"),
        Words(LineNeed.InCar, @"\bthe seat\b|\bwarning light\b|\bthe radio\b|\bthe spare\b"),
        Words(LineNeed.Away, @"\bthe till\b|\bcashier\b|\bvending machine\b|\ba cat is\b")
    };

    // The families of lines drawn at random or on a timer: life events, family life, people's texts, the street's crimes,
    // the roommate and household staff.
    private static readonly string[] RandomLines =
    {
        "reallife_ev_", "reallife_family_", "reallife_social_text_", "reallife_crime_", "reallife_text_roommate_", "reallife_roommate_talk_",
        "reallife_household_note_"
    };

    // Lines whose thing is somebody else's, a turn of phrase, or already so when the line is said.
    private static readonly string[] ThingWordsElsewhere =
    {
        "reallife_ev_tr01",                        // a pothole the size of a bathtub
        "reallife_ev_fr13",                        // the old friend's landlord
        "reallife_social_text_news_3",             // a friend's landlord
        "reallife_family_grown_call_1",            // the grown child's own flat
        "reallife_family_holiday_missed_1",        // a plate in Mom's fridge, at her dinner
        "reallife_family_scan_done_1",             // a scan's picture for the fridge, a turn of phrase
        "reallife_family_cook_4",                  // the radio on while cooking together
        "reallife_family_chore_groceries",         // asked only when the home's fridges are read nearly empty (no fridge reads -1)
        "reallife_family_chore_carwash",           // asked only when a car of the player's is read dirty
        "reallife_family_new_home_done",           // the answer to the player's own ask to move in, not drawn
        "reallife_family_sms_new_home_yes",        // ...the same, by text
        "reallife_family_reconciled_home",         // ...moving back in after making up
        "reallife_crime_parking_ticketed_1",       // a stranger's car
        "reallife_crime_carbreak_2",               // drawn of a car of the player's own (OffscreenCrimes)
        "reallife_crime_pullover_drunk",           // said to the player as they are pulled over driving
        "reallife_crime_stop_escaped",             // ...after a chase in it
        "reallife_crime_graffiti_told_2",          // the super of a building in the street
        "reallife_crime_graffiti_cleared_1",       // the landlord of a building in the street
        "reallife_crime_question_bolted_2",        // the officer's radio
        "reallife_crime_holdup_x_1",               // a hold-up staged in a shop
        "reallife_crime_shoplifter_escaped_2",     // ...a shoplifter, in a shop
        "reallife_crime_shoplifter_false_1",
        "reallife_crime_shoplifter_start_3",
        "reallife_household_note_no_fridge"        // says the home has none
    };

    // Where each listed line that is not a life event is asked about before it is drawn, by the words that ask.
    private static readonly string[][] LineNeedAskers =
    {
        new[] { @"Family\FamilyText.cs", "int index = LineNeeds.Pick(keys, last, Random01);" },
        new[] { @"Family\Relatives.cs", "(visit = FamilyText.Pick(Visits, ref _lastVisit)) != null" },
        new[] { @"Family\Relatives.cs", "int ask = LineNeeds.Pick(InLawAsks, -1, FamilyText.Random01);" },
        new[] { @"Family\PartnerGlad.cs", "if (LineNeeds.Fits(lines[s.PGladNext]))" },
        new[] { @"Family\HardTimesWatch.cs", "g.Bedless = !LineNeeds.Fits(HardTimes.TowelKey);" },
        new[] { @"Family\HardTimesRules.cs", "if (!g.LivesIn || g.Bedless)" },
        new[] { @"Social\SocialCatalogue.cs", "int index = LineNeeds.Pick(pool, Last.TryGetValue(pool, out int last) ? last : -1, Random01);" },
        new[] { @"Social\SocialDirector.Contact.cs", "if (kind == 0 && !LineNeeds.AnyFits(SocialCatalogue.TextLiftAsk))" },
        new[] { @"Crime\CrimeDeck.cs", "int index = LineNeeds.Pick(texts, last != null ? Array.IndexOf(texts, last) : -1, random01);" },
        new[] { @"Home\Roommates\RoommateDirector.Life.cs", "RoommateGame.FitsAt(\"reallife_text_roommate_cooked\", s.HomeStreet, s.HomeNumber) && RoommateGame.LeaveMeal(" },
        new[] { @"Home\Household\HouseholdDirector.cs", "else if (!HouseholdGame.CanKeep(home))" },
        new[] { @"Home\Household\HouseholdGame.cs", "LineNeedsGame.HomeHas(home, LineNeeds.Of(HouseholdText.CookedKey))" },
        new[] { @"Life\LifeEventCatalogue.cs", "|| !LineNeeds.Fits(Key))" }
    };

    // Each listed line that is not a life event, with the file that draws it (the asker above that guards it is in or behind it).
    private static readonly string[][] LineNeedHomes =
    {
        new[] { "reallife_family_drawing_", @"Family\Children.Growing.cs", "FamilyText.Pick(Drawings, ref _lastDrawing)" },
        new[] { "reallife_family_mom_visit_", @"Family\Relatives.cs", "FamilyText.Pick(Visits, ref _lastVisit)" },
        new[] { "reallife_family_encourage_", @"Family\HomeLife.cs", "FamilyText.Pick(Encourage, ref _lastEncourage)" },
        new[] { "reallife_family_greet_", @"Family\HomeLife.AtHome.cs", "FamilyText.Pick(pool, ref _lastGreet)" },
        new[] { "reallife_family_glad_", @"Family\PartnerGlad.cs", "PartnerVoice.Warm(lines[s.PGladNext], Args.Us);" },
        new[] { "reallife_family_argue_", @"Family\HardTimes.cs", "new Row(TowelKey," },
        new[] { "reallife_family_inlaws_", @"Family\Relatives.cs", "InLawsSay(InLawAsks[ask]);" },
        new[] { "reallife_family_grown_call_", @"Family\Children.cs", "FamilyText.Pick(GrownCalls, ref _lastGrown)" },
        new[] { "reallife_social_invite_business", @"Social\SocialDirector.Contact.cs", "SocialAskRules.InviteKind((int)person.Wants, MayAskTour(person))" },
        new[] { "reallife_social_follow_", @"Social\PersonDialog.CallBack.cs", "if (!LineNeeds.Fits(topic.Open[0]))" },
        new[] { "reallife_street_open_famous_", @"Street\StreetLines.cs", "if (!LineNeeds.Fits(pool[at]))" },
        new[] { "reallife_season_turn", @"Seasons\SeasonDirector.cs", "if (LineNeeds.Fits(TurnKey))" },
        new[] { "reallife_social_text_home_", @"Social\SocialDirector.Contact.cs", "SocialLines.Pick(pool)" },
        new[] { "reallife_social_text_lift_", @"Social\SocialDirector.Contact.cs", "SocialLines.Pick(text)" },
        new[] { "reallife_crime_parking_near", @"Crime\CrimeScene.cs", "Say(NotificationType.Info, Entry.Near, 7f);" },
        new[] { "reallife_text_roommate_cooked", @"Home\Roommates\RoommateDirector.Life.cs", "RoommateText.Message(\"reallife_text_roommate_cooked\"" },
        new[] { "reallife_roommate_talk_cooked", @"Home\Roommates\RoommateDirector.Life.cs", "s.Topics |= Topic.Cooked;" },
        new[] { "reallife_household_note_", @"Home\Household\HouseholdDirector.cs", "Note(day, HouseholdText.T(" }
    };

    private static KeyValuePair<LineNeed, Regex> Words(LineNeed need, string pattern) =>
        new KeyValuePair<LineNeed, Regex>(need, new Regex(pattern, RegexOptions.IgnoreCase | RegexOptions.CultureInvariant));

    private static void LineNeedRule()
    {
        Dictionary<string, string> en = LoadWords(IoPath.Combine(LocalesFolder(), "en.json"));
        Check(en != null, "line needs: the English is read");
        if (en == null)
        {
            return;
        }
        LineNeedTable(en);
        LineNeedSweep(en);
        LineNeedPicks();
        LineNeedEvents();
        LineNeedReads();
        LineNeedPaths();
        LineNeedTexts(en);
    }

    // The table: every key a real line, listed once, each with a need.
    private static void LineNeedTable(Dictionary<string, string> en)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);
        var wrong = new List<string>();
        foreach (KeyValuePair<string, LineNeed> row in LineNeeds.Table)
        {
            bool real = row.Key.StartsWith("reallife_ev_", StringComparison.Ordinal) ? LifeEventCatalogue.Contains(row.Key.Substring(12)) : en.ContainsKey(row.Key);
            if (!seen.Add(row.Key) || row.Value == LineNeed.None || !real)
            {
                wrong.Add(row.Key);
            }
        }
        Check(LineNeeds.Table.Length >= 50 && wrong.Count == 0,
            "line needs: every line of the table is a real line or life event, listed once, with a need (" + LineNeeds.Table.Length + " lines"
            + (wrong.Count > 0 ? "; not: " + string.Join(", ", wrong) : string.Empty) + ")");
        Check(LineNeeds.Of("reallife_ev_hm12") == LineNeed.Fridge && LineNeeds.Of("reallife_ev_fd10") == LineNeed.Fridge,
            "line needs: the fridge's repair bill (hm12) and the fridge with one lemon (fd10) need a fridge in the home");
    }

    // Every random line that names a thing of the player's needs it, whatever family it is in.
    private static void LineNeedSweep(Dictionary<string, string> en)
    {
        var missing = new List<string>();
        var speaking = new HashSet<string>(StringComparer.Ordinal);
        int named = 0;
        foreach (KeyValuePair<string, string> row in en)
        {
            if (!InRandomFamily(row.Key))
            {
                continue;
            }
            string owner = LineOwner(row.Key);
            foreach (KeyValuePair<LineNeed, Regex> thing in ThingWords)
            {
                if (!thing.Value.IsMatch(row.Value))
                {
                    continue;
                }
                speaking.Add(owner);
                if (Array.IndexOf(ThingWordsElsewhere, owner) >= 0)
                {
                    continue;
                }
                named++;
                LineNeed needs = LineNeeds.Of(owner);
                bool covered = (needs & thing.Key) == thing.Key || (thing.Key == LineNeed.Car && (needs & LineNeed.InCar) != 0);
                if (!covered && !missing.Contains(owner + " (" + thing.Key + ")"))
                {
                    missing.Add(owner + " (" + thing.Key + ")");
                }
            }
        }
        Check(named >= 50 && missing.Count == 0,
            "line needs: every random line whose English names a thing of the player's needs it in Core/LineNeeds (" + named + " namings"
            + (missing.Count > 0 ? "; not: " + string.Join(", ", missing) : string.Empty) + ")");
        var stale = new List<string>();
        foreach (string key in ThingWordsElsewhere)
        {
            if (!speaking.Contains(key))
            {
                stale.Add(key);
            }
        }
        Check(stale.Count == 0, "line needs: each line listed as speaking of somebody else's thing is there and still names one"
            + (stale.Count > 0 ? " (not: " + string.Join(", ", stale) + ")" : string.Empty));
    }

    private static bool InRandomFamily(string key)
    {
        foreach (string family in RandomLines)
        {
            if (key.StartsWith(family, StringComparison.Ordinal))
            {
                return true;
            }
        }
        return false;
    }

    // A life event's texts all answer to the event's own key (its header, body, answers and outcomes).
    private static string LineOwner(string key)
    {
        Match ev = Regex.Match(key, "^reallife_ev_[a-z]{2}[0-9]{2}");
        return ev.Success ? ev.Value : key;
    }

    // The rule itself: nothing read means no listed line; a pick is only ever of a line that fits, never the last while
    // another fits, and none when none does; the game's furniture by name.
    private static void LineNeedPicks()
    {
        Func<LineNeed, bool> before = LineNeeds.Read;
        try
        {
            LineNeeds.Read = null;
            Check(!LineNeeds.Fits("reallife_ev_hm12") && LineNeeds.Fits("reallife_ev_hm09") && !LineNeeds.Fits(null) && LineNeeds.Holds(LineNeed.None),
                "line needs: with nothing read, a listed line never fits; a line that needs nothing always does; no line never");
            LineNeeds.Read = needs => throw new InvalidOperationException("a read that fails");
            Check(!LineNeeds.Fits("reallife_ev_hm12") && LineNeeds.Fits("reallife_ev_hm09"), "line needs: a read that throws reads as not so");

            string[] drawings =
            {
                "reallife_family_drawing_1", "reallife_family_drawing_2", "reallife_family_drawing_3", "reallife_family_drawing_4",
                "reallife_family_drawing_5", "reallife_family_drawing_6", "reallife_family_drawing_7", "reallife_family_drawing_8"
            };
            var rng = new Random(1101);
            Func<float> r01 = () => (float)rng.NextDouble();
            bool onlyFitting = true, neverLast = true;
            var reached = new HashSet<int>();
            LineNeed has = LineNeed.None;
            LineNeeds.Read = needs => (has & needs) == needs;
            int last = -1;
            for (int i = 0; i < 4000; i++)
            {
                has = (LineNeed)rng.Next(1 << 12);
                int pick = LineNeeds.Pick(drawings, last, r01);
                onlyFitting &= pick >= 0 && LineNeeds.Fits(drawings[pick]);
                int fitting = 0;
                bool lastFits = false;
                for (int k = 0; k < drawings.Length; k++)
                {
                    fitting += LineNeeds.Fits(drawings[k]) ? 1 : 0;
                    lastFits |= k == last && LineNeeds.Fits(drawings[k]);
                }
                neverLast &= !(pick == last && lastFits && fitting > 1);
                reached.Add(pick);
                last = pick;
            }
            has = LineNeed.None;
            string[] lifts = { "reallife_social_text_lift_1", "reallife_social_text_lift_2" };
            string[] visits = { null, "reallife_family_mom_visit_2" };
            Check(onlyFitting && neverLast && reached.Count == drawings.Length && LineNeeds.Pick(lifts, -1, r01) == -1 && !LineNeeds.AnyFits(lifts)
                  && LineNeeds.Pick(visits, -1, r01) == -1 && LineNeeds.Pick(null, -1, r01) == -1 && LineNeeds.Pick(drawings, 2, () => 1f) >= 0,
                "line needs: a pick is only of a line that fits, never the last one while another fits, reaches every line in time, and is none when none fits (a null place never)");
            has = LineNeed.Car;
            Check(LineNeeds.Pick(lifts, 0, r01) == 1 && LineNeeds.Pick(new[] { lifts[0] }, 0, r01) == 0,
                "line needs: with a car the lift asks come, the other than last first, and a single line even after itself");
        }
        finally
        {
            LineNeeds.Read = before;
        }

        string[] game =
        {
            "ba:itemname_standardfridge", "ba:itemname_luxuryfridge", "ba:itemname_drinksfridge", "ba:itemname_industrialfreezer", "ba:itemname_bathtub",
            "ba:itemname_sink", "ba:itemname_countersinkmodern", "ba:itemname_sinksoap", "ba:itemname_sinksoapwallmounted", "ba:itemname_laptop",
            "ba:itemname_desktopcomputer", "ba:itemname_bed1", "ba:itemname_kingsizebed", "ba:itemname_bedsidetable", "ba:itemname_flatbed",
            "ba:itemname_beddoubleiron", "ba:itemname_sofa", "", null
        };
        LineNeed[] wanted =
        {
            LineNeed.Fridge, LineNeed.Fridge, LineNeed.Fridge, LineNeed.Fridge, LineNeed.Bath, LineNeed.Sink, LineNeed.Sink, LineNeed.None, LineNeed.None,
            LineNeed.Computer, LineNeed.Computer, LineNeed.Bed, LineNeed.Bed, LineNeed.None, LineNeed.None, LineNeed.Bed, LineNeed.None, LineNeed.None, LineNeed.None
        };
        var wrong = new List<string>();
        for (int i = 0; i < game.Length; i++)
        {
            if (LineNeeds.ItemIs(game[i]) != wanted[i])
            {
                wrong.Add((game[i] ?? "null") + " as " + LineNeeds.ItemIs(game[i]));
            }
        }
        Check(wrong.Count == 0, "line needs: the game's furniture is read by name: fridges and freezers, sinks but not soap, beds but not a bedside table or a flatbed"
            + (wrong.Count > 0 ? " (not: " + string.Join(", ", wrong) + ")" : string.Empty));
        Check((LineNeeds.AtHome & (LineNeed.Car | LineNeed.InCar | LineNeed.Business | LineNeed.Away)) == LineNeed.None
              && (LineNeeds.AtHome & (LineNeed.Home | LineNeed.Flat | LineNeed.Rented | LineNeed.Fridge | LineNeed.Sink | LineNeed.Bath | LineNeed.Bed | LineNeed.Computer))
              == LineNeeds.AtHome,
            "line needs: a home's needs are asked of one home together, and nothing else is");
    }

    // The fridge's bill itself, through the life events' own fitting: not without a fridge, as it was, and with one.
    private static void LineNeedEvents()
    {
        Func<LineNeed, bool> before = LineNeeds.Read;
        try
        {
            var home = new LifeContext
            {
                Flags = Need.HasHome | Need.Indoors | Need.NotDriving | Need.Weekday | Need.Dry | Need.Healthy | Need.Rested | Need.Fed | Need.EnergyReserve
                        | Need.CanPrompt | Need.Friend | Need.Mom | Need.Veteran,
                Hour = 13, Money = 5000f
            };
            LifeEvent fridge = null, car = null, flat = null;
            foreach (LifeEvent e in LifeEventCatalogue.All)
            {
                fridge = e.Id == "hm12" ? e : fridge;
                car = e.Id == "tr10" ? e : car;
                flat = e.Id == "hm04" ? e : flat;
            }
            LineNeed has = LineNeed.Home | LineNeed.Car;
            LineNeeds.Read = needs => (has & needs) == needs;
            bool without = fridge != null && !fridge.Fits(in home);
            has |= LineNeed.Fridge;
            bool with = fridge != null && fridge.Fits(in home);
            LifeContext walking = home;
            walking.Flags = (home.Flags & ~Need.Indoors) | Need.OnFoot | Need.HasVehicle;
            has = LineNeed.Home;
            bool bikeOnly = car != null && !car.Fits(in walking);
            has = LineNeed.Home | LineNeed.Car;
            bool hasCar = car != null && car.Fits(in walking);
            bool house = flat != null && !flat.Fits(in home);
            has |= LineNeed.Flat;
            bool inFlat = flat != null && flat.Fits(in home);
            Check(without && with && bikeOnly && hasCar && house && inFlat,
                "line needs: the fridge's repair bill never comes to a home with no fridge and does to one with; a parking ticket on a car only to somebody with a car (not a bicycle); the neighbour across the hall only in a flat");
        }
        finally
        {
            LineNeeds.Read = before;
        }
    }

    // The reads: from the game's own data, each need, wired and taken off with the rest of everyday life.
    private static void LineNeedReads()
    {
        string game = ModSource(@"Core\LineNeedsGame.cs");
        string holds = MemberText(game, "public static bool Holds(LineNeed needs)");
        string homeHas = MemberText(game, "public static bool HomeHas(BuildingRegistration home, LineNeed needs)");
        var unread = new List<string>();
        foreach (LineNeed need in (LineNeed[])Enum.GetValues(typeof(LineNeed)))
        {
            if (need != LineNeed.None && need != LineNeed.Home && !game.Contains("LineNeed." + need))
            {
                unread.Add(need.ToString());
            }
        }
        Check(unread.Count == 0 && holds.Contains("(needs & LineNeed.Car) != 0 && !HasCar(save)") && holds.Contains("(needs & LineNeed.InCar) != 0 && !InCar()")
              && holds.Contains("(needs & LineNeed.Business) != 0 && !OwnsBusiness(save)") && holds.Contains("(needs & LineNeed.Away) != 0 && here != null")
              && holds.Contains("LineNeed home = needs & LineNeeds.AtHome;") && holds.Contains("return HomeHas(here, home);")
              && holds.Contains("if (IsHome(all[i]) && HomeHas(all[i], home))") && holds.Contains("catch (Exception e)") && holds.Contains("return false;"),
            "line needs: every need is read (a car, being in one, a business, away from home, and a home's together: the one stood in, else any)"
            + (unread.Count > 0 ? " (not: " + string.Join(", ", unread) + ")" : string.Empty));
        Check(homeHas.Contains("building.IsHamptonsHouse()") && homeHas.Contains("home.BuildingOwnedByPlayer") && homeHas.Contains("home.itemInstances")
              && homeHas.Contains("LineNeeds.ItemIs(pair.Value.itemName)") && MemberText(game, "private static bool IsHome(BuildingRegistration reg)").Contains("GoingPublic.Mine(reg)")
              && MemberText(game, "private static bool IsCar(VehicleInstance vehicle)").Contains("vehicle.VehicleType.enclosed && vehicle.VehicleType.IsMotorVehicle")
              && MemberText(game, "private static bool HasCar(GameInstance save)").Contains("save.VehicleInstances"),
            "line needs: a flat is not a Hamptons house, rented is not owned, a home's things are its own furniture, homes are the player's own, a car is a closed motor vehicle of the save's");
        string links = ModSource(@"Wiring\LifeLinks.cs");
        Check(MemberText(links, "internal static void Connect()").Contains("LineNeeds.Read = LineNeedsGame.Holds;")
              && MemberText(links, "internal static void Disconnect()").Contains("LineNeeds.Read = null;")
              && ModSource(@"Life\LifeEventRunner.cs").Contains("flags |= If(LineNeedsGame.OwnsBusiness(save), Need.HasBusiness);"),
            "line needs: the reads are wired with everyday life and taken off with it; the life events count businesses by the same test");
    }

    // Every place that draws a listed line asks the rule first.
    private static void LineNeedPaths()
    {
        var missing = new List<string>();
        foreach (string[] asker in LineNeedAskers)
        {
            if (!ModSource(asker[0]).Contains(asker[1]))
            {
                missing.Add(asker[0]);
            }
        }
        foreach (KeyValuePair<string, LineNeed> row in LineNeeds.Table)
        {
            if (row.Key.StartsWith("reallife_ev_", StringComparison.Ordinal))
            {
                continue;   // LifeEvent.Fits asks for every event
            }
            string[] home = null;
            foreach (string[] h in LineNeedHomes)
            {
                home = row.Key.StartsWith(h[0], StringComparison.Ordinal) ? h : home;
            }
            if (home == null || !ModSource(home[1]).Contains(home[2]))
            {
                missing.Add(row.Key);
            }
        }
        string family = ModSource(@"Family\HomeLife.cs");
        Check(missing.Count == 0 && !family.Contains("EncourageNoBath") && !family.Contains("private static bool HasBath()"),
            "line needs: every place that draws a listed line asks Core/LineNeeds first (the life events' fitting, the family's, people's and the street's picks, Mom's visits, the in-laws' asks, the partner's glad words, the wet towel, the friend's lift, the roommate's portion, the household cook); the bath's own list is gone"
            + (missing.Count > 0 ? " (not: " + string.Join(", ", missing) + ")" : string.Empty));
        string text = ModSource(@"Family\FamilyText.cs");
        Check(MemberText(text, "public static void Toast(NotificationType type, string key, in Args a, float seconds = 9f)").Contains("if (key == null)")
              && MemberText(text, "public static void Message(Contact contact, string key, in Args a, bool notify = true)").Contains("if (key == null)"),
            "line needs: a family pool with no line that fits says nothing, rather than an empty notice");
        string life = ModSource(@"Life\LifeEvents.cs");
        string test = MemberText(life, "public void TriggerFridgeBill()");
        Check(ModSource(@"Core\RealLifeOverhaulMod.cs").Contains("AddButton(\"reallife_test_event_fridge\", () => LifeEvents.Instance?.TriggerFridgeBill())")
              && ModSource(@"Meta\FeatureList.cs").Contains(".Test(\"reallife_test_event_fridge\")") && test.Contains("if (bill == null || !LineNeeds.Fits(bill.Key))")
              && test.Contains("Notify(NotificationType.Warning, \"reallife_test_event_fridge_no\");") && test.Contains("LifeEventRunner.Run(bill);")
              && life.Contains("\"Line needs check (life event \""),
            "line needs: Options > Tests fires the fridge's repair bill only with a fridge at home and says why not otherwise; a drawn event's needs are logged with Developer diagnostics");
    }

    // The household cook's new note in all three, and What's new's line with its fix mark in every language.
    private static void LineNeedTexts(Dictionary<string, string> en)
    {
        bool note = true;
        foreach (string code in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(LocalesFolder(), code + ".json"));
            note &= words != null && words.TryGetValue("reallife_household_note_no_fridge", out string v) && v.Contains("{name}");
        }
        Check(note && en["reallife_household_note_no_fridge"].Contains("no fridge"),
            "line needs: the cook's note for a home with no fridge is in English, Portuguese and Chinese, with the cook's name");
        Check(FixLineEverywhere("now come only when what they speak of is really yours", "agora só aparecem quando aquilo de que falam é realmente seu",
                                "现在只会在它们提到的东西真的属于你时出现", "kommen jetzt nur noch, wenn das, wovon sie sprechen, wirklich dir gehört",
                                "n'arrivent plus que lorsque ce dont ils parlent vous appartient vraiment", "ahora solo llegan cuando aquello de lo que hablan es realmente tuyo",
                                "теперь приходят, только если то, о чём в них говорится, действительно ваше", "komen nu alleen als datgene waar ze over gaan echt van jou is",
                                "artık yalnızca sözünü ettikleri şey gerçekten sizinse gelir"),
            "line needs: What's new has the line, with its fix mark, in some row of every language");
        bool later = true;
        foreach (KeyValuePair<string, string> lang in new Dictionary<string, string> { { "pl", "Naprawiono: losowe zdarzenia" }, { "ko", "수정: 무작위 이벤트" } })
        {
            Dictionary<string, string> words = LoadWords(IoPath.Combine(LocalesFolder(), lang.Key + ".json"));
            later &= words != null && WhatsNewHas(words, lang.Value);
        }
        Check(later, "line needs: What's new has the line in Polish and Korean too, in some row");
    }
}
