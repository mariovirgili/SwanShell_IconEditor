# SwanShell Icon Editor

A small Windows editor for the **file icon sets (`.ICN`)** of the
[swanshell](https://github.com/asiekierka/swanshell) menu on the
**nileswan** flash cartridge for WonderSwan / WonderSwan Color.

Single C# source file, WinForms, .NET Framework 4.x (built into Windows 10 and 11):
nothing to install, no dependencies. No binaries are published: you build the
program on your PC with `build.bat`.

## Build and run

1. Download the repository (green **Code** button → **Download ZIP**).
   Before extracting, right-click the ZIP → Properties → **Unblock**,
   so Windows doesn't ask to confirm `build.bat`.
2. Double-click **`build.bat`**. It compiles `NileSwanIconEditor.exe` with the
   C# compiler that ships with Windows (`csc.exe` of .NET Framework 4.x),
   adds the program icon (stored base64 at the end of `build.bat`, extracted
   with `certutil`) and starts it.
3. From then on start `NileSwanIconEditor.exe` directly.

A program built locally is not marked as downloaded from the Internet, so
SmartScreen does not warn about it.

Building by hand, without the icon:

```
C:\Windows\Microsoft.NET\Framework64\v4.0.30319\csc.exe /target:winexe ^
  /out:NileSwanIconEditor.exe /r:System.Windows.Forms.dll ^
  /r:System.Drawing.dll NileSwanIconEditor.cs
```

## What it makes

An icon set is one `.ICN` file (4832 bytes). Copy it to `NILESWAN\ICONS` on the
microSD card and choose it in the menu: **Settings > Interface > Theme > Icons**.

A set has 10 icons (folder, file, ROM, image, music, `.bfb`, `.com`, `.fr`/`.il`,
`.zip`, `.fx`), each in 4 variants: 16×16 color, 16×16 mono, 8×8 color, 8×8 mono
(the 8×8 ones are used by the compact list style, the mono ones on the original
black-and-white WonderSwan).

## The rules (enforced by the editor)

- 16×16 icons: the last column (red, hatched) stays empty: it is the gap
  between the icon and the file name.
- **back** is the row background: white, black on the selected row. There is
  no opaque white: for light areas use a light color.
- **ink** is black, white on the selected row: use it for outlines.
- Each set has 12 free colors (12-bit: 16 levels per channel), shared by all
  its color icons. Double-click a color to change it. Mono icons: 4 tones.

## Using it

- **File > New** starts from the built-in icons (**New blank**: empty icons).
  **File > Open/Save .ICN**. **Import/Export PNG folder**: the 40 icons as
  `16color/00-folder.png` … **Export preview picture**: a PNG of the whole set
  as the console shows it.
- Left list: the icon. Variant box (or keys 1-4): the variant.
- Mouse: left button draws with the selected color, right button with the
  background; Alt+click picks a color from the icon.
- Tools: **P** pencil, **L** line, **R** rectangle, **F** fill, **K** picker.
- Ctrl+Z / Ctrl+Y undo / redo; Ctrl+C / Ctrl+V copy / paste an icon (same
  variant size); Delete clears it; Ctrl+arrows move it by one pixel;
  Edit > Flip horizontally/vertically.
- **Generate**: "Mono from color" (Ctrl+M) and "8x8 from 16x16" (Ctrl+H) make a
  first version of the other variants from what you drew; touch them up.
- The preview at the bottom shows all 10 icons as on the console: normal
  row, selected row, 8×8, mono. Click an icon there to edit it.
- Settings (last folder, window) are saved in `NileSwanIconEditor.ini` next
  to the program (or in `%APPDATA%\NileSwanIconEditor`).

## Command line

```
NileSwanIconEditor.exe --pack <png folder> <out.icn>
NileSwanIconEditor.exe --unpack <in.icn> <png folder>
NileSwanIconEditor.exe --preview <in.icn> <out.png>
NileSwanIconEditor.exe --builtin <out.icn>
```

## .ICN format

```
"WSIC", version 1, 3 reserved bytes
12 x 16-bit colors (0x0RGB) for color indexes 4..15
tile data: 16color, 16color_rot (10 x 128 bytes), 8color, 8color_rot (10 x 32),
           16mono, 16mono_rot (10 x 64), 8mono, 8mono_rot (10 x 16)
16x16 icons as tiles TL, BL, TR, BR
color indexes: 2 = row background, 3 = ink, 4..15 = the set's palette
mono:          2 = background, 0 = light, 1 = dark, 3 = ink
```

## License

GPL-3.0 — see [LICENSE](LICENSE).
