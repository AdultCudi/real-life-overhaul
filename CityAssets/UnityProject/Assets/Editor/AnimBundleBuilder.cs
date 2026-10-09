using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

/// <summary>
/// Turns the clips written by the Blender scripts (build/anim/clips.json and one FBX per clip)
/// into the asset bundle of Humanoid animation clips the mod ships, then proves that they play:
///
///   Build   imports every FBX as a Humanoid model, takes its one clip (baked into the pose: the
///           clip never moves the character), and bundles the clips with a catalogue.
///   Verify  loads the bundle back the way the mod does and plays every clip through the mod's own
///           player (ActivityAnimator, HeldTool, TwoBoneReach: the same source files, copied in by
///           build_bundle.ps1) on a stand-in built from the game's own skeleton (game_rig.json):
///           where hands, feet, head and tool end up against where the clip's author put them, and the
///           second hand on the tool. With a graphics device it also draws a contact sheet per clip.
///   Prove   enters play mode and lets the engine's own frame loop do it (Assets/Test/CrossFadeProbe):
///           a character driven by an Animator Controller is cross-faded into a bundled clip, held,
///           and handed back, with a tool in both hands; then the player must be costing nothing.
///
///   Unity.exe -batchmode -quit -projectPath . -executeMethod AnimBundleBuilder.Build  [-buildFolder path] [-outFolder path]
///   Unity.exe -batchmode -quit -projectPath . -executeMethod AnimBundleBuilder.Verify [-outFolder path] [-previews path] [-rig path]
///   Unity.exe -batchmode       -projectPath . -executeMethod AnimBundleBuilder.Prove  [-outFolder path] [-rig path]      (play mode; exits by itself)
/// </summary>
public static class AnimBundleBuilder
{
    public const string BundleName = "reallife_anim";
    public const string PropBundleName = "reallife_props";
    public const string CatalogueAsset = "clips";

    private const string Generated = "Assets/Generated";
    private const string Tag = "[AnimBundle] ";
    private const float JointTolerance = 0.07f;     // metres between where a joint is and where the clip's author put it
    private const float ToolTolerance = 0.08f;
    private const float ToolTurnTolerance = 12f;    // degrees
    private const float GripTolerance = 0.012f;     // the second hand on the tool

    // ---------------------------------------------------------------- what the Blender scripts write

    [Serializable]
    public class Catalogue
    {
        public int version;
        public int fps;
        public string rig;
        public ClipRow[] clips;
    }

    [Serializable]
    public class ClipRow
    {
        public string name;
        public string file;
        public float length;
        public bool loop;
        public string note;
        public float stretch;
        public float speed;
        public string mask;
        public string watch;
        public Mark[] marks;
        public Dressing[] dressing;
        public Tool tool;
        public Sample[] samples;
    }

    [Serializable]
    public class Mark
    {
        public string name;
        public float time;
    }

    [Serializable]
    public class Dressing
    {
        public string prop;
        public float[] position;
        public float[] rotation;
    }

    [Serializable]
    public class Tool
    {
        public string prop;
        public string hand;
        public float[] position;
        public float[] rotation;
        public OtherHand otherHand;
    }

    [Serializable]
    public class OtherHand
    {
        public string hand;
        public float[] wristOnTool;
    }

    [Serializable]
    public class Sample
    {
        public float time;
        public Joints joints;
        public float[] leftHandTurn, rightHandTurn;
        public ToolPose tool;
    }

    [Serializable]
    public class Joints
    {
        public float[] Hips, Head, LeftHand, RightHand, LeftFoot, RightFoot, LeftLowerArm, RightLowerArm, LeftLowerLeg, RightLowerLeg;
    }

    [Serializable]
    public class ToolPose
    {
        public float[] position;
        public float[] rotation;
    }

    // What ships in the bundle: the same without the samples.
    [Serializable]
    public class ShippedCatalogue
    {
        public int version;
        public ShippedClip[] clips;
    }

    [Serializable]
    public class ShippedClip
    {
        public string name;
        public float length;
        public bool loop;
        public string note;
        public float speed;      // metres a second a walking loop is made for; 0 for a clip that stands
        public string mask;      // full, upper, arms, left_arm, right_arm: the part of the body it is made for
        public Mark[] marks;     // named moments in seconds: grip, release...
        public Tool tool;
    }

    [Serializable]
    public class PropCatalogue
    {
        public PropItem[] items;
    }

    [Serializable]
    public class PropItem
    {
        public string id;
        public string mesh;
    }

    // ---------------------------------------------------------------- build

