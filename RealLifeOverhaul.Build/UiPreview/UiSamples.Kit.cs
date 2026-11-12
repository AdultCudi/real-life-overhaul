using RealLifeOverhaul.Ui;

/// <summary>
/// The kit's parts in every state the brief asks for, each a page of the parts' gallery: the controls (and a choice with
/// its list open), the big table of forty staff sorted, filtered and two picked (and one that finds no one), the charts,
/// a catalogue of twelve with one chosen, a statement of a busy week (and a new save's), and tracks. The words are the
/// locale's in the language given, the money the samples' dollars.
/// </summary>
internal static partial class UiSamples
{
    /// <summary>A state of the parts' gallery by its name, as the previews and the checks lay each one out.</summary>
    public static UiPartsGalleryModel Parts(UiWords t, string state)
    {
        var st = new UiPartsState();
        switch (state)
        {
            case "controls":
                st.Held = st.Focused = UiPartsGallery.Arg(false, 1, 0);   // the ads budget held, Left and Right stepping it
                break;
            case "fields_typing":
                st.Typing = true;
                st.Focused = UiPartsGallery.Arg(true, 2, 0);
                st.Name = "Bloom Caf";
                break;
            case "amount_typing":
                st.Typing = true;
                st.Focused = UiPartsGallery.Arg(false, 2, 2);
                st.Shelves = 12;
                break;
            case "choice_open": st.OpenChoice = UiPartsGallery.Arg(false, 0, 0); break;
            case "table":
                st.Page = UiPartsGallery.PageTable;   // one firm's staff, sorted by skill: the filter row with its count
                break;
            case "table_picked":
            {
                // Picking, with two of the rows in view picked, so the mass actions stand over them.
                st.Page = UiPartsGallery.PageTable;
                st.Business = 0;
                st.Picking = true;
                UiDataRow[] rows = UiPartsGalleryScreen.Model(t, st, Dollars).Table.Rows;
                st.Picked[rows[1].Values[rows[1].Values.Length - 1]] = true;
                st.Picked[rows[3].Values[rows[3].Values.Length - 1]] = true;
                st.Selected = rows[3].Values[rows[3].Values.Length - 1];   // the detail shows the last one picked
                break;
            }
            case "table_empty":
                st.Page = UiPartsGallery.PageTable;
                st.Business = 0;
                st.Query = "Zed";
                break;
            case "charts":
                st.Page = UiPartsGallery.PageCharts;
                st.LineFocus = 8;   // September, where the keys and the mouse rest in their shots, read in its card
                break;
            case "catalogue": st.Page = UiPartsGallery.PageCatalogue; break;
            case "catalogue_empty":
                st.Page = UiPartsGallery.PageCatalogue;
                st.ShelvesEmpty = true;
                break;
            case "statement": st.Page = UiPartsGallery.PageStatement; break;
            case "statement_new":
                st.Page = UiPartsGallery.PageStatement;
                st.NewSave = true;
                break;
            default: st.Page = UiPartsGallery.PageTrack; break;
        }
        return UiPartsGalleryScreen.Model(t, st, Dollars);
    }

    /// <summary>Every state of the parts' gallery, in the order the previews show them.</summary>
    public static readonly string[] PartStates =
    {
        "controls", "choice_open", "fields_typing", "amount_typing", "table", "table_picked", "table_empty", "charts", "catalogue", "catalogue_empty", "statement", "statement_new", "track"
    };
}
