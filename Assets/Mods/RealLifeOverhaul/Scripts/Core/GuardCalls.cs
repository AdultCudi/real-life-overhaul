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
    /// The guard's part for the game's own calls into the mod past its entry points: what it asks through its
    /// interfaces and base classes, and the delegates it keeps. Each runs in the guard of the class it is written in, so
    /// a fault or a feature switched off never reaches the game's code nor leaves the player stuck: a method the game
    /// asks gets <see cref="Failed{T}"/>'s answer; an activity whose feature is off is <see cref="Ended"/>; a dialog's
    /// pages (<see cref="Entry"/>, <see cref="Maker"/>) end with the game's own closing; the options page's rows
    /// (<see cref="Rows"/>) are caught but never skipped; a way out (<see cref="Way"/>) always runs. ContractCheck reads
    /// the built mod to see that each is so. COST: the try round a method; the wrappers are made once.
    /// </summary>
    public static partial class Guard
    {
        /// <summary>A method the game called failed: said as <see cref="Fail"/> says it; returns the answer the game gets instead.</summary>
        public static T Failed<T>(FeatureGuard guard, Exception e, T answer)
        {
            Fail(guard, e);
            return answer;
        }

        /// <summary>A mesh the game asked for failed: said, and the half made emptied so nothing broken is drawn.</summary>
        public static void FailMesh(FeatureGuard guard, Exception e, VertexHelper mesh)
        {
            Fail(guard, e);
            NoMesh(mesh);
        }

        /// <summary>Empties the mesh of a graphic whose feature is off: the engine shares the helper between graphics.</summary>
        public static void NoMesh(VertexHelper mesh)
        {
            try
            {
                mesh?.Clear();
            }
            catch (Exception)
            {
                // nothing more to be done for the mesh
            }
        }

        /// <summary>An empty line of the game's panels: what a label that failed shows.</summary>
        public static LabelInfo Blank()
        {
            return new LabelInfo(string.Empty, new Color32(255, 255, 255, 255), false);
        }

        /// <summary>
        /// An activity of the mod's on the game's work panel whose feature is off: ended once by its own Finish, then
        /// Finished, so the panel closes it as it closes its own.
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

        /// <summary>
        /// A page of one of the mod's dialogs as it goes to DialogController.ShowEntry, wrapped once: its answers, and
        /// each page they lead to, run in their class's guard, and one that fails or is off ends with the game's own
        /// closing; its cancels and on-show are caught, never skipped.
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

        /// <summary>A maker of one of the mod's dialogs (CallDialogFactory.RegisterDialog): one that fails or is off gives the closing page.</summary>
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

        /// <summary>A way out the game calls, such as a dialog's cancel: caught, never skipped, whatever is off.</summary>
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
            int slot = HandlerSlot(way, "Callback");
            return value =>
            {
                using var hotSpot = HotSpots.Time(slot);
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
        /// A question the game asks later (a city map filter's: where is it), in its class's guard;
        /// <paramref name="otherwise"/> when it fails or is off.
        /// </summary>
        public static Func<T> Asked<T>(Func<T> ask, T otherwise)
        {
            if (ask == null || !Ours(ask))
            {
                return ask;
            }
            FeatureGuard guard = OfHandler(ask);
            int slot = HandlerSlot(ask, "Asked");
            return () =>
            {
                using var hotSpot = HotSpots.Time(slot);
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

        // A delegate whose method is not of an assembly the mod is built against: the game's own (FinishDialog) are left as they are.
        private static bool Ours(Delegate handler)
        {
            System.Reflection.Assembly home = handler?.Method.DeclaringType?.Assembly;
            if (home == null)
            {
                return false;
            }
            lock (Calls.Theirs)
            {
                if (!Calls.Theirs.TryGetValue(home, out bool theirs))
                {
                    if (Calls.Referenced == null)
                    {
                        Calls.Referenced = new System.Collections.Generic.HashSet<string>(StringComparer.Ordinal);
                        foreach (System.Reflection.AssemblyName name in typeof(Guard).Assembly.GetReferencedAssemblies())
                        {
                            Calls.Referenced.Add(name.Name);
                        }
                    }
                    theirs = home != typeof(Guard).Assembly && Calls.Referenced.Contains(home.GetName().Name);
                    Calls.Theirs[home] = theirs;
                }
                return !theirs;
            }
        }

        // The game's own closing, as the mod's dialogs close; null if even that fails.
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

        /// <summary>
        /// The options page as it goes to OptionsService.Register: the same rows, each callback caught and never skipped.
        /// The game calls them as it draws the page, so one that throws would cost the rest of the page.
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
                    return option;   // a header, a splitter, a row of the mod's own (its SpawnUi is guarded where written)
            }
        }

        /// <summary>As <see cref="Listener(UnityAction)"/>, for the engine's scene loaded event (two arguments).</summary>
        public static UnityAction<T1, T2> Listener<T1, T2>(UnityAction<T1, T2> handler)
        {
            if (handler == null)
            {
                return null;
            }
            FeatureGuard guard = OfHandler(handler);
            int slot = HandlerSlot(handler, "Listener");
            return (a, b) =>
            {
                using var hotSpot = HotSpots.Time(slot);
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

        // The statics that name the game's types, apart from Guard's own, so Guard starts whatever the game has.
        private static class Calls
        {
            public static readonly object Mark = new object();
            public static readonly ConditionalWeakTable<DialogEntry, object> Pages = new ConditionalWeakTable<DialogEntry, object>();
            public static readonly ConditionalWeakTable<IPlayerActivity, object> Ended = new ConditionalWeakTable<IPlayerActivity, object>();
            public static readonly System.Collections.Generic.Dictionary<System.Reflection.Assembly, bool> Theirs = new System.Collections.Generic.Dictionary<System.Reflection.Assembly, bool>();
            public static System.Collections.Generic.HashSet<string> Referenced;   // the assemblies the mod is built against
            private static int _rowFaults;

            // A cancel that failed still ends the dialog.
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
                    // the guard itself must never be the next fault
                }
            }
        }
    }
}
