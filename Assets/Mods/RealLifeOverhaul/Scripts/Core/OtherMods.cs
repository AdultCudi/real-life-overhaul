using System;
using System.Reflection;
using BAModAPI;

namespace RealLifeOverhaul
{
    /// <summary>What another author's mod does that this one also does.</summary>
    public enum OtherModKind
    {
        Camera,   // it drives the game's camera
        Route     // it draws a route and markers on the city map
    }

    /// <summary>One mod of another author's that this mod knows by name.</summary>
    public sealed class OtherMod
    {
        public readonly string AssemblyPrefix;   // what its assembly's name begins with (some carry their version after it)
        public readonly string KnownType;        // a type every version of it has
        public readonly string Name;             // as its author calls it: a name, never translated
        public readonly OtherModKind Kind;

        /// <summary>True once its assembly, with its known type in it, has been seen among the loaded ones.</summary>
        public bool Loaded { get; internal set; }

        internal OtherMod(string assemblyPrefix, string knownType, string name, OtherModKind kind)
        {
            AssemblyPrefix = assemblyPrefix;
            KnownType = knownType;
            Name = name;
            Kind = kind;
        }
    }

    /// <summary>
    /// The other authors' mods this one knows of, and whether each is switched on. See
    /// RealLifeOverhaul.Build/docs/other-mods.md: the rule for living with another mod, and this table, to which the
    /// next clash is one more line.
    ///
    /// A mod is known by a type of its own, by name: the game loads every mod that is switched on into the same
    /// program, so its assembly is among the loaded ones. An assembly whose name begins as the table says and which
    /// holds the named type is that mod (the name alone would not do: one of them carries its version in it).
    ///
    /// The cost: nothing per frame. <see cref="Look"/> walks the loaded assemblies once when it is called (a city
    /// being loaded, the options page being drawn), and stops looking for a mod once it has been found. Everything
    /// else here is a field read.
    /// </summary>
    public static class OtherMods
    {
        public static readonly OtherMod RpgCamera = new OtherMod("Immersion", "Immersion.ImmersionEntry", "Immersion - RPG Camera", OtherModKind.Camera);
        public static readonly OtherMod VoogleRoute = new OtherMod("VoogleRoute", "VoogleRoute.VoogleRouteCityMod", "Voogle Route", OtherModKind.Route);

        public static readonly OtherMod[] All = { RpgCamera, VoogleRoute };

        /// <summary>
        /// One walk over the loaded assemblies for every mod of the table not found yet. One line in the log for each
        /// newly found. Safe at any time; nothing thrown here reaches the caller.
        /// </summary>
        public static void Look(IModLogger logger)
        {
            try
            {
                bool all = true;
                for (int i = 0; i < All.Length; i++)
                {
                    all &= All[i].Loaded;
                }
                if (all)
                {
                    return;
                }
                foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
                {
                    string name = assembly.GetName().Name;
                    for (int i = 0; i < All.Length; i++)
                    {
                        OtherMod mod = All[i];
                        if (!mod.Loaded && OtherModName.Is(name, mod.AssemblyPrefix) && assembly.GetType(mod.KnownType, false) != null)
                        {
                            mod.Loaded = true;
                            logger?.Warn("Other mods: '" + mod.Name + "' is also switched on (assembly " + name + "). "
                                         + (mod.Kind == OtherModKind.Camera
                                             ? "While it drives the camera, this mod's camera, mouse look and chase camera stand aside."
                                             : "The city map is left to it: this mod draws its route on its own minimap only, and not at all with 'Use the other mod's route line' on."));
                        }
                    }
                }
            }
            catch (Exception e)
            {
                logger?.Error(e);
            }
        }

        /// <summary>The first mod of a kind that is switched on, or null.</summary>
        public static OtherMod Of(OtherModKind kind)
        {
            for (int i = 0; i < All.Length; i++)
            {
                if (All[i].Kind == kind && All[i].Loaded)
                {
                    return All[i];
                }
            }
            return null;
        }
    }
}
