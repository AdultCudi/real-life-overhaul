using System;
using System.Collections.Generic;

namespace RealLifeOverhaul
{
    /// <summary>
    /// A call's composer, the words: what each phrase of the nine languages says to the reading (the Reading part), English
    /// with its casual spellings and the shortenings typed without an apostrophe ("dont", "gr8", "kinda"), then Portuguese,
    /// Spanish, French, German, Dutch, Russian, Turkish and Chinese, each with its own turns (where a word of one language
    /// would read the wrong way in another, it is left out: "pain" is French bread, "dire" is French "to say", "hasta" is
    /// Spanish "until", "ne" is Turkish "what"). Every phrase is made plain as typed words are (accents off, a Chinese
    /// character a word), so it is written as people type it. A phrase that is one sense in one language and another in
    /// another is a fault of the table, which EconomyCheck fails (TypedReadingChecks.cs).
    /// COST: read once, when the table is made.
    /// </summary>
    public static partial class SocialComposerRules
    {
        /// <summary>What a phrase of the table says to the reading.</summary>
        internal enum Sense : byte
        {
            Word,          // only words: one the table does not know, or a phrase kept whole so that one inside it is not read (不好意思)
            Good,          // good news, or going well
            Bad,           // bad news, or going badly
            Opening,       // good, unless it opens an answer that says more: "well, it lost money", "bom, mas lento"
            Plain,         // not clear: "meh", "so so", "not sure"
            Very,          // strengthens or softens the next word, and is passed over by a turn: "very", "kinda", 很
            VeryOrGood,    // strengthens a good or bad word after it, else good: "super", "bem", "bien", 好
            Turn,          // makes the next good or bad word within TurnReach the other: "not", "pas", "nicht", "без"
            TurnOrNo,      // makes the very next one the other, or is a no: "no", "нет"
            TurnFarOrNo,   // makes one within TurnReach the other, or is a no: "never", "não", 不, "not really"
            TurnOrBad,     // makes one within TurnReach the other, or is bad: "not very", "не очень", 不太
            TurnBefore,    // makes the good or bad word just before it the other, or is a no: Turkish "değil", "yok"
            Yes,           // only a yes
            No,            // only a no
            But,           // every word after it counts double: "but", "mas", "aber", 但是
            Back,          // a question back ("you?", "and you?", "et vous ?", 你呢): what comes before it is said, not asked
            Stop,          // (never in the table) the end of a clause
            Question       // (never in the table) the end of a clause that asks
        }

        // The table: every language's phrases, made plain; a fault for each phrase that cannot be one or says two things.
        private static Dictionary<string, Sense> Build(List<string> faults)
        {
            var t = new Dictionary<string, Sense>(2048, StringComparer.Ordinal)
            {
                { GoodSign, Sense.Good }, { BadSign, Sense.Bad }, { PlainSign, Sense.Plain }
            };
            English(t, faults);
            Portuguese(t, faults);
            Spanish(t, faults);
            French(t, faults);
            German(t, faults);
            Dutch(t, faults);
            Russian(t, faults);
            Turkish(t, faults);
            Chinese(t, faults);
            return t;
        }

        private static void Add(Dictionary<string, Sense> t, List<string> faults, Sense sense, params string[] phrases)
        {
            for (int i = 0; i < phrases.Length; i++)
            {
                List<string> words = Plain(phrases[i]);
                if (words.Count == 0 || words.Count > PhraseMost || words.Contains(Break) || words.Contains(Asked))
                {
                    faults.Add("\"" + phrases[i] + "\" is not one phrase of at most " + PhraseMost + " words");
                    continue;
                }
                string key = string.Join(" ", words);
                if (t.TryGetValue(key, out Sense had))
                {
                    if (had != sense)
                    {
                        faults.Add("\"" + phrases[i] + "\" is " + had + " and " + sense);
                    }
                    continue;
                }
                t[key] = sense;
            }
        }

