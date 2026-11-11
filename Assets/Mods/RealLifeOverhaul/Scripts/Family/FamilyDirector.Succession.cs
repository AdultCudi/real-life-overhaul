using System.Collections.Generic;

namespace RealLifeOverhaul.Family
{
    /// <summary>FamilyDirector's part for a new head of the family (Scripts/Dynasty): the family as the heir has it.</summary>
    public sealed partial class FamilyDirector
    {
        /// <summary>
        /// The heir is the played head of the family from now on. A partner who carries on is nobody's partner any more and keeps the children as theirs. A child or a relative
        /// who carries on starts a household of their own: the founder's partner (the heir's parent, or a relative), wedding and errands were the founder's, so the family begins
        /// afresh with its memories kept. The children stay: the founder's other children, their schooling and their college funds are now the new head's to look after (they are
        /// the heir's brothers and sisters, or a relative's cousins, whom nobody else in the mod can take in); only the heir, who is the head now, leaves the list. The founder's
        /// own parents are not touched: family life keeps one Mom for the head of the family, and she stays that for the heir. Everything shown or staged is put away first, as
        /// the option's own switch does it. No character is made, changed or dressed here.
        /// </summary>
        internal void NewHead(bool partnerCarriesOn, string founder, string heirName = null)
        {
            if (!FamilyState.Loaded)
            {
                return;
            }
            Guarded(() =>
            {
                Stop(false);
                FamilyModifiers.RemoveAll();
                if (partnerCarriesOn)
                {
                    FamilySave s = FamilyState.S;
                    FamilyState.DropErrand(Errands.Counselling);
                    FamilyState.DropErrand(Errands.Dinner);
                    Partner.Part();
                    s.ExName = null;
                    s.NextOfKin = null;
                    s.Custody = 0;
                    s.SeparatedDay = 0;
                    s.BondBefore = 0;
                    FamilyState.Bond = Bond.Single;
                }
                else
                {
                    var memories = new List<Memory>(FamilyState.S.Memories);
                    List<ChildRecord> children = KeptChildren(FamilyState.S.Children, heirName);
                    FamilyState.Clear();
                    FamilyState.S.Memories.AddRange(memories);
                    FamilyState.S.Children.AddRange(children);
                    Errands.Reset();
                }
                FamilyState.Remember("reallife_family_memory_took_over", founder);
                Partner.Resync();
                _started = true;
                Logger?.Info("Family: " + (partnerCarriesOn ? "the partner carries on as the head of the family; the children stay theirs"
                    : "a new head of the family starts a household of their own; the memories and the other children are kept") + ".");
            });
        }

        /// <summary>The children who stay with the family when a child or a relative becomes its head: all of them but the heir (found by name), each with their school and fund.</summary>
        internal static List<ChildRecord> KeptChildren(List<ChildRecord> all, string heirName)
        {
            return Dynasty.DynastyRules.Staying(all, c => c.Name, heirName);
        }
    }
}
