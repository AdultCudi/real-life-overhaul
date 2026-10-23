# City assets: the mod's own animation clips

The game ships few animations and almost nothing a street worker does (no sweeping, digging, kneeling, wiping a
window). Poses set on the bones by hand are why earlier scenes looked wrong. This folder is the answer: new clips,
authored by script, shipped as **Humanoid** clips in one asset bundle, and played on the game's own characters
without touching the game's animator controller. Parallel to `PropAssets\` and `CharacterAssets\`: own scripts, own
Unity project, own bundle.

```
build.ps1                 everything, start to finish
build_bundle.ps1          only Unity: build\anim -> the bundle, the checks, the proof
scripts\
  rig.py                  the authoring rig: poses said as "hips here, this hand on that tool", solved to bone turns; how fingers close (HOLDS)
  motion.py               what keeps a figure from looking like a robot: bending at the hips, weight from foot to foot, walking that does not slide
  clips.py                THE CLIPS: street workers, people standing about, the counter, handcuffs; and the list of all of them
  clips_scenes.py         accident and crime scenes: the stretcher, the lift, the bag, hands up, radio, notebook
  clips_jobs.py           the player's jobs: the gestures of the wish-list, into and out of a car, boxes, flyers, mop, shelf, till, guitar
  clips_life.py           city life: courier, mail, umbrella, painter, gardener, the park, waiting, chess
  clips_<name>.py         the later rounds, a module each (events, duty, everyday, garage, family, site); CLIPS_DEV=name[,name] loads only those
  check_clips.py          can every hand and foot reach where each clip puts it? do the phone clips read (the phone from the eyes, the screen, the elbows, the look)? (no Blender needed)
  build_clips.py          contact sheets, one FBX per clip, clips.json (lengths, loops, how each tool sits in the hand)
  game_rig.json           measurements of the game's street character: T-pose joints, hand-bone turns (numbers only)
  extract_game_skeleton.py, make_game_rig.py     how game_rig.json was measured (needs the game; not part of the build)
  inventory_game_animations.py                   lists what the game's own controllers and clips are (needs the game)
UnityProject\             a bare 2022.3 project: imports the FBX as Humanoid, builds the bundle, tests it
  Assets\Editor\AnimBundleBuilder.cs    Build, Verify, Prove
  Assets\Test\                          the stand-in (the game's skeleton) and the play-mode proof
previews\                 unity_<clip>.jpg: every clip on the game's skeleton with its real tool, and the hand close up (committed: look at them)
CREDITS.md                source and licence of every clip
_reference\ build\        generated, git-ignored
```

## Run it

Needs Blender (made with 5.2), Unity 2022.3.62f2 and `PropAssets\build` (run `PropAssets\build.ps1` once: the
sheets show the tools). The game is not needed.

```
powershell -File CityAssets\build.ps1
```

About five minutes, most of it Unity. It ends with one line per clip and the proof:

```
[AnimBundle]   ok   sweep   2.40 s loop  joints within 0.2 cm (head), hands turned within 0.0 deg, broom within 0.3 cm and 0.0 deg, other hand 0.3 mm off its grip
[AnimBundle] play mode, masked layer (job_flyer_hold, left arm) over the controller: the left hand moved 31.8 cm ...; hips, head, right arm, chest and both legs stayed within 7.69 mm ...
[AnimBundle] play mode, speed matching (push_cart, made for 1.2 m/s): planted foot goes back at 1.16 m/s at speed 1 and at 0.58 m/s after MatchSpeed(0.6, 1.2) = 0.50
[AnimBundle] PROVED: bundled Humanoid clips play on the game's skeleton through a playable graph layered over its controller, a masked clip takes one arm and nothing else, a walking clip keeps step with the ground speed it is matched to, and everything hands back cleanly
```

**Look at `previews\unity_<clip>.jpg` before committing.** Eight moments left to right, from the front quarter
(top) and the side (middle), and under those the hand that matters close up with its fingers (the game's hand: a
thumb, an index finger and one chain for the other three). Drawn by Unity from the shipped bundle through the mod's
own player on a stand-in with the game's skeleton, holding the real prop; things a clip is made to stand at (the
stretcher, the ladder, the chess table) are drawn with it, and a thing picked up or put down part of the way through
is drawn where it lies before and after. Right arm and leg orange, left blue. Whoever writes a clip is not the one
to judge it: have somebody else look at the sheets too.

## How it works

1. **The characters are Humanoid.** The street character's Avatar (`ArmatureLODAvatar`, read from the game's files)
   is a human one, and every street character shares it and its skeleton (looks change meshes and blend shapes,
   never bone lengths). A Humanoid clip is stored as muscle values, not bone turns, so a clip made on any rig plays
   on it.
2. **The rig is the game's character, joint for joint.** `rig.py` builds its skeleton from `game_rig.json`: the
   same joint positions in the same T-pose. So where a clip puts a hand is where the game's character puts it
   (measured: within 1.2 cm for every clip), and a tool modelled to a clip fits.
3. **Clips are written, not keyed.** A clip is a function of time returning a pose said in terms of places: hips,
   chest lean, where each foot stands, where the tool is. Hands follow the tool through a `Grip`; arms and legs are
   solved; the result is a turn per bone per frame. `check_clips.py` fails anything out of reach.
4. **Unity makes the clips.** The rig alone in its T-pose becomes the human Avatar, with the T-pose taken exactly as
   it stands (Unity's "enforce T-pose" would straighten the knees the game's character keeps bent). Each clip's FBX
   borrows that Avatar and is baked into the pose: a clip never moves or turns the character, so where somebody
   stands stays their navigation agent's business.
5. **The mod plays them over the game's controller** (`Scripts\CityLife\Animation`): a small playable graph on the
   character's Animator whose output weight is faded in (cross-fade from whatever the game was playing) and out
   (hand back; the graph is then destroyed). The tool is parented to the hand bone at the clip's grip; a second
   hand is put on it after the animation each frame by a two-bone solver.

### What is proven, and how

- `Verify` (edit mode) loads the shipped bundle as the mod does and plays every clip through the mod's own source
  files (copied into the test project by `build_bundle.ps1`) on a stand-in built from the game's skeleton. It
  compares hips, head, hands, feet, knees and the tool with where the author put them and fails beyond 7 cm, 8 cm
  and 12 degrees for the tool, 12 mm for the second hand. Today: joints within 1.2 cm, tools within 0.7 cm and 0.6
  degrees, second hand within 1 mm.
- `Prove` (play mode, the engine's own frame loop) puts an Animator Controller on the stand-in, cross-fades a
  bundled clip in over it, plays a second clip with a two-handed tool, and hands back. It checks the pose passes
  steadily from the controller's to the clip's and back (hips 0.906 m, 0.696 half way, 0.476, 0.906), the
  controller is untouched, the root never moves, the second hand stays on the tool, and afterwards there is no
  graph and the stepping component is switched off.
- `Prove` also runs a twin stand-in on the controller alone and compares. A clip played on the masked layer
  (`PlayOver`, left arm) moves the left hand 32 cm from where the controller has it while hips, head, chest, the
  other arm and both legs stay within 8 mm of the twin's, frame for frame (the 8 mm is what one pass through
  Humanoid muscle space costs, and it is constant, not a jitter); after `StopOver` the arm is the twin's again to
  0.0 mm. And a walking loop's planted foot goes back under the body at 1.16 m/s at speed 1 (listed: 1.2) and at
  0.58 m/s after `MatchSpeed(0.6, 1.2)`.
- **Not proven:** anything that needs the game itself. The stand-in has the game's skeleton and bone map but none
  of its 14 animator layers, its meshes or its clothes. See "Known limits".

## Using a clip from any feature

```csharp
if (ClipLibrary.TryGet("kneel_work", out CityClip clip))          // false until loaded, or if there is no such clip
{
    ActivityAnimator player = ActivityAnimators.For(character.animator);
    player.Play(clip.Clip);          // cross-fades in over 0.25 s; a loop runs until Stop, a one-off returns by itself
    ...
    player.Stop();                   // cross-fades back to the game's own animation; nothing is left behind
}
```

- `Play(clip, fade, speed, ClipEnd.Hold)` keeps a one-off on its last pose; `Dispose()` hands back at once.
- **Part of the body only: `PlayOver(clip, mask)`.** A second layer that takes just the arms, one arm or the upper
  body from a clip and leaves the rest to whatever is underneath: the game's own walk or idle, or a clip played
  with `Play`. This is how somebody carries something while the game walks them (nothing of the legs is ours, so
  nothing can slide), and how a gesture is made without the legs being taken:

  ```csharp
  if (ClipLibrary.TryGet("job_flyer_hold", out CityClip hold))
  {
      player.PlayOver(hold.Clip, hold.Mask);                       // BodyMask.LeftArm: over walking, standing, anything
      held.Attach(character.animator, stack, hold.Grip);           // the flyer stack, in that hand
  }
  if (ClipLibrary.TryGet("job_hand_over", out CityClip give))
  {
      player.PlayOver(give.Clip, BodyMask.Upper);                  // a one-off: returns by itself; the legs stay the game's
      // give.Mark("release") seconds later: take the object out of the hand
  }
  player.StopOver();                                               // the arm goes back to what is underneath
  ```

  `BodyMask.Upper` is trunk, head and both arms; `Arms`, `LeftArm`, `RightArm` are arms and fingers only (use
  these on somebody who is walking). The pelvis and the legs are never taken. Any clip can be played this way: the
  arms of `push_cart` over the game's walk are someone pushing a pushchair. It costs one small animation job per
  character that has such a clip, run with the Animator off the main thread, nothing while there is none.
- **Keeping step with the ground: `MatchSpeed(groundSpeed, clip.Speed)`.** For the whole-body walking loops
  (`cuffed_walk`, `push_cart`, `pull_cart`, `carry_bag_walk`, `carry_box_walk`, `hand_truck_push`): call it from your
  own tick with the character's real speed (`agent.velocity.magnitude`) and the planted foot goes back at exactly
  that speed. `SetSpeed(x)` sets a playback speed outright. Both tell the engine only when the value changes.

  ```csharp
  if (ClipLibrary.TryGet("cuffed_walk", out CityClip walk) && player.Play(walk.Clip))
  {
      // five times a second, not per frame:
      player.MatchSpeed(agent.velocity.magnitude, walk.Speed);     // 1.2 m/s -> 1.0; 0.9 m/s -> 0.75
  }
  ```
