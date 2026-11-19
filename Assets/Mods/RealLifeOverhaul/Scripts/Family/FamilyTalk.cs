using System;
using System.Collections.Generic;
using BAModAPI;
using Dialogs;
using Entities;
using Localizor;
using Localizor.LanguageChangeEvent;

namespace RealLifeOverhaul.Family
{
    /// <summary>One thing that can be done from a family conversation: offered only while it applies.</summary>
    internal sealed class Choice
    {
        public Func<bool> When;                               // null: always
        public Func<LanguageChangeEventDataHolder> Text;      // what the line says, with today's figures
        public string DoKey;                                  // the label of the button that does it
        public Func<DialogEntry> Do;
        public Action<DialogEntry> Costs;                     // what taking it pays, told under its answer (DialogAnswers); null for nothing

        public Choice(Func<bool> when, Func<LanguageChangeEventDataHolder> text, string doKey, Func<DialogEntry> action)
        {
            When = when;
            Text = text;
            DoKey = doKey;
            Do = action;
        }
    }

    /// <summary>The pieces of every family conversation: one thing at a time with "do it", "something else" and leaving.</summary>
    internal static class FamilyTalk
    {
        private const string InPersonType = "reallife_familytalk_calldialogtype";

        private static Func<DialogEntry> _first;
        private static bool _aboutPartner;   // the conversation being opened is with the partner (a character beside them, or a talk about them)

        /// <summary>Whether the conversation being opened is with the partner; read once by <see cref="FamilyDialog"/>.</summary>
        internal static bool TakePartner()
        {
            bool partner = _aboutPartner;
            _aboutPartner = false;
            return partner;
        }

        /// <summary>The partner's header for a talk with them: their name and age as People keeps it, else the age their look has; null when they have none.</summary>
        internal static void PartnerHead(out string who, out string age)
        {
            who = age = null;
            if (!FamilyState.Loaded || !FamilyState.HasPartner || string.IsNullOrEmpty(FamilyState.S.PName))
            {
                return;
            }
            who = FamilyState.S.PName;
            age = TalkAgeGame.Partner != null ? TalkAgeGame.Partner() : null;
            if (age == null)
            {
                age = TalkAgeGame.Exact(Partner.LookAge);
            }
        }

        /// <summary>The first entry of the face-to-face conversation being opened; read once by <see cref="FamilyDialog"/>.</summary>
        internal static DialogEntry TakeFirst()
        {
            Func<DialogEntry> first = _first;
            _first = null;
            try
            {
                return first != null ? first() : Close("reallife_family_menu_nothing");
            }
            catch (Exception e)
            {
                FamilyDirector.Logger?.Error(e);
                return Close("reallife_family_menu_nothing");
            }
        }

        /// <summary>Opens a conversation face to face, as the game's counters do; <paramref name="closed"/> is called however it ends.</summary>
        public static bool Open(Func<DialogEntry> first, Action closed = null, ThirdPersonCharacter with = null, bool partner = false)
        {
            var uis = InstanceBehavior<UI.UIs>.Instance;
            if (uis == null || uis.playerHUD == null || uis.playerHUD.dialogUI == null || DialogController.current != null)
            {
                closed?.Invoke();
                return false;
            }
            _first = first;
            _aboutPartner = partner || with != null;
            var type = (CallDialogType)ModEnumHash.GetSafeHash(InPersonType);
            CallDialogFactory.RegisterDialog(type, Guard.Maker(() => new FamilyDialog()));
            uis.playerHUD.dialogUI.ShowDialog(type, NavigationBlocker.SpecialEmployeeDialog, null, Guard.Way(closed), with);
            return true;
        }

        /// <summary>A last line: on the phone the call ends by itself, face to face there is a button to leave.</summary>
        public static DialogEntry Close(string key, in Args fill)
        {
            return Close(FamilyText.Say(key, fill));
        }

        public static DialogEntry Close(string key)
        {
            return Close(FamilyText.Say(key, Args.Us));
        }

        public static DialogEntry Close(LanguageChangeEventDataHolder text)
        {
            bool phone = DialogController.current != null && DialogController.current.dialogType == DialogType.PhoneCall;
            return new DialogEntry
            {
                messageData = text,
                OnVisible = phone ? DialogController.current.FinishDialog : (Action)null,
                OnCancel = phone ? (Action)null : Leave
            };
        }

