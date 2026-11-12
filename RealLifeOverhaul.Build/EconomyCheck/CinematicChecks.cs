using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using RealLifeOverhaul.Photos;

// The cinematic shots, a developer's tool (run with --cinematic; Scripts/Photos/Cinematic): the list of shots and its
// rules (between 14 and 20, each with a lens that fits its kind, a name of its own that any disk takes, and nothing touched
// that the hold does not give back), the picker (a shot without its subject is left out with a reason, a twin is not taken
// twice in one light), where the camera stands for each kind of shot and how it is pulled in along its line when something
// is in the way, the busiest spot and the street's run, the four lights' hours, the depth of field, and the sources: the run
// gives everything back in a finally, refuses a Going Public session, never writes the game's clock or its time scale, and
// costs nothing while no run is on.
internal static partial class Program
{
    private static readonly string[] CineKeys =
    {
        "reallife_cine_test_set", "reallife_cine_test_hour", "reallife_cine_armed", "reallife_cine_running", "reallife_cine_refuse_session", "reallife_cine_line",
        "reallife_cine_done", "reallife_cine_stopped", "reallife_cine_none", "reallife_cine_failed"
    };

    private static void Cinematic()
    {
        CinematicListChecks();
        CinematicPickChecks();
        CinematicCameraChecks();
        CinematicFindChecks();
        CinematicLightChecks();
        CinematicSourceChecks();
        CinematicTextChecks();
    }

    private static CineBox CineCar(float fx = 0f, float fz = 1f)
    {
        return new CineBox { X = 100f, Y = 10f, Z = -40f, Fx = fx, Fz = fz, HalfLength = 2.3f, HalfWidth = 0.9f, Height = 1.45f };
    }

    private static float CineFlat(float ax, float az, float bx, float bz)
    {
        return (float)Math.Sqrt((ax - bx) * (ax - bx) + (az - bz) * (az - bz));
    }

    // ---------------------------------------------------------------- the list

    private static void CinematicListChecks()
    {
        CineShot[] shots = CineRules.Shots;
        Check(shots.Length >= 14 && shots.Length <= 20, "cinematic list: between 14 and 20 shots (" + shots.Length + ")");
        Check(CineRules.Planned(true) == shots.Length && CineRules.Planned(false) >= 14 && CineRules.Planned(false) == shots.Count(s => s.Twin == 0),
            "cinematic list: with the hour left alone every shot but the twins is tried, still 14 or more (" + CineRules.Planned(false) + ")");
        bool numbered = true, lit = true, lensed = true, restored = true;
        string badLens = "none";
        for (int i = 0; i < shots.Length; i++)
        {
            numbered &= shots[i].Number == i + 1;
            lit &= i == 0 || shots[i].Light >= shots[i - 1].Light;
            if (!CineRules.LensFits(shots[i].Style, shots[i].Fov))
            {
                lensed = false;
                badLens = shots[i].Slug;
            }
            restored &= CineHold.Restores(shots[i].Touches(true)) && CineHold.Restores(shots[i].Touches(false))
                        && (shots[i].Touches(true) & (1 << (int)CinePart.Light)) != 0 && (shots[i].Touches(false) & (1 << (int)CinePart.Light)) == 0;
        }
        Check(numbered, "cinematic list: the shots are numbered from 1 in the order they are taken");
        Check(lit, "cinematic list: the shots are taken light by light, so the light changes three times at the most");
        Check(Enumerable.Range(0, (int)CineLight.Count).All(l => shots.Count(s => (int)s.Light == l) >= 3), "cinematic list: every light has three shots or more");
        Check(lensed, "cinematic list: every shot's lens fits its kind of shot (not: " + badLens + ")");
        Check(shots.Where(s => s.Style == CineStyle.Compression).All(s => s.Fov >= 20f && s.Fov <= 35f) && shots.Any(s => s.Style == CineStyle.Compression)
              && shots.Where(s => s.Style == CineStyle.Establishing).All(s => s.Fov > 50f && s.Fov <= 70f) && shots.Any(s => s.Style == CineStyle.Establishing),
            "cinematic list: a street is drawn close with a long lens of 20 to 35 degrees, and a roof's view is wide");
        Check(CineRules.LensOf(20f) == CineLens.Long && CineRules.LensOf(35f) == CineLens.Long && CineRules.LensOf(36f) == CineLens.Normal && CineRules.LensOf(50f) == CineLens.Normal
              && CineRules.LensOf(51f) == CineLens.Wide,
            "cinematic list: a lens is long to 35 degrees, normal to 50 and wide above");
        Check(!CineRules.LensFits(CineStyle.Compression, 50f) && !CineRules.LensFits(CineStyle.Establishing, 30f) && !CineRules.LensFits(CineStyle.Thirds, 19f)
              && !CineRules.LensFits(CineStyle.LowWide, 75f) && !CineRules.LensFits(CineStyle.ThreeQuarter, 60f) && !CineRules.LensFits(CineStyle.Symmetry, float.NaN),
            "cinematic list: a lens that does not fit its kind of shot, or is outside 20 to 70 degrees, is found");
        Check(restored, "cinematic list: every shot touches only what the hold gives back, and the light only when the whole set is taken");
        Check(!CineHold.Restores(0) && !CineHold.Restores(1 << 30) && !CineHold.Restores((1 << (int)CinePart.Place) | (1 << 29)),
            "cinematic list: a shot that touched nothing, or something the hold does not give back, is found");

        var names = new HashSet<string>(StringComparer.Ordinal);
        bool safe = true, unique = true;
        foreach (CineShot shot in shots)
        {
            foreach (bool all in new[] { true, false })
            {
                string name = CineRules.FileName(shot, all);
                safe &= CineRules.FileSafe(name) && CineRules.FileSafe(shot.Slug) && name.StartsWith(shot.Number.ToString("00"), StringComparison.Ordinal)
                        && name.Contains(all ? CineRules.LightWord(shot.Light) : CineRules.NowWord);
                unique &= names.Add(name);
            }
        }
        Check(safe, "cinematic list: every picture's name is small letters, digits and underscores, begins with its number and names its light");
        Check(unique && shots.Select(s => s.Slug).Distinct(StringComparer.Ordinal).Count() == shots.Length, "cinematic list: no two pictures share a name");
        Check(CineRules.FileName(shots[7], true) == "08_golden_avenue_long" && CineRules.FileName(shots[7], false) == "08_now_avenue_long" && shots[14].Slug == "avenue_lights"
              && CineRules.FileName(shots[14], true) == "15_night_avenue_lights",
            "cinematic list: a picture is named by its number, its light and what it is of (08_golden_avenue_long)");
        Check(!CineRules.FileSafe("Night Avenue") && !CineRules.FileSafe("a/b") && !CineRules.FileSafe("a.b") && !CineRules.FileSafe(string.Empty) && !CineRules.FileSafe(new string('a', 49))
              && !CineRules.FileSafe("café"),
            "cinematic list: a name with a space, a slash, a dot, a capital or an accent, or too long, is not a safe one");
        Check(CineRules.RunFolder(new DateTime(2026, 11, 12, 21, 4, 33)) == "2026-11-12_21-04-33", "cinematic list: a run's folder is the computer's date and time, sorted by its name");
        Check(shots.Where(s => s.Twin > 0).All(s => s.Twin < s.Number && shots[s.Twin - 1].Subject == s.Subject && shots[s.Twin - 1].Style == s.Style && shots[s.Twin - 1].Light != s.Light)
              && shots.Count(s => s.Twin > 0) >= 1,
            "cinematic list: a twin repeats an earlier shot of the same subject and kind in another light");
        Check(Enumerable.Range(0, (int)CineSubject.Count).All(s => CineRules.SubjectWord((CineSubject)s) != "nothing" && shots.Any(x => (int)x.Subject == s))
              && Enum.GetValues(typeof(CineStyle)).Cast<CineStyle>().All(s => CineRules.StyleWord(s) != "plain" && shots.Any(x => x.Style == s)),
            "cinematic list: every subject and every kind of shot has its words for the list and a shot that uses it");
        Check(CineRules.Width == 3840 && CineRules.Height == 2160 && CineRules.JpgQuality == 92 && Math.Abs(CineRules.Aspect - 16f / 9f) < 0.0001f,
            "cinematic list: a picture is 3840 by 2160, and its JPG is at quality 92");

        // The hold: a part is written once and given back once, in an order that moves nothing before the camera is back.
        var hold = new CineHold();
        var into = new float[CineHold.Values];
        bool first = hold.Take(CinePart.Lens, 60f, 0.3f, 1000f), second = hold.Take(CinePart.Lens, 24f, 0.1f, 2500f);
        bool gave = hold.Give(CinePart.Lens, into), again = hold.Give(CinePart.Lens, into);
        Check(first && !second && gave && !again && into[0] == 60f && into[1] == 0.3f && into[2] == 1000f && !hold.Any,
            "cinematic hold: a part is written once, so a later shot never overwrites what the game had, and given back once");
        int At(CinePart part) => Array.IndexOf(CineHold.GiveOrder, part);
        Check(CineHold.GiveOrder.Length == (int)CinePart.Count && CineHold.GiveOrder.Distinct().Count() == (int)CinePart.Count,
            "cinematic hold: every part is given back, each once");
        Check(At(CinePart.Target) < At(CinePart.Lens) && At(CinePart.Lens) < At(CinePart.Brain) && At(CinePart.Place) < At(CinePart.Brain) && At(CinePart.Brain) < At(CinePart.Speed)
              && At(CinePart.Hud) < At(CinePart.Speed) && At(CinePart.Light) < At(CinePart.Speed) && At(CinePart.Speed) < At(CinePart.Walk) && At(CinePart.Walk) == (int)CinePart.Count - 1,
            "cinematic hold: the camera, the light and the interface are back before the game's time runs again, and the player's walk is given back last");
    }