        private static void English(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "good", "gud", "gd", "great", "grt", "gr8", "nice", "noice", "lovely", "luvly", "wonderful", "amazing",
                "amazin", "awesome", "awsome", "awesum", "fantastic", "brilliant", "brill", "excellent", "superb", "perfect", "terrific", "fab",
                "fabulous", "splendid", "marvellous", "marvelous", "outstanding", "incredible", "stellar", "ideal", "cool", "sweet", "happy",
                "glad", "pleased", "proud", "thrilled", "delighted", "chuffed", "excited", "stoked", "buzzing", "ace", "lush", "champion",
                "peachy", "dandy", "fine", "ok", "okay", "okey", "oki", "okie", "okk", "k", "kk", "alright", "allright", "alrite", "aight",
                "all right", "all good", "all fine", "decent", "solid", "steady", "stable", "strong", "booming", "thriving", "flourishing",
                "growing", "improving", "recovering", "recovered", "healing", "better", "much better", "getting better", "doing better",
                "safe", "unhurt", "uninjured", "unharmed", "in one piece", "alive", "healthy", "lucky", "luckily", "fortunately", "blessed",
                "grateful", "thankful", "relieved", "content", "satisfied", "calm", "relaxed", "love", "loved", "loving", "luv", "love it",
                "enjoy", "enjoyed", "enjoying", "fun", "yay", "woo", "woohoo", "hooray", "hurray", "yippee", "phew", "thank god",
                "thank goodness", "success", "successful", "winning", "won", "profit", "profits", "profitable", "in profit", "in the black",
                "making money", "made money", "raking it in", "rolling in it", "loaded", "rich", "wealthy", "holding up", "picking up",
                "looking up", "on track", "on the up", "going strong", "over the moon", "never better",
                "never felt better", "never been better", "couldnt be better", "could not be better", "couldnt be happier", "better than ever",
                "could be worse", "couldve been worse", "could've been worse", "could have been worse", "well done", "top", "spot on", "sorted", "smashing",
                "cracking", "wicked", "killing it", "crushing it", "nailed it", "beautiful", "gorgeous", "positive", "promising");
            Add(t, f, Sense.Opening, "well");
            Add(t, f, Sense.Bad, "bad", "badly", "terrible", "terribly", "terible", "awful", "awfully", "aweful", "awfull", "horrible", "horible",
                "horrid", "horrific", "dreadful", "appalling", "atrocious", "abysmal", "dismal", "grim", "bleak", "poor", "poorly", "rough",
                "ruff", "tough", "rubbish", "rubish", "crap", "crappy", "shit", "shite", "shitty", "lousy", "naff", "shabby", "sucks", "suck",
                "sucked", "sucky", "worse", "worst", "worse than ever", "couldnt be worse", "could not be worse", "could be better",
                "couldve been better", "could've been better", "could have been better", "been better", "seen better days", "had better days", "struggling",
                "struggle", "struggles", "struggled", "failing", "failed", "fail", "failure", "sinking", "dying", "slow", "slowing", "sad",
                "unhappy", "upset", "angry", "furious", "annoyed", "frustrated", "frustrating", "gutted", "devastated", "heartbroken",
                "depressed", "depressing", "miserable", "lonely", "stressed", "stressful", "anxious", "worried", "worry", "worries", "worrying",
                "scared", "afraid", "terrified", "frightened", "frightening", "scary", "nervous", "shocking", "tired", "exhausted", "exausted",
                "exhausting", "knackered", "shattered", "drained", "worn out", "sore", "hurt", "hurts", "hurting", "injured", "bruised",
                "broken", "bleeding", "in pain", "painful", "aching", "ill", "sick", "unwell", "feverish", "under the weather", "loss",
                "losses", "losing", "lost", "lose", "in the red", "in debt", "debt", "debts", "broke", "bankrupt", "skint", "problem",
                "problems", "issue", "issues", "trouble", "troubles", "disaster", "disastrous", "nightmare", "catastrophe", "tragic", "mess",
                "messy", "messed up", "screwed", "chaos", "chaotic", "hell", "ugh", "urgh", "argh", "oof", "ouch", "yikes", "oh no",
                "oh dear", "oh god", "boo", "unfortunately", "sadly", "alas", "sorry", "bummer", "pity", "shame", "complain", "complaint",
                "complaints", "grumble", "falling apart", "going under", "hanging in there", "hard times", "hard day", "hard week", "its hard", "it's hard",
                "been hard", "hard going", "tough times", "fed up", "had enough", "sick of it", "pathetic", "useless", "hopeless", "unlucky");
            Add(t, f, Sense.Plain, "meh", "eh", "hmm", "hm", "so so", "soso", "average", "mixed", "up and down", "ups and downs", "not sure",
                "unsure", "dunno", "idk", "dont know", "do not know", "no idea", "who knows", "maybe", "perhaps", "possibly", "i guess",
                "guess so", "i suppose", "suppose so", "depends", "it depends", "same as usual", "as usual", "same old", "nothing much",
                "nothing new", "fair enough", "oh well", "whatever", "hard to say", "ok ish", "okish", "mediocre", "middling", "fifty fifty",
                "50 50", "more or less", "managing", "coping", "getting by", "surviving", "never mind");
            Add(t, f, Sense.Very, "very", "v", "really", "rly", "rlly", "realy", "so", "soo", "too", "pretty", "quite", "rather", "fairly", "kinda",
                "kind of", "sorta", "sort of", "bit", "a bit", "abit", "little", "a little", "slightly", "somewhat", "extremely", "incredibly",
                "completely", "utterly", "seriously", "honestly", "damn", "bloody", "proper", "real", "ultra", "insanely", "ridiculously",
                "way", "much", "lot", "a lot", "alot", "lots", "most", "more", "ever", "all", "just", "still", "even", "hella");
            Add(t, f, Sense.VeryOrGood, "super", "mega", "best");
            Add(t, f, Sense.Turn, "not", "dont", "doesnt", "didnt", "isnt", "wasnt", "arent", "werent", "aint", "cant", "cannot", "couldnt",
                "wont", "wouldnt", "shouldnt", "havent", "hasnt", "hadnt", "neednt", "mustnt", "hardly", "barely", "scarcely", "nothing",
                "nobody", "noone", "no one", "none", "without", "nor", "neither");
            Add(t, f, Sense.TurnOrNo, "no");
            Add(t, f, Sense.TurnFarOrNo, "never", "not really", "not rly", "not realy", "not much", "not at all", "not yet", "not quite");
            Add(t, f, Sense.TurnOrBad, "not very", "not so", "not too");
            Add(t, f, Sense.Yes, "yes", "yess", "yeah", "yeahh", "yea", "yep", "yup", "yah", "yeh", "ye", "yh", "yas", "yass", "yus", "yesh", "sure", "surely",
                "of course", "ofc", "course", "definitely", "def", "defo", "deffo", "absolutely", "totally", "indeed", "aye", "certainly",
                "correct", "true", "uh huh", "mhm", "mmhm", "hell yeah", "hell yes", "obviously", "obv", "tell me", "tell me more", "go on",
                "do tell", "im listening", "i'm listening", "i am listening", "i know", "ikr", "for sure", "you bet", "yessir", "no doubt");
            Add(t, f, Sense.No, "noo", "nah", "nahh", "naw", "nope", "nop", "nay", "no way", "hell no", "of course not", "nuh uh", "nuhuh", "dont think so",
                "not true", "not likely");
            Add(t, f, Sense.But, "but", "however");
            Add(t, f, Sense.Back, "you", "u", "and you", "and u", "you too", "and yourself", "yourself", "how about you", "what about you", "wbu",
                "hbu");
            Add(t, f, Sense.Word, "not only", "no matter", "no wonder");
        }

