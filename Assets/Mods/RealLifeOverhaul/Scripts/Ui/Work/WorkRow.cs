using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Ui
{
    /// <summary>A button of a row's detail: what it is called, how it is drawn and the app's own action number behind it.</summary>
    public sealed class WorkAct
    {
        public int Kind;
        public int Arg;
        public string Label;
        public UiButtonKind Style = UiButtonKind.Secondary;
        public UiIcon Icon = UiIcon.Check;
        public string Why;          // a reason it cannot be done now; the button is drawn disabled and the reason said
        public bool Opens;          // it opens a page or a place and changes nothing else
        public string Note;         // a choice's consequence, said under it (a haggle's numbered choices)
        public bool Choice;         // a numbered choice of a card (the keys 1 to 9)
    }

    /// <summary>A requirement of a post, a course or a deal, met or not, with what the player has against it (shown at its right).</summary>
    public struct WorkNeed
    {
        public string Words;
        public bool Met;
        public string Have;
    }

    /// <summary>A graded bar of a detail: a skill, a record, with what a course adds to it.</summary>
    public struct WorkBar
    {
        public string Label;
        public int Value;           // 0 to 100
        public string Note;         // "+12": what the course adds, in the good tone; null for none
    }

    /// <summary>A person in a detail or a list under it: a face, what they are, and a figure or words at the right.</summary>
    public sealed class WorkPerson
    {
        public string Name, Role;
        public string Right;        // "Day 399", "4.1", "$27,900"
        public string Label;        // what the record is of ("Craft"); with Score a graded bar
        public int Score = -1;
        public string Label2;
        public int Score2 = -1;
        public UiIcon Icon = UiIcon.None;   // a glyph on a tile instead of a face (a thing, not a person)
        public UiRole Ring = UiRole.None;
        public WorkAct Open;        // pressing them opens their page; null for none
        public string Chip;         // a chip before the figure ("Rival for the post"); null for none
        public UiRole ChipTone = UiRole.Warn;
        public int Halves = -1;     // a rating in halves of five stars, drawn as stars before the figure; -1 for none
    }

    /// <summary>What a section of a detail is made of.</summary>
    public enum WorkPart : byte
    {
        Pairs, Checks, Bars, People, Text, Note, Amount
    }

    /// <summary>One section of a detail: a capitals title, a note at its right, and its rows.</summary>
    public sealed class WorkSection
    {
        public WorkPart Part;
        public string Title, Note;
        public readonly List<KeyValuePair<string, string>> Pairs = new List<KeyValuePair<string, string>>(6);
        public readonly List<WorkNeed> Checks = new List<WorkNeed>(6);
        public readonly List<WorkBar> Bars = new List<WorkBar>(6);
        public readonly List<WorkPerson> People = new List<WorkPerson>(6);
        public string Text;         // a paragraph (Text) or the words after the bold lead (Note)
        public string Lead;         // a note's bold first words
        public UiIcon NoteIcon = UiIcon.Info;
        public int Stepper = -1;    // an amount's place in the page's steppers
    }

    /// <summary>
    /// An amount or a choice the player steps on a page: an offer, a haggle, a post count, a level among a few words. The value lives in the screen's
    /// view (the source reads it back when a button is pressed); the figure is words the source wrote for it.
    /// </summary>
    public sealed class WorkAmount
    {
        public int Id;
        public int Value, Min, Max, Step = 1;
        public string Figure;
        public string[] Words;      // a choice of a few words: Value is the one chosen; null for a figure typed or stepped
        public bool CanLess => Value > Min;
        public bool CanMore => Value < Max;
    }

    /// <summary>
    /// One row of a tab, whichever shape the tab takes: a line of its table, a card of its catalogue, and the detail it shows when chosen. Every
    /// string is already in the player's words. Game-free.
    /// </summary>
    public sealed class WorkRow
    {
        public int Id;
        public int Order;                      // the figure a list is sorted by when it is not one of its cells (a post's pay, in cents)
        // the face of the row
        public UiIcon Icon = UiIcon.Briefcase;
        public string Picture;                 // the game's own picture of the thing, by its UiPictures key; null for the glyph
        public UiRole Tint = UiRole.None;
        public string Name, Sub;               // its name and a line under it
        public string Person;                  // a portrait of this name in place of a tile: the row is a person
        public UiRole Ring = UiRole.None;      // their mood
        public string[] Cells = new string[0]; // by the tab's columns, the first the name
        public int[] Values = new int[0];      // by column: a graded figure, a glyph, a trend's direction
        public UiRole[] Tones = new UiRole[0]; // by column
        public string Status;                  // a chip: a table's state cell, a card's tag
        public UiRole Tone = UiRole.Ok;
        public int Grade = -1;
        // a card
        public string Line;                    // a line of words on the card
        public string FootLeft, FootRight;     // the card's foot: its length and its price
        public string NextLabel, NextValue;    // a course card's "Next class" line
        public string Seller;                  // a seller's portrait and name on the foot of a marketplace card
        public string Price, Under;            // a listing's price and its percentage against its worth ("-12%")
        public int UnderValue;                 // the same, as a number, for its tone
        // filters
        public int Group = -1;                 // the chip it belongs to (one of the tab's groups); -1 every chip
        public int Mask;                       // bits: the chips that count it (Open now, You qualify, Near you)
        // its detail
        public string Kicker;                  // capitals over the title in the detail
        public string Title;                   // the detail's title when it is not the name
        public string HeadSub;                 // a line beside the chip under the title (where it is)
        public string Chip;                    // a chip beside the title: what the employer is, a state
        public UiRole ChipTone = UiRole.Ok;
        public string Sentence;                // words under the head
        public readonly List<KeyValuePair<string, string>> Figures = new List<KeyValuePair<string, string>>(4);   // a grid of big figures under the head
        public readonly List<WorkSection> Sections = new List<WorkSection>(6);
        public readonly List<WorkFormBand> Bands = new List<WorkFormBand>(3);   // a page's own bands under its card (a haggle's seller and listings)
        public readonly List<WorkAct> Acts = new List<WorkAct>(4);
        public WorkAct Locate;                 // the row's own link to the map: the glyph at its right in a table
        public WorkAct Open;                   // pressing the row opens a page of its own (a person)
        public string Hint;                    // a second sentence the detail ends on (the desk line)
        public UiIcon HintIcon = UiIcon.Info;
        public string HintLead;
    }

    /// <summary>The shapes a tab takes: each a layout of its own, so no two tabs are one table and a box.</summary>
    public enum WorkShape : byte
    {
        Table, Cards, Diary, Statement, Skills, Profiles, Form, Reader, Haggle
    }

    /// <summary>A list at the left of a squad page: the open roles an applicants page is about.</summary>
    public sealed class WorkAside
    {
        public string Title, Count;
        public readonly List<WorkRow> Rows = new List<WorkRow>(8);
        public string Foot;                    // a sentence at the foot of the column
        public string ListTitle, ListCount;    // a second list under the first (hired this month)
        public readonly List<WorkPerson> List = new List<WorkPerson>(6);
    }

    /// <summary>One piece of the term's diary as a person reads it: a class, homework, an exam, a fee.</summary>
    public sealed class WorkDiary
    {
        public UiDiaryModel Month = new UiDiaryModel();
        public string[] Rows = new string[0];
    }

    /// <summary>A line of a transcript.</summary>
    public sealed class WorkMark
    {
        public int Id;
        public UiIcon Icon = UiIcon.Cap;
        public string Course, Place, Credits, Attendance, Exam, Letter, Percent, Status;
        public int ExamGrade = -1, AttendanceGrade = -1, LetterGrade = -1;
        public UiRole Tone = UiRole.Ok;
        public string Term;                    // the term it belongs to (a section row over its lines)
    }

    /// <summary>One section row of a transcript: a term with its days and what it holds.</summary>
    public sealed class WorkTerm
    {
        public string Title, Note;
        public readonly List<WorkMark> Marks = new List<WorkMark>(6);
    }

    /// <summary>A student's transcript page: the head with the average, the terms, the average by quarter, what is coming up and the class.</summary>
    public sealed class WorkStatement
    {
        public UiProfileModel Head;
        public string[] FigureLabels = new string[0], FigureValues = new string[0];
        public float CreditShare = -1f;
        public readonly List<WorkTerm> Terms = new List<WorkTerm>(4);
        public string TranscriptTitle, TotalLabel, TotalCredits, TotalGpa;
        public string[] Heads = new string[0];   // the transcript's column heads: course, credits, attendance, exam, grade, state
        public UiChartModel Chart;
        public string ComingTitle;
        public readonly List<KeyValuePair<string, string>> Coming = new List<KeyValuePair<string, string>>(4);
        public string ClassTitle, ClassNote;
        public readonly List<WorkPerson> Class = new List<WorkPerson>(8);
        public string GainTitle;
        public readonly List<WorkBar> Gains = new List<WorkBar>(8);
        public string EmptyTitle, EmptyBody;
        public readonly List<WorkFormBand> Below = new List<WorkFormBand>(2);   // bands under the transcript's column: what the degrees ask, in what the page cannot say in its lines
    }

    /// <summary>A child at university or in school: their profile, their college fund as a meter, and what can be done.</summary>
    public sealed class WorkKid
    {
        public string Name, Line, Role, FundLabel, FundNote;
        public int Fund = -1;
        public UiRole Ring = UiRole.Ok;
        public string[] FactLabels = new string[0], FactValues = new string[0];
        public readonly List<WorkAct> Acts = new List<WorkAct>(2);
    }

    /// <summary>A form's control: a label with a stepper, a switch, a choice or a figure, or a person or a button beneath.</summary>
    public enum WorkField : byte
    {
        Label, Stepper, Switch, Choice, Button, Person, Bar, Text, Tile, Check, Nudge
    }

    /// <summary>One row of a form band: a label with its control, a figure, a person or a button.</summary>
    public sealed class WorkFormRow
    {
        public WorkField Field;
        public string Label, Value, Hint;
        public int Stepper = -1;               // for a stepper: its place in the page's amounts
        public bool On;                        // for a switch
        public WorkAct Act, Act2;              // what a press does; a nudge's less and more
        public readonly List<WorkAct> More = new List<WorkAct>(2);   // more buttons beside the first, in the same row
        public int ActAt = -1;                 // its place in the page's buttons, set when the page is built
        public WorkPerson Person;
        public WorkBar Bar;
        public UiIcon Icon = UiIcon.None;
        public UiRole Tone = UiRole.Ok;
        public string Why;
    }

    /// <summary>A band of a form: a capitals title, a count, and its rows.</summary>
    public sealed class WorkFormBand
    {
        public string Title, Count, Note;
        public readonly List<WorkFormRow> Rows = new List<WorkFormRow>(6);
        public WorkAct Foot;                   // a button under the band
        public string FootWhy;                 // why that button is drawn dimmed, said beside it
        public int FootAt = -1;
        public bool Grow;                      // the band takes the rest of its column
    }

    /// <summary>A form of one or two columns of bands.</summary>
    public sealed class WorkFormModel
    {
        public readonly List<WorkFormBand> Left = new List<WorkFormBand>(4), Right = new List<WorkFormBand>(4);
        public string Lead;                    // a sentence over the form
        public UiProfileModel Head;
        public readonly List<WorkFormBand> Below = new List<WorkFormBand>(3);   // bands under both columns, up to three across: what fills the foot of a short page
    }

    /// <summary>One tab of an app: its shape, and everything the shape needs. Game-free.</summary>
    public sealed class WorkTab
    {
        public WorkShape Shape;
        public string Title, Lead;
        public UiProfileModel Profile;
        public UiDataColumn[] Columns = new UiDataColumn[0];
        public readonly List<WorkRow> Rows = new List<WorkRow>(24);
        public string[] Chips = new string[0];         // the names of the filter chips after "All"; the page adds every count
        public bool ChipsByMask;                       // the chips count the rows' masks; else their groups
        public string AllWord;                         // "All" in the player's words
        public string Search;                          // a search field's hint; null for none
        public UiIcon EmptyIcon = UiIcon.Search;
        public string EmptyTitle, EmptyBody;
        public WorkAct EmptyAct;
        public string EmptyActLabel;
        public WorkAside Aside;
        public readonly List<WorkAct> Acts = new List<WorkAct>(3);   // buttons of the page itself (a new ad, a search)
        public readonly List<WorkAmount> Amounts = new List<WorkAmount>(3);
        public string SortWord;                        // a sort choice's words
        // shapes
        public WorkDiary Diary;
        public WorkStatement Statement;
        public readonly List<WorkBar> Skills = new List<WorkBar>(12);
        public readonly List<string> SkillPlaces = new List<string>(12);
        public readonly List<UiIcon> SkillIcons = new List<UiIcon>(12);   // by skill: a cap where it was taught at a school, a briefcase where work taught it
        public readonly List<WorkKid> Kids = new List<WorkKid>(3);
        public WorkFormModel Form;
        public UiReaderModel Reader;
        public WorkRow PersonPage;                     // a person's page, when one is open
        public string[] PersonTabs = new string[0];
        public bool Sub;                               // a sub page (a person, a haggle): Back steps out of it
        public WorkRow Haggle;                         // a haggle's card, when one is open
        public string NoteLead, NoteText;
        /// <summary>Bands under the page's main part, in up to three columns: related lists, meters, the person. They have no buttons of their own; what fills a page's foot is facts.</summary>
        public readonly List<WorkFormBand> Below = new List<WorkFormBand>(3);
        public readonly List<KeyValuePair<string, string>> Stats = new List<KeyValuePair<string, string>>(4);   // figures that sum a short list, drawn in a strip under it
    }

    /// <summary>An app of the work apps in facts: its sections and a tab for each. Game-free.</summary>
    public sealed class WorkApp
    {
        public bool Ready;
        public UiNavEntry[] Nav = new UiNavEntry[0];
        public WorkTab[] Tabs = new WorkTab[0];
        public string OffTitle, OffBody;
        public string Head;                            // what the window's strip says at its right ("Day 402, 1 course under way")
    }
}
