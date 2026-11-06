namespace RealLifeOverhaul
{
    /// <summary>A reporting capability. A failed reader loses only its own data, never another capability's.</summary>
    public sealed class CompanionModule
    {
        public int Version = 1;      // the capability's own contract version, raised when a field's meaning or unit changes
        public string Status;        // "ready", "disabled", "unavailable" or "failed"; only "ready" carries data
        public object Data;

        public CompanionModule(string status, object data = null)
        {
            Status = status;
            Data = data;
        }
    }
}