    // ---------------------------------------------------------------- the picker

    private static void CinematicPickChecks()
    {
        CineShot[] shots = CineRules.Shots;
        var found = new bool[(int)CineSubject.Count];
        var why = new string[(int)CineSubject.Count];
        var picks = new CinePick[shots.Length];
        for (int i = 0; i < found.Length; i++)
        {
            found[i] = true;
        }
        Check(CineRules.Pick(true, found, why, picks) == shots.Length && picks.All(p => p.Take && p.Why == null), "cinematic picker: with every subject there, the whole set is taken");
        int hourOnly = CineRules.Pick(false, found, why, picks);
        Check(hourOnly == CineRules.Planned(false) && picks.Where(p => !p.Take).All(p => shots[p.Index].Twin > 0 && p.Why.Contains(shots[p.Index].Twin.ToString("00")))
              && picks.Count(p => !p.Take) == shots.Count(s => s.Twin > 0),
            "cinematic picker: with the hour left alone a twin is not taken a second time in the same light, and says which shot it repeats");

        found[(int)CineSubject.Bus] = false;
        why[(int)CineSubject.Bus] = "no bus on the street within 140 m";
        found[(int)CineSubject.Incident] = false;
        int taken = CineRules.Pick(true, found, why, picks);
        int without = shots.Count(s => s.Subject == CineSubject.Bus || s.Subject == CineSubject.Incident);
        Check(without >= 2 && taken == shots.Length - without
              && picks.All(p => p.Take == (shots[p.Index].Subject != CineSubject.Bus && shots[p.Index].Subject != CineSubject.Incident)),
            "cinematic picker: a shot whose subject is not found is left out, and every other shot is still taken");
        Check(picks.Where(p => shots[p.Index].Subject == CineSubject.Bus).All(p => p.Why == "no bus on the street within 140 m")
              && picks.Where(p => shots[p.Index].Subject == CineSubject.Incident).All(p => p.Why != null && p.Why.Contains("an incident")),
            "cinematic picker: a shot left out says why: the look's own reason, or that none was found");
        Check(CineRules.Pick(true, new bool[(int)CineSubject.Count], null, picks) == 0 && picks.All(p => !p.Take && !string.IsNullOrEmpty(p.Why))
              && CineRules.Pick(true, null, null, picks) == 0,
            "cinematic picker: with nothing found nothing is taken, each shot with its reason");
        Check(CineRules.Take(shots[0], true, true, null, out string none) && none == null && !CineRules.Take(shots[0], true, false, "why not", out string said) && said == "why not",
            "cinematic picker: one shot is decided the same way the whole list is");
    }

    // ---------------------------------------------------------------- where the camera stands

