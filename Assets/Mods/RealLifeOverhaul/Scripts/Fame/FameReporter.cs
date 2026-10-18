using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Helpers;
using Localizor;
using RealLifeOverhaul.GameUi;
using UnityEngine;

namespace RealLifeOverhaul.Fame
{
    /// <summary>
    /// A reporter (or a photographer) who walks up to the player from out of view and asks for a word in the game's box
    /// over a person; answering opens <see cref="FameTalk"/>. Ticked once a second while out.
    /// </summary>
    internal sealed class FameReporter
    {
        private enum Stage { None, Walking, Asking, Talking, Leaving }

        private const float Near = 2.4f;           // metres: close enough to ask
        private const float LostAt = 45f;          // metres: the player has walked off
        private const float WalkMost = 45f;        // real seconds to reach the player...
        private const float AskMost = 25f;         // ...to be answered...
        private const float LeaveMost = 15f;       // ...and to walk off out of view

        private readonly FameDirector _director;
        private readonly WorldPanel _panel = new WorldPanel { FollowLift = 2.15f, Name = "reporter" };
        private readonly Action _answer, _decline;
        private StreetActor _actor;
        private Stage _stage;
        private float _since;
        private bool _photographer;

        public FameReporter(FameDirector director)
        {
            _director = director;
            _answer = () => UiClone.Safe(Answer);
            _decline = () => UiClone.Safe(Decline);
        }

        public bool Active => _stage != Stage.None;

        /// <summary>A reporter, or a photographer, walks up from 18 to 28 metres off. False if nobody could be put out.</summary>
        public bool Begin(bool photographer)
        {
            ActorPool pool = ActorPool.Instance;
            if (Active || pool == null || PlayerHelper.PlayerController == null)
            {
                return false;
            }
            Vector3 player = PlayerHelper.GetPosition();
            Vector3 spot;
            if (!StreetSpots.PavementAround(player, 18f, 28f, true, out spot))
            {
                return false;
            }
            StreetActor actor = pool.Spawn(spot, ActorLook.Civilian, 3f);
            if (actor == null)
            {
                return false;
            }
            if (!actor.MoveTo(player, Pace.Walk))
            {
                pool.Release(actor);
                return false;
            }
            _actor = actor;
            _photographer = photographer;
            _stage = Stage.Walking;
            _since = Time.realtimeSinceStartup;
            return true;
        }

        public void Tick()
        {
            if (!Active)
            {
                return;
            }
            if (_actor == null || !_actor.InUse || !_actor.IsValid)
            {
                Clear();
                return;
            }
            float waited = Time.realtimeSinceStartup - _since;
            Vector3 player = PlayerHelper.GetPosition();
            float distance = FameGame.Flat(player, _actor.Position);
            bool away = BuildingManager.IsInsideBuilding || PlayerHelper.IsUsingVehicle || distance > LostAt;
            switch (_stage)
            {
                case Stage.Walking:
                    if (away || waited > WalkMost)
                    {
                        Leave(false);
                    }
                    else if (distance <= Near)
                    {
                        _actor.StandAt(_actor.Position, player);
                        Arrived(player);
                    }
                    else
                    {
                        _actor.MoveTo(player, Pace.Walk);   // the player may have moved
                    }
                    break;
                case Stage.Asking:
                    if (away || distance > Near * 3f || waited > AskMost)
                    {
                        _panel.Hide();
                        Leave(false);
                    }
                    else if (!_panel.Showing)
                    {
                        _panel.Show();   // the game's box may have been taken for a moment
                    }
                    break;
                case Stage.Talking:
                    if (DialogController.current == null)
                    {
                        Leave(true);   // a dialogue closed some other way than its own end
                    }
                    break;
                case Stage.Leaving:
                    if (waited > LeaveMost || (!_actor.Visible && waited > 2f) || _actor.HasArrived)
                    {
                        Clear();
                    }
                    break;
            }
        }

        // In front of the player: a photographer takes the picture and goes; a reporter asks for a word.
        private void Arrived(Vector3 player)
        {
            if (_photographer)
            {
                FameGame.Face(_actor.Character, CharacterEmojiName.CommonGreenHappy);
                _director.Photographed();
                Leave(false);
                return;
            }
            _panel.Follow = _actor.Character.transform;
            _panel.Lines(FameText.T("reallife_fame_reporter_title"), FameText.T("reallife_fame_reporter_ask"), null, WorldPanel.White());
            _panel.Buttons("reallife_fame_reporter_answer", _answer, true, "reallife_fame_reporter_walk_on", _decline, true);
            _panel.Show();
            _stage = Stage.Asking;
            _since = Time.realtimeSinceStartup;
        }

        private void Answer()
        {
            if (_stage != Stage.Asking)
            {
                return;
            }
            _panel.Hide();
            List<FameAsk> asks = _director.Interview(FameVenue.Street);
            if (asks == null || !FameTalk.Open(asks, () => Leave(true), _actor.Character))
            {
                Leave(false);
                return;
            }
            _stage = Stage.Talking;
        }

