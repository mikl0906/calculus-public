#!/usr/bin/env node
// The marketplace catalog generator (linea's `lineadraw manifest|check`,
// for calculus PACKAGES). Walks `packages/` for package files —
//   *.package.ts / *.package.js   a module: `export default definePackage({...})`
//   *.package.json                a data package (catalog data, no code)
// — validates each, and writes `.calculus/marketplace.json`, the catalog the
// Calculus editor's Library → Marketplace reads (calculus repo:
// packages/core/src/marketplace/catalog.ts). Pure filesystem walk, no git:
// rows carry repo-RELATIVE `source` paths the editor resolves against the
// repo the user added, at the ref the catalog was fetched from.
// Deterministic: same content, same bytes, so `check` is a plain compare.
//
//   node scripts/marketplace.mjs manifest   write the catalog
//   node scripts/marketplace.mjs check      fail when invalid or stale (CI)
//
// Modules are run (sucrase: TypeScript + ESM → CJS) against a shim of the
// virtual "calculus" module whose define<Kind> helpers return their spec —
// top-level code only; run()/compute() never execute here.
import { createRequire } from "node:module";
import {
  existsSync,
  mkdirSync,
  readFileSync,
  readdirSync,
  writeFileSync,
} from "node:fs";
import { dirname, join, resolve } from "node:path";
import { fileURLToPath } from "node:url";

const require_ = createRequire(import.meta.url);
const ROOT = resolve(dirname(fileURLToPath(import.meta.url)), "..");
const PACKAGES_DIR = "packages";
export const MANIFEST_PATH = ".calculus/marketplace.json";

// The editor's id rule (model/packages.ts IdSchema).
const ID_RE = /^[A-Za-z0-9][A-Za-z0-9_.-]*$/;
const SEMVER_RE = /^\d+\.\d+\.\d+$/;
const KINDS = new Set([
  "profiles",
  "materials",
  "bolts",
  "rebars",
  "component",
  "command",
  "property",
  "block",
]);
const DATA_KINDS = new Set(["profiles", "materials", "bolts", "rebars"]);

const readConfig = () => {
  const pkg = JSON.parse(readFileSync(join(ROOT, "package.json"), "utf8"));
  const cfg = pkg.calculus ?? {};
  return {
    name: cfg.marketplaceName ?? pkg.description ?? pkg.name ?? "Marketplace",
    owner: cfg.marketplaceOwner ?? pkg.author ?? "",
  };
};

// The "calculus" module's define<Kind> helpers as the editor's runtime has
// them (core extensions/types.ts): each adds its member's `kind`;
// definePackage and defineParametricProfile return their spec.
const withKind = (kind) => (spec) => ({ kind, ...spec });
const calculusShim = {
  definePackage: (spec) => spec,
  defineParametricProfile: (spec) => spec,
  defineProfiles: withKind("profiles"),
  // COMPAT (pre-Sept-2026): one parametric profile as a profiles member.
  defineProfile: ({ id, name, description, tags, ...entry }) => ({
    kind: "profiles",
    id,
    name,
    description,
    tags,
    sections: [],
    parametric: [{ name, description, ...entry }],
  }),
  defineMaterials: withKind("materials"),
  defineBolts: withKind("bolts"),
  defineRebars: withKind("rebars"),
  defineProperty: withKind("property"),
  defineComponent: withKind("component"),
  defineCommand: withKind("command"),
  defineBlock: withKind("block"),
};

const runModule = (source) => {
  const { transform } = require_("sucrase");
  const code = transform(source, { transforms: ["typescript", "imports"] }).code;
  const moduleShim = { exports: {} };
  new Function("exports", "require", "module", code)(
    moduleShim.exports,
    (spec) => {
      if (spec === "calculus") return calculusShim;
      throw new Error(`illegal import "${spec}" (only "calculus")`);
    },
    moduleShim,
  );
  return moduleShim.exports.default;
};

