using System.Collections.Generic;
using Helpers;
using TMPro;
using UnityEngine;
using UnityEngine.Rendering;

namespace RealLifeOverhaul
{
    /// <summary>Measuring the drawn body: the trunk and the head in their own frames, in one pass over the mesh.</summary>
    internal sealed partial class ServiceKit
    {
        private const byte Other = 0;

        private const byte Torso = 1;

        private const byte Skull = 2;

        private static readonly HumanBodyBones[] TorsoBones =
        {
            HumanBodyBones.Hips, HumanBodyBones.Spine, HumanBodyBones.Chest, HumanBodyBones.UpperChest,
            HumanBodyBones.LeftUpperLeg, HumanBodyBones.RightUpperLeg
        };

        private static readonly List<Vector3> Raw = new List<Vector3>(16384);

        private static readonly List<BoneWeight> Weights = new List<BoneWeight>(16384);

        private static readonly List<byte> Parts = new List<byte>(96);

        private static readonly Transform[] Trunk = new Transform[TorsoBones.Length];

        private static readonly List<Matrix4x4> Poses = new List<Matrix4x4>(96);

        private static Mesh _bake;

        // The drawn body as it is now, in one pass over the mesh: the trunk into Body in the torso's frame, the head into
        // Head in the head's frame (HeadFrame), told apart by the bone each point follows most, so that arms hanging at
        // the sides are never taken for the waist.
        private static bool Measure(SkinnedMeshRenderer skin, Animator animator, Transform head, Vector3 origin, Vector3 right, Vector3 up, Vector3 forward)
        {
            Body.Clear();
            Head.Clear();
            _headFramed = false;
            Mesh mesh = skin != null ? skin.sharedMesh : null;
            if (mesh == null)
            {
                return false;
            }
            if (_bake == null)
            {
                _bake = new Mesh { name = "RealLifeOverhaul Kit Measure" };
                _bake.MarkDynamic();
            }
            skin.BakeMesh(_bake);
            _bake.GetVertices(Raw);

            Parts.Clear();
            Weights.Clear();
            if (mesh.isReadable && mesh.vertexCount == Raw.Count)
            {
                mesh.GetBoneWeights(Weights);
                Transform[] bones = skin.bones;
                for (int i = 0; i < Trunk.Length; i++)
                {
                    Trunk[i] = animator.GetBoneTransform(TorsoBones[i]);
                }
                for (int i = 0; i < bones.Length; i++)
                {
                    Transform bone = bones[i];
                    byte part = bone != null && bone == head ? Skull : Other;
                    for (int t = 0; t < Trunk.Length && part == Other && bone != null; t++)
                    {
                        if (Trunk[t] == bone)
                        {
                            part = Torso;
                        }
                    }
                    Parts.Add(part);
                }
                _headFramed = head != null && HeadFrame(mesh, bones, animator, head);
            }
            bool weighted = Weights.Count == Raw.Count && Parts.Count > 0;

            // A baked mesh is in the renderer's own space with the scale already in it.
            Transform at = skin.transform;
            Vector3 place = at.position;
            Quaternion turn = at.rotation;
            int near = 0;
            for (int i = 0; i < Raw.Count; i++)
            {
                byte part = Torso;
                if (weighted)
                {
                    int bone = Weights[i].boneIndex0;
                    part = bone >= 0 && bone < Parts.Count ? Parts[bone] : Other;
                }
                if (part == Other)
                {
                    continue;
                }
                Vector3 world = place + turn * Raw[i];
                if (part == Skull)
                {
                    if (_headFramed)
                    {
                        Vector3 local = head.InverseTransformPoint(world) * _headMetres;
                        Head.Add(new Vector3(Vector3.Dot(local, _headAcross), Vector3.Dot(local, _headUp), Vector3.Dot(local, _headForward)));
                    }
                    continue;
                }
                Vector3 offset = world - origin;
                var point = new Vector3(Vector3.Dot(offset, right), Vector3.Dot(offset, up), Vector3.Dot(offset, forward));
                Body.Add(point);
                if (point.y > 0f && point.y < 0.6f && Mathf.Abs(point.x) < 0.3f && Mathf.Abs(point.z) < 0.3f)
                {
                    near++;
                }
            }
            Raw.Clear();
            Weights.Clear();
            Parts.Clear();
            // A body that is not where its skeleton is (a mesh in some other space) measures nothing.
            return near >= 40;
        }

