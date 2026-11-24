using System.Collections.Generic;
using BAModAPI;
using Buildings.Retail.Businesses.CinemaTheater;
using UnityEngine;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The ticket gates of a cinema or a theatre while a shift works on its floor. The game puts a collider across each screen's
    /// door for whoever holds no ticket (TicketEntryBlocker), and a cleaner or stocker sent there by the phone holds a mop or a box
    /// that cannot be put away to buy one, so the screens were out of reach and the job could not be finished. The colliders are
    /// switched off for the shift (<see cref="JobGateRules"/>) and the game decides again when it ends, the game's own
    /// <c>UpdateBlockers</c>. The game switches the gates back on whenever the hands change, so the director's tick looks again.
    /// Cost: a search of the interior once a shift (the scene's only when the interior holds none), then a flag test per gate a
    /// tick; a building with no gates costs one string comparison. Nothing is saved, and nobody else's gate is touched.
    /// </summary>
    internal sealed class JobGates
    {
        private const string Cinema = "ba:businesstype_cinema";
        private const string Theatre = "ba:businesstype_theater";

        private static readonly List<TicketEntryBlocker> Found = new List<TicketEntryBlocker>(8);

        private readonly IModLogger _logger;
        private readonly List<Collider> _gates = new List<Collider>(8);
        private Transform _looked;     // the interior the gates were looked for in
        private bool _done;            // ...and it was looked at, whether or not it holds gates: asked once an interior
        private bool _held;            // some gate has been switched off for the shift

        public JobGates(IModLogger logger)
        {
            _logger = logger;
        }

        /// <summary>Number of gates found in the building the shift works in.</summary>
        public int Count => _gates.Count;

        /// <summary>True while the shift keeps gates open.</summary>
        public bool Held => _held;

        /// <summary>From the job's tick while the shift works inside the building the player is in: every gate of it is open.</summary>
        public void Keep()
        {
            BuildingManager manager = InstanceBehavior<BuildingManager>.Instance;
            if (manager == null || !Ticketed(manager))
            {
                Release();
                return;
            }
            if (!_done || !ReferenceEquals(_looked, manager.currentBuildingVersion))
            {
                Look(manager);
            }
            bool shut = false;
            for (int i = 0; i < _gates.Count; i++)
            {
                shut |= _gates[i] != null && _gates[i].enabled;
            }
            if (JobGateRules.Decide(true, shut, _held) != JobGateRules.Act.Open)
            {
                return;
            }
            for (int i = 0; i < _gates.Count; i++)
            {
                if (_gates[i] != null)
                {
                    _gates[i].enabled = false;
                }
            }
            if (!_held)
            {
                _held = true;
                _logger?.Info("Jobs: the shift works inside a business with ticket gates (" + _gates.Count + "); the worker passes them without a ticket.");
            }
        }

        /// <summary>The shift is over or the worker is out: the game's own rule for the gates is asked again.</summary>
        public void Release()
        {
            bool give = JobGateRules.Decide(false, false, _held) == JobGateRules.Act.GiveBack;
            _held = false;
            _done = false;
            _looked = null;
            _gates.Clear();
            if (!give)
            {
                return;
            }
            try
            {
                TicketEntryBlocker.UpdateBlockers();
            }
            catch (System.Exception e)
            {
                _logger?.Error(e);
            }
        }

        // The game's own test for a business that has ticket gates (TicketEntryBlocker.UpdateBlockers).
        private static bool Ticketed(BuildingManager manager)
        {
            string type = manager.buildingRegistration != null ? manager.buildingRegistration.businessTypeName : null;
            return type == Cinema || type == Theatre;
        }

        // The gates of the interior the player is in, from the interior's own parts first: they are made with it.
        private void Look(BuildingManager manager)
        {
            _looked = manager.currentBuildingVersion;
            _done = true;
            _gates.Clear();
            Collect(manager.currentBuildingVersion);
            Collect(manager.currentLayout);
            Collect(manager.IndoorItemContainer);
            if (_gates.Count == 0)
            {
                TicketEntryBlocker[] all = SceneFinds.All<TicketEntryBlocker>("jobs: the ticket gates of the client's cinema", FindObjectsInactive.Include);
                for (int i = 0; all != null && i < all.Length; i++)
                {
                    Add(all[i]);
                }
            }
        }

        private void Collect(Transform root)
        {
            if (root == null)
            {
                return;
            }
            root.GetComponentsInChildren(true, Found);
            for (int i = 0; i < Found.Count; i++)
            {
                Add(Found[i]);
            }
            Found.Clear();
        }

        private void Add(TicketEntryBlocker blocker)
        {
            Collider box = blocker != null ? blocker.GetComponent<BoxCollider>() : null;
            if (box != null && !_gates.Contains(box))
            {
                _gates.Add(box);
            }
        }
    }
}
