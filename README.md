# Mirror's Edge Tweaks (Linux / Bazzite port)

A native Linux port of [softsoundd/MirrorsEdgeTweaks](https://github.com/softsoundd/MirrorsEdgeTweaks)
(v4.4.2) targeting the **Steam version of Mirror's Edge running under Proton**.
The WPF/MaterialDesign UI was ported to **Avalonia 11**; the patching engine
(UELib package patching, exe byte patches, ini editing, OpenAL installation)
is carried over from upstream unchanged.

## What's different from the Windows original

- **Steam/Proton aware** — auto-detects the game across Steam libraries
  (native and Flatpak Steam), and resolves the game's "My Documents" config
  folder inside the Proton prefix
  (`steamapps/compatdata/17410/pfx/drive_c/users/steamuser/Documents`).
- **Game launching** goes through the Steam client (`steam -applaunch 17410 …`)
  so Proton, the overlay, and Steam DRM work; launch arguments are passed through.
- **Game language registry keys** are written into the Proton prefix's
  `system.reg` instead of the Windows registry (close Steam/the game first).
- **PE version detection** is done by parsing the executable's
  VS_FIXEDFILEINFO directly, since FileVersionInfo can't read Win32 version
  resources on Linux.
- Settings are stored in `~/.config/MirrorsEdgeTweaks/metweaksconfig.ini`.
- EA App/OOA decryption and the WinForms folder browser were replaced or dropped.

## Building

```sh
dotnet publish -c Release -r linux-x64 --self-contained true -p:PublishSingleFile=true
```

Requires the .NET 8 SDK. The output is a single self-contained binary; no
runtime needs to be installed (works on immutable distros like Bazzite).

## Notes

- The game must be launched once through Steam before config-based tweaks are
  available (the Proton prefix and ini files are created on first run).
- For the OpenAL Soft audio backend, Proton may need
  `WINEDLLOVERRIDES="openal32=n,b" %command%` in the game's Steam launch options.
- `tools/` contains the scripts used to mechanically convert the upstream WPF
  XAML/code-behind to Avalonia, useful when rebasing onto a newer upstream release.

Upstream project by softsoundd (MIT). Port generated with Claude Code.