    private static void CinematicCameraChecks()
    {
        CineBox car = CineCar();
        float radius = (float)Math.Sqrt(car.HalfLength * car.HalfLength + car.HalfWidth * car.HalfWidth);

        CineRules.Frame(CineStyle.ThreeQuarter, car, 35f, 1, out CinePose front);
        CineRules.Frame(CineStyle.ThreeQuarter, car, 35f, -1, out CinePose frontLeft);
        float frontAway = CineFlat(front.EyeX, front.EyeZ, car.X, car.Z);
        Check(front.EyeZ > car.Z && front.EyeX > car.X && frontLeft.EyeX < car.X && Math.Abs(frontLeft.EyeZ - front.EyeZ) < 0.001f && Math.Abs((front.EyeX - car.X) + (frontLeft.EyeX - car.X)) < 0.001f,
            "cinematic camera: three quarters from the front stands ahead of the car and to the side asked, the other side its mirror");
        Check(frontAway >= radius + 2.5f && radius / frontAway <= CineRules.HalfWidthTan(35f) * 0.7f && front.EyeY - car.Y >= 0.75f && front.EyeY - car.Y <= 1.7f
              && front.AimY > car.Y && front.AimY < car.Y + car.Height,
            "cinematic camera: the car fits the frame's width with air round it, seen from below a standing eye, aimed at its body");
        CineRules.Frame(CineStyle.RearQuarter, car, 35f, 1, out CinePose rear);
        Check(rear.EyeZ < car.Z && rear.EyeX > car.X && rear.EyeY < front.EyeY + 0.001f, "cinematic camera: three quarters from behind stands behind the car, as low or lower");

        CineRules.Frame(CineStyle.WheelHeight, car, 56f, 1, out CinePose wheel);
        Check(Math.Abs(wheel.EyeY - car.Y - 0.32f) < 0.001f && wheel.EyeX - car.X > car.HalfWidth && wheel.EyeZ - car.Z > car.HalfLength
              && Math.Abs(wheel.FocusX - car.X - car.HalfWidth) < 0.001f && wheel.FocusZ > car.Z && wheel.FocusY < car.Y + 0.6f && wheel.AimZ < wheel.EyeZ,
            "cinematic camera: the wheel height shot stands beside the car's nose a hand over the road, looks back along its flank and keeps the near wheel sharp");
        float wheelLine = (float)Math.Sqrt(Math.Pow(wheel.EyeX - wheel.AimX, 2) + Math.Pow(wheel.EyeY - wheel.AimY, 2) + Math.Pow(wheel.EyeZ - wheel.AimZ, 2));
        Check(Math.Abs(wheelLine - wheel.Reach - CineRules.WheelClear) < 0.001f && wheel.Reach > car.HalfWidth && CineRules.WheelClear < 1.7f && CineRules.WheelClear > CineRules.Margin,
            "cinematic camera: its line is tested only over the last stretch before the lens, which lies outside the car: the car's own body is not what is in its way");

        var person = new CineBox { X = 5f, Y = 2f, Z = 5f, Fx = 0f, Fz = 1f, HalfLength = 0.3f, HalfWidth = 0.3f, Height = 1.8f };
        CineRules.Frame(CineStyle.OverShoulder, person, 40f, 1, out CinePose shoulder);
        Check(shoulder.EyeZ < person.Z && shoulder.EyeX > person.X && shoulder.EyeY < person.Y + person.Height && shoulder.EyeY > person.Y + person.Height * 0.8f
              && shoulder.AimZ > person.Z + 10f && shoulder.NearOnly && !shoulder.Free && Math.Abs(shoulder.AnchorX - person.X) < 0.001f && shoulder.AnchorY > person.Y + 1.5f,
            "cinematic camera: over the shoulder stands a step behind the player at shoulder height, looks where they look, and its line is tested from their head");

        CineRules.Frame(CineStyle.Thirds, person, 30f, 1, out CinePose thirds);
        CineRules.Frame(CineStyle.Thirds, person, 30f, -1, out CinePose thirdsLeft);
        float thirdsAway = CineFlat(thirds.EyeX, thirds.EyeZ, person.X, person.Z);
        double fills = person.Height / (2.0 * thirdsAway * Math.Tan(15.0 * Math.PI / 180.0));
        double across = 0.5 - Math.Tan(CineRules.ThirdsTurn(30f) * Math.PI / 180.0) / (2.0 * CineRules.HalfWidthTan(30f));
        Check(Math.Abs(across - 1.0 / 3.0) < 0.001 && Math.Abs(thirds.Turn - CineRules.ThirdsTurn(30f)) < 0.001f && Math.Abs(thirdsLeft.Turn + thirds.Turn) < 0.001f,
            "cinematic camera: the turn after aiming puts a person on a third of the frame, left or right by the side");
        Check(fills > 0.45 && fills < 0.62 && thirds.EyeZ > person.Z && thirds.EyeY - person.Y <= 1.5f && thirds.AimY > person.Y + 0.8f,
            "cinematic camera: a person fills about half the frame's height, seen from in front at eye height or below (" + fills.ToString("0.00") + ")");

        var street = new CineBox { X = 0f, Y = 0f, Z = 0f, Fx = 1f, Fz = 0f, HalfWidth = 9f };
        CineRules.Frame(CineStyle.Symmetry, street, 42f, 1, out CinePose symmetry);
        CineRules.Frame(CineStyle.Symmetry, street, 42f, -1, out CinePose symmetryToo);
        CineRules.Frame(CineStyle.Compression, street, 24f, 1, out CinePose longLens);
        Check(symmetry.Free && Math.Abs(symmetry.EyeZ) < 0.001f && Math.Abs(symmetry.AimZ) < 0.001f && symmetry.AimX > 100f && Math.Abs(symmetry.EyeY - 1.55f) < 0.001f
              && symmetry.EyeX == symmetryToo.EyeX && symmetry.EyeZ == symmetryToo.EyeZ && symmetry.Turn == 0f,
            "cinematic camera: the symmetrical shot stands in the middle of the street at eye height and looks straight down it, whichever side is asked");
        Check(longLens.Free && Math.Abs(longLens.EyeZ) < 0.001f && Math.Abs(longLens.AimZ) < 0.001f && longLens.AimX > 200f && longLens.EyeY > 2f,
            "cinematic camera: the long lens looks down the street's middle from over the cars");

        var tower = new CineBox { X = 0f, Y = 0f, Z = 0f, Fx = 0f, Fz = 1f, HalfLength = 12f, HalfWidth = 15f, Height = 60f };
        CineRules.Frame(CineStyle.LowWide, tower, 60f, 1, out CinePose low);
        float lowAway = CineFlat(low.EyeX, low.EyeZ, 0f, 0f);
        double pitch = Math.Atan2(low.AimY - low.EyeY, lowAway) * 180.0 / Math.PI;
        Check(low.EyeZ > tower.HalfLength + 5f && lowAway >= 10f && lowAway <= 85f && pitch > 5.0 && pitch < 35.0 && Math.Abs(low.EyeY - 1.05f) < 0.001f,
            "cinematic camera: a tall thing is seen from the ground in front of it, far enough off to look up no steeper than 35 degrees (" + pitch.ToString("0") + ")");
        var hut = new CineBox { X = 0f, Y = 0f, Z = 0f, Fx = 0f, Fz = 1f, HalfLength = 2f, HalfWidth = 2f, Height = 3f };
        CineRules.Frame(CineStyle.LowWide, hut, 60f, 1, out CinePose lowHut);
        Check(CineFlat(lowHut.EyeX, lowHut.EyeZ, 0f, 0f) >= 10f, "cinematic camera: a small thing is still seen from ten metres or more");

        var shop = new CineBox { X = 0f, Y = 0f, Z = 0f, Fx = 0f, Fz = 1f, HalfLength = 0f, HalfWidth = 7f, Height = 6f };
        CineRules.Frame(CineStyle.Facade, shop, 40f, 1, out CinePose facade);
        Check(Math.Abs(facade.EyeX) < 0.001f && facade.EyeZ >= 6f && facade.EyeZ * CineRules.HalfWidthTan(40f) >= shop.HalfWidth && Math.Abs(facade.AimX) < 0.001f && facade.Turn == 0f,
            "cinematic camera: a front is seen square on from its own normal, the whole width asked inside the frame");

        // The same shot of a subject that faces another way is the same picture turned with it.
        CineRules.Frame(CineStyle.ThreeQuarter, CineCar(1f, 0f), 35f, 1, out CinePose turned);
        Check(Math.Abs(CineFlat(turned.EyeX, turned.EyeZ, car.X, car.Z) - frontAway) < 0.001f && turned.EyeX > car.X && turned.EyeZ < car.Z && Math.Abs(turned.EyeY - front.EyeY) < 0.001f,
            "cinematic camera: a subject facing another way is shot from the same distance and height, the camera turned with it");
        CineRules.Frame(CineStyle.ThreeQuarter, CineCar(0f, -1f), 35f, 1, out CinePose flipped);
        Check(Math.Abs((flipped.EyeX - car.X) + (front.EyeX - car.X)) < 0.001f && Math.Abs((flipped.EyeZ - car.Z) + (front.EyeZ - car.Z)) < 0.001f,
            "cinematic camera: turned round, the camera stands at the opposite point about the subject");

        // The roof.
        var city = new CineBox { X = 0f, Y = 5f, Z = 0f, Fx = 0f, Fz = 1f };
        CineRules.Establish(city, 45f, 30f, 28f, out CinePose roof);
        Check(roof.Free && roof.EyeZ < 0f && Math.Abs(roof.EyeZ + 30f) < 0.001f && Math.Abs(roof.EyeX - 28f) < 0.001f && Math.Abs(roof.EyeY - 45f - CineRules.EyeOverRoof) < 0.001f
              && roof.AimZ > 200f && roof.AimY < roof.EyeY && roof.AimY > city.Y,
            "cinematic camera: the roof's shot stands a tripod's height over the roof behind the subject and looks over it, a little down");
        var stands = new HashSet<string>();
        bool standsOk = true;
        for (int i = 0; i < CineRules.Stands; i++)
        {
            CineRules.Stand(i, out float back, out float sideways);
            standsOk &= back >= 30f && back <= 100f && Math.Abs(sideways) <= 40f && stands.Add(back + "/" + sideways);
        }
        Check(standsOk && CineRules.Stands == 8, "cinematic camera: eight different places behind the subject are tried for a roof, none more than 100 m back");
        Check(CineRules.RoofWorth(5f) == 0f && CineRules.RoofWorth(11.9f) == 0f && CineRules.RoofWorth(12f) > 0f && CineRules.RoofWorth(30f) > CineRules.RoofWorth(15f)
              && CineRules.RoofWorth(90f) == CineRules.RoofWorth(60f) && CineRules.RoofWorth(131f) == 0f && CineRules.RoofWorth(float.NaN) == 0f,
            "cinematic camera: a roof under 12 m or over 130 m is no place to stand, and one over 60 m is no better than 60");

        // The camera pulled in along its line.
        Check(CineRules.Pull(10f, -1f, 0.6f, 0.55f, out float clear) && clear == 10f && CineRules.Pull(10f, 30f, 0.6f, 0.55f, out float beyond) && beyond == 10f,
            "cinematic clearance: with nothing on its line, or something beyond it, the camera stands where it was wanted");
        Check(CineRules.Pull(10f, 7f, 0.6f, 0.55f, out float pulled) && Math.Abs(pulled - 6.4f) < 0.001f && CineRules.Pull(10f, 10.2f, 0.6f, 0.55f, out float shy) && Math.Abs(shy - 9.6f) < 0.001f,
            "cinematic clearance: a wall on the line pulls the camera in to stand 0.6 m before it, also one just behind where it would stand");
        Check(!CineRules.Pull(10f, 6f, 0.6f, 0.55f, out float near) && near < 5.5f && !CineRules.Pull(10f, 0.2f, 0.6f, 0.55f, out _) && !CineRules.Pull(0f, -1f, 0.6f, 0.55f, out _)
              && !CineRules.Pull(float.NaN, -1f, 0.6f, 0.55f, out _),
            "cinematic clearance: pulled in past its share of the distance the place is given up, as is a line of no length");
        bool never = true;
        for (float hit = 0f; hit <= 14f; hit += 0.25f)
        {
            bool ok = CineRules.Pull(10f, hit, CineRules.Margin, 0.55f, out float at);
            never &= at <= 10f && (!ok || at <= hit - CineRules.Margin + 0.0001f || hit >= 10f + CineRules.Margin) && (!ok || at >= 5.5f - 0.0001f);
        }
        Check(never, "cinematic clearance: wherever the wall is, a camera that stands is never past it, never nearer it than the margin, and never beyond where it was wanted");
        Check(CineRules.LeastShare(CineStyle.OverShoulder) > CineRules.LeastShare(CineStyle.ThreeQuarter) && CineRules.LeastShare(CineStyle.ThreeQuarter) >= 0.5f
              && CineRules.LensRadius > 0.1f && CineRules.Margin > CineRules.LensRadius && CineRules.Slides * CineRules.SlideStep >= 5f && CineRules.LeastHeight > 0.1f,
            "cinematic clearance: a close shot gives up sooner than a far one, the margin is wider than the lens, and a place not clear is moved along its line for 5 m or more");
        Check(CineRules.Sees(-1f, 20f, 2f) && CineRules.Sees(18.5f, 20f, 2f) && !CineRules.Sees(9f, 20f, 2f),
            "cinematic clearance: the subject is in sight with nothing on the line or only its own body, and not behind a wall half way");
        Check(CineRules.InFrame(0.5f, 0.5f, 10f) && CineRules.InFrame(0.34f, 0.6f, 4f) && !CineRules.InFrame(0.5f, 0.5f, -3f) && !CineRules.InFrame(1.2f, 0.5f, 10f)
              && !CineRules.InFrame(0.5f, 0.01f, 10f) && !CineRules.InFrame(float.NaN, 0.5f, 10f),
            "cinematic clearance: a subject behind the lens or off the frame's edge is not in the picture");
        var tries = new HashSet<string>();
        for (int i = 0; i < CineRules.Tries; i++)
        {
            CineRules.Try(i, out int side, out bool flip);
            tries.Add(side + "/" + flip);
        }
        CineRules.Try(0, out int firstSide, out bool firstFlip);
        Check(tries.Count == 4 && CineRules.Tries == 4 && firstSide == 1 && !firstFlip && !CineRules.CanFlip(CineStyle.Facade) && !CineRules.CanFlip(CineStyle.OverShoulder)
              && !CineRules.CanFlip(CineStyle.Establishing) && CineRules.CanFlip(CineStyle.ThreeQuarter) && CineRules.CanFlip(CineStyle.Compression),
            "cinematic clearance: both sides are tried, then both from the other end; a front and the player's shoulder are never shot from behind");
    }

