using System;
using System.Collections.Generic;
using BAModAPI;
using BigAmbitions.Characters;
using Controllers;
using UnityEngine;

namespace RealLifeOverhaul.Economy
{
    /// <summary>
    /// A bank branch's floor while the player is in one of their branches: the hour's customers come in at the door, queue at
    /// a till where a teller (one of the game's own staff, or the player) stands, are seen to and leave; those nobody sees to
    /// wait and go. It shows what BranchRules.Hour booked and changes nothing. Played by the mod's people (People/ActorPool)
    /// as the Car Showroom's visitors are. Off elsewhere; inside, a time comparison a frame and four times a second a pass
    /// over at most three people. Local only in Going Public.
    /// </summary>
    public sealed class BranchFloor : MonoBehaviour
    {
        private static readonly FeatureGuard FrameGuard = Guard.For("economy_bank_branch", "BranchFloor", typeof(BranchFloor));

        private const float Beat = 0.25f;          // seconds between two looks at the people on the floor
        private const float Behind = 0.8f;         // metres between two customers in a queue
        private const int Late = 15;               // minutes after their minute a customer still comes in

        public static IModLogger Logger;
        public static BranchFloor Instance { get; private set; }

        private enum Step { Coming, Waiting, Served, Leaving }

        private sealed class Customer
        {
            public readonly ActorRole Role = new ActorRole();
            public FloorCustomer Plan;
            public int Till = -1;        // the till they queue at; -1 for none
            public int Place;            // their place in its queue
            public Step Step;
            public float Until;

            public bool Out => Role.Present;
        }

        private readonly Customer[] _customers = new Customer[BranchCatalogue.OnFloorMost];
        private readonly List<FloorCustomer> _plan = new List<FloorCustomer>(BranchCatalogue.ShownMost);
        private readonly List<CashRegisterController> _tills = new List<CashRegisterController>(4);
        private readonly List<Vector3> _fronts = new List<Vector3>(4);
        private readonly List<Vector3> _backs = new List<Vector3>(4);    // the way a queue grows from its till's front
        private BankBranch _branch;
        private Vector3 _door, _outside;
        private int _hour = -1;
        private int _next;
        private float _nextBeat;
        private int _shown, _skipped;

        private void Awake()
        {
            Instance = this;
            for (int i = 0; i < _customers.Length; i++)
            {
                _customers[i] = new Customer();
            }
            enabled = false;   // nobody to look after until the player walks into one of their branches
        }

        private void OnDestroy()
        {
            Clear();
            if (Instance == this)
            {
                Instance = null;
            }
        }

        /// <summary>The branch the player stands in (null: none of theirs), told once a second; nothing unless it changed.</summary>
        public void Stand(BankBranch br)
        {
            if (br == _branch)
            {
                return;
            }
            Clear();
            if (br == null)
            {
                return;
            }
            try
            {
                Begin(br);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
                Clear();
            }
        }

        private void Begin(BankBranch br)
        {
            BranchGame.Tills(_tills);
            Vector3 middle = _tills.Count > 0 ? _tills[0].transform.position : Vector3.zero;
            if (!ShopFloor.Door(middle, out _door, out _outside))
            {
                Logger?.Warn("Bank branch: no door of '" + br.Name + "' could be stood at, so no customers are shown on its floor.");
                return;
            }
            _branch = br;
            Fronts();
            Replan(br, true);
            enabled = true;
            if (Meta.Dev.Verbose)
            {
                Logger?.Info("Bank branch check (floor, '" + br.Name + "'): " + _plan.Count + " customer(s) of this hour to be seen, " + _tills.Count + " till(s), "
                             + _fronts.Count + " with a place in front to queue at: PASS.");
            }
        }

        // Where customers stand at each till: across it from its teller, and the way its queue grows.
        private void Fronts()
        {
            _fronts.Clear();
            _backs.Clear();
            for (int i = 0; i < _tills.Count; i++)
            {
                if (BranchGame.Front(_tills[i], out Vector3 front, out Vector3 teller))
                {
                    Vector3 back = front - teller;
                    back.y = 0f;
                    _fronts.Add(front);
                    _backs.Add(back.sqrMagnitude > 0.01f ? back.normalized : Vector3.forward);
                }
                else
                {
                    _fronts.Add(Vector3.zero);
                    _backs.Add(Vector3.zero);
                }
            }
        }

