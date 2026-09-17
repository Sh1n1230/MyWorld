# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

## Project Overview

Unity 6 (6000.6.0f1) / URP。**Interactive 3D Portfolio** の Unity 側リポジトリ。
訪問者が CAFE → VILLAGE → FACTORY を歩き回って作者のポートフォリオを発見していく Web 体験の、
「世界」を描く部分を担当する。

**設計の正本は `docs/ARCHITECTURE.md`（v2）。実装に入る前に必ず読むこと。**
Unity ↔ Web の通信契約は `docs/EVENT_SCHEMA.md`（Web リポジトリと共有する契約）。
構想・方向性は `docs/VISION.md` — **こちらは決定ではないので、実装の根拠にしてはいけない。**
v1 とその周辺資料は `docs/archive/` にある（歴史的記録。参照しない）。

次の 4 つはこのリポジトリのほぼ全ての判断の前提になっている:

- **D1 — Unity は文字を一切持たない。** 台詞も説明文も UI 文字列も Web(DOM) が描く。
  Unity が持つのは `id` と `labelKey` だけ。TMP の日本語フォントアトラスは明示的にやらない。
  現状 `PlayerChoose.unity` / `Cafe.unity` ともにテキストは **0 件**。この状態を壊さないこと。
  この判断を守るため、`SET_LANGUAGE` イベントは**意図的に存在しない**（Unity に言語を教えない）。
- **D2 — Unity インスタンスは一度ロードしたらアンマウントしない。**
  情報表示は DOM オーバーレイ + URL 同期。シーン遷移は Unity 内で完結する。
- **D4 — 3D 世界のスクリーンリーダー対応はしない。** Unity WebGL の canvas は
  アクセシビリティツリーを持たず、`UnityEngine.Accessibility` は WebGL 対象外。
  アクセシブルな経路は Phase 2 の「静的ポートフォリオ」が担う。
- **Phase 1 のスコープ** — CAFE のみ、interactable 2 個
  （`cafe.npc.smoker.cigarette` / `cafe.exhibit.penguin`）、対話 UI 1 種、**非公開**。
  カフェの interactable は最終的にも上限 4 個。

## Unity-Specific Workflow

Unity プロジェクトは基本的に Editor GUI で編集する。C# スクリプトが Claude Code の主な作業対象。
シーン(`.unity`)とプレハブ(`.prefab`)は YAML なので**直接編集しない** — UnityMCP 経由で
Editor に操作させる。

**起動:** Unity Hub → このプロジェクト(6000.6.0f1) → Play。

**UnityMCP:** `http://127.0.0.1:8080/mcp`。Editor が起動していないと繋がらない。
セッション開始時に落ちていると `ConnectionRefused` のまま固定されるので、
その場合は Editor を起動してから `/mcp` で再接続する。

**テスト:** Window → General → Test Runner。

**スクリプトは保存すると自動でコンパイルされる。** 変更後は `read_console` でエラーを確認する。

## Architecture

### Interaction（`Assets/Scripts/Interaction/`）

docs §5.2 / §5.3 / §10.3 の実装。詳細は `Assets/Scripts/Interaction/README.md`。

- **`Interactable.cs`** — abstract。`id` / `labelKey` / `hintRadius`(4m) / `focusRadius`(2m)。
  **文章は持たない。**
- **`InteractableRegistry.cs`** — シーン内の一覧と、正面かつ範囲内の最良候補の探索。
- **`ProximityInteractor.cs`** — プレイヤーに付く。正面の候補を選び E で選択。`Pause()`/`Resume()`
  が Web の `SET_PAUSED` に対応。
- **`PointerInteractor.cs`** — PC 用。マウス hover + 左クリック。対象にコライダーが要る。
- **`InteractionSignals.cs`** — Interactor の通知の集約点。`WebBridge` がここを購読する。
  **購読するのは `InRangeChanged` / `ObjectSelected` の 2 つだけ。**
  `HoverChanged` は購読しない — `HOVER_CHANGED` イベントは作らないと決めてある
  （PC の hover は Unity 内のアウトライン表現だけで足りるため）。
- **`DevPreview/`** — `#if UNITY_EDITOR` 限定のプレビュー UI。ビルドに含まれない。
  文字列は `Assets/Editor/DevStringTable.asset`（Editor 配下なのでビルド対象外）から引く。

### Character Controller System

`Assets/CharacterController/Assets/CharacterControler/Scripts/`:

- **`CharacterControllerBase.cs`** — `Rigidbody` + `CapsuleCollider` の物理キャラコン。
  13 点レイキャストでの接地判定、斜面、段差、足場の親子付け、速度クリップ、ジャンプ
  （即時 / カーブ）、コヨーテタイム、二段ジャンプ。`Update` / `FixedUpdate` の切替は
  `PhysicsManager.s_characterUseFixedUpdate`。
- **`CharacterInput3rdPerson.cs`** / **`CharacterInput1stPerson.cs`** — Input System を読んで
  `InputMoveVector` / `InputJump` に流す。
- **`Visuals3rdPerson.cs`** — 見た目メッシュを進行方向へ回す。**ルートは回らない**ので、
  「プレイヤーの正面」はこの Transform を見ること。