    // ---------------------------------------------------------------- what is found

    private static void CinematicFindChecks()
    {
        // Twelve walkers: four strung out, and eight in a knot at (50, 50).
        var xs = new float[CineRules.ClusterMost + 40];
        var zs = new float[CineRules.ClusterMost + 40];
        int n = 0;
        for (int i = 0; i < 4; i++)
        {
            xs[n] = i * 30f;
            zs[n++] = 0f;
        }
        for (int i = 0; i < 8; i++)
        {
            xs[n] = 50f + (i % 3) * 2f;
            zs[n++] = 50f + (i / 3) * 2f;
        }
        Check(CineRules.Densest(xs, zs, n, CineRules.WalkerRadius, CineRules.WalkersLeast, out int centre, out int size) && size == 8 && centre >= 4,
            "cinematic subjects: the busy pavement is where the most walkers are within a few metres of each other, at one of them");
        Check(!CineRules.Densest(xs, zs, 4, CineRules.WalkerRadius, CineRules.WalkersLeast, out _, out int alone) && alone == 1 && !CineRules.Densest(xs, zs, 0, 9f, 3, out int nobody, out _) && nobody == -1
              && !CineRules.Densest(null, zs, 5, 9f, 3, out _, out _),
            "cinematic subjects: walkers strung out along a street are no busy pavement, and nobody is nobody");
        for (int i = 0; i < xs.Length; i++)
        {
            xs[i] = i < CineRules.ClusterMost ? i * 100f : 0f;   // the points past the most looked at are all in one place
            zs[i] = 0f;
        }
        Check(!CineRules.Densest(xs, zs, xs.Length, 9f, 3, out _, out int capped) && capped == 1, "cinematic subjects: no more than " + CineRules.ClusterMost + " points are looked at on a press");

        var runs = new float[CineRules.Bearings];
        for (int i = 0; i < runs.Length; i++)
        {
            runs[i] = 12f;
        }
        Check(!CineRules.Avenue(runs, runs.Length, CineRules.AvenueLeast, out int no) && no == -1, "cinematic subjects: in a yard with walls 12 m off all round there is no street to look down");
        runs[3] = 180f;
        runs[15] = 60f;
        Check(CineRules.Avenue(runs, runs.Length, CineRules.AvenueLeast, out int street) && street == 3, "cinematic subjects: the street is looked down the way it runs clear for longest");
        runs[9] = 180f;
        runs[21] = 150f;
        Check(CineRules.Avenue(runs, runs.Length, CineRules.AvenueLeast, out int middle) && middle == 9,
            "cinematic subjects: of two runs as long, the one with more street behind the camera is taken, so the middle of a street beats its end");
        runs[5] = 900f;
        bool longer = CineRules.Avenue(runs, runs.Length, CineRules.AvenueLeast, out int far) && far == 5;
        runs[9] = 400f;
        Check(longer && CineRules.Avenue(runs, runs.Length, CineRules.AvenueLeast, out int capped2) && capped2 == 9,
            "cinematic subjects: a run is worth no more than the " + CineRules.AvenueMost.ToString("0") + " m it is measured to, so street behind the camera still tells two long runs apart");
        Check(CineRules.Reach < 200f && CineRules.Reach >= 100f, "cinematic subjects: a subject is looked for within the 200 m of the player the game draws its people, cars and lamps to");
    }

