using System.Text;

namespace RealLifeOverhaul
{
    /// <summary>
    /// The figures the Guide's Social life page prints that need more than one number: the outings with what they cost and
    /// the hours they can be agreed for, read from <see cref="SocialCatalogue.Outings"/>. Worked out only as the page opens
    /// (Meta/FeatureList.Life); nothing here runs per frame.
    /// </summary>
    public static class SocialGuide
    {
        /// <summary>The outings a call can plan, paid ones with their cost a head ("coffee ($6, 07:00 to 19:00)") or free ones with their hours.</summary>
        public static string Outings(bool paid)
        {
            var b = new StringBuilder(256);
            for (int i = 0; i < SocialCatalogue.Outings.Length; i++)
            {
                OutingDef d = SocialCatalogue.Outings[i];
                if (d.Kind == PlanKind.Lift || d.Kind == PlanKind.Moving || (d.Cost > 0) != paid)
                {
                    continue;   // a lift and a move are asked for by friends, not planned
                }
                b.Append(b.Length > 0 ? ", " : string.Empty).Append(SocialLife.T(d.Name)).Append(" (");
                if (paid)
                {
                    b.Append(SocialLife.Money(d.Cost)).Append(", ");
                }
                b.Append(Careers.CareersText.Hour(d.From)).Append(' ').Append(SocialLife.T("reallife_guide_work_to")).Append(' ')
                    .Append(Careers.CareersText.Hour(d.To)).Append(')');
            }
            return b.ToString();
        }
    }
}