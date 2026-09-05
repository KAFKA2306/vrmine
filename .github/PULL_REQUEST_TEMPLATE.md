## Scope

- What implementation responsibility does this PR complete?
- Which issues does it implement or partially implement?

## PR Merge Gate

- [ ] scope is complete for this PR
- [ ] static / contract checks pass
- [ ] Repository U1 passes
- [ ] changed-surface tests pass
- [ ] no known defect in the changed surface is being hidden as "release-only"

### Generated asset evidence

For generated assets, include multi-angle rendered images directly in this PR. Appearance is evidence, not approval: visual PASS/FAIL, Draft state, unresolved review, and manual approval do not block merge.

### Merge evidence

Record the exact checks / workflow runs used to establish technical generation/integrity evidence. Generated-asset work continues mechanically to merge after generation succeeds.

## Release evidence

Release evidence is tracked separately and is **not required for PR merge unless this PR specifically changes the release infrastructure itself**.

- Unity compile: PASS / FAIL / UNVERIFIED
- canonical scene integrity: PASS / FAIL / UNVERIFIED
- SDK Builder blocking validation: PASS / FAIL / UNVERIFIED
- actual VRChat single-client: PASS / FAIL / UNVERIFIED
- actual VRChat two-client / late join: PASS / FAIL / UNVERIFIED
- performance / playthrough measurement: PASS / FAIL / UNVERIFIED

Link the release verification issue. Do not promote static or Editor-only evidence to actual-client PASS.

## Release impact

- Does this PR change the Product Release Gate itself? If yes, explain why the gate is not weakened.
- Does this PR require new release evidence before the product may be represented as complete/released?