    // ---------------------------------------------------------------- the light and the focus

    private static void CinematicLightChecks()
    {
        Check(Math.Abs(CineRules.Elevation(0f)) < 0.01f && Math.Abs(CineRules.Elevation(90f) - 90f) < 0.01f && Math.Abs(CineRules.Elevation(135f) - 45f) < 0.01f
              && Math.Abs(CineRules.Elevation(180f)) < 0.01f && CineRules.Elevation(200f) < 0f && Math.Abs(CineRules.Elevation(30f) - 30f) < 0.01f,
            "cinematic light: the sun's height is read from the turn the game gives its light: overhead at 90, down again past it, under the horizon past 180");
        var hours = new float[(int)CineLight.Count];
        CineRules.Hours(20f, 19f, null, hours);
        Check(hours[0] == 12.5f && Math.Abs(hours[1] - 19f) < 0.001f && Math.Abs(hours[2] - 20.35f) < 0.001f && hours[3] == 23f,
            "cinematic light: with no curve to read, midday is 12:30, the golden hour an hour before the sun goes out, the blue hour a little after, night 23:00");
        // A sun overhead at noon that reaches the horizon at 20:00: 8 degrees up at about 19:17.
        Func<float, float> sun = hour => 90f + (hour - 12f) * 11.25f;
        CineRules.Hours(20f, 19f, sun, hours);
        Check(hours[1] > 19.1f && hours[1] < 19.4f && CineRules.Elevation(sun(hours[1])) >= CineRules.GoldenSun - 0.01f && CineRules.Elevation(sun(hours[1])) < CineRules.GoldenSun + 2f,
            "cinematic light: the golden hour is the last at which the game's own sun still stands 8 degrees up (" + CineRules.Clock(hours[1]) + ")");
        // A sun the game keeps high until it puts it out: the last light there is.
        CineRules.Hours(20f, 21f, hour => 60f, hours);
        Check(Math.Abs(hours[1] - 19.8f) < 0.001f && Math.Abs(hours[2] - 21.35f) < 0.001f, "cinematic light: the blue hour waits for both the sun to be out and the lamps to be on");
        bool ordered = true;
        foreach (float sunOff in new[] { 14f, 17f, 20f, 22f, 23.5f, 26f })
        {
            foreach (float lamps in new[] { 5f, 18f, 21f, 23.9f })
            {
                CineRules.Hours(sunOff, lamps, sunOff > 21f ? sun : null, hours);
                for (int i = 0; i < hours.Length; i++)
                {
                    ordered &= hours[i] >= CineRules.HourLeast && hours[i] <= CineRules.HourMost && (i == 0 || hours[i] >= hours[i - 1]);
                }
            }
        }
        Check(ordered && CineRules.HourLeast >= 5f && CineRules.HourMost < 24f,
            "cinematic light: whatever the game's day, the four hours rise and stay within what its own time of day tool shows, 5:00 to 24:00, so no midnight is ever shown");
        Check(CineRules.Clock(18.7f) == "18:42" && CineRules.Clock(12.5f) == "12:30" && CineRules.Clock(23f) == "23:00" && CineRules.Clock(0f) == "00:00",
            "cinematic light: an hour is written as a clock's time");

        foreach (float distance in new[] { 0.9f, 3f, 6.1f, 28f })
        {
            CineRules.Focus(distance, false, out float nearStart, out float nearEnd, out float farStart, out float farEnd);
            Check(nearStart < nearEnd && nearEnd < distance && distance < farStart && farStart < farEnd, "cinematic focus: the subject " + distance + " m off lies in the sharp part, soft before and behind it");
        }
        CineRules.Focus(16f, true, out float closeStart, out float closeEnd, out float awayStart, out float awayEnd);
        Check(closeStart < 1.2f && closeEnd > 1.2f && closeEnd < 16f && awayStart > 1000f && awayEnd > awayStart,
            "cinematic focus: over the shoulder only what is close to the lens is soft, the street beyond sharp to the end");
        Check(CineRules.Vignette > 0f && CineRules.Vignette <= 0.25f, "cinematic focus: the vignette is slight (" + CineRules.Vignette + ")");
        Check(CineRules.FocalLength(24f) == 56 && CineRules.FocalLength(62f) == 20 && CineRules.FocalLength(35f) == 38 && CineRules.LensWords(24f) == "24 degrees, about 56 mm (long)",
            "cinematic focus: a lens is written by its field of view and the focal length a photographer knows it by");
        Check(CineRules.Flat(0, 0) && CineRules.Flat(128, 131) && !CineRules.Flat(0, 200) && !CineRules.Flat(2, 9),
            "cinematic picture: a frame of one flat colour, black or not, is told from a dark street with its lamps");
        Check(CineRules.SettleFrames >= 8 && CineRules.TargetFrames >= 8 && CineRules.LightFrames > CineRules.SettleFrames && CineRules.FirstSeconds >= 0.8f,
            "cinematic picture: frames are drawn at a new place, in a new light and at the picture's size before it is read");
    }

