# `Scripts/Photos`

The Camera and the Gallery on the phone: the photos of a save, favourites, the phone's wallpaper from a photo, and sharing to Flare. Every file is read and written off the main thread; nothing goes into the save.

## Read first

- `PhotosDirector.cs`: the two apps kept going.
- `CameraMode.cs`: the camera itself.
- `GalleryRules.cs`: the Gallery (game-free).
- `PhotoStore.cs`: the save's photos on disk.
- `CameraRules.cs`, `PhotoPixels.cs`, `PhotoSeenRules.cs`: the camera's tools, the pixel work on the worker (cut, frame, grade, turn) and what the lens saw (game-free).
- `GalleryQuery.cs`: the albums, search, filters and sort (game-free); the kit's face is `Ui/Life/GalleryScreen*.cs`, the picker for Flare's post sheet `Ui/Flare/FlareScreen.Photos.cs`.
- `PhotoThumbCache.cs`: the small copies, only the photos in view, at most 120.
- `Cinematic/CineDirector.cs`: the cinematic shots, a developer's tool that takes the mod's own pictures ([Cinematic shots](../../../../../docs/features/cinematic-shots.md)); `Cinematic/CineRules.cs` is its game-free part.

## Documentation

How it works, its rules and its tests: [Wiring (PhotosLinks.cs)](../../../../../docs/reference/wiring.md).
How the code is laid out, and how features meet: [Architecture](../../../../../docs/reference/architecture.md).

Maintained by the Real Life Overhaul maintainer.
