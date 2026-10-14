---
name: calculus-model
description: Read and edit a Calculus structural model in a running Calculus editor over MCP (the desktop app serves it; a web tab pairs with the app). Use whenever the task involves the .calculus model — beams, plates, bolts, welds, rebars, grids, the analytical/FEM model, selection, view state, or catalogs — and a Calculus editor is open.
---

# Working with a Calculus model

Calculus is a structural-engineering CAD/BIM editor. You drive a **live** model
in a running editor through MCP tools (prefixed `mcp__calculus__`), served on
`http://127.0.0.1:7421/mcp`. Every write is applied immediately and is
undoable in the editor (one tool call = one undo step). The same operations
are plain REST on the same port: `GET /api/editors`,
`POST /api/<operation>?editor=<id>` with the arguments as the JSON body,
`GET /api/model`, `GET /api/objects?type=beam` — use REST (curl) when the MCP
server is not configured; the two never differ.

## Who serves the port

- **The Calculus desktop app serves agents itself.** Every open model is a
  window of the app, registered with its in-process bridge; nothing to
  connect, nothing to configure.
- **Web / PWA editors** join the same desktop app by pairing once (a code
  from the app's Agents popover, typed into the tab's Agents popover) and
  then show up in `listEditors` with `kind: "web"`.

## Several models open: say which

Start with **`listEditors`** (or `GET /api/editors`): id, name, file path,
kind, readOnly, dirty, one per open model. With more than one open, every
tool needs **`editor`** (a document id, file path or unique name) — or call
**`selectEditor { editor }`** once and the rest of the session's calls go
there. Without it the bridge answers `ambiguous` with the candidates; it
never guesses. With one model open nothing is needed.

## Opening and closing models yourself

- **`openEditor { path }`** (or `POST /api/openEditor`) opens a `.calculus`
  file in a new window of the desktop app and returns its editor record once
  it is up — then address it with `editor`. A file already open is returned
  at once. Absolute paths.
- **`closeEditor { discard? }`** closes an editor like its window's × — on
  the desktop the window goes, on the web the app returns to its start
  screen. Unsaved file changes are REFUSED (`closed: false, dirty: true`)
  unless `discard: true`. Name the editor with `editor` when several are
  open. Never discard without the user's say-so.

## A token, when the app requires one

If the desktop app has "Require a token from agents" on, `/mcp` and `/api`
answer 401 `unauthorized` without `Authorization: Bearer <token>`. The token
is in the app's Agents popover and in its `bridge.json`
(`%APPDATA%/build.calculus.desktop/bridge.json` on Windows). The
repo's `.mcp.json` sends `$CALCULUS_BRIDGE_TOKEN`; the `calculus` CLI
(`node packages/cli/dist/calculus.mjs`, built with
`npm run build -w @calculus/cli`) reads bridge.json itself — `calculus
editors`, `calculus call <op> '<json>' --editor <x>`, `calculus open <file>`,
and headless `calculus inspect <file>` / `calculus apply <file> <ops.json>`.
Ask the user for the token; never read another user's bridge.json.

## If a call fails

The error names what is missing. Don't launch anything pre-emptively; only on
these failures.

- **The `mcp__calculus__*` tools are missing or unreachable** → nothing serves
  7421: the Calculus desktop app is not running. Ask the user to start it
  and open a model.
- **`noEditor` — "No model is open in Calculus"** → the app is running but
  every window is on its start screen. Ask the user to open a model, or
  `openEditor` a file yourself. A web tab shows up only after the user has
  paired it and pressed **Connect** in its Agents popover.
- **`ambiguous`** → several models are open: pick one with `editor` or
  `selectEditor` (above).
- **`noSuchEditor`** → the name you passed matches nothing open; list again.

Give a restarted editor a few seconds, then reconnect the MCP server (`/mcp`)
and retry.

> Note: there is deliberately **no `mcp__calculus__connect` tool** — MCP calls
> are relayed *over* the editor's own connection, so a tool can't create the
> very connection it would travel on.

## Conventions — get these wrong and the model is wrong

- **Units:** lengths in **millimetres (mm)**, angles in **radians**, forces in
  kN/kNm, everything else SI. A 6 m beam is `6000`, not `6`.
- **Up axis:** **Z is up.** A column runs along +Z; a floor is the XY plane.
- **Identity — let Calculus assign ids.** Every object has a UUID `id`, but
  **do not invent or pass `id` when creating.** `addObjects` assigns ids and
  returns the created objects — keep those ids and use them for references.
  Reference objects by `id`, never by name or position.
