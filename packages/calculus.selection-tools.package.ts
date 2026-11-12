import { defineCommand, definePackage } from "calculus";

// Selection helpers: each command selects by a filter query (CFQL — the
// same language as the view filter), so the result can be inspected,
// isolated or edited in one go. A module package: code, run in the editor
// as one undo step per command.
export default definePackage({
  id: "calculus.selection-tools",
  version: "1.0.0",
  name: "Selection tools",
  description:
    "Select beams longer than a length, or every part with a given profile — the selection can then be isolated, edited or exported.",
  tags: ["selection", "filter"],
  authors: ["Calculus"],
  extensions: [
    defineCommand({
      id: "select-long-beams",
      name: "Select beams longer than…",
      description: "Selects every beam longer than the given length.",
      params: [
        { name: "length", type: "number", label: "Longer than (mm)", default: 6000 },
      ],
      run: async (api, { params }) => {
        await api.ops.selectByFilter({
          query: `type = beam and length > ${Number(params.length)} mm`,
        });
      },
    }),
    defineCommand({
      id: "select-by-profile",
      name: "Select by profile…",
      description: "Selects every part whose profile is the given one.",
      params: [
        { name: "profile", type: "string", label: "Profile", default: "IPE300" },
      ],
      run: async (api, { params }) => {
        const profile = String(params.profile).replace(/"/g, "");
        await api.ops.selectByFilter({ query: `profile = "${profile}"` });
      },
    }),
  ],
});
