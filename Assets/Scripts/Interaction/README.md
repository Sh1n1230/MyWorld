# Interaction — インタラクション基盤

`docs/ARCHITECTURE.md` §5.2 / §5.3 / §10.3 の実装。

## 設計の前提（これを外すと方針違反になる）

**Unity は文字を一切持たない**（判断1 / D5）。`Interactable` が持つのは `id` と `labelKey` だけで、
実際に表示する文章は Web(DOM) 側が `labelKey` から引く。ここに日本語の台詞やラベルを
直接書き始めたら設計が壊れていると思ってよい。

## 構成

| スクリプト | 付ける場所 | 役割 |
|---|---|---|
| `Interactable` | （abstract） | id / labelKey / hintRadius / focusRadius を持つ基底。docs §5.3 |
| `InteractableRegistry` | （static） | シーン内の一覧と、正面かつ範囲内の最良候補の探索 |
| `ProximityInteractor` | プレイヤー | 正面の候補を選び、E で選択。Mobile の近接ボタンもこの候補に連動する |
| `PointerInteractor` | プレイヤー | PC 用。マウスの hover を追い、左クリックで選択 |
| `InteractionSignals` | （static） | Interactor が出す通知の集約点。WebBridge がここを購読する予定 |
| `SignalInteractable` | 対象 | 一番単純な実装。UnityEvent を呼ぶだけ |
| `DevPreview/*` | （Editor 専用） | Web ができるまでの見た目確認用。ビルドに含まれない |

## Web への接続点

`InteractionSignals` の 3 つのイベントが docs §7.3 のイベントと対応する。
WebBridge（§10.2、未実装）ができたら、そこがこれを購読して Envelope に詰め替えて送る。
Interactor 側は「誰が聞いているか」を知らない。

| InteractionSignals | docs §7.3 |
|---|---|
| `HoverChanged(interactable, screenPos)` | `HOVER_CHANGED { id, labelKey, screenPos }` |
| `InRangeChanged(interactable)` | `INTERACTABLE_IN_RANGE { id, labelKey }` |
| `ObjectSelected(interactable)` | `OBJECT_SELECTED { id, kind }` |

Web からの `SET_PAUSED` は `ProximityInteractor.Pause()` / `Resume()` に対応する。

## セットアップ

`Assets/Prefabs/Player.prefab` の `Character3rdPerson` に `ProximityInteractor` と
`PointerInteractor` を設定済み。参照はすべて配線済みで、追加の作業は要らない。

対象を増やすときは:

1. 対象のオブジェクトに `SignalInteractable`（または `Interactable` の独自実装）を付ける
2. `Id` を docs §9.3 の命名規約で決める。例: `cafe.npc.smoker.cigarette`
3. `Label Key` を決める。例: `cafe.cigarette.label`。**文章そのものは書かない**
4. `Hint Radius`（発光開始 4m）と `Focus Radius`（操作可 2m）を調整する

`PointerInteractor`（クリック経路）で拾わせたい対象にはコライダーが要る。
`ProximityInteractor`（近づいて E）だけならコライダーは不要。

## 判定のしくみ

- `focusRadius` 以内、かつ `Max Angle`（既定 70°）以内が候補。
- 複数あるときは「正面度」と「近さ」の合成スコアで 1 つに絞る。
  `Angle Weight` を上げると正面のものを、下げると近いものを優先する。
- 向きの基準はプレイヤーの見た目メッシュ（`characterMedium`）。3rd person では
  ルートではなく見た目だけが進行方向を向くため。
- `Check Line Of Sight` は**既定 off**。`Obstacle Mask` を Everything のままで on にすると、
  CAFE では椅子やテーブルが遮蔽物と判定されて手前の対象に反応しなくなる（実測済み）。
  使うなら壁だけを含む専用レイヤーを用意すること。

## Editor プレビュー（`DevPreview/`）

`#if UNITY_EDITOR` で囲ってあるのでビルドには一切入らない。Web ができるまでの間、
Editor で当たり判定と会話の流れを目で確認するためだけのもの。

- `Assets/Editor/DevStringTable.asset` に `key → label / lines` を書く。
  `Assets/Editor/` 配下なのでビルド対象外。
- テーブルに無いキーは `labelKey`（または `id`）がそのまま画面に出る。
- ここに書いた文章は**正本ではない**。正本は Web 側のコンテンツファイル。

WebBridge ができたら、`InteractionSignals` を購読する立場を WebBridge に譲って
`DevPreview/` は役目を終える。

## まだやっていないこと

- `HighlightController`（docs §10.3）— 距離に応じた発光 / アウトライン。
  `Interactable.HintStrength(from)` が 0→1 を返すのでフックは用意済み。
  URP の Renderer Feature を使うか Emission 差し替えにするかはマテリアル側の判断待ち。
- `Portfolio.Runtime.asmdef`（docs §10.1）— 今入れると壊れる。
  `ProximityInteractor` は `CharacterControllerBase` / `Visuals3rdPerson` /
  `SceneIntroSequence` を参照しているが、これらは asmdef の無い Assembly-CSharp にいる。
  asmdef 側から Assembly-CSharp は参照できないので、**先に CharacterController 側にも
  asmdef を切る**必要がある。
- `InputModeController`（docs §10.3）— pointer / touch の切替と `INPUT_MODE_CHANGED`。
- ゲームパッドの Interact 割当 — 面ボタン 4 つがすべて Jump に埋まっているため未割当。
