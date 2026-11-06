namespace RealLifeOverhaul.Shops
{
    /// <summary>The locale keys of the new kinds of shop, written out whole so the text checks find them.</summary>
    public static class ShopsText
    {
        public const string Header = "reallife_shops_header";                    // the options page's group
        public const string Enabled = "reallife_shops_enabled";                  // the option
        public const string TestSay = "reallife_shops_test_say";                 // the test button: are the kinds there
        public const string TestToday = "reallife_shops_test_today";             // the test button: one of the player's shops today
        public const string TestHelp = "reallife_shops_test_help";               // the test button: a kind's page in the game's help
        public const string SayOk = "reallife_shops_say_ok";                     // {count} kinds, offered in BizMan
        public const string SayHidden = "reallife_shops_say_hidden";             // {count} kinds, not offered (the option is off)
        public const string SayMissing = "reallife_shops_say_missing";           // {count} of {all} kinds ready
        public const string NoShop = "reallife_shops_none";                      // the player has none of the kinds
        public const string Unmet = "reallife_shops_unmet";                      // {name} cannot trade: {need}
        public const string Closed = "reallife_shops_closed";                    // {name} is closed all day today
        public const string Today = "reallife_shops_today";                      // {name}: {count} customers today, {wanting} for its goods
        public const string HelpFailed = "reallife_shops_help_failed";           // the game's help screen is not there
    }
}
