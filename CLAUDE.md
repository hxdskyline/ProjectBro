# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

**All documentation must be placed in the `DesignDocs/` folder at the project root.** Do not create .md or .txt docs elsewhere.
**All responses must be in Chinese (中文).** 与用户的所有交流都使用中文。
**思考过程必须使用中文。** 所有内部推理、分析、调试思路都用中文表达。
**已接入 Unity MCP**，启动命令：`C:\Users\hxd_s\.local\bin\uvx.exe --prerelease explicit --from "mcpforunityserver>=0.0.0a0" mcp-for-unity --transport http --http-url http://127.0.0.1:8080 --project-scoped-tools`。如果 MCP 连不上，不要尝试用其他方法代替，直接提醒用户打开 MCP Server。prefab/场景/GameObject 操作由你自动完成，不要等用户确认。
**所有操作直接执行**，包括 bash、MCP 工具调用等，不要询问用户确认（--yes 模式）。
**Unity 组件添加/修改由你完成**，不要让用户手动在 Inspector 中操作。如果需要给 GameObject 添加组件，通过 MCP 或修改 prefab/脚本自动完成。
**UI 必须使用预制体**：所有 UI 元素（按钮、列表、卡片等）必须做成预制体或子预制体，通过预制体实例化生成。禁止用代码 `new GameObject` + `AddComponent` 现拼 UI。所有组件（包括 LayoutElement、ContentSizeFitter 等布局组件）都必须在预制体中预先添加好。ScrollRect、Viewport、Content 等滚动容器应作为父预制体的子节点（而非单独预制体）；条目类 UI（列表项、buff 条目等）应做成单独子预制体，通过 Instantiate 实例化。
**UI 加载规则**：代码中通过 `transform.Find` 或序列化字段引用预制体中的节点。找不到节点时报错，不要代码 fallback 创建。只有大量重复的动态列表项（滑杆行、敌人卡片、tab 按钮等）才允许动态创建，但也必须基于子预制体 Instantiate，禁止裸 `new GameObject`。

## Build & Run

## Project Overview

猫村守护者 (Cat Village Guardians) — a Unity tribe-commanding strategy game. Players lead 6 cat tribes through a roguelike loop of recruit/ritual/shop/battle rounds, aiming to defeat a final Boss.

Unity version: **2022.3.62f3c1** (China edition). Build via Unity Editor manually — no CLI build automation exists.

## Build & Run

- Open in Unity Editor (2022.3.x China edition)
- Open scene `Assets/Scenes/MainScene.unity`
- Press Play — `DemoStarter.cs` or `GameInitializer.cs` bootstraps the game
- No automated tests exist in the project. The test framework package is installed but unused.

## Architecture

### Initialization

Two entry scripts (scene uses one or the other):
- `DemoStarter` — simple: loads `GameManager.Instance.LoadGame()` → shows MainPanel
- `GameInitializer` — full: loads player data, configs (`TribeConfigLoader`), hands off to `GameFlowController`

### Core Singletons

`GameManager` (DontDestroyOnLoad) is the hub. It initializes all subsystems in `Awake()`:
- `ResourceManager` — Addressable Asset wrapper
- `TableReader` — reads JSON from `StreamingAssets/Tables/` via LitJson
- `DataManager` + `CurrencyManager` — persistence (`playerdata.json` via JsonUtility)
- `UIManager` — loads panels via Addressables, 5 layers (Background/Normal/Top/PopUp/Alert)
- `BattleCampaignRuntime` — in-memory campaign progress (not persisted)
- `GameFlowController` — state machine: InitialSelection → RoundPreparation → BattlePhase → GameOver

Access everything through `GameManager.Instance.XxxManager`.

### Game Flow State Machine

`GameFlowController` drives the round loop. States: `Uninitialized` → `InitialSelection` (pick 2 of 6 tribes) → `RoundPreparation` (recruit/ritual/shop, driven by `TribeBuildPanel`) → `BattlePhase` → `GameOver`.

`RoundManager` controls which events appear each round (recruitment, ritual, shop, battle) within a 10-round cycle.

### TribeSystem (`Assets/Scripts/Game/TribeSystem/`)

Core gameplay services. All files use namespace `TribeSystem`.
- `TribeData.cs` — all data types: `TribeType`, `CatQuality`, `TribeRecord`, `LeaderData`, `CatData`, enums
- `TribeStatsCalculator.cs` — stat formulas: leader final stats (base + permanent/temp buffs), cat stats (leader * quality ratio), command penalty, damage formula
- `TribeConfigLoader.Instance` — singleton that loads all JSON configs
- `RecruitmentService` / `RitualService` / `ShopService` — gameplay services

### Battle System (`Assets/Scripts/Game/Battle/`)

Global namespace (no TribeSystem prefix).
- `BattleManager` — orchestrates battle lifecycle, exposes `PlayerFighters`/`EnemyFighters`
- `BattleSimulation` — tick-based combat loop: AI, movement, attack resolution, consumable effects
- `BattleFighter` — per-unit state (HP, attack cooldown, freeze timer, death state)
- `BattleFlowController` — thin wrapper managing BattleManager lifecycle

Damage formula in `BattleSimulation.UpdatePendingHit`:
```
DMG = MAX(CATK - CDEF, 0)
DR = MAX(1 - CDEF / (CDEF + 100), 0.2)
FDMG = MAX(DMG * DR * SkillMultiplier, 1) + TrueDamage
```

### UI Framework

- `UIPanel` — base class with Show/Hide/Close and alpha fade via CanvasGroup
- `UIManager` — loads prefabs by address string, manages layered panels
- Panels are `UIPanel` subclasses, loaded by address (e.g. `"ui/mainpanel"`)
- UI panels under `UI/TribeBuild/` use namespace `TribeSystem.UI`

### Data Persistence

`DataManager` saves to `Application.persistentDataPath/PlayerData/playerdata.json` via `JsonUtility`. The `PlayerData` class contains: tribes, currentRound, catFood, consumables, shopRefreshCount, round-completion flags. Use `saveImmediately` parameter on mutation methods.

### JSON Configs (`Assets/StreamingAssets/Tables/`)

- `tribe_config.json` — 6 tribe base stats, deploy cost
- `quality_config.json` — 4 quality tiers (ratio ranges, probability weights)
- `recruitment_config.json`, `ritual_config.json`, `shop_config.json` — event configs
- Loaded by `TribeConfigLoader` (tribe-specific) and `TableReader` (general, via LitJson)

## Key Conventions

- **Namespaces**: `TribeSystem` for Game/TribeSystem/*, `TribeSystem.UI` for UI/TribeBuild/*, global namespace for Framework/* and Game/Battle/*
- **File naming**: PascalCase, class name = file name
- **JSON field naming**: snake_case in JSON configs, camelCase in C# classes (mapped via LitJson)
- **Config changes**: Edit JSON files in `StreamingAssets/Tables/`, no rebuild needed — they're read at runtime
- **UI panels**: Created/loaded at runtime via UIManager, not baked into scene hierarchy (except BattlePanel which builds its UI in code)

## Design Docs

**需求以 `DesignDocs/正式文档` 为准。** 正式文档是需求的唯一来源。

`DesignDocs/描述性介绍/` 仅作整理用，**除非用户明确要求，不要主动读取。**

Located in `DesignDocs/`:
- `正式文档/` — 正式需求文档（权威来源）
- `描述性介绍/` — 整理性文档，不作为需求依据
- `旧需求/` — 旧版需求（已过时）

**人工添加**
研发期间，功能全部都不做存档兼容。每次运行我都会清空存档。