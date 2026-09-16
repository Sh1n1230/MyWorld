# EVENT_SCHEMA — Unity ↔ Web の通信契約

> Status: **v1 / protocol `v: 1`** — 2026-09-09 制定
> このファイルは **Unity リポジトリと Web リポジトリの両方が参照する契約**である。
> 変更するときは両方のリポジトリを同時に直すこと。片方だけ直すと、例外は出ずに**ただ無反応になる**。

---

## 1. 前提

- **Unity は文字を一切持たない。** メッセージに載るのは `id` と `labelKey` だけで、文章は Web が持つ。
- **Unity は言語を知らない。** `SET_LANGUAGE` は**意図的に存在しない**（§5）。
- **Unity インスタンスはアンマウントしない。** シーン遷移は Unity 内部で完結し、Web は関与しない。
- iframe は使わない。WebSocket も HTTP API も使わない。通信路は `SendMessage` と `.jslib` の 2 本だけ。

---

## 2. 封筒（Envelope）

すべてのメッセージを 1 つの型に統一する。種類ごとにメソッドを生やすと、`SendMessage` の受け口（GameObject 名 + メソッド名）の管理が破綻する。

```ts
type Envelope = {
  v: 1;              // プロトコルバージョン
  id: string;        // メッセージ ID（相関・重複排除用。crypto.randomUUID() など）
  type: string;      // 下表の type
  payload: unknown;  // type ごとの構造
  ts: number;        // 発行時刻（epoch ms）
};
```

**Web → Unity**

```js
unityInstance.SendMessage("WebBridge", "Receive", JSON.stringify(envelope));
```

**Unity → Web**

```
.jslib の PortfolioEmit(ptr)
  → window.dispatchEvent(new CustomEvent("unity:message", { detail: JSON.parse(str) }))
```

Web 側は `zod` で封筒をパースし、`type` ごとに payload を検証する。
Unity 側は `JsonUtility` で受け、**未知の `type` は warn して無視する**（前方互換。絶対に例外を投げない）。

---

## 3. イベント一覧（Phase 1 = 全 12 種）

**この 12 種がすべてである。** 増やしたくなったら、まず「Web 側だけで解決できないか」を疑うこと。

### 3.1 Unity → Web（7 種）

| # | type | payload | 用途 |
|---|---|---|---|
| 1 | `BRIDGE_READY` | `{ protocolVersions: number[], unityVersion: string }` | ハンドシェイク。**これが来るまで Web は送信をキューする** |
| 2 | `SCENE_READY` | `{ scene: "player_choose" \| "cafe" }` | シーンの初期化完了。ローディング表示を消す |
| 3 | `CHARACTER_SELECTED` | `{ characterId: string }` | `PlayerChoose` でキャラが選ばれた。Web は localStorage に保存し、カフェへの遷移を待つ |
| 4 | `INTERACTABLE_IN_RANGE` | `{ id: string \| null, labelKey: string \| null }` | 操作可能な対象が圏内に入った / 出た。Web はプロンプトを出し入れする |
| 5 | `OBJECT_SELECTED` | `{ id: string }` | 汎用の選択通知。**Web が id から表示内容を決める**（ペンギンなど） |
| 6 | `DIALOGUE_REQUESTED` | `{ dialogueId: string, speakerId: string \| null }` | Unity 側の演出が特定の対話を要求する（NPC のように、選択後の演出が Unity 主導のもの） |
| 7 | `SEQUENCE_STATE` | `{ sequenceId: string, state: string }` | 演出の進行段階。Web はこれでタイトル演出などを出し入れする |

### 3.2 Web → Unity（5 種）

| # | type | payload | 用途 |
|---|---|---|---|
| 8 | `SET_PAUSED` | `{ paused: boolean }` | DOM の UI を出している間、Unity の入力を止める |
| 9 | `SET_AUDIO_MUTED` | `{ muted: boolean }` | ミュートボタン / 音声トーストの反映 |
| 10 | `ADVANCE_DIALOGUE` | `{ dialogueId: string, lineIndex: number, emotion?: string }` | 対話送り。**進行の主導権は Web 側**（行数を知っているのは Web だから） |
| 11 | `END_DIALOGUE` | `{ dialogueId: string }` | 対話終了。Unity は次の演出（着火など）に進む |
| 12 | `RESTORE_SESSION` | `{ characterId: string \| null, seen: string[] }` | **`BRIDGE_READY` の直後に必ず 1 回送る。** 再訪者にキャラを選ばせず、見終わった演出を繰り返さないため（§10）。初回は `{ null, [] }` |