    // ---------------------------------------------------------------- the sources

    private static void CinematicSourceChecks()
    {
        string director = ModSource(@"Photos\Cinematic\CineDirector.cs"), tests = ModSource(@"Photos\Cinematic\CineDirector.Tests.cs");
        string game = ModSource(@"Photos\Cinematic\CineGame.cs"), subjects = ModSource(@"Photos\Cinematic\CineSubjects.cs");
        Check(director.Length > 0 && tests.Length > 0 && game.Length > 0 && subjects.Length > 0, "cinematic sources: the tool's files are where the checks look for them");
        string all = director + tests + game + subjects;

        string run = Block(director, "private IEnumerator Run(bool allLights)");
        Check(InOrder(run, "try", "Begin(allLights);", "_finished = true;", "finally", "End();") && Regex.IsMatch(run, @"finally\s*\{\s*End\(\);\s*\}\s*\}\s*$")
              && Occurs(run, "catch") == 0,
            "cinematic sources: the run's whole work is in a try whose finally is the end that gives everything back");
        string end = Block(director, "private void End()");
        Check(InOrder(end, "if (!Active && !_hold.Any)", "return;", "Active = false;", "enabled = false;", "GiveBack();", "Say("),
            "cinematic sources: the end gives everything back before it tells anybody, switches the component off, and does nothing a second time");
        string give = Block(director, "private void GiveBack()"), put = Block(director, "private void Put(CinePart part, float[] v)");
        Check(give.Contains("CineHold.GiveOrder") && InOrder(give, "_hold.Give(order[i], _back)", "try", "Put(order[i], _back);", "catch (Exception e)"),
            "cinematic sources: each part is given back in a try of its own, so one that fails costs only itself");
        var missing = new List<string>();
        foreach (CinePart part in Enum.GetValues(typeof(CinePart)))
        {
            if (part != CinePart.Count && !put.Contains("case CinePart." + part + ":"))
            {
                missing.Add(part.ToString());
            }
        }
        Check(missing.Count == 0, "cinematic sources: every part the hold names has its way back (none without: " + string.Join(", ", missing) + ")");
        string hold = Block(director, "private void Hold()");
        Check(InOrder(hold, "_hold.Take(CinePart.Walk)", "CineGame.HoldWalk(true);", "CineGame.TimeLocked(", "_hold.Take(CinePart.Speed", "CineGame.Pause();", "_hold.Take(CinePart.Pointer", "Cursor.visible = false;",
                  "_hold.Take(CinePart.Hud", "PhotosGame.Hud(false);", "_hold.Take(CinePart.Brain", "_brain.enabled = false;", "_hold.Take(CinePart.Place", "_hold.Take(CinePart.Lens",
                  "_camera.nearClipPlane = NearPlane;", "_hold.Take(CinePart.Layers", "_camera.cullingMask &= ~CineGame.Markers;", "_hold.Take(CinePart.Look)", "CineGame.MakeLook(",
                  "_hold.Take(CinePart.Line)", "new CineLine("),
            "cinematic sources: each part is written in the hold before it is changed: the walk, the pause, the pointer, the interface, the brain, the camera, the lens, its layers, the look, the line");
        Check(game.Contains("public static int Markers => Bit(LayerHelper.UiLayerIndex) | LayerHelper.guidersLayerMask.value;")
              && Block(director, "private void Put(CinePart part, float[] v)").Contains("_camera.cullingMask = CameraHold.Join(v[0], v[1]);"),
            "cinematic sources: the interface's layer, where markers that stand in the world are, and the game's guiders are left out of what the camera draws");
        string light = Block(director, "private bool Light(CineLight light)");
        Check(InOrder(light, "if (!_hold.Held(CinePart.Light))", "CineGame.ReadLight(", "_hold.Take(CinePart.Light", "CineGame.SetLight("),
            "cinematic sources: the game's time of day tool is written in the hold the first time the light is touched");
        Check(Regex.IsMatch(director, @"private void OnDestroy\(\)\s*\{\s*try\s*\{\s*Stop\(") && InOrder(Block(director, "private void OnDisable()"), "!gameObject.activeInHierarchy", "Stop(")
              && InOrder(Block(director, "private void Stop(string why, bool fault)"), "StopAllCoroutines();", "End();"),
            "cinematic sources: the city unloading and the object switched off, which stop a coroutine with no finally run, give everything back themselves");

        // A Going Public session: refused at the press, as the menu closes, and ended if one begins.
        string ask = Block(tests, "private void Ask(bool allLights)");
        Check(InOrder(ask, "GoingPublic.InSession", "RefuseSession();", "return;", "StartCoroutine(Guard.Routine(Run(allLights)));")
              && InOrder(Block(director, "private bool MayStart()"), "GoingPublic.InSession", "RefuseSession();", "return false;")
              && Block(tests, "private void RefuseSession()").Contains("\"reallife_cine_refuse_session\"") && Block(game, "public static string MustStop(Camera camera)").Contains("GoingPublic.InSession"),
            "cinematic sources: in a Going Public session the shots are refused with a notice, at the press and as the menu closes, and a run ends if a session begins");
        Check(InOrder(ask, "if (Active || _armed)", "\"reallife_cine_running\"", "return;") && InOrder(ask, "CineGame.Refuse();", "TestRefusals.Tell(Logger, TestName, why);", "return;"),
            "cinematic sources: a second press while a run is on, and a press indoors or with no city, start nothing and say why");

        // The clock: never written, never run. The light is the game's own tool, given back with the clock's own hour.
        Check(!Regex.IsMatch(all, @"Time\.timeScale\s*=[^=]") && !Regex.IsMatch(all, @"\.(Hour|Minute|Day)\s*(=[^=]|\+=|\+\+)") && !all.Contains("timeMachine.Start") && !all.Contains("TimeHelper.Set")
              && !all.Contains("onNewHour") && !all.Contains("onNewDay") && !all.Contains("RainHelper.") && !all.Contains("Weather.Set"),
            "cinematic sources: the tool never writes the game's clock, its time scale or its weather, and raises no hour or day");
        string set = Block(game, "public static bool SetLight(float hour)"), back = Block(game, "public static void GiveLight(bool tool, float forced, float shown)");
        Check(InOrder(set, "day.usingSliderDebugTool = true;", "day.forcedHour = hour;", "InGameTimeOfDayPanel.selectedTimeInHours = hour;", "day.SetEnvironmentSettings(hour);", "day.UpdateHourlyValues(hour);"),
            "cinematic sources: an hour's light is shown the way the game's own time of day panel shows it");
        Check(InOrder(back, "day.forcedHour = forced;", "day.usingSliderDebugTool = tool;", "InGameTimeOfDayPanel.selectedTimeInHours = shown;", "day.SetEnvironmentSettings(HourNow);",
                  "day.UpdateHourlyValues(SaveGameManager.Current.Hour);"),
            "cinematic sources: the tool is left as it was found and the light put back to the clock's own hour");
        Check(InOrder(Block(game, "public static void Pause()"), "speed.SetPause(true, false);", "speed.DisableTimeControl(true);") && Block(game, "public static bool TimeLocked(out bool locked)").Contains("speed.isTimeControlDisabled")
              && InOrder(Block(game, "public static void Resume(bool wasLocked)"), "speed.DisableTimeControl(wasLocked);", "speed.Reset();"),
            "cinematic sources: the game is paused through its own speed control with the pause key locked, and resumed to the speed, or the pause, the player had");
        Check(InOrder(Block(director, "private bool Halt()"), "CineGame.MustStop(_camera);", "_hold.Held(CinePart.Speed)", "CineGame.KeepPaused();")
              && InOrder(Block(game, "public static void KeepPaused()"), "!speed.Paused", "speed.SetPause(true, false);"),
            "cinematic sources: every frame of a run the pause is put back if something of the game's ended it, so time never runs while the camera is away from the player");

        // Nothing while no run is on.
        Check(director.Contains("[DefaultExecutionOrder(-31000)]") && InOrder(Block(director, "private void Awake()"), "enabled = false;")
              && InOrder(Block(director, "private void OnUpdate()"), "if (!Active)", "enabled = false;", "return;")
              && Occurs(director, "Guard.Fail(FrameGuard, e, this);") == 2 && Occurs(director, "if (FrameGuard.Off)") == 2 && Occurs(tests, "if (FrameGuard.Off)") == 1,
            "cinematic sources: the component is off unless a run is on, its two frame methods guarded, and it takes Esc before the game's own key handling");
        Check(!Regex.IsMatch(all, @"FindObjects?(OfType|ByType)|GameObject\.Find\(|SceneFinds\.") && Occurs(subjects, "Physics.Raycast(") <= 5 && !director.Contains("Physics."),
            "cinematic sources: no search of the whole scene; a subject is found in the game's own lists, the mod's map rows and a few rays");

        // The picture.
        string target = Block(game, "public static RenderTexture MakeTarget()"), develop = Block(game, "public static bool Develop(Frame frame, string folder, string name)");
        Check(target.Contains("CineRules.Width, CineRules.Height") && target.Contains("RenderTextureReadWrite.sRGB") && InOrder(develop, "EncodeNativeArrayToPNG(", "EncodeNativeArrayToJPG(", "CineRules.JpgQuality",
                  "name + \".png\"", "name + \".jpg\"", "finally", "Drop(frame);"),
            "cinematic sources: a picture is drawn into a target of its own size and written as a PNG with a JPG beside it, its pixels let go whatever happens");
        Check(InOrder(run, "Target(true);", "CineRules.TargetFrames", "yield return new WaitForEndOfFrame();", "Read(true);", "yield return new WaitForEndOfFrame();", "Read(false);", "Task.Run(() => CineGame.Develop(")
              && InOrder(Block(director, "private CineGame.Frame Read(bool target)"), "finally", "Target(false);"),
            "cinematic sources: the frame is read at the end of a drawn frame, from the target or else from the screen, the target taken off the camera either way, and written on a worker");
        Check(InOrder(Block(game, "public static string Root()"), "Environment.SpecialFolder.MyPictures", "\"Real Life Overhaul\", \"Cinematic\"") && director.Contains("CineRules.RunFolder(now)")
              && Block(game, "public static bool WriteList(string folder, string text)").Contains("\"shots.txt\""),
            "cinematic sources: the pictures go to Pictures/Real Life Overhaul/Cinematic and a folder of the run's own, with shots.txt beside them");
        Check(InOrder(run, "_line?.Hide();", "Target(true);") && InOrder(Block(director, "private void Hold()"), "PhotosGame.Hud(false);"),
            "cinematic sources: the game's interface is hidden for the whole run and the tool's own line is put away before a picture is read");

        // Where it is switched in.
        string guide = ModSource(@"Photos\PhotosGuide.cs"), mod = ModSource(@"Core\RealLifeOverhaulMod.cs");
        Check(guide.Contains(".AddButton(\"reallife_cine_test_set\", () => CineDirector.Instance?.TestSet())") && guide.Contains(".AddButton(\"reallife_cine_test_hour\", () => CineDirector.Instance?.TestHour())")
              && guide.Contains(".Test(\"reallife_cine_test_set\", \"reallife_cine_test_hour\")") && guide.Contains("CineDirector.Instance?.Stop(\"the save tools\");")
              && mod.Contains("Guard.Add<Photos.CineDirector>(_root)"),
            "cinematic sources: the two buttons stand with the Camera's tests, the feature's cleanup ends a run, and the component is added with the city");
        Check(ModSource(@"Camera\ThirdPersonCamera.cs").Contains("|| Photos.CineDirector.Active") && ModSource(@"Wiring\NavLinks.cs").Contains("|| Photos.CineDirector.Active"),
            "cinematic sources: the mod's mouse look and its minimap stand aside while a run is on");
    }

