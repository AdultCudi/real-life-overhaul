using System;
using System.Collections.Generic;
using BigAmbitions.Characters;
using Entities;
using RealLifeOverhaul.GameUi;
using Streets.Pedestrians;
using UnityEngine;
using Random = UnityEngine.Random;

namespace RealLifeOverhaul
{
    /// <summary>Busking: how the player plays (the mod's clip or the game's own performance) and the music.</summary>
    public sealed partial class BuskerJob
    {
        private AudioSource _music;
        private StreetPerformerData _act;     // the game's own performance for the look and the music, or null

        // One of the game's own street performances that the player's animator can play, or null.
        private StreetPerformerData GameAct(PermanentAnimationType animation)
        {
            try
            {
                if (!Ctx.Self.Knows(animation))
                {
                    return null;
                }
                IReadOnlyList<StreetPerformerData> acts = StreetPerformerDataHelper.GetStreetPerformerDataList();
                for (int i = 0; acts != null && i < acts.Count; i++)
                {
                    if (acts[i] != null && acts[i].animation == animation)
                    {
                        return acts[i];
                    }
                }
            }
            catch (Exception e)
            {
                Ctx.Logger?.Error(e);
            }
            return null;
        }

        // The mod's clip and guitar if the bundle has them, else the game's own performance; the game's music either way.
        private bool StartPlaying()
        {
            bool playing = Ctx.Self.Clip(StrumClip);
            if (!playing && _act != null && Ctx.Self.Pose(_act.animation))
            {
                playing = true;
                // Everything the game hangs on a performer of this act, and the first of the things it picks one of.
                Carry(_act.objectsData, false);
                Carry(_act.spawnOnlyOneObjectsData, true);
            }
            _playing = playing;
            if (playing)
            {
                Music(true);
            }
            return playing;
        }

        private void Carry(PerformerObjectData[] things, bool onlyOne)
        {
            ThirdPersonCharacter body = Ctx.Player.Character;
            for (int i = 0; things != null && i < things.Length; i++)
            {
                PerformerObjectData thing = things[i];
                if (thing == null || (!thing.isHandObject && !thing.isUpperChestObject))
                {
                    continue;   // stood on the ground beside a performer (a stool, an amplifier): not for a pitch set up anywhere
                }
                Transform bone = thing.isUpperChestObject ? body.upperChest : thing.isRightHand ? body.rightHand : body.leftHand;
                if (Ctx.Self.GameProp(thing.objectName, bone, thing.position, Quaternion.Euler(thing.rotation)) && onlyOne)
                {
                    return;
                }
            }
        }

        private void Music(bool on)
        {
            try
            {
                if (!on)
                {
                    if (_music != null)
                    {
                        _music.Stop();
                        _music.clip = null;
                    }
                    return;
                }
                StreetPerformerData sound = _act ?? GameAct(PermanentAnimationType.PlayingGuitar);
                AudioClip clip = sound != null ? sound.clip : null;
                if (clip == null)
                {
                    return;
                }
                if (_music == null)
                {
                    // One source on the jobs' own root, kept for the session.
                    Transform host = Ctx.Director.transform.Find("RealLifeOverhaul Busker Sound");
                    if (host == null)
                    {
                        host = new GameObject("RealLifeOverhaul Busker Sound").transform;
                        host.SetParent(Ctx.Director.transform, false);
                        host.gameObject.AddComponent<AudioSource>();
                    }
                    _music = host.GetComponent<AudioSource>();
                    _music.playOnAwake = false;
                    _music.loop = true;
                    _music.spatialBlend = 1f;
                    _music.minDistance = 4f;
                    _music.maxDistance = 40f;
                    _music.volume = 0.8f;
                    var references = InstanceBehavior<GlobalReferences>.Instance;
                    if (references != null)
                    {
                        _music.outputAudioMixerGroup = references.cityMixerGroup;
                    }
                }
                _music.transform.position = Ctx.Player.transform.position + Vector3.up * 1.2f;
                _music.clip = clip;
                _music.Play();
            }
            catch (Exception e)
            {
                Ctx.Logger?.Error(e);   // silent busking is still busking
            }
        }
    }
}
