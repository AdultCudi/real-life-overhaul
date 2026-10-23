using System.Globalization;
using BigAmbitions.Characters;
using Helpers;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>The cleaner's fallback without the game's dirt: the mod's own spills, mopped one at a time from a panel over each, paid per spill.</summary>
    public sealed partial class CleanerJob
    {
        private const float PatchReach = 1.1f;         // metres
        private const float PatchesApart = 2.4f;       // metres
        private const float MopSeconds = 5.5f;         // seconds a spill
        private const int MostTries = 4;               // looks for clear floor before calling the floor done
        private const string GameMop = "Mop";
        private const string PatchProp = "floor_patch";
        private const string BucketProp = "mop_bucket";

        private readonly Vector3[] _done = new Vector3[16];
        private readonly Vector3[] _laid = new Vector3[16];
        private readonly Transform[] _laidProp = new Transform[16];
        private readonly Vector3[] _taken = new Vector3[32];

        private int _mopped;           // spills mopped, by hand and by the clock
        private int _hand;             // ...of which by hand
        private int _tries;
        private Vector3 _patchAt;
        private bool _hasPatch;
        private Transform _patch;
        private Transform _bucket;
        private int _laidCount;
        private int _target = -1;
        private int _layTries;
        private float _nextLay;
        private float _mopFrom;
        private float _mopShown = -1f;
        private string _panelSpill;
        private string _panelFar;
        private string _panelLeft;
        private string _patchLabel;

        private void SpillTexts()
        {
            _patchLabel = JobText.T("reallife_job_cleaner_marker_patch");
            _panelSpill = JobText.T("reallife_job_cleaner_panel_spill");
            _panelFar = JobText.T("reallife_job_cleaner_panel_far");
            _panelLeft = JobText.T("reallife_job_cleaner_panel_left");
        }

        private void FloorSpills(float now)
        {
            if (_panelSpill == null)
            {
                SpillTexts();
                _nextLay = now;
            }
            if (_mopped >= _units)
            {
                Ctx.Say("reallife_job_cleaner_done", 7f);   // the last of them was done by the clock
                _panel.Hide();
                Conclude();
                return;
            }
            LayMore(now);
            if (_laidCount == 0)
            {
                _hasPatch = false;
                _panel.Hide();
                Unmark();
                if (_layTries >= MostTries)
                {
                    // No clear floor left in a shop this size: what was done is the job.
                    _tries = MostTries;
                    Ctx.Say("reallife_job_cleaner_small", 7f);
                    Conclude();
                }
                return;
            }
            // The nearest of them is offered.
            Vector3 here = Ctx.Player.transform.position;
            int nearest = 0;
            float best = float.MaxValue;
            for (int i = 0; i < _laidCount; i++)
            {
                float flat = JobSpots.Flat(here, _laid[i]);
                if (flat < best)
                {
                    best = flat;
                    nearest = i;
                }
            }
            _target = nearest;
            _patchAt = _laid[nearest];
            _patch = _laidProp[nearest];
            _hasPatch = true;
            Mark(_patchAt, _patchLabel);
            string left = JobText.Number(_units - _mopped);
            bool onIt = JobSpots.Flat(Ctx.Player.transform.position, _patchAt) < PatchReach;
            if (onIt)
            {
                bool hands = PlayerHelper.IsHoldingItem;
                _panel.Offer(hands ? _panelHands : _panelSpill, _panelLeft, left, _panel.White, ButtonMop, !hands, hands ? null : ButtonSkip, true);
                _panel.Show(_patchAt + Vector3.up * PanelUp);
                Hint(hands ? 4 : 2);
            }
            else
            {
                // Not on it yet, however far: the panel stands over the spill to walk to (at the screen's edge toward it while it is
                // out of the picture, as indoors the game's guider is hidden), its button off until they are.
                _panel.Offer(_panelFar, _panelLeft, left, _panel.White, ButtonMop, false, null, false);
                _panel.Show(_patchAt + Vector3.up * PanelUp);
                Hint(2);
            }
        }

        // One more spill on clear floor away from the others, a quarter of a second apart, until all are down or there is no room.
        private void LayMore(float now)
        {
            if (now < _nextLay || _mopped + _laidCount >= _units || _laidCount >= _laid.Length || _layTries >= MostTries)
            {
                return;
            }
            _nextLay = now + 0.25f;
            int taken = 0;
            for (int i = 0; i < _hand && i < _done.Length && taken < _taken.Length; i++)
            {
                _taken[taken++] = _done[i];
            }
            for (int i = 0; i < _laidCount && taken < _taken.Length; i++)
            {
                _taken[taken++] = _laid[i];
            }
            if (!JobSites.FloorSpot(Ctx.Player.transform.position, _taken, taken, PatchesApart, out Vector3 spot))
            {
                _layTries++;
                _nextLay = now + 1f;
                return;
            }
            _layTries = 0;
            Transform prop = Ctx.Props.Take(PatchProp);
            if (prop != null)
            {
                prop.localScale = Vector3.one;
                prop.SetPositionAndRotation(spot + Vector3.up * 0.004f, Quaternion.Euler(0f, Random.Range(0f, 360f), 0f));
            }
            _laid[_laidCount] = spot;
            _laidProp[_laidCount] = prop;
            _laidCount++;
            // The bucket stands by the first of them, where there is floor for it.
            if (_bucket == null)
            {
                Vector3 beside = spot + Quaternion.Euler(0f, Random.Range(0f, 360f), 0f) * Vector3.forward * 0.75f;
                if (ShopFloor.Standable(beside, 0.3f, out Vector3 stood) && JobSpots.Flat(stood, spot) > 0.55f)
                {
                    _bucket = Ctx.Props.Take(BucketProp);
                    if (_bucket != null)
                    {
                        _bucket.SetPositionAndRotation(stood, Quaternion.identity);
                    }
                }
            }
            if (Meta.Dev.Verbose && _mopped + _laidCount >= _units)
            {
                Ctx.Logger?.Info("Jobs: cleaner: " + _laidCount + " spill(s) lie on the floor at once (" + _mopped + " of " + _units + " already done).");
            }
        }

        // The spill mopped goes from the floor; the others stay where they lie.
        private void Lift(int index)
        {
            if (index < 0 || index >= _laidCount)
            {
                return;
            }
            if (_laidProp[index] != null)
            {
                Ctx.Props.Return(_laidProp[index]);
            }
            _laidCount--;
            _laid[index] = _laid[_laidCount];
            _laidProp[index] = _laidProp[_laidCount];
            _laidProp[_laidCount] = null;
            _patch = null;
            _target = -1;
        }

        // The panel's Mop it up, over a spill.
        private void ActMop()
        {
            if (_mode != Mode.Spills || _step != Step.Floor || !_hasPatch || PlayerHelper.IsHoldingItem)
            {
                return;
            }
            _panel.Hide();
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: cleaner: mopping spill " + (_mopped + 1) + " of " + _units + ", "
                                 + JobSpots.Flat(Ctx.Player.transform.position, _patchAt).ToString("F1", CultureInfo.InvariantCulture)
                                 + " m from its middle; " + Mathf.Max(0, Mathf.CeilToInt(_endsAt - Time.time)) + " s of the floor's time left.");
            }
            float now = Time.time;
            // Held on the patch and mopping: the mod's clip and mop if the bundle has them, else the game's own.
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
            _mopShown = -1f;
            _step = Step.Mopping;
            Unmark();
            Hint(3);
        }

        private void Mopping(float now)
        {
            float share = Mathf.Clamp01((now - _mopFrom) / MopSeconds);
            // The patch shrinks as it is mopped, pushed only by a visible step.
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
            if (_hand < _done.Length)
            {
                _done[_hand] = _patchAt;
            }
            _hand++;
            _mopped++;
            _mopOver = now;
            _hint = -1;   // the count in the line has changed
            StopMopping();
            Lift(_target);
            _hasPatch = false;
            _step = Step.Floor;
            if (Meta.Dev.Verbose)
            {
                Ctx.Logger?.Info("Jobs: cleaner: spill mopped by hand in " + MopSeconds.ToString("F1", CultureInfo.InvariantCulture) + " s; mopped " + _mopped + " of " + _units + ", "
                                 + Mathf.Max(0, Mathf.CeilToInt(_endsAt - now)) + " s of the floor's time left.");
            }
            if (_mopped >= _units)
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
            for (int i = 0; i < _laidCount; i++)
            {
                if (_laidProp[i] != null)
                {
                    Ctx.Props.Return(_laidProp[i]);
                    _laidProp[i] = null;
                }
            }
            _laidCount = 0;
            _layTries = 0;
            _target = -1;
            _patch = null;
            if (_bucket != null)
            {
                Ctx.Props.Return(_bucket);
                _bucket = null;
            }
        }
    }
}