### 3.3 `sequenceId` / `state` の値（Phase 1）

`sequenceId: "cafe.intro"`

| state | 意味 | Web の反応 |
|---|---|---|
| `entering` | 入店演出中 | 操作説明を出さない。ローディングは既に消えている |
| `explore` | 自由行動 | 操作ヒントを表示。音声トーストを一度だけ出す |
| `focusing` | 対象にカメラを寄せている（0.6s） | 何もしない |
| `lighting` | 着火演出中（1.2s） | 対話ボックスを閉じ切る |
| `smoke` | 煙をカメラが追っている | 何もしない |
| `title` | タイトル演出のタイミング | `WELCOME TO MY WORLD` / `SHIN NAKAMURA` を出す |
| `free` | 演出終了、以降は自由行動 | タイトルを消し、`SET_PAUSED { false }` を送る |

---

## 4. カフェの一連の流れ（Phase 1 の正）

```
Web                                             Unity
 │  createUnityInstance() 開始                    │
 │───────────────────────────────────────────────▶│
 │  （送信はすべてキューに積む）                    │  WebBridge.Awake()
 │◀──────────────── BRIDGE_READY ─────────────────│
 │  protocolVersions に 1 が含まれるか確認          │
 │  キューをフラッシュ（SET_AUDIO_MUTED など）      │
 │◀──────────── SCENE_READY { player_choose } ────│
 │  ローディング表示を消す                         │
 │  （アンケートが未完なら、ここで完了を待つ）        │
 │◀──────────── CHARACTER_SELECTED { id } ────────│
 │  localStorage に保存                            │  Unity 内でカフェへ遷移
 │◀──────────── SCENE_READY { cafe } ─────────────│
 │◀──────── SEQUENCE_STATE { entering } ──────────│
 │◀──────── SEQUENCE_STATE { explore } ───────────│
 │  操作ヒント + 音声トースト（初回のみ）            │
 │◀────── INTERACTABLE_IN_RANGE { cigarette } ────│
 │  プロンプト表示                                 │
 │                                （プレイヤーが E / クリック）
 │◀──────── SEQUENCE_STATE { focusing } ──────────│
 │◀── DIALOGUE_REQUESTED { "cafe.intro" } ────────│
 │─────────── SET_PAUSED { true } ───────────────▶│
 │  対話ボックスを開く                              │
 │──── ADVANCE_DIALOGUE { lineIndex: 0 } ────────▶│
 │  …（6〜8 行）…                                  │
 │──── ADVANCE_DIALOGUE { lineIndex: n } ────────▶│  最終行 =「火、持ってない?」
 │──────── END_DIALOGUE { "cafe.intro" } ────────▶│
 │◀──────── SEQUENCE_STATE { lighting } ──────────│
 │◀──────── SEQUENCE_STATE { smoke } ─────────────│
 │◀──────── SEQUENCE_STATE { title } ─────────────│
 │  WELCOME TO MY WORLD を出す                     │
 │◀──────── SEQUENCE_STATE { free } ──────────────│
 │  タイトルを消す                                  │
 │─────────── SET_PAUSED { false } ──────────────▶│
```

ペンギン（作品展示）はこの流れを通らない。

```
 │◀── INTERACTABLE_IN_RANGE { penguin } ──────────│
 │◀── OBJECT_SELECTED { "cafe.exhibit.penguin" } ─│
 │─────────── SET_PAUSED { true } ───────────────▶│
 │  id から content を引いて、対話ボックス（話者行なし）で表示
 │─────────── SET_PAUSED { false } ──────────────▶│
```

**この 2 経路が両方動くことが、「Unity は id しか知らない」設計の実証になる。**

### 4.1 同じ選択で 2 つ送らないこと

タバコは `DIALOGUE_REQUESTED` の経路を通るので、**`OBJECT_SELECTED` を送ってはいけない**。
両方送ると Web 側が二重に反応する。

Unity 側は `Interactable.NotifyWebOnSelect` でこれを表現している。

| 対象 | `NotifyWebOnSelect` | 送るもの |
|---|---|---|
| `cafe.npc.smoker.cigarette` | **false**（Inspector でチェックを外す） | `DIALOGUE_REQUESTED`（`CafeSequence` が送る） |
| `cafe.exhibit.penguin` | true（既定） | `OBJECT_SELECTED` |

