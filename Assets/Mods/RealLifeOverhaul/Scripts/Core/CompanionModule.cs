namespace RealLifeOverhaul
{
    /// <summary>A reporting capability. A failed reader loses only its own data, never another capability's.</summary>
    public sealed class CompanionModule
    {
        public int Version = 1;
        public string Status;
        public object Data;

        public CompanionModule(string status, object data = null)
        {
            Status = status;
            Data = data;
        }
    }

}
