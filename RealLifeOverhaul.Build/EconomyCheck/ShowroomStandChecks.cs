using System;
using System.Collections.Generic;
using System.IO;
using RealLifeOverhaul;
using RealLifeOverhaul.Economy.Dealership;

// The car paid for is the car that stands (CarStandRules, docs/features/car-suppliers.md): a listing carries its model by name from the market to the game's vehicle,
// the game must have the model before any money moves, the vehicle it stands is read back against the model paid for, and a car that cannot be stood as it was paid
// for is paid back in full and said why: a purchase at once, a car on its way and a car found stood as another model when the showroom is walked into.
internal static partial class Program
{
    private const string StandFanton = "ba:vehicletype_petrollsfanton", StandMimic = "ba:vehicletype_honzamimic";

    private static void ShowroomStand()
    {
        ShowroomStandRules();
        ShowroomStandChain();
        ShowroomStandSources();
        ShowroomStandWords();
    }

    private static void ShowroomStandRules()
    {
        Check(CarStandRules.Verdict(StandFanton, StandFanton) == CarMaking.Made, "showroom stand: the model the game stood is the model paid for");
        Check(CarStandRules.Verdict(StandFanton, StandMimic) == CarMaking.WrongModel && CarStandRules.Verdict(StandMimic, StandFanton) == CarMaking.WrongModel,
            "showroom stand: a Honza Mimic where a Petrolls Fanton was paid for is not the car paid for, and neither is the other way round");
        Check(CarStandRules.Verdict(StandFanton, null) == CarMaking.Made && CarStandRules.Verdict(StandFanton, string.Empty) == CarMaking.Made,
            "showroom stand: a vehicle that does not name its model passes (the game's object may not say), so only a named other model fails");
        Check(CarStandRules.Verdict(null, StandFanton) == CarMaking.UnknownModel && CarStandRules.Verdict(string.Empty, null) == CarMaking.UnknownModel,
            "showroom stand: there is no car to pay for without a model");
        Check(CarStandRules.Verdict(StandFanton, StandFanton.ToUpperInvariant()) == CarMaking.WrongModel, "showroom stand: a model is told by its name exactly");

        var faults = new[] { CarMaking.UnknownModel, CarMaking.WrongModel, CarMaking.Failed };
        var lots = new[] { CarMaking.Made, CarMaking.NoPlace };
        bool modelFault = true, lotFault = true;
        var keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (CarMaking m in faults)
        {
            modelFault &= CarStandRules.ModelFault(m) && !string.IsNullOrEmpty(CarStandRules.NoticeKey(m));
            keys.Add(CarStandRules.NoticeKey(m));
        }
        foreach (CarMaking m in lots)
        {
            lotFault &= !CarStandRules.ModelFault(m) && CarStandRules.NoticeKey(m) == null;
        }
        Check(modelFault && keys.Count == faults.Length, "showroom stand: a model the game has not, another model stood and a make that failed are each the model's fault, each with a notice of its own");
        Check(lotFault, "showroom stand: a car that stands, and a lot with no place for it, are not the model's fault: the first is kept, the second waits as before");

        bool whole = CarStandRules.Refund(-5) == 0 && CarStandRules.Refund(0) == 0 && CarStandRules.Refund(int.MaxValue) == int.MaxValue;
        foreach (int cost in new[] { 1, 2275, 650000, 9999999, int.MaxValue / 2 })
        {
            foreach (CarMaking m in faults)
            {
                long cash = 1000000000L;
                cash -= cost;   // paid for the car
                cash += CarStandRules.ModelFault(m) ? CarStandRules.Refund(cost) : 0;   // the model's fault gives it all back
                whole &= cash == 1000000000L && CarStandRules.Refund(cost) == cost;
            }
        }
        Check(whole, "showroom stand: when the model cannot be stood the player has exactly the cash they had before the purchase, for any price, and a refund is never less than nothing");
        Check(DealerLines.Of(DealerLines.BoughtType) == DealerLine.Stock,
            "showroom stand: the price given back is booked under the line the purchase was, so the showroom's stock row nets to nothing");
    }

