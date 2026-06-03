# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

A Unity 6 (6000.4.6f1) 3D game project using the Universal Render Pipeline (URP). It features a third-person character controller with physics-based movement, animation, audio, and a nature/skybox environment.

## Unity-Specific Workflow

Unity projects are edited primarily through the Unity Editor GUI — C# scripts are the main artifact Claude Code can meaningfully read and edit. Scene files (`.unity`) and prefabs (`.prefab`) are YAML-based binary-adjacent formats; avoid editing them directly.

**To run the project:** Open Unity Hub → open this project (Unity 6000.4.6f1) → press Play in the Editor.

**To run tests:** Window → General → Test Runner in the Unity Editor. The project includes `com.unity.test-framework`.

**Scripts are compiled automatically** by the Unity Editor when saved — there is no manual build step for scripts.

## Architecture

### Character Controller System

The core character logic lives in `Assets/CharacterController/Assets/CharacterControler/Scripts/`:

- **`CharacterControllerBase.cs`** — Physics-driven character controller using `Rigidbody` + `CapsuleCollider`. Handles ground detection via multi-point raycasts (13 sample points), slope handling, step climbing, platform parenting, velocity clipping, jumping (instant or curve-based), coyote time, and double jump. Can run in either `Update` or `FixedUpdate` mode (controlled by `PhysicsManager.s_characterUseFixedUpdate`).
- **`CharacterInput3rdPerson.cs`** / **`CharacterInput1stPerson.cs`** — Read Unity Input System actions and forward `InputMoveVector` / `InputJump` to `CharacterControllerBase`.
- **`Visuals3rdPerson.cs`** — Rotates the visual mesh to face the movement direction.
- **`CameraFollow.cs`** — Third-person camera orbit and follow.
- **`CharacterAudio.cs`** — Plays footstep, jump, and land sounds.
- **`PhysicsManager.cs`** — Global physics configuration (fixed vs. dynamic update mode, simulation mode).

### Input

Uses Unity Input System (`com.unity.inputsystem` 1.19.0). Input actions are defined in `Assets/CharacterController/Assets/CharacterControler/Input/InputCharacter.inputactions`.

### Animation

- **`Assets/CharacterAnimation.cs`** (`AnimationTest` class) — Minimal script wiring keyboard input to Animator parameters: `Speed` (float) driven by W key, `Jump` (trigger) on Space.
- **`Assets/New Animator Controller.controller`** — The Animator Controller asset (currently modified).

### Scene

- **`Assets/Scenes/MyWorldScene.unity`** — Main world scene (currently modified). Uses the nature pack environment and Extended Skybox.

### Imported Assets

- `Assets/ImportedAssets/Assetstore/BOXOPHOBIC/Skybox Cubemap Extended/` — Skybox shader and editor tools.
- Nature Pack assets (PBA) — environment meshes and materials.
- Character controller prefabs: `Character3rdPerson.prefab`, `FullCharacter3rdPerson.prefab`, `Camera3rdPerson.prefab`.

## Key Packages

| Package | Version | Purpose |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.4.0 | URP rendering |
| `com.unity.inputsystem` | 1.19.0 | New Input System |
| `com.unity.ai.navigation` | 2.0.12 | NavMesh |
| `com.unity.timeline` | 1.8.12 | Timeline/cutscenes |
| `com.unity.ai.assistant` | 2.9.0-pre.2 | Unity AI assistant |

## Physics Notes

- `CharacterControllerBase` requires `CapsuleCollider` and `Rigidbody` on the same GameObject; `[DefaultExecutionOrder(1)]` ensures it runs after input scripts.
- The `worldMask` LayerMask must be configured in the Inspector to include ground/wall layers.
- Ground detection uses 13 radial raycast points below the capsule; slopes beyond `maxSlope` are skipped and handled by physics sliding.