const entryOf = (file) => {
  const source = readFileSync(join(ROOT, PACKAGES_DIR, file), "utf8");
  const form = file.endsWith(".json") ? "data" : "module";
  let pkg;
  try {
    pkg = form === "data" ? JSON.parse(source) : runModule(source);
  } catch (e) {
    return { file, problems: [`does not load: ${e.message}`] };
  }
  const problems = [];
  if (pkg === null || typeof pkg !== "object")
    return { file, problems: ["no default-exported definePackage({...})"] };
  if (typeof pkg.id !== "string" || !ID_RE.test(pkg.id))
    problems.push(`id ${JSON.stringify(pkg.id)} is not a package id`);
  if (typeof pkg.name !== "string" || pkg.name.trim() === "")
    problems.push("missing name");
  if (typeof pkg.version !== "string" || !SEMVER_RE.test(pkg.version))
    problems.push(`version ${JSON.stringify(pkg.version)} is not semver`);
  const extensions = Array.isArray(pkg.extensions) ? pkg.extensions : null;
  if (extensions === null) problems.push("missing extensions");
  const members = [];
  const ids = new Set();
  for (const [i, ext] of (extensions ?? []).entries()) {
    const where = `extensions[${i}]`;
    if (!ext || typeof ext !== "object") {
      problems.push(`${where} is not an object`);
      continue;
    }
    if (!KINDS.has(ext.kind)) problems.push(`${where}: unknown kind ${ext.kind}`);
    else if (form === "data" && !DATA_KINDS.has(ext.kind))
      problems.push(`${where}: a data package carries only catalog data`);
    if (typeof ext.id !== "string" || !ID_RE.test(ext.id))
      problems.push(`${where}: id ${JSON.stringify(ext.id)} is not an id`);
    else if (ids.has(ext.id)) problems.push(`${where}: id "${ext.id}" twice`);
    ids.add(ext.id);
    if (typeof ext.name !== "string" || ext.name.trim() === "")
      problems.push(`${where}: missing name`);
    members.push({ kind: ext.kind, id: ext.id, name: ext.name });
  }
  if (problems.length > 0) return { file, problems };
  return {
    file,
    entry: {
      kind: "package",
      id: pkg.id,
      name: pkg.name,
      version: pkg.version,
      ...(typeof pkg.description === "string"
        ? { description: pkg.description }
        : {}),
      tags: Array.isArray(pkg.tags) ? pkg.tags.filter((t) => typeof t === "string") : [],
      authors: Array.isArray(pkg.authors)
        ? pkg.authors.filter((a) => typeof a === "string")
        : [],
      form,
      members,
      // POSIX-relative, unencoded — the editor encodes it for the URL.
      source: `${PACKAGES_DIR}/${file}`,
    },
  };
};

/** The catalog and every problem (a non-empty list must not publish). */
export const buildManifest = () => {
  const cfg = readConfig();
  const files = existsSync(join(ROOT, PACKAGES_DIR))
    ? readdirSync(join(ROOT, PACKAGES_DIR))
        .filter((f) => /\.package\.(ts|js|json)$/.test(f))
        .sort()
    : [];
  const results = files.map(entryOf);
  const problems = results.filter((r) => r.problems);
  const entries = results.filter((r) => r.entry).map((r) => r.entry);
  const dupes = [
    ...new Set(entries.map((e) => e.id).filter((id, i, all) => all.indexOf(id) !== i)),
  ];
  if (dupes.length > 0)
    problems.push({ file: "(catalog)", problems: [`duplicate ids: ${dupes.join(", ")}`] });
  return {
    manifest: { name: cfg.name, owner: { name: cfg.owner }, entries },
    problems,
  };
};

const manifestJson = (manifest) => JSON.stringify(manifest, null, 2) + "\n";

const report = (problems) => {
  for (const { file, problems: list } of problems)
    for (const p of list) console.error(`  ${file}: ${p}`);
};

const main = (command) => {
  const { manifest, problems } = buildManifest();
  if (problems.length > 0) {
    console.error("Invalid packages:");
    report(problems);
    process.exit(1);
  }
  const path = join(ROOT, MANIFEST_PATH);
  if (command === "manifest") {
    mkdirSync(dirname(path), { recursive: true });
    writeFileSync(path, manifestJson(manifest));
    console.log(`${MANIFEST_PATH}: ${manifest.entries.length} packages`);
    return;
  }
  if (command === "check") {
    const committed = existsSync(path)
      ? JSON.parse(readFileSync(path, "utf8"))
      : null;
    // Parsed JSON compared: formatting and line endings don't count.
    if (JSON.stringify(committed) !== JSON.stringify(manifest)) {
      console.error(
        `${MANIFEST_PATH} is stale. Run \`npm run manifest\` and commit the result.`,
      );
      process.exit(1);
    }
    console.log(`${MANIFEST_PATH} is up to date (${manifest.entries.length} packages).`);
    return;
  }
  console.error("usage: node scripts/marketplace.mjs manifest|check");
  process.exit(2);
};

if (process.argv[1] && resolve(process.argv[1]) === fileURLToPath(import.meta.url))
  main(process.argv[2]);
