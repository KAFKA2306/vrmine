import assert from "node:assert/strict";
import fs from "node:fs";

const schema = JSON.parse(fs.readFileSync("config/astra-visual-verdict.schema.json", "utf8"));
const requiredChecks = [
  "scale_dimensions", "floating_parts", "mesh_intersection", "topology",
  "material_assignment", "texture_uv", "rig_joints", "colliders",
  "camera_clipping", "layout_obstruction"
];

assert.equal(schema.$schema, "https://json-schema.org/draft/2020-12/schema");
assert.equal(schema.additionalProperties, false);
assert.deepEqual(schema.properties.verdict.enum, ["PASS", "FAIL"]);
assert.deepEqual(schema.properties.checks.required, requiredChecks);
assert.equal(schema.properties.checks.additionalProperties, false);
assert.deepEqual(schema.$defs.check.properties.status.enum, ["PASS", "FAIL", "NOT_APPLICABLE"]);

const failRule = schema.allOf.find((rule) => rule.if?.properties?.verdict?.const === "FAIL");
assert.equal(failRule.then.properties.repair.properties.required.const, true);
assert.equal(failRule.then.properties.repair.properties.instructions.minItems, 1);
const passRule = schema.allOf.find((rule) => rule.if?.properties?.verdict?.const === "PASS");
assert.equal(passRule.then.properties.repair.properties.required.const, false);
assert.equal(passRule.then.properties.repair.properties.instructions.maxItems, 0);

console.log("astra visual verdict contract: PASS");