    // The chain from the market to the vehicle changes nothing about the model: found by its number, carried by its name, saved and read back as it was.
    private static void ShowroomStandChain()
    {
        var s = new DealerState { Seed = 7, NextId = 50, MarketDay = 3 };
        s.Market.Add(new DealerListing { Id = 5, Kind = ListingKind.Wholesale, Type = "ba:vehicletype_a", Asking = 100 });
        s.Market.Add(new DealerListing { Id = 9, Kind = ListingKind.Wholesale, Type = StandFanton, Colour = "Red", Condition = 1000, Shown = 1000, Asking = 650000 });
        s.Market.Add(new DealerListing { Id = 12, Kind = ListingKind.Used, Type = "ba:vehicletype_c", Asking = 300 });
        s.Market.RemoveAt(0);   // a refresh between choosing and paying took the first listing away
        Check(s.Listing(9) != null && s.Listing(9).Type == StandFanton && s.Listing(12).Type == "ba:vehicletype_c" && s.Listing(5) == null,
            "showroom stand: a listing is found by its number, so a refresh between choosing and paying can never turn one car into another");
        var y = new DealerYard { Place = "Elm~4", Name = "Elm" };
        y.Spots.Add(new LotSpot { X = 1, Y = 2, Z = 3, Yaw = 0 });
        y.Cars.Add(new StockCar { Id = 1, VehicleId = "ab+cd=", Type = StandFanton, Colour = "Red", Km = 10, Condition = 900, Cost = 650000, Asking = 700000, Bought = 2, Spot = 0 });
        s.Yards.Add(y);
        s.Deliveries.Add(new DealerDelivery { Id = 21, Place = "Elm~4", Type = StandMimic, Colour = "Blue", Cost = 2275, Due = 100 });
        DealerState back = DealerStore.Read(DealerStore.Write(s), out int _);
        Check(back != null && back.Listing(9) != null && back.Listing(9).Type == StandFanton && back.Yards.Count == 1 && back.Yards[0].Cars.Count == 1 && back.Yards[0].Cars[0].Type == StandFanton
              && back.Deliveries.Count == 1 && back.Deliveries[0].Type == StandMimic && back.Deliveries[0].Cost == 2275,
            "showroom stand: a listing's model, a car's and a delivery's are saved by name and read back as they were");
        string orders = EconomySource(@"Dealer\Supply\DealerDirector.Supply.cs"), business = EconomySource(@"Dealer\DealerDirector.Business.cs"), actions = EconomySource(@"Dealer\DealerDirector.Actions.cs");
        Check(MemberText(orders, "private bool BuyShipped(").Contains("Type = l.Type,") && MemberText(business, "private DealerDelivery Ship(").Contains("Type = l.Type,")
              && MemberText(actions, "public bool Buy(").Contains("Arrive(y, l.Type, "),
            "showroom stand: a car bought, ordered or won carries the listing's own model to the lot");
    }

