using System;

namespace RealLifeOverhaul.Ui
{
    /// <summary>What a row of the Stocks sheet is: a stepper, a choice that cycles, a switch, a fact, a line of words or a section's head.</summary>
    public enum StocksFieldKind : byte
    {
        Stepper = 0,
        Choice,
        Switch,
        Fact,
        Note,
        Head,
        Text           // a name typed in a field
    }

    /// <summary>One row of the sheet. The screen keys a value by Id; the builder says how it reads and how far it may move.</summary>
    public struct StocksFieldModel
    {
        public StocksFieldKind Kind;
        public int Id;
        public string Label, Value;
        public bool CanLess, CanMore, On, Typed, Off;
        public int Tone;            // a fact's or a note's: 1 good, -1 against, 0 plain
        public long Min, Max, Step; // a stepper's ends and its step
        public int Count;           // a choice's options
    }

    /// <summary>
    /// A sheet of the Stocks desk over its page: a bond to buy, a contract to open, a firm to hire, a rule to write. After the
    /// order ticket of the mockup: a column at the left with what it is about, the fields at the right under its title, the
    /// reason it cannot be done in one line, and its buttons at the foot with the main one last. Plain data.
    /// </summary>
    public sealed class StocksSheetModel
    {
        public string Kicker, Title;
        public UiIcon Emblem = UiIcon.ChartLineUp;
        public string Name, Big, Sub, About;
        public string[] SideLabels = new string[0], SideValues = new string[0];
        public StocksFieldModel[] Fields = new StocksFieldModel[0];
        public string Reason;
        public bool ReasonGood;
        public string Confirm, Back;
        public bool ConfirmOff;
        public UiActionModel[] Extras = new UiActionModel[0];
    }
}
