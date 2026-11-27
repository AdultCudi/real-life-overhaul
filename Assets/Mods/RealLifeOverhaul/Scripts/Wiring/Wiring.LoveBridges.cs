using System;
using RealLifeOverhaul.Fame;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Flare;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The love life (Social/Love) with family life (a separation is Family's own flow, the partner at home waits through its ask,
        /// the in-laws, a child of a night, what comes next after living together), Standing (how known the player is, a fading dent),
        /// Flare (followers lost and drawn by a scandal) and the City Ledger (through Social's News). Each link is null until set here.
        /// </summary>
        public static class LoveBridges
        {
            internal static void Connect()
            {
                LoveLinks.Bound = p => p != null && FamilyAffairs.Bound(p.Name);
                LoveLinks.Separate = theirs => FamilyAffairs.Separate(theirs);
                LoveLinks.AskHome = FamilyAffairs.Raise;
                LoveLinks.AskDone = FamilyAffairs.Lower;
                LoveLinks.InLawsHear = FamilyAffairs.InLawsHear;
                LoveLinks.AddChild = (girl, name, index, away, carrier) => FamilyAffairs.AddChild(girl, name, index, away);
                LoveLinks.ChildRoom = FamilyAffairs.ChildRoom;
                LoveLinks.NextStep = NextStep;
                FamilyBridges.LoveTalk = LoveTalk.ConfrontHome;
                LoveLinks.Recognition = () => FameDirector.Instance != null ? FameDirector.Instance.Recognition : -1;
                LoveLinks.Dent = points => FameDirector.Instance?.PrivateScandal(points, 0);
                LoveLinks.Integrity = points => FameDirector.Instance?.PrivateScandal(0, points);
                LoveLinks.FlareGossip = (lost, gained) => FlareDirector.Instance != null ? FlareDirector.Instance.Gossip(lost, gained) : null;
            }

            internal static void Disconnect()
            {
                LoveLinks.Bound = null;
                LoveLinks.Separate = null;
                LoveLinks.AskHome = null;
                LoveLinks.AskDone = null;
                LoveLinks.InLawsHear = null;
                LoveLinks.AddChild = null;
                LoveLinks.ChildRoom = null;
                LoveLinks.NextStep = null;
                FamilyBridges.LoveTalk = null;
                LoveLinks.Recognition = null;
                LoveLinks.Dent = null;
                LoveLinks.Integrity = null;
                LoveLinks.FlareGossip = null;
            }

            // What comes next after living together, said on the partner's page in People: the ring, the proposal, the wedding planner, hard times.
            private static string NextStep(SocialPerson p)
            {
                try
                {
                    string key = FamilyAffairs.NextStep(out int days);
                    if (key == null || !FamilyAffairs.Bound(p.Name) && !FamilyApi.HasPartner)
                    {
                        return null;
                    }
                    return SocialLife.T(key, SocialLife.Data(p, "days", days.ToString()));
                }
                catch (Exception)
                {
                    return null;
                }
            }
        }
    }
}