- **Is there such a clip: `ClipLibrary.Has("push_cart")`.** True once the bundle is loaded and has it; says nothing
  in the log (unlike `TryGet`), so a caller with a fallback can ask freely. `CityClip` also carries `Speed`,
  `Mask` and `Mark(name)`.
- A tool: `var held = new HeldTool(); held.Attach(character.animator, toolTransform, clip.Grip);` and
  `held.Detach(parent)` when done. Take the tool's mesh from `PropLibrary` (`clip.Prop` is its id).
- `ActivityAnimators` must exist once (the city life director adds it to the mod's root object). It runs only while
  a weight is changing or a two-handed tool is held; a character the game has hidden costs nothing.
- Before throwing someone down as a ragdoll, `Dispose()` their player: physics and a playing clip must not share bones.
- For the accident and crime scenes: `kneel_work` (a kneel), `cpr`, `direct_traffic`, `cuffed_stand`, `cuffed_walk`,
  `push_cart`, `pull_cart`, `lift_patient`, `carry_bag_walk`, `hands_up` (+ `hands_up_hold`), `radio_talk`,
  `take_notes`, `point_direct`. For the jobs: everything in the "Jobs" table below.

## The clips

160 clips, 621 s of motion, 6.1 MB on disk and 8.7 MB in memory. Lengths in seconds. "m/s" is the ground speed a
walking loop is made for (`CityClip.Speed`: play it at ground speed / that and the planted foot does not slide).
"made for" is the part of the body a clip is meant to be layered with (`CityClip.Mask`, `PlayOver`); blank is the
whole body. "marks" are named moments (`CityClip.Mark("release")`): when a hand closes on something or lets it go.
"Blunt" is how each looks on its contact sheet, honestly: every sheet was looked at by its author and then judged
again by a reviewer who had not made them, and what that review found was fixed or is said here. None has a face.
Standing loops shift weight and move a foot once a loop; they do not walk about.

### Street workers, people standing about, the counter, handcuffs (`clips.py`)

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `sweep` | 2.4 | loop |  |  | `broom` (right + other hand) |  | street broom, both hands; sweeps across the front, 0.84 m ahead | reads at once, back straight, weight goes across with the stroke; the knees stay bent throughout |
| `shovel_dig` | 3.6 | loop |  |  | `shovel` (right + other hand) |  | digs 0.8 m ahead, throws the load off to the left | WEAK: the back is straight now but it is a crouch with the right elbow winged up at the D-handle; reads as digging at street distance only |
| `jackhammer` | 1.2 | loop |  |  | `jackhammer` (right + other hand) |  | the bit is 0.47 m ahead of the feet | good at street distance; the shake is small |
| `paddle_hold` | 4.0 | loop |  |  | `stop_paddle` (right) |  | paddle stood on the ground at the right; left hand slows traffic | reads; quiet |
| `direct_traffic` | 3.2 | loop |  |  |  |  | left hand holds one stream, right arm waves the other on | reads; the beckoning hand passes close to the face |
| `squeegee_wipe` | 4.0 | loop |  |  | `squeegee` (right) |  | the glass is 0.50 m ahead of the feet | reads; the pull is done more by the knees than the arm |
| `kneel_work` | 3.0 | loop |  |  |  |  | right knee down, hands working 0.45 m ahead at knee height | back straight, reads as kneeling at work; the hands do little |
| `cpr` | 1.1 | loop |  |  |  |  | both knees down; the chest being pressed is 0.40 m ahead, 0.25 m up | position right (shoulders over hands, back flat); the compressions are small |
| `clipboard_write` | 4.5 | loop |  |  | `clipboard` (left) |  | board in the left hand, writing with the right | fine |
| `leaflet_idle` | 6.0 | loop |  |  | `leaflet_stack` (left) |  | leaflets on the left palm; looks up and down the street | fine, a little stiff |
| `leaflet_offer` | 2.6 | once |  |  | `leaflet` (right) |  | holds one out 0.65 m ahead; the stack stays in the left hand | good: step, lean, arm out |
| `talk_a` | 6.0 | loop |  |  |  |  | talking, with the hands; face the other person | adequate |
| `talk_b` | 6.4 | loop |  |  |  |  | listening: nods, a shrug, a look away | adequate, quiet |
| `wave_hello` | 2.2 | once |  |  |  |  | right arm up, three waves | fine |
| `clap` | 2.0 | loop |  |  |  |  | four claps a loop | adequate |
| `watch_nod` | 6.4 | loop |  |  |  |  | listening to music, arms down | quiet by design: reads as standing |
| `smoke` | 9.0 | loop |  |  | `cigarette` (right) |  | a drag every nine seconds | the drag reads |
| `take_photo` | 5.0 | once |  |  | `phone` (right + other hand) |  | filming or a picture of what is ahead: the phone upright in both hands, up to eye level 0.30 m from the eyes with the screen to them, framed, held, lowered to the chest | reads at once from behind and in front: elbows down in front of the chest, no wider than the shoulders. Until 14 Oct 2026 the phone was held by its ends at the chin, 0.44 m out, which with the game's short arms put both elbows up level with the shoulders and out to the sides; check_clips.py now fails that |
| `selfie` | 5.0 | once |  |  | `phone` (right) |  | a selfie: the right arm takes the phone up and out to its length (0.42 m from the eyes, 40 degrees round to the right, 15 up), the screen to the face, the head turned up into it and tipped; two pictures, lowered to the chest for a look. Play it with the back to what is to be in the picture (Leisure's sightseeing turns the player first) | reads as a selfie from in front, from the side and from behind (the phone above the head); the left arm hangs. check_clips.py measures the reach, the screen's facing and the look |
| `counter_reach` | 2.4 | once |  |  | `coffee_cup` (right) | grip 0.90 | the right hand goes to a cup on a counter 0.62 m ahead and 0.975 m up, and back: put the cup in or take it out of the hand half way | good: hinge at the hips, cup lands in the hand |
| `cup_hold` | 7.0 | loop |  |  | `coffee_cup` (right) |  | a cup held at the chest | fine, quiet |
| `sip` | 3.4 | once |  |  | `coffee_cup` (right) |  | a drink from the cup, from and back to cup_hold | fixed after review (the cup went to the cheek); now at the mouth, not checked close up |
| `broom_rest` | 7.0 | loop |  |  | `broom` (right + other hand) |  | leaning on the broom; alternate with sweep | fine |
| `shovel_rest` | 7.0 | loop |  |  | `shovel` (right) |  | shovel stood in the right hand, the left wipes the brow; alternate with shovel_dig | good |
| `jackhammer_idle` | 6.0 | loop |  |  | `jackhammer` (right + other hand) |  | stopped, looking round; alternate with jackhammer | fine |
| `squeegee_rest` | 6.0 | loop |  |  | `squeegee` (right) |  | squeegee down at the side, looking the glass over | adequate; the squeegee hangs close to the thigh |
| `counter_wipe` | 3.6 | loop |  |  |  |  | wiping a counter 0.55 m ahead and 0.975 m up | fine |
| `serve` | 2.2 | once |  |  |  |  | hands something over a counter | good |
| `cuffed_stand` | 5.0 | loop |  |  |  |  | hands behind the back, head down | fine, head down |
| `cuffed_walk` | 1.1 | loop | 1.2 |  |  |  | walking on the spot, hands behind the back: made for 1.2 m/s, the game's walking pace | a real walk: heel strike, toe-off; proven not to slide at 1.2 m/s |

### Accident and crime scenes (`clips_scenes.py`)

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `push_cart` | 1.1 | loop | 1.2 |  |  |  | walking behind a rail 0.44 m ahead and 0.95 m up, hands 0.45 m apart (the stretcher's bar, a pushchair): made for 1.2 m/s | good: fists on the bar's grips, a walk with weight in it |
| `pull_cart` | 1.1 | loop | 0.9 |  |  |  | walking BACKWARDS at 0.9 m/s drawing the same rail after; the character still faces the rail | adequate: a backward walk; little lean against the load |
| `lift_patient` | 1.6 | once |  |  |  | lift 0.42, up 1.38 | one of two lifting together: from the right knee, hands under someone lying 0.42 m ahead, to standing with the hands 0.30 m ahead and 0.88 m up; hold the end | adequate: straight back, the legs do the lift; folds a long way forward at the bottom |
| `carry_bag_walk` | 1.1 | loop | 1.3 |  | `medic_bag` (right) |  | walking at 1.3 m/s with the medic bag in the right hand | fine; the bag hangs stiff |
| `hands_up` | 1.0 | once |  |  |  |  | both hands go up; hold the end or go on with hands_up_hold | reads |
| `hands_up_hold` | 5.0 | loop |  |  |  |  | hands kept up beside the head; follows hands_up | reads; quiet |
| `radio_talk` | 5.0 | loop |  |  |  |  | left hand at the shoulder radio on the left collar bone, speaking and listening | reads as a hand at the collar |
| `take_notes` | 5.0 | loop |  |  | `notebook` (left) |  | notebook in the left hand, writing with the right, a look up and a nod | fine |
| `point_direct` | 2.6 | once |  |  |  |  | points to the character's right with the right arm and waves someone in front along that way | good |

### Jobs (`clips_jobs.py`)

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `job_hail` | 1.6 | loop |  | upper |  |  | right arm up and out, two waves a loop, up on the toes: hailing a car in front | improved after review (the arm hugged the head); adequate |
| `job_hand_over` | 1.2 | once |  | upper |  | release 0.55 | holds something out 0.50 m ahead at 1.15 m with the right hand and lets go | good |
| `job_hand_over_l` | 1.2 | once |  | upper |  | release 0.55 | the same with the left hand (the flyer hand) | good |
| `job_receive` | 1.3 | once |  | upper |  | grip 0.50 | reaches 0.50 m ahead at 1.15 m, takes what is offered, draws it in, nods | good |
| `job_receive_box` | 1.3 | once |  | upper | `parcel_m` (right + other hand) | grip 0.50 | both hands take a box 0.38 m wide held out in front and draw it in; ends carrying it (carry_box_hold) | good; only the fingers are under the box |
| `job_refuse` | 1.2 | once |  | upper |  |  | right palm up and out at shoulder height, head turned away, half a step back | good |
| `job_thank` | 1.2 | once |  | upper |  |  | a nod and a short open-hand wave at head height | moved clear of the head after review; adequate |
| `job_knock` | 1.6 | once |  | upper |  | rap 0.42 | three raps at 1.50 m on a door 0.50 m ahead, then a step back | adequate; the raps are small |
| `job_annoyed` | 1.4 | once |  | upper |  |  | both hands thrown up, a shake of the head, a turn away | adequate: more a shrug than exasperation |
| `job_car_enter` | 1.5 | once |  |  |  | door 0.35, seated 1.25 | from standing facing a door on the car's RIGHT side, feet 0.75 m out from the middle of the seat, into the seat (0.52 m up); the root does not move | plausible (step in, duck, turn, sit) but made without a car to fit it to: UNVERIFIED against any vehicle |
| `job_car_enter_l` | 1.5 | once |  |  |  | door 0.35, seated 1.25 | the same for a door on the car's LEFT side | as job_car_enter |
| `job_car_exit` | 1.5 | once |  |  |  | standing 1.20 | job_car_enter backwards: out of the seat to standing at the same point, facing the car | as job_car_enter |
| `job_car_exit_l` | 1.5 | once |  |  |  | standing 1.20 | job_car_enter_l backwards | as job_car_enter |
| `job_load_boot` | 1.8 | once |  |  | `parcel_m` (right + other hand) | release 1.00 | lowers a box carried in both arms on to a boot floor 0.52 m ahead and 0.70 m up, lets go and straightens | adequate after review (was a full squat): a hip hinge to a floor 0.70 m up |
| `job_flyer_hold` | 5.0 | loop |  | left arm | `flyer_stack` (left) |  | a stack of flyers held to the chest by the left hand; made to be layered over walking (left arm only) | good as an arm overlay |
| `carry_box_hold` | 5.0 | loop |  | arms | `parcel_m` (right + other hand) |  | a box in both arms; made to be layered over the game's walk or idle (arms only): the way to carry a parcel | good |
| `carry_box_walk` | 1.1 | loop | 1.2 |  | `parcel_m` (right + other hand) |  | the whole body walking at 1.2 m/s with a box in both arms | fine; the box is rigid on the chest |
| `mop_floor` | 3.2 | loop |  |  | `mop` (right + other hand) |  | mopping in a figure of eight 0.7 m ahead | reads |
| `stock_shelf` | 2.6 | once |  |  |  | grip 0.60, release 1.50 | right hand takes from a box at the left hip (0.95 m) and sets it on a shelf 0.55 m ahead at 1.30 m | adequate; the hand is empty (no item prop) |
| `serve_counter` | 2.0 | once |  |  | `coffee_cup` (right) | grip 0.28, release 1.10 | behind a counter 0.975 m high: lifts a cup from 0.33 m ahead and sets it down 0.78 m ahead for the customer | good |
| `till_tap` | 1.6 | once |  | upper |  | tap 0.50 | three taps with the right forefinger on a till 0.34 m ahead, 1.15 m up | reads |
| `guitar_strum` | 3.2 | loop |  |  | `guitar` (left) |  | standing, strumming a guitar held across the body; left hand on the neck | the pose reads; the strum is small |

### City life (`clips_life.py`)

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `hand_truck_push` | 1.1 | loop | 1.2 |  | `hand_truck` (right + other hand) |  | walking at 1.2 m/s behind the sack truck leant back 38 degrees; its axle is 1.09 m ahead of the feet | good |
| `box_set_down` | 2.2 | once |  |  | `parcel_m` (right + other hand) | release 1.20 | a box carried in both arms is put on the ground 0.40 m ahead, and up again | adequate after review (was a collapsed ball): a deep squat, head up |
| `box_pick_up` | 2.2 | once |  |  | `parcel_m` (right + other hand) | grip 1.00 | box_set_down backwards: down to a box on the ground 0.40 m ahead and up with it; ends carrying (carry_box_hold) | as box_set_down |
| `mail_deliver` | 2.6 | once |  |  |  | grip 0.70, release 1.60 | takes a letter from the satchel at the left hip and posts it through a slot 0.52 m ahead, 1.05 m up | adequate; the letter itself is not modelled |
| `umbrella_hold` | 6.0 | loop |  | right arm | `umbrella` (right) |  | an open umbrella held up in the right hand; made to be layered over the game's walk (right arm only) | raised after review (the canopy sat on the head); adequate |
| `paint_roll` | 4.4 | loop |  |  | `paint_roller` (right) |  | standing on the first tread of step_ladder (0.24 m up), rolling a wall 0.80 m ahead between 1.6 and 2.0 m | adequate: on the first tread (the second put the shins through the platform); short strokes |
| `water_plants` | 6.0 | loop |  |  | `watering_can` (right) |  | watering a planter 0.65 m ahead from a can in the right hand | good |
| `rake_leaves` | 2.8 | loop |  |  | `rake` (right + other hand) |  | raking towards the feet from 0.8 m ahead | reads |
| `tai_chi` | 12.0 | loop |  |  |  |  | slow 'cloud hands' in a wide stance; needs 0.8 m clear either side | reads roughly: slow arms over a wide stance |
| `map_read` | 8.0 | loop |  |  | `map_folded` (right + other hand) |  | a map open in both hands: studied, then a look up and down the street | good |
| `read_paper` | 9.0 | loop |  |  | `newspaper` (right + other hand) |  | standing reading a newspaper held open in both hands | good |
| `arms_crossed` | 8.0 | loop |  |  |  |  | standing square with folded arms, looking up and down the street | lowered to the chest after review; fine |
| `lean_wait` | 9.0 | loop |  |  |  |  | leaning back on a car's side 0.14 m behind the feet, arms folded, ankles crossed | fixed after review (was a rigid plank): seat back, trunk upright; fine |
| `guard_stand` | 8.0 | loop |  |  |  |  | feet apart, hands clasped in front: on a door | good |
| `queue_wait` | 9.0 | loop |  |  |  |  | waiting in a queue: a lean out to see the front, a look at the watch | the look at the watch reads; the lean out is small |
| `pay_tap` | 1.8 | once |  | upper |  | tap 0.70 | a card held to a reader 0.50 m ahead on a counter 0.975 m up | reads; no card is modelled |
| `chess_sit` | 10.0 | loop |  |  |  |  | sitting on a stool of chess_table (0.45 m), thinking and making a move; the table's centre is 0.62 m ahead | good |

<!-- module: events -->
### Fires and street events (`clips_events.py`)

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `hose_hold` | 4.0 | loop |  |  | `hose_nozzle` (right + other hand) |  | braced behind a nozzle whose pivot is 0.46 m ahead, 0.17 m right and 1.02 m up, pointing ahead and 12 degrees up (where `FireScene.StartWater` stands it): left foot 0.50 m forward, right 0.17 m back, the body carried 0.15 m forward and low; right palm 0.06 m along the nozzle, left under the barrel 0.30 m along; twice a loop the jet shoves the hips 4 cm back and 3 cm down on to the rear leg and they drive forward again | after review: reads as braced on a hose, the rock back shows in the side view and the arms are less jammed; still a quiet clip, and the right wrist is cocked. In the game `HoseHold` puts the wrists where this clip's own grip has them on the nozzle (not on the two palm points, which put the hands 0.08 m too far along), so at the clip's aim it corrects by about nothing |
| `pump_panel` | 4.0 | loop |  |  |  |  | leaning in 9 degrees at a panel 0.50 m ahead: the right palm takes a hand wheel on it (0.16 m right, 1.10 m up) round once, the left palm down on a ledge 0.99 m up; then the head tilts up to a gauge and the left forefinger reaches up and forward to tap it twice, about 1.45 m up on the centre line | after review: adequate. The reach to the gauge and the look up read, the right hand is out at the panel; the wheel turn is still small, and nothing of a panel is drawn with it |
| `keep_back` | 3.0 | loop |  |  |  |  | both arms out, palms forward about 0.45 m ahead of the shoulders at 1.36 m, a step forward with the left foot (0.34 m) and back, two thrusts of about 0.12 m with the chest behind them, then hips, chest and head turn to look over the RIGHT shoulder (the fire is behind) | after review: reads as "stay back", no longer a surrender; arms long, the thrusts and the step show. The look over the shoulder is still brief (0.4 s held) |
| `crowd_look_up` | 6.0 | loop |  |  |  |  | weight on the right leg, a little back lean, head back looking 50 degrees up; the right hand comes up along the body and is held as a peak just off the brow from 1.2 to 2.2 s; then the weight goes to the left leg and the left arm points up from 4.1 to 4.8 s | after review: reads as watching the sky; the hand is now a peak in front of the brow, edge on, and the arms come up by the chest, not straight out. The hands turn over quickly on the way up and down |
| `placard_hold` | 4.0 | loop |  |  | `placard` (right) |  | standing: the staff (`placard`, `banner_pole`; the stop paddle's grip) upright in the right fist 1.10 m up, 0.27 m right and 0.22 m ahead, elbow down by the ribs, pumped up 0.15 m twice a loop with a dip and spring in the knees; the weight goes to the left leg as the left fist punches the air once a loop. The right arm alone (`PlayOver`, `RightArm`) is the marcher's | after review: good. Elbow tucked, the bounce and the punch read; the pump is kept at the wish-list's 0.15 m; seen from the front the board crosses the head's line |
| `placard_hold_arm` | 4.0 | loop |  | right arm | `placard` (right) |  | the same right arm and grip over a quiet body, listed for the right arm; the code does not need it (it masks `placard_hold` itself) | fine as an overlay (elbow tucked as above); dull by itself |
| `camera_operate` | 6.0 | loop |  |  |  |  | at `film_camera`, whose pivot is 0.75 m AHEAD facing the same way: bent 22 degrees at the hips with the legs nearly straight, feet in a short stride behind the tripod's legs (left 0.02 m back, right 0.20 m back), right eye at the eyecup (0.032 m right, 0.16 m ahead, 1.50 m up) and kept there while the weight shifts; right hand on the pan bar, left on the handle of the focus whip; from 3.5 to 5.5 s the chest comes up 10 degrees, the head 0.11 m off the eyepiece and the left hand drops to the thigh | after review: good with the prop on the sheet, no longer a crouch. The hips sit 0.2 m behind the feet's point, which only works because the hands are on the camera. The left hand is on the focus whip, not the lens (0.8 m away from there). Only right if the operator really stands 0.75 m behind the camera: 0.1 m off and the eye misses the cup |

- **The nozzle** (`hose_hold`): the character stands where it stood; the nozzle's pivot is 0.46 m ahead, 0.17 m right, 1.02 m up, pitched 12 degrees up. The game aims it anywhere from 3 to 45 degrees up and its two-bone reach takes up the difference, from the wrists' places in the clip's grip (`HoseHold.Begin(animator, nozzle, grip)`).
- **A staff** (`placard_hold`, `placard_hold_arm`): `placard` and `banner_pole` at the stop paddle's grip (the fist 1.10 m above the prop's pivot); both clips share that `Grip`, as does `paddle_hold`.
- **The camera** (`camera_operate`): the operator 0.75 m behind `film_camera`'s pivot on the lens's line, facing the way the lens looks.
<!-- end: events -->

<!-- module: duty -->
### On duty and getting about (`clips_duty.py`)

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `arrest_cuffed` | 2.4 | once |  |  |  | click 1.50 | from `hands_up_hold`'s first pose (exactly): the hands come down out to the sides, palms down, and go behind the back, the right a moment before the left (there at 1.4 and 1.46 s); at the click the shoulders are jerked back and the head drops; ends in `cuffed_stand`'s first pose (exactly): cut or fade to it | after review (the hands were there at 40 % and the rest stood still): reads, with a jolt at the click; the arms go wide on the way down; not seen on a sheet beside the officer |
| `officer_cuff` | 2.4 | once |  |  | `handcuffs` (right) | click 1.50 | the officer's half of `arrest_cuffed`: whoever is cuffed stands 0.90 m AHEAD, back turned. A short step in with the left foot (0.29 m; the whole body: play it with `Play`) and a lean, the cuffs put on wrists 0.77 m ahead and 0.94 m up, then the left hand on their left shoulder (0.17 m left, 0.86 m ahead, 1.40 m up). ENDS SO, stepped in: hold the end | after review (open fingers, a long lunge on the toes): a fist round the cuff, both heels down, elbow low; the step in and the reach read. The last pose is a straight arm at shoulder height that only makes sense with the prisoner there; the pair was checked by numbers (palms within 4 cm of the wrists and the forearm), never seen together |
| `officer_guard_idle` | 8.0 | loop |  |  |  |  | standing easy on a door: thumbs hooked in the duty belt either side of the buckle (wrists 0.15 m out and 0.18 m ahead of the hip joints), elbows out, the hips going 5 cm from foot to foot, the head 45 degrees each way with the shoulders following, a rock on the toes | after review (too still, a hand in the pelvis): adequate; reads as hands on the front of the belt rather than thumbs hooked |
| `cuffed_car_enter` | 2.0 | once |  |  |  | door 0.25, seated 1.70 | `job_car_enter` with the hands cuffed behind the back, from `cuffed_stand`'s first pose: a door on the car's RIGHT side, feet 0.75 m out from the middle of the seat (0.52 m up); ducks, turns, sits leaning forward off the hands; the door must be open by 0.25 s; the root does not move | after review (folded past level, a leg kicked out straight, a shin dragged): the duck is about 50 degrees with the head above the hips, the lead knee bent; plausible; as `job_car_enter`, UNVERIFIED against any vehicle |
| `cuffed_car_enter_l` | 2.0 | once |  |  |  | door 0.25, seated 1.70 | the same for a door on the car's LEFT side | as `cuffed_car_enter` |
| `board_step` | 1.4 | once |  |  |  | step 0.50, aboard 1.25 | up through a bus door: the step's edge 0.35 m ahead and 0.30 m up, the right hand takes an upright rail 0.40 m right and 0.80 m ahead at 1.42 m (at 0.78 s) and slides up it to 1.62 m; the root does not move; ENDS STANDING ON THE STEP leaning in a little (hips 0.44 m ahead, feet flat 0.30 m up): take the person off the street then | after review (the fist ended at the jaw): the hand is out ahead and to the right with the elbow open; reads as a step up with a hand on a rail; quick; made without a bus to fit it to |
| `strap_hang` | 6.0 | loop |  |  |  |  | standing in a moving bus, the right hand in a strap 1.82 m up, 0.16 m ahead and 0.28 m to the right, the elbow bent (110 to 135 degrees); the trunk leans 4 degrees away from the strap with the weight on that arm, and swings 9 cm fore and aft and 6 cm side to side under it, the knees giving and the left arm swinging against it; a turn of the head and chest to the window | after two reviews (the fist sat on the skull; then no weight on the strap and a dead left arm): reads as hanging off a strap in something that moves |
| `sit_bench` | 1.6 | once |  |  |  | seated 1.30 | sits down on a seat 0.46 m up whose middle is 0.30 m BEHIND the feet; the root and the feet stay; ends in `sit_wait`'s first pose exactly | good |
| `sit_bench_up` | 1.6 | once |  |  |  | standing 1.40 | gets up off that seat, from `sit_wait`'s first pose exactly, to standing where the feet are | good |
| `sit_wait` | 8.0 | loop |  |  |  |  | sitting on that seat, hands on the thighs: leans out forward and to the left and looks down the street for the bus, sits back, shifts, stretches a foot (home again by 6.9 s), a shorter look the other way | good on `precinct_bench`; the lean out is now sideways as well, still modest |
| `refuel_hold` | 5.0 | loop |  |  | `jerry_can` (right + other hand) |  | bent 30 degrees at the hips emptying a jerry can into a filler 0.40 m ahead and 0.90 m up: the left foot 0.15 m ahead, the hips back; the can slants back and to the left from the filler, between 0.75 and 1.1 m up, 0.3 m in front of the chest; right hand on the carry handle, left under it by the base, which is heaved up 13 degrees (about 9 cm) to pour and let down, twice a loop, the body rising with it; one look up the road | after two reviews (hugged under the chin; then still close and static): ADEQUATE: at the belly, clear of the head, visibly pouring. The right arm stays well bent: the handle is 5 cm from the cap, so that hand has to be at the filler. Made without a car |
| `bonnet_check` | 6.0 | loop |  |  |  |  | leaning over an open bonnet 0.90 m up: the left hand braced on the bay's edge 0.40 m left and 0.44 m ahead, the feet 0.15 m apart fore and aft; the right hand reaches 11 cm in to something 0.70 m ahead and 0.86 m up, twists it and pulls back, the head and that shoulder dipping in with it, then the same 0.16 m further left; then up, both hands wiped on each other, and slowly in again | after two reviews (frozen, the hand by the thigh, a snap back): the hand reaches, twists and pulls, the body goes in with it, the return takes 1.3 s; fine |
| `carry_suitcase_walk` | 1.08 | loop | 1.3 |  | `suitcase` (right) |  | `carry_bag_walk` with a suitcase in the right hand, held 0.34 m out from the middle so that it clears the leg, its bottom 0.18 m off the ground, broad sides along the way they go; the trunk leans 5 degrees away from it and the left arm swings 0.27 m each way | fine; the case is nearly rigid |
| `desk_plan` | 6.0 | loop |  | upper |  |  | at a desk 1.00 m high: the right hand lifts a marker off a board 0.45 m ahead and sets it down elsewhere, twice; between, a lean and a look up at a wall map beyond with a finger held out at it 0.64 m ahead and 1.40 m up; the left hand lies on the desk's edge | after review (the elbow winged out, the finger pointed at the chest): elbow down, the arm goes out at the map; adequate; the markers are not modelled. Over the game's legs the lean at the hips (4 to 6 degrees) is lost |
| `staff_struggle` | 6.0 | loop |  |  |  |  | stuck: both hands on the hips with the elbows out, the weight on the left leg, the head pushed forward and down at something on the floor 0.6 m ahead and shaken, a short breath out; at 2 s the weight goes over on to the right hip; a look away, a breath in and a long one out (the chest sinks 3 cm, the shoulders fall 3 cm); the weight comes back and an open hand goes out palm up at it, 0.4 m ahead at waist height | after two reviews (one arm hung; then the first 4 s stood still): reads as hands on hips and fed up, and no longer holds one pose |
| `collector_lean` | 5.0 | loop |  |  |  |  | leaning in on the left hand flat on a counter 0.975 m up and 0.45 m ahead, the feet under him (0.07 and 0.17 m ahead, flat); the trunk rocks on to that hand and off it (9 degrees); the right hand gestures over the counter near the middle (open palm twice at 1.15 m, then a forefinger jabbed down twice, stopping 3 cm clear of the top); looks at whoever is 1.1 m ahead | after two reviews (crouched as if pushing the cart; then near-still with the hand skimming the worktop): stands at the counter, rocks on it, the hand is clear of it; reads AT A COUNTER (seen on `vendor_cart`); with no counter under the hand it is somebody bent forward with a hand in the air: WEAK there |
| `inspect_look` | 6.0 | loop |  |  | `clipboard` (left) |  | clipboard in the left hand (`clipboard_write`'s grip, so the two cross-fade), the pen hand waiting beside it with the elbow bent: a look up and along a ceiling, down and along a floor (the head does it, the body hardly leans), then a tick on the board with the right hand | reads; the tick is small |

Where the character must be put (for that list in the README; all from the character's feet: right, ahead, up):

- **The arrest** (`arrest_cuffed`, `officer_cuff`): the officer 0.90 m straight behind the prisoner, both facing the same way. The two clips are the same length with the same `click` (1.5 s): start them together. `officer_cuff` is a whole-body clip (the officer steps in 0.29 m with the left foot, leans, and ends there with a hand on the shoulder): `Play` it and hold its end, do not layer it. The handcuffs in the officer's hand end exactly where `CuffPose` lays its own pair on `cuffed_stand`: from the click show one pair only.
- **Into a car, cuffed** (`cuffed_car_enter`, `_l`): exactly as `job_car_enter`.
- **A bus door** (`board_step`): the step's edge 0.35 m ahead and 0.30 m up, a rail at the right inside the door (0.40 m right, 0.80 m ahead). The clip ends on the step and does not come back.
- **A strap** (`strap_hang`): 1.82 m up, 0.16 m ahead, 0.28 m right.
- **A bench** (`sit_bench`, `sit_wait`, `sit_bench_up`): the seat 0.46 m up, its middle 0.30 m behind the feet, the character's back to it. On `precinct_bench` (sitter faces +Z, seat middle 0.03 m ahead of the pivot) that is the character 0.33 m to the +Z of the bench's pivot line; on `bus_shelter` (seat along the back, sitter facing -X) 0.30 m to the -X of the seat's middle line, facing -X.
- **A filler** (`refuel_hold`): 0.40 m ahead and 0.90 m up; the can needs 0.45 m clear to the character's left and front at belly height.
- **An open bonnet** (`bonnet_check`): the bay 0.90 m up; the front of the car 0.25 m ahead of the feet, the work 0.70 m ahead.
- **A desk** (`desk_plan`): top 1.00 m up, near edge 0.30 m ahead. **A counter** (`collector_lean`): top 0.975 m up, near edge 0.32 to 0.36 m ahead with room for the toes under it (on the carts: the customer's place, 0.72 m from the cart's middle).
<!-- end: duty -->

<!-- module: everyday -->
### Everyday life: home, friends, leisure (`clips_everyday.py`)

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `cook_stir` | 3.0 | loop |  |  | `pan` (left) |  | at a hob 0.45 m ahead and 0.92 m up (the pan's base): the left hand on the pan's handle, which points back and left; the right stirs three turns a loop with a spoon that is not modelled; the pan is shaken once a loop | after review (was bolt upright): bent to the hob from the hips, head down, the forearm going round; the stirring hand is still an empty fist |
| `pushup` | 2.0 | loop |  |  |  |  | press-ups along the character's forward axis, CENTRED on the origin: toes 0.79 m behind it, hands 0.50 m ahead and 0.50 m apart, the crown 0.9 m ahead (`PUSHUP_FEET` moves all of it) | after review: legs at full stretch (no sag at the knee), chest stops a hand's breadth up with the head up, elbows 45 degrees back; good |
| `situp` | 2.4 | loop |  |  |  |  | sit-ups, knees bent, feet flat, fingertips at the temples: the hips 0.08 m ahead of the origin, the feet 0.60 m ahead, the head 0.70 m behind it when lying | hands moved back and up to the temples after review; good |
| `stretch` | 10.0 | loop |  |  |  |  | both arms overhead and up on the toes, a bend to the left and to the right, arms down, a forward fold, and up; needs 0.8 m clear either side | after review: arms straight up, straight out through the sides on the way down, the fold with a round back and hanging head; reads |
| `eat_sit` | 4.0 | loop |  |  |  |  | seated (seat 0.45 m up, its middle at the origin) at a table 0.75 m high whose near edge is 0.20 m ahead: fork to the mouth from a `plate` 0.35 m ahead, the left forearm on the table | reads; no fork is modelled, the fist stops 3 cm further from the face than a fork's length |
| `drink_stand` | 3.0 | once |  |  | `mug` (right) |  | a mug carried in front of the right hip (0.28 m ahead, 1.04 m up) is lifted to the mouth, tipped and lowered; held round the body on the side AWAY from the handle | after review (the forearm stuck out level at chin height): the mug turns as it rises so the wrist stays out to its right, elbow low, forearm steep; rim at the lips; good |
| `social_handshake` | 1.8 | once |  | upper |  | grip 0.50, release 1.30 | for two FACING 0.80 m apart, both playing it: the right palms meet on the centre line 0.40 m ahead and 1.05 m up, two shakes; nothing below the chest moves | bigger shake (5.5 cm), a lean of the chest and a nod after review; good |
| `social_hug` | 2.6 | once |  | upper |  |  | the upper body's hug, both playing it FACING: the chest leans in, the left hand high (1.40 m) and the right low (1.15 m) close round a body 0.45 m ahead, a pat, each head to its own left | after a second review: elbows low and bent, both hands closing round a body 0.45 m ahead (left 1.40 m up, right 1.15 m), chest in 12 degrees, a 5.5 cm pat; reads as a hug made on the spot; two copies at 0.80 m reach each other's chest and shoulder, no further |
| `social_hug_a` | 2.6 | once |  |  |  |  | the whole body's hug, one of a pair (with `social_hug_b`, or with itself): partner FACING 0.80 m ahead; half a step in with the left foot, finishing 0.35 m apart, left arm over their shoulder, right under their arm, two pats, and back; the root stays put | after review (was a dab at the own chest): arms go right round, left hand to the middle of the other's upper back, right to the middle of their back; measured against a copy, not against a mesh; alone on a sheet the high forearm still crosses in front of the own face, 0.4 m ahead of it |
| `social_hug_b` | 2.6 | once |  |  |  |  | the other of the pair: the same stepping in with the right foot, holding still while patted | as `social_hug_a`; both heads go to their OWN left, which is opposite sides for two who face each other |
| `social_cheek_kiss` | 1.6 | once |  |  |  |  | for two FACING 0.80 m apart, both playing it: half a step in with the right foot, a lean, right cheeks side by side 0.40 m ahead, the right hand on the other's arm; the root stays put | after review: more lean, head turned to offer the cheek, hand round the other's upper arm; adequate |
| `sit_chat_a` | 6.0 | loop |  |  |  |  | seated (seat 0.45 m up, its middle at the origin), turned to somebody 0.55 m to the LEFT on the same bench, talking with the right hand | chest turned 25 degrees to the partner, bigger nods after review; fine |
| `sit_chat_b` | 6.0 | loop |  |  |  |  | seated the same way, turned to somebody 0.55 m to the RIGHT, listening: nods, sits back with open hands, a look away | chest turned 24 degrees to the partner, deep nods after review; fine |
| `cheers` | 1.8 | once |  |  | `wine_glass` (right) | clink 0.80 | a glass held by its stem in front of the right hip (1.05 m up) goes out at chest height with a lean and straight back: at 0.8 s the bowl is 0.53 m ahead, 1.30 m up and 0.04 m to the right, so two FACING 1.05 m apart touch bowls side by side (at 0.80 m they pass); starts and ends holding the glass, not in the idle | after a second review: a quick out-touch-back at chest height (bowl 1.30 m up, never near the mouth), elbow opening to about 85 degrees (not the 110 asked for: that needs the glasses to meet 0.58 m ahead), a lean in; fine. Two copies touch at 1.05 m apart, not at 0.80 m |
| `eat_standing` | 3.4 | once |  |  | `takeaway_box` (left) |  | a carton on the left palm 0.30 m ahead and 1.06 m up from start to end; the right hand takes a piece out and brings it to the mouth as the carton comes up under the chin | elbow kept low and in, carton lifted 14 cm, head dips to it after review; good |
| `walk_talk` | 5.0 | loop |  | upper |  |  | over the game's walk, for the one walking on the RIGHT: head turned 25 degrees to the left, the right hand making a point twice a loop, both arms otherwise swinging once a second | idle arm swings 15 cm each way after review; adequate; the swing cannot stay in step with the game's legs |
| `walk_talk_l` | 5.0 | loop |  | upper |  |  | `walk_talk` mirrored, for the one walking on the LEFT: head to the right, the left hand talking | as `walk_talk` |
| `point_show` | 2.2 | once |  | upper |  |  | an open right hand sweeps from the left (0.12 m left, 0.40 m ahead, 1.17 m up) round past straight ahead and out to the right (0.45 m right, 0.48 m ahead, 1.27 m up), chest and then head following | after review the hand finishes 0.45 m out to the right, chest and then head following; fine |
| `phone_answer` | 1.6 | once |  | upper | `phone` (right) | grip 0.45 | the right hand dips to the trouser pocket (show the phone from 0.45 s), a glance at the screen in front of the chest, then to the right ear; ENDS there: play it with `ClipEnd.Hold` and go on with the game's `TalkingPhone` | after review: phone 4 cm off the ear, elbow down and forward 30 degrees below level, head tipped to it; the wrist is so near the shoulder that the elbow cannot hang lower; adequate |
| `box_lift_with` | 2.2 | once |  |  |  | grip 0.80, up 1.90 | one of two lifting a box 1.20 m long that lies between them: the partner FACING 2.04 m ahead, the box's near end 0.42 m ahead; hands 0.35 m apart under the end (0.47 m ahead), on the ground at 0.8 s; its underside (0.03 m under the wrists) is 0.85 m up at 1.9 s and it has not moved over the ground; ENDS holding | adequate: a very deep squat (as `box_set_down`), long arms on the way up; no box is modelled |
| `box_lift_with_b` | 2.2 | once |  |  |  | grip 0.80, up 1.90 | `box_lift_with` mirrored, for the partner at the other end | as `box_lift_with` |
| `goodbye_wave_walk` | 1.4 | once |  | right arm |  |  | over the game's walk: the right hand up beside the head (0.36 m right, 1.66 m up), palm to the BACK, two flicks, and down | bigger flick of hand and forearm after review; reads as a wave; the elbow still leads on the way up |
| `cast_rod` | 1.6 | once |  |  | `fishing_rod` (right) | release 0.90 | from and back to `fish_wait`'s hold: the left hand lets go, the right takes the rod back over the right shoulder (24 degrees past upright) and whips it forward; rod in the RIGHT hand 0.10 m from the butt | good; back-swing hand moved 10 cm out and 9 cm back from the head with 24 degrees of trunk turn; starts and ends EXACTLY in fish_wait's first pose |
| `fish_wait` | 8.0 | loop |  |  | `fishing_rod` (right + other hand) |  | the rod held out 29 degrees above level and 22 degrees to the left of straight ahead: right hand beside the right hip 0.10 m up the handle, left 0.40 m up over the reel seat; the tip is lifted once a loop | good; quiet |
| `reel_in` | 1.2 | loop |  |  | `fishing_rod` (right) |  | the rod up at 48 degrees, its butt held to the right hip by the RIGHT hand; the LEFT hand winds the crank (0.40 m up the rod, under it, on its left side) two turns a loop; leaning back, pumping once a loop | after review: 9 degrees back on a braced front leg, 8 cm crank circle; good |
| `fish_land` | 2.0 | once |  |  | `fishing_rod` (right) |  | from and back to `fish_wait`'s hold: the rod swung up to 76 degrees, a step back with the right foot (it comes back; the root stays), the left hand out to the line 0.36 m ahead, a look at the catch 0.6 m ahead | after review the rod goes up out in front and to the right, clear of the face, head down to the catch; starts and ends exactly in fish_wait's first pose; good |
| `drink_bar` | 3.6 | once |  |  | `drink_glass` (right) | grip 0.50, release 3.00 | at a bar 1.05 m high, the left hand on it: the glass stands 0.35 m ahead and 0.06 m to the right; it is in the right hand only between the marks (stand it on the bar before and after), to the mouth and down | elbow low, forearm steep, head back on the drink after review; good |
| `browse_rack` | 4.0 | loop |  | upper |  |  | at a rail 1.55 m up and 0.50 m ahead: the right hand, hooked over the hangers, draws them from 0.50 m right to 0.27 m right twice a loop; the left holds a garment out 0.21 to 0.26 m to the left, 1.20 m up | after a second review: the stroke is 0.50 to 0.27 m right of the head line, elbow under the hand, shoulders turning 21 degrees with the draw, the garment hand giving 7 cm; adequate, still a small move at head height |
| `try_on_turn` | 3.0 | once |  |  |  |  | arms a little out, looking down at oneself: about fifty degrees round to the left, then to the right, and back; the feet step round, the root does not turn | after review: feet under the hips, head 29 degrees down, hips lead the shoulders, right hand up at the lapel for the turn to the right; adequate; fifty degrees each way |
| `chess_move_player` | 2.4 | once |  |  |  |  | seated as `chess_sit` (stool 0.45 m, table centre 0.62 m ahead), from and back to its first pose: the right hand moves a piece 0.46 m ahead two squares on | feet either side of the table's base, thumb and two fingers pinched after review; good |
| `throw_ball` | 1.4 | once |  |  |  | release 0.80 | a free throw: the ball (not modelled) leaves the right hand 0.33 m ahead and 1.95 m up | set point in front of the forehead, arm near straight at the release, up on the toes, follow-through held after review; adequate with empty hands |

Where the character must be put (additions to that list):

- **Two people greeting** (`social_handshake`, `social_hug`, `social_hug_a` / `_b`, `social_cheek_kiss`, `cheers`): FACING each other, 0.80 m between their origins. Each clip is made to fit its own mirror image, so both play the same id: right hands meet on the centre line; in the hugs each one's left arm is the high one and each head goes to its own left.
- **On the floor** (`pushup`, `situp`): the body is centred on the origin and lies along the character's forward axis, within 0.9 m of it either way (the 0.9 m of clear floor `HomeActivity` asks for).
- **Seated** (`eat_sit`, `sit_chat_a`, `sit_chat_b`, `chess_move_player`): the origin is the middle of the seat at ground level, the seat 0.45 m up, as `chess_sit`.
- **The rod** (`cast_rod`, `fish_wait`, `reel_in`, `fish_land`): in the right hand in all four, by the same `Grip` (0.10 m from the butt), so they cross-fade with the rod in the hand. The clips turn the rod themselves.
- **A long box** (`box_lift_with`, `box_lift_with_b`): 1.20 m long, lying along the line between the two, who stand facing 2.04 m apart.
<!-- end: everyday -->

<!-- module: family -->
### Family: a proposal and its answer, the rings, a cheer, a baby (`scripts\clips_family.py`)

**The code is the specification** for the first five (`Scripts\Family\Courtship.cs` lines 376 and 466, `Wedding.cs` lines 930 and 965); the rest are not asked for by code yet. Distances are from the character's feet (the root): "ahead" is the character's forward, heights are above the ground, x is to the character's right. Two-person clips: each side is its own clip and never moves the root (a "step" moves a foot and the hips, and comes home); place the two as the row says. The blunt column is the author's judgement of the Blender sheets after the review round; none of the changes of that round has been seen on the game's meshes.

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `propose_kneel` | 6.0 | **loop** |  |  | `ring_box` (right) |  | the player's kneel while the question is asked and answered (`JobBody.Clip`: whole body, played until `Rest`, 5.6 s in all). On the right knee (left foot 0.27 m forward, the right knee on the ground 0.03 m ahead of the root, its foot 0.33 m behind), leaning 9.5 degrees to the partner from the hips, the open box on the right palm **0.40 m ahead, 0.90 m up**, tipped 18 degrees to them, the left hand on the left knee, the head up at a face **1.25 m ahead**, 1.60 m up. Once a loop (0.6 to 4.6 s) the box is offered 15 cm higher and 6 cm nearer, the chest lifting and the weight settling 3.5 cm forward; the weight shifts on the knee, the head tilts. The box comes with the clip. Faded into from standing in the code's 0.35 s: there is no going down in it (see `propose_kneel_down`) | good: the lean and the bigger offer make the moments of the loop differ; at the top of the offer the box is at chin height, which is a lot. The cross-fade from standing is the code's |
| `propose_react_yes` | 2.2 | once |  |  |  | hands 0.62, down 1.54 | the partner's yes (`CityPerson.Work`, whole body, returns). **The kneeler is 1.25 m ahead**, their face 1.02 m up. A start, half a step back with the right foot (0.24 m; the hips go 0.085 m back, the trunk rocks 5 degrees back), both hands up level in front of the mouth (palms 0.17 m ahead of the face, elbows down by the ribs, shoulders up), two nods with the hands bobbing 4.5 cm on each, the hands down and the foot home. Starts and ends in the standing idle | good: reads at once; the hands are now well clear of the face on the dummy, so on the game's mesh they should no longer touch it. Busy for 2.2 s |
| `propose_react_unsure` | 2.2 | once |  |  |  | hand 0.66, down 1.54 | the partner's doubt, same places. The weight sinks back on to the right hip (2 cm down), the left foot drawn back 0.13 m, the right hand flat on the middle of the breastbone with the elbow down, the chest turned 13 degrees and the eyes further to the left, off the kneeler; two small shakes of the head, a breath out with the shoulders dropping, a glance back; back to the standing idle | adequate to good: the turn away says it. On the dummy the hand is now at the bottom of the breastbone; whether that is "mid-sternum" on the game's mesh has not been seen |
| `ring_present` | 3.0 | once |  |  |  | take 0.95, ring 1.86, release 2.5 | the player puts the ring on (`JobBody.Clip`, whole body). **The other stands 1.10 m ahead, facing** (`Wedding.Lay`). Half a step in with the left foot; the left hand comes palm up under their left hand **at (0.03, 0.61, 1.05)**, the right brings the ring between finger and thumb and slides it 8 cm along their finger from 1.26 to 1.86 s, both hands close round theirs; at the release the chest and the head lift to their face; back to standing. No prop | adequate: the arms reach further from the body than before, but the two hands still work at one point low in front and the slide is small on a sheet. **It needs the other's hand there: the code leaves the partner in the game's idle (see `ring_receive`)** |
| `cheer` | 3.2 | once |  |  |  | up 0.55, down 2.62 | the partner at "you may kiss" (`CityPerson.Work`, whole body, returns). A dip of 7 cm at 0.3 s, then both arms thrown up in a V with the elbows 20 degrees off straight (wrists to 1.89 m, fists to about 1.98) and up off the heels, three bounces of 4.5 cm with the fists pumping 14 cm in turn, the shoulders tilting 6 degrees each way, the head back; arms up from 0.55 to 2.6 s (the code's photograph is at 2.2 s). Needs 0.5 m clear either side | good: a real dip and straight arms; the arm that is pumping down is bent at that moment, by design |
| `propose_kneel_down` | 2.8 | once, **ends held** |  |  | `ring_box` (right) | kneel 1.74, present 2.5 | not asked for by code yet. From standing into the first pose of `propose_kneel` (exactly): the left foot steps 0.27 m forward from the first frame (0 to 0.56 s), the right goes back on to its toes and its knee down (0.48 to 1.74 s), the box comes up in the right hand as the knee goes down (0.95 to 2.5 s) and the left settles on the knee. Play with `ClipEnd.Hold`, then the loop. The box is in the hand from the first frame, 6 cm clear of the right thigh | good: the step, the drop and the arm now overlap instead of following one another |
| `propose_kneel_up` | 1.8 | once |  |  | `ring_box` (right) |  | not asked for by code yet. From the first pose of `propose_kneel` back to standing on the spot: up off the knee with the box still held up (it rises to 1.21 m), the right foot forward, the left back, then the box lowered to the right side with the elbow kept in | adequate: a little brisk |
| `ring_receive` | 3.0 | once |  |  |  | taken 0.95, ring 1.86 | not asked for by code yet: the other side of `ring_present` (**the giver is 1.10 m ahead, facing**; start both together). Half a step in and a lean over the hand, the left hand held out palm down, wrist level, fingers together, **to (-0.03, 0.49, 1.05)**; it dips as the ring goes on; the right hand comes to the chest; the eyes on the ring and then up | adequate: the left arm is nearly straight now (the forearm was asked further out); the pair has not been seen together; by arithmetic the giver's palm is 3 cm under the offered hand and 4 cm short of its middle |
| `cheer_loop` | 2.4 | loop |  |  |  |  | not asked for by code yet: a crowd's loop. Both fists up in the same straight-armed V, each pumping 14 cm once a second in turn, four bounces off the heels a loop with 5.5 cm of rise (wrists to 1.93 m), the shoulders tilting 7 degrees to the side of the fist that is up. Needs 0.5 m clear either side | good: no longer parked; in a crowd start each one at a different time or they bounce in step |
| `cheer_once` | 1.8 | once |  |  |  | top 0.50 | not asked for by code yet. "Yes!": a dip, then up on the toes with the right fist driven straight up beside the head (wrist 1.96 m, fist over 2.0 m, 0.23 m to the right of the middle) at 0.5 s, the trunk leaning 5 degrees to the left under it, the left fist clenched at the chest; held 0.3 s; back to standing | good: a punch now, not a salute |
| `slow_dance_a` | 8.0 | loop |  |  |  |  | not asked for by code yet: the one who leads. **The partner plays `slow_dance_b` 0.45 m ahead, turned to face.** Left hand holds theirs at (-0.45, 0.225, 1.35). Right hand on the partner's LEFT SIDE 1.25 m up (0.17 m right of centre, 0.50 m ahead), fingers round to the back, elbow down. Two slow sways a loop, the pelvis 5.5 cm each way with the shoulders tilting 3.5 degrees against it and the hands going with the body, the free foot lifted 2 cm and set down | adequate: the sway is in the trunk now and shows from the front; still a sway, not a dance, and the hand is on the partner's flank, not across the back (out of reach at 0.45 m) |
| `slow_dance_b` | 8.0 | loop |  |  |  |  | not asked for by code yet: the one who follows. **The partner plays `slow_dance_a` 0.45 m ahead, turned to face.** Right hand in theirs at (0.45, 0.225, 1.35); left hand over the partner's right shoulder, 1.45 m up and 0.51 m ahead, elbow hanging. **Start both clips at the same moment**: each is the other's mirror half a sway apart | adequate, as `_a`; the hand over the shoulder is placed by the skeleton's shoulder joint and has not been seen on the game's mesh |
| `hold_baby` | 8.0 | loop |  | upper | `baby_bundle` (left) |  | not asked for by code yet. The bundle in the crook of the left arm against the chest: its middle 0.19 m ahead and 1.19 m up, **head to the holder's LEFT** at 1.31 m; the left palm flat under it 0.10 m towards the feet from its middle (the grip the bundle hangs on), the right hand on its legs with the elbow down by the ribs, patting between 4.5 and 6.5 s; the trunk sways 7 degrees each way twice a loop; the head is bent to it with a look up at 2.2 to 3.2 s | adequate, unproven: raised 12 cm and brought 5 cm in as the review asked; on the dummy the bundle is now high on the chest and the right hand presses 2 cm into the blanket, which should be "on it" if the game's bundle hangs lower than the dummy's, as the review saw |
| `rock_cot` | 6.0 | loop |  |  |  |  | not asked for by code yet. Bent over the long side of `cot` from the hips, both palms closed on its rail: **the rail is 0.35 m ahead, 0.885 m up, the hands 0.21 m either side**, so the cot's centre is 0.68 m ahead and the cot is turned a quarter turn (its length across the character). The hands push the rail 0.028 m away and back twice a loop (rail y = 0.35 + 0.028 sin(2 pi t / 3 s)), the hips going 1.5 cm fore and aft with it: move the cot with it or the hands slide on the rail | adequate: a lean with nearly straight legs now; the push (5.6 cm in all) shows only if the cot moves with it |
<!-- end: family -->

<!-- module: garage -->
### Garage: riding bicycles and motorbikes (`clips_garage.py`)

Made to how `Scripts/Garage/BikeMount.cs` plays them: one loop by what the rider is doing (riding, freewheeling, stopped: `BikeDef.Ride`, `Coast`, `Stand`), each model's own, whole body, at the clip's own speed, with the rider's origin put at the vehicle's (0, 0, seat z + 0.04). So in all ten **the character's origin is on the ground 0.04 m ahead of the middle of the seat**, facing the bike's front, and the root never moves; the bike's own origin is 0.26 m ahead of it on `bicycle_city`, 0.25 m on `bicycle_road`, 0.16 m on `motorbike_standard`, 0.18 m on `motorbike_sport`. Points below are in Unity axes from the character's origin (x right, y up, z forward). The Hips bone rides 0.058 m above a saddle's top and 0.085 m above a motorbike's seat; the middle of each palm is on the grip's axis (grips lie along X). After the clip the code aims the game's hand rig at its own grips (the same points) and, on a bicycle only, puts both ankles 0.10 m above and 0.03 m behind the pedals' middles each frame, keeping the feet's turn from the clip, except while a `_stand` clip plays (the legs are then the clip's, and the code brings the cranks round to the right pedal forward); on a motorbike the feet are the clip's. The helmet follows the Head bone, turn and all (it used to keep the player's rotation, which is why the motorbike clips look no more than 9 degrees aside). No tool: the bike is `dressing` only (drawn under the rider on the sheets, where the code has it).

| clip | s | loop or once | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `cycle_ride` | 4.0 | loop |  | Garage: `BikeMount`, the town bicycle pedalling |  |  | seated upright on `bicycle_city`: saddle top (0, 0.93, -0.04), palms on the grips (+-0.26, 1.14, 0.42), back 12 degrees forward, a slow sway, a short look right and one left with the shoulders following 4 degrees. The legs turn the cranks five times (75 a minute; bottom bracket (0, 0.29, 0.18), 0.17 cranks, pedals 0.13 out, the right crank forward at 0 s and going down) with the ankles where the code's `LegIk` puts them: **the code replaces the legs every frame** and keeps only the feet's 8 degrees toes-down; the rider sits low enough (0.058 over the saddle) that the code's ankle leaves the knee 21 degrees of bend at the bottom of the stroke (159 degrees at its straightest). | good: reads as town cycling at once, hands on the grips, elbows in beside the body, the knee never locks. The upper body is quiet (a 4 degree shoulder turn with each look). The modelled cranks and pedals now go round under the feet (the bike is in parts) |
| `cycle_coast` | 4.0 | loop |  | Garage: the town bicycle rolling without pedalling |  |  | freewheeling on `bicycle_city`: cranks level, right foot forward (exactly `cycle_ride` at 0 s), a look right and a look left, the trunk and shoulders turning 9 degrees with each and the weight going 1.4 cm across the saddle | fine: no longer a held pose, the turn of the shoulders reads from the front; the code's `LegIk` holds the feet on the pedals where the crank stopped, so only the trunk and head of it are seen |
| `cycle_stand` | 6.0 | loop |  | Garage: the town bicycle stopped |  |  | stopped astride `bicycle_city`, off the saddle and standing just ahead of its nose (hips 0.16 ahead of the origin): left foot flat on the ground at (-0.21, 0, 0.20), right foot on its pedal (forward, level), hands on the grips, pelvis and trunk tilted 4.5 degrees over the grounded foot, the shoulders turning 9 degrees with each look | PASSABLE: reads as stopped astride a bike with the weight on one leg; the elbows are now down along the ribs instead of winged, but still folded to 70 degrees because the code's grips are only 0.25 m ahead of the trunk (nothing in the clip can cure that). Step-through frame only (the road bicycle has `cycle_stand_road`); the code fades its `LegIk` out for it |
| `moto_ride` | 4.0 | loop |  | Garage: `BikeMount`, the roadster moving |  |  | riding `motorbike_standard`: seat top (0, 0.83, -0.04), arches on the pegs (+-0.24, 0.34, 0.06), palms on the grips (+-0.31, 1.075, 0.46), knees held at (+-0.215, 0.65, 0.29) against the tank's flanks, back 12 degrees forward; the hips go 2.5 cm to either side of the seat with the shoulders 3 degrees against them, the back gives twice a loop and the elbows flex with it, the shoulders turn 4 degrees into each 9 degree look. | good: sits it like a roadster, elbows soft; the seat shift and the give of the elbows now show on the sheet, the head scan stays small (the helmet limit) |
| `moto_ride_sport` | 4.0 | loop |  | Garage: the sport bike moving |  |  | on `motorbike_sport`: seat top (0, 0.84, -0.04), balls of the feet on its pegs (+-0.24, 0.38, 0.02), palms on its clip-ons (+-0.27, 0.915, 0.60), back 45 degrees forward over the tank (33 at the hips, 12 more in the chest), elbows tucked and low, the head up; the same seat shift, give and scan as `moto_ride` | REMADE for the clip-ons (the grips were 0.135 higher and 0.10 nearer) and seen only on the Blender sheet, without the bike drawn: a crouch over the tank with the knees high; within reach throughout (no stretch). Not seen in Unity |
| `moto_stand` | 6.0 | loop |  | Garage: the roadster stopped |  |  | stopped on `motorbike_standard`, sitting to the left of the seat: pelvis 0.06 to the left and 0.04 down, trunk leaning 5 degrees over the planted foot, left foot flat on the ground at (-0.30, 0, 0.15) (beside and ahead of its peg) with the knee at 150 degrees, right foot on its peg, hands on the grips with the elbows low; shoulders turn 4.5 degrees with the looks. The bike is upright under them | fine: now reads as a rider holding the bike up on one leg, not as `moto_ride` with a foot dropped; the lean is over a bike that the code keeps upright |
| `cycle_ride_road` | 4.0 | loop |  | Garage: the road bicycle pedalling |  |  | on `bicycle_road`, hands on the TOPS of its drop bar: saddle top (0, 0.95, -0.04), palms (+-0.13, 1.00, 0.67), back 42 degrees forward (30 at the hips, 12 in the chest), elbows narrow under the shoulders, head up along the road; legs and looks as `cycle_ride` | NEW, seen only on the Blender sheet without the bike drawn: reads as a road rider leaning to a narrow bar; the hands are 0.26 m apart, which is what riding on the tops is. 5 mm of stretch at the far point of the stroke. Not seen in Unity |
| `cycle_coast_road` | 4.0 | loop |  | Garage: the road bicycle rolling |  |  | `cycle_ride_road` with the cranks level, right foot forward, the back 2 degrees higher, a look right and one left with the shoulders following 7 degrees | NEW, as `cycle_ride_road` |
| `cycle_stand_road` | 6.0 | loop |  | Garage: the road bicycle stopped |  |  | astride `bicycle_road` ahead of the saddle's nose, over its sloping top tube: hips (-0.075, 0.90, 0.175), left foot flat on the ground at (-0.22, 0, 0.21), right on its pedal (forward, level), leaning 20 degrees on to the tops | NEW, sheet only. The top tube's top is 0.84 m up under the rider and the hip joints 0.90: the tube is some 2 cm into the crotch between the thighs (the code keeps the bike upright; a real rider leans it away). Whether that shows can only be seen in the game |
| `moto_stand_sport` | 6.0 | loop |  | Garage: the sport bike stopped |  |  | `moto_stand` on `motorbike_sport`: left foot flat on the ground at (-0.30, 0, 0.13), right on its peg, hands on the clip-ons with the arms nearly straight, back 37 degrees forward | NEW, sheet only |

Dropped: `cycle_pedal` (now `cycle_ride`). The road bicycle has a drop bar and clips of its own again (`_road`).
<!-- end: garage -->

<!-- module: site -->
### Civic life and the courts (`scripts\clips_site.py`)

`litter_pick` is asked for by code (`Scripts\Justice\CommunityService.cs` lines 31 and 190: played whole-body on the player with `ActivityAnimator.Play`, 1.8 s, the litter switched off 1.1 s in; no tool is attached, so the hand is bare). `speech_gesture` is on Civic's wish-list: the code plays `talk_a` on a rally's speaker today (`Scripts\Civic\CampaignWorld.cs` line 508), whole-body and looping, standing on `soapbox`. The other two are not asked for by code. Distances are from the character's feet: "ahead" is the character's forward, heights are above the ground. The game's hand has three fingers, so every open hand here keeps them together (spread, they read as a V).

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `litter_pick` | 1.8 | once |  |  |  | grip 1.10 | picking litter off the ground with the bare right hand: a step forward with the left foot (0.27 m) and down into a squat between the knees, the right heel up, the chest kept up (36 degrees forward at the hips) and the head with it, the left forearm along the left thigh; the right shoulder dips and the fingers close on something **on the ground 0.55 m ahead**, 0.05 m right of the middle, at 1.1 s; back up to standing where one stood. Nothing is held | adequate to good on the Blender sheet after the review (a squat with the chest up, no longer folded flat; it moves from the first frame); not seen again in Unity. Where the hand goes afterwards (there is no bag) is left to the viewer |
| `speech_gesture` | 8.0 | loop |  |  |  |  | a speaker to a crowd, standing (on `soapbox`: the feet stay within 0.36 x 0.26 m), listeners 2.3 m ahead and 0.30 m lower, 1.15 m apart: the right forefinger up 0.3 m clear of the head and jabbed 0.2 m forward (0.7 to 2.2 s), both arms opened wide at chest height, palms up (3.0 to 4.3 s), the edge of the right hand down on the left palm three times (5.2 to 6.7 s), hands down for a breath; each beat begins as the one before lets go; the weight changes feet four times a loop; the head goes along the listeners. Needs 0.65 m clear either side | good on the Blender sheet after the review: three gestures that can be told apart, no dead frame between them; a little stagey, and the same eight seconds every time. Not seen again in Unity |
| `oath_hand` | 3.0 | once |  | upper body |  | raised 0.60, lower 2.20 | not asked for by code (Justice's `court_swear` is this hand). The right hand raised beside the shoulder with the forearm upright over the elbow, which hangs at the ribs (palm 1.54 m up, 0.40 m to the right, 0.22 m ahead: 0.3 m clear of the head), palm square to the front, fingers together, held from 0.6 to 2.2 s, lowered | good on the Blender sheet after the review: an oath, not a cupped ear. Not seen again in Unity |
| `podium_speak` | 8.0 | loop |  | upper body |  |  | not asked for by code. Leaning a little on a lectern 1.10 m up whose middle is 0.40 m ahead: the hands flat on its top 0.19 m either side with the fingers over the far edge, the elbows hanging below the shoulders; the right hand comes up as an open palm at 1.4 s, the left as an open hand on edge at 5.2 s; the head goes round the audience | adequate on the Blender sheet after the review (elbows down, flat hands, open-palm points); it still needs a lectern nobody has made, and without one the resting hands lie on air. Not seen again in Unity |
<!-- end: site -->

### Building sites (`scripts\clips_build.py`)

Both are asked for by code (`Scripts\Civic\SiteDresser.Plot.cs`: the frame stage's carpenter and the slab stage's concrete
gang), which plays `kneel_work` and `shovel_dig` in their place while the anim bundle does not have them (`ClipLibrary.Has`).
**Not in the anim bundle yet:** the bundle could not be built when they were written (no Unity licence on the machine);
`build.ps1` puts them in with the rest. Reach checked with `check_clips.py` (stretch 0.012 and 0.025 m).

| clip | s | | m/s | made for | tool (grip) | marks (s) | what it is, and where things are | blunt |
|---|---|---|---|---|---|---|---|---|
| `hammer_nail` | 1.6 | loop |  |  | `hammer` of the second props bundle (right, 0.10 m up the handle) |  | on one knee, two blows a loop at a nail 0.44 m ahead, 0.08 m right and 0.32 m up (a board at the frame's foot), the left hand holding it; the hammer rises 50 degrees from the wrist and elbow and comes down onto the nail | adequate on the Blender sheet with a plain bar for the hammer, by its author only: the swing is small at street distance. Not seen in Unity |
| `rake_asphalt` | 2.6 | loop |  |  | `rake` (right 0.95 m, left 1.30 m, as `rake_leaves`) |  | levelling: the rake pushed out flat to 0.82 m ahead and drawn back to 0.50 m, the tines on the ground throughout, the lean from the hips doing the work | adequate on the Blender sheet, by its author only. Not seen in Unity |

Clips that hold the same tool share its `Grip`, so they can be cross-faded into each other with the tool in the hand.

### Where the character must be put (the numbers a clip assumes)

All from the character's feet: right, ahead, up.

- **A push rail** (`push_cart`, `pull_cart`): 0.44 m ahead, 0.95 m up, hands 0.45 m apart. `stretcher_push_bar` and
  `pushchair` are modelled to it. For a walking character use the ARMS of these clips over the game's own walk
  (`PlayOver(clip, BodyMask.Arms)`) and nothing can slide; the whole-body clips are for when the game's walk is not
  wanted, with `MatchSpeed`.
- **Into a car** (`job_car_enter`, `_l`, `job_car_exit`, `_l`): the character does not move. Stand it FACING THE
  DOOR, feet on the ground 0.75 m out from the middle of the seat on the line square to the car through the seat.
  `job_car_enter` is for a door on the car's right-hand side (the nose is to the character's right), `_l` for the
  left. The clip carries the hips to 0.71 m ahead and 0.60 m up, turned to face the nose, feet on a floor 0.30 m
  up. The exits are the same backwards. Made without a car to fit to: expect to tune the 0.75.
- **A counter** (`serve_counter`, `counter_wipe`, `counter_reach`, `pay_tap`, `till_tap`): the top is 0.975 m up.
  The vendor's cup goes down 0.78 m ahead of the vendor; a customer's hand closes on a cup 0.62 m ahead of the
  customer. On the carts (counter 0.80 m deep) that is the vendor 0.68 m behind the cart's middle and the customer
  0.72 m in front of it.
- **A box** (`job_receive_box`, `carry_box_hold`, `carry_box_walk`, `job_load_boot`, `box_set_down`, `box_pick_up`):
  `parcel_m` (0.38 m wide), a hand under each end. The boot floor is 0.52 m ahead and 0.70 m up; the ground spot
  0.40 m ahead.
- **The ladder** (`paint_roll`): feet on the FIRST tread of `step_ladder` (0.24 m up). Put the character 0.24 m
  behind the ladder's pivot, facing the wall, which is 0.80 m ahead of the character.
- **The chess table** (`chess_sit`): the character at the centre of a stool, facing the table (centre 0.62 m ahead).
- **Worn and wheeled things** are not in a hand: `mail_satchel` hangs on the UpperChest bone at local zero;
  `hand_truck` leans back 38 degrees with its axle 1.09 m ahead (`CourierActivity` has the numbers).

## Add a clip

0. Read the head of `scripts\motion.py` first: bend at the hips (`stand(lean=...)`, never a big `chest(pitch=...)`),
   weight from foot to foot (`idle`), the hand a moment behind the body (`lag`), walking with `Walk`.
1. Write a function in the right `scripts\clips*.py` (copy the nearest) and add a `Clip(...)` to that file's list. A looping clip is
   built from whole turns of `t` so that it comes back to where it started. For a tool, make the tool's pose first
   (`shaft(foot, towards)` for anything long) and hand it to `p.tool(...)` with its `Grip`s: the first grip is the
   hand the game hangs the tool on, a second is the hand the solver keeps on it.
2. `python -I scripts\check_clips.py <name>` until nothing is out of reach, then
   `blender -b -P scripts\build_clips.py -- <name>` and look at `previews\<name>.png`.
3. `build.ps1`, look at `previews\unity_<name>.jpg`, add a row to the table above.
4. A new tool is a prop: `PropAssets\scripts\city_props.py` (it must agree with the clip about where it is held).

Motion from elsewhere must be verifiably CC0 (record it in `CREDITS.md`): drop its FBX in with a row in
`clips.json`'s format and it goes through the same Humanoid import. Mixamo is not CC0 and is not acceptable.

## What the game already has (and the mod reuses)

From `inventory_game_animations.py` (the full list is its JSON output). The street character's controller,
`CharacterAnimator`, has 14 layers, 120 parameters and 134 clips; the game's pooled pedestrians use the much smaller
`CharacterAnimator_Pedestrians` (18 parameters).

- Looping poses (`PermanentAnimationType`, a bool each): `TalkingPhone` 2.9 s, `TextingPhone` 1.3, `Sitting`,
  `ReadingABook` 4.8, `Drunk` 16.3, `HoldingAnItem`, `CleaningIdle`/`Cleaning` (the mop) 2.0, `HammerIdle` 2.0,
  `PlayingGuitar` 6.2, `Singing` 28, `PlayingBucketDrums` 2.9, `Juggling` 1.7, `CaricaturePainting` 11.6,
  `FactoryWorking`, and the gym, hairdresser, casino and pool sets.
- One-off gestures (`AnimationType`, a trigger each): `Drink` 2.7 s, `ConsumeFood` 3.5, `ThrowingTrash` 3.1,
  `UsingCashRegister` 2.7, `SweatRemove` 3.1, `StretchArms` 4.6, `IDontThinkSo` 1.8, `HammerHitting` 1.3, `Faint` 4.7.
- Hand props by name (`AddHandObject`): `PhoneOff`, `PhoneOn`, `Umbrella`, `OpenBook`, `Mop`, `GlassOfBeer`,
  `IceCream`, `DrunkBottle`, `DealerPackOfCards` are the ones the game's own code hands out. Prefabs named
  `Cigarette`, `Guitar`, `PaperBag`, `DeliveryBackpack` exist too; how they sit in a hand has not been looked at.
- The game already has street performers (guitar, singing, bucket drums, juggling, caricatures) with music and a
  donation bucket the player can tip into. The mod gives them an audience instead of adding buskers of its own.
- Not on the street characters: clapping (only in the cinema's controller), the hand truck (its own animator).

## Known limits

- **Animator layers.** The game's controller has 14 layers (fingers, a "Fat" layer that holds a heavy character's
  arms wider). A clip of ours at full weight replaces all of them. The city life people are therefore kept to slim
  and average builds (`CityPeople.MaxBuild`); on a heavy character hands and tools would sink into the belly.
- **Clothes.** Clips are made to the skeleton, not to the meshes. Loose garments, bags and big hair can still be
  passed through by an arm or a tool; that can only be seen in the game.
- **The ground.** Baked into the pose, with foot IK off: on a slope a kneeling knee or a broom's bristles can be a
  few centimetres above or below the surface.
- **Walking loops** are made for one speed each. `MatchSpeed` keeps the planted foot honest at others, but the gait
  is the same gait played faster or slower: below about half and above about one and a half times its speed it
  looks wrong. Where the game's own walk will do, prefer it with `PlayOver`.
- **`PlayOver` and the game's 14 animator layers.** On the stand-in the masked layer leaves the rest of the body
  within 8 mm. The game's controller has finger and "Fat" layers of its own; what those do to an arm the mask has
  taken can only be seen in the game.
- **The car clips** were made without a car: where the seat, the sill and the door handle really are on any of the
  game's vehicles has not been measured.
