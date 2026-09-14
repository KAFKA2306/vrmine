import importlib.util
import json
import subprocess
import tempfile
import unittest
from pathlib import Path

ROOT = Path(__file__).resolve().parents[1]
SPEC = importlib.util.spec_from_file_location(
    "verify_verification_worktree", ROOT / "scripts" / "verify_verification_worktree.py"
)
VERIFY = importlib.util.module_from_spec(SPEC)
SPEC.loader.exec_module(VERIFY)

POLICY = {
    "normal_run": {
        "delete_existing_run": False,
        "reuse_failed_run": False,
        "reuse_partial_run": False,
        "collision_strategy": "allocate-new-run",
        "retry_strategy": "allocate-new-run",
    },
    "destructive_recovery": {
        "allowed": False,
        "forbidden_commands": [
            "git worktree remove",
            "git clean",
            "git reset --hard",
            "Remove-Item -Recurse",
            "rm -rf",
        ],
    },
}


class SafeIsolationTest(unittest.TestCase):
    def setUp(self):
        self.temp = tempfile.TemporaryDirectory()
        self.root = Path(self.temp.name)
        self.repo = self.root / "repo"
        self.repo.mkdir()
        subprocess.run(["git", "init", "-q", str(self.repo)], check=True)
        subprocess.run(
            ["git", "-C", str(self.repo), "config", "user.email", "ci@example.invalid"],
            check=True,
        )
        subprocess.run(
            ["git", "-C", str(self.repo), "config", "user.name", "CI"], check=True
        )
        (self.repo / "tracked.txt").write_text("base\n", encoding="utf-8")
        subprocess.run(["git", "-C", str(self.repo), "add", "tracked.txt"], check=True)
        subprocess.run(
            ["git", "-C", str(self.repo), "commit", "-qm", "base"], check=True
        )
        self.sha = subprocess.check_output(
            ["git", "-C", str(self.repo), "rev-parse", "HEAD"], text=True
        ).strip()
        self.state = self.root / "state"
        self.worktree = self.state / "runs" / "r1"
        self.worktree.parent.mkdir(parents=True)
        subprocess.run(
            [
                "git",
                "-C",
                str(self.repo),
                "worktree",
                "add",
                "--detach",
                str(self.worktree),
                self.sha,
            ],
            check=True,
            stdout=subprocess.DEVNULL,
        )
        self.policy = self.root / "policy.json"
        self.policy.write_text(json.dumps(POLICY), encoding="utf-8")
        self.manifest = self.state / "evidence" / "r1" / "allocation.json"
        self.manifest.parent.mkdir(parents=True)

    def tearDown(self):
        self.temp.cleanup()

    def manifest_data(self):
        return {
            "schema_version": 2,
            "run_id": "r1",
            "repository": str(self.repo),
            "source_head": self.sha,
            "source_dirty": True,
            "source_status_porcelain": ["?? local.txt"],
            "state_root": str(self.state),
            "revision": self.sha,
            "worktree": str(self.worktree),
            "evidence": str(self.manifest.parent),
            "state": "ALLOCATED",
            "recovery_policy": "allocate-new-run",
        }

    def test_accepts_allocated_exact_sha_and_recorded_dirty_source(self):
        self.manifest.write_text(json.dumps(self.manifest_data()), encoding="utf-8")
        self.assertEqual("PASS", VERIFY.validate(self.manifest, self.policy)["status"])

    def test_rejects_nonallocated_outside_root_and_nonexact_revision(self):
        data = self.manifest_data()
        data.update(state="UNVERIFIED", revision="HEAD", worktree=str(self.root / "escape"))
        self.manifest.write_text(json.dumps(data), encoding="utf-8")
        with self.assertRaises(SystemExit):
            VERIFY.validate(self.manifest, self.policy)

    def test_rejects_destructive_policy(self):
        self.manifest.write_text(json.dumps(self.manifest_data()), encoding="utf-8")
        policy = json.loads(self.policy.read_text(encoding="utf-8"))
        policy["destructive_recovery"]["allowed"] = True
        self.policy.write_text(json.dumps(policy), encoding="utf-8")
        with self.assertRaises(SystemExit):
            VERIFY.validate(self.manifest, self.policy)


if __name__ == "__main__":
    unittest.main()
