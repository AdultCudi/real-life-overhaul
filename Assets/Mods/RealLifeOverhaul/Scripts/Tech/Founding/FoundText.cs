namespace RealLifeOverhaul.Tech
{
    /// <summary>The words founding adds to the cap table: who a line of it belongs to.</summary>
    public static class FoundText
    {
        /// <summary>The name on a line of a firm's cap table: the player, the staff pool, a co-founder by name, friends and family, or a venture firm.</summary>
        public static string HolderName(Holder h, FoundedFile file, string place)
        {
            if (h.Kind == (int)HolderKind.Player)
            {
                return TechText.T("reallife_vc_holder_you");
            }
            if (h.Kind == (int)HolderKind.Pool)
            {
                return TechText.T("reallife_vc_holder_pool");
            }
            if (h.Kind == (int)HolderKind.Founders)
            {
                CoFounder co = file != null ? file.Cofounder(place, h.Who) : null;
                return co != null ? co.Name : TechText.T("reallife_found_holder_cofounder");
            }
            if (h.Who == VentureCatalogue.FamilyId)
            {
                return TechText.T("reallife_found_holder_family");
            }
            return VentureCatalogue.Firms[TalentCatalogue.Clamp(h.Who, 0, VentureCatalogue.FirmCount - 1)].Name;
        }
    }
}
