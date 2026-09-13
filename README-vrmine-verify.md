# vrmine verification

`vrmine.cmd verify` is the evidence-producing verification entry point for
this checkout. It reads `config/vrmine-request.json`, resolves the exact source
revision, captures a dirty-tree snapshot when needed, and runs the selected
checks in a detached Git worktree.

Each run is stored below the configured verification root as
`runs/<run-id>/.vrmine-verify/`. The run contains the manifest, snapshot,
summary, hashes, logs, test results, and producer evidence. Results use three
states: `PASS`, `FAIL`, and `UNVERIFIED`.

Surface detection selects the required static, VPM, Blender, Unity, EditMode,
and PlayMode checks, plus the optional Unity bridge probe when configured. VPM
runs before Unity and Blender; a failed or unverified required upstream gate
stops downstream work. `vrmine verify --full` selects the complete configured
surface. `vrmine verify --clean` starts a fresh isolated run while preserving
prior evidence; cleanup is maintenance-only.

The verifier validates the local Unity/VRChat SDK toolchain and bridge state. It
does not publish or upload a world to VRChat.

`powershell -NoProfile -File scripts/vrmine-maintenance.ps1` is a dry-run
report for old terminal runs. A separately scheduled maintenance job may pass
`-Apply`; it can remove only runs that are old enough, owned by this verifier,
Git-clean, and not held by a live process. Normal verification never invokes
physical cleanup.
