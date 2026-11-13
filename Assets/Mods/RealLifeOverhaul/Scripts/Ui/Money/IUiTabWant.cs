namespace RealLifeOverhaul.Ui
{
    /// <summary>A live source a notice or a button of the old app can ask for a tab: the screen takes the request once, the next time it lays out.</summary>
    public interface IUiTabWant
    {
        /// <summary>The tab asked for, or -1 for none; the request is forgotten once taken.</summary>
        int TakeWanted();
    }
}
