# Interactive 3D Portfolio — アーキテクチャ設計書 v2

> Status: **v2 — 2026-09-09 全面改訂。**
> 対象: Unity `github.com/Sh1n1230/MyWorld`（6000.6.0f1 / URP 17.6.0） + Web `github.com/Sh1n1230/sh1n1230.dev`
> **このファイルには「決まったこと」しか書かない。** 構想・方向性・まだ決めていないことは `VISION.md` に置く。
> Unity ↔ Web の通信契約は `EVENT_SCHEMA.md`。v1 とその周辺資料は `docs/archive/` にある。

---

## 0. 現状（2026-09-09 実測）

### 0.1 Unity プロジェクト

| 項目 | 値 |
|---|---|
| Unity | **6000.6.0f1** |
| URP | 17.6.0 |
| Input System | 1.20.0 / AI Navigation 2.0.14 / ProBuilder 6.1.2 / Timeline 6.6.0 / uGUI 2.6.0 |
| Cinemachine | **未導入**（入れない。§6.4） |
| リポジトリ | ルート直下が Unity プロジェクト。`.gitignore` / `.gitattributes` / Git LFS はすべて健全 |

### 0.2 シーン

| シーン | 状態 |
|---|---|
| `Assets/Scenes/PlayerChoose.unity` | Build Settings 有効。**起動シーン**。キャラ 2 体（Kenney `skaterMaleA` / `skaterFemaleA`）+ hover 拡大 + クリック選択。**テキストは 0 件** |
| `Assets/Scenes/Cafe/Cafe.unity` | Build Settings 有効。メイン。**テキストは 0 件** |
| `Assets/Scenes/Village.unity` | Phase 4。触らない |
| `Assets/Scenes/Factory.unity` | Phase 4。触らない |

~~`EditorBuildSettings.asset` に存在しない `SampleScene.unity` への死んだ参照が残っている~~ → **削除済み**（2026-09-09）。

### 0.3 Cafe.unity の中身

- NPC **1 体**（Kenney `characterMedium` + `skaterMaleA`、`Chair_Sit_Idle_1_L.controller` で着席アイドル）
- `SignalInteractable` **2 個** — `cafe.npc.smoker.cigarette`（`notifyWebOnSelect: false`）/ `cafe.exhibit.penguin`
- `CafeSequence`（2026-09-17 時点で配置済み）
- `ParticleSystem` 1 個（煙） + `CigaretteSmokeController`
- `SceneIntroSequence`（入店演出）/ `BgmSceneTrigger`
- **専用カメラは無い**（カメラは `Player.prefab` 側）
- **NavMesh は未ベイク**（`NavMeshSurface` が 0 個）

### 0.4 実装済みのスクリプト

- `Assets/Scripts/Interaction/` — `Interactable` / `InteractableRegistry` / `ProximityInteractor` / `PointerInteractor` / `SignalInteractable` / `InteractionSignals` / `DevPreview/`
- `Assets/Scripts/CharacterSelection.cs`（static、シーンをまたいで保持）/ `PlayerMaterialApplier.cs` / `CharacterChoiceButton.cs`
- `Assets/Scripts/Audio/` / `SceneIntroSequence.cs` / `MenuController.cs` / `DevSettings.cs`

**2026-09-09 に `WebBridge` / `WebEvents` / `CafeSequence` / `CigaretteCutscene` / `PortfolioBridge.jslib` を追加した。**
Editor での配線（`EVENT_SCHEMA.md` §9）とペンギンの設置は済んだ。**Unity 側で残っているのは L2 / L3 のヒント表現（§4.2）と店員（§4.1）。**

### 0.5 WebGL ビルド実測（2026-08-20 時点、PlayerChoose 追加前）

| 項目 | 実測 | 目標 | 判定 |
|---|---|---|---|
| `.data.unityweb` | 18 MB | — | |
| `.wasm.unityweb` | 7.9 MB | — | |
| **合計** | **26 MB** | < 30 MB | ✅ |
| PC / localhost 動作 | 問題なし | — | ✅ |
| iOS Safari | **未検証** | — | ⚠ Phase 2 |

