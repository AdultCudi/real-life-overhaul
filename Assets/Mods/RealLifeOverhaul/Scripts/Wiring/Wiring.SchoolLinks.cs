using System;
using System.Collections.Generic;
using RealLifeOverhaul.Careers;
using RealLifeOverhaul.Family;
using RealLifeOverhaul.Schools;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The schools (Schools/SchoolsBridges) with the rest of the mod:
        ///
        ///   Children, SetSchool   family life's children and their schooling (FamilyApi); and family life asks the campus
        ///                         a child's fee, reports and gate (FamilyBridges.School)
        ///   Council               the council's money for schools: Civic's grants lever, as the university's fees read it
        ///   SchoolsTab            the Campus app's Schools tab (CareersBridges.SchoolsTab), written by the schools' page
        ///   News.Extra            the morning's City Ledger: the schools' news, a lead on the front page
        ///
        /// Asked on a day's pass, a page or a press, never per frame; a fault is said once (<see cref="Fault"/>) and the
        /// bridge's default answered.
        /// </summary>
        public static class SchoolLinks
        {
            private static Action<int, List<NewsItem>> _paperBefore;
            private static readonly List<SchoolStory> Stories = new List<SchoolStory>(6);
            private static readonly List<string> Names = new List<string>(4);
            private static readonly List<float> Ages = new List<float>(4);
            private static readonly List<int> Schooling = new List<int>(4);
            private static readonly List<bool> Grown = new List<bool>(4);

            internal static void Connect()
            {
                SchoolsBridges.Children = Children;
                SchoolsBridges.SetSchool = (name, campus) => FamilyApi.SetSchool(name, campus);
                SchoolsBridges.Council = () => CivicPolicy.Position(Lever.Grants);   // neutral while civic life is off
                FamilyBridges.School = new CampusSchool();
                CareersBridges.SchoolsTab = SchoolsPage.Write;
                _paperBefore = News.Extra;
                News.Extra = Paper;
            }

            internal static void Disconnect()
            {
                SchoolsBridges.Reset();
                FamilyBridges.School = null;
                CareersBridges.SchoolsTab = null;
                News.Extra = _paperBefore;
                _paperBefore = null;
            }

            // The player's children as family life has them.
            private static int Children(List<SchoolChild> into)
            {
                into.Clear();
                try
                {
                    int n = FamilyApi.ChildrenOf(Names, Ages, Schooling, Grown);
                    for (int i = 0; i < n; i++)
                    {
                        into.Add(new SchoolChild { Name = Names[i], Age = Ages[i], School = Schooling[i], Grown = Grown[i] });
                    }
                }
                catch (Exception e)
                {
                    Fault("SchoolLinks.Children", e);
                }
                return into.Count;
            }

            // What family life asks the campus of a child, each answered with family life's own default after a fault.
            private sealed class CampusSchool : ISchoolBridge
            {
                public int FeeFor(string child)
                {
                    try
                    {
                        return SchoolsDirector.FeeFor(child);
                    }
                    catch (Exception e)
                    {
                        Fault("SchoolLinks.FeeFor", e);
                        return -1;
                    }
                }

                public float StandingOf(string child)
                {
                    try
                    {
                        return SchoolsDirector.StandingOf(child);
                    }
                    catch (Exception e)
                    {
                        Fault("SchoolLinks.StandingOf", e);
                        return float.NaN;
                    }
                }

                public bool AddressFor(string child, out string street, out int number)
                {
                    try
                    {
                        return SchoolsDirector.AddressFor(child, out street, out number);
                    }
                    catch (Exception e)
                    {
                        Fault("SchoolLinks.AddressFor", e);
                        street = null;
                        number = 0;
                        return false;
                    }
                }
            }

            // The schools' news in the morning's City Ledger: a lead goes to the front page (so the Ledger's account on Flare
            // posts it first), the rest after the others' pieces.
            private static void Paper(int day, List<NewsItem> into)
            {
                try
                {
                    _paperBefore?.Invoke(day, into);
                }
                catch (Exception e)
                {
                    Fault("SchoolLinks.Paper (the pieces before)", e);
                }
                try
                {
                    SchoolsDirector.Stories(day, Stories);
                    bool led = false;
                    for (int i = 0; i < Stories.Count; i++)
                    {
                        var item = new NewsItem { Key = Stories[i].Key, Data = Stories[i].Data };
                        if (Stories[i].Lead && !led && into.Count > 0)
                        {
                            into.Insert(1, item);   // after the masthead
                            led = true;
                        }
                        else
                        {
                            into.Add(item);
                        }
                    }
                    Stories.Clear();
                }
                catch (Exception e)
                {
                    Fault("SchoolLinks.Paper", e);
                }
            }
        }
    }
}
