from __future__ import annotations
import hashlib, importlib.util, json, tempfile, unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location("world_item_visual_loop", ROOT / "scripts" / "world_item_visual_loop.py")
assert SPEC is not None and SPEC.loader is not None
M = importlib.util.module_from_spec(SPEC); SPEC.loader.exec_module(M)

def digest(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()

class WorldItemVisualLoopTests(unittest.TestCase):
    def fixture(self, root: Path):
        spec = {"id":"fixture-item","parts":[{"name":"body","component":"box","material":"body","size":[1.0,1.0,1.0]}],"materials":{"body":{"base_color":[0.4,0.4,0.4],"roughness":0.6,"metallic":0.0}}}
        spec_path = root / "fixture.json"; spec_path.write_text(json.dumps(spec))
        out = root / "renders"; out.mkdir()
        hashes, framing = {}, {}
        for view in M.VIEWS:
            path = out / f"view-{view}.png"; path.write_bytes(f"png:{view}".encode()); hashes[path.name] = digest(path)
            framing[view] = {"center_error_x":0.0,"center_error_y":0.0,"fill_ratio":0.84,"normalized_bounds":[-0.42,0.42,-0.42,0.42],"ortho_scale":1.0}
        glb = out / "fixture-item.glb"; glb.write_bytes(b"glTFfixture"); hashes[glb.name] = digest(glb)
        manifest = {"schema_version":2,"id":"fixture-item","spec_sha256":digest(spec_path),"blender":"4.2.0","dimensions_m_actual":[1,1,1],"triangles":12,"parts_count":1,"render_framing":framing,"sha256":hashes}
        (out / "manifest.json").write_text(json.dumps(manifest))
        return spec_path, out

    def review(self, bundle):
        return {"schemaVersion":1,"productId":bundle["productId"],"bundleSha256":bundle["bundleSha256"],"candidateArtifactSha256":bundle["candidateArtifact"]["sha256"],"renderProtocolSha256":bundle["renderProtocolSha256"],"status":"FAIL","decision":"REVISE","findings":[{"view":"front","observedDefect":"too wide","severity":"MAJOR","probableCause":"box width","confidence":0.9,"recommendedChange":"reduce width"}],"patches":[{"path":"/parts/0/size","value":[0.8,1.0,1.0]}]}

    def test_bundle_and_stale_guard(self):
        with tempfile.TemporaryDirectory() as tmp:
            spec, out = self.fixture(Path(tmp)); bundle = M.build_review_bundle(spec, out)
            self.assertEqual(len(bundle["renders"]), 6)
            review = self.review(bundle); review["bundleSha256"] = "0" * 64
            with self.assertRaisesRegex(ValueError, "bundleSha256"):
                M.validate_review(review, bundle, M.read_object(spec))

    def test_patch_materializes_candidate_spec_only(self):
        with tempfile.TemporaryDirectory() as tmp:
            spec_path, out = self.fixture(Path(tmp)); bundle = M.build_review_bundle(spec_path, out); spec = M.read_object(spec_path)
            candidate = M.apply_review_patches(spec, self.review(bundle), bundle)
            self.assertEqual(candidate["parts"][0]["size"], [0.8,1.0,1.0]); self.assertEqual(spec["parts"][0]["size"], [1.0,1.0,1.0])

    def test_identity_patch_is_rejected(self):
        with tempfile.TemporaryDirectory() as tmp:
            spec_path, out = self.fixture(Path(tmp)); bundle = M.build_review_bundle(spec_path, out); review = self.review(bundle)
            review["patches"] = [{"path":"/id/value","value":1}]
            with self.assertRaisesRegex(ValueError, "not allowed"):
                M.validate_review(review, bundle, M.read_object(spec_path))

    def test_last_good_reverts_and_stops_at_limit(self):
        with tempfile.TemporaryDirectory() as tmp:
            spec_path, out = self.fixture(Path(tmp)); before = M.build_review_bundle(spec_path, out)
            after = json.loads(json.dumps(before)); after["candidateArtifact"]["sha256"] = "1" * 64; after["bundleSha256"] = M._bundle_digest(after)
            comparison = {"beforeBundleSha256":before["bundleSha256"],"afterBundleSha256":after["bundleSha256"],"verdict":"REGRESSED"}
            first = M.decide_last_good(before, after, comparison, iteration=2, max_iterations=3)
            final = M.decide_last_good(before, after, {**comparison,"verdict":"UNCHANGED"}, iteration=3, max_iterations=3)
            self.assertEqual(first["status"], "REVERTED"); self.assertEqual(first["lastGoodBundleSha256"], before["bundleSha256"]); self.assertEqual(final["status"], "STOPPED_LIMIT")

if __name__ == "__main__":
    unittest.main()