**旧 Phase 0（ビルドサイズが実用になるか）は PC については決着した。** 887 MB のインポート済みアセットは、実際にはビルドに載っていない。

---

## 1. このプロジェクトが作るもの

**「Web サイトの中にゲームを埋め込む」のではなく、「3D ワールドそのものがポートフォリオである」Web 体験。**

訪問者は言語を選び、「あなたは誰か」を 2 問だけ聞かれ、キャラクターを選び、カフェに立つ。店内には落ち着かない様子の男が一人座っている。話しかけると彼は自分のことを話し、最後に「火、持ってない?」と言う。火をつけてやると煙が立ちのぼり、カメラがそれを追い、`WELCOME TO MY WORLD` が出る。

---

## 2. 設計の中核となる判断

### D1 — Unity は「世界」だけを描く。文字はすべて Web（DOM）が描く

Unity 内に日本語テキストを持つと、TMP の日本語フォントアトラスがビルドサイズとメモリを圧迫し、i18n 差し替えのたびに Unity ビルドが必要になり、スマホでの可読性調整も難しく、SEO には一切寄与しない。**セリフも説明文も UI 文字列も、すべて Web 側の DOM でレンダリングする。** Unity が持つのは `id` と `labelKey` だけ。

これ 1 つでフォント問題・多言語問題・レスポンシブ問題・アクセシビリティ問題・SEO 問題が同時に解ける。

> **現状: 守られている。** `PlayerChoose.unity` / `Cafe.unity` ともに TextMeshPro / Text コンポーネントは 0 件。
> **この判断を守るために、`SET_LANGUAGE` イベントを意図的に作らない**（`EVENT_SCHEMA.md` §5）。

### D2 — Unity インスタンスは一度ロードしたらアンマウントしない

Unity WebGL のロードは初回 20〜60 秒かかる。作品を 1 つ見るたびに再ロードする体験は成立しない。情報表示は **DOM オーバーレイ + `history.pushState` による URL 同期**にする。Unity の canvas は Next.js の layout に置き、ルート変更では children だけが差し替わる。

同時に、`/ja/projects/xxx` に**直接アクセスした場合は静的 HTML ページを返す**。同じ URL に「ワールド内オーバーレイ」と「静的ページ」の 2 経路を用意することで、SEO・SNS シェア・WebGL 非対応端末のフォールバックが同一 URL 空間で成立する。

### D3 — Unity リポジトリには手を触れない。Web は新規リポジトリ

`MyWorld` の Git 履歴・LFS 設定・`.gitignore` は健全。移行作業ゼロ = 履歴破壊リスクゼロ。

### D4 — アクセシブルな経路は「簡易版ポートフォリオ」が担う

Unity WebGL は `<canvas>` に描画するだけで、**DOM のアクセシビリティツリーを持たない**。スクリーンリーダーからは空のキャンバスに見える。`UnityEngine.Accessibility` API は iOS VoiceOver / Android TalkBack 向けで **WebGL は対象外**。

つまり 3D 世界のスクリーンリーダー対応は、Unity 側に手段が存在しない。**D2 が用意する「同じ URL 空間の静的ページ」が、そのままアクセシブルな経路になる。**

- **Phase 1** — DOM の UI（言語選択・アンケート・対話・ミュートボタン）は Tab / Enter / Esc で操作できる。3D 内はスクリーンリーダー対応を試みない。
- **Phase 2** — 簡易版ポートフォリオがアクセシブルな経路を担う。

---

## 3. 体験フロー

