using System;
using System.IO;

// The game's toast showed behind the kit's card while hovered (6 Nov). The game's entry is a Button whose own picture is the tint target: the kit
// hides that picture by its renderer, but the pointer over the card is also over the entry, and the button's hover tint writes the picture
// back. The picture now takes no pointer while the kit holds it, so the hover never starts, in any state and for every entry.
internal static partial class Program
{
    private static void ToastHoverChecks()
    {
        string root = FindRoot();
        string ui = System.IO.Path.Combine(root, "Assets", "Mods", "RealLifeOverhaul", "Scripts", "Ui");
        string conceal = File.ReadAllText(System.IO.Path.Combine(ui, "Host", "UiConceal.cs"));
        string notices = File.ReadAllText(System.IO.Path.Combine(ui, "Shell", "ShellNotices.cs"));
        int take = notices.IndexOf("private void Take(Transform entry)", StringComparison.Ordinal);
        int takeEnd = take >= 0 ? notices.IndexOf("// The once-a-session line", take, StringComparison.Ordinal) : -1;
        string takeBody = take >= 0 && takeEnd > take ? notices.Substring(take, takeEnd - take) : "";
        Check(takeBody.Contains("_hidden.AddDeaf(entry.GetComponent<Graphic>())") && !takeBody.Contains("_hidden.Add(entry.GetComponent<Graphic>())"),
            "toast hover: the game's entry picture is held deaf, so its hover tint never starts and shows no game piece");
        Check(notices.Contains("_live.Arrived(newest)") && notices.Contains("Take(entry);") && notices.Contains("_hidden.Hiding"),
            "toast hover: a toast arriving while another is hovered is held deaf the moment the game makes it, as every stacked entry is");
        int hide = conceal.IndexOf("public void Hide()", StringComparison.Ordinal);
        int show = conceal.IndexOf("public void Show()", StringComparison.Ordinal);
        int prune = conceal.IndexOf("public void Prune()", StringComparison.Ordinal);
        string hideBody = hide >= 0 && show > hide ? conceal.Substring(hide, show - hide) : "";
        string showBody = show >= 0 && prune > show ? conceal.Substring(show, prune - show) : "";
        Check(hideBody.Contains("Deafen(ref d)") && conceal.Contains("d.Picture.raycastTarget = false") && conceal.Contains("up.Heard = graphic.raycastTarget"),
            "toast hover: holding a deaf picture turns its pointer off and keeps what it was, also for one added while the hold is on");
        Check(showBody.Contains("Picture.raycastTarget = _graphics[i].Heard"),
            "toast hover: giving the game's toast back gives its pointer back as found, so its own click works again");
        Check(conceal.Contains("public void AddDeaf(Graphic graphic)") && conceal.Contains("public void Add(Graphic graphic) => Add(graphic, false)"),
            "toast hover: only the toast's entry is deaf, every other screen's picture is held as before");
        Check(!conceal.Contains("Update()") && !conceal.Contains("LateUpdate") && !conceal.Contains("FindObject") && notices.Contains("Every = 0.25f")
            && !takeBody.Contains("FindObject"),
            "toast hover: no per-frame work, the hold is set once a toast and the stack is looked at four times a second as before");
        Check(notices.Contains("Press(int command, int arg)") && notices.Contains("UiWidgets.Press(_entries[at].GetComponent<Button>())"),
            "toast hover: a click on the card still runs the entry's own button, which needs no pointer on the entry");
    }
}