- **`CameraFollow.cs`** — 3rd person カメラ。
- **`CharacterAudio.cs`** — 足音・ジャンプ・着地。
- **`PhysicsManager.cs`** — 物理の全体設定。

### Audio / 演出

- `Assets/Scripts/Audio/` — `BgmManager` と各種トリガ（Scene / Zone / Time）。
- `Assets/Scripts/SceneIntroSequence.cs` — 入店演出。実行時に Canvas を自前で組む。
  演出中は `SceneIntroSequence.IsPlaying` が true になり、Interactor はその間止まる。
- `Assets/Scripts/MenuController.cs` — Esc メニュー。
- `Assets/Scripts/DevSettings.cs` — `SkipIntro` など開発用フラグ。
- `Assets/Scripts/Cafe/CigaretteSmokeController.cs` — 喫煙パーティクルの制御。

### Web Bridge（`Assets/Scripts/Web/`）

- **`WebBridge.cs`** — Unity ↔ Web の唯一の窓口。`DontDestroyOnLoad` の GameObject `WebBridge`
  に付く。**`SendMessage` の受け口はこの 1 つだけ**（`WebBridge.Receive`）。
  `InteractionSignals` を購読して Envelope に詰め替える。
  未知の `type` は warn して無視する — **絶対に例外を投げないこと**（前方互換のため）。
- **`Assets/Plugins/WebGL/PortfolioBridge.jslib`** — Unity → Web。
  `window.dispatchEvent(new CustomEvent("unity:message", { detail }))` を投げる。
- エディタ実行時は `.jslib` を呼ばず `Debug.Log` に出るので、Editor だけでも配線を確認できる。
- **イベントは全 11 種。増やす前に `docs/EVENT_SCHEMA.md` を読み、
  「Web 側だけで解決できないか」を必ず疑うこと。**

### Input

Input System 1.20.0。`Assets/CharacterController/Assets/CharacterControler/Input/InputCharacter.inputactions`。

- Character: `Move` / `Look` / `Jump` / `MouseDelta` / `Interact`(E)
- UI: `Menu`(Esc)

`.inputactions` を編集すると `InputCharacter.cs` が自動再生成される（`generateWrapperCode: 1`）。
ゲームパッドの面ボタン 4 つはすべて `Jump` に割り当て済みなので、`Interact` は未割当。

### Scenes

| シーン | 状態 |
|---|---|
| `Assets/Scenes/PlayerChoose.unity` | **起動シーン。** キャラ選択。Build Settings で有効 |
| `Assets/Scenes/Cafe/Cafe.unity` | メイン。Phase 1 の対象。Build Settings で有効 |
| `Assets/Scenes/Village.unity` | Phase 4。今は触らない |
| `Assets/Scenes/Factory.unity` | Phase 4。今は触らない |

`EditorBuildSettings.asset` に**存在しない `SampleScene.unity` への死んだ参照**が残っている
（`enabled: 0`）。Phase 1 で削除する。

`Assets/Prefab/Player.prefab` が全シーン共通のプレイヤー。
中身は `FullCharacter3rdPerson.prefab`（= `Camera3rdPerson` + `Character3rdPerson`）に
Kenney の `characterMedium` を差し込んだもの。元の `Visuals` と `DebugCube` は無効化済み。
`PlayerChoose` で選んだマテリアルは `CharacterSelection`（static）→ `PlayerMaterialApplier`
（Player.prefab に付く）の経路で反映される。キャラは 2 体、**見た目だけで意味は持たせない**。

`Cafe.unity` の中身: NPC 1 体（着席アイドル）、`SignalInteractable` 2 個
（`cafe.npc.smoker.cigarette` / `cafe.exhibit.penguin`）、`CafeSequence`、ParticleSystem 1 個（煙）、
`SceneIntroSequence`、`BgmSceneTrigger`。**専用カメラは無く、NavMesh も未ベイク。**

## Key Packages

| Package | Version | Purpose |
|---|---|---|
| `com.unity.render-pipelines.universal` | 17.6.0 | URP |
| `com.unity.inputsystem` | 1.20.0 | New Input System |
| `com.unity.ugui` | 2.6.0 | uGUI（TextMeshPro を含む） |
| `com.unity.probuilder` | 6.1.2 | 内装のモデリング |
| `com.unity.ai.navigation` | 2.0.14 | NavMesh |
| `com.unity.timeline` | 6.6.0 | Timeline |

## Physics Notes

- `CharacterControllerBase` は同じ GameObject に `CapsuleCollider` と `Rigidbody` を要求する。
  `[DefaultExecutionOrder(1)]` で入力スクリプトより後に走る。
- `worldMask` は Inspector で地面・壁のレイヤーを含むよう設定が必要。
- 接地判定はカプセル下の 13 点レイキャスト。`maxSlope` を超える斜面は物理の滑りに任せる。
- **カスタムレイヤーが 1 つも定義されていない。** 視線判定や遮蔽判定でレイヤーマスクを
  使いたい場合は先にレイヤーを切ること（`ProximityInteractor.checkLineOfSight` が
  既定 off なのはこれが理由）。
