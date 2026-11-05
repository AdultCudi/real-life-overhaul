using System;
using System.Collections.Generic;
using RealLifeOverhaul;
using RealLifeOverhaul.Nav;

// The wraps the mod puts on the game's queue of work on the city map's buildings (Core/WorkQueueStep, used by NavMapFollow and ClaimedMap), run against replicas of the game's
// queue class: the step it runs for each item in a private field. The game's own step always runs; each wrap does its part once an item, in either order of the two wraps;
// a queue the contract gives up, one without the field, one without a step and one holding a step of another kind of item are left as they are.
internal static partial class Program
{
    private class ReplicaQueue<T>
    {
        private Action<T> _updateAction;

        public ReplicaQueue(Action<T> step)
        {
            _updateAction = step;
        }

        public void Work(T item) => _updateAction(item);
    }

    private sealed class ReplicaDerived<T> : ReplicaQueue<T>
    {
        public ReplicaDerived(Action<T> step) : base(step)
        {
        }
    }

    private sealed class ReplicaWithoutField
    {
    }

    private static void WorkQueueChecks()
    {
        // the game's step, and the part each wrap does after it
        int game = 0, claimed = 0;
        Func<Action<int>, Action<int>> claim = step => item =>
        {
            step(item);
            claimed++;
        };
        int seen = NavMapWork.Items;
        var follow = new ReplicaQueue<int>(item => game++);
        bool a1 = WorkQueueStep.Wrap<int>(follow, false, NavMapWork.Wrap);
        bool a2 = WorkQueueStep.Wrap<int>(follow, false, claim);
        for (int i = 0; i < 25; i++)
        {
            follow.Work(i);
        }
        Check(a1 && a2 && game == 25 && claimed == 25 && NavMapWork.Items - seen == 25,
            "city map queue: the count wrapped first and the mod's rows wrapped over it, the game's own step runs for every item and each wrap does its part once an item (" + game + ", " + claimed + ", " + (NavMapWork.Items - seen) + ")");
        game = claimed = 0;
        seen = NavMapWork.Items;
        var rows = new ReplicaQueue<int>(item => game++);
        bool b1 = WorkQueueStep.Wrap<int>(rows, false, claim);
        bool b2 = WorkQueueStep.Wrap<int>(rows, false, NavMapWork.Wrap);
        for (int i = 0; i < 25; i++)
        {
            rows.Work(i);
        }
        Check(b1 && b2 && game == 25 && claimed == 25 && NavMapWork.Items - seen == 25,
            "city map queue: the same the other way round (the mod's rows first, the count over them), whichever sets itself up first (" + game + ", " + claimed + ", " + (NavMapWork.Items - seen) + ")");

        // the wrap is asked for the game's step itself, and not asked at all of a queue it leaves alone
        int asked = 0;
        Func<Action<int>, Action<int>> count = step =>
        {
            asked++;
            return step;
        };
        game = 0;
        var off = new ReplicaQueue<int>(item => game++);
        bool contractOff = WorkQueueStep.Wrap<int>(off, true, count);
        off.Work(1);
        var none = new ReplicaQueue<int>(null);
        bool noStep = WorkQueueStep.Wrap<int>(none, false, count);
        bool noQueue = WorkQueueStep.Wrap<int>(null, false, count);
        bool noField = WorkQueueStep.Wrap<int>(new ReplicaWithoutField(), false, count);
        bool otherItems = WorkQueueStep.Wrap<int>(new ReplicaQueue<string>(item => { }), false, count);
        Check(!contractOff && !noStep && !noQueue && !noField && !otherItems && asked == 0 && game == 1,
            "city map queue: a queue the contract gives up, one with no step, no queue, a class without the field and a step of another kind of item are left as they are, and the wrap is never asked");

        // the field a class derived from the game's holds is found on the class that declares it
        game = claimed = 0;
        var derived = new ReplicaDerived<int>(item => game++);
        bool found = WorkQueueStep.Wrap<int>(derived, false, claim);
        derived.Work(1);
        Check(found && game == 1 && claimed == 1, "city map queue: a queue of a class derived from the game's is wrapped through the field its base declares");

        // a wrap that hands back what it was given (a step that is ours already) leaves the queue working
        game = 0;
        var again = new ReplicaQueue<int>(item => game++);
        bool first = WorkQueueStep.Wrap<int>(again, false, count);
        again.Work(1);
        Check(first && game == 1 && asked == 1, "city map queue: a wrap may hand the step back as it was, and the queue goes on with it");
        var kinds = new List<string>();
        Check(WorkQueueStep.Field == "_updateAction" && kinds.Count == 0, "city map queue: the field is the game's DistributedWork step");
    }
}
