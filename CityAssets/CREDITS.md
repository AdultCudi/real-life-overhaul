# City assets: sources and licences

## Animation clips (`Bundles/reallife_anim`)

Every clip in the bundle was authored for this mod, by script, in `scripts/clips.py`, `clips_scenes.py`,
`clips_jobs.py` and `clips_life.py`: poses are computed from
positions (`scripts/rig.py`), baked onto a rig built by the same scripts, exported with Blender and imported by
Unity as Humanoid clips. No motion capture, no third-party animation, no clip from the game or from any pack is in
the bundle. Licence: the repository's own (see `LICENSE`).

| clips | source |
|---|---|
| all 77 (`sweep` ... `chess_sit`, see README.md) | written here; nothing borrowed |

No third-party motion was used, and none was evaluated: a clip has to match the tool in the hand and the reach
of this character, which is what writing them here guarantees. Mixamo is not CC0 and must not be used. If CC0
motion is added later, check the licence at its source and record the pack, its URL and where it says CC0 here,
per clip.

## Measurements of the game (`scripts/game_rig.json`)

Joint positions of the game's street character in its T-pose, the turn of its two hand bones there, and its bone
names and hierarchy, read from the installed game with `scripts/extract_game_skeleton.py`. Numbers and names only,
kept so that the clips can be rebuilt and tested without the game. No mesh, texture, animation or other asset of
the game is in this repository or in the bundle.

## Tools

Blender (GPL; used as a tool, nothing of Blender's is shipped), Unity 2022.3.62f2, UnityPy (MIT; used only by the
two scripts that read the installed game for measurements).
