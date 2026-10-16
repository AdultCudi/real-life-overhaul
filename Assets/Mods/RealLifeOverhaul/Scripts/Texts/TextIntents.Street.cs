namespace RealLifeOverhaul.Texts
{
    /// <summary>
    /// The intents of data v4 (data/gen/v4/defs_v4.py BRANCHES, SPEC_v4.txt): street talk, a stranger the player stopped in
    /// the street saying one spoken line (Scripts/Street; the card is asked by Wiring/StreetTexts). One intent per thing the
    /// game decides, a branch per outcome: v2's friend face with a stranger's role, the outcome told by the news and the
    /// facts. Registered with data version 4: a pack of an older data version is never asked them (TextsHub.Data), so until
    /// a v4 pack is trained street talk words what fits with data v3's intents and the rest with its written lines. Written
    /// from the generator's own table (one row per branch) and proved byte-identical to its cards (EconomyCheck,
    /// TextsChecks.cs, fixtures_cs.py --v4). Game-free. COST: none until asked.
    /// </summary>
    public static partial class TextIntents
    {
        public const int DataV4 = 4;   // ...and of street talk's (data/gen/v4: v3's rows unchanged and street talk's 70 cards)

        public const string StreetWrite = "spoken: street";

        private static void V4()
        {
            S4("street.greet", "stranger", "{player} stops you in the street to talk; you have never met", None, null);
            S4("street.greet", "again", "{player} stops you again; you talked earlier today", None, null);
            S4("street.greet", "famous", "you recognise {player}, a well-known business owner the city likes", None, L("{player}"));
            S4("street.greet", "infamous", "you recognise {player}, a business owner with a bad name", None, L("{player}"));
            S4("street.brush", "hurry", "{player} stops you in the street, but you have no time: you are in a hurry", None, null);
            S4("street.brush", "work", "{player} stops you in the street, but you have no time: you are on your way to work", None, null);
            S4("street.brush", "night", "{player} stops you in the street, but you have no time: it is late at night and they are a stranger", None, null);
            S4("street.brush", "rain", "{player} stops you in the street, but you have no time: it is raining hard", None, null);
            S4("street.brush", "mood", "{player} stops you in the street, but you have no time: you are having a bad day", None, null);
            S4("street.brush", "rude", "{player} stops you in the street, but you have no time: they were rude to you earlier today", None, null);
            S4("street.brush", "unwashed", "{player} stops you in the street, but you have no time: they smell as if they have not washed", None, null);
            S4("street.brush", "record", "{player} stops you in the street, but you have no time: you know they have a criminal record", None, null);
            S4("street.brush", "wanted", "{player} stops you in the street, but you have no time: you know the police are looking for them", None, null);
            S4("street.brush", "phone", "{player} stops you in the street, but you have no time: you are on the phone", None, null);
            S4("street.smalltalk", "well", "{player} makes small talk with you in the street", L("it lands: well"), null);
            S4("street.smalltalk", "badly", "{player} makes small talk with you in the street", L("it lands: badly"), null);
            S4("street.about", "share", "{player} asks about you", L("job: {job}", "lives in: {district}"), L("{district}"));
            S4("street.about", "retired", "{player} asks about you", L("job: retired", "lives in: {district}"), L("{district}"));
            S4("street.about", "refuse", "{player} asks about you; you would rather not say", None, null);
            S4("street.name", "give", "{player} tells you their name and asks yours", L("your first name: {first}"), L("{first}"));
            S4("street.name", "withhold", "{player} tells you their name and asks yours; you would rather not give it", None, null);
            S4("street.name", "known", "{player} asks your name; you told them earlier today", L("your first name: {first}"), L("{first}"));
            S4("street.area", "places", "{player} asks what is good around here", L("nearby: {place}; {place_two}"), L("{place}", "{place_two}"));
            S4("street.area", "event", "{player} asks what is good around here", L("today: {event}"), L("{event}"));
            S4("street.area", "news", "{player} asks what is good around here", L("trouble today in: {district}"), L("{district}"));
            S4("street.area", "nothing", "{player} asks what is good around here; there is nothing much nearby", None, null);
            S4("street.directions", "found", "{player} asks you the way somewhere; you know a place", L("place: {place}", "street: {street}", "way: {way}", "walk: {walk}"), L("{place}", "{street}"));
            S4("street.directions", "far", "{player} asks you the way somewhere; you know a place, but it is a long walk", L("place: {place}", "street: {street}", "way: {way}", "walk: {walk}"), L("{place}", "{street}"));
            S4("street.directions", "none", "{player} asks you the way somewhere; there is none nearby", L("looking for: {what}"), L("{what}"));
            S4("street.compliment", "well", "{player} pays you a compliment", L("it lands: well"), null);
            S4("street.compliment", "badly", "{player} pays you a compliment", L("it lands: badly"), null);
            S4("street.joke", "well", "{player} tells you a joke", L("it lands: well"), null);
            S4("street.joke", "badly", "{player} tells you a joke", L("it lands: badly"), null);
            S4("street.flirt", "yes", "{player} flirts with you, and you like it", None, null);
            S4("street.flirt", "no", "{player} flirts with you; you are not interested", None, null);
            S4("street.flirt", "taken", "{player} flirts with you; you are seeing somebody", None, null);
            S4("street.number", "yes", "{player} asks for your number; you give it", L("your first name: {first}"), L("{first}"));
            S4("street.number", "no", "{player} asks for your number; you say no", None, null);
            S4("street.number", "taken", "{player} asks for your number; you are seeing somebody", None, null);
            S4("street.number", "already", "{player} asks for your number; they have it already", None, null);
            S4("street.number", "later", "{player} asks for your number; not yet, maybe if you meet again", None, null);
            S4("street.invite", "yes", "{player} asks you out; you say yes and the plan is booked", L("outing: {outing}", "place: {place}", "time: {time}"), L("{place}", "{time}"));
            S4("street.invite", "maybe_barely", "{player} asks you out; you are not sure yet", L("outing: {outing}", "reason: you barely know them"), null);
            S4("street.invite", "maybe_busy", "{player} asks you out; you are not sure yet", L("outing: {outing}", "reason: you are busy at the moment"), null);
            S4("street.invite", "maybe_night", "{player} asks you out; you are not sure yet", L("outing: {outing}", "reason: it is late"), null);
            S4("street.invite", "maybe_soon", "{player} asks you out; you are not sure yet", L("outing: {outing}", "reason: it is too soon for that"), null);
            S4("street.invite", "maybe_nowhere", "{player} asks you out; you are not sure yet", L("outing: {outing}", "reason: there is nowhere near you would go"), null);
            S4("street.invite", "maybe_plans", "{player} asks you out; you are not sure yet", L("outing: {outing}", "reason: you already have plans with them"), null);
            S4("street.invite", "no_taken", "{player} asks you out; you say no", L("outing: {outing}", "reason: you are seeing someone"), null);
            S4("street.invite", "no_dislike", "{player} asks you out; you say no", L("outing: {outing}", "reason: you do not think well of them"), null);
            S4("street.invite", "no", "{player} asks you out; you say no", L("outing: {outing}", "reason: you are not interested"), null);
            S4("street.election", "for", "{player} asks about the city election", L("you will vote for: {candidate}"), L("{candidate}"));
            S4("street.election", "unsure", "{player} asks about the city election; you have not decided", None, null);
            S4("street.election", "none", "{player} asks about the election; there is none on", None, null);
            S4("street.economy", "up", "{player} asks about prices", L("prices: rising"), null);
            S4("street.economy", "down", "{player} asks about prices", L("prices: falling a little"), null);
            S4("street.economy", "flat", "{player} asks about prices", L("prices: about the same"), null);
            S4("street.business", "heard", "{player} tells you about their business; you have heard of it", L("their business: {shop}"), L("{shop}"));
            S4("street.business", "will", "{player} tells you about their business; you will try it", L("their business: {shop}"), L("{shop}"));
            S4("street.business", "no", "{player} tells you about their business; you are not interested", L("their business: {shop}"), null);
            S4("street.business", "again", "{player} tells you about their business again; they told you already", L("their business: {shop}"), L("{shop}"));
            S4("street.said", "warm", "{player} says something to you in the street", L("you take it: well"), null);
            S4("street.said", "cool", "{player} says something to you in the street", L("you take it: badly"), null);
            S4("street.said", "odd", "{player} says something you do not understand", None, null);
            S4("street.rude", "cold", "{player} is rude to you", None, null);
            S4("street.rude", "leave", "{player} is rude to you; you walk off", None, null);
            S4("street.threat", "leave", "{player} threatens you; you get away from them", None, null);
            S4("street.tired", null, "you have given {player} enough of your time; you have to go now", None, null);
            S4("street.bye", "warm", "{player} says goodbye", None, null);
            S4("street.bye", "cool", "{player} says goodbye", None, null);
        }

        // A card of street talk: voice "stranger", spoken, at most 140 characters, the friend face (who: name, age, job, home; a stranger's role).
        private static void S4(string key, string branch, string news, string[] facts, string[] must)
        {
            List.Add(new TextIntent(key, branch, "stranger", "spoken", 140, TextFace.Friend, TextUse.People, news, facts, StreetWrite, must, DataV4));
        }
    }
}