    public static void Build()
    {
        Run(() =>
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string buildFolder = Path.GetFullPath(Argument("-buildFolder", Path.Combine(project, "..", "build", "anim")));
            string outFolder = OutFolder(project);
            string cataloguePath = Path.Combine(buildFolder, "clips.json");
            Check(File.Exists(cataloguePath), "no clips.json in " + buildFolder + ". Run the Blender script first.");
            Catalogue catalogue = JsonUtility.FromJson<Catalogue>(File.ReadAllText(cataloguePath));
            Check(catalogue.clips != null && catalogue.clips.Length > 0, "clips.json lists no clips");

            if (AssetDatabase.IsValidFolder(Generated))
            {
                AssetDatabase.DeleteAsset(Generated);
            }
            Directory.CreateDirectory(Generated + "/Fbx");
            Directory.CreateDirectory(Generated + "/Clips");
            AssetDatabase.Refresh();

            Check(!string.IsNullOrEmpty(catalogue.rig) && File.Exists(Path.Combine(buildFolder, catalogue.rig)), "clips.json names no rig file, or it is missing");
            string rigTarget = Generated + "/Fbx/" + catalogue.rig;
            File.Copy(Path.Combine(buildFolder, catalogue.rig), rigTarget, true);
            AssetDatabase.ImportAsset(rigTarget, ImportAssetOptions.ForceSynchronousImport);
            Avatar rigAvatar = ImportRig(rigTarget);

            var paths = new List<string>();
            var names = new List<string>();
            var seen = new HashSet<string>();
            var shipped = new List<ShippedClip>();
            float seconds = 0f;
            foreach (ClipRow row in catalogue.clips)
            {
                Check(!string.IsNullOrEmpty(row.name) && seen.Add(row.name), "clip '" + row.name + "' is listed twice or has no name");
                string source = Path.Combine(buildFolder, row.file);
                Check(File.Exists(source), "clip file missing: " + source);
                string target = Generated + "/Fbx/" + row.file;
                File.Copy(source, target, true);
                AssetDatabase.ImportAsset(target, ImportAssetOptions.ForceSynchronousImport);
                AnimationClip clip = ImportHumanoid(target, row, rigAvatar);
                Check(Mathf.Abs(clip.length - row.length) < 0.05f, row.name + ": the clip is " + clip.length.ToString("F2") + " s, the catalogue says " + row.length.ToString("F2"));

                var copy = new AnimationClip();
                EditorUtility.CopySerialized(clip, copy);
                copy.name = row.name;
                string clipPath = Generated + "/Clips/" + row.name + ".anim";
                AssetDatabase.CreateAsset(copy, clipPath);
                paths.Add(clipPath);
                names.Add(row.name);
                seconds += clip.length;
                shipped.Add(new ShippedClip { name = row.name, length = clip.length, loop = row.loop, note = row.note, speed = row.speed, mask = string.IsNullOrEmpty(row.mask) ? "full" : row.mask,
                                              marks = row.marks ?? new Mark[0], tool = HasTool(row.tool) ? row.tool : null });
            }

            string catalogueTarget = Generated + "/" + CatalogueAsset + ".json";
            File.WriteAllText(catalogueTarget, JsonUtility.ToJson(new ShippedCatalogue { version = catalogue.version, clips = shipped.ToArray() }, true));
            AssetDatabase.ImportAsset(catalogueTarget, ImportAssetOptions.ForceSynchronousImport);
            paths.Add(catalogueTarget);
            names.Add(CatalogueAsset);
            AssetDatabase.SaveAssets();

            string staging = Path.Combine(project, "BundleOutput");
            Directory.CreateDirectory(staging);
            var bundle = new AssetBundleBuild { assetBundleName = BundleName, assetNames = paths.ToArray(), addressableNames = names.ToArray() };
            AssetBundleManifest manifest = BuildPipeline.BuildAssetBundles(staging, new[] { bundle },
                BuildAssetBundleOptions.ChunkBasedCompression | BuildAssetBundleOptions.StrictMode | BuildAssetBundleOptions.ForceRebuildAssetBundle,
                BuildTarget.StandaloneWindows64);
            Check(manifest != null, "BuildAssetBundles returned nothing");
            Directory.CreateDirectory(outFolder);
            string file = Path.Combine(outFolder, BundleName);
            File.Copy(Path.Combine(staging, BundleName), file, true);
            Debug.Log(Tag + "BUILT " + file + "  " + (new FileInfo(file).Length / 1024) + " KB, " + shipped.Count + " clips, " + seconds.ToString("F1") + " s of motion");
        });
    }

    // The rig in its T-pose becomes the human Avatar, with the T-pose taken exactly as it stands: Unity's own
    // "enforce T-pose" would straighten the knees and level the arms, and the rig is deliberately the game's
    // character joint for joint, the slight bend of its knees and droop of its arms included.
    private static Avatar ImportRig(string path)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Generic;
        importer.importAnimation = false;
        importer.useFileScale = true;
        importer.globalScale = 1f;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.SaveAndReimport();
        var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
        Check(model != null, "the rig did not import");
        var skeleton = new List<SkeletonBone>();
        var present = new HashSet<string>();
        foreach (Transform bone in model.GetComponentsInChildren<Transform>(true))
        {
            skeleton.Add(new SkeletonBone { name = bone.name, position = bone.localPosition, rotation = bone.localRotation, scale = bone.localScale });
            present.Add(bone.name);
        }
        var human = new List<HumanBone>();
        foreach (string humanName in HumanTrait.BoneName)
        {
            string boneName = humanName.Replace(" ", "");
            if (present.Contains(boneName))
            {
                human.Add(new HumanBone { boneName = boneName, humanName = humanName, limit = new HumanLimit { useDefaultValues = true } });
            }
        }
        Check(human.Count >= 22, "only " + human.Count + " of the rig's bones have Humanoid names");
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
        importer.humanDescription = new HumanDescription
        {
            human = human.ToArray(), skeleton = skeleton.ToArray(), upperArmTwist = 0.5f, lowerArmTwist = 0.5f, upperLegTwist = 0.5f, lowerLegTwist = 0.5f,
            armStretch = 0.05f, legStretch = 0.05f, feetSpacing = 0f, hasTranslationDoF = false
        };
        importer.SaveAndReimport();
        Avatar avatar = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is Avatar made)
            {
                avatar = made;
            }
        }
        Check(avatar != null && avatar.isValid && avatar.isHuman, "Unity could not make a human avatar from the rig");
        Debug.Log(Tag + "rig: " + skeleton.Count + " transforms, " + human.Count + " Humanoid bones, T-pose taken as it stands");
        return avatar;
    }

    private static AnimationClip ImportHumanoid(string path, ClipRow row, Avatar rigAvatar)
    {
        var importer = (ModelImporter)AssetImporter.GetAtPath(path);
        importer.animationType = ModelImporterAnimationType.Human;
        importer.avatarSetup = ModelImporterAvatarSetup.CopyFromOther;
        importer.sourceAvatar = rigAvatar;
        importer.importAnimation = true;
        importer.useFileScale = true;
        importer.globalScale = 1f;
        importer.materialImportMode = ModelImporterMaterialImportMode.None;
        importer.animationCompression = ModelImporterAnimationCompression.KeyframeReduction;
        importer.animationRotationError = 0.25f;
        importer.animationPositionError = 0.25f;
        importer.animationScaleError = 0.5f;
        importer.SaveAndReimport();

        ModelImporterClipAnimation[] takes = importer.defaultClipAnimations;
        Check(takes != null && takes.Length > 0, row.name + ": the FBX has no animation take");
        ModelImporterClipAnimation take = takes[0];
        take.name = row.name;
        take.loopTime = row.loop;
        take.loopPose = false;
        // Everything is baked into the pose, exactly as authored: the clip never moves or turns the
        // character, and someone kneeling is low because their hips are, not because their root is.
        take.lockRootRotation = true;
        take.lockRootHeightY = true;
        take.lockRootPositionXZ = true;
        take.keepOriginalOrientation = true;
        take.keepOriginalPositionY = true;
        take.keepOriginalPositionXZ = true;
        take.heightFromFeet = false;
        importer.clipAnimations = new[] { take };
        importer.SaveAndReimport();

        AnimationClip clip = null;
        foreach (UnityEngine.Object asset in AssetDatabase.LoadAllAssetsAtPath(path))
        {
            if (asset is AnimationClip found && !found.name.StartsWith("__preview__"))
            {
                clip = found;
            }
        }
        Check(clip != null, row.name + ": no clip came out of the import");
        Check(clip.isHumanMotion, row.name + ": the clip is not a Humanoid clip");
        Check(clip.isLooping == row.loop, row.name + ": loop flag was not taken");
        return clip;
    }

    // ---------------------------------------------------------------- prove (play mode)

    /// <summary>
    /// Enters play mode, where CrossFadeProbe (Assets/Test) plays a bundled clip over an Animator Controller on
    /// the stand-in with the engine's own frame loop, and exits the editor with its verdict. Run without -quit.
    /// </summary>
    public static void Prove()
    {
        try
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string file = Path.Combine(OutFolder(project), BundleName);
            Check(File.Exists(file), "no bundle at " + file);
            string rigPath = Path.GetFullPath(Argument("-rig", Path.Combine(project, "..", "scripts", "game_rig.json")));
            Check(File.Exists(rigPath), "no game_rig.json at " + rigPath);
            // The stand-in's Animator Controller: one state, a standing clip. It stands for the game's own controller.
            AnimationClip standing = AssetDatabase.LoadAssetAtPath<AnimationClip>(Generated + "/Clips/direct_traffic.anim");
            Check(standing != null, "the proof needs " + Generated + "/Clips/direct_traffic.anim: run Build first");
            AssetDatabase.DeleteAsset(CrossFadeProbe.ControllerPath);
            UnityEditor.Animations.AnimatorController controller = UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(CrossFadeProbe.ControllerPath);
            controller.AddMotion(standing);
            AssetDatabase.SaveAssets();
            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            SessionState.SetString(CrossFadeProbe.BundleKey, file);
            SessionState.SetString(CrossFadeProbe.RigKey, rigPath);
            SessionState.SetBool(CrossFadeProbe.WantedKey, true);
            EditorSettings.enterPlayModeOptionsEnabled = false;
            EditorApplication.EnterPlaymode();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "FAILED: " + e.Message + " " + e.StackTrace);
            EditorApplication.Exit(1);
        }
    }

    // ---------------------------------------------------------------- verify

    public static void Verify()
    {
        Run(() =>
        {
            string project = Directory.GetParent(Application.dataPath).FullName;
            string outFolder = OutFolder(project);
            string file = Path.Combine(outFolder, BundleName);
            Check(File.Exists(file), "no bundle at " + file);
            string rigPath = Path.GetFullPath(Argument("-rig", Path.Combine(project, "..", "scripts", "game_rig.json")));
            Check(File.Exists(rigPath), "no game_rig.json at " + rigPath);
            string previews = Path.GetFullPath(Argument("-previews", Path.Combine(project, "..", "previews")));
            string samplesPath = Path.GetFullPath(Path.Combine(Argument("-buildFolder", Path.Combine(project, "..", "build", "anim")), "clips.json"));

            EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            AssetBundle bundle = AssetBundle.LoadFromFile(file);
            Check(bundle != null, "AssetBundle.LoadFromFile failed for " + file);
            AssetBundle props = null;
            try
            {
                var clips = new Dictionary<string, AnimationClip>();
                TextAsset catalogueAsset = null;
                foreach (UnityEngine.Object asset in bundle.LoadAllAssets())
                {
                    if (asset is AnimationClip clip)
                    {
                        clips[clip.name] = clip;
                    }
                    else if (asset is TextAsset text && text.name == CatalogueAsset)
                    {
                        catalogueAsset = text;
                    }
                    else
                    {
                        throw new Exception("unexpected asset in the bundle: " + asset.name + " (" + asset.GetType().Name + ")");
                    }
                }
                Check(catalogueAsset != null, "the bundle has no catalogue");
                ShippedCatalogue catalogue = JsonUtility.FromJson<ShippedCatalogue>(catalogueAsset.text);
                Check(catalogue.clips != null && catalogue.clips.Length == clips.Count, "the catalogue lists " + (catalogue.clips != null ? catalogue.clips.Length : 0) + " clips, the bundle holds " + clips.Count);
                var samples = new Dictionary<string, ClipRow>();
                if (File.Exists(samplesPath))
                {
                    foreach (ClipRow row in JsonUtility.FromJson<Catalogue>(File.ReadAllText(samplesPath)).clips)
                    {
                        samples[row.name] = row;
                    }
                }

                // The tools, from the prop bundle, when it is there.
                var toolMeshes = new Dictionary<string, Mesh>();
                Texture2D atlas = null;
                string propFile = Path.Combine(outFolder, PropBundleName);
                if (File.Exists(propFile) && (props = AssetBundle.LoadFromFile(propFile)) != null)
                {
                    var meshes = new Dictionary<string, Mesh>();
                    TextAsset propCatalogue = null;
                    foreach (UnityEngine.Object asset in props.LoadAllAssets())
                    {
                        if (asset is Mesh mesh) { meshes[mesh.name] = mesh; }
                        else if (asset is Texture2D texture && texture.name == "props_base") { atlas = texture; }
                        else if (asset is TextAsset text && text.name == "catalogue") { propCatalogue = text; }
                    }
                    if (propCatalogue != null)
                    {
                        foreach (PropItem item in JsonUtility.FromJson<PropCatalogue>(propCatalogue.text).items)
                        {
                            if (meshes.TryGetValue(item.mesh, out Mesh mesh)) { toolMeshes[item.id] = mesh; }
                        }
                    }
                }

                Animator animator = StandInRig.Build(File.ReadAllText(rigPath));
                Check(animator.isHuman && animator.avatar.isValid, "the stand-in built from the game's skeleton is not a valid human avatar");
                Debug.Log(Tag + "stand-in: the game's skeleton, " + animator.transform.GetComponentsInChildren<Transform>().Length + " transforms, human scale " + animator.humanScale.ToString("F3"));

                bool draw = SystemInfo.graphicsDeviceType != UnityEngine.Rendering.GraphicsDeviceType.Null;
                Stage stage = draw ? new Stage(animator, atlas) : null;
                int failures = 0;
                float seconds = 0f;
                foreach (ShippedClip row in catalogue.clips)
                {
                    Check(clips.TryGetValue(row.name, out AnimationClip clip), "clip '" + row.name + "' is not in the bundle");
                    Check(clip.isHumanMotion, row.name + " is not a Humanoid clip");
                    Check(clip.isLooping == row.loop, row.name + ": loop flag differs from the catalogue");
                    seconds += clip.length;
                    samples.TryGetValue(row.name, out ClipRow authored);
                    failures += CheckClip(animator, clip, row, authored, toolMeshes, stage, previews) ? 0 : 1;
                }
                long bytes = 0;
                foreach (AnimationClip clip in clips.Values)
                {
                    bytes += UnityEngine.Profiling.Profiler.GetRuntimeMemorySizeLong(clip);
                }
                Check(failures == 0, failures + " clip(s) do not play as authored (see above)");
                Debug.Log(Tag + "VERIFIED " + file + "  " + (new FileInfo(file).Length / 1024) + " KB on disk: " + clips.Count + " Humanoid clips, " + seconds.ToString("F1")
                          + " s of motion, " + (bytes / 1024) + " KB in memory" + (draw ? ", contact sheets in " + previews : ", no graphics device: no contact sheets"));
            }
            finally
            {
                ActivityAnimators.ReleaseAll();
                bundle.Unload(true);
                if (props != null)
                {
                    props.Unload(true);
                }
            }
        });
    }

    private static bool CheckClip(Animator animator, AnimationClip clip, ShippedClip row, ClipRow authored, Dictionary<string, Mesh> toolMeshes, Stage stage, string previews)
    {
        ActivityAnimator player = ActivityAnimators.For(animator);
        var held = new HeldTool();
        GameObject tool = null;
        ToolGrip grip = default;
        bool hasTool = HasTool(row.tool);
        if (hasTool)
        {
            grip = Grip(row.tool);
            tool = new GameObject("Tool " + row.tool.prop);
            toolMeshes.TryGetValue(row.tool.prop, out Mesh mesh);
            stage?.Dress(tool, mesh);
            Check(held.Attach(animator, tool.transform, grip), row.name + ": the tool could not be put in the hand");
        }

        float worstJoint = 0f, worstTool = 0f, worstTurn = 0f, worstGrip = 0f, worstHand = 0f;
        string worstName = "";
        if (authored != null && authored.samples != null)
        {
            foreach (Sample sample in authored.samples)
            {
                // At a moment when the clip's author has the tool lying somewhere (not yet picked up, already
                // put down) the hands are measured empty, as they are in the game once the tool is detached.
                bool inHand = hasTool && sample.tool != null && sample.tool.position != null && sample.tool.position.Length == 3;
                if (hasTool && inHand != held.Held)
                {
                    if (inHand)
                    {
                        held.Attach(animator, tool.transform, grip);
                    }
                    else
                    {
                        held.Detach(null);
                    }
                }
                Pose(player, clip, sample.time);
                Joints j = sample.joints;
                Measure(animator, HumanBodyBones.Hips, j.Hips, "hips", ref worstJoint, ref worstName);
                Measure(animator, HumanBodyBones.Head, j.Head, "head", ref worstJoint, ref worstName);
                Measure(animator, HumanBodyBones.LeftFoot, j.LeftFoot, "left foot", ref worstJoint, ref worstName);
                Measure(animator, HumanBodyBones.RightFoot, j.RightFoot, "right foot", ref worstJoint, ref worstName);
                Measure(animator, HumanBodyBones.LeftLowerLeg, j.LeftLowerLeg, "left knee", ref worstJoint, ref worstName);
                Measure(animator, HumanBodyBones.RightLowerLeg, j.RightLowerLeg, "right knee", ref worstJoint, ref worstName);
                Measure(animator, HumanBodyBones.LeftHand, j.LeftHand, "left hand", ref worstJoint, ref worstName);
                Measure(animator, HumanBodyBones.RightHand, j.RightHand, "right hand", ref worstJoint, ref worstName);
                if (sample.leftHandTurn != null && sample.leftHandTurn.Length == 4)
                {
                    worstHand = Mathf.Max(worstHand, Quaternion.Angle(animator.GetBoneTransform(HumanBodyBones.LeftHand).rotation, Q(sample.leftHandTurn) * StandInRig.LeftHandTurn));
                    worstHand = Mathf.Max(worstHand, Quaternion.Angle(animator.GetBoneTransform(HumanBodyBones.RightHand).rotation, Q(sample.rightHandTurn) * StandInRig.RightHandTurn));
                }
                if (inHand)
                {
                    worstTool = Mathf.Max(worstTool, (tool.transform.position - V(sample.tool.position)).magnitude);
                    worstTurn = Mathf.Max(worstTurn, Quaternion.Angle(tool.transform.rotation, Q(sample.tool.rotation)));
                    if (grip.TwoHanded)
                    {
                        Transform wrist = animator.GetBoneTransform(grip.OtherHand);
                        worstGrip = Mathf.Max(worstGrip, (wrist.position - tool.transform.TransformPoint(grip.WristOnTool)).magnitude);
                    }
                }
            }
        }
        bool good = worstJoint <= JointTolerance && worstTool <= ToolTolerance && worstTurn <= ToolTurnTolerance && worstGrip <= GripTolerance;
        Debug.Log(Tag + (good ? "  ok   " : "  BAD  ") + row.name.PadRight(18) + clip.length.ToString("F2") + " s " + (row.loop ? "loop" : "once") + "  joints within " + (worstJoint * 100f).ToString("F1")
                  + " cm (" + worstName + "), hands turned within " + worstHand.ToString("F1") + " deg" + (hasTool ? ", " + row.tool.prop + " within " + (worstTool * 100f).ToString("F1") + " cm and " + worstTurn.ToString("F1") + " deg"
                  + (grip.TwoHanded ? ", other hand " + (worstGrip * 1000f).ToString("F1") + " mm off its grip" : "") : "") + (authored == null ? "  (no authored samples to compare with)" : ""));

        if (stage != null)
        {
            // What stands about the figure on the sheet (a stretcher, a ladder): never shipped, only drawn.
            var dressing = new List<GameObject>();
            if (authored != null && authored.dressing != null)
            {
                foreach (Dressing item in authored.dressing)
                {
                    if (item.position == null || item.position.Length != 3 || !toolMeshes.TryGetValue(item.prop, out Mesh dressed))
                    {
                        continue;
                    }
                    var placed = new GameObject("Dressing " + item.prop);
                    placed.transform.SetPositionAndRotation(V(item.position), Q(item.rotation));
                    stage.Dress(placed, dressed);
                    dressing.Add(placed);
                }
            }
            // A tool that is picked up or put down part of the way through is drawn where it lies before and after.
            float gripAt = Marked(row, "grip"), releaseAt = Marked(row, "release");
            if (row.name == "cast_rod") releaseAt = -1f;     // its "release" is the line going out: the rod stays in the hand
            Action<float> pose = time =>
            {
                float holdAt = gripAt >= 0f && time < gripAt ? gripAt : releaseAt >= 0f && time > releaseAt ? releaseAt : -1f;
                if (tool == null || holdAt < 0f)
                {
                    if (tool != null && !held.Held)
                    {
                        held.Attach(animator, tool.transform, grip);
                    }
                    Pose(player, clip, time);
                    return;
                }
                held.Attach(animator, tool.transform, grip);
                Pose(player, clip, holdAt);
                Vector3 lies = tool.transform.position;
                Quaternion turned = tool.transform.rotation;
                held.Detach(null);
                tool.transform.SetPositionAndRotation(lies, turned);
                Pose(player, clip, time);
            };
            bool leftHand = authored != null && !string.IsNullOrEmpty(authored.watch) ? authored.watch == "L" : hasTool && grip.Hand == HumanBodyBones.LeftHand;
            stage.Sheet(clip, row.loop, pose, Path.Combine(previews, "unity_" + row.name + ".jpg"), animator.GetBoneTransform(leftHand ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand), leftHand);
            foreach (GameObject placed in dressing)
            {
                UnityEngine.Object.DestroyImmediate(placed);
            }
        }
        held.Detach(null);
        if (tool != null)
        {
            UnityEngine.Object.DestroyImmediate(tool);
        }
        player.Dispose();
        return good;
    }

    // The stand-in at one moment of a clip, through the mod's own player, with the second hand put on the tool.
    private static void Pose(ActivityAnimator player, AnimationClip clip, float time)
    {
        player.Dispose();
        player.Play(clip, 0f, 1f, ClipEnd.Hold, time);
        ActivityAnimators.StepAll(0.001f);
        player.Evaluate(0f);
        ActivityAnimators.ReachAll(10f);
    }

    private static void Measure(Animator animator, HumanBodyBones bone, float[] expected, string name, ref float worst, ref string worstName)
    {
        if (expected == null || expected.Length != 3)
        {
            return;
        }
        float off = (animator.GetBoneTransform(bone).position - V(expected)).magnitude;
        if (off > worst)
        {
            worst = off;
            worstName = name;
        }
    }

    private static float Marked(ShippedClip row, string name)
    {
        if (row.marks != null)
        {
            foreach (Mark mark in row.marks)
            {
                if (mark.name == name)
                {
                    return mark.time;
                }
            }
        }
        return -1f;
    }

    private static bool HasTool(Tool tool)
    {
        return tool != null && !string.IsNullOrEmpty(tool.prop) && tool.position != null && tool.position.Length == 3;
    }

    private static ToolGrip Grip(Tool tool)
    {
        var grip = new ToolGrip
        {
            Hand = tool.hand == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand,
            Position = V(tool.position),
            Rotation = Q(tool.rotation)
        };
        if (tool.otherHand != null && tool.otherHand.wristOnTool != null && tool.otherHand.wristOnTool.Length == 3)
        {
            grip.TwoHanded = true;
            grip.OtherHand = tool.otherHand.hand == "Left" ? HumanBodyBones.LeftHand : HumanBodyBones.RightHand;
            grip.WristOnTool = V(tool.otherHand.wristOnTool);
        }
        return grip;
    }

    private static Vector3 V(float[] v) => new Vector3(v[0], v[1], v[2]);

    private static Quaternion Q(float[] q) => new Quaternion(q[0], q[1], q[2], q[3]);

    // ---------------------------------------------------------------- contact sheets

    private sealed class Stage
    {
        private const int Tiles = 8;
        private const int TileWidth = 200;
        private const int TileHeight = 320;
        private const int HandHeight = 200;       // the third row: the hand that holds the tool, close up

        private readonly Camera _camera;
        private readonly Material _grey, _right, _left, _skin, _tool;
        private readonly Mesh _cube, _sphere;
        private readonly Transform _root;

        public Stage(Animator animator, Texture2D atlas)
        {
            _root = animator.transform;
            _cube = Resources.GetBuiltinResource<Mesh>("Cube.fbx");
            _sphere = Resources.GetBuiltinResource<Mesh>("Sphere.fbx");
            Shader shader = Shader.Find("Standard");
            _grey = Colour(shader, new Color(0.55f, 0.57f, 0.62f));
            _right = Colour(shader, new Color(0.90f, 0.42f, 0.10f));
            _left = Colour(shader, new Color(0.16f, 0.38f, 0.80f));
            _skin = Colour(shader, new Color(0.82f, 0.66f, 0.55f));
            _tool = Colour(shader, atlas != null ? Color.white : new Color(0.75f, 0.70f, 0.20f));
            _tool.mainTexture = atlas;

            foreach (Transform bone in animator.GetComponentsInChildren<Transform>())
            {
                string name = bone.name;
                if (bone == _root || name == "Armature")
                {
                    continue;
                }
                if (name.Contains("Finger"))
                {
                    // The game's hand: a thumb, an index finger and one chain for the other three.
                    Transform next = null;
                    for (int i = 0; i < bone.childCount; i++)
                    {
                        if (bone.GetChild(i).name.Contains("Finger"))
                        {
                            next = bone.GetChild(i);
                        }
                    }
                    Vector3 along = next != null ? next.localPosition : bone.localPosition.normalized * 0.022f;
                    float wide = name.StartsWith("Ring") ? 0.046f : 0.017f;
                    Part(bone, _cube, name.StartsWith("Thumb") ? (name.Contains(".R") ? _right : _left) : _skin, along * 0.5f, Quaternion.FromToRotation(Vector3.up, along),
                         new Vector3(0.016f, along.magnitude, wide));
                    continue;
                }
                Material material = name.EndsWith(".R") ? _right : name.EndsWith(".L") ? _left : _grey;
                float thick = name.StartsWith("UpperLeg") ? 0.13f : name.StartsWith("LowerLeg") ? 0.10f : name.StartsWith("UpperArm") ? 0.09f : name.StartsWith("LowerArm") ? 0.075f
                    : name.StartsWith("Shoulder") ? 0.07f : 0.2f;
                if (name == "Skull")
                {
                    Part(bone, _sphere, _skin, new Vector3(0f, 0.10f, 0.02f), Quaternion.identity, new Vector3(0.19f, 0.24f, 0.22f));
                    Part(bone, _cube, _grey, new Vector3(0f, 0.08f, 0.12f), Quaternion.identity, new Vector3(0.03f, 0.03f, 0.04f));
                    continue;
                }
                if (name.StartsWith("Hand"))
                {
                    Part(bone, _cube, _skin, new Vector3(0f, 0.042f, 0f), Quaternion.identity, new Vector3(0.026f, 0.084f, 0.08f));
                    continue;
                }
                if (name.StartsWith("ToesFoot"))
                {
                    continue;
                }
                Transform child = null;
                for (int i = 0; i < bone.childCount; i++)
                {
                    Transform candidate = bone.GetChild(i);
                    if (!candidate.name.Contains("Finger") && !candidate.name.StartsWith("UpperLeg") && !candidate.name.StartsWith("Shoulder") && candidate.GetComponent<MeshRenderer>() == null)
                    {
                        child = candidate;
                    }
                }
                if (child == null)
                {
                    continue;
                }
                Vector3 to = child.localPosition;
                Vector3 size = name.StartsWith("Foot") ? new Vector3(0.10f, to.magnitude * 1.5f, 0.07f)
                    : name == "Hips" || name == "Spine" || name == "Chest" ? new Vector3(0.30f, to.magnitude, 0.19f)
                    : name == "UpperChest" ? new Vector3(0.34f, to.magnitude, 0.20f) : name == "Neck" ? new Vector3(0.08f, to.magnitude, 0.08f) : new Vector3(thick, to.magnitude, thick);
                Part(bone, _cube, material, to * 0.5f, Quaternion.FromToRotation(Vector3.up, to), size);
            }

            var light = new GameObject("Sun").AddComponent<Light>();
            light.type = LightType.Directional;
            light.intensity = 1.1f;
            light.transform.rotation = Quaternion.Euler(48f, -30f, 0f);
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = new Color(0.55f, 0.57f, 0.60f);
            var ground = new GameObject("Ground");
            ground.AddComponent<MeshFilter>().sharedMesh = _cube;
            ground.AddComponent<MeshRenderer>().sharedMaterial = Colour(shader, new Color(0.30f, 0.31f, 0.33f));
            ground.transform.position = new Vector3(0f, -0.05f, 0f);
            ground.transform.localScale = new Vector3(30f, 0.1f, 30f);
            _camera = new GameObject("Camera").AddComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = new Color(0.62f, 0.67f, 0.74f);
            _camera.fieldOfView = 24f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
            _camera.enabled = false;
        }

        public void Dress(GameObject tool, Mesh mesh)
        {
            if (mesh == null)
            {
                Part(tool.transform, _cube, _tool, new Vector3(0f, 0.6f, 0f), Quaternion.identity, new Vector3(0.03f, 1.2f, 0.03f));
                return;
            }
            tool.AddComponent<MeshFilter>().sharedMesh = mesh;
            tool.AddComponent<MeshRenderer>().sharedMaterial = _tool;
        }

        // Eight moments of the clip in one picture: front quarter above, side below, and under those the hand
        // that holds the tool (or the right hand) close up, fingers drawn.
        public void Sheet(AnimationClip clip, bool loop, Action<float> pose, string path, Transform hand, bool leftHand)
        {
            var sheet = new Texture2D(TileWidth * Tiles, TileHeight * 2 + HandHeight, TextureFormat.RGB24, false);
            var target = new RenderTexture(TileWidth, TileHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            var close = new RenderTexture(TileWidth, HandHeight, 24, RenderTextureFormat.ARGB32) { antiAliasing = 4 };
            for (int k = 0; k < Tiles; k++)
            {
                pose(clip.length * (loop ? k / (float)Tiles : k / (float)(Tiles - 1)));
                _camera.fieldOfView = 24f;
                _camera.targetTexture = target;
                for (int row = 0; row < 2; row++)
                {
                    Vector3 centre = _root.position + new Vector3(0f, 0.9f, 0.25f);
                    Quaternion view = Quaternion.Euler(row == 0 ? 10f : 4f, row == 0 ? 180f + 28f : 270f, 0f);     // from the front right; from the character's right
                    _camera.transform.position = centre - view * Vector3.forward * 6.6f;
                    _camera.transform.rotation = view;
                    _camera.Render();
                    RenderTexture.active = target;
                    sheet.ReadPixels(new Rect(0, 0, TileWidth, TileHeight), k * TileWidth, HandHeight + (1 - row) * TileHeight);
                    RenderTexture.active = null;
                }
                if (hand != null)
                {
                    Quaternion view = Quaternion.Euler(22f, leftHand ? 180f - 40f : 180f + 40f, 0f);
                    _camera.fieldOfView = 16f;
                    _camera.targetTexture = close;
                    _camera.transform.position = hand.position + hand.up * 0.06f - view * Vector3.forward * 1.25f;
                    _camera.transform.rotation = view;
                    _camera.Render();
                    RenderTexture.active = close;
                    sheet.ReadPixels(new Rect(0, 0, TileWidth, HandHeight), k * TileWidth, 0);
                    RenderTexture.active = null;
                }
            }
            sheet.Apply();
            _camera.targetTexture = null;
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllBytes(path, sheet.EncodeToJPG(84));
            UnityEngine.Object.DestroyImmediate(target);
            UnityEngine.Object.DestroyImmediate(close);
            UnityEngine.Object.DestroyImmediate(sheet);
        }

        private static Material Colour(Shader shader, Color colour)
        {
            var material = new Material(shader) { color = colour };
            material.SetFloat("_Glossiness", 0.2f);
            return material;
        }

        private static void Part(Transform parent, Mesh mesh, Material material, Vector3 position, Quaternion rotation, Vector3 scale)
        {
            var part = new GameObject("Part");
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localRotation = rotation;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            part.AddComponent<MeshRenderer>().sharedMaterial = material;
        }
    }

    // ---------------------------------------------------------------- shared

    private static void Check(bool condition, string problem)
    {
        if (!condition)
        {
            throw new Exception(problem);
        }
    }

    private static string OutFolder(string project)
    {
        return Path.GetFullPath(Argument("-outFolder", Path.Combine(project, "..", "..", "Assets", "Mods", "RealLifeOverhaul", "Bundles")));
    }

    private static string Argument(string name, string fallback)
    {
        string[] arguments = Environment.GetCommandLineArgs();
        for (int i = 0; i < arguments.Length - 1; i++)
        {
            if (arguments[i] == name)
            {
                return arguments[i + 1];
            }
        }
        return fallback;
    }

    // Batch mode: a failure must end the process with a non-zero exit code, not leave the editor open.
    private static void Run(Action work)
    {
        try
        {
            work();
        }
        catch (Exception e)
        {
            Debug.LogError(Tag + "FAILED: " + e.Message + "\n" + e.StackTrace);
            if (Application.isBatchMode)
            {
                EditorApplication.Exit(1);
            }
            return;
        }
        if (Application.isBatchMode)
        {
            EditorApplication.Exit(0);
        }
    }
}
