using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

// Faults found in Scripts/World and Phone: the visuals put the game's level of detail back from a value read only when
// they started, so going before the first frame set it to nothing for the session; and a payment refused on the doctor's
// call, or at the clinic's desk, left the offer on the screen with nothing said, since the game's dialog keeps its entry
// when a choice gives none back (DialogController.ConfirmCurrentEntry).
internal static partial class Program
{
    private static void PhoneWorldFixes()
    {
        string visuals = ModSource(@"World\VisualEnhancer.cs");
        string start = MemberText(visuals, "private void Start()"), update = MemberText(visuals, "private void OnUpdate()"), destroy = MemberText(visuals, "private void OnDestroy()");
        Check(!start.Contains("_vanillaLodBias") && update.Contains("if (!_lodBiasHeld)") && update.IndexOf("_vanillaLodBias = QualitySettings.lodBias;", StringComparison.Ordinal) > 0
              && update.IndexOf("_vanillaLodBias = QualitySettings.lodBias;", StringComparison.Ordinal) < update.IndexOf("QualitySettings.lodBias = on", StringComparison.Ordinal)
              && Regex.IsMatch(destroy, @"if \(_lodBiasHeld\)\s*\{\s*QualitySettings\.lodBias = _vanillaLodBias;"),
            "visuals fix: the level of detail is recorded where it is first set, and put back only if it was");

        string contacts = ModSource(@"Phone\PhoneContacts.cs"), visit = ModSource(@"Home\DoctorVisit.cs");
        string treat = MemberText(contacts, "private static DialogEntry Treat()");
        string prescribe = MemberText(visit, "private static DialogEntry Prescribe(bool medicine, bool note)");
        Check(!treat.Contains("return null;") && treat.Contains("PhoneContacts.Closing(\"reallife_doctor_call_no_money\"")
              && !prescribe.Contains("return null;") && prescribe.Contains("PhoneContacts.Closing(\"reallife_doctor_prescribe_no_money\""),
            "doctor fix: a payment refused on the doctor's call or at the clinic ends with a closing line, as the other refusals do");
        foreach (string language in new[] { "en", "pt", "zh-cn" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), language + ".json"));
            Check(words != null && words.TryGetValue("reallife_doctor_call_no_money", out string call) && call.Contains("{amount}")
                  && words.TryGetValue("reallife_doctor_prescribe_no_money", out string desk) && desk.Contains("{amount}"),
                "doctor fix (" + language + "): both closing lines are written and name the amount");
        }
        Check(FixLineEverywhere("a payment refused at the doctor's", "um pagamento recusado no médico", "在医生处付款被拒时", "Eine abgelehnte Zahlung beim Arzt",
                                "un paiement refusé chez le médecin", "un pago rechazado en el médico", "отклонённая оплата у врача", "een geweigerde betaling bij de dokter",
                                "reddedilen bir ödeme"),
            "doctor fix: What's new has the refused payment's line, with its fix mark, in some row of every language");
        Check(FixLineEverywhere("driving another player's car no longer gives it this mod's engine figures", "dirigir o carro de outro jogador não dá mais",
                                "驾驶其他玩家的车不再让它获得本模组的引擎数值", "nicht mehr die Motorwerte dieser Mod", "conduire la voiture d'un autre joueur ne lui donne plus",
                                "conducir el coche de otro jugador ya no le da", "больше не получает параметры двигателя этого мода", "niet meer de motorwaarden van deze mod",
                                "artık ona bu modun motor değerlerini vermiyor"),
            "driving fix: What's new has the car tuning's Going Public line, with its fix mark, in some row of every language");
    }
}
