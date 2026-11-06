using System;
using System.Collections.Generic;
using BigAmbitions.Mods;
using RealLifeOverhaul.TestRun;

namespace RealLifeOverhaul.Meta
{
    /// <summary>The four presets. An option says what it is at Relaxed, Realistic and Hardcore; Standard is always its default.</summary>
    public enum Intensity
    {
        Relaxed = 0,
        Standard = 1,
        Realistic = 2,
        Hardcore = 3
    }

    /// <summary>What the presets set one option to; a toggle is 0 or 1.</summary>
    public sealed class PresetRule
    {
        public readonly string OptionId;
        public readonly int Relaxed;
        public readonly int Realistic;
        public readonly int Hardcore;

        public PresetRule(string optionId, int relaxed, int realistic, int hardcore)
        {
            OptionId = optionId;
            Relaxed = relaxed;
            Realistic = realistic;
            Hardcore = hardcore;
        }
    }

    /// <summary>A test button: its label on the options page and what it does.</summary>
    public sealed class TestAction
    {
        public readonly string LabelKey;
        public Action Run;

        public TestAction(string labelKey, Action run)
        {
            LabelKey = labelKey;
            Run = run;
        }
    }

    /// <summary>One feature as the mod's own tools see it, filled in by one <see cref="Features.Add"/> statement whose calls each return it.</summary>
    public sealed class Feature
    {
        public readonly string Id;
        public readonly string TitleKey;

        public string AboutKey;                 // one sentence: what it is
        public string HelpKey;                  // its page in the guide
        public int CleanupOrder;                // lower first; whoever lends people to the others goes last
        public bool Auto;                       // made from an options header nobody registered

        public readonly List<string> Groups = new List<string>();        // options headers whose rows are its
        public readonly List<string> Options = new List<string>();       // option ids it owns
        public readonly List<string> RowLabels = new List<string>();     // its buttons by label key (not its tests)
        public readonly List<string> ReloadOptions = new List<string>(); // option ids that only apply after the save is loaded again
        public readonly List<PresetRule> Presets = new List<PresetRule>();
        public readonly List<string> DataKeys = new List<string>();      // modData keys (or prefixes) it writes
        public readonly List<string> ContactIds = new List<string>();    // phone contacts it adds
        public readonly List<string> ObjectNames = new List<string>();   // GameObjects of its own that are not under the mod's root
        public readonly List<TestAction> Tests = new List<TestAction>();
        public readonly List<RunCheck> Checks = new List<RunCheck>();     // its tests that the city expansion's test run runs by itself
        public readonly List<ModOption> ExtraRows = new List<ModOption>(); // rows it adds to the page itself
        public readonly List<KeyAction> DiagnosticKeys = new List<KeyAction>();
        public readonly Dictionary<string, Func<string>> Facts = new Dictionary<string, Func<string>>();
        public readonly Dictionary<string, Func<bool>> RowPresent = new Dictionary<string, Func<bool>>();   // option id: whether its row is on the page
        public readonly List<string> AsideOptions = new List<string>();   // option ids that do nothing while AsideWhy says so
        public Func<string> AsideWhy;           // the sentence that says why, or null while they work

        public Action CleanupAction;            // take everything of mine off the street and stop what is running
        public Action ResetAction;              // forget what I remember of this save
        public Action RepairAction;             // put back what I add to a save (contacts, modifiers) if it has gone

        internal Feature(string id, string titleKey)
        {
            Id = id;
            TitleKey = titleKey;
        }

        /// <summary>An options header (as RealLifeOverhaulMod.cs adds it) whose rows are this feature's.</summary>
        public Feature Group(string headerKey)
        {
            Groups.Add(headerKey);
            return this;
        }

        public Feature About(string key)
        {
            AboutKey = key;
            return this;
        }

        /// <summary>Its page in the guide: one en.json text. "# " starts a heading, "- " a bullet, {name} a fact.</summary>
        public Feature Help(string key)
        {
            HelpKey = key;
            return this;
        }

        /// <summary>Option ids that are this feature's wherever they stand on the page.</summary>
        public Feature Owns(params string[] optionIds)
        {
            Options.AddRange(optionIds);
            return this;
        }

        /// <summary>Buttons (by label key) that are this feature's and are not tests.</summary>
        public Feature Rows(params string[] labelKeys)
        {
            RowLabels.AddRange(labelKeys);
            return this;
        }

        public Feature Row(ModOption option)
        {
            ExtraRows.Add(option);
            return this;
        }

        /// <summary>What the presets set an option to (Standard is its default; a toggle is 0 or 1). The option becomes this feature's.</summary>
        public Feature Preset(string optionId, int relaxed, int realistic, int hardcore)
        {
            Presets.Add(new PresetRule(optionId, relaxed, realistic, hardcore));
            if (!Options.Contains(optionId))
            {
                Options.Add(optionId);
            }
            return this;
        }