`SignalInteractable` では `notifyWebOnSelect` という serialize されたフラグとして出ている。
**既定は true なので、タバコだけ手で外す必要がある。**

---

## 5. 意図的に存在させないもの

| 落としたもの | 理由 |
|---|---|
| `SET_LANGUAGE` | Unity に文字が無い以上、言語を知る理由がない。持たせると「じゃあ Unity にも少しくらい文字を」という穴になる。**この設計を守るための欠落である** |
| `GOTO_SCENE` | `PlayerChoose` → `Cafe` の遷移は Unity 内で完結する。Web が指図する必要がない。再訪時に `PlayerChoose` を飛ばす件は `RESTORE_SESSION`（§10）で解いた — **シーンを指図するのではなく、状態を預けて Unity に決めさせる** |
| `HOVER_CHANGED` | PC の hover は Unity 内のアウトライン表現だけで用が足りる。毎フレーム相当の頻度で JS を叩く価値がない |
| `ERROR` | Unity 側の異常はコンソールに出す。Web に投げても Phase 1 では出し先がない |
| Phase 2 以降のイベント（`OPEN_PROJECT` 等） | 使われないメッセージ定義は必ず腐る。**必要になった日に足す** |

---

## 6. ID 命名規約

```
<area>.<category>.<name>[.<part>]
```

**この文字列だけが Unity と Web をつなぐ。** 名前がずれても例外は出ず、ただ無反応になる。

### Phase 1 に存在する ID（実装済みのものは ✅）

| ID | 種別 | 状態 |
|---|---|---|
| `cafe.npc.smoker` | NPC | シーンに存在（`Interactable` は付いていない） |
| `cafe.npc.smoker.cigarette` | Interactable | ✅ `Cafe.unity` に設置済み |
| `cafe.exhibit.penguin` | Interactable | 未設置 |
| `cafe.staff` | 背景 NPC | 未設置。**`Interactable` を付けない** |

### labelKey

| labelKey | 使う場所 | 状態 |
|---|---|---|
| `cafe.cigarette.label` | ✅ 設置済み | |
| `cafe.penguin.label` | 未設置 | |

### dialogueId

| dialogueId | 内容 |
|---|---|
| `cafe.intro` | NPC の 6〜8 行。最終行が「火、持ってない?」 |
| `cafe.exhibit.penguin` | ペンギンの説明（話者行なし、1〜2 行） |

Web 側には **「未知の id / labelKey / dialogueId を受け取ったら `console.warn` する」防御を必ず入れる。**

---

## 7. ハンドシェイクと異常系

| 異常 | 挙動 |
|---|---|
| `BRIDGE_READY` が 60 秒来ない | ローディング画面にエラー + リロード導線 + 名刺ページへのリンク |
| プロトコルバージョン不一致 | Web がコンソールに警告。Unity 再ビルドが必要な旨を開発者向けに表示 |
| 未知の `type` | 双方向とも warn して無視。**絶対に例外を投げない** |
| payload の検証失敗 | warn して無視。UI は変化させない |
| 未知の `id` / `dialogueId` | Web が warn。UI は変化させない（無反応になるが世界は止まらない） |

---

## 8. バージョニング

- 封筒の `v: 1` は、**既存イベントの payload 変更・削除**を行うときだけインクリメントする。
- イベントの**追加**ではバージョンを上げない（未知の type は無視される設計なので安全）。
- Unity は `BRIDGE_READY` で `protocolVersions: [1]` を申告し、Web が対応範囲を判定する。

---

## 9. Unity 側のセットアップ手順（Editor 作業）

スクリプトは実装済み。以下は Editor でしかできない配線。

