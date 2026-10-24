using System;
using System.IO;
using System.Text.RegularExpressions;
using RealLifeOverhaul.NewBuildings;

// A made building with one of the game's special services (Scripts/NewBuildings, MadeGame.Services.cs): the special type
// only with a service attached, so the game's towing (which reads the service's settings on every special-type building)
// always finds one; a service only on a district's building, never another structure than its own; what a registration is
// to the service (the player's stays ordinary, the service's stays, anything else is filled as the game fills one); and the
// sources: the record's service a copy of the game's own, the registration filled as the game's own, the list of special
// buildings in and out, a branch's contact kept the head office's on the game's own event, a branch no second lender.
internal static partial class Program
{
    private static void SpecialServiceChecks()
    {
        SpecialServiceTypeBan();
        SpecialServiceVerdicts();
        SpecialServiceSources();
    }

    private static MadeSpec ServedSpec()
    {
        MadeSpec s = NewBuildingsTestPlot.Spec();
        s.Life = true;
        s.Service = "CityWorkforceInc";
        s.ServiceName = "Test Agency";
        s.Type = "ba:buildingtype_retail";
        s.PlainType = "ba:buildingtype_retail";
        return s;
    }

    private static void SpecialServiceTypeBan()
    {
        Func<string, int, string, bool> takes = (size, version, type) => type != null;
        Func<string, int, string, bool> ordinaryOnly = (size, version, type) => type != NewBuildingsRules.SpecialType;
        Func<string, bool> known = district => true;

        MadeSpec bare = NewBuildingsTestPlot.Spec();
        bare.Type = NewBuildingsRules.SpecialType;
        bare.Life = true;
        string noService = NewBuildingsRules.Refusal(bare, takes, known, true);
        Check(noService != null && noService.Contains("special service"),
            "special services: the game's special building type without a service of the game's is refused (its towing reads the service's settings on every special-type building)");

        MadeSpec served = ServedSpec();
        served.Type = NewBuildingsRules.SpecialType;
        served.PlainType = "ba:buildingtype_warehouse";
        Check(NewBuildingsRules.Refusal(served, takes, known, true) == null && NewBuildingsRules.ServiceRefusal(served, takes) == null,
            "special services: the special type with a service attached and an ordinary type to stand as is made (the game's own importers stand as special-type H1)");

        served.PlainType = NewBuildingsRules.SpecialType;
        string plainSpecial = NewBuildingsRules.ServiceRefusal(served, takes);
        served.PlainType = "ba:buildingtype_warehouse";
        string plainUnknown = NewBuildingsRules.ServiceRefusal(served, (size, version, type) => type == NewBuildingsRules.SpecialType);
        served.PlainType = null;
        string noPlain = NewBuildingsRules.ServiceRefusal(served, takes);
        Check(plainSpecial != null && plainUnknown != null && noPlain == null,
            "special services: the type a place stands as without its service is never the special one and is a structure the game has; none at all is allowed (it then stands down where a player holds the lot)");

        MadeSpec own = ServedSpec();
        own.Life = false;
        string notDistrict = NewBuildingsRules.Refusal(own, takes, known, true);
        Check(notDistrict != null && notDistrict.Contains("district"),
            "special services: a service stands only in a district's building, whose registration is the save's as the game's special buildings' are");

        MadeSpec goods = ServedSpec();
        goods.ServiceGoods = new string[0];
        Check(NewBuildingsRules.ServiceRefusal(goods, takes) != null && NewBuildingsRules.ServiceRefusal(ServedSpec(), takes) == null,
            "special services: an importer of its own goods with no goods is refused; a retail place with a service is made");

        MadeSpec plain = NewBuildingsTestPlot.Spec();
        Check(NewBuildingsRules.ServiceRefusal(plain, ordinaryOnly) == null && NewBuildingsRules.Refusal(plain, ordinaryOnly, known, true) == null,
            "special services: an ordinary declaration without a service is as before");

        var other = new MadeStructure { Size = "ba:buildingsize_a", Version = 1, Type = "ba:buildingtype_retail" };
        MadeSpec withOthers = ServedSpec();
        MadeSpec withoutService = ServedSpec();
        withoutService.Service = null;
        Check(!NewBuildingsRules.OtherTakes(withOthers, other, takes, true) && NewBuildingsRules.OtherTakes(withoutService, other, takes, true),
            "special services: a place with a service never takes another structure (its layout is the game's for its own size and version alone)");
    }