```
初回訪問                              再訪
  │                                    │
  ├─ 言語選択（JA / EN[準備中]）          ├─ 言語選択（毎回出す）
  │   └ 裏で Unity のロード開始          │   └ 裏で Unity のロード開始
  │   └ クリック = 音声再生の user gesture│
  │                                    │
  ├─ アンケート 2 問（ロード中の DOM）     │  （skip: surveyDone）
  │   「ちょっと待って、あなたは誰?」       │
  │                                    │
  ├─ キャラ選択（Unity: PlayerChoose）    │  （skip: character）
  │                                    │
  ├─ 入店演出（Unity）                   │  （skip: seen に cafe.intro）
  │                                    │
  └──────────────┬─────────────────────┘
                 ▼
        自由行動（+ 音声トースト、初回のみ）
                 ▼
   ┌─────────────┴─────────────┐
   ▼                           ▼
NPC のタバコ                 ペンギン（作品）
   │                           │
   ├ カメラ寄せ 0.6s            └ 対話枠（話者行なし）で説明
   ├ 対話 6〜8 行
   ├ 「火、持ってない?」
   ├ 着火 → パーティクル
   ├ カメラが煙を追う
   └ WELCOME TO MY WORLD（DOM）
                 ▼
            自由行動
```

### 3.1 アンケート

**ロード待ちを体験に変えるための装置**であり、同時に将来の Analytics の入口。

- **語り手は「世界の声」**（無人称）。暗転した画面に文字だけ。誰が喋っているか分からない不穏さ。
- **設問は 2 問だけ。** ロード時間（20〜40 秒）に収まる上限。
- **スキップ可。** 画面下に「回答はこのブラウザにのみ保存されます・スキップできます」と明示する。

| 年齢 | 役職 |
|---|---|
| 18 歳未満 / 18〜24 / 25〜34 / 35〜44 / 45〜54 / 55〜64 / 65 歳以上 / **答えない** | 学生 / エンジニア / 採用・人事 / 研究者 / デザイナー・クリエイター / その他 / **答えない** |

**この選択肢は Phase 3 で DB のスキーマになる。** 後から選択肢を足すと過去データと繋がらないので、ここで確定させている。

### 3.2 ロードとアンケートのタイミング

**`surveyDone && unityReady` の AND ゲート**で次に進む。どちらが先に終わっても待つ。アンケートが先に終わった場合はロード進捗を表示する。

### 3.3 言語

- 画面には **JA / EN 両方**を出し、**EN は「準備中」で無効化**する。
- URL は最初から `/ja`。文字列は最初から `{ ja, en }` の型で持つ。
- **再訪でも言語選択は毎回出す**（音声の user gesture がどうしても必要 + 言語を変えたい可能性）。

### 3.4 音声

- **既定は OFF。** DOM の隅に常駐するミュートボタンで切り替える。
- **入店演出の直後に一度だけ**トースト `🔊 音を出すとより楽しめます` を出す。5 秒で自動消滅。押すと即座に鳴り出す。
- 「一度だけ」は localStorage で記録する。トーストは常駐ボタンのすぐ横に出し、**ボタンの存在を教える役割も兼ねる**。

**入口の曲**（カフェに入るまでの音楽）

- サイトを開いてからカフェに入るまでの間、**Web が**流す（言語選択とアンケートの間は Unity がまだ読み込み中で音を出せないため）。
- 言語選択の「音を出す」チェックボックスは**必ず OFF から始める**。チェックした瞬間に鳴り始め、外すと止まる。チェック操作そのものが user gesture になる。
- カフェに入ったらフェードアウトし、以後は Unity の BGM が持つ。
- 原曲は Web リポジトリの `audio/`（配信しない）。配信用の mp3 は `npm run audio:encode` で `public/audio/` に作る。

### 3.5 localStorage に保存するもの

**この 5 つだけ。** カフェ内の細かい進行（どの作品を見たか等）は Phase 1 では保存しない。状態が増えるほど壊れる。