        // The head's frame in the head joint's own space, from the pose the mesh was bound in (upright, else hips to head,
        // and across the shoulders), so a bowed or turned head changes nothing. False if the mesh does not say.
        private static bool HeadFrame(Mesh mesh, Transform[] bones, Animator animator, Transform head)
        {
            mesh.GetBindposes(Poses);
            int headAt = -1, hipsAt = -1, leftAt = -1, rightAt = -1;
            Transform hips = animator.GetBoneTransform(HumanBodyBones.Hips);
            Transform left = animator.GetBoneTransform(HumanBodyBones.LeftUpperArm);
            Transform right = animator.GetBoneTransform(HumanBodyBones.RightUpperArm);
            for (int i = 0; i < bones.Length && i < Poses.Count; i++)
            {
                Transform bone = bones[i];
                if (bone == null)
                {
                    continue;
                }
                if (headAt < 0 && bone == head)
                {
                    headAt = i;
                }
                else if (hipsAt < 0 && bone == hips)
                {
                    hipsAt = i;
                }
                else if (leftAt < 0 && bone == left)
                {
                    leftAt = i;
                }
                else if (rightAt < 0 && bone == right)
                {
                    rightAt = i;
                }
            }
            bool found = headAt >= 0 && hipsAt >= 0 && leftAt >= 0 && rightAt >= 0;
            Matrix4x4 pose = found ? Poses[headAt] : default;
            Vector3 spine = found ? Bound(headAt) - Bound(hipsAt) : default;
            Vector3 across = found ? Bound(rightAt) - Bound(leftAt) : default;
            Poses.Clear();
            float scale = head.lossyScale.y;
            if (!found || spine.sqrMagnitude < 0.01f || scale < 0.01f)
            {
                return false;
            }
            Vector3 up = Vector3.Dot(spine.normalized, Vector3.up) > 0.9f ? Vector3.up : spine.normalized;
            across -= up * Vector3.Dot(across, up);
            if (across.sqrMagnitude < 0.0025f)
            {
                return false;
            }
            Vector3 forward = Vector3.Cross(across.normalized, up);
            _headUp = pose.MultiplyVector(up).normalized;
            _headForward = pose.MultiplyVector(forward);
            _headForward = (_headForward - _headUp * Vector3.Dot(_headForward, _headUp)).normalized;
            _headAcross = Vector3.Cross(_headUp, _headForward);
            _headMetres = scale;
            return _headForward.sqrMagnitude > 0.5f;
        }

        private static Vector3 Bound(int bone)
        {
            return Poses[bone].inverse.MultiplyPoint3x4(Vector3.zero);
        }

        private static bool Section(List<Vector3> points, float height, float band, float reach, out float halfWidth, out float front, out float back)
        {
            halfWidth = 0f;
            front = 0f;
            back = 0f;
            int found = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                if (Mathf.Abs(point.y - height) > band || Mathf.Abs(point.x) > reach || Mathf.Abs(point.z) > reach + 0.06f)
                {
                    continue;
                }
                found++;
                halfWidth = Mathf.Max(halfWidth, Mathf.Abs(point.x));
                front = Mathf.Max(front, point.z);
                back = Mathf.Max(back, -point.z);
            }
            return found >= 6;
        }

        // How far the body's surface is from the spine, forward (sign 1) or back (-1), round a place.
        private static bool Surface(List<Vector3> points, float across, float height, float patch, float sign, out float depth)
        {
            depth = 0f;
            int found = 0;
            for (int i = 0; i < points.Count; i++)
            {
                Vector3 point = points[i];
                if (Mathf.Abs(point.x - across) > patch || Mathf.Abs(point.y - height) > patch || Mathf.Abs(point.z) > 0.32f)
                {
                    continue;
                }
                float reach = point.z * sign;
                if (reach > 0f)
                {
                    found++;
                    depth = Mathf.Max(depth, reach);
                }
            }
            return found >= 2 && depth > 0.03f;
        }
    }
}
