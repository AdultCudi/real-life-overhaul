using System;
using System.Collections.Generic;

namespace RealLifeOverhaul.Tech
{
    /// <summary>One story of the tech sector's for the morning paper: the day it is printed, its key and what fills it.</summary>
    public sealed class TechStory
    {
        public int Day;
        public string Key = string.Empty;
        public string A = string.Empty, AName = string.Empty, B = string.Empty, BName = string.Empty;
    }

    /// <summary>
    /// The tech sector's stories for the City Ledger, kept so that the paper of a day is the same however often it is
    /// asked for and after a reload: one line of the save under <see cref="Key"/> (the last <see cref="Most"/>
    /// stories), written by <see cref="TechWriter"/>. Game-free.
    /// </summary>
    public sealed class TechNews
    {
        public const string Key = "reallife:tech_news";
        public const int Version = 1;
        public const int Most = 40;
        public const int PerDayMost = 3;

        public readonly List<TechStory> Stories = new List<TechStory>(Most);

        /// <summary>A story for the paper of a day; dropped if that day already has its share. The oldest goes when the list is full.</summary>
        public bool Add(int day, string key, string aName, string a, string bName = "", string b = "")
        {
            int that = 0;
            for (int i = 0; i < Stories.Count; i++)
            {
                if (Stories[i].Day == day)
                {
                    if (Stories[i].Key == key && Stories[i].A == (a ?? string.Empty))
                    {
                        return false;
                    }
                    that++;
                }
            }
            if (that >= PerDayMost)
            {
                return false;
            }
            if (Stories.Count >= Most)
            {
                Stories.RemoveAt(0);
            }
            Stories.Add(new TechStory { Day = day, Key = key ?? string.Empty, AName = aName ?? string.Empty, A = a ?? string.Empty, BName = bName ?? string.Empty, B = b ?? string.Empty });
            return true;
        }

        public void For(int day, List<TechStory> into)
        {
            for (int i = 0; i < Stories.Count; i++)
            {
                if (Stories[i].Day == day)
                {
                    into.Add(Stories[i]);
                }
            }
        }

        public string Write()
        {
            var w = new TechWriter(Version);
            w.Part('s');
            for (int i = 0; i < Stories.Count; i++)
            {
                TechStory s = Stories[i];
                w.Record().Add(s.Day).Add(s.Key).Add(s.AName).Add(s.A).Add(s.BName).Add(s.B);
            }
            return w.ToString();
        }

        public static TechNews Read(string text)
        {
            var news = new TechNews();
            if (string.IsNullOrEmpty(text))
            {
                return news;
            }
            try
            {
                var r = new TechReader(text);
                foreach (Fields f in r.Records('s'))
                {
                    if (news.Stories.Count < Most)
                    {
                        news.Stories.Add(new TechStory { Day = f.Int(), Key = f.Text(), AName = f.Text(), A = f.Text(), BName = f.Text(), B = f.Text() });
                    }
                }
            }
            catch (Exception)
            {
                news.Stories.Clear();
            }
            return news;
        }
    }
}