| キー | 用途 |
|---|---|
| `language` | 再訪時の初期選択 |
| `character` | 再訪時にキャラ選択を飛ばす |
| `surveyDone` | 再訪時にアンケートを飛ばす |
| `seen` | 見終わった `sequenceId` の一覧。再訪時に入店演出と `WELCOME TO MY WORLD` を飛ばす |
| `visitor_id` | `crypto.randomUUID()`。Phase 3 で DB に繋ぐときの結合キー |

**記憶はここに一本化する。Unity 側には保存しない。** `PlayerPrefs` は WebGL でも動く（IndexedDB に載る）が、
覚えている場所が 2 つになると「はじめから」で両方消さない限り食い違う。Unity へは起動のたびに
`RESTORE_SESSION` で渡し、Unity はそれに合わせて状態を作り直す（`EVENT_SCHEMA.md` §10）。

**Cookie は使わない。IP も取らない。** 送信もしない（Phase 3 まで）。

---

## 4. インタラクション設計

### 4.1 総数の制限

**カフェの interactable は上限 4 個。** カフェは入口であって展示場ではない。作品を並べる場所は FACTORY である。6 個も置くと FACTORY を作る理由が消える。

| Phase 1 | 2 個 — `cafe.npc.smoker.cigarette` / `cafe.exhibit.penguin` |
|---|---|
| 予備 | 2 枠。埋めたくなった時点で「これは本当にカフェか、FACTORY か」を考える関門として機能させる |

**店員は `Interactable` を付けない。** 近づくと軽く会釈するだけ（Unity 内で完結、Bridge を通さない）。反応が皆無だと不気味に見えるが、対話まで持たせると上限の意味が消える。

### 4.2 認知の手がかり（3 層）

| 層 | 発火条件 | 表現 | 実装場所 |
|---|---|---|---|
| **L1 環境的誘導** | 常時 | 照明・色・動線・アニメーション・サウンドの定位。UI ではない | Unity（レベルデザイン） |
| **L2 近接ヒント** | 距離 < `hintRadius`（4m） | リムライト / 淡い発光が距離に応じてフェードイン。< `focusRadius`（2m）で小さなワールド空間アイコン | Unity |
| **L3 照準ヒント** | ホバー中 | アウトライン + カーソル変化 | Unity |

**ラベル文字列は Web が描く**（D1）。ただし `HOVER_CHANGED` は作らない — PC の hover は Unity 内のアウトラインだけで用が足りるため。プロンプトは `INTERACTABLE_IN_RANGE` で出す。

### 4.3 操作

**PC**

| 操作 | 割当 |
|---|---|
| 移動 | WASD / 矢印キー |
| カメラ回転 | 右ドラッグ（**ポインタロックは使わない**） |
| インタラクト | 左クリック（ホバー中）または `E` |
| 対話送り | クリック / Space / Enter |
| 閉じる | Esc（**対話中は物の説明だけ閉じられる。NPC の対話では効かない**。§7.4） |

> ポインタロックを使わない理由: カーソルが見えていることが「クリックできる対象がある」という認知の前提になる。Esc で解除される・許可プロンプトが出る・iOS 非対応など摩擦が大きい。

**クリック移動（point-and-click）は今は作らない。** 採否は「WASD を知らない訪問者が歩けるか」という一点で決まり、それは Phase 1 の完了条件（他人が説明なしで通せたか）の検証がそのまま答えを出す。必要と分かってから NavMesh をベイクして追加する。それまでは操作系を `PortfolioInput` として差し替え可能な形にしておくだけでよい。

**スマホは Phase 1 では対象外。** 「PC でご覧ください」+ 主要作品への外部リンク + 自己紹介 3 行 + 連絡先 の 1 画面で受ける（= **簡易版ポートフォリオ**の最小版。`/[lang]/text`）。ここに辿り着くのは「Unity が動かなかった recruiter」である可能性が最も高く、リンクも連絡先も無いまま帰すのが最大の失点になる。この 1 画面が Phase 2 の簡易版ポートフォリオの出発点になる。

