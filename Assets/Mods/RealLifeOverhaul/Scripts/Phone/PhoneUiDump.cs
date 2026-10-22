using System.IO;
using System.Text;
using BAModAPI;
using TMPro;
using UI;
using UnityEngine;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A developer aid: F8 writes the structure of the game's phone UI (the HUD phone and the full menu), the characters'
    /// rigs and a screenshot to the game's data folder. A frame costs one key check.
    /// </summary>
    public class PhoneUiDump : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("diagnostics", "PhoneUiDump", typeof(PhoneUiDump));

        private const int MaxDepth = 7;           // levels below each root
        private const int MaxLinesPerRoot = 900;  // lines written per root

        public static IModLogger Logger;

        private static readonly int HotUpdate = HotSpots.Slot("Phone: PhoneUiDump.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (System.Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            if (!Keys.Down(KeyAction.PhoneDump))
            {
                return;
            }
            var uis = InstanceBehavior<UIs>.Instance;
            if (uis == null)
            {
                return;
            }

            var text = new StringBuilder();
            text.AppendLine($"Screen {Screen.width}x{Screen.height}");
            Dump(text, "HUD PHONE (smartphoneUI)", uis.smartphoneUI != null ? uis.smartphoneUI.transform : null);
            Dump(text, "FULL MENU (fullMenu)", uis.fullMenu != null ? uis.fullMenu.transform : null);

            string path = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul_phone_ui.txt");
            File.WriteAllText(path, text.ToString());

            try
            {
                File.WriteAllText(Path.Combine(Application.persistentDataPath, "RealLifeOverhaul_rig.txt"), DescribeCharacters());
            }
            catch (System.Exception e)
            {
                Logger?.Error(e);
            }

            string folder = Path.Combine(Application.persistentDataPath, "RealLifeOverhaul_shots");
            Directory.CreateDirectory(folder);
            string shot = Path.Combine(folder, $"shot_{System.DateTime.Now:HHmmss}.png");
            ScreenCapture.CaptureScreenshot(shot);
            Logger?.Info($"Phone UI structure written to {path}; screenshot {shot}");
        }

        // The rigs of the player and up to three pedestrians within 45 metres.
        private static string DescribeCharacters()
        {
            var text = new StringBuilder();
            var gm = InstanceBehavior<GameManager>.Instance;
            Vector3 origin = gm != null && gm.playerController != null ? gm.playerController.transform.position : Vector3.zero;

            int described = 0;
            foreach (ThirdPersonCharacter character in FindObjectsOfType<ThirdPersonCharacter>())
            {
                float distance = Vector3.Distance(character.transform.position, origin);
                if (!character.isPlayer && (distance > 45f || described >= 3))
                {
                    continue;
                }
                if (!character.isPlayer)
                {
                    described++;
                }

                text.AppendLine($"===== {(character.isPlayer ? "PLAYER" : "NPC")} '{character.name}' at {distance:F0} m, layer {LayerMask.LayerToName(character.gameObject.layer)} =====");
                text.Append("root components:");
                foreach (Component component in character.GetComponents<Component>())
                {
                    if (component != null)
                    {
                        text.Append(' ').Append(component.GetType().Name);
                    }
                }
                text.AppendLine();
                Transform parent = character.transform.parent;
                text.AppendLine("parent: " + (parent != null ? parent.name : "none"));

                Animator animator = character.animator;
                if (animator == null)
                {
                    text.AppendLine("animator: none");
                    continue;
                }
                text.AppendLine($"animator on '{animator.name}': isHuman={animator.isHuman} avatar={(animator.avatar != null ? animator.avatar.name : "none")} "
                                + $"culling={animator.cullingMode} updateMode={animator.updateMode} applyRootMotion={animator.applyRootMotion}");
                if (animator.isHuman)
                {
                    foreach (HumanBodyBones bone in new[]
                             {
                                 HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.Head,
                                 HumanBodyBones.LeftUpperArm, HumanBodyBones.LeftLowerArm, HumanBodyBones.RightUpperArm, HumanBodyBones.RightLowerArm,
                                 HumanBodyBones.LeftUpperLeg, HumanBodyBones.LeftLowerLeg, HumanBodyBones.LeftFoot,
                                 HumanBodyBones.RightUpperLeg, HumanBodyBones.RightLowerLeg, HumanBodyBones.RightFoot
                             })
                    {
                        Transform t = animator.GetBoneTransform(bone);
                        text.AppendLine($"  {bone}: {(t != null ? t.name + " localPos " + t.localPosition.ToString("F3") + " lossyScale " + t.lossyScale.x.ToString("F2") : "MISSING")}");
                    }
                }

                text.AppendLine($"rigidbodies in children: {character.GetComponentsInChildren<Rigidbody>(true).Length}, colliders: {character.GetComponentsInChildren<Collider>(true).Length}, "
                                + $"joints: {character.GetComponentsInChildren<Joint>(true).Length}");
                foreach (Collider collider in character.GetComponentsInChildren<Collider>(true))
                {
                    text.AppendLine($"  collider {collider.GetType().Name} on '{collider.name}' layer {LayerMask.LayerToName(collider.gameObject.layer)} "
                                    + $"trigger={collider.isTrigger} enabled={collider.enabled}");
                }
                foreach (Rigidbody body in character.GetComponentsInChildren<Rigidbody>(true))
                {
                    text.AppendLine($"  rigidbody on '{body.name}' kinematic={body.isKinematic} mass={body.mass}");
                }
                foreach (SkinnedMeshRenderer skin in character.GetComponentsInChildren<SkinnedMeshRenderer>(false))
                {
                    text.AppendLine($"  skinned mesh '{skin.name}' bones={skin.bones.Length} rootBone={(skin.rootBone != null ? skin.rootBone.name : "none")} "
                                    + $"updateWhenOffscreen={skin.updateWhenOffscreen}");
                }

                Transform skeleton = animator.isHuman ? animator.GetBoneTransform(HumanBodyBones.Hips) : null;
                if (skeleton != null && skeleton.parent != null)
                {
                    int lines = 0;
                    Walk(text, skeleton.parent, 0, ref lines);
                }
            }
            return text.ToString();
        }

        private static void Dump(StringBuilder text, string title, Transform root)
        {
            text.AppendLine();
            text.AppendLine("===== " + title + " =====");
            if (root == null)
            {
                text.AppendLine("(not found)");
                return;
            }

            // Parents first, so the canvas and scaling the phone sits under are visible too.
            var chain = new StringBuilder();
            for (Transform t = root.parent; t != null; t = t.parent)
            {
                chain.Insert(0, t.name + Describe(t) + " > ");
            }
            text.AppendLine("parents: " + chain);

            int lines = 0;
            Walk(text, root, 0, ref lines);
        }

        private static void Walk(StringBuilder text, Transform t, int depth, ref int lines)
        {
            if (lines++ >= MaxLinesPerRoot)
            {
                return;
            }
            text.Append(' ', depth * 2).Append(t.gameObject.activeSelf ? "" : "(off) ").Append(t.name).AppendLine(Describe(t));
            if (depth >= MaxDepth)
            {
                if (t.childCount > 0)
                {
                    text.Append(' ', depth * 2 + 2).AppendLine($"... {t.childCount} children not shown");
                }
                return;
            }
            for (int i = 0; i < t.childCount; i++)
            {
                Walk(text, t.GetChild(i), depth + 1, ref lines);
            }
        }

        private static string Describe(Transform t)
        {
            var info = new StringBuilder();
            if (t is RectTransform rect)
            {
                info.Append($"  [anchor {rect.anchorMin}-{rect.anchorMax} pivot {rect.pivot} pos {rect.anchoredPosition} size {rect.rect.size} scale {rect.localScale.x:F2}]");
            }
            foreach (Component component in t.GetComponents<Component>())
            {
                switch (component)
                {
                    case null:
                    case Transform _:
                    case CanvasRenderer _:
                        break;
                    case Canvas canvas:
                        info.Append($"  Canvas(mode={canvas.renderMode} order={canvas.sortingOrder} override={canvas.overrideSorting})");
                        break;
                    case CanvasScaler scaler:
                        info.Append($"  CanvasScaler(mode={scaler.uiScaleMode} ref={scaler.referenceResolution} match={scaler.matchWidthOrHeight})");
                        break;
                    case Image image:
                        info.Append($"  Image(sprite={(image.sprite != null ? image.sprite.name : "none")} type={image.type} color={ColorUtility.ToHtmlStringRGBA(image.color)})");
                        break;
                    case RawImage raw:
                        info.Append($"  RawImage(texture={(raw.texture != null ? raw.texture.name : "none")})");
                        break;
                    case TMP_Text label:
                        string value = label.text ?? "";
                        info.Append($"  Text(\"{(value.Length > 28 ? value.Substring(0, 28) + "..." : value)}\" size={label.fontSize} color={ColorUtility.ToHtmlStringRGBA(label.color)})");
                        break;
                    case Mask _:
                        info.Append("  Mask");
                        break;
                    case RectMask2D _:
                        info.Append("  RectMask2D");
                        break;
                    case LayoutGroup layout:
                        info.Append($"  {layout.GetType().Name}(padding={layout.padding.left},{layout.padding.right},{layout.padding.top},{layout.padding.bottom})");
                        break;
                    default:
                        info.Append("  " + component.GetType().Name);
                        break;
                }
            }
            return info.ToString();
        }
    }
}