        /// <summary>A new hour of the branch the player stands in has been worked: what it brought is shown.</summary>
        public void NewHour(BankBranch br)
        {
            if (br != _branch || _branch == null)
            {
                return;
            }
            try
            {
                Replan(br, false);
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }

        private void Replan(BankBranch br, bool walkingIn)
        {
            OwnBankDirector director = OwnBankDirector.Instance;
            OwnBank bank = director != null ? director.Bank : null;
            if (bank == null)
            {
                return;
            }
            if (_hour >= 0 && Meta.Dev.Verbose)
            {
                Logger?.Info("Bank branch check (floor, hour " + _hour % 24 + "): " + _shown + " of " + _plan.Count + " customer(s) planned were seen, " + _skipped
                             + " left out for want of room or of people: " + (_shown + _skipped <= _plan.Count ? "PASS" : "FAIL") + ".");
            }
            _hour = br.HourIndex;
            _shown = _skipped = 0;
            BranchRules.Floor(bank.Seed, br.HourIndex, br.Place, br.HourOpen ? br.HourVisitors : 0, br.HourServed, bank.Run, _plan);
            _next = 0;
            if (walkingIn)
            {
                int minute = Minute;
                while (_next < _plan.Count && _plan[_next].Minute + Late < minute)
                {
                    _next++;   // what came well before the player did is past
                }
            }
        }

        private static int Minute => SaveGameManager.Current != null ? Mathf.Clamp((int)SaveGameManager.Current.Minute, 0, 59) : 0;
        private static int HourIndex => SaveGameManager.Current != null ? SaveGameManager.Current.Day * 24 + SaveGameManager.Current.Hour : 0;

        // Everyone off the floor, and the component off.
        private void Clear()
        {
            for (int i = 0; i < _customers.Length; i++)
            {
                _customers[i].Role.Exit();
                _customers[i].Step = Step.Leaving;
                _customers[i].Till = -1;
            }
            _branch = null;
            _plan.Clear();
            _tills.Clear();
            _fronts.Clear();
            _backs.Clear();
            _hour = -1;
            _next = 0;
            enabled = false;
        }

        private static readonly int HotUpdate = HotSpots.Slot("Economy: BranchFloor.Update");
        private void Update()
        {
            using var hotSpot = HotSpots.Time(HotUpdate);
            if (FrameGuard.Off)
            {
                enabled = false;   // its feature was switched off after its faults
                return;
            }
            try
            {
                OnUpdate();
            }
            catch (Exception e)
            {
                Guard.Fail(FrameGuard, e, this);
            }
        }

        private void OnUpdate()
        {
            float now = Time.time;
            if (now < _nextBeat)
            {
                return;
            }
            _nextBeat = now + Beat;
            if (_branch == null || !Settings.EconomyBranch)
            {
                Clear();
                return;
            }
            Admit(now);
            for (int i = 0; i < _customers.Length; i++)
            {
                Tend(_customers[i], now);
            }
        }

        // Customers whose minute has come, while there is room on the floor; one who waited too long for room is left out.
        private void Admit(float now)
        {
            if (_next >= _plan.Count || HourIndex != _hour)
            {
                return;   // the next hour's plan comes with NewHour, once the branch's hour has been worked
            }
            FloorCustomer c = _plan[_next];
            int minute = Minute;
            if (minute < c.Minute)
            {
                return;
            }
            if (minute - c.Minute > Late)
            {
                _skipped++;
                _next++;
                return;
            }
            Customer free = Free();
            if (free == null)
            {
                return;   // waits for room
            }
            if (!free.Role.EnterPresence(_door, ActorLook.Civilian, 1.5f, false))
            {
                _skipped++;   // room, but nobody to be had from the people pool
                _next++;
                return;
            }
            free.Plan = c;
            _shown++;
            _next++;
            Queue(free, now);
        }

        private Customer Free()
        {
            for (int i = 0; i < _customers.Length; i++)
            {
                if (!_customers[i].Out)
                {
                    return _customers[i];
                }
            }
            return null;
        }

        // To the end of the shortest queue at a staffed till; with none, somewhere on the floor to wait.
        private void Queue(Customer c, float now)
        {
            int best = -1, bestLength = int.MaxValue;
            for (int t = 0; t < _tills.Count; t++)
            {
                if (_fronts[t] == Vector3.zero || !BranchGame.Staffed(_tills[t]))
                {
                    continue;
                }
                int length = Waiting(t);
                if (length < bestLength)
                {
                    best = t;
                    bestLength = length;
                }
            }
            c.Till = best;
            c.Step = Step.Coming;
            c.Role.Quiet();
            if (best < 0)
            {
                Vector3 spot = ShopFloor.RandomSpot(out Vector3 random) ? random : _door;
                c.Role.Go(spot, Pace.Walk, _tills.Count > 0 ? _tills[0].transform.position : _door, 25f);
                c.Until = now + 30f;
                return;
            }
            c.Place = bestLength;
            Vector3 at = _fronts[best] + _backs[best] * (Behind * c.Place);
            c.Role.Go(at, Pace.Walk, _tills[best].transform.position, 25f);
            c.Until = now + 30f;
        }

        private int Waiting(int till)
        {
            int n = 0;
            for (int i = 0; i < _customers.Length; i++)
            {
                Customer c = _customers[i];
                n += c.Out && c.Till == till && c.Step != Step.Leaving ? 1 : 0;
            }
            return n;
        }

        private void Tend(Customer c, float now)
        {
            c.Role.Tick(now);
            if (!c.Out)
            {
                return;
            }
            switch (c.Step)
            {
                case Step.Coming:
                    if (c.Role.Settled || now > c.Until)
                    {
                        c.Step = Step.Waiting;
                        c.Until = now + BranchCatalogue.WaitSeconds;
                    }
                    break;

                case Step.Waiting:
                    if (c.Till >= 0 && c.Place > 0 && Ahead(c) < c.Place)
                    {
                        c.Place = Ahead(c);   // the queue moved up
                        c.Role.Go(_fronts[c.Till] + _backs[c.Till] * (Behind * c.Place), Pace.Walk, _tills[c.Till].transform.position, 25f);
                        c.Step = Step.Coming;
                        c.Until = now + 20f;
                        break;
                    }
                    if (c.Plan.Served && c.Till >= 0 && c.Place == 0 && BranchGame.Staffed(_tills[c.Till]))
                    {
                        c.Step = Step.Served;
                        c.Until = now + BranchCatalogue.ServeSeconds;
                        c.Role.Repeat(AnimationType.PressAction, AnimationType.IDontThinkSo, 5f, 9f);
                        break;
                    }
                    if (now >= c.Until)
                    {
                        c.Role.Emote(CharacterEmojiName.CustomerCantFindEmployeeOrRegister, 3f);   // the game's own face for a customer nobody saw to
                        Leave(c);
                    }
                    break;

                case Step.Served:
                    if (now >= c.Until)
                    {
                        c.Role.Emote(c.Plan.Kind == BranchCatalogue.Loan ? CharacterEmojiName.CommonGreenHappy : CharacterEmojiName.CustomerGoodService, 3f);
                        Leave(c);
                    }
                    break;
            }
        }

        // How many still queue in front of a customer at their till.
        private int Ahead(Customer c)
        {
            int n = 0;
            for (int i = 0; i < _customers.Length; i++)
            {
                Customer o = _customers[i];
                n += o != c && o.Out && o.Till == c.Till && o.Step != Step.Leaving && o.Place < c.Place ? 1 : 0;
            }
            return n;
        }

        private void Leave(Customer c)
        {
            c.Step = Step.Leaving;
            c.Role.Quiet();
            c.Role.WalkOff(_outside, Pace.Walk, 40f);
            c.Till = -1;
        }

        /// <summary>For Options > Tests: a busy hour's customers come in now; only the floor is shown, no figure changes.</summary>
        public void TestFloor()
        {
            const string test = "Customers at my branch";
            try
            {
                OwnBankDirector director = OwnBankDirector.Instance;
                BankBranch br = director != null ? director.BranchInside() : null;
                if (br == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_branch_test_not_inside");
                    return;
                }
                if (_branch != br)
                {
                    Stand(br);
                }
                if (_branch == null)
                {
                    TestRefusals.TellKey(Logger, test, "reallife_branch_test_no_door");
                    return;
                }
                OwnBank bank = director.Bank;
                BranchRules.Floor(bank.Seed, HourIndex + 7919, br.Place, BranchCatalogue.ShownMost, BranchCatalogue.ShownMost * 3 / 4, false, _plan);
                for (int i = 0; i < _plan.Count; i++)
                {
                    FloorCustomer c = _plan[i];
                    c.Minute = Minute;   // every one of them due now: they come in as room is made
                    _plan[i] = c;
                }
                _hour = HourIndex;
                _next = 0;
                _shown = _skipped = 0;
                Fronts();
                enabled = true;
                Logger?.Info("Bank branch: a test hour of " + _plan.Count + " customer(s) on the floor of '" + br.Name + "' (" + _tills.Count
                             + " till(s)); nothing of the bank's figures is changed.");
            }
            catch (Exception e)
            {
                Logger?.Error(e);
            }
        }
    }
}
