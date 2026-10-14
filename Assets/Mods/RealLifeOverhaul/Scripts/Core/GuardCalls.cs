using System;
using System.Runtime.CompilerServices;
using BigAmbitions.Mods;
using Dialogs;
using Entities;
using PlayerActivity;
using UI.Elements;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The guard (Core/Guard) for the game's own calls into the mod, past its entry points: what the game asks of an
    /// object of the mod's through an interface or a base class of its own, and the delegates of the mod's it keeps and
    /// calls later. Each runs inside the guard of the class it is written in, so that a fault, or a feature switched off,
    /// never reaches the game's code and never leaves the player in something that cannot go on or be left:
    ///   asked        an activity of the game's work panel, an overlay, a call to action, a pointer, a table and its
    ///                cells, a mesh, a drag or a scroll, a row of the options page: the method's body in a try whose
    ///                catch is <see cref="Guard.Fail"/>, the game given <see cref="Failed{T}"/>'s answer instead; an
    ///                activity whose feature is off <see cref="Ended"/> as its own Finish ends it, so the panel closes;
    ///   dialogs      every page of the mod's dialogs as it goes to the game (<see cref="Entry"/>), and every dialog
    ///                the game makes for a contact of the mod's (<see cref="Maker"/>): an answer that fails, or whose
    ///                feature is off, ends the conversation with the game's own closing; a way out is never skipped;
    ///   options      the options page as it is registered (<see cref="Rows"/>): every row's callback caught, never
    ///                skipped, since a row of the page must go on working whatever is off;
    ///   listeners    <see cref="Guard.Listener"/>, <see cref="Guard.Wrap(Action)"/> and <see cref="Way"/> for the rest.
    /// ContractCheck reads the built mod to see that each of these is so (the game's calls, the callbacks, the dialogs
    /// and the options page). Nothing here runs per frame but the try round a method; the wrappers are made once, as a
    /// dialog's page, a row or a listener is made.
    /// </summary>
    public static partial class Guard
    {
        // ---------------------------------------------------------------- what the game asks

        /// <summary>A method the game called failed: said as <see cref="Fail"/> says it, and the answer the game is given instead.</summary>
        public static T Failed<T>(FeatureGuard guard, Exception e, T answer)
        {
            Fail(guard, e);
            return answer;
        }

        /// <summary>A mesh the game asked for failed: said, and the half made emptied, so that nothing broken is drawn.</summary>
        public static void FailMesh(FeatureGuard guard, Exception e, VertexHelper mesh)
        {
            Fail(guard, e);
            try
            {
                mesh?.Clear();
            }
            catch (Exception)
            {
                // (nothing more to be done for the mesh: it is the guard's own catch)
            }
        }

        /// <summary>A line of the game's panels with nothing in it: what a label that failed is shown as.</summary>
        public static LabelInfo Blank()
        {
            return new LabelInfo(string.Empty, new Color32(255, 255, 255, 255), false);
        }

        /// <summary>
        /// An activity of the mod's on the game's work panel whose feature is off, after its faults or on a game that lacks
        /// what it stands on: it is ended once as its own Finish ends it (the player stands up, its hold on the player
        /// goes), and then says Finished, so that the game's panel closes it as it closes its own.
        /// </summary>
        public static PlayerActivityState Ended(IPlayerActivity activity)
        {
            try
            {
                if (activity != null && !Calls.Ended.TryGetValue(activity, out _))
                {
                    Calls.Ended.Add(activity, Calls.Mark);
                    Logger?.Warn("Guard: " + activity.GetType().Name + " on the game's work panel is ended: its feature is off.");
                    activity.Finish();
                }
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
            return PlayerActivityState.Finished;
        }

        // ---------------------------------------------------------------- dialogs

        /// <summary>
        /// A page of one of the mod's dialogs as it goes to the game (DialogController.ShowEntry): its two answers run
        /// inside the guard of the class they are written in, and so does each page they lead to, in turn; what runs as
        /// it appears, its cancel and its forced cancel are caught too, and never skipped, since they are the way out. An
        /// answer that fails, or whose feature is off, ends the conversation with the game's own closing (a phone call
        /// hangs up, a talk in person can be left), so that no dialog is left that cannot go on. A page is wrapped once.
        /// </summary>
        public static DialogEntry Entry(DialogEntry entry)
        {
            if (entry == null || Calls.Pages.TryGetValue(entry, out _))
            {
                return entry;
            }
            Calls.Pages.Add(entry, Calls.Mark);
            if (Ours(entry.OnConfirm))
            {
                entry.OnConfirm = new Answer(entry.OnConfirm).Give;
            }
            if (Ours(entry.OnSecondOption))
            {
                entry.OnSecondOption = new Answer(entry.OnSecondOption).Give;
            }
            entry.OnCancel = Way(entry.OnCancel, true);
            entry.OnForcedCancel = Way(entry.OnForcedCancel, false);
            entry.OnVisible = Way(entry.OnVisible, false);
            return entry;
        }

        /// <summary>
        /// The game's maker of one of the mod's dialogs (CallDialogFactory.RegisterDialog): a dialog that cannot be made,
        /// or whose feature is off, gives the game's own closing page instead of a fault in the middle of the game's
        /// opening of the call.
        /// </summary>
        public static Func<Dialog> Maker(Func<Dialog> make)
        {
            if (make == null || !Ours(make))
            {
                return make;
            }
            FeatureGuard guard = OfHandler(make);
            return () =>
            {
                if (guard.Off)
                {
                    return new Closed();
                }
                try
                {
                    return make();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                    return new Closed();
                }
            };
        }

        /// <summary>
        /// A way out the game calls (a dialog's cancel, what runs when a dialog stops): caught, never skipped, whatever is
        /// off. A cancel that fails still ends the dialog, with the game's own.
        /// </summary>
        public static Action Way(Action way)
        {
            return Way(way, false);
        }

        /// <summary>As <see cref="Way(Action)"/>, for a callback with one argument (an asset bundle's load done).</summary>
        public static Action<T> Way<T>(Action<T> way)
        {
            if (way == null || !Ours(way))
            {
                return way;
            }
            FeatureGuard guard = OfHandler(way);
            return value =>
            {
                try
                {
                    way(value);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        /// <summary>
        /// A question the game asks of the mod later (a row of the city map's filters: is it there, where is it): inside
        /// the guard of the class it is written in; the given answer when it fails or its feature is off.
        /// </summary>
        public static Func<T> Asked<T>(Func<T> ask, T otherwise)
        {
            if (ask == null || !Ours(ask))
            {
                return ask;
            }
            FeatureGuard guard = OfHandler(ask);
            return () =>
            {
                if (guard.Off)
                {
                    return otherwise;
                }
                try
                {
                    return ask();
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                    return otherwise;
                }
            };
        }

        private static Action Way(Action way, bool leaves)
        {
            if (way == null || !Ours(way) || way.Target is Calls.Caught)
            {
                return way;
            }
            return new Calls.Caught(way, OfHandler(way), leaves).Run;
        }

        // A delegate of the mod's own (the game's own, such as its dialog's FinishDialog, is left as it is).
        private static bool Ours(Delegate handler)
        {
            return handler != null && handler.Method.DeclaringType != null && handler.Method.DeclaringType.Assembly == typeof(Guard).Assembly;
        }

        // The game's own closing, as the mod's dialogs close (Phone/PhoneContacts.Closing), or nothing if even that fails.
        private static DialogEntry Closing()
        {
            try
            {
                return PhoneContacts.Closing("reallife_guard_dialog_closed");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return null;
            }
        }

        // One answer of a page: the page it leads to, wrapped in turn; the closing if it fails or its feature is off.
        private sealed class Answer
        {
            private readonly Func<DialogEntry> _next;
            private readonly FeatureGuard _guard;

            public Answer(Func<DialogEntry> next)
            {
                _next = next;
                _guard = OfHandler(next);
            }

            public DialogEntry Give()
            {
                if (_guard.Off)
                {
                    return Closing();
                }
                try
                {
                    return Entry(_next());
                }
                catch (Exception e)
                {
                    Fail(_guard, e);
                    return Closing();
                }
            }
        }

        // A dialog that could not be made: the closing page alone.
        private sealed class Closed : Dialog
        {
            public Closed()
            {
                try
                {
                    DialogController.current?.ShowEntry(Closing());
                }
                catch (Exception e)
                {
                    Logger?.Error(e);
                }
            }
        }

        // ---------------------------------------------------------------- the options page

        /// <summary>
        /// The options page as it goes to the game (OptionsService.Register): the same rows, each callback caught and
        /// written to Player.log (a member this game lacks: once), and never skipped. The game calls a row's callback as
        /// it draws the page, too, so a row that throws would cost the game the rest of the page.
        /// </summary>
        public static ModOptions Rows(ModOptions page)
        {
            if (page == null)
            {
                return null;
            }
            try
            {
                var rows = new ModOptions();
                foreach (ModOption option in page.Options)
                {
                    rows.AddCustom(Row(option));
                }
                return rows;
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                return page;
            }
        }

        private static ModOption Row(ModOption option)
        {
            switch (option)
            {
                case ButtonOption button when button.OnClick != null:
                    return new ButtonOption(button.Label, Calls.Row(button.OnClick, button.Label));
                case ToggleOption toggle when toggle.OnValueChanged != null:
                    return new ToggleOption(toggle.Id, toggle.Label, toggle.DefaultValue, Calls.Row(toggle.OnValueChanged, toggle.Label));
                case SliderOption slider when slider.OnValueChanged != null:
                    return new SliderOption(slider.Id, slider.Label, slider.Min, slider.Max, slider.DefaultValue, Calls.Row(slider.OnValueChanged, slider.Label), slider.ValueLabelKey);
                case DropdownOption dropdown when dropdown.OnValueChanged != null:
                    return new DropdownOption(dropdown.Id, dropdown.Label, dropdown.ChoiceKeys, dropdown.DefaultIndex, Calls.Row(dropdown.OnValueChanged, dropdown.Label));
                default:
                    return option;   // (a header, a splitter, a row of the mod's own: its SpawnUi is guarded where it is written)
            }
        }

        // ---------------------------------------------------------------- the scene's events

        /// <summary>As <see cref="Listener(UnityAction)"/>, for the engine's scene loaded event (two arguments).</summary>
        public static UnityAction<T1, T2> Listener<T1, T2>(UnityAction<T1, T2> handler)
        {
            if (handler == null)
            {
                return null;
            }
            FeatureGuard guard = OfHandler(handler);
            return (a, b) =>
            {
                if (guard.Off)
                {
                    return;
                }
                try
                {
                    handler(a, b);
                }
                catch (Exception e)
                {
                    Fail(guard, e);
                }
            };
        }

        // ---------------------------------------------------------------- what is kept

        // The statics that name the game's types, apart from Guard's own: Guard starts whatever the game has.
        private static class Calls
        {
            public static readonly object Mark = new object();
            public static readonly ConditionalWeakTable<DialogEntry, object> Pages = new ConditionalWeakTable<DialogEntry, object>();
            public static readonly ConditionalWeakTable<IPlayerActivity, object> Ended = new ConditionalWeakTable<IPlayerActivity, object>();
            private static int _rowFaults;

            // A way out: caught and said, never skipped; a cancel that failed still ends the dialog.
            public sealed class Caught
            {
                private readonly Action _way;
                private readonly FeatureGuard _guard;
                private readonly bool _leaves;

                public Caught(Action way, FeatureGuard guard, bool leaves)
                {
                    _way = way;
                    _guard = guard;
                    _leaves = leaves;
                }

                public void Run()
                {
                    try
                    {
                        _way();
                    }
                    catch (Exception e)
                    {
                        Fail(_guard, e);
                        if (_leaves)
                        {
                            try
                            {
                                DialogController.current?.CancelDialog();
                            }
                            catch (Exception again)
                            {
                                Logger?.Error(again);
                            }
                        }
                    }
                }
            }

            public static Action Row(Action call, string label)
            {
                return () =>
                {
                    try
                    {
                        call();
                    }
                    catch (Exception e)
                    {
                        RowFailed(label, e);
                    }
                };
            }

            public static Action<T> Row<T>(Action<T> call, string label)
            {
                return value =>
                {
                    try
                    {
                        call(value);
                    }
                    catch (Exception e)
                    {
                        RowFailed(label, e);
                    }
                };
            }

            // A row's callback failed: the first ones in full, then a short line each, then nothing; the row goes on.
            private static void RowFailed(string label, Exception e)
            {
                try
                {
                    string where = "the options row '" + label + "'";
                    if (DoesWithout(e, where, "the row does nothing"))
                    {
                        return;
                    }
                    int n = System.Threading.Interlocked.Increment(ref _rowFaults);
                    if (n <= MostDetailed)
                    {
                        Logger?.Warn("Guard: " + where + " failed (" + n + "); the page goes on.");
                        Logger?.Error(e);
                    }
                    else if (n <= MostShort)
                    {
                        Logger?.Warn("Guard: " + where + " failed again (" + n + "): " + e.GetType().Name + ": " + e.Message + (n == MostShort ? " (Said no more this session.)" : string.Empty));
                    }
                }
                catch (Exception)
                {
                    // (the guard itself must never be the next fault)
                }
            }
        }
    }
}