        private static void Portuguese(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "boa", "ótimo", "ótima", "excelente", "maravilhoso", "maravilhosa", "incrível", "perfeito", "perfeita", "legal",
                "massa", "beleza", "blz", "tranquilo", "tranquila", "tudo certo", "tudo em ordem", "de boa", "sucesso", "lucro", "lucrando", "ganhando", "ganhamos",
                "lucrativo", "feliz", "contente", "satisfeito", "satisfeita", "melhor", "melhorando", "fantástico", "fantástica", "bacana", "joia",
                "firme", "suave", "td bem", "tdb", "vale a pena", "dando lucro", "aguentando firme", "a mil", "bombando", "sossegado", "salvo",
                "salva", "inteiro", "inteira", "vivo", "viva", "saudável", "animado", "animada", "orgulhoso", "orgulhosa", "aliviado", "aliviada",
                "amei", "adorei", "gostei", "sorte", "ainda bem", "graças a deus", "felizmente");
            Add(t, f, Sense.Opening, "bom");
            Add(t, f, Sense.VeryOrGood, "bem");
            Add(t, f, Sense.Bad, "mal", "ruim", "péssimo", "péssima", "horrível", "terrível", "pior", "triste", "difícil", "duro", "dura", "cansado",
                "cansada", "cansativo", "exausto", "exausta", "prejuízo", "perdendo", "perdi", "perdeu", "perdemos", "perda", "perdas", "falido",
                "falida", "falindo", "quebrado", "quebrada", "dívidas", "dívida", "endividado", "endividada", "chato", "chata", "preocupado",
                "preocupada", "estressado", "estressada", "doente", "machucado", "machucada", "ferido", "ferida", "dor", "doendo", "infelizmente",
                "que pena", "lento", "lenta", "parado", "parada", "fraco", "fraca", "complicado", "complicada", "puxado", "puxada", "problema",
                "problemas", "sofrendo", "desastre", "um saco", "droga", "poxa", "afundando", "no vermelho", "apertado", "apertada", "chateado",
                "chateada", "irritado", "irritada", "mal das pernas", "merda");
            Add(t, f, Sense.Plain, "mais ou menos", "sei lá", "não sei", "talvez", "depende", "normal", "na mesma", "mesma coisa", "de sempre",
                "como sempre", "vou indo", "vamos levando", "levando");
            Add(t, f, Sense.Very, "muito", "muita", "mto", "mt", "tão", "bastante", "meio", "um pouco", "demais", "pra caramba", "realmente",
                "totalmente", "extremamente");
            Add(t, f, Sense.Turn, "nem", "nada", "sem", "pouco", "ninguém");
            Add(t, f, Sense.TurnFarOrNo, "não", "nunca", "jamais");
            Add(t, f, Sense.TurnOrBad, "não muito", "não tão");
            Add(t, f, Sense.Yes, "sim", "claro", "com certeza", "certeza", "uhum", "aham", "pode crer", "isso mesmo", "claro que sim", "conta",
                "me conta", "conta mais", "pode falar", "verdade", "é verdade");
            Add(t, f, Sense.No, "de jeito nenhum", "nem pensar", "ainda não", "claro que não", "nem tanto");
            Add(t, f, Sense.But, "mas", "porém", "contudo", "entretanto");
            Add(t, f, Sense.Back, "e você", "e vc", "e tu", "e contigo");
        }

        private static void Spanish(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "buena", "buenísimo", "buenísima", "genial", "excelente", "estupendo", "estupenda", "fantástico", "fantástica",
                "maravilloso", "maravillosa", "perfecto", "perfecta", "increíble", "guay", "chévere", "bacán", "chido", "feliz", "contento",
                "contenta", "mejor", "mejorando", "éxito", "ganancias", "ganando", "ganamos", "rentable", "tranquilo", "tranquila", "de maravilla",
                "de lujo", "sano", "sana", "a salvo", "vivo", "entero", "orgulloso", "orgullosa", "aliviado", "aliviada", "encantado",
                "encantada", "me encanta", "me gusta", "todo en orden", "beneficios", "menos mal", "gracias a dios", "afortunadamente",
                "por suerte");
            Add(t, f, Sense.Opening, "bueno");
            Add(t, f, Sense.VeryOrGood, "bien");
            Add(t, f, Sense.Bad, "mal", "malo", "mala", "fatal", "terrible", "horrible", "pésimo", "pésima", "peor", "triste", "difícil", "duro",
                "dura", "cansado", "cansada", "agotado", "agotada", "pérdidas", "perdiendo", "perdí", "perdimos", "quiebra", "en quiebra",
                "deudas", "deuda", "endeudado", "enfermo", "enferma", "herido", "herida", "dolor", "duele", "me duele", "preocupado",
                "preocupada", "estresado", "estresada", "lento", "lenta", "flojo", "floja", "lamentablemente", "desgraciadamente", "lástima",
                "que pena", "una pena", "problema", "problemas", "desastre", "un desastre", "jodido", "jodida", "chungo", "hundiendo",
                "complicado", "complicada", "agobiado", "agobiada", "harto", "harta", "enojado", "enojada", "enfadado", "enfadada");
            Add(t, f, Sense.Plain, "más o menos", "regular", "no sé", "quizá", "quizás", "tal vez", "depende", "así así", "normal", "como siempre",
                "lo de siempre", "ahí vamos", "tirando");
            Add(t, f, Sense.Very, "muy", "tan", "bastante", "re", "demasiado", "un poco", "algo", "medio", "realmente", "muchísimo", "mucho");
            Add(t, f, Sense.Turn, "ni", "sin", "poco", "nadie", "no está", "no estoy", "no estamos", "no es", "no son", "no va", "no van", "no fue",
                "no me", "no ha", "no hay", "no anda", "no salió");
            Add(t, f, Sense.TurnFarOrNo, "nunca", "jamás");
            Add(t, f, Sense.TurnOrBad, "no muy", "no tan");
            Add(t, f, Sense.Yes, "sí", "claro", "vale", "por supuesto", "desde luego", "sip", "claro que sí", "efectivamente", "exacto", "cuéntame",
                "dime", "es verdad", "verdad", "seguro");
            Add(t, f, Sense.No, "nop", "para nada", "en absoluto", "qué va", "todavía no", "aún no", "claro que no", "ni hablar", "de ninguna manera",
                "no mucho");
            Add(t, f, Sense.But, "pero", "sin embargo");
            Add(t, f, Sense.Back, "y tú", "y vos", "y usted", "y a ti");
        }

        private static void French(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "bonne", "génial", "excellent", "excellente", "parfait", "parfaite", "formidable", "magnifique", "merveilleux",
                "merveilleuse", "sympa", "content", "contente", "heureux", "heureuse", "ravi", "ravie", "mieux", "ça va", "ça roule", "ça marche",
                "ça va pas mal", "nickel", "impeccable", "bénéfice", "bénéfices", "rentable", "succès", "réussi", "chouette", "tranquille",
                "en forme", "en pleine forme", "sain et sauf", "saine et sauve", "vivant", "vivante", "soulagé", "soulagée", "fier", "fière",
                "au top", "on gagne", "gagne", "gagnons", "ça cartonne", "ça tient", "ça tient le coup", "ça pourrait être pire", "heureusement", "dieu merci", "top", "cool");
            Add(t, f, Sense.Opening, "bon");
            Add(t, f, Sense.VeryOrGood, "bien");
            Add(t, f, Sense.Bad, "mal", "mauvais", "mauvaise", "nul", "nulle", "horrible", "affreux", "affreuse", "catastrophique", "catastrophe",
                "pire", "triste", "difficile", "dur", "dure", "fatigué", "fatiguée", "crevé", "crevée", "épuisé", "épuisée", "pertes", "perte",
                "perdu", "perd", "perdons", "déficit", "faillite", "dettes", "malade", "blessé", "blessée", "douleur", "inquiet", "inquiète", "stressé", "stressée",
                "galère", "dommage", "malheureusement", "problème", "problèmes", "souci", "soucis", "terrible", "pas terrible", "ça va pas",
                "ça ne va pas", "ça va mal", "la cata", "lent", "lente", "ça pourrait être mieux", "ras le bol", "déprimé", "déprimée",
                "en difficulté", "ça coule", "grave", "j'en ai assez");
            Add(t, f, Sense.Plain, "bof", "moyen", "moyennement", "comme ci comme ça", "couci couça", "je sais pas", "je ne sais pas", "sais pas",
                "peut-être", "ça dépend", "pas sûr", "pas sûre", "normal", "comme d'habitude", "on fait aller", "ça peut aller");
            Add(t, f, Sense.Very, "très", "trop", "vraiment", "assez", "plutôt", "un peu", "tellement", "carrément", "hyper", "ultra", "vachement",
                "franchement", "totalement", "complètement");
            Add(t, f, Sense.Turn, "pas", "rien", "sans", "aucun", "aucune", "peu");
            Add(t, f, Sense.TurnFarOrNo, "jamais", "pas vraiment", "pas du tout", "pas encore", "pas tellement", "pas spécialement");
            Add(t, f, Sense.TurnOrBad, "pas très", "pas trop", "pas si");
            Add(t, f, Sense.Yes, "oui", "ouais", "ouai", "ui", "si", "bien sûr", "exactement", "absolument", "évidemment", "d'accord", "dac",
                "raconte", "dis-moi", "vas-y", "c'est vrai", "vrai");
            Add(t, f, Sense.No, "non", "nan", "absolument pas", "certainement pas", "jamais de la vie", "pas question");
            Add(t, f, Sense.But, "mais", "pourtant", "cependant");
            Add(t, f, Sense.Back, "et vous", "et toi", "et de votre côté", "et de ton côté");
        }

        private static void German(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "gut", "gute", "guter", "gutes", "toll", "prima", "klasse", "spitze", "wunderbar", "ausgezeichnet", "hervorragend",
                "perfekt", "genial", "bestens", "besser", "glücklich", "froh", "zufrieden", "erfolgreich", "gewinn", "gewinne", "profit", "geil",
                "stark", "passt", "passt schon", "alles gut", "alles klar", "alles bestens", "in ordnung", "wohlauf", "unverletzt", "gesund",
                "erleichtert", "stolz", "glück", "zum glück", "gott sei dank", "fein", "könnte schlimmer sein", "cool", "top", "okay", "ok");
            Add(t, f, Sense.Bad, "schlecht", "schlechter", "schlimm", "schlimmer", "furchtbar", "schrecklich", "mies", "übel", "katastrophal",
                "katastrophe", "mist", "scheiße", "beschissen", "traurig", "schwer", "schwierig", "hart", "müde", "erschöpft", "kaputt", "verlust",
                "verluste", "pleite", "schulden", "krank", "verletzt", "schmerzen", "sorgen", "besorgt", "gestresst", "stressig", "leider",
                "schade", "problem", "probleme", "langsam", "flaute", "könnte besser sein");
            Add(t, f, Sense.Plain, "geht so", "weiß nicht", "keine ahnung", "vielleicht", "kommt drauf an", "naja", "na ja", "mittel", "mittelmäßig",
                "so lala", "solala", "durchwachsen", "wie immer", "das übliche");
            Add(t, f, Sense.Very, "sehr", "echt", "wirklich", "total", "ziemlich", "ganz", "so", "zu", "voll", "richtig", "etwas", "bisschen",
                "ein bisschen", "recht", "eher", "extrem", "unglaublich", "besonders");
            Add(t, f, Sense.VeryOrGood, "super", "mega");
            Add(t, f, Sense.Turn, "nicht", "nichts", "kein", "keine", "keinen", "keiner", "keinem", "ohne", "kaum");
            Add(t, f, Sense.TurnFarOrNo, "nie", "niemals", "nicht wirklich", "gar nicht", "überhaupt nicht", "noch nicht");
            Add(t, f, Sense.TurnOrBad, "nicht so", "nicht besonders", "nicht sehr");
            Add(t, f, Sense.Yes, "ja", "jap", "jo", "jawohl", "klar", "natürlich", "sicher", "genau", "doch", "na klar", "auf jeden fall", "stimmt",
                "freilich", "erzähl", "erzähl mal", "schieß los", "und wie");
            Add(t, f, Sense.No, "nein", "nee", "auf keinen fall", "eher nicht", "nein danke");
            Add(t, f, Sense.But, "aber", "jedoch", "trotzdem");
            Add(t, f, Sense.Back, "und du", "und dir", "und ihnen", "und bei dir", "selbst");
        }

        private static void Dutch(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "goed", "prima", "top", "toppie", "geweldig", "fantastisch", "uitstekend", "perfect", "mooi", "leuk", "fijn",
                "lekker", "gaaf", "blij", "tevreden", "gelukkig", "beter", "winst", "succes", "succesvol", "oké", "okee", "in orde", "alles goed",
                "heerlijk", "gezond", "veilig", "ongedeerd", "opgelucht", "trots", "kan slechter", "nooit beter", "gelukkig maar", "godzijdank");
            Add(t, f, Sense.Bad, "slecht", "slechter", "slechtst", "verschrikkelijk", "vreselijk", "afschuwelijk", "rot", "beroerd", "waardeloos",
                "zwaar", "moeilijk", "lastig", "moe", "doodmoe", "uitgeput", "kapot", "verlies", "verliezen", "verloren", "failliet", "schulden",
                "ziek", "gewond", "pijn", "zorgen", "bezorgd", "gestrest", "verdrietig", "droevig", "balen", "jammer", "helaas", "kut", "klote",
                "verkeerd", "probleem", "problemen", "traag", "kan beter");
            Add(t, f, Sense.Plain, "gaat wel", "weet niet", "weet ik niet", "geen idee", "misschien", "hangt ervan af", "matig", "zozo", "zo zo",
                "gaat zo", "zoals altijd");
            Add(t, f, Sense.Very, "heel", "erg", "echt", "zeer", "vrij", "nogal", "behoorlijk", "een beetje", "beetje", "zo", "te", "ontzettend",
                "hartstikke", "enorm");
            Add(t, f, Sense.VeryOrGood, "redelijk", "best", "super");
            Add(t, f, Sense.Turn, "niet", "geen", "niks", "niets", "zonder", "nauwelijks", "niemand");
            Add(t, f, Sense.TurnFarOrNo, "nooit", "niet echt", "helemaal niet", "nog niet");
            Add(t, f, Sense.TurnOrBad, "niet zo", "niet erg", "niet heel");
            Add(t, f, Sense.Yes, "ja", "jazeker", "jawel", "jep", "jup", "zeker", "natuurlijk", "tuurlijk", "inderdaad", "klopt", "absoluut",
                "vertel", "vertel eens", "vertel op", "zeker weten");
            Add(t, f, Sense.No, "nee", "neen", "zeker niet", "echt niet", "absoluut niet");
            Add(t, f, Sense.But, "maar", "echter");
            Add(t, f, Sense.Back, "en jij", "en jou", "en met jou", "en jijzelf");
        }

        private static void Russian(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "хорошо", "хороший", "хорошая", "хорошее", "хорошие", "отлично", "отличный", "отличная", "прекрасно", "прекрасный",
                "замечательно", "замечательный", "класс", "классно", "круто", "здорово", "великолепно", "шикарно", "чудесно", "неплохо", "неплохой",
                "неплохая", "нормально", "норм", "нормалек", "ок", "окей", "лучше", "успешно", "успех", "прибыль", "зарабатываем", "в плюсе", "рад", "рада",
                "доволен", "довольна", "счастлив", "счастлива", "в порядке", "порядок", "держимся", "держится", "цел", "цела", "жив", "жива",
                "здоров", "здорова", "бывало и хуже", "могло быть хуже", "лучше не бывает", "идеально", "огонь", "слава богу", "к счастью");
            Add(t, f, Sense.Bad, "плохо", "плохой", "плохая", "плохое", "ужасно", "ужасный", "ужасная", "кошмар", "кошмарно", "хуже", "худший",
                "тяжело", "тяжёлый", "трудно", "сложно", "грустно", "печально", "жаль", "увы", "к сожалению", "отстой", "хреново", "фигово",
                "паршиво", "дерьмово", "беда", "устал", "устала", "устали", "болею", "болен", "больна", "заболел", "заболела", "больно", "ранен",
                "ранена", "убыток", "убытки", "в минусе", "банкрот", "долги", "проблемы", "проблема", "проблем", "неважно", "могло быть лучше",
                "бывало и лучше", "медленно", "провал", "тонем", "теряем", "теряю");
            Add(t, f, Sense.Plain, "так себе", "не знаю", "без понятия", "может быть", "возможно", "наверное", "средне", "по-разному", "как обычно",
                "как всегда", "сойдёт");
            Add(t, f, Sense.Very, "очень", "так", "слишком", "совсем", "довольно", "вполне", "реально", "прям", "прямо", "весьма", "немного",
                "немножко", "чуть", "слегка", "жутко", "дико");
            Add(t, f, Sense.VeryOrGood, "супер");
            Add(t, f, Sense.Turn, "не", "без", "ничего", "никто", "ни");
            Add(t, f, Sense.TurnOrNo, "нет");
            Add(t, f, Sense.TurnFarOrNo, "никогда", "не совсем");
            Add(t, f, Sense.TurnOrBad, "не очень", "не особо", "не слишком", "не так");
            Add(t, f, Sense.Yes, "да", "ага", "угу", "конечно", "разумеется", "точно", "верно", "ладно", "естественно", "давай", "расскажи",
                "рассказывай", "правда", "это правда", "ну да", "да да");
            Add(t, f, Sense.No, "неа", "не-а", "нету", "никак", "вовсе нет", "совсем нет", "пока нет", "конечно нет", "да нет", "неправда",
                "не правда");
            Add(t, f, Sense.But, "но", "однако", "зато", "хотя");
            Add(t, f, Sense.Back, "а ты", "а у тебя", "а вы", "а у вас", "у тебя как");
        }

        private static void Turkish(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "iyi", "iyiyim", "iyiyiz", "iyidir", "iyi gidiyor", "harika", "harikayım", "mükemmel", "güzel", "muhteşem",
                "şahane", "keyifli", "mutlu", "mutluyum", "memnun", "memnunum", "başarılı", "kâr ediyoruz", "kârdayız", "para kazanıyoruz",
                "kazanıyoruz", "kazandık", "her şey yolunda", "yolunda", "sağlam", "sağlıklı", "güvende", "hayattayım", "rahat", "huzurlu",
                "tamam", "bomba gibi", "iyileşiyorum", "toparlıyoruz", "rahatladım", "gurur duyuyorum", "şükür", "çok şükür", "allaha şükür",
                "neyse ki");
            Add(t, f, Sense.Bad, "kötü", "kötüyüm", "kötü gidiyor", "berbat", "korkunç", "rezalet", "zor", "zorlu", "yorgun", "yorgunum", "yorucu",
                "bitkin", "üzgün", "üzgünüm", "mutsuz", "zarar", "zarardayız", "zarar ediyoruz", "batıyoruz", "battık", "iflas", "borç", "borçluyum",
                "hastayım", "hastayız", "hasta oldum", "yaralı", "yaralıyım", "ağrı", "ağrıyor", "acıyor", "endişeli", "stresli", "sıkıntı",
                "sıkıntılı", "sorun", "sorunlu", "problem", "dert", "felaket", "kabus", "maalesef", "yazık", "fena", "kötüleşiyor", "yavaş",
                "durgun", "zor durumda");
            Add(t, f, Sense.Plain, "idare eder", "bilmiyorum", "bilmem", "fikrim yok", "belki", "olabilir", "duruma göre", "eh işte", "şöyle böyle",
                "orta", "her zamanki gibi", "ne bileyim");
            Add(t, f, Sense.Very, "çok", "pek", "gayet", "oldukça", "epey", "baya", "bayağı", "biraz", "aşırı", "son derece", "fazla", "hiç",
                "gerçekten");
            Add(t, f, Sense.TurnBefore, "değil", "yok", "gitmiyor");
            Add(t, f, Sense.TurnFarOrNo, "asla");
            Add(t, f, Sense.Yes, "evet", "evt", "hee", "tabii", "tabi", "tabiki", "elbette", "kesinlikle", "aynen", "olur", "doğru", "tamamdır",
                "anlat", "anlatsana", "dinliyorum");
            Add(t, f, Sense.No, "hayır", "yoo", "pek sayılmaz", "olmaz");
            Add(t, f, Sense.But, "ama", "fakat", "ancak", "lakin");
            Add(t, f, Sense.Back, "ya sen", "sen", "sende", "sen nasılsın");
        }

        private static void Chinese(Dictionary<string, Sense> t, List<string> f)
        {
            Add(t, f, Sense.Good, "不错", "棒", "优秀", "完美", "厉害", "顺利", "成功", "赚钱", "赚了", "赚", "盈利", "挣钱", "开心", "高兴", "快乐", "幸福",
                "满意", "还行", "还可以", "没事", "没问题", "没关系", "不要紧", "平安", "安全", "健康", "稳定", "稳", "好多了", "好转", "红火", "兴旺", "发财",
                "有钱", "放心", "幸亏", "幸好", "还好", "好看", "好吃", "好玩", "爽", "赞", "666", "完好", "活着", "顶住了", "撑得住", "挺住", "好极了",
                "不坏", "不差", "谢天谢地", "挺好的", "很好的");
            Add(t, f, Sense.Bad, "差", "糟", "糟糕", "烂", "惨", "坏", "难", "困难", "难过", "伤心", "累", "辛苦", "疲惫", "亏", "亏钱", "亏损", "赔钱",
                "赔了", "倒闭", "破产", "欠债", "病", "生病", "受伤", "伤", "疼", "痛", "担心", "焦虑", "压力", "烦", "郁闷", "失望", "可怕", "倒霉",
                "不顺", "不行", "不怎么样", "一塌糊涂", "惨淡", "完蛋", "崩溃", "撑不住", "可惜", "遗憾", "可怜", "麻烦", "问题", "慢", "冷清");
            Add(t, f, Sense.Plain, "一般", "一般般", "马马虎虎", "凑合", "还凑合", "老样子", "就那样", "还那样", "不知道", "不清楚", "也许", "可能", "说不定",
                "看情况", "好吧", "随便", "无所谓", "差不多");
            Add(t, f, Sense.Very, "很", "太", "非常", "挺", "真", "超", "特别", "十分", "比较", "有点", "有些", "蛮", "相当", "极", "更", "最", "超级",
                "巨", "贼");
            Add(t, f, Sense.VeryOrGood, "好");
            Add(t, f, Sense.Turn, "别", "无", "未", "没什么", "并不", "不用");
            Add(t, f, Sense.TurnFarOrNo, "不", "没", "没有", "不是", "并没有", "一点也不", "从不", "还没", "还没有", "不会", "才不", "当然不");
            Add(t, f, Sense.TurnOrBad, "不太", "不怎么", "不是很", "不够");
            Add(t, f, Sense.Yes, "是", "是的", "对", "对的", "嗯", "当然", "没错", "确实", "肯定", "好的", "说说", "讲讲", "说吧", "快说", "我在听", "真的");
            Add(t, f, Sense.But, "但是", "但", "可是", "不过", "然而");
            Add(t, f, Sense.Back, "你呢", "你怎么样", "那你呢");
            Add(t, f, Sense.Word, "不好意思", "你好", "好像", "好久", "爱好", "只好", "最好", "刚好", "正好", "好几", "好处", "好多", "不但", "难道",
                "看病", "不管", "要不", "不然", "不得不", "不客气");
        }
    }
}
