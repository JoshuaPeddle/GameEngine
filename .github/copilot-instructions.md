# GameEngine - Entity Component System Game Engine

## Overview
This repository contains a cross-platform 2D game engine built in C# using Entity Component System (ECS) architecture. The engine runs on desktop, mobile (Android), and web browsers via WebAssembly, with Avalonia UI as the primary rendering framework and SkiaSharp for graphics.

## Architecture & Key Concepts

### Entity Component System (ECS) Pattern
The engine follows ECS architecture where:
- **Entities**: Game objects identified by unique IDs, containing components
- **Components**: Data containers that define entity properties (position, animation, input, etc.)  
- **Systems**: Logic processors that operate on entities with specific component combinations

Architecture (essentials)
- Entities (GameEngine.Core.Entity): containers of Components. Use AddComponent/RemoveComponent. Components are keyed by exact Type.
- Components (derive from GameEngine.Core.Component): data-only. Examples: CTransform, CBoundingBox, CAnimation, CMovement, CInput, CGravity, CCamera, CText.
- Systems (implement GameEngine.Core.ISystem): Update(EntityManager em, double deltaMs). Use em queries to process entities that have specific components.
- EntityManager: CreateEntity(tag), GetEntitiesWithComponent<T>(), GetEntitiesWithComponents<T>(), GetEntitiesWithComponents<T1,T2>(), GetEntitiesWith<T>(). Maintains component→entity maps. Entities created are added on next Update().
- Engine: Initializes systems in order: Input, Movement, Physics, Animation, Render, Audio (optional). Update loop supplies delta in milliseconds.
- Scene: abstract; Initialize(em, input, audio, ResetScene). Optional Update overloads, VirtualWidth/VirtualHeight for scaling.
- Rendering: RenderSystem.DrawEntitiesToCanvas(SKCanvas). Honors RenderOptions (VirtualWidth/Height, ScalingStrategy, FPS, debug flags) and optional CCamera.
- Assets: Assets reads assets.txt; supports Texture, Animation, Sound. _fileFetcher enables platform-specific loading.
- Physics: PhysicsSystem handles gravity and AABB collisions (CBoundingBox, CTransform, CGravity). Emits CollisionEvents.

Key usage patterns
- Delta time is milliseconds. Convert when needed: seconds = deltaMs / 1000.0.
- When a System needs components, use EntityManager queries to ensure presence:
  - em.GetEntitiesWithComponent<T>() returns entities that have T.
  - em.GetEntitiesWithComponents<T>() returns (Entity,T) tuples.
  - em.GetEntitiesWithComponents<T1,T2>() returns (Entity,T1,T2) tuples.
- Rendering should stay inside RenderSystem. Other systems must not use SkiaSharp.
- Core must remain platform-agnostic. Do not depend on Avalonia/WinForms from GameEngine.Core.

Best practices (do/avoid)
- Do: Check component definitions before using them. Use TryGetComponent/HasComponent or the "WithComponents" queries. GetComponent<T>() throws if missing.
- Do: Keep Components as data holders; put logic in Systems.
- Do: Keep per-frame loops allocation-free where possible. Avoid LINQ in tight loops.
- Do: Respect existing style and visibility (public fields are used in components like CTransform).
- Do: Keep new files small and focused. Prefer adding a new System over ad-hoc logic.
- Avoid: Modifying Engine main loop control flow. Avoid changing delta units. Avoid UI/framework code in Core.

Common extensions (how to implement)
- New Component: create GameEngine.Core/Components/MyComponent.cs, derive from Component, add data fields/properties only.
- New System: create GameEngine.Core/Systems/MySystem.cs implementing ISystem.Update. Query entities via EntityManager, convert deltaMs to seconds if needed, mutate component data.
- Scene logic: in Initialize, create entities via em.CreateEntity(tag), add required components. Use provided AudioSystem via Initialize parameter if present.
- Rendering feature flag: extend RenderOptions and handle in RenderSystem with minimal branching.

Notes
- Target frameworks: .NET 8/9. Use modern C# features already present (records/readonly structs, target-typed new, collection expressions).
