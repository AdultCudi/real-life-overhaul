using RealLifeOverhaul.Careers;

namespace RealLifeOverhaul.Ui
{
    /// <summary>
    /// The glyph each profession, subject and kind of thing has on the work apps, so no two kinds of thing share one picture where the kit has a
    /// glyph for each. A glyph names a kind and nothing else (RLO-UI-017). Game-free.
    /// </summary>
    public static class WorkGlyphs
    {
        public static UiIcon Profession(int id)
        {
            switch (id)
            {
                case ProCatalogue.Developer: return UiIcon.Code;
                case ProCatalogue.DataPro: return UiIcon.Database;
                case ProCatalogue.SecurityPro: return UiIcon.ShieldCheck;
                case ProCatalogue.Designer: return UiIcon.PaintBrush;
                case ProCatalogue.Accountant: return UiIcon.ChartLineUp;
                case ProCatalogue.Lawyer: return UiIcon.Scales;
                case ProCatalogue.Architect: return UiIcon.Layout;
                case ProCatalogue.CivilEngineer: return UiIcon.Hammer;
                case ProCatalogue.Nurse: return UiIcon.Heartbeat;
                case ProCatalogue.Teacher: return UiIcon.Books;
                case ProCatalogue.Marketer: return UiIcon.Megaphone;
                case ProCatalogue.Banker: return UiIcon.Bank;
                case ProCatalogue.Manager: return UiIcon.Presentation;
                case ProCatalogue.CouncilOfficer: return UiIcon.Building;
                case ProCatalogue.Electrician: return UiIcon.Lightning;
                case ProCatalogue.Plumber: return UiIcon.Drop;
                case ProCatalogue.Carpenter: return UiIcon.Wrench;
                case ProCatalogue.Chef: return UiIcon.ForkKnife;
                case ProCatalogue.Stylist: return UiIcon.Star;
                case ProCatalogue.ItSupport: return UiIcon.Headset;
                case ProCatalogue.Sales: return UiIcon.Handshake;
                case ProCatalogue.HumanResources: return UiIcon.IdBadge;
                case ProCatalogue.BarStaff: return UiIcon.Coffee;
                case ProCatalogue.SecurityGuard: return UiIcon.Lock;
                case ProCatalogue.Warehouse: return UiIcon.Package;
                default: return UiIcon.Briefcase;
            }
        }

        /// <summary>The subject a course is of, by its place in the filter chips: 0 business, 1 tech, 2 trades, 3 food and drink, 4 care and service.</summary>
        public static int Subject(CourseDef c)
        {
            switch (c.Id)
            {
                case StudyCatalogue.Business: case StudyCatalogue.Accounting: case StudyCatalogue.Finance: case StudyCatalogue.Law: case StudyCatalogue.Marketing:
                case StudyCatalogue.Bookkeeping: case StudyCatalogue.CustomerService: case StudyCatalogue.DigitalMarketing: case StudyCatalogue.Mba: case StudyCatalogue.LegalPractice:
                case StudyCatalogue.ProjectManagement: case StudyCatalogue.CharteredAccountant: case StudyCatalogue.BarExam: case StudyCatalogue.Design:
                    return 0;
                case StudyCatalogue.CodingBootcamp: case StudyCatalogue.ComputerScience: case StudyCatalogue.SoftwareEngineering: case StudyCatalogue.DataScience:
                case StudyCatalogue.Cybersecurity: case StudyCatalogue.MscComputing: case StudyCatalogue.ItSupport: case StudyCatalogue.CloudAssociate: case StudyCatalogue.CloudArchitect:
                case StudyCatalogue.SecurityPractitioner: case StudyCatalogue.DataAnalyst: case StudyCatalogue.NetworkProfessional: case StudyCatalogue.MachineLearning:
                case StudyCatalogue.PrivacyProfessional: case StudyCatalogue.TestingProfessional:
                    return 1;
                case StudyCatalogue.Electrician: case StudyCatalogue.Plumber: case StudyCatalogue.Carpenter: case StudyCatalogue.Bricklaying: case StudyCatalogue.CraneTicket:
                case StudyCatalogue.Forklift: case StudyCatalogue.Architecture: case StudyCatalogue.CivilEngineering: case StudyCatalogue.ConstructionManagement:
                case StudyCatalogue.ArchitectLicence: case StudyCatalogue.CharteredEngineer: case StudyCatalogue.SecurityLicence: case StudyCatalogue.DrivingRefresher:
                    return 2;
                case StudyCatalogue.Barista: case StudyCatalogue.Bartending: case StudyCatalogue.Chef:
                    return 3;
                default:
                    return 4;
            }
        }

