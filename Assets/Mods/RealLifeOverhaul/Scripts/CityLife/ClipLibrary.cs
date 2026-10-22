using System;
using System.Collections.Generic;
using System.IO;
using BAModAPI;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>One of the mod's animation clips, with the tool it was made for, if any, and how the hand holds it.</summary>
    public sealed class CityClip
    {
        public string Name;
        public AnimationClip Clip;
        public float Length;
        public bool Loop;
        public string Prop;        // a PropLibrary id, or null
        public ToolGrip Grip;
        public float Speed;        // metres a second a walking loop is made for (ActivityAnimator.MatchSpeed); 0 for one that stands
        public BodyMask Mask;      // anything but Full is for ActivityAnimator.PlayOver
        public KeyValuePair<string, float>[] Marks;   // named moments in seconds ("grip", "release"...); never null

        public bool HasTool => Prop != null;

        /// <summary>A named moment of the clip in seconds (the hand closing, letting go), or the fallback.</summary>
        public float Mark(string name, float fallback = -1f)
        {
            for (int i = 0; i < Marks.Length; i++)
            {
                if (Marks[i].Key == name)
                {
                    return Marks[i].Value;
                }
            }
            return fallback;
        }
    }

    /// <summary>
    /// The mod's own Humanoid clips by name (listed in CityAssets/README.md), from the Bundles/reallife_anim bundle, read
    /// once a session in the background and kept (about 1 MB). Nothing here runs per frame.
    /// <code>
    /// if (ClipLibrary.TryGet("kneel_work", out CityClip clip))
    /// {
    ///     ActivityAnimators.For(character.animator).Play(clip.Clip);   // and .Stop() to hand back
    /// }
    /// </code>
    /// TryGet says no while loading, without the bundle, or for a clip it lacks (logged once); the caller then leaves the
    /// character to the game's animations.
    /// </summary>
    public static class ClipLibrary
    {
        private const string BundlePath = "Bundles/reallife_anim";
        private const string CatalogueName = "clips";
        private const int SupportedVersion = 1;

#pragma warning disable 0649   // filled in from the catalogue file
        private sealed class CatalogueFile
        {
            public int version;
            public ClipFile[] clips;
        }

        private sealed class ClipFile
        {
            public string name;
            public float length;
            public bool loop;
            public float speed;
            public string mask;
            public MarkFile[] marks;
            public ToolFile tool;
        }

        private sealed class MarkFile
        {
            public string name;
            public float time;
        }

        private sealed class ToolFile
        {
            public string prop;
            public string hand;
            public float[] position;
            public float[] rotation;
            public OtherHandFile otherHand;
        }

        private sealed class OtherHandFile
        {
            public string hand;
            public float[] wristOnTool;
        }
#pragma warning restore 0649

        private enum State { NotStarted, Loading, Ready, Failed }

        public static IModLogger Logger;

        private static readonly Dictionary<string, CityClip> Clips = new Dictionary<string, CityClip>(96);
        private static readonly HashSet<string> MissingLogged = new HashSet<string>();
        private static readonly HashSet<string> PlayedLogged = new HashSet<string>();
        private static State _state = State.NotStarted;

        /// <summary>True once the bundle has loaded; false while loading or after a failure.</summary>
        public static bool Ready => _state == State.Ready;

        public static int Count => Clips.Count;

        /// <summary>Starts the one asynchronous load. Safe to call again.</summary>
        public static void Load(ModContext context)
        {
            if (_state != State.NotStarted || context == null)
            {
                return;
            }
            try
            {
                string path = Path.Combine(context.ModRootPath, BundlePath);
                if (!File.Exists(path))
                {
                    Fail("the asset bundle is not installed (" + path + ")");
                    return;
                }
                _state = State.Loading;
                AssetBundleCreateRequest opening = AssetBundle.LoadFromFileAsync(path);
                opening.completed += Guard.Way<AsyncOperation>(_ => Opened(opening));
            }
            catch (Exception e)
            {
                Fail("the asset bundle could not be opened", e);
            }
        }

        /// <summary>Whether there is such a clip, with nothing logged if not; false while loading.</summary>
        public static bool Has(string name)
        {
            return _state == State.Ready && name != null && Clips.ContainsKey(name);
        }

        /// <summary>
        /// Logs once per clip and player, with the verbose log on, that the clip is playing on <paramref name="by"/> ("the
        /// player", "a street character"). Called after the clip has started.
        /// </summary>
        public static void Played(string name, string by)
        {
            if (Meta.Dev.Verbose && PlayedLogged.Add(name + "|" + by))
            {
                Logger?.Info("Clips: the '" + name + "' clip is played by " + by + " (said the first time only).");
            }
        }

        /// <summary>A clip by name; false if the bundle is not ready or lacks it.</summary>
        public static bool TryGet(string name, out CityClip clip)
        {
            clip = null;
            if (_state != State.Ready || name == null)
            {
                return false;
            }
            if (Clips.TryGetValue(name, out clip))
            {
                return true;
            }
            if (MissingLogged.Add(name))
            {
                Logger?.Warn("Clips: '" + name + "' is not in the asset bundle; whoever wanted it does without.");
            }
            return false;
        }

        private static void Opened(AssetBundleCreateRequest opening)
        {
            try
            {
                AssetBundle bundle = opening.assetBundle;
                if (bundle == null)
                {
                    Fail("the asset bundle could not be read (built for another Unity version?)");
                    return;
                }
                AssetBundleRequest reading = bundle.LoadAllAssetsAsync();
                reading.completed += Guard.Way<AsyncOperation>(_ => Loaded(reading));
            }
            catch (Exception e)
            {
                Fail("the asset bundle could not be read", e);
            }
        }

        private static void Loaded(AssetBundleRequest reading)
        {
            try
            {
                var found = new Dictionary<string, AnimationClip>(32);
                TextAsset catalogueText = null;
                foreach (UnityEngine.Object asset in reading.allAssets)
                {
                    if (asset is AnimationClip clip)
                    {
                        found[clip.name] = clip;
                    }
                    else if (asset is TextAsset text && text.name == CatalogueName)
                    {
                        catalogueText = text;
                    }
                }
                if (catalogueText == null)
                {
                    Fail("the asset bundle has no catalogue");
                    return;
                }
                // Not JsonUtility: Unity's serializer does not know classes from an assembly loaded at run time.
                CatalogueFile catalogue = Newtonsoft.Json.JsonConvert.DeserializeObject<CatalogueFile>(catalogueText.text);
                if (catalogue == null || catalogue.version != SupportedVersion || catalogue.clips == null)
                {
                    Fail("the clip catalogue is version " + (catalogue != null ? catalogue.version : 0) + "; this build reads version " + SupportedVersion);
                    return;
                }
                float seconds = 0f;
                foreach (ClipFile file in catalogue.clips)
                {
                    if (string.IsNullOrEmpty(file.name) || !found.TryGetValue(file.name, out AnimationClip clip) || !clip.isHumanMotion)
                    {
                        Logger?.Warn("Clips: '" + file.name + "' is left out: it is not in the bundle as a Humanoid clip.");
                        continue;
                    }
                    var made = new CityClip { Name = file.name, Clip = clip, Length = clip.length, Loop = clip.isLooping, Speed = file.speed, Mask = MaskOf(file.mask) };
                    int marks = file.marks != null ? file.marks.Length : 0;
                    made.Marks = new KeyValuePair<string, float>[marks];
                    for (int i = 0; i < marks; i++)
                    {
                        made.Marks[i] = new KeyValuePair<string, float>(file.marks[i].name, file.marks[i].time);
                    }
                    ToolFile tool = file.tool;
                    if (tool != null && !string.IsNullOrEmpty(tool.prop) && Three(tool.position) && tool.rotation != null && tool.rotation.Length == 4)
                    {
                        made.Prop = tool.prop;
                        made.Grip = new ToolGrip
                        {
                            Hand = tool.hand == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
                            Position = new Vector3(tool.position[0], tool.position[1], tool.position[2]),
                            Rotation = new Quaternion(tool.rotation[0], tool.rotation[1], tool.rotation[2], tool.rotation[3])
                        };
                        if (tool.otherHand != null && Three(tool.otherHand.wristOnTool))
                        {
                            made.Grip.TwoHanded = true;
                            made.Grip.OtherHand = tool.otherHand.hand == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
                            made.Grip.WristOnTool = new Vector3(tool.otherHand.wristOnTool[0], tool.otherHand.wristOnTool[1], tool.otherHand.wristOnTool[2]);
                        }
                    }
                    Clips[made.Name] = made;
                    seconds += made.Length;
                }
                _state = State.Ready;
                Logger?.Info("Clips: " + Clips.Count + " Humanoid clips loaded from the asset bundle (" + seconds.ToString("F0") + " s of motion).");
            }
            catch (Exception e)
            {
                Fail("the clip catalogue could not be read", e);
            }
        }

        private static BodyMask MaskOf(string name)
        {
            switch (name)
            {
                case "upper": return BodyMask.Upper;
                case "arms": return BodyMask.Arms;
                case "left_arm": return BodyMask.LeftArm;
                case "right_arm": return BodyMask.RightArm;
                default: return BodyMask.Full;
            }
        }

        private static bool Three(float[] values)
        {
            return values != null && values.Length == 3;
        }

        private static void Fail(string problem, Exception error = null)
        {
            _state = State.Failed;
            Logger?.Warn("Clips: switched off, " + problem + ". People keep to the game's own animations.");
            if (error != null)
            {
                Logger?.Error(error);
            }
        }
    }
}
