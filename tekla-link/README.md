# Calculus Link for Tekla Structures

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
| `CalculusLink/` | The WPF app: Tekla ↔ Calculus mapping, the bridge client, sync plan and preview. |
| `Tsep/` | The Tekla extension package definition and the macro that starts the app from Tekla. |

## Build

```
dotnet build CalculusLink.slnx -c Release
```

The Tekla Open API comes from nuget.org at compile time only
(`ExcludeAssets="runtime"`). At runtime the app uses the assemblies of the
Tekla Structures installation it runs under: Tekla rewrites the
`.exe.config` of extensions in its extension folders to point at its own
`bin`. **Never ship Tekla or Trimble assemblies** with the app or in the
package.

For debugging, the Debug configuration sets `TeklaVersion` (see
`CalculusLink.csproj`) so `TSAppConfigPatcherTask` patches the config for
that installed version; start Tekla first, then F5.

## Package

1. Empty `Tsep/extension/`, then copy the Release build output into it.
2. Check that the `<File>` list in `Tsep/TsepDefinition.xml` matches the
   folder, and that no `Tekla.*` or `Trimble.*` assembly is in either.
3. Build the `.tsep` with Tekla's TSEP builder. Packages are published as
   release assets on this repository, never committed.

## Licence

[MIT](LICENSE). The licence covers this code only. The Tekla Open API it
builds against is Trimble's, under Trimble's own terms (below).

## Trademarks

Tekla and Tekla Structures are trademarks of Trimble Solutions Corporation.
This project is not affiliated with, sponsored by or endorsed by Trimble.
Use of the Tekla Open API is subject to Trimble's end-user licence agreement.
