namespace RealLifeOverhaul.Ui
{
    /// <summary>A block of a population page's facts: a label in capitals over key and value rows.</summary>
    public struct PopulationSection
    {
        public string Title;
        public UiPairModel[] Lines;
    }

    /// <summary>
    /// What a page of the city's people shows, with no colour or place in it: a head (a person's face, or a place's glyph
    /// with its name and key figures) or the city's figures, the rows a press opens as the big table's, the facts beside
    /// them and the one action. A record that has gone is its words alone. Game-free.
    /// </summary>
    public sealed class PopulationModel
    {
        public bool Gone;                           // the record is not there: the words stand alone
        public string GoneTitle, GoneBody;
        public UiProfileModel Head;                 // null for the city, whose figures stand across instead
        public UiFigureModel[] Figures = new UiFigureModel[0];
        public bool HasAction;
        public UiActionModel Action;
        public UiDataTableModel List = new UiDataTableModel();
        public PopulationSection[] Sections = new PopulationSection[0];
        public UiPrompt[] Prompts = new UiPrompt[0];
        public int PromptCount;
    }
}
