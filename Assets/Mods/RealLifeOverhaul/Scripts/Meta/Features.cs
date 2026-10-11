using System;
using System.Collections.Generic;
using BigAmbitions.Mods;

namespace RealLifeOverhaul.Meta
{
    /// <summary>Where a feature's section goes on the options page.</summary>
    public enum FeatureKind
    {
        Gameplay,      // alphabetical, under "Features"
        Controls,
        Performance,
        Tools          // save tools, diagnostics: last
    }

    /// <summary>The four presets. An option says what it is at Relaxed, Realistic and Hardcore; Standard is always its default.</summary>
    public enum Intensity
    {
        Relaxed = 0,
        Standard = 1,
        Realistic = 2,
        Hardcore = 3
    }

    /// <summary>One option's place in the presets. Toggles are 0 and 1.</summary>
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

    /// <summary>
    /// Everything the mod's own tools need to know about one feature: its section on the options page, its
    /// options and what the presets do with them, what it writes into a save, what it adds to the phone, how
    /// to take it off the street, and its page in the guide. Filled in with one statement
    /// (<see cref="Features.Add"/> and the methods below, each of which returns the feature).
    /// </summary>
    public sealed class Feature
    {
        public readonly string Id;
        public readonly string TitleKey;

        public FeatureKind Kind = FeatureKind.Gameplay;
        public string AboutKey;                 // one sentence: what it is
        public string HelpKey;                  // its page in the guide
        public int CleanupOrder;                // lower first; whoever lends people to the others goes last
        public bool Auto;                       // made from an options header nobody registered
        public bool StartsOpen;                 // its section is open the first time the page is shown

        public readonly List<string> Groups = new List<string>();        // header keys of the original page that are its
        public readonly List<string> Options = new List<string>();       // option ids it owns
        public readonly List<string> RowLabels = new List<string>();     // buttons of the original page that are its (not tests)
        public readonly List<string> ReloadOptions = new List<string>(); // option ids that only apply after the save is loaded again
        public readonly List<PresetRule> Presets = new List<PresetRule>();
        public readonly List<string> DataKeys = new List<string>();      // modData keys (or prefixes) it writes
        public readonly List<string> ContactIds = new List<string>();    // phone contacts it adds
        public readonly List<string> ObjectNames = new List<string>();   // GameObjects of its own that are not under the mod's root
        public readonly List<TestAction> Tests = new List<TestAction>();
        public readonly List<ModOption> ExtraRows = new List<ModOption>(); // rows that are not on the original page
        public readonly List<KeyAction> DiagnosticKeys = new List<KeyAction>();
        public readonly Dictionary<string, Func<string>> Facts = new Dictionary<string, Func<string>>();
        public readonly Dictionary<string, Func<bool>> RowPresent = new Dictionary<string, Func<bool>>();   // option id: whether its row is on the page at all
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

        /// <summary>The header of its options group as the page is built in RealLifeOverhaulMod.cs: rows under it are this feature's.</summary>
        public Feature Group(string headerKey)
        {
            Groups.Add(headerKey);
            return this;
        }

        public Feature As(FeatureKind kind)
        {
            Kind = kind;
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

        /// <summary>A row of its own that is not on the original page.</summary>
        public Feature Row(ModOption option)
        {
            ExtraRows.Add(option);
            return this;
        }

        /// <summary>
        /// What the presets set this option to. Standard is not given: it is the option's default.
        /// A toggle is 0 or 1; a slider its value. The option becomes this feature's.
        /// </summary>
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

        /// <summary>A test button that is on the original page: only its label, the action is taken from the page.</summary>
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

        /// <summary>Put back what it adds to a save if it has gone missing.</summary>
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

        /// <summary>
        /// A row that is on the options page only while something is true (another author's mod is switched on):
        /// asked each time the page is drawn. The option keeps its saved value while its row is away.
        /// </summary>
        public Feature Present(string optionId, Func<bool> present)
        {
            RowPresent[optionId] = present;
            return this;
        }

        /// <summary>
        /// Options that do nothing for a while (another mod has what they steer). While <paramref name="why"/>
        /// answers a sentence, their rows are drawn as the game draws a control that cannot be used, and the
        /// section's button says the sentence after its name. Asked each time the page is drawn.
        /// </summary>
        public Feature StandsAside(Func<string> why, params string[] optionIds)
        {
            AsideWhy = why;
            AsideOptions.AddRange(optionIds);
            return this;
        }

        public Feature OpenAtFirst()
        {
            StartsOpen = true;
            return this;
        }
    }

    /// <summary>
    /// Put on a class that has a <c>static void DescribeFeature()</c>: it is called once at game start, so a feature
    /// can register itself from its own file instead of from Meta/FeatureList.cs.
    /// </summary>
    [AttributeUsage(AttributeTargets.Class)]
    public sealed class DescribesFeatureAttribute : Attribute
    {
    }

    /// <summary>
    /// The one list of the mod's features. The options page, the presets, the guide, the save tools and the
    /// diagnostics switch all read it, so a feature that registers here appears in each of them.
    /// </summary>
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
