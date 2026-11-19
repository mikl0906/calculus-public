# Calculus Tekla Link

Transfers structural objects between [Calculus](https://github.com/mikl0906/calculus-public)
and Tekla Structures: beams, plates, cuts, bolts, welds, rebars, grids and
lines.

- **Direct transfer** through the Calculus desktop app (its local bridge on
  `127.0.0.1:7421`): each object is classified as new, updated, unchanged, in
  conflict or gone, the preview colours both models, and Transfer applies it.
  Objects are paired across the two models by `userdata.tekla_id` in Calculus
  and the `CALCULUS_ID` user-defined attribute in Tekla.
- **File transfer**: export the Tekla selection to a `.calculus` file, or
  import `.calculus` files into the open Tekla model.

## Requirements

- Tekla Structures 2022 or newer, **with your own Tekla licence**
- .NET Framework 4.8 (part of Windows 10 / 11)
- The Calculus desktop app, for direct transfer

## Layout

| Path | What |
|---|---|
| `CalculusModel/` | The `.calculus` format in C# (structural objects only) and the sync hash. Mirrors the TypeScript schema in Calculus; keep the two aligned. |
| `CalculusTeklaLink/` | The WPF app: Tekla ↔ Calculus mapping, the bridge client, sync plan and preview. |
| `Tsep/` | The Tekla extension package definition and the macro that starts the app from Tekla. |
| `icons/` | `icon.svg`, the source of the app icon (`CalculusTeklaLink/AppIcon.ico`), the macro icon and the package image (`Tsep/`). |

## Build

```
dotnet build CalculusTeklaLink.slnx -c Release
```

The Tekla Open API comes from nuget.org at compile time only
(`ExcludeAssets="runtime"`). At runtime the app uses the assemblies of the
Tekla Structures installation it runs under: Tekla rewrites the
`.exe.config` of extensions in its extension folders to point at its own
`bin`. **Never ship Tekla or Trimble assemblies** with the app or in the
package.

For debugging, the Debug configuration sets `TeklaVersion` (see
`CalculusTeklaLink.csproj`) so `TSAppConfigPatcherTask` patches the config for
that installed version; start Tekla first, then F5.

## Package

Building the `.tsep` needs Tekla Structures installed: the packager,
`TeklaExtensionPackage.BatchBuilder.exe`, ships in its `bin` folder.

```powershell
./Tsep/build-tsep.ps1
```

The script:

1. builds the solution in Release;
2. empties `Tsep/extension/` and copies the build output into it;
3. stops if any `Tekla.*`, `Trimble.*` or `DotNetKit` assembly is in it, or if
   the files differ from the `<File>` list in `Tsep/TsepDefinition.xml`;
4. runs the newest installed Tekla's BatchBuilder with that manifest and
   writes `Tsep/out/CalculusTeklaLink_<version>.tsep`.

The package version is the `<Version>` in `CalculusTeklaLink/CalculusTeklaLink.csproj`
(major.minor); the `Version` in `TsepDefinition.xml` is overridden. Pass
`-TeklaBin <a Tekla bin folder>` to use a specific Tekla's packager.

When a dependency is added or removed, update the `<File>` entries in
`TsepDefinition.xml` (a new entry needs a new GUID as its `Id`); the script
lists what differs. Tekla's GUI packager, `TeklaExtensionPackage.Builder.exe`
in the same folder, opens the same manifest. Packages are published as
release assets on this repository, never committed (`Tsep/out/` is ignored).

## Releasing

A plugin release is a GitHub release on this repository with the `.tsep` as
its asset. This repository also carries the desktop app's releases
(`desktop-v…`), so the tag says which product it is:

- **1.4 and later:** tag `tekla-link-v<major>.<minor>` (e.g.
  `tekla-link-v1.4`) and create the release **not** as Latest
  (`gh release create … --latest=false`).
- **1.3** was the last plain `v1.3` tag. It is the Latest release and must
  stay Latest: installed 1.0–1.2 plugins read only the repository's Latest
  release and parse its tag as `v<version>`, so a `tekla-link-v…` release
  as Latest would silently end their update notices. Latest = `v1.3` gives
  them 1.3, and from 1.3 on the updater (`CalculusTeklaLink/Updater.cs`)
  lists the releases and takes the newest `.tsep` under either tag form.

## Licence

[MIT](LICENSE). The licence covers this code only. The Tekla Open API it
builds against is Trimble's, under Trimble's own terms (below).

## Trademarks

Tekla and Tekla Structures are trademarks of Trimble Solutions Corporation.
This project is not affiliated with, sponsored by or endorsed by Trimble.
Use of the Tekla Open API is subject to Trimble's end-user licence agreement.