    private static void SpecialServiceVerdicts()
    {
        const string type = "ba:businesstype_recruitmentagency", name = "Test Agency";
        Check(NewBuildingsRules.ServiceOf(true, type, name, type, name) == ServiceVerdict.Held && NewBuildingsRules.ServiceOf(true, null, null, type, name) == ServiceVerdict.Held,
            "special services: a registration the player rents or owns (in a session, another player's too) stays an ordinary building, whatever it holds");
        Check(NewBuildingsRules.ServiceOf(false, type, name, type, name) == ServiceVerdict.Theirs,
            "special services: a registration of the service's type with the copy's name is the service's already");
        Check(NewBuildingsRules.ServiceOf(false, "ba:businesstype_empty", null, type, name) == ServiceVerdict.Fill
              && NewBuildingsRules.ServiceOf(false, "ba:businesstype_clothingstore", "Some Shop", type, name) == ServiceVerdict.Fill
              && NewBuildingsRules.ServiceOf(false, type, "Another Agency", type, name) == ServiceVerdict.Fill
              && NewBuildingsRules.ServiceOf(false, null, null, type, name) == ServiceVerdict.Fill,
            "special services: an empty unit, an AI business of the game's, a service of another name or a new registration is filled as the game fills a special building's");
        Check(NewBuildingsRules.ServiceOf(false, type, name, null, name) == ServiceVerdict.Fill,
            "special services: a service with no type never counts as the registration's");
    }

