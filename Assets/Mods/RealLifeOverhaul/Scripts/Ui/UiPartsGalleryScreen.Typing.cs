namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The parts' gallery's words typed through the host's real text field (IUiTyping, beside the host): the business's
    /// name or the goods' search, whichever field has focus, held to the name's limit as the game's own field holds it.
    /// </summary>
    public sealed partial class UiPartsGalleryScreen : IUiTyping
    {
        public int TypingCommand => UiPartsGallery.Field;

        public string TypingText => _state.Focused == UiPartsGallery.Arg(true, 2, 0) ? _state.Name : _state.Search;

        public void Typed(string text)
        {
            text = text ?? string.Empty;
            if (text.Length > NameLimit)
            {
                text = text.Substring(0, NameLimit);   // as the game's own name field holds its limit
            }
            if (_state.Focused == UiPartsGallery.Arg(true, 2, 0))
            {
                _state.Name = text;
            }
            else
            {
                _state.Search = text;
            }
            _dirty = true;
        }

        private const int NameLimit = 32;   // characters a business's name may run to
    }
}