        public static UiIcon Course(CourseDef c)
        {
            switch (c.Id)
            {
                case StudyCatalogue.Business: case StudyCatalogue.Mba: case StudyCatalogue.ProjectManagement: return UiIcon.Presentation;
                case StudyCatalogue.Accounting: case StudyCatalogue.Finance: case StudyCatalogue.Bookkeeping: case StudyCatalogue.CharteredAccountant: return UiIcon.ChartLineUp;
                case StudyCatalogue.Law: case StudyCatalogue.LegalPractice: case StudyCatalogue.BarExam: return UiIcon.Scales;
                case StudyCatalogue.Marketing: case StudyCatalogue.DigitalMarketing: return UiIcon.Megaphone;
                case StudyCatalogue.CustomerService: return UiIcon.Headset;
                case StudyCatalogue.Design: return UiIcon.PaintBrush;
                case StudyCatalogue.CodingBootcamp: case StudyCatalogue.ComputerScience: case StudyCatalogue.SoftwareEngineering: case StudyCatalogue.MscComputing: return UiIcon.Code;
                case StudyCatalogue.DataScience: case StudyCatalogue.DataAnalyst: case StudyCatalogue.MachineLearning: return UiIcon.Database;
                case StudyCatalogue.Cybersecurity: case StudyCatalogue.SecurityPractitioner: case StudyCatalogue.PrivacyProfessional: case StudyCatalogue.SecurityLicence: return UiIcon.ShieldCheck;
                case StudyCatalogue.ItSupport: case StudyCatalogue.NetworkProfessional: case StudyCatalogue.CloudAssociate: case StudyCatalogue.CloudArchitect: return UiIcon.Cpu;
                case StudyCatalogue.TestingProfessional: return UiIcon.Bug;
                case StudyCatalogue.Electrician: return UiIcon.Lightning;
                case StudyCatalogue.Plumber: return UiIcon.Drop;
                case StudyCatalogue.Carpenter: case StudyCatalogue.Bricklaying: case StudyCatalogue.CraneTicket: case StudyCatalogue.ConstructionManagement: return UiIcon.Hammer;
                case StudyCatalogue.Forklift: return UiIcon.Package;
                case StudyCatalogue.DrivingRefresher: return UiIcon.Car;
                case StudyCatalogue.Architecture: case StudyCatalogue.ArchitectLicence: return UiIcon.Layout;
                case StudyCatalogue.CivilEngineering: case StudyCatalogue.CharteredEngineer: return UiIcon.Wrench;
                case StudyCatalogue.Barista: return UiIcon.Coffee;
                case StudyCatalogue.Bartending: case StudyCatalogue.Chef: return UiIcon.ForkKnife;
                case StudyCatalogue.Nursing: case StudyCatalogue.FirstAid: case StudyCatalogue.CareWorker: return UiIcon.Heartbeat;
                case StudyCatalogue.Teaching: case StudyCatalogue.Pgce: return UiIcon.Books;
                case StudyCatalogue.Hairdresser: case StudyCatalogue.Beautician: return UiIcon.Star;
                default: return UiIcon.Cap;
            }
        }

        /// <summary>A thing for sale by its catalogue id: a sofa a sofa, a phone a phone.</summary>
        public static UiIcon Thing(string id, bool car)
        {
            if (car)
            {
                return UiIcon.Car;
            }
            switch (id)
            {
                case "phone": return UiIcon.Phone;
                case "laptop": return UiIcon.Monitor;
                case "console": return UiIcon.GameController;
                case "television": return UiIcon.Monitor;
                case "speaker": return UiIcon.SpeakerHigh;
                case "camera": return UiIcon.Camera;
                case "sofa": case "armchair": return UiIcon.Sofa;
                case "table": return UiIcon.ForkKnife;
                case "bookcase": return UiIcon.Books;
                case "jacket": case "coat": case "suit": return UiIcon.ShoppingBag;
                case "trainers": case "boots": case "heels": return UiIcon.Tag;
                case "espresso_machine": return UiIcon.Coffee;
                case "commercial_grill": return UiIcon.Fire;
                case "display_fridge": case "chest_freezer": return UiIcon.Thermometer;
                case "dough_mixer": return UiIcon.Cpu;
                default: return UiIcon.Package;
            }
        }
    }
}
