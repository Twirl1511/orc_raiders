# AGENTS.md

## Project Context

This is a Unity 6000.4.5f1 2D game prototype. The current goal is fast iteration on playable mechanics, not production-grade architecture or final art polish.

The game is UI-heavy: most of the player-facing visuals and interactions live in Unity UI. Prioritize UI that is easy for the developer to edit in the scene/prefabs and easy for the player to read, click, drag, and understand.

## Prototype Priorities

- Build mechanics as quickly as possible while keeping the code understandable.
- Prefer simple, direct implementations over abstract systems until repetition or complexity clearly justifies an abstraction.
- Placeholder art, temporary layout, debug controls, and rough balancing are acceptable when they help validate gameplay faster.
- Put gameplay numbers and parameters into config assets, preferably ScriptableObject configs, instead of scattering them across scene components or burying them in code.
- Favor visible feedback for every important player action, even if the effect is temporary or simple.
- When the quickest useful route requires changing scenes or prefabs, do it.

## Unity Workflow

- It is allowed to modify scenes, prefabs, UI objects, and serialized references when needed for the feature.
- Keep scene and prefab changes focused on the requested mechanic or UI flow.
- After changing serialized fields, prefabs, or scene objects, mention the affected assets in the final response.
- Do not assume a prefab or scene hierarchy is fixed. Improve it when that makes iteration easier.
- Preserve Unity `.meta` files and avoid moving assets without a clear reason.
- Use existing packages and Unity systems already present in the project before adding new dependencies.
- Before implementing or changing gameplay mechanics, read `docs/game-description.md` and keep the implementation consistent with it.
- If mechanic rules change during implementation, update `docs/game-description.md` in the same change.

## Config Workflow

- Follow the config workflow from `D:/WORK/Cradle of Winter`: ScriptableObject configs live as separate assets, and a central `GameplayConfig` references the feature configs used by gameplay.
- Store config assets in `Assets/7_Configs` with numeric prefixes so important configs stay easy to scan and compare.
- Runtime config classes should use `[CreateAssetMenu(..., menuName = "GAME/...")]`, private serialized fields, and public read-only properties.
- Add new mechanic numbers to focused configs instead of putting balance values directly on scene components or prefabs.
- Reference feature configs through `GameplayConfig` or a scene-level config provider. Do not scatter direct config references across every prefab unless the prefab itself is a reusable config-driven UI/control template.
- Because this prototype does not currently include Zenject, use `GameplayConfigProvider` as the lightweight scene entry point. If DI is added later, bind configs from `GameplayConfig` the same way `Cradle of Winter` does in its gameplay installer.
- Use `🛠️Configs🛠️/Open` to edit configs in one window and `🛠️Configs🛠️/Create Default Configs` to create the starter config asset set.

## Code Style

- Keep scripts small and readable. A prototype script may be pragmatic, but it should still be easy to delete, replace, or extend.
- Use clear names that describe gameplay intent.
- Prefer serialized references over runtime `FindObjectOfType`/name searches for scene-critical dependencies.
- Keep gameplay tuning data in named config assets. Use component `[SerializeField]` fields mainly for object references, local presentation settings, and links to configs.
- Group related numbers into focused configs such as unit stats, wave settings, economy, skills, rewards, UI balance, and difficulty curves.
- Make configs easy to duplicate and compare so different mechanic variants can be tested quickly.
- Use events/callbacks only when they simplify the flow. Direct references are fine for prototype features.
- Avoid premature save systems, networking, asset pipelines, service layers, or heavy data frameworks unless explicitly requested.
- Add comments only for non-obvious gameplay logic or temporary prototype shortcuts.

## UI Rules

- UI must be comfortable for both editing and playing.
- Primary gameplay UI should exist as scene or prefab objects visible in Edit Mode. Runtime scripts should use serialized references and only populate dynamic content.
- Use anchors, layout groups, content size fitters, and prefabs where they reduce manual layout work.
- Keep important UI elements readable at common desktop resolutions.
- Avoid overlapping text, tiny click targets, and layout that breaks when labels or numbers change.
- Prefer TextMeshPro for visible text.
- Put reusable UI pieces into prefabs when they are likely to be repeated or tuned often.
- Make key UI state obvious: selected, disabled, affordable/unaffordable, cooldown, empty, full, warning, success.
- If a UI control affects gameplay tuning, connect it to a clear config value instead of hardcoding the value in the UI component.

## Prototype Visuals

- When creating buildings, use simple white 2D sprites with text inside the sprite that says what the building is.
- When creating units, use simple white 2D sprites with a text label above the unit that says what the unit is.
- Keep these placeholder visuals clean, readable, and easy to replace later.

## Gameplay Implementation

- Optimize for a playable loop first: input, decision, result, feedback, repeat.
- Add debug shortcuts or editor-only helpers when they speed up iteration.
- Use simple deterministic logic before complex AI, animation, or balancing.
- Make mechanics testable in the current scene with minimal setup.
- If something is intentionally temporary, name it clearly or leave a short comment.

## Verification

- After code changes, check that the Unity C# project compiles when practical.
- For scene/prefab/UI changes, verify the main scene opens and the changed flow can be exercised.
- If Unity cannot be run in the current environment, say so in the final response and describe what was checked instead.
- Report known prototype limitations honestly instead of polishing around them.

## Communication

- Be concise and practical.
- Explain tradeoffs only when they affect iteration speed, scene/prefab workflow, or player experience.
- In final responses, list changed files/assets and any manual Unity steps the developer should know about.