        /// <summary>A toggle that is on from this intensity up and off below it.</summary>
        public Feature OnFrom(string optionId, Intensity from)
        {
            return Preset(optionId, from <= Intensity.Relaxed ? 1 : 0, from <= Intensity.Realistic ? 1 : 0, 1);
        }

        /// <summary>Options that are only read when a save is loaded: the presets say so when they change one.</summary>
        public Feature NeedsReload(params string[] optionIds)
        {
            ReloadOptions.AddRange(optionIds);
            return this;
        }

        /// <summary>modData keys it writes. A key ending in '*' is a prefix. Everything under "reallife:" is removed anyway.</summary>
        public Feature Data(params string[] keys)
        {
            DataKeys.AddRange(keys);
            return this;
        }

        public Feature Contact(params string[] contactIds)
        {
            ContactIds.AddRange(contactIds);
            return this;
        }

        /// <summary>Objects of its own outside the mod's root object, destroyed by name when the save is prepared for removal.</summary>
        public Feature Objects(params string[] names)
        {
            ObjectNames.AddRange(names);
            return this;
        }

        /// <summary>Test buttons already on the page, by label key; their actions are taken from the page.</summary>
        public Feature Test(params string[] labelKeys)
        {
            foreach (string key in labelKeys)
            {
                AddTest(key, null);
            }
            return this;
        }

        /// <summary>A test button that is not on the original page.</summary>
        public Feature Test(string labelKey, Action run)
        {
            AddTest(labelKey, run);
            return this;
        }

        /// <summary>A test button placed first among the feature's (the one a developer is most often sent to).</summary>
        public Feature TestFirst(string labelKey, Action run)
        {
            AddTest(labelKey, run);
            for (int i = 1; i < Tests.Count; i++)
            {
                if (Tests[i].LabelKey == labelKey)
                {
                    TestAction test = Tests[i];
                    Tests.RemoveAt(i);
                    Tests.Insert(0, test);
                    break;
                }
            }
            return this;
        }

        /// <summary>
        /// A test the city expansion's test run (Scripts/TestRun) runs by itself, and its button: <paramref name="run"/> works
        /// without any input and writes one line beginning <paramref name="line"/> ("Landmarks check (tower)"), which says
        /// PASS or FAIL; the run puts the player at <paramref name="place"/> first and waits <paramref name="seconds"/> s of
        /// the game running for the line. One line in a feature's guide:
        /// <c>.Check(LandmarksText.TestTower, LandmarksDirector.TestTower, "Landmarks check (tower)", RunPlace.UptownLot)</c>, its label key first
        /// </summary>
        public Feature Check(string labelKey, Action run, string line, RunPlace place = RunPlace.Here, RunPhase phase = RunPhase.BeforeLoad, float seconds = RunRules.DefaultSeconds)
        {
            AddTest(labelKey, run);
            Checks.Add(new RunCheck(labelKey, Id, line, place, phase, seconds) { Run = run });
            return this;
        }

        internal void AddTest(string labelKey, Action run)
        {
            foreach (TestAction test in Tests)
            {
                if (test.LabelKey == labelKey)
                {
                    test.Run = test.Run ?? run;
                    return;
                }
            }
            Tests.Add(new TestAction(labelKey, run));
        }

        /// <summary>Take everything of this feature off the street and end what is running. Must be safe when nothing is.</summary>
        public Feature Cleanup(Action action, int order = 0)
        {
            CleanupAction = action;
            CleanupOrder = order;
            return this;
        }

        /// <summary>Forget what is remembered of this save (its modData has just been removed).</summary>
        public Feature ResetData(Action action)
        {
            ResetAction = action;
            return this;
        }

        public Feature Repair(Action action)
        {
            RepairAction = action;
            return this;
        }

        /// <summary>A number or name the guide prints in place of {name}, read from the code when the page is opened.</summary>
        public Feature Fact(string name, Func<string> value)
        {
            Facts[name] = value;
            return this;
        }

        /// <summary>Keys that are development aids: off while "Developer diagnostics" is off.</summary>
        public Feature Diagnostic(params KeyAction[] actions)
        {
            DiagnosticKeys.AddRange(actions);
            return this;
        }

        /// <summary>A row on the page only while <paramref name="present"/> is true, asked each time the page is drawn; its option keeps its value.</summary>
        public Feature Present(string optionId, Func<bool> present)
        {
            RowPresent[optionId] = present;
            return this;
        }

        /// <summary>Options that do nothing while <paramref name="why"/> returns a sentence (another mod has what they steer): drawn disabled, the reason on the section.</summary>
        public Feature StandsAside(Func<string> why, params string[] optionIds)
        {
            AsideWhy = why;
            AsideOptions.AddRange(optionIds);
            return this;
        }
    }

