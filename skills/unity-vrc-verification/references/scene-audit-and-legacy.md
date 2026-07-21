# Scene Audit and Legacy Assets

## Scene audit

Open candidate scenes one at a time and capture the Console delta for each. Inspect descriptor count, spawn points, reference camera, missing scripts, UdonSharp backing programs, pickups, rigidbodies, object sync, colliders, and the expected game controller. Restore the selected release scene after the audit.

A scene can pass structure checks while producing import, serialization, or build-preprocessing errors. Any new error fails the scene audit.

## Legacy prefab preprocessing

Older UdonSharp prefabs can throw during Play Mode or VRChat build preprocessing even when their scene instances look correct. Trace the stack to the prefab and Udon program asset. If the scene instance is the only release target, unpack that instance, preserve the original licensed prefab, save, and rerun the same preprocessing gate. Do not rewrite unrelated package assets.

## Unity object null semantics

`UnityEngine.Object` uses Unity's overloaded null behavior. Do not use C# null-coalescing to decide whether an Editor reference exists. Use an explicit Unity null comparison, assign the reference, mark the component dirty, mark the scene dirty, save, and reopen before verification.

## UdonSharp proxy lifecycle

An Editor verifier calling a UdonSharp C# proxy directly does not prove that private state initialized only by the backing Udon `Start` event exists on the proxy. Extract a public deterministic initialization/reset method and invoke it from both `Start` and the verifier. Assert observable scene or domain state after the call.

## ClientSim object reset

Calling object-sync respawn APIs on kinematic rigidbodies can produce velocity-write errors in ClientSim. For transform-based tabletop reset, require ownership, restore the captured transform, zero only valid physics state, call the synchronization discontinuity API, and verify the remote result in G3. Never hide the Console error.
