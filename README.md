# Yier & Bubu desktop pet

一二 (Yier, the white panda) and 布布 (Bubu, the brown bear) live on top of your Windows taskbar.
They walk around, react when you click them, can be picked up and dropped, come together for a hug
every now and then, and fall asleep when you've been away from the PC for a while.

> **Unofficial fan project.** Yier & Bubu (一二布布) belong to their creators. This repository contains
> no character artwork. On first run the app offers to download a starter set of stickers from
> [Tenor](https://tenor.com), for your personal use.

## Requirements

- Windows 10 or 11 (developed on Windows 11)
- [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0) to build

## Getting started

```
git clone https://github.com/lesyhau/yier-bubu-pet.git
cd yier-bubu-pet
dotnet publish -c Release -o app
app\YierBubuPet.exe
```

On first run, the app sees that `animations\` is empty and offers to download the starter pack
(75 stickers, about 5 MB). You can also run that download from the tray menu, or from the command line:

```
app\YierBubuPet.exe --starter-pack animations
```

## Using it

- **Click** a pet to make it react.
- **Drag** a pet to pick it up; let go and it falls back down to the taskbar.
- **Right-click** a pet, or use the tray icon, for the menu:
  - **Together time 💕**: they walk to each other and play a "together" animation (double-clicking the tray icon does the same)
  - **Bedtime 💤 / Wake up**: they also fall asleep by themselves after 5 minutes without keyboard or mouse input
  - **Size**: Small / Medium / Large / Huge
  - **Hide pets**, **Open animations folder**, **Reload animations**, **Download starter stickers**
  - **Start with Windows**
  - **Exit**

Settings are saved in `%AppData%\YierBubuPet\settings.json`.

## Adding your own animations

Animations are plain files in `animations\`. The folder decides when each one is used:

| Folder | Used when |
|---|---|
| `yier\idle`, `bubu\idle` | standing around (a character appears once it has at least one idle animation) |
| `yier\walk`, `bubu\walk` | walking (mirrored automatically to face the walking direction) |
| `yier\click`, `bubu\click` | clicked, and sometimes just for fun |
| `yier\drag`, `bubu\drag` | being carried or falling |
| `yier\sleep`, `bubu\sleep` | asleep, when there's no `together\sleep` animation |
| `together\play` | hugging or playing together (one file showing both characters) |
| `together\sleep` | sleeping together |

Supported formats are **GIF, animated WebP and PNG**. Drop a file into a folder and it's picked
up within a couple of seconds, with no restart needed.

- Transparent stickers look best. GIFs on a plain white (or other single-colour) background work too,
  because the background is removed automatically. GIFs with a full scene behind the characters are kept as a rectangle.
- Empty space around the character is trimmed, and everything is scaled to the same height, so any size works.
- Walk animations are assumed to face left (or the viewer). If one faces right, put `[right]` in its file name.

To preview how a file will be processed (background removed, trimmed), write its frames out as PNGs:

```
app\YierBubuPet.exe --dump some.gif out_folder
```

### Finding more stickers

`tools\fetch_tenor.py` (Python 3, no extra packages) downloads transparent stickers from a Tenor search into `incoming\`:

```
python tools\fetch_tenor.py "bubu dudu dance" --limit 20
```

Look through them and move the ones you like into the right folder. In English the characters are
usually tagged "bubu dudu". Try "bubu dudu sleep", "bubu dudu hug", "bubu dudu walk" or "yier bubu".

## How it works

| File | Role |
|---|---|
| `Program.cs` | Entry point, single-instance check, command-line options |
| `Stage.cs` | Frame loop, bringing the pets together, sleep/wake, tray menu |
| `Pet.cs` | One character's behaviour: idle → walk → react, dragging and falling |
| `SpriteWindow.cs` | A borderless, transparent, always-on-top window that plays one animation. Clicks pass through transparent pixels. |
| `AnimationLibrary.cs` | Finds animations by folder, decodes them in the background, caches them, reloads when files change |
| `ClipDecoder.cs` | Decodes GIF/WebP/PNG with [SkiaSharp](https://github.com/mono/SkiaSharp), removes solid backgrounds, trims and scales |
| `StarterPack.cs`, `StarterPack.json` | The starter sticker list and its downloader |

## License

The code is [MIT](LICENSE) licensed. That covers the code only, not the characters or any downloaded artwork.