- **Create in dependency order, across calls.** Because you use *returned* ids as
  references, create referenced objects first, then the referrers in a later
  call: for a FEM model that's nodes → member1D/member2D → supports/hinge2Ds/links
  → load cases → loads → combinations. (`addObjects` does not validate that a
  referenced id exists; a dangling reference just fails silently at solve time.)
- **Names are catalog keys.** A beam's `profile` and `material` are *names*.
  `listProfiles` / `listMaterials` / `listBolts` / `listRebars` now include the
  built-in **system catalogs**, so call them first and use a real name.
- **Use real steel sections, not parametric rectangles.** The system catalog has
  IPE, HEA, HEB, HEM, UPE/UPN, L, SHS, RHS, CHS. For a steel member pick e.g.
  `IPE300` / `HEA200` / `SHS100x5` — `listProfiles({ search: "IPE" })` to find
  one. A parametric `400*200` is a *solid rectangle*, fine for concrete/generic
  but **not** a rolled steel profile — don't use it for steel.
- **Profiles are `H×B`, height first.** A parametric rectangle `300*600` (also
  `300x600`) is **300 mm tall × 600 mm wide** — a *flat* section. For an upright
  600-deep × 300-wide beam use `600*300`. Height maps to the beam's local **Z**
  (vertical for a horizontal beam), breadth to local **Y**. Call
  `getBeamProfile(name)` to get the resolved `height`/`width` and confirm
  orientation before you place anything (e.g. rebar) relative to the section.
- **Plate / member2D thickness is just a number in the string.** A plate's (and
  member2D panel's) `profile` is not a catalog section — the first number is
  taken as the thickness (mm), so `200`, `PL200`, `t=200` all give a 200 mm
  plate. Check with `getPlateProfile(name)`, not `getBeamProfile`. Beams and
  member1D use catalog/parametric sections (`getBeamProfile`); plates and
  member2D use thickness strings (`getPlateProfile`).
