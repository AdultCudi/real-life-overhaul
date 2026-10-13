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
    /// A reporter of the City Ledger who comes up to the player in the street: one of the mod's street people (People/
    /// ActorPool), put on a pavement out of view 18 to 28 metres off, who walks up, stops in front of the player and asks
    /// for a word in the game's own box over a person (GameUi.WorldPanel: the interact key to answer, the secondary key to
    /// walk on). Answering opens the game's dialogue face to face (FameTalk): two or three questions, two answers each and
    /// a way to end it. Whatever the answer, the reporter walks off out of view and is put back.
    ///
    /// Ticked once a second by the director while there is one; nothing at all otherwise. A reporter who cannot reach the
    /// player in 45 seconds, or whom the player leaves behind, gives up.
    /// </summary>
    internal sealed class FameReporter
    {
        private enum Stage { None, Walking, Asking, Talking, Leaving }

        private const float Near = 2.4f;           // metres: close enough to ask
        private const float LostAt = 45f;          // metres: the player has walked off
        private const float WalkMost = 45f;        // real seconds to reach the player
        private const float AskMost = 25f;         // ...and to be answered
        private const float LeaveMost = 15f;       // ...and to walk off out of view

        private readonly FameDirector _director;
        private readonly WorldPanel _panel = new WorldPanel { FollowLift = 2.15f };
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

        /// <summary>A reporter (or, with photographer, somebody with a camera) walks up. False if nobody could be put out.</summary>
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

        /// <summary>Once a second while there is one.</summary>
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
            float distance = Flat(player, _actor.Position);
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
                        _actor.MoveTo(player, Pace.Walk);   // the player may have moved on
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
                        _panel.Show();   // the game's box may have been taken a moment: asked again
                    }
                    break;
                case Stage.Talking:
                    if (DialogController.current == null)
                    {
                        Leave(true);   // (the dialogue's own closing does this; this is for one closed some other way)
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

        // Off out of view; put back when nobody can see, or when there.
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

        /// <summary>Everything put back now: the box down, the person back.</summary>
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

        private static float Flat(Vector3 a, Vector3 b)
        {
            a.y = b.y = 0f;
            return Vector3.Distance(a, b);
        }
    }

    /// <summary>
    /// A street interview in the game's own dialogue panel, face to face with the reporter: one question at a time with two
    /// answers and "That will do" to end it. Each answer is given at once (FamePress.Answer through the director); the last
    /// line says how it went. The rules are FamePress's; this class only words them.
    /// </summary>
    internal static class FameTalk
    {
        private const string InPersonType = "reallife_fametalk_calldialogtype";
        private const string Npc = "reallife-fame-reporter";

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
            CallDialogFactory.RegisterDialog(type, () => new FameTalkDialog());
            uis.playerHUD.dialogUI.ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, null, closed, with);
            return true;
        }

        internal static string NpcKey => Npc;

        internal static DialogEntry First()
        {
            return Question(0);
        }

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

        // An answer runs from the game's button handler: a fault ends the interview politely, never escapes.
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
            DialogController.current.ShowEntry(first);
        }
    }
}