        private void Decline()
        {
            if (_stage != Stage.Asking)
            {
                return;
            }
            _panel.Hide();
            _director.Declined();
            Leave(false);
        }

        // Off out of view; put back once unseen or there.
        private void Leave(bool talked)
        {
            if (_stage == Stage.Leaving || _stage == Stage.None)
            {
                return;
            }
            _panel.Hide();
            if (talked)
            {
                FameGame.Face(_actor.Character, CharacterEmojiName.CommonGreenHappy);
            }
            Vector3 spot;
            if (_actor != null && _actor.InUse && StreetSpots.AwayFrom(_actor.Position, PlayerHelper.GetPosition(), 30f, false, out spot) && _actor.MoveTo(spot, Pace.Walk))
            {
                _stage = Stage.Leaving;
                _since = Time.realtimeSinceStartup;
                return;
            }
            Clear();
        }

        public void Clear()
        {
            _panel.Hide();
            if (_actor != null && ActorPool.Instance != null)
            {
                ActorPool.Instance.Release(_actor);
            }
            _actor = null;
            _stage = Stage.None;
        }
    }

    /// <summary>A street interview in the game's own dialogue panel; each answer takes effect at once.</summary>
    internal static class FameTalk
    {
        internal const string NpcKey = "reallife-fame-reporter";
        private const string InPersonType = "reallife_fametalk_calldialogtype";

        private static List<FameAsk> _asks;
        private static int _good, _hard;

        /// <summary>Opens the interview. False (and nothing shown) if a dialogue is already open or the game's panel is not there.</summary>
        public static bool Open(List<FameAsk> asks, Action closed, ThirdPersonCharacter with)
        {
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.playerHUD == null || uis.playerHUD.dialogUI == null || DialogController.current != null || asks == null || asks.Count == 0)
            {
                return false;
            }
            _asks = asks;
            _good = _hard = 0;
            var type = (CallDialogType)ModEnumHash.GetSafeHash(InPersonType);
            CallDialogFactory.RegisterDialog(type, Guard.Maker(() => new FameTalkDialog()));
            uis.playerHUD.dialogUI.ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(closed), with);
            return true;
        }

        internal static DialogEntry First() => Question(0);

        private static DialogEntry Question(int index)
        {
            if (_asks == null || index >= _asks.Count)
            {
                return End();
            }
            FameAsk ask = _asks[index];
            string[] keys = FameText.QuestionKeys(ask);
            return new DialogEntry
            {
                messageData = "reallife_fame_talk_line".Localize(new { text = FameText.Fill(keys[0], "promise", FameText.PromiseWords(ask.Promise)) }),
                ConfirmTextOverride = keys[1].Localize(),
                OnConfirm = Safe(() => Answered(index, 0)),
                SecondOptionTextOverride = keys[2],
                OnSecondOption = Safe(() => Answered(index, 1)),
                CancelTextOverride = "reallife_fame_talk_end",
                OnCancel = Leave
            };
        }

        private static DialogEntry Answered(int index, int option)
        {
            FameDirector director = FameDirector.Instance;
            if (director == null || director.File == null || _asks == null)
            {
                return Close("reallife_fame_talk_gone");
            }
            FameAnswer a = director.Answered(_asks[index], option, FameVenue.Street);
            _good += a.Good ? 1 : 0;
            _hard += a.Good ? 0 : 1;
            return Question(index + 1);
        }

        private static DialogEntry End()
        {
            FameDirector.Instance?.Interviewed(FameVenue.Street, _good, _hard);
            return Close(_hard > 0 ? "reallife_fame_talk_done_hard" : "reallife_fame_talk_done");
        }

        private static DialogEntry Close(string key)
        {
            return new DialogEntry
            {
                messageData = "reallife_fame_talk_line".Localize(new { text = FameText.T(key) }),
                OnCancel = Leave
            };
        }

        private static void Leave()
        {
            if (DialogController.current != null)
            {
                DialogController.current.CancelDialog();
            }
        }

        // An answer runs in the game's button handler: a fault ends the interview instead of escaping.
        private static Func<DialogEntry> Safe(Func<DialogEntry> action)
        {
            return () =>
            {
                try
                {
                    return action();
                }
                catch (Exception e)
                {
                    FameDirector.Logger?.Error(e);
                    return Close("reallife_fame_talk_gone");
                }
            };
        }
    }

    /// <summary>The street interview face to face: whatever <see cref="FameTalk.Open"/> was given.</summary>
    public class FameTalkDialog : Dialog
    {
        public FameTalkDialog()
        {
            npcNameKey = FameTalk.NpcKey;
            DialogEntry first;
            try
            {
                first = FameTalk.First();
            }
            catch (Exception e)
            {
                FameDirector.Logger?.Error(e);
                first = new DialogEntry { messageData = "reallife_fame_talk_line".Localize(new { text = FameText.T("reallife_fame_talk_gone") }) };
            }
            DialogController.current.ShowEntry(Guard.Entry(first));
        }
    }
}