- **振り分け条件**: `WebGL2 が使えない` **または** `pointer: coarse かつ 画面幅 < 1024px`。UA 判定は使わない（壊れやすい）。該当したら Unity をロードせずに簡易版ポートフォリオへ送る。
- **「それでも 3D を試す」導線は置かない。** 判定から外れた人は PC で来ればよい。操作できない 26 MB を落とさせない。
- **連絡先**: メール / X / LinkedIn / GitHub の 4 つ。

---

## 5. Unity ↔ Web の責務分割

| Unity が持つ | Web が持つ |
|---|---|
| 3D 世界 / プレイヤー / カメラ / NPC | **すべての文章** |
| アニメーション / ライティング / SFX / BGM | 翻訳 / UI / URL / ルーティング |
| 当たり判定 / インタラクション判定 | 作品情報 / レスポンシブ / アクセシビリティ / SEO |
| 演出（着火・煙・カメラワーク） | Unity のロード管理 / 状態の永続化 |
| **オブジェクト ID と labelKey** | ID → 意味 の対応表 |

通信契約は `EVENT_SCHEMA.md`（全 11 イベント）。

---

## 6. Unity 側の設計

### 6.1 スクリプト構成

```
Assets/Scripts/
├── Web/
│   ├── WebBridge.cs              ✅ Unity ↔ Web の唯一の窓口
│   └── WebEvents.cs              ✅ 型・イベント名・payload。schema.ts と 1:1
├── Interaction/                  ✅
├── Cafe/
│   ├── CafeSequence.cs           ✅ 状態機械（entering → … → free）
│   ├── CigaretteCutscene.cs      ✅ 煙を追うカメラ
│   └── CigaretteSmokeController.cs  ✅ Assets/Animations/ から移設済み
├── Audio/                        ✅
├── CharacterSelection.cs         ✅ Selected イベントを追加済み
├── PlayerMaterialApplier.cs      ✅
├── SceneIntroSequence.cs         ✅
├── MenuController.cs             ✅
└── DevSettings.cs                ✅

Assets/Plugins/WebGL/
└── PortfolioBridge.jslib         ✅ Unity → Web
```

Editor での配線（`EVENT_SCHEMA.md` §9）は済んだ。

### 6.2 WebBridge

- `DontDestroyOnLoad` の GameObject `WebBridge` に付く。**`SendMessage` の受け口はこの 1 つだけ。**
- `InteractionSignals` の `InRangeChanged` / `ObjectSelected` を購読して Envelope に詰め替える（`HoverChanged` は購読しない）。
- 未知の `type` は `Debug.LogWarning` して無視する。**例外を投げない。**
- `#if !UNITY_WEBGL || UNITY_EDITOR` では `.jslib` を呼ばず、`Debug.Log` に出す（エディタで動作確認できるようにする）。

### 6.3 カフェの状態機械

```
entering ─▶ explore ─▶ focusing ─▶ (Web: 対話 6〜8 行) ─▶ lighting ─▶ smoke ─▶ title ─▶ free
              ▲            │
              └────────────┘  （対象から離れる）
```

各遷移で `SEQUENCE_STATE` を送る。**Unity は「Web が今どんな UI を出しているか」を知らない。**

### 6.4 カメラ演出は手書きにする

煙を追うカットは 1 本だけ。`CigaretteCutscene.cs` で位置と注視点を補間する（既存の `SceneIntroSequence.cs` が「実行時に自前で組む」流儀なので一貫する）。

- **Cinemachine を入れない** — カットが 1 本しかない今、依存を 1 つ増やすだけ。VILLAGE / FACTORY でカメラ演出が増えたら再検討する。
- **Timeline を使わない** — `.playable` は YAML なので Claude Code から直接編集できず、UnityMCP 経由の往復が増える。数値をコードに置いた方が速い。

### 6.5 キャラ選択

`PlayerChoose.unity` の実装をそのまま使う。

