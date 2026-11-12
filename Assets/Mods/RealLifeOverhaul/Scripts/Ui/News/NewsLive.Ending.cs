using System;
using System.Text;
using RealLifeOverhaul.Ledger;
using RealLifeOverhaul.Outlook;
using RealLifeOverhaul.Permadeath;
using RealLifeOverhaul.Rivals;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The permadeath ending as a decision card: the late player's portrait, the obituary the story writes, a strip of the life's
    /// facts, and two numbered choices with nothing under them.
    /// </summary>
    public sealed partial class NewsLive
    {
        private void FillEnding(NewsModel model)
        {
            model.Shape = NewsShape.Ending;
            LifeStory story = EndingStory ?? new LifeStory();
            ObituaryPage page = PermadeathText.Page(story, false);
            model.Decision = new UiDecisionModel
            {
                Kicker = page.Kicker.Length > 0 ? page.Kicker : OutlookText.T("reallife_news_end_kicker"),
                Title = page.Headline.Length > 0 ? page.Headline : OutlookText.T("reallife_news_end_title"),
                Portrait = story.Name.Length > 0 ? story.Name : null,
                PortraitLines = PortraitLines(story, page),
                Emblem = UiIcon.Person,
                Body = Obituary(page, story),
                BodyLines = 10,
                FactLabels = new[]
                {
                    OutlookText.T("reallife_news_end_days"), OutlookText.T("reallife_news_end_shops"), OutlookText.T("reallife_news_end_staff"),
                    OutlookText.T("reallife_news_end_worth"), OutlookText.T("reallife_news_end_rank")
                },
                FactValues = Facts(story),
                Prompts = model.Prompts,
                PromptCount = model.Prompts.Length,
                Choices = new[]
                {
                    new UiDecisionChoice { Key = UiAction.Pick1, Name = OutlookText.T("reallife_news_end_load") },
                    new UiDecisionChoice { Key = UiAction.Pick2, Name = OutlookText.T("reallife_news_end_quit") }
                }
            };
        }

        // The whole obituary the life wrote: its standfirst, its story, and what it says of those the life left behind.
        private static string Obituary(ObituaryPage page, LifeStory story)
        {
            var text = new StringBuilder(640);
            Append(text, page.Stand);
            Append(text, page.Body);
            for (int i = 0; i < page.Texts.Count; i++)
            {
                for (int k = 0; k < page.Texts[i].Count; k++)
                {
                    Append(text, page.Texts[i][k]);
                }
            }
            if (text.Length == 0)
            {
                Append(text, OutlookText.T("reallife_news_end_body"));
            }
            return text.ToString();
        }

        private static void Append(StringBuilder text, string part)
        {
            if (string.IsNullOrEmpty(part))
            {
                return;
            }
            text.Append(text.Length > 0 ? " " : string.Empty).Append(part);
        }

        // Under the portrait: their age, and the career they had.
        private static string[] PortraitLines(LifeStory story, ObituaryPage page)
        {
            var lines = new System.Collections.Generic.List<string>(3);
            if (page.EarRight.Length > 0)
            {
                lines.Add(page.EarRightLabel + " " + page.EarRight);
            }
            if (story.Career.Length > 0)
            {
                lines.Add(story.Career);
            }
            return lines.ToArray();
        }

        // Days in the city, businesses, people employed, net worth and the place on the rich list.
        private static string[] Facts(LifeStory story)
        {
            int rank = 0;
            RivalFile file = RivalDirector.Instance != null ? RivalDirector.Instance.File : null;
            if (file != null)
            {
                rank = RivalRules.Rank(file, story.Worth, RealLifeOverhaul.Prices.Level);
            }
            return new[]
            {
                story.Days.ToString(Inv),
                story.Owned.ToString(Inv),
                story.Employees.ToString(Inv),
                LedgerText.Large(story.Worth),
                rank > 0 ? "#" + rank.ToString(Inv) : "-"
            };
        }
    }
}
