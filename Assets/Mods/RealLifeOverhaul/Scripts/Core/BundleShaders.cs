using System;
using System.Collections.Generic;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The shaders' check of a bundle of the mod's as it is read (<see cref="BundleShaderRules"/>). Each loader calls
    /// <see cref="Check"/> once with what it read, behind the loading screen, before any of it is used. A material that came
    /// in the bundle is given the game's own shader of its name, with its properties, keywords and render queue kept, since
    /// the one it brought is built for Windows alone; one that the game has no shader for is named in the line, which is a
    /// warning then. The line is written whatever Developer diagnostics says: it answers a report of pink or missing
    /// things from a player's log. Cost: once a bundle a session; nothing a frame and nothing kept.
    /// </summary>
    public static class BundleShaders
    {
        private const string ErrorShader = "Hidden/InternalErrorShader";   // what the engine draws pink with, and what a material without its shader reads as

        /// <summary>
        /// Checks one bundle: <paramref name="read"/> is what its loader read of it (null for a bundle read piece by piece),
        /// <paramref name="drawnWith"/> the game's shader its pieces' material is made from (null for clips, and for pieces
        /// worn on copies of the game's own materials).
        /// </summary>
        public static void Check(IModLogger logger, string bundle, AssetBundle file, UnityEngine.Object[] read, string drawnWith)
        {
            try
            {
                Checked(logger, bundle, file, read, drawnWith);
            }
            catch (Exception e)
            {
                logger?.Warn("Shaders check (" + bundle + "): could not be made (" + e.GetType().Name + ": " + e.Message + "); nothing of the bundle was changed.");
            }
        }

        private static void Checked(IModLogger logger, string bundle, AssetBundle file, UnityEngine.Object[] read, string drawnWith)
        {
            var materials = new List<Material>(4);
            for (int i = 0; read != null && i < read.Length; i++)
            {
                Gather(read[i], materials);
            }
            var came = new List<Shader>(materials.Count);   // what each material came with, before any is changed
            for (int i = 0; i < materials.Count; i++)
            {
                came.Add(materials[i].shader);
            }
            var shaders = new List<Shader>(2);       // as the bundle brought them, each once
            int unsupported = 0, repointed = 0, left = 0;
            string names = null;
            for (int i = 0; i < came.Count; i++)
            {
                if (came[i] != null && !shaders.Contains(came[i]))
                {
                    shaders.Add(came[i]);
                    unsupported += Draws(came[i]) ? 0 : 1;
                    repointed += Replaced(came[i], materials, came) ? 1 : 0;
                }
            }
            for (int i = 0; i < materials.Count; i++)
            {
                if (!Draws(materials[i].shader))
                {
                    left++;
                    names = (names != null ? names + ", " : string.Empty) + materials[i].name;
                }
            }
            int named = 0;
            string[] list = file != null && read == null ? file.GetAllAssetNames() : null;   // a bundle read piece by piece: its list says whether it names any
            for (int i = 0; list != null && i < list.Length; i++)
            {
                named += BundleShaderRules.BringsShader(list[i]) ? 1 : 0;
            }
            Shader with = drawnWith != null ? Shader.Find(drawnWith) : null;
            string line = BundleShaderRules.Line(bundle, materials.Count, shaders.Count, unsupported, repointed, left, names,
                BundleShaderRules.With(drawnWith, with != null, with != null && with.isSupported, named),
                SystemInfo.graphicsDeviceType.ToString(), SystemInfo.graphicsDeviceName, SystemInfo.operatingSystem, GameBuild.Running);
            if (left == 0)
            {
                Meta.Dev.Always(logger, line);
            }
            else
            {
                logger?.Warn(line);
            }
        }

        // A material itself, or every material a model in the bundle wears.
        private static void Gather(UnityEngine.Object asset, List<Material> into)
        {
            if (asset is Material material)
            {
                if (!into.Contains(material))
                {
                    into.Add(material);
                }
            }
            else if (asset is GameObject model)
            {
                foreach (Renderer renderer in model.GetComponentsInChildren<Renderer>(true))
                {
                    foreach (Material worn in renderer.sharedMaterials)
                    {
                        if (worn != null && !into.Contains(worn))
                        {
                            into.Add(worn);
                        }
                    }
                }
            }
        }

        // Every material that came with this shader given the game's own of its name (else the game's that gives the same
        // look, where one is written down); true when none of them keeps the bundle's.
        private static bool Replaced(Shader brought, List<Material> materials, List<Shader> came)
        {
            Shader own = Own(brought.name, brought) ?? Own(BundleShaderRules.Fallback(brought.name), brought);
            if (own == null)
            {
                return false;
            }
            for (int i = 0; i < materials.Count; i++)
            {
                if (ReferenceEquals(came[i], brought))
                {
                    Point(materials[i], brought, own);
                }
            }
            return true;
        }

        // The game's shader of a name, one this graphics device runs and never the bundle's own: by name, and where the name
        // finds the bundle's own copy (the engine keeps the two under one name), among every shader in memory, once.
        private static Shader Own(string name, Shader not)
        {
            if (string.IsNullOrEmpty(name))
            {
                return null;
            }
            Shader found = Shader.Find(name);
            if (found != null && !ReferenceEquals(found, not) && Draws(found))
            {
                return found;
            }
            foreach (Shader shader in SceneFinds.InMemory<Shader>("shaders: the game's own shader of a bundle material's name looked for among those in memory (behind the loading screen)"))
            {
                if (shader != null && !ReferenceEquals(shader, not) && shader.name == name && Draws(shader))
                {
                    return shader;
                }
            }
            return null;
        }

        // The material drawn with another shader, everything else of it kept: its properties stay by name, its keywords are
        // set again and a render queue of its own is put back (one it only had from its old shader is the new shader's to give).
        private static void Point(Material m, Shader was, Shader shader)
        {
            int queue = m.renderQueue;
            bool own = queue != was.renderQueue;
            string[] keywords = m.shaderKeywords;
            m.shader = shader;
            m.shaderKeywords = keywords;
            if (own && m.renderQueue != queue)
            {
                m.renderQueue = queue;
            }
        }

        private static bool Draws(Shader shader)
        {
            return shader != null && shader.isSupported && shader.name != ErrorShader;
        }
    }
}