- キャラは **2 体**のまま。増やしても体験は良くならない。
- **見た目だけ。** ゲーム的な意味は一切持たせない。
- 選んだ `characterId` は `CHARACTER_SELECTED` で Web に送る（Phase 3 でアンケートの回答と並べると意味のあるデータになる。送信コストはイベント 1 個ぶん）。
- 画面に出る文字（「キャラクターを選ぶ」等）は **DOM オーバーレイ**で被せる。Unity 側には入れない。

---

## 7. Web 側の設計

### 7.1 技術選定

| | 採用 | 理由 |
|---|---|---|
| フレームワーク | **Next.js App Router + TypeScript** | Unity canvas を layout に常駐でき、ルート変更で children だけ差し替わる（D2）。静的 HTML も生成できる（SEO） |
| スタイル | Tailwind CSS | |
| ホスティング | **Vercel** | Next.js の本家。`vercel.json` で `Content-Encoding` を制御できるので Brotli 配信に支障がない。GitHub Pages はヘッダを設定できず、Unity の decompression fallback（遅い JS 展開）に頼ることになる |
| リポジトリ | **`Sh1n1230/sh1n1230.dev`** | `Sh1n1230/Portfolio`（private、既存）と衝突せず、`Sh1n1230/Sh1n1230`（プロフィール README 専用枠）とも衝突しない |
| ドメイン | `sh1n1230.dev` | **取得は Phase 2 の公開直前。** それまで Vercel のドメイン |

### 7.2 リポジトリ構成

```
sh1n1230.dev/
├── app/[lang]/            layout.tsx（Unity canvas 常駐）/ page.tsx / projects/[slug]/
├── components/
│   ├── unity/             ローダー / canvas
│   ├── overlay/           LanguageSelect / Survey / CharacterSelectOverlay
│   │                      DialogueBox / TitleCard / InteractionPrompt
│   │                      MuteButton / AudioToast / NoWebGL
│   └── ui/
├── lib/
│   ├── bridge/            envelope / queue / zod スキーマ
│   ├── i18n/
│   └── storage/           localStorage の 4 キー
├── content/
│   ├── projects/*.ts      作品データ
│   ├── dialogue/*.ts      セリフ
│   └── ui/*.ts            UI 文字列
├── public/unity/          Unity のビルド成果物（git にコミットする）
└── scripts/
```

### 7.3 コンテンツ管理 — Git が CMS

CMS は使わない。一人運用・型安全・Git に変更履歴が残る・外部サービス不要・**Unity 再ビルド不要**。

```ts
type Localized<T> = { ja: T; en: T | null };   // ← en: null を許すのが肝

type DialogueLine = {
  id: string;                  // "cafe.intro.01"
  speaker: string | null;      // null なら話者行を出さない（= 物の説明）
  text: Localized<string>;
  emotion?: string;            // Unity のアニメ切替に使う
  pauseMs?: number;
};
```

`pauseMs` は**行の後の間**。送り操作のあと、その時間だけ本文を消して黙ってから次の行を出す（間の最中の送り操作は無視）。0 または未指定なら間を置かない。

訪問者に見せる呼び名も「**簡易版ポートフォリオ**」（`/[lang]/text`）。開発側の呼び名と揃える。

### 7.4 対話 UI は 1 コンポーネント

NPC の台詞も、ペンギンの説明も、**同じコンポーネント**が描く。違いは `speaker` の有無だけ。

| | 話者行 | 表示 |
|---|---|---|
| NPC | あり（`男`） | 「〜」の形で台詞 |
| ペンギン | なし | 説明文のみ + 小さくオブジェクト名 |

話者行なしの台詞ボックスは「そのオブジェクトが喋っている」と読まれるので、この 1 行の有無で人と物を区別する。

- 送りは **クリック / Space / Enter**。自動では流れない。
- **スキップは付けない。** 6〜8 行がこの体験の唯一の核である。
- **Esc は話者行の有無で変わる。** NPC（話者あり）では無効、ペンギンなど物の説明（話者なし）では閉じる。§4.3 の「閉じる = Esc」は物の説明にだけ適用する。
- **Phase 1 では外部リンクを 1 本も置かない。** 入って 60 秒で外部サイトに飛ばす導線があると「入る体験」という設計の中心が崩れる。