1. **`WebBridge` は置かなくてよい。** `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で
   自分を `WebBridge` という名前の GameObject として生成し、`DontDestroyOnLoad` する。
   **この名前が `SendMessage` の宛先そのもの**なので、手で置くと typo で無反応になり得る。
2. `Cafe.unity` の `SignalInteractable`（`cafe.npc.smoker.cigarette`）の
   **`Notify Web On Select` のチェックを外す**（§4.1）。
3. `Cafe.unity` に空の GameObject を作り、`CafeSequence` を付ける。Inspector で繋ぐもの:
   - `Cutscene` — 同じシーンに置いた `CigaretteCutscene`
   - `Focus Target` — NPC の顔／タバコの位置に置いた空オブジェクト
   - `Smoke Target` — 煙のパーティクルの少し上に置いた空オブジェクト
   - `Smoke` — 既存の `CigaretteSmokeController`
   - `Npc Animator` — NPC の Animator（着火アニメがあれば。無ければ空でよい）
4. `CigaretteCutscene` の `Disable During Cutscene` に、Player の **`CameraFollow`** を入れる。
   ここを入れ忘れると、演出中もカメラ追従が働いてカメラが暴れる。
5. エディタで Play すると、`.jslib` の代わりに `Debug.Log("[WebBridge] → Web ...")` が出る。
   **Web が無くても Unity 側だけで配線を確認できる。**

### Editor だけで送信をテストする

Web が無い状態で `SET_PAUSED` などを試したいときは、Console から相当の処理を呼ぶか、
一時的なデバッグ用スクリプトから `WebBridge.Instance.Receive(json)` を直接叩く。

```
{"v":1,"id":"t1","type":"SET_AUDIO_MUTED","payload":{"muted":false},"ts":0}
{"v":1,"id":"t2","type":"END_DIALOGUE","payload":{"dialogueId":"cafe.intro"},"ts":0}
```

---

## 10. 再訪時に何を飛ばすか（決定済み — 案 A を採用）

### 何が問題だったか

決定事項（ARCHITECTURE.md §3）では、再訪時は **言語選択だけ通し、アンケートとキャラ選択は飛ばす**。
Web は `localStorage` を見れば自分で判断できるが、**Unity は毎回 `PlayerChoose` から起動し、
localStorage を知らない。** そのままでは再訪者が文字のない選択画面の前で止まる。

### 決めたこと

**`RESTORE_SESSION` を Web → Unity に 1 つ足す（イベントは 11 → 12 種）。**

```jsonc
{ "characterId": "skaterMaleA" | null, "seen": ["cafe.intro"] }
```

- **`BRIDGE_READY` の直後に必ず 1 回送る。** 初回でも `{ null, [] }` を送る。
  「送るときと送らないときがある」が一番壊れやすい。
- `characterId` があれば Unity は `PlayerChoose` を自動で通過し、そのままカフェへ。
- `seen` に `"cafe.intro"` があれば、**入店演出も、タバコから WELCOME TO MY WORLD までの
  一連も再生しない。** 再訪者は最初から `free`（自由行動）で始まる。
- 渡すのは id と id の配列だけなので **D1（Unity は文字を持たない）は無傷**。

### 記憶の在り処は Web の localStorage。Unity は保存しない

`PlayerPrefs` は WebGL でも動く（IndexedDB に載る）が、**使わない。**
言語・アンケート・キャラを既に localStorage が持っている以上、Unity にも覚えさせると
同じことを覚えている場所が 2 つになり、「はじめから」で両方消さないと食い違う。
**Unity は起動のたびに `RESTORE_SESSION` を受け取って状態を作り直すだけ**にする。

`seen` に `"cafe.intro"` を書き足すのは Web の役目（`SEQUENCE_STATE { title }` を受けたとき）。

### 「はじめから」は Web が localStorage を消してページを読み直す

Unity 側に「カフェから PlayerChoose へ戻す」経路は**作らない**。
作れば、意図的に落とした `GOTO_SCENE`（§5）が名前を変えて戻ってくる。
代償は Unity の再ロード（20〜60 秒）だが、めったに押されない操作なので引き受ける。

### 採らなかった案

| 案 | 内容 | 落とした理由 |
|---|---|---|
| **B** | 再訪でもキャラ選択を毎回通す | ARCHITECTURE.md §3 の決定と矛盾する。イベントを 11 種に保つこと自体は目的ではない |
| **C** | キャラ選択を DOM に移す | Unity 側の `PlayerChoose` が無駄になる。§6.5 の決定と矛盾する |

### Unity 側の実装

| ファイル | 役割 |
|---|---|
| `Assets/Scripts/Web/WebSession.cs` | 受け取った `characterId` / `seen` の置き場。保存はしない |
| `Assets/Scripts/CharacterAutoSelect.cs` | `PlayerChoose` に 1 つ置く。一致する `CharacterChoiceButton` を人が押したのと同じ経路で押す |
| `SceneIntroSequence.skipIfSeenSequenceId` | `seen` に入っていれば入店演出を飛ばす |
| `CafeSequence.Start` | `seen` に入っていれば `free` から始める |
