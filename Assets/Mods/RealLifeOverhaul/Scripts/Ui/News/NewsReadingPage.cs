using System;
using RealLifeOverhaul.Ledger;

namespace RealLifeOverhaul.Ui
{
    /// <summary>The game's side of the Newsstand's reading page: the editions kept, the reader's text, the printed page and the words. A check gives a plain one.</summary>
    public interface INewsReadDesk
    {
        /// <summary>The game's day.</summary>
        int Today { get; }

        /// <summary>Changes when the page's answer to a press does, so the reader is made again and not on every look.</summary>
        int Stamp { get; }

        /// <summary>The edition of a day: today's, or one kept on the phone; null if there is none.</summary>
        LedgerEdition Edition(int day);

        /// <summary>An edition as a title prints it, for the kit's reader.</summary>
        UiReaderModel Reader(LedgerEdition edition, int title);

        /// <summary>The edition opened as the printed page. False if it cannot be opened just now.</summary>
        bool ReadPrinted(LedgerEdition edition);

        /// <summary>A notice for the player, by its locale key.</summary>
        void Tell(string key);

        /// <summary>A text with its names filled, in pairs of name and value.</summary>
        string Fill(string key, params string[] pairs);

        string TitleName(int title);

        string DayWords(int day);

        string WeekPrice(int title);

        string DeliveryHour();
    }

    /// <summary>
    /// The Newsstand's Reading tab: only the papers you hold are opened, a paper and a day are picked, the Ledger's edition is read as the printed
    /// page, and a paper you do not hold shows the way to take it out. The old page's rules (NewsstandApp.Reading) on the kit. The reader is kept
    /// between looks so that its scroll is not lost. Game-free (EconomyCheck drives it); named so that the preview leaves it out.
    /// </summary>
    public sealed class NewsReadingPage
    {
        public readonly NewsstandReading Reading = new NewsstandReading();
        private UiReaderModel _reader;
        private LedgerEdition _readerOf;
        private int _readerTitle = -1, _readerStamp = -1;
        private INewsReadDesk _keptOf;
        private Func<int, bool> _kept;

        /// <summary>Fills the model for the Reading tab from what the player holds.</summary>
        public void Fill(NewsModel model, NewsstandState state, INewsReadDesk desk)
        {
            Settle(state, desk);
            model.Shape = NewsShape.Reading;
            model.Reader = null;
            model.ReadDays = new UiActionModel[0];
            model.ReadPrinted = default(UiActionModel);
            model.ReadNote = null;
            model.EmptyAction = desk.Fill("reallife_ledger_online_to_titles");
            model.ReadTitles = TitleChips(desk);
            switch (Reading.State)
            {
                case NewsstandReadState.Edition:
                    FillEdition(model, state, desk);
                    break;
                case NewsstandReadState.NotHeld:
                    string name = desk.TitleName(Reading.Title);
                    model.EmptyTitle = desk.Fill("reallife_news_read_not_held", "title", name);
                    model.EmptyBody = desk.Fill("reallife_news_read_not_held_body", "title", name, "money", desk.WeekPrice(Reading.Title));
                    break;
                default:
                    model.EmptyTitle = desk.Fill("reallife_news_read_none");
                    model.EmptyBody = desk.Fill("reallife_ledger_online_nothing", "hour", desk.DeliveryHour());
                    break;
            }
        }

        /// <summary>A press on the reading page. False for a press that changes nothing (a paper that is no paper, a day that cannot be read, a printed page that is not there).</summary>
        public bool Press(NewsModel model, int command, int arg, NewsstandState state, INewsReadDesk desk)
        {
            Settle(state, desk);
            switch (command)
            {
                case NewsCommands.ReadTitle:
                    return Reading.PickTitle(arg);
                case NewsCommands.ReadDay:
                    return Reading.PickDay(arg);
                case NewsCommands.ToTitles:
                    model.Tab = 0;
                    return true;
                case NewsCommands.ReadPrinted:
                    LedgerEdition edition = Reading.State == NewsstandReadState.Edition && Reading.Title == NewsstandCatalogue.Ledger ? desk.Edition(Reading.Day) : null;
                    if (edition == null)
                    {
                        return false;
                    }
                    if (!desk.ReadPrinted(edition))
                    {
                        desk.Tell("reallife_ledger_online_no_printed");
                    }
                    return true;
                default:
                    return false;
            }
        }

        private void Settle(NewsstandState state, INewsReadDesk desk)
        {
            if (!ReferenceEquals(_keptOf, desk))
            {
                _keptOf = desk;
                _kept = day => desk.Edition(day) != null;
            }
            Reading.Settle(state, desk.Today, _kept);
        }

        private UiActionModel[] TitleChips(INewsReadDesk desk)
        {
            var chips = new UiActionModel[NewsstandCatalogue.Titles];
            for (int t = 0; t < chips.Length; t++)
            {
                chips[t] = new UiActionModel
                {
                    Label = desk.TitleName(t),
                    Kind = t == Reading.Title ? UiButtonKind.Chip : UiButtonKind.Secondary,
                    Key = UiAction.Count,
                    Command = NewsCommands.ReadTitle,
                    Arg = t
                };
            }
            return chips;
        }

        private void FillEdition(NewsModel model, NewsstandState state, INewsReadDesk desk)
        {
            int title = Reading.Title;
            LedgerEdition edition = desk.Edition(Reading.Day);
            var days = new UiActionModel[Reading.Days.Count];
            for (int i = 0; i < days.Length; i++)
            {
                days[i] = new UiActionModel
                {
                    Label = desk.DayWords(Reading.Days[i]),
                    Kind = Reading.Days[i] == Reading.Day ? UiButtonKind.Chip : UiButtonKind.Secondary,
                    Key = UiAction.Count,
                    Command = NewsCommands.ReadDay,
                    Arg = Reading.Days[i]
                };
            }
            model.ReadDays = days;
            if (title == NewsstandCatalogue.Ledger && edition != null)
            {
                model.ReadPrinted = new UiActionModel
                {
                    Label = desk.Fill("reallife_ledger_online_printed"),
                    Kind = UiButtonKind.Primary,
                    Key = UiAction.Count,
                    Command = NewsCommands.ReadPrinted
                };
            }
            if (!NewsstandRules.Covered(state, title))
            {
                model.ReadNote = desk.Fill("reallife_ledger_online_box_kept");
            }
            if (edition == null)
            {
                model.EmptyTitle = desk.Fill("reallife_ledger_online_gone");
                return;
            }
            if (_reader == null || !ReferenceEquals(_readerOf, edition) || _readerTitle != title || _readerStamp != desk.Stamp)
            {
                _reader = desk.Reader(edition, title);
                _readerOf = edition;
                _readerTitle = title;
                _readerStamp = desk.Stamp;
            }
            model.Reader = _reader;
        }
    }
}