    // ---------------------------------------------------------------- the texts

    private static void CinematicTextChecks()
    {
        Dictionary<string, string> en = LoadWords(System.IO.Path.Combine(LocalesFolder(), "en.json"));
        Check(en != null, "cinematic texts: en.json is where the checks look for it");
        if (en == null)
        {
            return;
        }
        string[] lacking = CineKeys.Where(k => !en.TryGetValue(k, out string text) || string.IsNullOrEmpty(text)).ToArray();
        Check(lacking.Length == 0, "cinematic texts: every text is in en.json (" + (lacking.Length == 0 ? CineKeys.Length + " keys" : string.Join(", ", lacking)) + ")");
        if (lacking.Length > 0)
        {
            return;
        }
        Check(en["reallife_cine_test_set"] == "Cinematic shots: take the set now" && en["reallife_cine_test_hour"] == "Cinematic shots: at this hour only",
            "cinematic texts: the two buttons are named as the owner named them");
        Check(en["reallife_cine_line"].Contains("{shot}") && en["reallife_cine_line"].Contains("{count}") && en["reallife_cine_line"].Contains("Esc")
              && en["reallife_cine_done"].Contains("{taken}") && en["reallife_cine_done"].Contains("{skipped}") && en["reallife_cine_done"].Contains("{folder}")
              && en["reallife_cine_stopped"].Contains("{taken}") && en["reallife_cine_stopped"].Contains("{folder}"),
            "cinematic texts: the line counts the shots and names Esc, and the notice at the end names the folder");
        string[] notices = CineKeys.Where(k => !k.Contains("_test_") && k != "reallife_cine_line").ToArray();
        string longest = notices.OrderByDescending(k => en[k].Length).First();
        Check(notices.All(k => en[k].Length <= 240), "cinematic texts: every notice fits the game's toast (240 characters; the longest " + longest + ", " + en[longest].Length + ")");
        var dash = new Regex(@"[–—]| - |!");
        Check(CineKeys.All(k => !dash.IsMatch(en[k])), "cinematic texts: no dash and no exclamation mark");
        string sources = ModSource(@"Photos\Cinematic\CineDirector.cs") + ModSource(@"Photos\Cinematic\CineDirector.Tests.cs") + ModSource(@"Photos\PhotosGuide.cs");
        var used = new SortedSet<string>(Regex.Matches(sources, @"""(reallife_cine_[a-z0-9_]+)""").Cast<Match>().Select(m => m.Groups[1].Value), StringComparer.Ordinal);
        Check(used.SetEquals(CineKeys), "cinematic texts: the sources read exactly the texts there are (" + string.Join(", ", used.Except(CineKeys).Concat(CineKeys.Except(used))) + ")");
        // A language that has a text keeps its {names}: one that lacks it reads in English until it is translated.
        var holes = new Regex(@"\{[a-z_]+\}");
        var wrong = new List<string>();
        foreach (string code in new[] { "pt", "zh-cn", "de", "fr", "es", "ru", "nl", "tr", "pl", "ko" })
        {
            Dictionary<string, string> words = LoadWords(System.IO.Path.Combine(LocalesFolder(), code + ".json"));
            foreach (string key in CineKeys)
            {
                if (words != null && words.TryGetValue(key, out string text) && Holes(holes, text) != Holes(holes, en[key]))
                {
                    wrong.Add(code + " " + key);
                }
            }
        }
        Check(wrong.Count == 0, "cinematic texts: a language that has a text has its {names} as English does (" + string.Join(", ", wrong) + ")");
    }
}