    private static void SpecialServiceSources()
    {
        string scripts = System.IO.Path.GetFullPath(System.IO.Path.Combine(LocalesFolder(), "..", "Scripts"));
        string Read(string file)
        {
            string path = System.IO.Path.Combine(scripts, file);
            return File.Exists(path) ? CodeAlone(File.ReadAllText(path)) : string.Empty;
        }
        string made = Read(@"NewBuildings\MadeGame.cs"), served = Read(@"NewBuildings\MadeGame.Services.cs"), director = Read(@"NewBuildings\NewBuildingsDirector.cs");
        string life = Read(@"Uptown\LifeGame.Buildings.cs"), banks = Read(@"Economy\Banking\Loans\BankTerms.cs");
        Check(made.Length > 0 && served.Length > 0 && director.Length > 0 && life.Length > 0 && banks.Length > 0, "special services: the made buildings' Game files, their director, Uptown's buildings and the banks' terms are there");

        string record = MemberText(made, "public static Building MakeRecord(");
        Check(record.Contains("service = Serve(spec, registration, fresh, out why, out bool held);") && record.Contains("b.SpecialService = service;") && record.Contains("b.BuildingType = type;")
              && record.Contains("if (spec.Type == NewBuildingsRules.SpecialType && (!held || string.IsNullOrEmpty(spec.PlainType)))") && record.Contains("return null;"),
            "special services: the record carries the copy of the game's service, or stands as its ordinary type where a player holds it; a special-type place without its service stands down");
        string serve = MemberText(served, "private static SpecialService Serve(");
        Check(serve.Contains("Building head = HeadOf(spec.Service);") && serve.Contains("UnityEngine.Object.Instantiate(head.SpecialService)") && serve.Contains("copy.hideFlags = HideFlags.DontUnloadUnusedAsset;")
              && serve.Contains("copy.businessName = name;") && serve.Contains("Served.Add(copy);") && serve.Contains("UnityEngine.Object.Instantiate(theirs)") && serve.Contains("copy.settings = own;")
              && serve.Contains("NewBuildingsRules.ServiceOf(players,") && serve.Contains("registration.Reset();") && serve.Contains("FillAsGame(registration, copy, spec);")
              && serve.IndexOf("ServiceVerdict.Held", StringComparison.Ordinal) < serve.IndexOf("Instantiate(head.SpecialService)", StringComparison.Ordinal),
            "special services: the service is a copy of the game's own (found on its own building), kept from the engine's sweep, named the place's, an importer's settings its own; nothing is copied where a player holds the lot");
        string fill = MemberText(served, "private static void FillAsGame(");
        Check(fill.Contains("reg.BusinessName = service.businessName;") && fill.Contains("reg.BusinessDescription = service.businessDescription;") && fill.Contains("reg.businessTypeName = service.businessTypeName;")
              && fill.Contains("reg.Layout = service.layout;") && fill.Contains("reg.scheduleDays = service.scheduleDays;") && fill.Contains("reg.signAppearanceSettings = service.signAppearanceSettings;")
              && fill.Contains("reg.logoSettings = service.logoSettings;") && fill.Contains("CompetitionHelper.GetAiBusinessCustomerCapacity(") && fill.Contains("reg.RentedByPlayer = false;")
              && fill.Contains("reg.AvailableForRent = false;") && fill.Contains("sale.RemoveAt(i);"),
            "special services: the registration is filled field by field as BuildingHelper.GetBuildingRegistration fills a special building's, never to let or for sale");
        string head = MemberText(served, "public static Building HeadOf(");
        Check(head.Contains("BuildingHelper.SpecialServiceBuildings") && head.Contains("MadeStreets.IsMine(b.StreetName)") && head.Contains("string.CompareOrdinal(b.StreetName, best.StreetName) < 0"),
            "special services: the game's service is found on the game's own building alone, the same one on every machine should two carry it");

        string enter = MemberText(made, "public static bool Enter("), leave = MemberText(made, "public static void Leave(");
        string listed = MemberText(served, "public static void ServiceListed(");
        Check(enter.Contains("ServiceListed(record, true);") && leave.Contains("ServiceListed(record, false);") && listed.Contains("BuildingHelper.SpecialServiceBuildings[address] = record;")
              && listed.Contains("BuildingHelper.SpecialServiceBuildings.Remove(address);") && listed.Contains("there == record"),
            "special services: a made special place is in the game's list of special buildings while it is in the game's lookups, and out of it with them");
        string leaveLists = MemberText(life, "public static void LeaveLists()");
        Check(leaveLists.Contains("MadeGame.ServiceListed(Entered[i], false);") && leaveLists.Contains("MadeGame.ImporterListed(Entered[i], BuildingHelper.GetBuildingRegistration(Entered[i].Address), false);"),
            "special services: when Uptown stands down its special places leave the game's list of special buildings and its importers");
        string drop = MemberText(made, "private static void Drop()"), drops = MemberText(served, "private static void DropServices()");
        Check(drop.Contains("DropServices();") && drops.Contains("UnityEngine.Object.Destroy(Served[i]);") && drops.Contains("Branches.Clear();"),
            "special services: the copies go with the city, as the records that carry them do");

        string hook = MemberText(director, "private void HookContacts()"), contactEvent = MemberText(director, "private void OnContactAdded()");
        Check(director.Contains("_contactAdded = Guard.Listener(OnContactAdded);") && hook.Contains("ContactsApp.onContactAdded.AddListener(_contactAdded);")
              && MemberText(director, "private void UnhookContacts()").Contains("ContactsApp.onContactAdded.RemoveListener(_contactAdded);")
              && MemberText(director, "private void OnDestroy()").Contains("UnhookContacts();") && contactEvent.Contains("MadeGame.Rebranch();"),
            "special services: a branch's contact is put back to its head office's address on the game's own contact event, behind the guard, taken off when the city goes");
        string rebranch = MemberText(served, "public static int Rebranch()");
        Check(rebranch.Contains("c.id == branch.Name && c.description == branch.Type") && rebranch.Contains("c.streetName = branch.Head.streetName;") && rebranch.Contains("c.streetNumber = branch.Head.streetNumber;"),
            "special services: a contact is the branch's by the game's own key (its name and type) and its branch's address, and takes the head office's");
        Check(MemberText(banks, "public static int Find()").Contains("NewBuildings.MadeStreets.IsMine(pair.Key.streetName) && OfTheCity(settings)")
              && MemberText(banks, "private static bool OfTheCity(BankSettings settings)").Contains("pair.Value.SpecialService.settings == settings"),
            "special services: a branch of a city bank is no second lender in the mod's banks (its loans are the head office's), whatever order the game lists them in");
        Check(!Regex.IsMatch(served, @"GetField\(|GetProperty\(|GetMethod\(|BindingFlags") && !Regex.IsMatch(served, @"\bRandom\b"),
            "special services: nothing of the game's is read by name, and nothing is left to chance");
    }
}
