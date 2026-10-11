using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace RealLifeOverhaul
{
    internal static partial class Program
    {
        private static int _failures;

        private static void Fail(string message)
        {
            _failures++;
            Console.WriteLine("  FAIL: " + message);
        }

        private static void Section(string title) => Console.WriteLine("\n== " + title + " ==");

        private static int Main(string[] args)
        {
            string src = args.Length > 0 ? args[0] : Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, @"..\..\..\..\..\Assets\Mods\RealLifeOverhaul"));
            string localePath = Path.Combine(src, "Locales", "en.json");
            Console.WriteLine("Mod source: " + src);

            LifeEvent[] all = LifeEventCatalogue.All;

            // ---------------------------------------------------------------- locale file
            Section("en.json");
            var locale = new Dictionary<string, string>(StringComparer.Ordinal);
            using (JsonDocument doc = JsonDocument.Parse(File.ReadAllText(localePath)))
            {
                foreach (JsonProperty p in doc.RootElement.EnumerateObject())
                {
                    if (p.Value.ValueKind != JsonValueKind.String) Fail($"{p.Name}: value is not a string");
                    else if (!locale.TryAdd(p.Name, p.Value.GetString())) Fail($"{p.Name}: key appears twice");
                    if (!p.Name.StartsWith("reallife", StringComparison.Ordinal)) Fail($"{p.Name}: key lacks the reallife prefix");
                }
            }
            Console.WriteLine($"  parsed OK, {locale.Count} keys");

            // ---------------------------------------------------------------- ids and counts
            Section("Catalogue");
            var ids = new HashSet<string>();
            foreach (LifeEvent e in all)
            {
                if (!ids.Add(e.Id)) Fail($"duplicate event id {e.Id}");
                if (!Regex.IsMatch(e.Id, "^[a-z]{2}[0-9]{2}$")) Fail($"{e.Id}: id is not two letters + two digits");
                if (e.Id.Contains(',')) Fail($"{e.Id}: id contains the list separator");
            }
            int choices = all.Count(e => e.IsChoice);
            int randomChoices = all.Count(e => e.IsChoice && (e.Yes.Length > 1 || e.No.Length > 1));
            Console.WriteLine($"  events: {all.Length}   unique ids: {ids.Count}");
            Console.WriteLine($"  choices: {choices} ({100.0 * choices / all.Length:0.0}%), of which {randomChoices} have a random outcome after choosing");
            Console.WriteLine($"  distinct outcomes: {all.Sum(e => e.AllOutcomes.Count())}");
            var names = new Dictionary<string, string>
            {
                ["st"] = "street & strangers", ["wx"] = "weather", ["tr"] = "transport & driving", ["hm"] = "home & neighbours",
                ["bz"] = "business & work", ["fr"] = "friend (Alex)", ["fm"] = "family (Mom)", ["hl"] = "health", ["mn"] = "money & bureaucracy",
                ["fd"] = "food", ["lk"] = "luck & oddities", ["tc"] = "tech mishaps", ["pt"] = "pets & animals", ["wk"] = "weekday/weekend moments"
            };
            foreach (var g in all.GroupBy(e => e.Category))
            {
                if (!names.ContainsKey(g.Key)) Fail($"unknown category prefix {g.Key}");
                Console.WriteLine($"    {g.Key}  {names.GetValueOrDefault(g.Key, "?"),-26} {g.Count(),3}  ({g.Count(e => e.IsChoice)} choices)");
            }
            if (all.Length < 200) Fail("fewer than 200 events");

            // ---------------------------------------------------------------- keys
            Section("Locale keys referenced by the catalogue");
            var referenced = new HashSet<string>();
            int missing = 0;
            foreach (LifeEvent e in all)
            {
                foreach (string key in e.TextKeys().Concat(e.TransactionKeys()))
                {
                    referenced.Add(key);
                    if (!locale.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
                    {
                        missing++;
                        Fail($"{e.Id}: key {key} is missing or empty in en.json");
                    }
                }
            }
            Console.WriteLine($"  {referenced.Count} distinct keys referenced, {missing} missing");
            List<string> orphans = locale.Keys.Where(k => k.StartsWith(LifeEvent.KeyPrefix, StringComparison.Ordinal) && !referenced.Contains(k)).ToList();
            foreach (string k in orphans) Fail($"en.json key {k} is not used by any event");
            Console.WriteLine($"  unused {LifeEvent.KeyPrefix}* keys in en.json: {orphans.Count}");

            // ---------------------------------------------------------------- text sanity
            Section("Text and effect consistency");
            var placeholder = new Regex(@"\{([^}]*)\}");
            int longest = 0; string longestKey = null;
            foreach (LifeEvent e in all)
            {
                if (e.IsChoice)
                {
                    foreach (string key in new[] { e.HeaderKey, e.BodyKey, e.ConfirmKey, e.CancelKey })
                    {
                        if (locale.TryGetValue(key, out string t) && placeholder.IsMatch(t)) Fail($"{key}: prompts cannot fill placeholders");
                    }
                    if (e.Cost > 0)
                    {
                        string prompt = locale.GetValueOrDefault(e.BodyKey, "") + " | " + locale.GetValueOrDefault(e.ConfirmKey, "");
                        if (!Regex.IsMatch(prompt, @"\$" + e.Cost + @"(?![0-9])")) Fail($"{e.Id}: costs ${e.Cost} but the prompt never says so");
                    }
                    if (e.Yes.Length == 0) Fail($"{e.Id}: choice without a yes outcome");
                    if ((e.Needs & Need.NotDriving) == 0 || (e.Needs & Need.CanPrompt) == 0) Fail($"{e.Id}: choice can fire while driving or with a prompt open");
                }
                else if (e.Outcomes.Length == 0) Fail($"{e.Id}: no outcome");

                foreach (Outcome o in e.AllOutcomes)
                {
                    string key = e.TextKey(o);
                    if (!locale.TryGetValue(key, out string text)) continue;
                    if (text.Length > longest) { longest = text.Length; longestKey = key; }
                    bool yes = Array.IndexOf(e.Yes, o) >= 0;
                    foreach (Match m in placeholder.Matches(text))
                    {
                        if (m.Groups[1].Value != "amount") Fail($"{key}: unknown placeholder {m.Value}");
                        else if (!o.HasMoney && !(yes && e.Cost > 0)) Fail($"{key}: uses {{amount}} but the outcome moves no money");
                    }
                    if (o.HasMoney && !text.Contains("{amount}")) Fail($"{key}: moves money without saying how much");
                    if (o.HasMoney && string.IsNullOrEmpty(o.Txn)) Fail($"{key}: money without a transaction key");
                    if (o.MoneyPct == 0f && o.MoneyMin < 0 != o.MoneyMax < 0 && o.HasMoney) Fail($"{key}: money range crosses zero");
                    if ((o.HurtHours > 0 || o.SickMax > 0) && o.Via != Via.Toast) Fail($"{key}: injuries and colds must be toasts");
                    if (o.SickMax > 0 && (e.Needs & Need.Healthy) == 0) Fail($"{key}: can make an already sick player sick");
                    if (o.DrainsEnergy && (e.Needs & Need.EnergyReserve) == 0) Fail($"{key}: drains energy without an energy reserve");
                    if (o.Via == Via.Friend && (e.Needs & Need.Friend) == 0) Fail($"{key}: text from Alex without the contact");
                    if (o.Via == Via.Mom && (e.Needs & Need.Mom) == 0) Fail($"{key}: text from Mom without the contact");
                    if (o.Odds <= 0f) Fail($"{key}: outcome can never happen");
                    if (o.MaxLoss > 400 || Math.Max(o.MoneyMax, 0) > 400 && o.MoneyPct == 0f) Fail($"{key}: amount out of the sensible range");
                    if (o.Energy < -12f || o.HurtEnergy > 12f) Fail($"{key}: energy loss too large for a random event");
                }
                if (e.MinMoney < e.Cost + e.AllOutcomes.Max(o => o.MaxLoss)) Fail($"{e.Id}: can fire when the player cannot afford it");
                if (e.Weight <= 0f) Fail($"{e.Id}: weight must be positive");
            }
            Console.WriteLine($"  longest outcome text: {longest} chars ({longestKey})");
            var texts = all.SelectMany(e => e.AllOutcomes.Select(o => locale.GetValueOrDefault(e.TextKey(o), e.TextKey(o)))).ToList();
            foreach (var dup in texts.GroupBy(t => t).Where(g => g.Count() > 1)) Fail($"same text used {dup.Count()} times: {dup.Key}");
            Console.WriteLine($"  outcome texts: {texts.Count}, all different: {texts.Distinct().Count() == texts.Count}");

            // ---------------------------------------------------------------- every reallife key in the mod source
            Section("Every reallife_* string literal in the mod source");
            var notLocale = new Regex("^(reallife_sick|reallife_accident|reallife_[a-z0-9]+_calldialogtype|reallife_happinessmodifier_|reallife_ev_|reallife_transaction_)$");
            int literals = 0;
            foreach (string file in Directory.GetFiles(src, "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), "\"(reallife[_-][A-Za-z0-9_-]*)\""))
                {
                    string key = m.Groups[1].Value;
                    if (notLocale.IsMatch(key)) continue;   // internal names and key prefixes, never shown
                    literals++;
                    if (!locale.ContainsKey(key)) Fail($"{Path.GetFileName(file)}: \"{key}\" is not in en.json");
                }
            }
            Console.WriteLine($"  {literals} literal keys checked (happiness modifier labels, phone texts, options, toasts...)");

            // ---------------------------------------------------------------- no-repeat dealing
            Section("No-repeat dealing (LifeEventDeck)");
            var rng = new Random(12345);
            Func<float> r01 = () => (float)rng.NextDouble();

            // 1. One fixed situation: every round must be a permutation of everything that fits.
            LifeContext street = Context(Need.OnFoot | Need.NotDriving | Need.Dry | Need.Weekday | Need.Healthy | Need.Rested | Need.EnergyReserve | Need.Fed
                                         | Need.Happy | Need.NoVehicle | Need.Friend | Need.Mom | Need.CanPrompt | Need.Veteran, 14, 1500f);
            int fits = all.Count(e => e.Fits(street));
            var seen = new HashSet<string>();
            string last = null;
            bool roundsOk = true;
            for (int round = 0; round < 5; round++)
            {
                var thisRound = new HashSet<string>();
                for (int i = 0; i < fits; i++)
                {
                    LifeEvent e = LifeEventDeck.Draw(all, street, seen, last, r01);
                    if (e == null || !thisRound.Add(e.Id)) roundsOk = false;
                    if (i == 0 && round > 0 && e != null && e.Id == last) roundsOk = false;
                    last = e?.Id;
                }
            }
            if (!roundsOk) Fail("an event repeated before the round was complete");
            Console.WriteLine($"  fixed situation, {fits} fitting events, 5 full rounds without a repeat inside a round: {roundsOk}");

            // 2. Situation changing on every draw: a seen event may only come back once everything fitting was seen.
            seen.Clear(); last = null;
            int draws = 0, reshuffles = 0, early = 0, nothing = 0, firstRepeatAt = -1;
            var everDrawn = new HashSet<string>();
            for (int i = 0; i < 200000; i++)
            {
                LifeContext c = RandomContext(rng);
                List<LifeEvent> fitting = all.Where(e => e.Fits(c)).ToList();
                bool exhausted = fitting.All(e => seen.Contains(e.Id));
                var before = new HashSet<string>(seen);
                LifeEvent drawn = LifeEventDeck.Draw(all, c, seen, last, r01);
                if (drawn == null) { nothing++; if (fitting.Count > 0) Fail("nothing dealt although events fit"); continue; }
                draws++;
                if (!drawn.Fits(c)) Fail($"{drawn.Id} dealt in a situation it does not fit");
                if (!seen.Contains(drawn.Id)) Fail($"{drawn.Id} dealt but not recorded as seen");
                if (before.Contains(drawn.Id))
                {
                    if (!exhausted) early++;
                    reshuffles++;
                    if (firstRepeatAt < 0) firstRepeatAt = draws;
                    foreach (string id in before) if (!seen.Contains(id) && !fitting.Any(e => e.Id == id)) Fail("reshuffle forgot an event from another situation");
                }
                everDrawn.Add(drawn.Id);
                last = drawn.Id;
            }
            if (early > 0) Fail($"{early} events repeated while unseen fitting events remained");
            Console.WriteLine($"  200000 draws in random situations: {draws} dealt, {nothing} with nothing fitting, {early} early repeats, first repeat on draw {firstRepeatAt}");
            Console.WriteLine($"  events reached at least once: {everDrawn.Count}/{all.Length}");
            foreach (LifeEvent e in all) if (!everDrawn.Contains(e.Id)) Fail($"{e.Id} was never dealt in 200000 random situations (conditions contradict?)");

            // 3. Edge cases.
            if (LifeEventDeck.Draw(Array.Empty<LifeEvent>(), street, new HashSet<string>(), null, r01) != null) Fail("empty catalogue should deal nothing");
            LifeContext impossible = Context(Need.None, 12, -1f);
            int impossibleFits = all.Count(e => e.Fits(impossible));
            LifeEvent none = LifeEventDeck.Draw(all, impossible, new HashSet<string>(), null, r01);
            Console.WriteLine($"  no flags at all (nothing readable): {impossibleFits} fit, dealt {(none == null ? "nothing" : none.Id)}");
            if (impossibleFits == 0 != (none == null)) Fail("deal/no-deal disagrees with what fits");
            var stale = new HashSet<string>(all.Select(e => e.Id)) { "zz99", "" };
            LifeEvent afterAllSeen = LifeEventDeck.Draw(all, street, stale, "st01", r01);
            if (afterAllSeen == null || afterAllSeen.Id == "st01") Fail("all-seen save did not reshuffle cleanly");
            Console.WriteLine($"  everything already seen (+ unknown ids): reshuffled and dealt {afterAllSeen?.Id}; unknown ids left alone: {stale.Contains("zz99")}");
            var single = new[] { all[0] };
            var one = new HashSet<string>();
            LifeContext any = Context((Need)~0, 12, 100000f);
            bool singleOk = LifeEventDeck.Draw(single, any, one, null, r01) == all[0] && LifeEventDeck.Draw(single, any, one, all[0].Id, r01) == all[0];
            if (!singleOk) Fail("a one-event deck must keep dealing that event");
            Console.WriteLine($"  one-event deck keeps dealing: {singleOk}");

            // 4. The saved form.
            string saved = string.Join(",", all.Select(e => e.Id));
            bool roundTrip = saved.Split(',').Where(LifeEventCatalogue.Contains).ToHashSet().SetEquals(ids);
            if (!roundTrip) Fail("seen list does not survive join/split");
            Console.WriteLine($"  saved form of a fully seen catalogue: {saved.Length} characters, round-trips: {roundTrip}");
            Console.WriteLine($"  old save (empty string) -> {"".Split(',').Count(LifeEventCatalogue.Contains)} ids; junk \"x,,st01, st02\" -> {"x,,st01, st02".Split(',').Count(LifeEventCatalogue.Contains)} id");

            // ---------------------------------------------------------------- how big is the deck in practice
            Section("Events available per situation (healthy, rested, fed, both contacts, $1500, day 20)");
            Console.WriteLine("  place     weather  day       hour  no business  with business");
            int smallest = int.MaxValue;
            foreach (Need place in new[] { Need.OnFoot, Need.Indoors, Need.Driving })
            foreach (Need weather in new[] { Need.Dry, Need.Rain })
            foreach (Need day in new[] { Need.Weekday, Need.Weekend | Need.Saturday })
            foreach (int hour in new[] { 3, 9, 14, 20 })
            {
                Need baseFlags = place | weather | day | (place == Need.Driving ? Need.HasVehicle : Need.NotDriving | Need.NoVehicle) | Need.Healthy | Need.Rested
                                 | Need.EnergyReserve | Need.Fed | Need.Friend | Need.Mom | Need.CanPrompt | Need.Veteran;
                int without = all.Count(e => e.Fits(Context(baseFlags, hour, 1500f)));
                int with = all.Count(e => e.Fits(Context(baseFlags | Need.HasBusiness, hour, 1500f)));
                smallest = Math.Min(smallest, without);
                if (hour == 14 || hour == 3)
                    Console.WriteLine($"  {place,-9} {weather,-8} {(day == Need.Weekday ? "weekday" : "saturday"),-9} {hour,4}  {without,11}  {with,13}");
            }
            Need bare = Need.Indoors | Need.NotDriving | Need.Dry | Need.Weekday | Need.Healthy | Need.NoVehicle | Need.CanPrompt | Need.Tired | Need.Hungry;
            int worst = all.Count(e => e.Fits(Context(bare, 3, 40f)));
            Console.WriteLine($"  smallest in the grid above: {smallest}");
            Console.WriteLine($"  worst case (indoors, 3am, $40, tired, hungry, phone contacts switched off): {worst}");
            if (smallest < 70) Fail("a common situation has fewer than 70 events (about what 10 hours of play can show)");

            CheckCrimes(src, locale);
            CheckContacts(src);
            GigCheck.Run(Fail, locale);
            SavedShiftCheck.Run(Fail, locale, src);
            OptionsCheck.Run(Fail, locale, src);

            Console.WriteLine(_failures == 0 ? "\nALL CHECKS PASSED" : $"\n{_failures} CHECK(S) FAILED");
            return _failures == 0 ? 0 : 1;
        }

        // The crime catalogue: ids, lines, endings, a scene for every entry, and the dealing.
        private static void CheckCrimes(string src, Dictionary<string, string> locale)
        {
            Section("Crime catalogue");
            CrimeEntry[] crimes = CrimeCatalogue.All;
            var ids = new HashSet<string>();
            var allowed = new HashSet<string> { "amount" };
            var placeholder = new Regex(@"\{([^}]*)\}");
            var usedBy = new Dictionary<string, string>();
            int lines = 0, endings = 0;
            foreach (CrimeEntry e in crimes)
            {
                if (!ids.Add(e.Id)) Fail($"duplicate crime id {e.Id}");
                if (!Regex.IsMatch(e.Id, "^[a-z_]+$")) Fail($"{e.Id}: id is not lower case letters and underscores");
                if (CrimeCatalogue.Find(e.Id) != e) Fail($"{e.Id}: Find does not return the entry");
                if (e.Cast < 1 || e.Cast > 4) Fail($"{e.Id}: a scene has four roles at most (and the pool six people)");
                if (e.Place == CrimePlace.None) Fail($"{e.Id}: can happen nowhere");
                if (e.Weight < 0f) Fail($"{e.Id}: negative weight");
                if (e.Endings.Length == 0 || !e.Endings.Any(x => x.Odds > 0f)) Fail($"{e.Id}: no ending that can be rolled");
                if (e.Dealt && e.Starts.Length == 0 && e.Family != CrimeFamily.Player) Fail($"{e.Id}: dealt at random but announces nothing when it starts");
                if (e.Family == CrimeFamily.Player && (e.Needs & CrimeNeed.AgainstPlayer) == 0) Fail($"{e.Id}: a crime against the player that ignores the option for them");
                if ((e.Needs & CrimeNeed.AgainstPlayer) != 0 && (e.Needs & CrimeNeed.Cash) == 0) Fail($"{e.Id}: can rob a player who carries nothing");
                var names = new HashSet<string>();
                foreach (CrimeEnding ending in e.Endings)
                {
                    endings++;
                    if (!names.Add(ending.Name)) Fail($"{e.Id}: ending {ending.Name} listed twice");
                    if (ending.Texts.Length == 0) Fail($"{e.Id}/{ending.Name}: an ending with nothing to say");
                    if (ending.Odds < 0f) Fail($"{e.Id}/{ending.Name}: negative odds");
                }
                foreach (string key in e.TextKeys())
                {
                    lines++;
                    if (!locale.TryGetValue(key, out string text) || string.IsNullOrWhiteSpace(text))
                    {
                        Fail($"{e.Id}: key {key} is missing or empty in en.json");
                        continue;
                    }
                    if (key != e.Marker && usedBy.TryGetValue(key, out string other) && other != e.Id) Fail($"{key}: used by both {other} and {e.Id}");
                    usedBy[key] = e.Id;
                    foreach (Match m in placeholder.Matches(text))
                    {
                        if (!allowed.Contains(m.Groups[1].Value)) Fail($"{key}: unknown placeholder {m.Value}");
                    }
                    if (text.Length > 240) Fail($"{key}: too long for a toast ({text.Length} characters)");
                }
                // Lines shown without an amount must not ask for one.
                foreach (string key in e.Starts.Concat(e.Near).Concat(e.Extra))
                {
                    if (locale.TryGetValue(key, out string text) && text.Contains("{amount}")) Fail($"{key}: starts and remarks are shown without an amount");
                }
            }
            List<string> sameText = crimes.SelectMany(e => e.TextKeys().Where(k => k != e.Marker)).Distinct()
                .Where(locale.ContainsKey).GroupBy(k => locale[k]).Where(g => g.Count() > 1).Select(g => string.Join(", ", g)).ToList();
            foreach (string dup in sameText) Fail($"same line under several keys: {dup}");
            Console.WriteLine($"  crimes: {crimes.Length}   endings: {endings}   lines: {lines}   dealt at random: {crimes.Count(e => e.Dealt)}");
            foreach (var g in crimes.GroupBy(e => e.Family))
            {
                Console.WriteLine($"    {g.Key,-8} {g.Count(),2}  ({string.Join(", ", g.Select(e => e.Id))})");
            }

            // Every entry has a scene, every scene an entry.
            string scenes = File.ReadAllText(Path.Combine(src, "Scripts", "Crime", "CrimeScenes.cs"));
            string sceneDir = Path.Combine(src, "Scripts", "Crime", "Scenes");
            var played = new HashSet<string>();
            foreach (Match m in Regex.Matches(scenes, @"new (\w+)\((?:""([a-z_]+)"")?"))
            {
                string id = m.Groups[2].Success ? m.Groups[2].Value : null;
                if (id == null)
                {
                    string file = Path.Combine(sceneDir, m.Groups[1].Value + ".cs");
                    Match own = File.Exists(file) ? Regex.Match(File.ReadAllText(file), @"string Id => ""([a-z_]+)""") : Match.Empty;
                    if (!own.Success) continue;   // not a scene (a dictionary, say)
                    id = own.Groups[1].Value;
                }
                if (!played.Add(id)) Fail($"two scenes registered for {id}");
                if (!ids.Contains(id)) Fail($"scene registered for {id}, which is not in the catalogue");
            }
            foreach (string id in ids) if (!played.Contains(id)) Fail($"{id}: no scene registered in CrimeScenes.cs");
            Console.WriteLine($"  scenes registered: {played.Count}, entries: {ids.Count}");

            // Ending names a scene asks for must exist in an entry (a misspelt one would silently never happen).
            var known = new HashSet<string>(crimes.SelectMany(e => e.Endings.Select(x => x.Name)));
            foreach (string file in Directory.GetFiles(Path.Combine(src, "Scripts", "Crime"), "*.cs", SearchOption.AllDirectories))
            {
                foreach (Match m in Regex.Matches(File.ReadAllText(file), @"(?:\bIs|Conclude|ConcludeAmount|Ending|Arrest)\((?:[A-Za-z]+, now, )?""([a-z_]+)"""))
                {
                    if (!known.Contains(m.Groups[1].Value)) Fail($"{Path.GetFileName(file)}: ending \"{m.Groups[1].Value}\" is in no catalogue entry");
                }
            }

            Section("No-repeat dealing (CrimeDeck)");
            var rng = new Random(2468);
            Func<float> r01 = () => (float)rng.NextDouble();
            CrimeNeed everything = (CrimeNeed)~0;
            foreach (CrimePlace place in new[] { CrimePlace.Street, CrimePlace.OwnShop, CrimePlace.OtherShop })
            {
                int fits = crimes.Count(e => e.Dealt && e.Fits(place, everything, 6));
                var seen = new HashSet<string>();
                string last = null;
                bool ok = true;
                for (int round = 0; round < 6 && fits > 0; round++)
                {
                    var thisRound = new HashSet<string>();
                    for (int i = 0; i < fits; i++)
                    {
                        CrimeEntry e = CrimeDeck.Draw(crimes, place, everything, 6, seen, last, r01);
                        if (e == null || !e.Dealt || !e.Fits(place, everything, 6) || !thisRound.Add(e.Id)) ok = false;
                        if (i == 0 && round > 0 && fits > 1 && e != null && e.Id == last) ok = false;
                        last = e?.Id;
                    }
                }
                if (!ok) Fail($"{place}: a crime repeated before the round was complete");
                Console.WriteLine($"  {place,-10} {fits,2} kinds fit; six full rounds without a repeat inside a round: {ok}");
                if (place == CrimePlace.Street && fits < 12) Fail("fewer than a dozen kinds of crime on the street");
            }
            if (CrimeDeck.Draw(crimes, CrimePlace.None, everything, 6, new HashSet<string>(), null, r01) != null) Fail("something dealt for nowhere");
            if (CrimeDeck.Draw(crimes, CrimePlace.Street, everything, 0, new HashSet<string>(), null, r01) != null) Fail("something dealt with nobody to play it");
            CrimeNeed sober = everything & ~CrimeNeed.AgainstPlayer;
            for (int i = 0; i < 2000; i++)
            {
                CrimeEntry e = CrimeDeck.Draw(crimes, CrimePlace.Street, sober, 6, new HashSet<string>(), null, r01);
                if (e != null && e.Family == CrimeFamily.Player) { Fail($"{e.Id} dealt with crimes against the player switched off"); break; }
            }

            // Endings: odds respected, zero-odds endings never rolled, no ending twice running where three can be rolled.
            int rolled = 0;
            foreach (CrimeEntry e in crimes)
            {
                int rollable = e.Endings.Count(x => x.Odds > 0f);
                string lastEnding = null;
                var got = new HashSet<string>();
                for (int i = 0; i < 400; i++)
                {
                    CrimeEnding ending = CrimeDeck.DrawEnding(e.Endings, lastEnding, r01);
                    rolled++;
                    if (ending == null) { Fail($"{e.Id}: no ending rolled"); break; }
                    if (ending.Odds <= 0f) { Fail($"{e.Id}: rolled {ending.Name}, which has no odds"); break; }
                    if (rollable >= 3 && ending.Name == lastEnding) { Fail($"{e.Id}: {ending.Name} twice running"); break; }
                    got.Add(ending.Name);
                    lastEnding = ending.Name;
                }
                if (got.Count != rollable) Fail($"{e.Id}: only {got.Count} of {rollable} endings ever came up in 400 rolls");
                foreach (CrimeEnding ending in e.Endings)
                {
                    string lastText = null;
                    for (int i = 0; i < 50 && ending.Texts.Length > 1; i++)
                    {
                        string text = CrimeDeck.DrawText(ending.Texts, lastText, r01);
                        if (text == lastText) { Fail($"{e.Id}/{ending.Name}: the same line twice running"); break; }
                        lastText = text;
                    }
                }
            }
            Console.WriteLine($"  {rolled} endings rolled: every rollable ending reached, none without odds, no ending or line twice running");
        }

        private static LifeContext Context(Need flags, int hour, float money) => new LifeContext { Flags = flags, Hour = hour, Money = money };

        // Mirrors the combinations LifeEventRunner.ReadContext can produce.
        private static LifeContext RandomContext(Random rng)
        {
            bool Coin(double p = 0.5) => rng.NextDouble() < p;
            Need f = Need.None;
            int place = rng.Next(4);   // 3 = on foot inside a parking garage: neither indoors nor outdoors
            f |= place == 2 ? Need.Driving : Need.NotDriving;
            if (place == 0) f |= Need.OnFoot;
            if (place == 1) f |= Need.Indoors;
            int weather = rng.Next(3); // 2 = clouding over: raining by the game's flag, no drops yet
            if (weather == 0) f |= Need.Dry;
            if (weather == 1) f |= Need.Rain;
            int day = rng.Next(1, 8);
            f |= day >= 5 ? Need.Weekend : Need.Weekday;
            if (day == 1) f |= Need.Monday;
            if (day == 5) f |= Need.Friday;
            if (day == 6) f |= Need.Saturday;
            if (day == 7) f |= Need.Sunday;
            f |= Coin(0.15) ? Need.Sick : Need.Healthy;
            int energy = rng.Next(0, 101);
            if (energy < 35) f |= Need.Tired;
            if (energy >= 60) f |= Need.Rested;
            if (energy >= 30) f |= Need.EnergyReserve;
            int hunger = rng.Next(0, 101);
            if (hunger < 35) f |= Need.Hungry;
            if (hunger >= 60) f |= Need.Fed;
            int happiness = rng.Next(0, 101);
            if (happiness >= 60) f |= Need.Happy;
            if (happiness < 35) f |= Need.Unhappy;
            f |= place == 2 || Coin() ? Need.HasVehicle : Need.NoVehicle;
            if (Coin()) f |= Need.HasBusiness;
            if (Coin(0.3)) f |= Need.MomGiftDue;
            if (Coin(0.9)) f |= Need.Friend | Need.Mom;
            if (Coin(0.95)) f |= Need.CanPrompt;
            int gameDay = rng.Next(1, 40);
            if (gameDay <= 7) f |= Need.Newcomer;
            if (gameDay >= 15) f |= Need.Veteran;
            float[] money = { -50f, 0f, 30f, 120f, 400f, 1500f, 6000f, 250000f };
            return Context(f, rng.Next(24), money[rng.Next(money.Length)]);
        }
    }
}
