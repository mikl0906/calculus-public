# Calculus

The public repository of the Calculus project: issues, desktop app
releases, and open-source pieces around it.

- [`tekla-link/`](tekla-link/) — **Calculus Link for Tekla Structures**, the
  Tekla Structures extension (source, MIT; requires your own Tekla licence)
- [`skills/calculus-model/`](skills/calculus-model/) — the Claude skill for
  working with Calculus models through the desktop app's bridge
- [`packages/`](packages/) — **the Calculus public collection**: packages
  (profiles, components, commands) the editor's Library → Marketplace
  installs into a model. Its catalog, `.calculus/marketplace.json`, is
  generated from the package files — see below.

## The public collection

Each file in `packages/` is one package:

- `*.package.ts` — a module, `export default definePackage({...})` with its
  extensions (`defineCommand`, `defineComponent`, `defineProfiles`, …). Code:
  it runs in the editor once installed.
- `*.package.json` — a data package (profiles, materials, bolts, rebars), no
  code.

A package's `id` (`vendor.name`) is permanent — models reference its
extensions as `<package id>/<extension id>` — and its `version` is what the
editor's Update badge compares. To add or change one:

```
npm install
npm run manifest   # regenerate .calculus/marketplace.json
npm run check      # what CI runs: every package valid, the catalog current
```

and commit the package and the catalog together. Any repository with the
same layout (`packages/`, `scripts/marketplace.mjs`, the generated catalog)
can be added as a marketplace in the editor (App settings → Marketplaces),
private ones with a read-only token.