    private static void ShowroomStandSources()
    {
        string game = EconomySource(@"Dealer\DealerGame.cs"), actions = EconomySource(@"Dealer\DealerDirector.Actions.cs"), business = EconomySource(@"Dealer\DealerDirector.Business.cs");
        string stand = EconomySource(@"Dealer\DealerDirector.Stand.cs"), director = EconomySource(@"Dealer\DealerDirector.cs"), rules = EconomySource(@"Dealer\CarStandRules.cs");
        string make = MemberText(game, "public static string MakeCar("), spawn = MemberText(game, "private static CarMaking Spawn("), park = MemberText(game, "public static int Park(");

        int spawns = 0;
        string folder = System.IO.Path.Combine(LocalesFolder(), @"..\Scripts\Economy\Dealer");
        foreach (string file in Directory.Exists(folder) ? Directory.GetFiles(folder, "*.cs", SearchOption.AllDirectories) : new string[0])
        {
            spawns += Occurs(File.ReadAllText(file), "CreateAndSpawnVehicle(");
        }
        Check(spawns == 1 && Occurs(spawn, "VehicleHelper.CreateAndSpawnVehicle(") == 1 && !make.Contains("CreateAndSpawnVehicle(") && make.Contains("why = Spawn(v, s);"),
            "showroom stand: the showroom makes a vehicle in one place only, and it is the one that reads the model back");
        Check(make.Contains("out CarMaking why") && make.Contains("Knows(type)") && make.Contains("why = CarMaking.UnknownModel;") && make.Contains("why = CarMaking.NoPlace;")
              && make.Contains("return why == CarMaking.Made ? v.id : null;"),
            "showroom stand: a car is made only of a model the game has, and the car's number is given only for a car that stands");
        Check(spawn.Contains("CarStandRules.Verdict(v.vehicleTypeName, stood)") && spawn.Contains("string stood = ModelOf(made);") && Occurs(spawn, "Unmake(v, made);") == 2
              && spawn.Contains("catch (Exception e)") && spawn.Contains("return CarMaking.Failed;"),
            "showroom stand: the vehicle the game stood is read back against the model paid for, and one that is another model, or that the game throws on, is taken out again whole");
        string unmake = MemberText(game, "private static void Unmake(");
        Check(unmake.Contains("v.Delete(controller);") && unmake.Contains("Save.VehicleInstances.Remove(v);") && unmake.Contains("Controller(v.id)") && unmake.Contains("catch (Exception e)"),
            "showroom stand: a car taken out again leaves neither its object nor its entry in the save, finds a half made object by its vehicle, and never throws");
        string modelOf = MemberText(game, "private static string ModelOf(");
        Check(modelOf.Contains("car.vehicleType.vehicleTypeName") && game.Contains("public static bool Knows(string type)") && game.Contains("VehicleTypeHelper.GetVehicleType(type) != null"),
            "showroom stand: a model is the one its object names and the game knows");

        string buy = MemberText(actions, "public bool Buy(");
        int knows = buy.IndexOf("DealerGame.Knows(l.Type)", StringComparison.Ordinal), take = buy.IndexOf("DealerGame.Take(price", StringComparison.Ordinal);
        int arrive = buy.IndexOf("Arrive(y, l.Type", StringComparison.Ordinal), refund = buy.IndexOf("DealerGame.Give(CarStandRules.Refund(price)", StringComparison.Ordinal);
        int said = buy.IndexOf("return StandNot(l.Type, price, making);", StringComparison.Ordinal);
        Check(knows > 0 && take > knows && arrive > take && refund > arrive && said > refund && buy.Contains("return Unavailable(l.Type);") && Occurs(buy, "DealerLines.BoughtType") == 2,
            "showroom stand: a model the game has not is refused before any money moves; a car that cannot be made is paid back whole, under the line it was paid, and the notice says why");
        string arrives = MemberText(actions, "private StockCar Arrive(");
        Check(arrives.Contains("out CarMaking why") && arrives.Contains("why = CarMaking.NoPlace;") && arrives.Contains("DealerGame.MakeCar(reg, y, spot, type, colour, condition, out why)"),
            "showroom stand: every car that is made, at once, on its way or in part exchange, says why when it is not");

        string arrivals = MemberText(business, "private void Arrivals(");
        int unknown = arrivals.IndexOf("_models.Count > 0 && !DealerGame.Knows(q.Type)", StringComparison.Ordinal), made = arrivals.IndexOf("out making, true)", StringComparison.Ordinal);
        int fault = arrivals.IndexOf("CarStandRules.ModelFault(making)", StringComparison.Ordinal), unplaced = arrivals.IndexOf("if (!AnyShowroom())", StringComparison.Ordinal);
        Check(unknown > 0 && made > unknown && fault > made && unplaced > fault && arrivals.Contains("Unstood(q, CarMaking.UnknownModel);") && arrivals.Contains("Unstood(q, making);"),
            "showroom stand: a car on its way whose model the game has not, or cannot stand as paid for, is paid back before it is made to wait or sold on, and a lot with no place still waits");
        string unstood = MemberText(stand, "private void Unstood(");
        Check(unstood.Contains("CarStandRules.Refund(q.Cost)") && unstood.Contains("DealerGame.Give(back, DealerLines.BoughtType") && unstood.Contains("Book(home, 0, -back, 0, 0);")
              && unstood.Contains("_state.Deliveries.Remove(q);") && unstood.Contains("_supply.Arrived(q.Id);") && unstood.Contains("StandTell(q.Type, back, making, TabStock);"),
            "showroom stand: a car on its way that cannot be stood has all it cost given back and taken off the showroom's day, its delivery and its order ended, and the player told");

        string dropWrong = MemberText(stand, "private void DropWrong(");
        Check(dropWrong.Contains("CarStandRules.Refund(car.Cost)") && dropWrong.Contains("DealerGame.RemoveCar(car.VehicleId);") && dropWrong.Contains("y.Cars.Remove(car);")
              && dropWrong.Contains("RemoveDeals(y, car.Id);") && dropWrong.Contains("_bays.Forget(car.VehicleId);") && dropWrong.Contains("DealerGame.Give(back, DealerLines.BoughtType")
              && dropWrong.Contains("CarMaking.WrongModel") && dropWrong.Contains("RebuildIds();"),
            "showroom stand: a car found stood as another model has its vehicle taken out and its offers and bay let go, and all it cost given back, said why");
        Check(park.Contains("List<StockCar> wrong") && park.Contains("wrong?.Clear();") && park.Contains("List<StockCar> list = own == null ? lost : wrong;") && park.Contains("!Stands(car, own)")
              && MemberText(game, "private static bool Stands(").Contains("own.vehicleTypeName") && MemberText(game, "private static bool Stands(").Contains("ModelOf(Controller(car.VehicleId))"),
            "showroom stand: walking in reads each car's vehicle, the save's entry and the object the game stood, against the model paid for, and gives a wrong one no bay");
        string seeTo = MemberText(director, "private void SeeTo("), relay = MemberText(actions, "public bool Relay(");
        Check(seeTo.Contains("DealerGame.Park(y, _lost, _wrong, _bays);") && seeTo.IndexOf("DropLost(y);", StringComparison.Ordinal) < seeTo.IndexOf("DropWrong(y);", StringComparison.Ordinal)
              && relay.Contains("DealerGame.Park(y, _lost, _wrong, _bays);") && relay.IndexOf("DropLost(y);", StringComparison.Ordinal) < relay.IndexOf("DropWrong(y);", StringComparison.Ordinal),
            "showroom stand: walking in and laying a lot out again both pay back a car that is not the model paid for");

        Check(!stand.Contains("Update(") && !stand.Contains("FindObjectsByType") && !stand.Contains("Send(") && !stand.Contains("Network") && !stand.Contains("GameManager.ChangeMoney") && !stand.Contains("BizBooks.")
              && !rules.Contains("UnityEngine") && !rules.Contains("System.Linq"),
            "showroom stand: nothing per frame, nothing sent, no money moved but through the showroom's own lines, and the rules know no game type");
        Check(!stand.Contains("TODO") && !rules.Contains("TODO") && !stand.Contains("NotImplemented"), "showroom stand: no stub");
    }

