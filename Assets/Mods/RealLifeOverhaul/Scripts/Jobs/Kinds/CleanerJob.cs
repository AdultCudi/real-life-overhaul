using BigAmbitions.Characters;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// Contract cleaner. A business near the player that is not theirs, and is open, has spills
    /// on its floor: walk to its door, go in, and mop each patch in turn. A patch is one of the
    /// mod's own (a flat mark on a clear piece of the shop's real floor, with a bucket beside
    /// it), not one of the game's dirt spots, which belong to the owner's own cleaning. Stand on
    /// a patch and mop: the patch shrinks under the mop and is gone in a few seconds. Paid per
    /// patch, a little more for the whole floor.
    ///
    /// The mopping is played on the player, who is held on the spot for it (the game's own
    /// blocker), in this order of preference:
    ///  - the mod's <c>mop_floor</c> clip with the modelled <c>mop</c> at the clip's grip;
    ///  - the game's own cleaning animation with the game's own mop in the hand, exactly as its
    ///    cleaners and the player's own mop use them.
    /// If the player's animator has neither, the job is not offered here.
    ///
    /// The shift goes on inside that one building (<see cref="WorksInside"/>); going into any
    /// other ends it, and the player may step out and come back.
    /// </summary>
    public sealed class CleanerJob : Job
    {
        private enum Step { ToClient, Floor, Mopping }

        private const float ClientMin = 15f;
        private const float ClientMax = 260f;
        private const float DoorReach = 3.2f;
        private const float PatchReach = 1.1f;
        private const float PatchesApart = 2.4f;
        private const float MopSeconds = 5.5f;
        private const float Seconds = 330f;            // for the whole floor, the walk there included
        private const float SecondsPerMetre = 0.5f;    // more for a client further off
        private const int MostTries = 4;               // looks for the next clear piece of floor before calling the floor done
        private const string MopClip = "mop_floor";
        private const string GameMop = "Mop";
        private const string PatchProp = "floor_patch";
        private const string BucketProp = "mop_bucket";

        private readonly JobDef _def = JobCatalogue.Find(JobCatalogue.Cleaner);
        private readonly Vector3[] _done = new Vector3[16];

        private Step _step;
        private DoorSpot _client;
        private string _clientName;
        private int _patches;          // how many the shift is
        private int _mopped;
        private float _endsAt;
        private Vector3 _patchAt;
        private bool _hasPatch;
        private int _tries;
        private float _nextTry;
        private Transform _patch;
        private Transform _bucket;
        private float _mopFrom;
        private float _mopShown = -1f;
        private bool _wasInside;
        private bool _paid;
        private int _hint = -1;
        private string _doorLabel;
        private string _patchLabel;
        private string _promptEnter;
        private string _promptMop;

        public override JobDef Def => _def;

        /// <summary>Walking out on a floor that is mostly still dirty is a bad shift.</summary>
        public override bool QuitIsFailure => _mopped > 0 && _mopped * 2 < _patches;

        public override bool WorksInside(Buildings.Building building)
        {
            return _client.Building != null && ReferenceEquals(_client.Building.building, building);
        }

        protected override bool OnBegin(float now)
        {
            if (Ctx.Driving || PlayerHelper.IsUsingVehicle)
            {
                Refusal = "reallife_job_on_foot";
                return false;
            }
            if (!(ClipLibrary.Ready && ClipLibrary.TryGet(MopClip, out _)) && !Ctx.Self.Knows(PermanentAnimationType.Cleaning))
            {
                Refusal = "reallife_job_no_here";
                Ctx.Logger?.Info("Jobs: no cleaning work: neither the mop clip nor the game's own cleaning animation can be played on the player.");
                return false;
            }
            _patches = Ctx.FullShift ? Def.ShiftUnits : Mathf.Min(Def.ShiftUnits, JobState.LeftToday(Def));
            if (_patches <= 0)
            {
                Refusal = "reallife_job_no_daily";
                return false;
            }
            if (!JobSpots.RandomDoor(Ctx.PlayerPosition, ClientMin, ClientMax, null, 0f, out _client, JobSites.AnyClient, 96))
            {
                Refusal = "reallife_job_no_client";
                return false;
            }
            _clientName = JobSites.NameOf(_client);
            _doorLabel = JobText.T("reallife_job_cleaner_marker_door");
            _patchLabel = JobText.T("reallife_job_cleaner_marker_patch");
            _promptEnter = JobText.T("reallife_job_prompt_enter");
            _promptMop = JobText.T("reallife_job_cleaner_prompt_mop");
            _endsAt = now + Seconds + JobSpots.Flat(Ctx.PlayerPosition, _client.Front) * SecondsPerMetre;
            _step = Step.ToClient;
            Mark(_client.Front, _doorLabel);
            Hint(0);
            Ctx.Logger?.Info("Jobs: cleaning at " + _clientName + " (" + _client.Name + "), " + _patches + " patches.");
            return true;
        }

        public override void Tick(float now)
        {
            ClockSeconds = Mathf.CeilToInt(_endsAt - now);
            if (now >= _endsAt && _step != Step.Mopping)
            {
                Ctx.Say("reallife_job_cleaner_time", 8f);
                Conclude();
                return;
            }
            bool inside = JobSites.Inside(_client);
            if (inside != _wasInside)
            {
                _wasInside = inside;
                _hint = -1;
                if (!inside)
                {
                    StopMopping();
                    ClearPatch();   // the floor they left is not the street's: found again when they are back in
                    _hasPatch = false;
                    _step = Step.ToClient;
                }
                else
                {
                    _step = Step.Floor;
                    _nextTry = now + 0.6f;   // a moment for the shop to finish arriving round them
                }
            }
            switch (_step)
            {
                case Step.ToClient: ToClient(); break;
                case Step.Floor: Floor(now); break;
                case Step.Mopping: Mopping(now); break;
            }
        }

        private void ToClient()
        {
            Mark(_client.Front, _doorLabel);
            Hint(_mopped > 0 ? 1 : 0);
            bool atDoor = !Ctx.Driving && !PlayerHelper.IsUsingVehicle && JobSpots.Flat(Ctx.PlayerPosition, _client.Front) < DoorReach;
            Prompt = atDoor ? _promptEnter : null;
        }

        private void Floor(float now)
        {
            if (!_hasPatch)
            {
                Prompt = null;
                Unmark();
                if (now < _nextTry)
                {
                    return;
                }
                _nextTry = now + 1f;
                if (!NextPatch())
                {
                    if (++_tries >= MostTries)
                    {
                        // No clear floor left to work on in a shop this size: what was done is the job.
                        Ctx.Say("reallife_job_cleaner_small", 7f);
                        Conclude();
                    }
                    return;
                }
                _tries = 0;
                Hint(2);
            }
            Mark(_patchAt, _patchLabel);
            bool onIt = JobSpots.Flat(Ctx.Player.transform.position, _patchAt) < PatchReach;
            Prompt = onIt && !PlayerHelper.IsHoldingItem ? _promptMop : null;
            if (onIt && PlayerHelper.IsHoldingItem)
            {
                Hint(4);
            }
            else
            {
                Hint(2);
            }
        }

        // A clear piece of the shop's floor the player can walk to, away from the patches already done.
        private bool NextPatch()
        {
            if (!JobSites.FloorSpot(Ctx.Player.transform.position, _done, Mathf.Min(_mopped, _done.Length), PatchesApart, out _patchAt))
            {
                return false;
            }
            ClearPatch();
            _patch = Ctx.Props.Take(PatchProp);
            if (_patch != null)
            {
                _patch.SetPositionAndRotation(_patchAt + Vector3.up * 0.004f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
                _mopShown = -1f;
            }
            // The bucket stands at the edge of the patch, if there is floor for it there.
            Vector3 beside = _patchAt + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 0.75f;
            if (ShopFloor.Standable(beside, 0.3f, out Vector3 stood) && JobSpots.Flat(stood, _patchAt) > 0.55f)
            {
                _bucket = Ctx.Props.Take(BucketProp);
                if (_bucket != null)
                {
                    _bucket.SetPositionAndRotation(stood, Quaternion.identity);
                }
            }
            _hasPatch = true;
            return true;
        }

        public override void Interact(float now)
        {
            if (Prompt == null)
            {
                return;
            }
            string prompt = Prompt;
            Prompt = null;
            if (ReferenceEquals(prompt, _promptEnter))
            {
                if (_client.Building != null)
                {
                    _client.Building.Interact();   // the game's own way in: it says so itself if the business has just shut
                }
                return;
            }
            if (_step != Step.Floor || !_hasPatch)
            {
                return;
            }
            // Held on the patch, facing across it, and mopping: the mod's clip and mop if the bundle has them, else the game's own.
            Ctx.Self.Hold();
            bool mopping = Ctx.Self.Clip(MopClip);
            if (!mopping && Ctx.Self.Pose(PermanentAnimationType.Cleaning, PermanentAnimationType.CleaningIdle))
            {
                mopping = true;
                Ctx.Self.GameProp(GameMop, Ctx.Player.Character.rightHand);
            }
            if (!mopping)
            {
                Ctx.Self.Free();
                Ctx.Say("reallife_job_cleaner_no_mop", 7f);
                Conclude();
                return;
            }
            _mopFrom = now;
            _step = Step.Mopping;
            Unmark();
            Hint(3);
        }

        private void Mopping(float now)
        {
            float share = Mathf.Clamp01((now - _mopFrom) / MopSeconds);
            // The patch goes as it is mopped: pushed only when it has shrunk by a visible step.
            if (_patch != null && share - _mopShown >= 0.1f)
            {
                _mopShown = share;
                float left = Mathf.Max(0.05f, 1f - share);
                _patch.localScale = new Vector3(left, 1f, left);
            }
            if (share < 1f)
            {
                return;
            }
            if (_mopped < _done.Length)
            {
                _done[_mopped] = _patchAt;
            }
            _mopped++;
            _hint = -1;   // the count in the line has changed
            StopMopping();
            ClearPatch();
            _hasPatch = false;
            _step = Step.Floor;
            _nextTry = now + 0.4f;
            if (_mopped >= _patches)
            {
                Ctx.Say("reallife_job_cleaner_done", 7f);
                Conclude();
            }
        }

        private void StopMopping()
        {
            if (_step == Step.Mopping || Ctx.Self.Holding)
            {
                Ctx.Self.Rest();
                Ctx.Self.Free();
            }
        }

        private void ClearPatch()
        {
            if (_patch != null)
            {
                Ctx.Props.Return(_patch);
                _patch = null;
            }
            if (_bucket != null)
            {
                Ctx.Props.Return(_bucket);
                _bucket = null;
            }
        }

        // The line under the job's name, rebuilt only when what it says changes.
        private void Hint(int which)
        {
            if (which == _hint)
            {
                return;
            }
            _hint = which;
            switch (which)
            {
                case 0: Show(JobText.T("reallife_job_cleaner_obj_go", "place", _clientName)); break;
                case 1: Show(JobText.T("reallife_job_cleaner_obj_back", "place", _clientName)); break;
                case 2: Show(JobText.T("reallife_job_cleaner_obj_floor", "done", _mopped.ToString(), "count", _patches.ToString())); break;
                case 3: Show(JobText.T("reallife_job_cleaner_obj_mopping")); break;
                default: Show(JobText.T("reallife_job_obj_hands")); break;
            }
        }

        private void Conclude()
        {
            Settle();
            // A floor with no room to work on is nobody's fault; half the patches or more is a shift done.
            Finish(_mopped * 2 >= _patches || _tries >= MostTries ? JobEnd.Done : JobEnd.Failed);
        }

        private void Settle()
        {
            if (_paid || _mopped <= 0 || Ctx == null)
            {
                return;
            }
            _paid = true;
            int each = UnitPay();
            var slip = new PaySlip
            {
                Units = _mopped,
                Base = _mopped * each,
                Bonus = _mopped >= _patches ? Mathf.RoundToInt(_mopped * each * 0.1f) : 0   // the whole floor
            };
            Ctx.Pay(this, slip, "reallife_job_cleaner_slip");
        }

        public override void End(JobEnd why)
        {
            if (Ctx == null)
            {
                return;
            }
            Prompt = null;
            Ctx.Self.Rest();
            Ctx.Self.Free();
            ClearPatch();
            if (why != JobEnd.Fault)
            {
                Settle();
            }
        }
    }
}
