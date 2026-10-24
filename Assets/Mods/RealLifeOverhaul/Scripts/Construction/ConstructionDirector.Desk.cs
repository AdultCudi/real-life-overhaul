using System.Collections.Generic;
using UnityEngine;

namespace RealLifeOverhaul.Construction
{
    /// <summary>
    /// The director's part for the front desks (docs/features/construction.md, "The front desk"): in a rival builder's office
    /// in Uptown (while the district and that office stand at this load, and never a unit the player or another player rents
    /// or owns) its receptionist, and in an office of the player's own company its office manager, each the game's own staff
    /// at the workstation nearest the door, to be talked to through the game's own call to action (BuilderDeskGame) in the
    /// game's dialogue panel (BuilderDesk). Once a second: where the player is, one comparison while nothing changed; a
    /// look for the desk's person when they come in, and again now and then while nobody sits there.
    /// </summary>
    public sealed partial class ConstructionDirector
    {
        private const int DeskLooksFirst = 8;        // looks a second apart after coming in: the game seats its staff as the player arrives
        private const float DeskLookAgain = 15f;     // s between later looks while nobody sits at the front desk

        private readonly TenderBid[] _deskQuotes = new TenderBid[BuildCatalogue.RivalCount];
        private readonly List<BuildingView> _deskOwned = new List<BuildingView>(4);
        private BuilderDeskPointer _deskPointer;
        private string _deskAt;
        private int _deskRival = -1, _deskLooks;
        private float _deskNext;
        private bool _deskTold;

        /// <summary>The front desk the player is at now, if one stands: its pointer (null when nobody sits there yet).</summary>
        internal BuilderDeskPointer FrontDesk => _deskPointer != null ? _deskPointer : null;

        // Once a second while running: the desk of the office the player is in kept up, or taken away.
        private void DeskSecond()
        {
            string at = BuilderDeskGame.Inside();
            if (at != _deskAt)
            {
                DropDesk();
                _deskAt = at;
                _deskLooks = 0;
                _deskNext = 0f;
                _deskTold = false;
                _deskRival = -1;
                if (at != null)
                {
                    _deskRival = ConstructionBridges.RivalHere != null ? ConstructionBridges.RivalHere() : -1;
                    if (_deskRival < 0 && !BuilderDeskGame.InOwnOffice())
                    {
                        _deskAt = at;   // nobody's front desk here: nothing more is looked at until the player goes elsewhere
                        _deskLooks = int.MaxValue;
                    }
                }
            }
            if (at == null || _deskLooks == int.MaxValue)
            {
                return;
            }
            if (_deskPointer != null && _deskPointer.Staffed)
            {
                return;
            }
            float now = Time.unscaledTime;
            if (_deskLooks >= DeskLooksFirst && now < _deskNext)
            {
                return;
            }
            _deskLooks = _deskLooks < DeskLooksFirst ? _deskLooks + 1 : _deskLooks;
            _deskNext = now + DeskLookAgain;
            MakeDesk();
        }

        // The person at the front desk found and made a thing to talk to; said once a visit when nobody sits at one.
        private void MakeDesk()
        {
            DropDesk();
            EmployeeStationController station = BuilderDeskGame.Front(out ThirdPersonCharacter person);
            if (station == null || !BuilderDeskGame.Install())
            {
                if (!_deskTold && _deskLooks >= DeskLooksFirst)
                {
                    _deskTold = true;
                    Logger?.Info("Construction: nobody sits at a desk of " + DeskWhere() + " yet, so there is nobody to talk to; looked at again every " + DeskLookAgain + " s.");
                }
                return;
            }
            _deskPointer = BuilderDeskPointer.Make(station, person, _deskRival, _deskRival >= 0 ? BuilderDesk.NpcKey : BuilderDesk.ManagerKey);
            if (_deskPointer != null && Verbose)
            {
                string away = Mathf.Sqrt(CitySpots.Flat(station.transform.position - ConstructionGame.Player)).ToString("0.0", System.Globalization.CultureInfo.InvariantCulture);
                Logger?.Info("Construction check (front desk): " + DeskWhere() + "'s front desk is the workstation '" + station.name + "', " + away
                             + " m from the player, its person seated: PASS.");
            }
        }

        private string DeskWhere()
        {
            return _deskRival >= 0 ? ConstructionText.T(BuildCatalogue.RivalKeys[_deskRival]) + "'s office" : "the company's own office";
        }

        // The desk's pointer taken away (the game's workstation it lives on goes with the building in any case).
        private void DropDesk()
        {
            if (_deskPointer != null)
            {
                UnityEngine.Object.Destroy(_deskPointer.gameObject);
            }
            _deskPointer = null;
        }

        // The desks put away with the construction game: the pointer, the click and the clock.
        private void DesksDown()
        {
            DropDesk();
            BuilderDeskGame.Remove();
            BuilderDeskGame.Release();
            _deskAt = null;
            _deskRival = -1;
        }

        /// <summary>A click on the person at a desk: the player walks up to them, and the conversation opens there.</summary>
        internal void DeskClicked(BuilderDeskPointer pointer)
        {
            if (!Running || pointer == null || !pointer.Staffed || BuilderDesk.Talking)
            {
                return;
            }
            int rival = pointer.Rival;
            ThirdPersonCharacter person = pointer.Person;
            BuilderDeskGame.WalkUp(pointer, () =>
            {
                if (Running && pointer != null && pointer.Staffed && BuilderDesk.Open(rival, person))
                {
                    string who = rival >= 0 ? "the receptionist at " + ConstructionText.T(BuildCatalogue.RivalKeys[rival]) : "the company's office manager";
                    Logger?.Info("Construction: the player talks to " + who + ".");
                }
            });
        }

        /// <summary>
        /// What the front desk talks about first (FrontDeskRules.Step): the player's building furthest along of those a builder can
        /// be hired for, with its project's id, or what they need first.
        /// </summary>
        internal DeskStep DeskStepNow(out int project)
        {
            bool ground = _state.Lots.Count > 0;
            if (!ground)
            {
                ConstructionGame.Owned(_deskOwned);
                ground = _deskOwned.Count > 0;
                _deskOwned.Clear();
            }
            return FrontDeskRules.Step(_state.Projects, ground, out project);
        }

        /// <summary>A builder's quote this week for a project: the very one the app's quotes page shows (<see cref="Quotes"/>). False for none.</summary>
        internal bool DeskQuote(ProjectFile p, int rival, out TenderBid bid)
        {
            int count = Quotes(p, _deskQuotes);
            bool found = rival >= 0 && rival < count;
            bid = found ? _deskQuotes[rival] : default(TenderBid);
            return found;
        }
    }
}
