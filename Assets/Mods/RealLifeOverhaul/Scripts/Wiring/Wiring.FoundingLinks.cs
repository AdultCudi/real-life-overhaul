using System;
using System.Collections.Generic;
using Entities;
using RealLifeOverhaul.Tech;
using Streets;

namespace RealLifeOverhaul
{
    public static partial class Wiring
    {
        /// <summary>
        /// The start days of the player's businesses (Core/FoundingGame) with the moving companies' messages (Tech/Firms/MovesGame) and the game's
        /// statements: a business moved before the days were kept has none of the game's, and its day is estimated once, from the day the
        /// statements of the building it left begin (the game keeps a year of them). Asked once for each such business a session; nothing per frame.
        /// </summary>
        public static class FoundingLinks
        {
            private static readonly List<MoveSeen> Seen = new List<MoveSeen>(4);
            private static readonly List<string> Asked = new List<string>(1);

            internal static void Connect()
            {
                FoundingGame.Estimate = Estimate;
            }

            internal static void Disconnect()
            {
                FoundingGame.Estimate = null;
            }

            /// <summary>
            /// The earliest day of the statements of the buildings the business was moved out of, following its moves back (the messages that tell them name the
            /// business and both buildings); 0 when no move of it is told, or no statement is left of the buildings it left.
            /// </summary>
            private static int Estimate(string place, string name)
            {
                try
                {
                    GameInstance save = SaveGameManager.Current;
                    if (save == null || save.financialSummaries == null)
                    {
                        return 0;
                    }
                    Asked.Clear();
                    Asked.Add(place);
                    MovesGame.Moves(Seen, Asked);
                    int first = 0;
                    string at = place;
                    for (int hop = 0; hop < MoveRules.HopsMost; hop++)
                    {
                        MoveSeen move = LatestTo(at, name);
                        if (move == null)
                        {
                            break;
                        }
                        int day = EarliestStatement(save, move.From);
                        if (day > 0 && day <= move.Day && (first == 0 || day < first))
                        {
                            first = day;
                        }
                        at = move.From;
                    }
                    Seen.Clear();
                    Asked.Clear();
                    return first;
                }
                catch (Exception e)
                {
                    Fault("FoundingLinks.Estimate", e);
                    return 0;
                }
            }

            // The last move the business of that name was told to have made into the building; null if none.
            private static MoveSeen LatestTo(string to, string name)
            {
                MoveSeen found = null;
                for (int i = 0; i < Seen.Count; i++)
                {
                    MoveSeen m = Seen[i];
                    if (m.To == to && string.Equals(TechWriter.Clean(m.Name), name, StringComparison.Ordinal)
                        && (found == null || m.Day > found.Day || (m.Day == found.Day && m.Hour >= found.Hour)))
                    {
                        found = m;
                    }
                }
                return found;
            }

            // The first day the game's statements hold one for the building (0 if none): they are kept for a year only.
            private static int EarliestStatement(GameInstance save, string place)
            {
                if (!PlaceRules.Street(place, out string street) || !PlaceRules.Number(place, out int number))
                {
                    return 0;
                }
                var address = new Address(street, number);
                int first = 0;
                for (int i = 0; i < save.financialSummaries.Count; i++)
                {
                    FinancialSummary day = save.financialSummaries[i];
                    if (day == null || day.businessIncomeStatements == null || (first != 0 && day.dayNumber >= first))
                    {
                        continue;
                    }
                    for (int s = 0; s < day.businessIncomeStatements.Count; s++)
                    {
                        FinancialSummary.BusinessIncomeStatement statement = day.businessIncomeStatements[s];
                        if (statement != null && statement.Address == address)
                        {
                            first = day.dayNumber;
                            break;
                        }
                    }
                }
                return first;
            }
        }
    }
}
