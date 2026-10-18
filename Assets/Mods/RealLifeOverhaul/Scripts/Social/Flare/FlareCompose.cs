namespace RealLifeOverhaul.Flare
{
    /// <summary>What the player is writing in Flare's compose sheet: the built part, the typed text, the picture.</summary>
    public sealed class FlareCompose
    {
        public FlareSource Kind = FlareSource.Life;   // Life: a moment of now; a draft's source; Report, Review, Announce or Reply
        public int Draft = -1;           // a moment from a draft
        public int Arg;                  // a report's kind, a review's stars, an announcement's kind
        public string Place, Name;       // the business it is about
        public string District;          // a report's, a moment's
        public FlareTone Tone = FlareTone.Cheerful;
        public bool ToneChosen;          // the player picked the tone (else the one that suits it best)
        public bool Tag = true;          // a draft's friend tagged
        public string Mention;           // an account called on: "o:0", "b:name", "p:player"
        public string Text;              // typed
        public string Pic;               // "f:" a picture of the player's
        public int ReplyTo = -1;         // a reply's post
        public int When = -1;            // an event's hour (day * 24 + hour)
        public string Tags;              // tags picked from the chips, a space between each

        public void Reset()
        {
            Kind = FlareSource.Life;
            Draft = -1;
            Arg = 0;
            Place = Name = District = Mention = Text = Pic = Tags = null;
            Tone = FlareTone.Cheerful;
            ToneChosen = false;
            Tag = true;
            ReplyTo = -1;
            When = -1;
        }
    }
}