### 7.5 英語化の運用

**同時に書かない。** 巻き戻りが 2 倍になる。**区切り（Phase の完了）ごとにまとめて翻訳する。**

そのために `Localized<T>` の `en` に **`null` を許す**。`npm run i18n:missing` で未翻訳の一覧が出るようにし、Phase の区切りでそれを空にする。

> `en` に日本語をそのまま入れる運用は**採らない**。正常と異常の区別がつかなくなり、翻訳漏れが検出不能になる。

EN を選べるようにするのは、未翻訳がゼロになった **Phase 2 の最後**。

---

## 8. ビルドとデプロイ

### 8.1 Unity ビルドの受け渡し

```
MyWorld/Build/WebGL/  ──(scripts/deploy-unity.sh)──▶  sh1n1230.dev/public/unity/
                                                            │
                                                       git commit → Vercel が自動デプロイ
```

**成果物を Web リポジトリの git にコミットする。** 26 MB がビルドのたびに積まれるが、公開までのビルド回数はせいぜい 10〜20 回で実害が出ない。**膨らんだら Cloudflare R2 等の外部ストレージに移す**（そのとき Web 側は URL 参照に変える）。

CI での Unity ビルドは**行わない**（ライセンス設定の手間に見合わない）。

### 8.2 WebGL 設定

- 圧縮は Brotli。`vercel.json` で `Content-Encoding: br` / `Content-Type` を設定する。
- ヘッダが効いていることを確認できたら **`webGLDecompressionFallback` を切る**（JS 展開のコードぶんサイズが減る）。切る前に必ず実機で確認すること。

### 8.3 Phase 1 は非公開

**Vercel の Preview URL + `noindex`** で確認する。**Deployment Protection は切る**（URL を知っている人だけが見られる状態）。テスターに Vercel のログインを要求すると完了条件が成立しないため。localhost だけで済ませると、配信ヘッダ・Brotli・キャッシュ・実機の遅さという**本番でしか出ない問題が Phase 2 まで見つからない**。

---

## 9. Roadmap

### Phase 1 — 配線を通す（**非公開**）

1. `ARCHITECTURE.md` v2 / `VISION.md` / `EVENT_SCHEMA.md`
2. ~~Unity: `WebBridge.cs` + `PortfolioBridge.jslib` + `InteractionSignals` への接続~~ ✅
3. ~~Unity: `CafeSequence.cs` / `CigaretteCutscene.cs` → Editor で配線 / ペンギン配置~~ ✅
   + 店員追加（会釈のみ）/ L2・L3 のヒント表現（§4.2）
4. ~~Web: `Sh1n1230/sh1n1230.dev` 新規 + Next.js + Unity canvas 常駐~~ ✅
5. ~~Web: 言語選択 → アンケート → キャラ選択オーバーレイ → 対話ボックス → タイトル演出~~ ✅ + ~~再訪（`RESTORE_SESSION`）~~ ✅ + 対話中の Esc（§7.4）
6. Web: ミュートボタン / 音声トースト / 非対応時の簡易版ポートフォリオ（振り分け・連絡先。§4.3）
7. Vercel Preview（`noindex`）にデプロイ

**完了条件: 他人に Preview URL を渡して、説明なしで最後まで行けた。**
自分で通せたことは条件にしない（作った本人は迷わないので必ず通る）。

テスターは作者の友人（電気通信大学、PC に慣れた人が多い）に頼む。非同期で頼む場合は**画面録画をもらう**（どこで何秒止まったかが分からないと直せない）。
全員が WASD に慣れている場合、クリック移動の採否（§4.3）はこの検証では決まらないので、Phase 1 では保留とする。