    private static void ShowroomStandWords()
    {
        Dictionary<string, string>[] langs = LanguageWords(out string[] codes);
        if (langs.Length == 0 || langs[0] == null || codes[0] != "en")
        {
            Check(false, "showroom stand: the English texts are read");
            return;
        }
        Dictionary<string, string> en = langs[0];   // the other languages are filled in by the translation step, from these
        string rules = EconomySource(@"Dealer\CarStandRules.cs");
        bool there = true, names = true, plain = true, used = true;
        foreach (string key in new[] { CarStandRules.RefuseKey, CarStandRules.UnknownKey, CarStandRules.WrongKey, CarStandRules.FailedKey })
        {
            bool has = en.TryGetValue(key, out string text) && !string.IsNullOrWhiteSpace(text);
            there &= has;
            names &= has && text.Contains("{car}") && (key == CarStandRules.RefuseKey || text.Contains("{price}"));
            plain &= has && !text.Contains("!") && !text.Contains("—") && !text.Contains("–") && !text.Contains("-") && text.Length <= 240;
            used &= rules.Contains("\"" + key + "\"");
        }
        Check(there && used, "showroom stand: the four texts are in English, and each is named by the rules that use it");
        Check(names, "showroom stand: each text names the car, and each notice of a refund says the price");
        Check(plain, "showroom stand: the texts have no dash or exclamation mark and keep to 240 characters");
        Check(WhatsNewHas(en, "Fixed: A car bought for a Car Showroom is now checked against the model you paid for"), "showroom stand: What's new has the line in English");
    }
}