- **A 1D member's end hinges are fields on the member**, not objects: set
  `startRelease` / `endRelease` (six DOFs, each `"fixed"`, `"free"` or a
  stiffness number) with `updateObjects`. All-`"fixed"` means an un-hinged end,
  and is the default. Only `hinge2D` (a 2D member's edge release) is its own
  object type.

## Useful reads

- `getBeamProfile(name)` — resolved beam/member section dimensions + orientation
  (see above). `getPlateProfile(name)` — the thickness a plate string resolves to.
- `getBoundingBox({ ids? })` — global AABB of objects (or the whole model);
  sanity-check that geometry landed where you intended.
- `getRelatedObjects({ ids, direction? })` — objects connected by id reference.
  Use `referencedBy` to find the bolts/welds/cuts/rebars/supports attached to a
  part **before** you delete or move it (`removeObjects` does not auto-clean most
  referrers).
- `getSelection({ full: true })` — selected objects, not just ids.

## Workflow

1. **Orient first, cheaply.** `countByType` for an overview; `getAllObjects`
   (optionally `{ type }`) for a category. Avoid `getModel` unless you truly need
   catalogs + view state — it's large.
2. **Learn a type before creating it.** `describe({ type: "beam" })` returns the
   JSON Schema: which fields are required, their types, enums, and units. The
   bundled `schema-reference.md` lists the required fields for every type and the
   id-reference relationships (e.g. a `bolt.partIds` points at beams/plates).
3. **Create / edit.**
   - `addObjects` — create. Supply each object's `type` and required fields;
     defaults fill the rest.
   - `patchObjects` — change a few fields on existing objects
     (`[{ id, properties }]`). **Prefer this for edits** — it merges.
   - `updateObjects` — full replacement by id; omitted fields reset to defaults.
     Use only when you really mean "replace".
   - `transformObjects` — move / copy by translate, rotate or mirror. What
     belongs to the objects goes along (a member's openings, hinges, supports,
     loads; a part's cuts, rebars, and bolts/welds whose parts all move). A
     node shared with a member that is NOT moved stays put and the moved
     member is rebound to a node at the new position; include the node's id
     to force it to move (and drag every member on it).
   - `removeObjects` — delete by id. Cascades only to bolts/welds whose every
     part is removed; other referrers are **not** auto-cleaned — fix referential
     integrity yourself (see relationships in `schema-reference.md`).
4. **Selection & view** (non-destructive, great for showing the user what you
   did): `setSelection` / `addToSelection` / `clearSelection` / `getSelection`;
   `setActiveModel` (BIM vs a FEM model), `setStructuralRepresentation`,
   `setAnalyticalRepresentation`, `setViewFilter`, `setSidebarTab` (open a
   sidebar pane — models/drawings/search/comments/fem-results, or null
   to collapse); camera `zoomToModel` / `zoomToSelection` / `setView` /
   `setCamera`.
5. **Analytical (FEM) pipeline.** `createAnalyticalModel` (or
   `createAnalyticalModelFromSelected`), add objects in dependency order (see
   above), optionally `splitMembersAtIntersections` / `fixProfilesToSimilar` to
   fix connectivity, `runCommand` with `calculus.loads/en1990-combinations`
   (`selection`: the load case ids — omit it to combine every load case of
   the active analytical model; `params: { kfi: "1.0" }`) for Eurocode
   combinations (`listCommands { slot: "loadCombinations" }` lists every generator);
   `runCommand` with `calculus.loads/en1991-wind` for EN 1991-1-4 wind load
   cases + zoned area loads on the ACTIVE model's load panels — the
   `loadPanelIds` input / selection when given, else the panels that carry
   `userdata.wind = "true"`, else every load panel of the model (the result
   warns) (`isParapet = "true"` for parapets; flat /
   monopitch / duopitch roofs; params terrain `"0".."IV"`, `vb0`, `co`,
   `cpiPositive`, `cpiNegative`, `rotation` °, `wallCorrelation`), 16 cases
   (24 with a duopitch roof) in exclusive group `wind` — regenerate the
   combinations afterwards;
   `calculateAnalyticalModel` to solve, then `getFemResultsInfo` +
   `showFemResults` to display results (it auto-tidies the view; `resultType` is
   displacements / reactionForces / memberForces / shellForces / stresses — the
   last is member2D von Mises & σx/σy/τxy contour, optionally drawn on the
   deformed shape via `deformed: true`). Read the solved
   numbers with `getNodeDisplacements` (ux/uy/uz mm, rx/ry/rz rad, `u` magnitude),
   `getSupportReactions` (per support: point kN/kNm, line kN/m, area kN/m²,
   plus each support's resultant — prefer it), `getReactions` (concentrated
   per FE node, fx/fy/fz kN, mx/my/mz kNm in global components, plus `local`
   for an inclined support), `getMemberForces` (N/Vy/Vz kN,
   Mx/My/Mz kNm — per-member `extremes`, or `samples:true` for the force diagram),
   and `getShellStresses` (member2D panels in MPa: `averaged` = what the contour
   and its legend show, `element` = the raw corner peaks over both surfaces);
   all keyed by analytical object id and selectable by load case / combination /
   combination group. Every value is `{ min, max }` — equal for a load case or a
   combination, the group's envelope over its combinations otherwise.
   **Check `stale` on every result read** (also on `getStabilityResults`, and
   per model in `getFemResultsInfo.results`): `true` means the analytical
   model changed since that solve, so the numbers describe an older model —
   re-run `calculateAnalyticalModel` before trusting them. `solvedAt` is the
   solve time.
   **Long solves:** `calculateAnalyticalModel({ async: true })` returns a
   `jobId` at once; poll `getAnalysisJob` (status, phase/fraction progress,
   then the result) instead of holding the call open. `itemIds` solves only
   the given load cases / combinations / groups (a stability analysis then
   skips every other combination's eigen solve).
   **Buckling:** `getStabilityResults` lists α_cr per item (`modeIndex` picks
   the mode its buckling lengths use); `getBucklingMode` details one mode —
   each member's share of the mode's strain energy (`energy`, and `byName`
   per member name: sway of the whole frame vs one member buckling locally),
   buckling lengths, and the mode shape with `shape: true`.
   **Before solving**, `findUnconnectedNodes` lists supports / loads on nodes
   no member uses (they carry nothing); `snapUnconnectedNodes` moves them
   onto the member node within the merge tolerance. The solve warns about
   them too.

## Reference files

- `schema-reference.md` (next to this file) — generated from the real schemas:
  the full tool list and the required fields per object type. Read it when you
  need the cheat-sheet; use the `describe` tool for a type's complete schema.

## Recipes

**Create a grid of columns (HEA200, S355), 3×3 at 6 m spacing, 3.5 m tall:**
```
// profile/material must exist — check first with listProfiles / listMaterials
addObjects({ objects: [
  // one beam per grid node, from (x,y,0) to (x,y,3500)
  { type: "beam", start: { x: 0,    y: 0,    z: 0 }, end: { x: 0,    y: 0,    z: 3500 }, profile: "HEA200", material: "S355" },
  { type: "beam", start: { x: 6000, y: 0,    z: 0 }, end: { x: 6000, y: 0,    z: 3500 }, profile: "HEA200", material: "S355" },
  // ... remaining 7 nodes
] })
```

**Re-profile every beam to HEA240:**
```
const { objects } = getAllObjects({ type: "beam" });
patchObjects({ patches: objects.map(b => ({ id: b.id, properties: { profile: "HEA240" } })) });
```

**Select all plates so the user sees them:**
```
const { objects } = getAllObjects({ type: "plate" });
setSelection({ ids: objects.map(o => o.id) });
```

**Build & analyse an analytical (FEM) frame, then show displacements:**
```
// Pick a real steel section + material first.
listProfiles({ search: "IPE" });           // -> e.g. "IPE300"
// 1. Create the model; it becomes active. Keep its id.
const { id: modelId } = createAnalyticalModel({ name: "Frame" });
// 2. Nodes FIRST — capture the returned ids (do NOT pass ids yourself).
const { objects: nodes } = addObjects({ objects: [
  { type: "node", modelId, p: { x: 0,    y: 0, z: 0    } },
  { type: "node", modelId, p: { x: 0,    y: 0, z: 3500 } },
  { type: "node", modelId, p: { x: 6000, y: 0, z: 3500 } },
  { type: "node", modelId, p: { x: 6000, y: 0, z: 0    } },
] });
const [a, b, c, d] = nodes.map(n => n.id);
// 3. Members reference those node ids. Real catalog sections.
const { objects: members } = addObjects({ objects: [
  { type: "member1D", modelId, startId: a, endId: b, profile: "HEA240", material: "S355" },
  { type: "member1D", modelId, startId: b, endId: c, profile: "IPE300", material: "S355" },
  { type: "member1D", modelId, startId: c, endId: d, profile: "HEA240", material: "S355" },
] });
// 4. Supports (pinned bases), a load case, then loads referencing it.
addObjects({ objects: [
  { type: "pointSupport", modelId, targetId: a, position: { type: "0d" },
    release: { ux: "fixed", uy: "fixed", uz: "fixed", rx: "free", ry: "free", rz: "free" } },
  { type: "pointSupport", modelId, targetId: d, position: { type: "0d" },
    release: { ux: "fixed", uy: "fixed", uz: "fixed", rx: "free", ry: "free", rz: "free" } },
] });
const { objects: [lc] } = addObjects({ objects: [
  { type: "loadCase", modelId, name: "Imposed", actionType: "variable",
    variableActionType: "catA", duration: "medium" },
] });
addObjects({ objects: [
  { type: "lineLoad", modelId, loadCaseId: lc.id, targetId: members[1].id,
    position: { type: "1d", start: 0, end: 1 }, cs: "global", direction: "z", value: -10 },
] });
// 5. Solve, then show the deflected shape. showFemResults auto-tidies the view
//    (ghost lines + hides nodes/links/hinges/loads); pass declutter:false to keep it.
calculateAnalyticalModel();                 // { success: true }
showFemResults({ resultType: "displacements", value: "u", loadCaseId: lc.id, scaleFactor: 50 });
zoomToModel(); setView({ view: "iso" });
```
Members that should connect but don't share a node: link them with a `memberLink`
(`{ type: "memberLink", modelId, member1Id, member2Id }`); or call
`splitMembersAtIntersections({ ids })` to insert shared nodes where members cross.

**Reinforce a concrete beam (stirrups + longitudinal bars):**
```
// 1. Confirm the section's real orientation BEFORE laying out the cage.
getBeamProfile({ name: "600*300" });  // -> height 600 (local Z, up), width 300 (local Y)
// 2. shapes/polygon are in GLOBAL mm. A shape is one bar's centreline anchored
//    at polygon[0]; polygon is the path copies spread along. Express cover with
//    onPlaneCover (in the bending plane) / fromPlaneCover ([start,end], across
//    the distribution) and startOffset/endOffset — do NOT hand-inset coordinates.
addObjects({ objects: [{
  type: "rebar", fatherId: "<beamId>", name: "STIRRUP",
  grade: "A500HW", size: 10, bendingRadius: 30, class: 3,
  shapes: [[ /* closed loop tracing the section, in its plane */ ]],
  polygon: [ /* two points along the beam axis */ ],
  onPlaneCover: [30], fromPlaneCover: [0, 0],
  method: "target", spacings: [200], number: 0,
  startOffset: 30, startOffsetType: "cover",
  endOffset: 30, endOffsetType: "cover", exclude: "none",
}]});
```

## If a write fails

Validation errors come back with a `message` and a zod `issues` array naming the
bad field/path. Read it, fix that field (units? missing required field? unknown
profile name?), and retry. When unsure of a type's shape, `describe` it rather
than guessing.

## In-app scripting (for reference)

The same operations are available to in-app scripts as `await api.ops.<name>(input)`
(e.g. `await api.ops.setSelection({ ids })`), plus lower-level primitives
(`api.addModelObjects`, `api.transaction`, …). The MCP tools and `api.ops` share
one operation registry, so behaviour is identical across both.
