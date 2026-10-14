---
name: calculus-model
description: Read and edit a Calculus structural model (beams, plates, bolts, welds, rebars, grids, the analytical / FEM model, loads, results, drawings) in the Calculus desktop app running on this machine, over its local MCP / REST server at http://127.0.0.1:7421. Use whenever a task involves a Calculus model or a .calculus file and the app is open.
---

# Working with a Calculus model

Calculus is a structural-engineering BIM / FEM editor. The **Calculus desktop
app** serves the models open in it to agents on `http://127.0.0.1:7421`:
MCP at `POST /mcp`, the same operations as REST at `POST /api/<operation>`.
Every write is applied at once and is one undo step in the app.

## First: read the app's own guide

```
curl -s http://127.0.0.1:7421/
```

The app serves a guide that matches its version: endpoints, how to connect,
the conventions, the analysis workflow and examples. Read it before working
— it is the reference; this skill only gets you there. `GET /api/tools` lists
every operation with its input schema; `describe { type }` gives an object
type's schema.

- **Nothing answers on 7421:** the Calculus desktop app is not running. Ask
  the user to start it and open a model. Don't try other ports.
- **401 `unauthorized`:** the app requires a token. Ask the user for it (the
  app's Agents popover) and send `Authorization: Bearer <token>`.
- **The `mcp__calculus__*` tools are not there:** use REST with `curl`
  (`POST /api/<operation>` with a JSON body) — it runs the same operations.
  To add the MCP server to Claude Code:
  `claude mcp add --transport http calculus http://127.0.0.1:7421/mcp`.
  Claude Desktop's Settings → Connectors cannot reach this machine; the guide
  shows the local configuration that can.

## The rules that matter most

- Start with `listEditors`; with several models open, pass `editor` on every
  call (or `selectEditor` once).
- Units are **mm**, **radians**, **kN**; **Z is up**. A 6 m beam is `6000`.
- Don't pass `id` when creating: `addObjects` returns the objects with their
  ids. Create referenced objects first (nodes → members → supports → load
  cases → loads), using the returned ids.
- Use real catalog names for profiles and materials (`listProfiles`,
  `listMaterials`); `describe` a type before creating it.
- Never discard unsaved changes (`closeEditor { discard: true }`) without the
  user's say-so.
