using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What an item of a scene is drawn as.</summary>
    public enum UiKind : byte
    {
        Box = 0,      // a filled shape: a panel, a row, a button's face, a bar, a dot
        Text,
        Icon,         // a glyph of the one icon family, tinted
        Avatar,       // initials on a hue's circle (Value is the hue)
        Spark,        // a sparkline: Points the values
        KeyCap,       // a prompt's key: Text is the key's name, or Icon the button's glyph
        Busy,         // three dots that beat while a loading state shows
        HueDot        // a small circle in a hue's own colour (Value is the hue): a group's dot
    }

    /// <summary>What an item is, beyond its kind: how its words sit and what the checks may allow it.</summary>
    [Flags]
    public enum UiFlags : ushort
    {
        None = 0,
        Truncate = 1,      // player data (a name, a message): may end in an ellipsis, never wraps beyond its lines
        Wrap = 2,          // wraps within its width, up to its height's lines
        Centre = 4,        // words centred in the box
        Right = 8,         // words to the right
        Label = 16,        // a translated label: must fit whole in every language (EconomyCheck)
        Circle = 32,       // a box drawn as a circle or a pill
        Disabled = 64,
        Selected = 128,
        Decor = 256,       // a rule or a ring drawn over the layout's own boxes: skipped by the layout checks
        Overflow = 512,    // may sit across its parent's edge on purpose (a badge on an avatar)
        Middle = 1024,     // centred on its parent's height: its top is where centring puts it, not a grid step
        Share = 2048,      // placed by a share of its parent's width (a segment, a slider's knob): its left is not a grid step
        CountUp = 4096,    // a figure that ticks up from nothing as it arrives
        Prompt = 8192,     // part of the prompt bar, on the game's own bottom bar rather than a panel
        Panel = 16384      // a screen's framed panel: the theme may mark its head (the Ledger's column rule), drawn by the view alone
    }

    /// <summary>One thing drawn: its kind, its box in px and how it is painted, by role and style only.</summary>
    public struct UiItem
    {
        public UiKind Kind;
        public UiFlags Flags;
        public UiRole Fill;      // a box's or an icon's colour
        public UiRole Edge;      // a box's clear edge (a hairline), or None
        public UiRole Ink;       // words' colour
        public UiStyle Style;
        public UiIcon Icon;
        public float Radius;     // px; a circle by flag
        public float X, Y, W, H; // px from the scene's top left, or from a scroll region's content top when Clip >= 0
        public string Text;
        public float Value;      // an avatar's hue, a share 0 to 1 of a bar, a text's most lines
        public float Alpha;      // 0 to 1: a level's shade of a role (a weaker segment, a key cap on the accent)
        public float[] Points;   // a sparkline's values
        public short Clip;       // the scroll region it moves with, or -1
        public short Node;       // the focus target it belongs to, or -1
        public byte Layer;       // 0 the screen, 1 an overlay, 2 a toast
        public short Order;      // its place in an arriving list (staggered), or -1 for none
        public int Parent;       // the item it sits inside, or -1: what the clip and overlap checks measure against
    }

    /// <summary>A focus target: where it is, what pressing it does and how the d-pad leaves it.</summary>
    public struct UiNode
    {
        public float X, Y, W, H;
        public short Clip;
        public byte Layer;
        public byte Region;      // the column it stands in: up and down look there first
        public int Command;      // the screen's own number for the action, given back on a press
        public int Arg;          // a row's index, a choice's number
        public int Face;         // the item that brightens under focus (its face), or -1
        public short List;       // the list it is a row of, or -1: up and down then step through the list's rows
        public UiAction Key;     // the key that presses it from anywhere on its layer (its cap is drawn on it), or Count
        public int Index;        // its row in that list
        public bool Disabled;
        public int Up, Down, Left, Right;   // a node the d-pad goes to, or -1 to find the nearest that way
    }

    /// <summary>A region that scrolls inside a fixed frame: its viewport on the screen and how far its content has moved.</summary>
    public struct UiClip
    {
        public float X, Y, W, H;       // the viewport, px
        public float Offset;           // px scrolled
        public float ContentHeight;    // px of content laid out
        public bool Fills;             // its whole room is its content's (a short thread under the line of its beginning)
        public short List;             // the list it shows, or -1
    }

    /// <summary>A list whose rows are virtualised: only those in view are in the scene, the rest are counted.</summary>
    public struct UiList
    {
        public int Count;          // rows in all
        public int First;          // the first row in the scene
        public int Shown;          // rows in the scene
        public short Clip;
        public int Selected;       // the row shown as selected, or -1
    }

    /// <summary>One prompt of the bar: an action of the input and the words it does.</summary>
    public struct UiPrompt
    {
        public UiAction Action;
        public string Label;
    }

    /// <summary>
    /// A laid-out screen: every item drawn, every focus target, the scroll regions, the lists and the prompt bar, in px of
    /// a 1920 x 1080 screen. A template writes it from a view model; the renderer draws it and EconomyCheck checks it, so
    /// both see the same boxes. Its arrays grow while a screen is first laid out and are then kept: laying the same
    /// screen out again allocates nothing. Cost: a few array writes an item.
    /// </summary>
    public sealed class UiScene
    {
        public UiItem[] Items = new UiItem[256];
        public int ItemCount;
        public UiNode[] Nodes = new UiNode[64];
        public int NodeCount;
        public UiClip[] Clips = new UiClip[4];
        public int ClipCount;
        public UiList[] Lists = new UiList[4];
        public int ListCount;
        public UiPrompt[] Prompts = new UiPrompt[8];
        public int PromptCount;
        public float Width, Height;
        public bool Dark;
        public int FocusFirst = -1;      // the node focus starts on
        public bool FocusReset;          // a new page: focus goes to FocusFirst (the row Back came from), not to the old page's target of the same number
        public string Name;              // the screen's id, for the checks' messages
        public float PromptX, PromptY, PromptW, PromptH;   // where the prompt bar stands

        /// <summary>Empties the scene for a new layout at a size, keeping its arrays.</summary>
        public void Begin(string name, float width, float height, bool dark)
        {
            Name = name;
            Width = width;
            Height = height;
            Dark = dark;
            ItemCount = NodeCount = ClipCount = ListCount = PromptCount = 0;
            FocusFirst = -1;
            FocusReset = false;
            PromptW = PromptH = 0f;
        }

        /// <summary>Adds an item and returns its index.</summary>
        public int Add(ref UiItem item)
        {
            if (ItemCount == Items.Length)
            {
                Array.Resize(ref Items, Items.Length * 2);
            }
            Items[ItemCount] = item;
            return ItemCount++;
        }

        /// <summary>Adds a focus target and returns its index.</summary>
        public int AddNode(ref UiNode node)
        {
            if (NodeCount == Nodes.Length)
            {
                Array.Resize(ref Nodes, Nodes.Length * 2);
            }
            Nodes[NodeCount] = node;
            if (FocusFirst < 0 && !node.Disabled && node.Layer == 0)
            {
                FocusFirst = NodeCount;
            }
            return NodeCount++;
        }

        /// <summary>Adds a scroll region and returns its index.</summary>
        public short AddClip(float x, float y, float w, float h, float offset)
        {
            if (ClipCount == Clips.Length)
            {
                Array.Resize(ref Clips, Clips.Length * 2);
            }
            Clips[ClipCount] = new UiClip { X = x, Y = y, W = w, H = h, Offset = offset, List = -1 };
            return (short)ClipCount++;
        }

        /// <summary>Adds a virtualised list and returns its index.</summary>
        public short AddList(int count, int first, int shown, short clip, int selected)
        {
            if (ListCount == Lists.Length)
            {
                Array.Resize(ref Lists, Lists.Length * 2);
            }
            Lists[ListCount] = new UiList { Count = count, First = first, Shown = shown, Clip = clip, Selected = selected };
            if (clip >= 0)
            {
                Clips[clip].List = (short)ListCount;
            }
            return (short)ListCount++;
        }

        /// <summary>Adds a prompt to the bar.</summary>
        public void AddPrompt(UiAction action, string label)
        {
            if (PromptCount == Prompts.Length)
            {
                Array.Resize(ref Prompts, Prompts.Length * 2);
            }
            Prompts[PromptCount++] = new UiPrompt { Action = action, Label = label };
        }

        /// <summary>An item's box on the screen, its scroll region's movement applied.</summary>
        public void ScreenBox(int item, out float x, out float y, out float w, out float h)
        {
            ref UiItem it = ref Items[item];
            x = it.X;
            y = it.Y;
            w = it.W;
            h = it.H;
            if (it.Clip >= 0)
            {
                ref UiClip c = ref Clips[it.Clip];
                x += c.X;
                y += c.Y - c.Offset;
            }
        }

        /// <summary>A node's box on the screen, its scroll region's movement applied.</summary>
        public void NodeBox(int node, out float x, out float y, out float w, out float h)
        {
            ref UiNode n = ref Nodes[node];
            x = n.X;
            y = n.Y;
            w = n.W;
            h = n.H;
            if (n.Clip >= 0)
            {
                ref UiClip c = ref Clips[n.Clip];
                x += c.X;
                y += c.Y - c.Offset;
            }
        }

        /// <summary>The node that runs a command (with its argument), or -1.</summary>
        public int FindNode(int command, int arg)
        {
            for (int i = 0; i < NodeCount; i++)
            {
                if (Nodes[i].Command == command && Nodes[i].Arg == arg)
                {
                    return i;
                }
            }
            return -1;
        }
    }
}
