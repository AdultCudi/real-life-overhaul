using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using RealLifeOverhaul;

// Moving furniture (a player's report: older pieces could not be moved, or would not go down where clicked). The click
// check's lines say why the game refuses (Diagnostics/FurnitureRules, Diagnostics/ClickWatch.Moves): the game's own first
// reason for a right click and for a place click, in its own order, how far the pointer may move on a place click, whose
// a thing in the way is; and, over the sources, that the lines only read, only with Developer diagnostics on.
internal static partial class Program
{
    private static void FurnitureMoves()
    {
        FurnitureMoveRuleChecks();
        FurnitureMoveSourceChecks();
    }

    private static MoveSignals FurnitureLifted()
    {
        return new MoveSignals { Inside = true, Yours = true };
    }

    private static void FurnitureMoveRuleChecks()
    {
        Check(FurnitureRules.First(FurnitureLifted()) == MoveRefusal.None, "furniture moves: in the player's own building with nothing in the way, the game lifts a piece");

        // Each of the game's reasons alone, as its right click asks it.
        var alone = new List<KeyValuePair<MoveRefusal, MoveSignals>>();
        MoveSignals s;
        s = FurnitureLifted(); s.Hidden = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Hidden, s));
        s = FurnitureLifted(); s.Activity = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Activity, s));
        s = FurnitureLifted(); s.Inside = false; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Outdoors, s));
        s = FurnitureLifted(); s.AtTheDoor = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.AtTheDoor, s));
        s = FurnitureLifted(); s.Yours = false; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.NotYours, s));
        s = FurnitureLifted(); s.InUse = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.InUse, s));
        s = FurnitureLifted(); s.Placing = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Placing, s));
        s = FurnitureLifted(); s.Buying = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Buying, s));
        s = FurnitureLifted(); s.ToHospital = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.ToHospital, s));
        s = FurnitureLifted(); s.Previewing = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Previewing, s));
        s = FurnitureLifted(); s.Designer = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Designer, s));
        s = FurnitureLifted(); s.ActivityPanel = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.ActivityPanel, s));
        s = FurnitureLifted(); s.SmallMenu = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.SmallMenu, s));
        s = FurnitureLifted(); s.Using = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Using, s));
        s = FurnitureLifted(); s.Vehicle = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Vehicle, s));
        s = FurnitureLifted(); s.Structure = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Structure, s));
        s = FurnitureLifted(); s.Residential = true; alone.Add(new KeyValuePair<MoveRefusal, MoveSignals>(MoveRefusal.Residential, s));
        foreach (KeyValuePair<MoveRefusal, MoveSignals> one in alone)
        {
            Check(FurnitureRules.First(one.Value) == one.Key, "furniture moves: " + one.Key + " alone is the game's reason (" + FurnitureRules.First(one.Value) + ")");
        }
        Check(alone.Count == Enum.GetValues(typeof(MoveRefusal)).Length - 1, "furniture moves: every reason of the game's is checked alone");

        // The game's order: its outer test first, then in use before the panels, the building's own pieces last.
        s = FurnitureLifted(); s.InUse = true; s.Placing = true; s.Designer = true;
        Check(FurnitureRules.First(s) == MoveRefusal.InUse, "furniture moves: a piece in use says so even with a panel open, as the game's right click does");
        s = FurnitureLifted(); s.Activity = true; s.InUse = true;
        Check(FurnitureRules.First(s) == MoveRefusal.Activity, "furniture moves: an activity under way stops a right click before anything is said");
        s = FurnitureLifted(); s.Yours = false; s.Editable = true;
        Check(FurnitureRules.First(s) == MoveRefusal.None, "furniture moves: a building open for editing lets a piece be lifted though it is not the player's");
        s = FurnitureLifted(); s.InUse = true; s.Editable = true;
        Check(FurnitureRules.First(s) == MoveRefusal.None, "furniture moves: a building open for editing lets a piece in use be lifted, as the game does");
        s = FurnitureLifted(); s.Structure = true; s.Residential = true; s.Placing = true;
        Check(FurnitureRules.First(s) == MoveRefusal.Placing, "furniture moves: the building's own pieces are asked about last, after the right click's own tests");
        s = FurnitureLifted(); s.Hidden = true; s.Activity = true; s.Inside = false;
        Check(FurnitureRules.First(s) == MoveRefusal.Hidden, "furniture moves: a hidden piece takes no right click at all");

        Check(FurnitureRules.Noticed(MoveRefusal.InUse) && FurnitureRules.Noticed(MoveRefusal.Structure) && FurnitureRules.Noticed(MoveRefusal.Residential)
              && !FurnitureRules.Noticed(MoveRefusal.None) && !FurnitureRules.Noticed(MoveRefusal.Designer) && !FurnitureRules.Noticed(MoveRefusal.Activity),
            "furniture moves: only in use, part of the building and a piece fixed in a flat come with a notice of the game's");

        // The place click.
        var clear = new PlaceSignals { Floor = true, InsidePlot = true, Fits = true };
        Check(FurnitureRules.First(clear) == PlaceRefusal.None, "furniture moves: on the building's floor, inside the plot, fitting, the game puts the piece down");
        var places = new List<KeyValuePair<PlaceRefusal, PlaceSignals>>();
        PlaceSignals p;
        p = clear; p.InputSelected = true; places.Add(new KeyValuePair<PlaceRefusal, PlaceSignals>(PlaceRefusal.InputSelected, p));
        p = clear; p.OverInterface = true; places.Add(new KeyValuePair<PlaceRefusal, PlaceSignals>(PlaceRefusal.OverInterface, p));
        p = clear; p.Floor = false; places.Add(new KeyValuePair<PlaceRefusal, PlaceSignals>(PlaceRefusal.NoFloor, p));
        p = clear; p.InsidePlot = false; places.Add(new KeyValuePair<PlaceRefusal, PlaceSignals>(PlaceRefusal.OutsidePlot, p));
        p = clear; p.Blocked = true; places.Add(new KeyValuePair<PlaceRefusal, PlaceSignals>(PlaceRefusal.Blocked, p));
        p = clear; p.Fits = false; places.Add(new KeyValuePair<PlaceRefusal, PlaceSignals>(PlaceRefusal.NoFit, p));
        p = clear; p.Moved = true; places.Add(new KeyValuePair<PlaceRefusal, PlaceSignals>(PlaceRefusal.Moved, p));
        foreach (KeyValuePair<PlaceRefusal, PlaceSignals> one in places)
        {
            Check(FurnitureRules.First(one.Value) == one.Key, "furniture moves: " + one.Key + " alone keeps the piece in the air (" + FurnitureRules.First(one.Value) + ")");
        }
        Check(places.Count == Enum.GetValues(typeof(PlaceRefusal)).Length - 1, "furniture moves: every reason a piece is kept in the air is checked alone");
        p = clear; p.OverInterface = true; p.InputSelected = true; p.Fits = false;
        Check(FurnitureRules.First(p) == PlaceRefusal.InputSelected, "furniture moves: a selected piece of the interface stops the game's placing before the pointer is asked about");
        p = clear; p.Floor = false; p.InsidePlot = false; p.Fits = false;
        Check(FurnitureRules.First(p) == PlaceRefusal.NoFloor, "furniture moves: with no floor under the pointer nothing else is asked");
        p = clear; p.Blocked = true; p.Fits = false; p.Moved = true;
        Check(FurnitureRules.First(p) == PlaceRefusal.Blocked, "furniture moves: blocked floor is said before a fit, as the game's place click asks");

        // The game's slack for a place click: a hundredth of the screen's height.
        Check(!FurnitureRules.MovedTooFar(21f, 0f, 2160f) && FurnitureRules.MovedTooFar(22f, 0f, 2160f) && !FurnitureRules.MovedTooFar(15f, 15f, 2160f) && FurnitureRules.MovedTooFar(16f, 16f, 2160f)
              && !FurnitureRules.MovedTooFar(0f, 0f, 1080f) && FurnitureRules.MovedTooFar(0f, -11f, 1080f),
            "furniture moves: the pointer may move a hundredth of the screen's height between press and release, any way, at any size of screen");

        // Whose a thing in the way is.
        const string own = "RealLifeOverhaul";
        Check(FurnitureRules.Owner(own, string.Empty, own, false) == MoveOwner.ThisMod && FurnitureRules.Owner("BigAmbitions", string.Empty, own, true) == MoveOwner.ThisMod,
            "furniture moves: this mod's by its assembly, or by its own name on the thing or above it");
        Check(FurnitureRules.Owner("BigAmbitions", @"E:\Program Files (x86)\Steam\steamapps\common\Big Ambitions\Big Ambitions_Data\Managed\BigAmbitions.dll", own, false) == MoveOwner.Game
              && FurnitureRules.Owner("UnityEngine.PhysicsModule", "G:/Steam/steamapps/common/Big Ambitions/Big Ambitions_Data/Managed/UnityEngine.PhysicsModule.dll", own, false) == MoveOwner.Game,
            "furniture moves: the game's (and the engine's) by the game's own folder of assemblies, either way of writing the path");
        Check(FurnitureRules.Owner("VendingMachines", @"E:\Program Files (x86)\Steam\steamapps\workshop\content\1331550\3800000000\VendingMachines.dll", own, false) == MoveOwner.OtherMod
              && FurnitureRules.Owner("EmpireCasino", string.Empty, own, false) == MoveOwner.OtherMod && FurnitureRules.Owner(null, null, own, false) == MoveOwner.Nobody
              && FurnitureRules.Owner(string.Empty, "x", own, false) == MoveOwner.Nobody,
            "furniture moves: another mod's from anywhere else or from memory, nobody's without a script");
        Check(FurnitureRules.Words(MoveOwner.OtherMod, "VendingMachines").Contains("VendingMachines") && FurnitureRules.Words(MoveOwner.Game, null) == "the game's"
              && FurnitureRules.Words(MoveOwner.ThisMod, null) == "this mod's" && FurnitureRules.Words(MoveOwner.Nobody, null).Length > 0,
            "furniture moves: the line names another mod by its assembly");

        // The words: every reason has its own, plain, with no dash or exclamation mark.
        var said = new HashSet<string>();
        bool plain = true;
        foreach (MoveRefusal r in Enum.GetValues(typeof(MoveRefusal)))
        {
            string w = FurnitureRules.Words(r);
            plain &= w.Length > 10 && said.Add(w) && !w.Contains('!') && !w.Contains((char)0x2014) && !w.Contains((char)0x2013) && !w.Contains(" - ") && !w.Contains("does not know");
        }
        foreach (PlaceRefusal r in Enum.GetValues(typeof(PlaceRefusal)))
        {
            string w = FurnitureRules.Words(r);
            plain &= w.Length > 10 && said.Add(w) && !w.Contains('!') && !w.Contains((char)0x2014) && !w.Contains((char)0x2013) && !w.Contains(" - ") && !w.Contains("does not know");
        }
        Check(plain, "furniture moves: each reason has words of its own for the log, plain and without a dash");
    }

    private static void FurnitureMoveSourceChecks()
    {
        string watch = ModSource(@"Diagnostics\ClickWatch.cs");
        string moves = ModSource(@"Diagnostics\ClickWatch.Moves.cs");
        string rules = ModSource(@"Diagnostics\FurnitureRules.cs");
        Check(watch.Length > 0 && moves.Length > 0 && rules.Length > 0, "furniture moves sources: ClickWatch, its move lines and FurnitureRules are there");
        string onUpdate = MemberText(watch, "private void OnUpdate()");
        Check(InOrder(onUpdate, "if (!Meta.Dev.Verbose)", "return;", "WatchMove();", "Input.GetMouseButtonDown(0)", "WritePlace();", "WriteMove();"),
            "furniture moves sources: the lines are written only with Developer diagnostics on, the move watched before a click is read");
        string code = Regex.Replace(moves, @"//[^\n]*", string.Empty);
        Check(!Regex.IsMatch(code, @"\.Occupied\s*=|\.occupied\s*=|SetParent\(|SetActive\(|\.enabled\s*=|StartPlacementMode|CancelPlacementMode|CanPlaceItem\(|SetItemPosition|\.position\s*=|AddItemInstanceToBuilding|RemoveItemInstanceFromBuilding|\.layer\s*=|Destroy\("),
            "furniture moves sources: the move lines read the game and change nothing of it (CanPlaceItem is never asked: it would show the game's notice again)");
        Check(MemberText(moves, "private void WritePlace()").Contains("FurnitureRules.First(signals)") && MemberText(moves, "private void WriteMove()").Contains("FurnitureRules.First(Signals(item, manager, game.playerController))"),
            "furniture moves sources: both lines take the game's reason from FurnitureRules");
        Check(moves.Contains("GameContract.SittingPositions.Field") && !Regex.IsMatch(code, @"GetField\(|GetProperty\(|BindingFlags"),
            "furniture moves sources: the game's places to sit are read through the game contract, never by a name of its own");
        Check(MemberText(moves, "private void WatchMove()").Contains("InteriorDesignerUI.IsOpen ? null") && !Regex.IsMatch(MemberText(moves, "private void WatchMove()"), @"\bnew\b|""\s*\+|\+\s*""|ToString\("),
            "furniture moves sources: the watch a frame allocates nothing and leaves the interior designer's own moves alone");
        Check(!Regex.IsMatch(Regex.Replace(rules, @"//[^\n]*", string.Empty), @"\bUnityEngine\b|\bRandom\b|DateTime"), "furniture moves sources: FurnitureRules is game-free");
        Check(Regex.IsMatch(code, @"\.Info\(") && !Regex.IsMatch(code, @"Notifications\.|\.Warn\(") && !Regex.IsMatch(code, @"\bmodData\b|PlayerPrefs|Send\("),
            "furniture moves sources: the lines go to the log alone: no notice, nothing saved or sent");
    }
}