    /// <summary>On a class with a <c>static void DescribeFeature()</c>, called once at start-up, so a feature can register itself.</summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DescribesFeatureAttribute : Attribute
    {
    }

    /// <summary>The one list of the mod's features: the options page, the presets, the Guide and the save tools all read it.</summary>
    public static class Features
    {
        private static readonly List<Feature> List = new List<Feature>();
        private static bool _registered;

        public static IReadOnlyList<Feature> All
        {
            get
            {
                EnsureRegistered();
                return List;
            }
        }

        /// <summary>Registers a feature (or returns the one already registered under this id).</summary>
        public static Feature Add(string id, string titleKey)
        {
            Feature existing = FindRaw(id);
            if (existing != null)
            {
                return existing;
            }
            var feature = new Feature(id, titleKey);
            List.Add(feature);
            return feature;
        }

        public static Feature Find(string id)
        {
            EnsureRegistered();
            return FindRaw(id);
        }

        private static Feature FindRaw(string id)
        {
            foreach (Feature feature in List)
            {
                if (feature.Id == id)
                {
                    return feature;
                }
            }
            return null;
        }

        public static Feature OwnerOfOption(string optionId)
        {
            if (optionId != null)
            {
                foreach (Feature feature in All)
                {
                    if (feature.Options.Contains(optionId))
                    {
                        return feature;
                    }
                }
            }
            return null;
        }

        public static Feature OwnerOfRow(string labelKey)
        {
            if (labelKey != null)
            {
                foreach (Feature feature in All)
                {
                    if (feature.RowLabels.Contains(labelKey))
                    {
                        return feature;
                    }
                }
            }
            return null;
        }

        public static Feature OwnerOfTest(string labelKey)
        {
            if (labelKey != null)
            {
                foreach (Feature feature in All)
                {
                    foreach (TestAction test in feature.Tests)
                    {
                        if (test.LabelKey == labelKey)
                        {
                            return feature;
                        }
                    }
                }
            }
            return null;
        }

        public static Feature OwnerOfGroup(string headerKey)
        {
            if (headerKey != null)
            {
                foreach (Feature feature in All)
                {
                    if (feature.Groups.Contains(headerKey))
                    {
                        return feature;
                    }
                }
            }
            return null;
        }

        public static PresetRule RuleFor(string optionId)
        {
            foreach (Feature feature in All)
            {
                foreach (PresetRule rule in feature.Presets)
                {
                    if (rule.OptionId == optionId)
                    {
                        return rule;
                    }
                }
            }
            return null;
        }

        public static bool NeedsReload(string optionId)
        {
            foreach (Feature feature in All)
            {
                if (feature.ReloadOptions.Contains(optionId))
                {
                    return true;
                }
            }
            return false;
        }

        /// <summary>True for a modData key some feature has declared, or one under the mod's own prefix.</summary>
        public static bool OwnsData(string key)
        {
            if (key == null)
            {
                return false;
            }
            if (key.StartsWith(ModState.DataPrefix, StringComparison.Ordinal))
            {
                return true;
            }
            return DeclaredData(key) != null;
        }

        /// <summary>The feature that declared this modData key, or null.</summary>
        public static Feature DeclaredData(string key)
        {
            foreach (Feature feature in All)
            {
                foreach (string declared in feature.DataKeys)
                {
                    bool prefix = declared.EndsWith("*", StringComparison.Ordinal);
                    if (prefix ? key.StartsWith(declared.Substring(0, declared.Length - 1), StringComparison.Ordinal) : key == declared)
                    {
                        return feature;
                    }
                }
            }
            return null;
        }

        public static Feature OwnerOfContact(string contactId)
        {
            foreach (Feature feature in All)
            {
                if (feature.ContactIds.Contains(contactId))
                {
                    return feature;
                }
            }
            return null;
        }

        /// <summary>Registers every feature once: the list in FeatureList.cs, then any class marked <see cref="DescribesFeatureAttribute"/>.</summary>
        public static void EnsureRegistered()
        {
            if (_registered)
            {
                return;
            }
            _registered = true;
            try
            {
                FeatureList.RegisterAll();
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
            try
            {
                foreach (Type type in typeof(Features).Assembly.GetTypes())
                {
                    if (!type.IsDefined(typeof(DescribesFeatureAttribute), false))
                    {
                        continue;
                    }
                    var method = type.GetMethod("DescribeFeature",
                        System.Reflection.BindingFlags.Static | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                    if (method == null || method.GetParameters().Length != 0)
                    {
                        MetaLog.Warn("Features: " + type.Name + " is marked DescribesFeature but has no static DescribeFeature().");
                        continue;
                    }
                    try
                    {
                        method.Invoke(null, null);
                    }
                    catch (Exception e)
                    {
                        MetaLog.Error(e);
                    }
                }
            }
            catch (Exception e)
            {
                MetaLog.Error(e);
            }
        }
    }
}
