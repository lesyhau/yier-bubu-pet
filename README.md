# Yier & Bubu desktop pet

一二 (Yier, the white panda) and 布布 (Bubu, the brown bear) live on top of your Windows taskbar.
They walk around, react when you click them (with little sound effects), can be picked up and dropped,
come together for a hug every now and then, and fall asleep when you've been away from the PC for a while.

> **Unofficial fan project.** Yier & Bubu (一二布布) belong to their creators. This repository contains
> no character artwork. On first run the app offers to download a starter set of stickers from
> [Tenor](https://tenor.com), for your personal use.

## Download

Get the latest version from [Releases](https://github.com/lesyhau/yier-bubu-pet/releases/latest).

**Installer (recommended):** `YierBubuPet-<version>-setup-x64.exe` for most PCs (Intel/AMD), or
`...-setup-arm64.exe` for ARM PCs (Snapdragon, Surface Pro X). It installs for your user account
without needing admin rights, adds a Start menu entry, can start the pets when you sign in, and can
be removed from **Settings → Apps**.

**Portable exe:** `YierBubuPet-win-x64.exe` / `YierBubuPet-win-arm64.exe`. Put it in a folder of its
own (an `animations` folder is created next to it) and run it.

Nothing else needs to be installed. Windows may say "Windows protected your PC" because the files aren't
code-signed; click **More info → Run anyway**.

Works on Windows 10 and 11 (developed on Windows 11).

## Building from source

Needs the [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

```
git clone https://github.com/lesyhau/yier-bubu-pet.git
cd yier-bubu-pet
dotnet publish src\YierBubuPet -c Release -o app
app\YierBubuPet.exe
```

Or open `YierBubuPet.sln` in Visual Studio. Dev builds use the `animations\` folder at the repository root.

To build a single self-contained exe like the ones on the Releases page (use `win-arm64` for ARM):

```
dotnet publish src\YierBubuPet -c Release -r win-x64 --self-contained -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -o dist\win-x64
```

On first run, the app sees that `animations\` is empty and offers to download the starter pack
(75 stickers, about 5 MB, plus the default sound effects). You can also run that download from the
tray menu, or from the command line:

```
app\YierBubuPet.exe --starter-pack animations
```

### Building the installer

Needs [Inno Setup 6](https://jrsoftware.org/isdl.php) (6.3 or newer) as well. In PowerShell:

```
.\installer\build.ps1                    # x64
.\installer\build.ps1 -Arch x64, arm64   # both
```

This publishes the single-file exe to `dist\win-<arch>\` and compiles
[`installer/YierBubuPet.iss`](installer/YierBubuPet.iss) into `dist\YierBubuPet-<version>-setup-<arch>.exe`.
The version comes from `<Version>` in `src/YierBubuPet/YierBubuPet.csproj`. You can also open the
`.iss` file in the Inno Setup IDE and compile it there after publishing.

Where things go when installed:

| What | Where |
|---|---|
| The app | `%LocalAppData%\Programs\YierBubuPet` (or `Program Files\YierBubuPet` if you choose "all users") |
| Animations and sounds | `animations\` next to the app; for an all-users install, `%LocalAppData%\YierBubuPet\animations` |
| Settings | `%AppData%\YierBubuPet\settings.json` |

Uninstalling asks whether to delete the animations folder too; by default it's kept.

## Using it

- **Click** a pet to make it react.
- **Drag** a pet to pick it up; let go and it falls back down to the taskbar.
- **Right-click** a pet, or use the tray icon, for the menu:
  - **Together time 💕**: they walk to each other and play a "together" animation (double-clicking the tray icon does the same)
  - **Bedtime 💤 / Wake up**: they also fall asleep by themselves after 5 minutes without keyboard or mouse input
  - **Size**: Small / Medium / Large / Huge
  - **Sound**: Off / When I click or drag them / For everything they do, and Quiet / Normal / Loud
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

### Sounds

Sounds live in the same folders as the animations. Supported formats are **WAV, MP3, WMA and M4A**.

- A sound with **the same name as an animation** plays with that animation only:
  `bubu\click\wave.webp` + `bubu\click\wave.mp3`.
- Any **other sound in a folder** plays with the animations in that folder that don't have their own
  (one is picked at random if there are several). For example, `yier\click\boop.wav` plays whenever
  Yier reacts to a click.
- A sound plays once when its animation starts and stops when the next animation starts.
  Put `[loop]` in a sound's file name to repeat it for as long as the animation is showing.

By default sounds only play for things you do (clicking, dragging, **Together time**, **Bedtime**),
so the pets don't make noise on their own. Choose **Sound → For everything they do** in the menu to
hear them all the time, or **Off** to mute them.

The starter pack comes with a few sound effects (a boop for clicks, a slide whistle when picked up, a
chime for hugs and a snore for bedtime). They're generated by `tools\make_sounds.py`, and the source
files are in [`assets/sounds`](assets/sounds). Delete or replace them as you like; the app only adds
default sounds to folders that have no sounds at all, when you use **Download starter stickers**.

### Previewing animations

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

```
YierBubuPet.sln
src/YierBubuPet/        the app
assets/sounds/          default sound effects (embedded in the exe)
installer/              Inno Setup script and build script
tools/                  sticker downloader, sound generator
animations/             your animations and sounds (not in git)
```

Inside `src/YierBubuPet/`:

| File | Role |
|---|---|
| `Program.cs` | Entry point, single-instance check, command-line options, finding the animations folder |
| `Stage.cs` | Frame loop, bringing the pets together, sleep/wake, tray menu |
| `Settings.cs` | Saved settings (size, speed, sound) |
| `Pets/Pet.cs` | One character's behaviour: idle → walk → react, dragging and falling |
| `Pets/SpriteWindow.cs` | A borderless, transparent, always-on-top window that plays one animation. Clicks pass through transparent pixels. |
| `Animation/AnimationLibrary.cs` | Finds animations and their sounds by folder, decodes in the background, caches, reloads when files change |
| `Animation/ClipDecoder.cs` | Decodes GIF/WebP/PNG with [SkiaSharp](https://github.com/mono/SkiaSharp), removes solid backgrounds, trims and scales |
| `Audio/SoundChannel.cs` | Plays the sound that goes with a window's current animation |
| `StarterPack/` | The starter sticker list, its downloader, and installing the default sounds |
| `Platform/` | Win32 calls, screens and DPI |
| `Resources/` | App icon and manifest |

## License

The code and the sound effects in `assets/sounds` are [MIT](LICENSE) licensed. That doesn't cover the
characters or any downloaded artwork.