        public static void Leave()
        {
            if (DialogController.current != null)
            {
                DialogController.current.CancelDialog();
            }
        }

        /// <summary>A line with one way on (and leaving).</summary>
        public static DialogEntry Then(LanguageChangeEventDataHolder text, string nextKey, Func<DialogEntry> next)
        {
            return new DialogEntry
            {
                messageData = text,
                ConfirmTextOverride = nextKey.Localize(),
                OnConfirm = Safe(next),
                OnCancel = Leave
            };
        }

        /// <summary>A question with two answers (and leaving, which answers nothing).</summary>
        public static DialogEntry Either(LanguageChangeEventDataHolder text, string firstKey, Func<DialogEntry> first, string secondKey, Func<DialogEntry> second)
        {
            return new DialogEntry
            {
                messageData = text,
                ConfirmTextOverride = firstKey.Localize(),
                OnConfirm = Safe(first),
                SecondOptionTextOverride = secondKey,
                OnSecondOption = Safe(second),
                OnCancel = Leave
            };
        }

        /// <summary>The choices that apply, one at a time from <paramref name="index"/> on, round and round; a closing line if none applies.</summary>
        public static DialogEntry Menu(List<Choice> choices, int index, string nothingKey = "reallife_family_menu_nothing")
        {
            int count = choices.Count;
            int at = -1;
            int next = -1;
            for (int i = 0; i < count; i++)
            {
                int k = (index + i) % count;
                if (choices[k].When != null && !choices[k].When())
                {
                    continue;
                }
                if (at < 0)
                {
                    at = k;
                }
                else
                {
                    next = k;
                    break;
                }
            }
            if (at < 0)
            {
                return Close(nothingKey);
            }
            Choice choice = choices[at];
            var entry = new DialogEntry
            {
                messageData = choice.Text(),
                ConfirmTextOverride = choice.DoKey.Localize(),
                OnConfirm = Safe(choice.Do),
                OnCancel = Leave
            };
            if (next >= 0)
            {
                int from = next;
                entry.SecondOptionTextOverride = "reallife_family_menu_next";
                entry.OnSecondOption = Safe(() => Menu(choices, from, nothingKey));
            }
            choice.Costs?.Invoke(entry);
            return entry;
        }

        // A choice's action runs from the game's button handler: a fault must end the conversation politely, not escape.
        private static Func<DialogEntry> Safe(Func<DialogEntry> action)
        {
            return () =>
            {
                try
                {
                    if (!FamilyState.Loaded || SaveGameManager.Current == null)
                    {
                        return Close("reallife_family_menu_nothing");
                    }
                    return action();
                }
                catch (Exception e)
                {
                    FamilyDirector.Logger?.Error(e);
                    return Close("reallife_family_no_fault");
                }
            };
        }

        /// <summary>Takes money for something; false (with the game's own "not enough money" notice) if the player cannot pay.</summary>
        public static bool Pay(int amount, string transaction)
        {
            return amount <= 0 || RealLifeOverhaul.Economy.BizBooks.Book(-amount, new TransactionInfo(transaction), null, null, false, true);
        }

        /// <summary>Money in, or a cost that must be met (a bill that was agreed): never refused.</summary>
        public static void Move(int amount, string transaction)
        {
            if (amount != 0)
            {
                RealLifeOverhaul.Economy.BizBooks.Book(amount, new TransactionInfo(transaction), null, null, true);
            }
        }
    }

    /// <summary>The face-to-face family conversation: whatever <see cref="FamilyTalk.Open"/> was given.</summary>
    public sealed class FamilyDialog : Dialog, ITalkAge
    {
        private readonly bool _partner;
        private string _who, _age;

        public string TalkWho => _who;

        public string TalkAge => _age;

        public FamilyDialog()
        {
            _partner = FamilyTalk.TakePartner();
            if (_partner)
            {
                FamilyTalk.PartnerHead(out _who, out _age);
            }
            using var head = TalkAgeGame.Making(this);   // the first page is headed with their age too
            npcNameKey = FamilyPhone.FamilyId;
            DialogController.current.ShowEntry(Guard.Entry(FamilyTalk.TakeFirst()));
        }
    }
}
