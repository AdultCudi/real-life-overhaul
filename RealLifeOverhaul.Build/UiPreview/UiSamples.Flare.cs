using RealLifeOverhaul.Ui;

internal static partial class UiSamples
{
    public static readonly string[] FlareStates =
    {
        "home_empty", "home", "explore", "profile", "business", "thread", "messages", "notices", "compose", "hire", "city", "city_new"
    };

    public static void Flare(UiScene s, UiFrame frame, bool dark, IUiKeys keys, UiWords t, string state)
    {
        FlareFace.Lay(s, frame, dark, keys, t, FlareSample(t, state));
    }

    public static FlareFaceModel FlareSample(UiWords t, string state)
    {
        var m = new FlareFaceModel
        {
            Name = "flare_" + state,
            Nav = new[]
            {
                t("reallife_flare_nav_home", "Home"), t("reallife_flare_nav_explore", "Explore"), t("reallife_flare_nav_notices", "Notices"),
                t("reallife_flare_nav_messages", "Messages"), t("reallife_flare_nav_profile", "Profile"), t("reallife_flare_nav_business", "Business"),
                t("reallife_flare_nav_city", "In the city"), t("reallife_flare_nav_hire", "Hire"), t("reallife_flare_nav_studio", "Studio")
            },
            NavIcon = new[]
            {
                UiIcon.Home, UiIcon.Search, UiIcon.Bell, UiIcon.Message, UiIcon.Person, UiIcon.Store, UiIcon.Building, UiIcon.People, UiIcon.Chart
            },
            Post = t("reallife_flare_post", "Post"),
            Field = t("reallife_flare_field", "What's happening?"),
            Chips = new[]
            {
                t("reallife_flare_chip_photo", "Photo"), t("reallife_flare_chip_place", "Place"), t("reallife_flare_chip_tag", "Tag"), t("reallife_flare_chip_promote", "Promote")
            },
            EmptyTitle = t("reallife_flare_empty_feed", "Nothing posted yet."),
            EmptyBody = t("reallife_flare_empty_following", "Nothing yet from the accounts you follow. Follow some from Explore.")
        };
        switch (state)
        {
            case "home_empty":
                m.Page = 0;
                m.Feed = new UiFeedModel { EmptyTitle = m.EmptyTitle, EmptyBody = m.EmptyBody, Profile = Profile(t, "0") };
                break;
            case "explore":
                m.Page = 1;
                m.NavAt = 1;
                m.Feed = Feed(t, true);
                break;
            case "profile":
                m.Page = 4;
                m.NavAt = 4;
                Rows(m, t, t("reallife_flare_nav_profile", "Profile"));
                break;
            case "business":
                m.Page = 5;
                m.NavAt = 5;
                Rows(m, t, t("reallife_flare_nav_business", "Business"));
                break;
            case "thread":
                m.Page = 9;
                m.Head = t("reallife_flare_nav_messages", "Messages");
                Rows(m, t, t("reallife_flare_terms", "The offer"));
                break;
            case "messages":
                m.Page = 3;
                m.NavAt = 3;
                m.Messages = new UiMessageListModel
                {
                    SearchPlaceholder = t("reallife_flare_field", "What's happening?"),
                    Rows = new[]
                    {
                        new UiMessageRow { Name = t("reallife_flare_you", "You"), Preview = t("reallife_flare_field", "What's happening?"), Time = "2h", Hue = 28f, Section = t("reallife_flare_nav_messages", "Messages") }
                    },
                    RowCount = 1
                };
                break;
            case "notices":
                m.Page = 2;
                m.NavAt = 2;
                Rows(m, t, t("reallife_flare_nav_notices", "Notices"));
                m.Action = t("reallife_flare_answer", "Answer");
                break;
            case "compose":
                m.Page = 7;
                m.Head = t("reallife_flare_expect", "What to expect");
                Rows(m, t, t("reallife_flare_expect", "What to expect"));
                m.Action = t("reallife_flare_post", "Post");
                break;
            case "hire":
                m.Page = 8;
                m.NavAt = 7;
                Rows(m, t, t("reallife_flare_nav_hire", "Hire"));
                break;
            case "city":
                m.Page = 6;
                m.NavAt = 6;
                Rows(m, t, t("reallife_flare_nav_city", "In the city"));
                break;
            case "city_new":
                m.Page = 6;
                m.NavAt = 6;
                break;
            default:
                m.Page = 0;
                m.Story = new[] { t("reallife_flare_you", "You"), t("reallife_flare_nav_business", "Business") };
                m.StoryHue = new[] { 28f, 200f };
                m.Feed = Feed(t, false);
                break;
        }
        return m;
    }

    private static UiFeedProfile Profile(UiWords t, string followers)
    {
        return new UiFeedProfile
        {
            Name = t("reallife_flare_you", "You"),
            Handle = "@me",
            Hue = 28f,
            FactLabels = new[] { t("reallife_flare_followers", "Followers"), t("reallife_flare_posts", "Posts") },
            FactValues = new[] { followers, "3" }
        };
    }

    private static UiFeedModel Feed(UiWords t, bool tag)
    {
        return new UiFeedModel
        {
            PostCount = 2,
            Posts = new[]
            {
                new UiFeedPost
                {
                    Author = t("reallife_flare_you", "You"), Hue = 28f, Handle = "@me", Time = "1h",
                    Text = t("reallife_flare_field", "What's happening?"), Picture = UiIcon.Image,
                    Caption = t("reallife_flare_result", "46 visits, +$1,180"),
                    Likes = "12", Comments = "2", Shares = "1", Liked = true
                },
                new UiFeedPost
                {
                    Author = t("reallife_flare_nav_business", "Business"), Icon = UiIcon.Store, Handle = "@shop", Time = "3h",
                    Text = tag ? t("reallife_flare_trend", "Trending near you") : t("reallife_flare_empty_following", "Nothing yet from the accounts you follow. Follow some from Explore."),
                    Likes = "4", Comments = "0", Shares = "0"
                }
            },
            Profile = Profile(t, "5,000")
        };
    }

    private static void Rows(FlareFaceModel m, UiWords t, string head)
    {
        m.Head = head;
        m.Links = 1;
        m.Label = new[]
        {
            t("reallife_flare_followers", "Followers"), t("reallife_flare_visits", "Visits"), t("reallife_flare_takings", "Takings"),
            t("reallife_flare_hours", "Hours"), t("reallife_flare_rating", "Rating"), t("reallife_flare_perk_job", "Job offers")
        };
        m.Value = new[] { "5,000", "46", "$1,180", "6", "4.2", "+8%" };
    }
}
