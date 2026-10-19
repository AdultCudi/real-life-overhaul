using System;
using System.Collections.Generic;
using BAModAPI;
using Entities;
using Helpers;
using Streets;

namespace RealLifeOverhaul.Tech
{
    /// <summary>
    /// What following a move reads from the game: the player's businesses as they stand, the moves the moving companies say
    /// they made, and the moves booked. Read only, never per frame: the businesses on the parts' own reads, the messages only
    /// when data has lost its business. The options page's test carries out a booked move as the game's own hour would.
    /// </summary>
    internal static class MovesGame
    {
        private const string Empty = "ba:businesstype_empty";

        public static IModLogger Logger;

        private static GameInstance Save => SaveGameManager.Current;

        private static readonly Dictionary<string, string> PlaceOfAddress = new Dictionary<string, string>(32, StringComparer.Ordinal);

        /// <summary>The player's businesses: place, firm kind (-1 for none) and the business's own name. One pass over the buildings.</summary>
        public static void Businesses(List<BusinessNow> into)
        {
            into.Clear();
            List<BuildingRegistration> all = Save != null ? Save.BuildingRegistrations : null;
            for (int i = 0; all != null && i < all.Count; i++)
            {
                BuildingRegistration reg = all[i];
                if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress && reg.HasEstablishedBusiness && !string.IsNullOrEmpty(reg.businessTypeName)
                    && reg.businessTypeName != Empty)
                {
                    into.Add(new BusinessNow { Place = TalentGame.Place(reg), Kind = FirmsGame.KindOf(reg.businessTypeName), Name = reg.BusinessName ?? string.Empty });
                }
            }
        }

        /// <summary>
        /// The moves the moving companies' messages tell of in which the business went too: MovingServiceHelper.SendDoneMessage
        /// names the old building by its bare address once BizManTransfer has shut its business there, and the new one as the
        /// business's composed name. Addresses are matched as the game writes them, against the player's buildings and the
        /// places asked about (an old building may since have been let go).
        /// </summary>
        public static void Moves(List<MoveSeen> into, List<string> asked)
        {
            into.Clear();
            PlaceOfAddress.Clear();
            GameInstance save = Save;
            if (save == null)
            {
                return;
            }
            try
            {
                List<BuildingRegistration> all = save.BuildingRegistrations;
                for (int i = 0; all != null && i < all.Count; i++)
                {
                    BuildingRegistration reg = all[i];
                    if (reg != null && GoingPublic.Mine(reg) && reg.HasValidAddress)
                    {
                        Put(reg.Address.ToFormattedString(), TalentGame.Place(reg));
                    }
                }
                for (int i = 0; asked != null && i < asked.Count; i++)
                {
                    if (PlaceRules.Street(asked[i], out string street) && PlaceRules.Number(asked[i], out int number))
                    {
                        Put(new Address(street, number).ToFormattedString(), asked[i]);
                    }
                }
                Messages(save, into);
            }
            catch (Exception e)
            {
                into.Clear();
                Logger?.Error(e);
            }
            PlaceOfAddress.Clear();
        }

        private static void Put(string address, string place)
        {
            if (!string.IsNullOrEmpty(address) && !string.IsNullOrEmpty(place) && !PlaceOfAddress.ContainsKey(address))
            {
                PlaceOfAddress.Add(address, place);
            }
        }

        // Kept in a method of its own: the contacts' messages are the game's.
        private static void Messages(GameInstance save, List<MoveSeen> into)
        {
            List<Contact> contacts = save.Contacts;
            for (int c = 0; contacts != null && c < contacts.Count; c++)
            {
                Queue<TextMessage> messages = contacts[c] != null ? contacts[c].messagesQueue : null;
                if (messages == null)
                {
                    continue;
                }
                foreach (TextMessage m in messages)
                {
                    if (m == null || !MoveRules.IsDone(m.messageKey) || m.messageData == null
                        || !m.messageData.TryGetValue(MoveRules.OriginField, out string origin) || origin == null || !PlaceOfAddress.TryGetValue(origin, out string from)
                        || !m.messageData.TryGetValue(MoveRules.DestinationField, out string destination)
                        || !MoveRules.Composed(destination, out string name, out string address) || !PlaceOfAddress.TryGetValue(address, out string to))
                    {
                        continue;
                    }
                    into.Add(new MoveSeen { From = from, To = to, Name = name, Day = m.timestamp != null ? m.timestamp.Day : 0, Hour = m.timestamp != null ? m.timestamp.Hour : 0 });
                }
            }
        }

        /// <summary>Moves booked with the moving companies and not yet made; -1 with no save.</summary>
        public static int Booked()
        {
            List<MovingServiceContract> booked = Save != null ? Save.movingServiceContracts : null;
            return booked != null ? booked.Count : -1;
        }

        /// <summary>For the options page's test: the first move booked from a building of the player's is carried out now, by the game's own contract. False with none.</summary>
        public static bool MoveNow()
        {
            List<MovingServiceContract> booked = Save != null ? Save.movingServiceContracts : null;
            for (int i = 0; booked != null && i < booked.Count; i++)
            {
                MovingServiceContract c = booked[i];
                if (c != null && c.originMovingAddress != null && GoingPublic.Mine(BuildingHelper.GetBuildingRegistration(c.originMovingAddress)))
                {
                    c.DoMove();
                    return true;
                }
            }
            return false;
        }
    }
}