**Phase 1 でやらないこと**（明示）
ProjectPanel / WorldMap / HUD / VILLAGE / FACTORY / スマホ対応 / クリック移動 / DB / Analytics / 英語版 / 外部リンク / Cinemachine / 独自ドメイン。

### Phase 2 — 公開版

簡易版ポートフォリオ（スマホ / SEO / OGP / アクセシブル経路）+ ProjectPanel + **ペンギンを ProjectPanel に昇格** + **この時点の全文章の英語版** + iOS Safari 実機検証 + ドメイン取得 → **ここで初めて公開する**。

### Phase 3 — アンケートの DB 化 + Analytics

PostgreSQL。バックエンドを初めて作る段階。**「バックエンドがあると格好いいから作る」はやらない** — アンケートを保存する必要が生じたから作る。

### Phase 4 — VILLAGE / FACTORY

### Phase 5 — LAB / THOUGHTS

> **Phase 3 以降の順序は保証しない。** 3 年先の順番を今決めても意味がない。

---

## 10. Risks

| リスク | 度合 | 対策 |
|---|---|---|
| **スコープの膨張** | 🔴 | Phase 1 の「やらないこと」を明示済み（§9）。カフェの interactable 上限 4 個（§4.1）。**公開されないことが最大の失敗である** |
| iOS Safari のメモリ | 🟡 未検証 | Phase 1 では対象外。Phase 2 で実機検証。ダメなら簡易版ポートフォリオに寄せる |
| 「何をクリックすればいいか分からない」 | 🟡 | 3 層のヒント（§4.2）+ 完了条件を「他人が説明なしで通せた」にすることで検証する |
| ID の不一致 | 🟡 | 例外が出ずに無反応になるだけなので気づきにくい。ID 一覧を `EVENT_SCHEMA.md` §6 に集約し、Web 側に未知 ID の warn を入れる |
| ビルド成果物による git の肥大 | 🟢 | 10〜20 回までは許容。膨らんだら外部ストレージへ（§8.1） |
| 英語版の腐敗 | 🟢 | `en: null` を型で許して未翻訳を可視化（§7.5） |
| ビルドサイズ | 🟢 解決 | 26 MB で目標クリア済み（§0.5） |

---

## 11. 明示的に「やらない」と決めたこと

| やらないこと | 理由 |
|---|---|
| Unity に日本語テキスト / TMP 日本語アトラス | D1。フォント・i18n・レスポンシブ・SEO の問題が一気に発生する |
| `SET_LANGUAGE` イベント | D1 を守るための欠落。あると「Unity にも少しくらい文字を」の穴になる |
| iframe / WebSocket / HTTP API での Unity ↔ Web 通信 | `SendMessage` と `.jslib` で足りる |
| Unity 内 UI での情報表示 | D1 / D2 |
| ページ遷移による作品表示 | D2。Unity が再ロードされる |
| CMS | 一人運用。Git で足りる |
| Cinemachine / Timeline（Phase 1） | カットが 1 本しかない |
| ポインタロック | 摩擦が大きい。確実性を優先する |
| 3D 世界のスクリーンリーダー対応 | D4。Unity WebGL に手段が存在しない。簡易版ポートフォリオが担う |
| Cookie / IP の取得 | プライバシーを主題に置く以上、必要になるまで持たない |
| GitHub Actions での Unity ビルド | ライセンス設定の手間に見合わない |

---

## 12. 用語

| 語 | 意味 |
|---|---|
| **Envelope** | Unity ↔ Web の全メッセージを包む共通の型。`{ v, id, type, payload, ts }` |
| **labelKey** | 表示ラベルの「キー」。文字列そのものではない。Unity が持ち、Web が解決する |
| **Interactable** | ワールド内で選択できるものの基底クラス。`id` / `labelKey` / `hintRadius` / `focusRadius` を持つ |
| **簡易版** | Unity を使わない静的 HTML のポートフォリオ。スマホ・SEO・アクセシビリティの受け皿を兼ねる |
