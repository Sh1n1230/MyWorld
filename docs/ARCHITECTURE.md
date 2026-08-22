# Interactive 3D Portfolio — アーキテクチャ設計書

> Status: **v1.1 — D1/D2/D5/D6/D7/D10/D11 合意済み。D3/D4 は Phase 0 の計測結果で確定。D8 合意済み。D9 は簡素化。**
> 作成日: 2026-08-16 / 更新: 2026-08-16
> 対象リポジトリ: `github.com/Sh1n1230/MyWorld`（Unity 6000.5.7f1 / URP 17.5.0）
> このドキュメントは Claude Code / Cowork が参照する**正本**です。人間向けの要約は `PORTFOLIO_DESIGN.html` を参照。

---

## 0. 現状調査サマリ（事実）

実装提案の前に、既存リポジトリを調査した結果を事実として記録する。以降の設計判断はすべてこの調査結果に基づく。

### 0.1 リポジトリ

| 項目 | 実測値 |
|---|---|
| リモート | `https://github.com/Sh1n1230/MyWorld.git` |
| ブランチ | `main` のみ（origin/main と一致） |
| コミット数 | 43 |
| 追跡ファイル数 | 18,622 |
| Git オブジェクト（pack） | **約 19 MB**（履歴そのものは非常に軽い） |
| `.git/lfs` ローカルキャッシュ | 約 864 MB / LFS オブジェクト 4,010 個 |
| ワーキングツリー総容量 | 6.2 GB（うち `Library/` 等の無視対象が大半） |
| Unity プロジェクトの位置 | **リポジトリのルート直下**（`Assets/`, `Packages/`, `ProjectSettings/` がルート） |

### 0.2 Git 設定（すでに適切）

- `.gitignore` は GitHub 公式 Unity テンプレート（2025-12-18 同期）。`Library/` `Temp/` `Obj/` `Build/` `Builds/` `Logs/` `UserSettings/` `*.csproj` `*.slnx` はすべて除外済み。**追加対応は不要。**
- `.gitattributes` は gitattributes/gitattributes の公式 Unity テンプレート（2026-02-04 同期）。`unity-yaml` / `unity-json` マクロ、および FBX・PNG・EXR・WAV・PDF 等の **LFS 指定が完備**。
- **Git LFS は正常に稼働している。** `.git/config` に `[lfs]` セクションと GitHub LFS エンドポイントが登録済み。HEAD の PNG blob を確認したところ、中身は `version https://git-lfs.github.com/spec/v1` のポインタファイルであり、期待どおり LFS 管理されている。

> **注意（誤検知）**: 本セッションの `device_bash` は macOS 上ではなく、ファイルを mount した Linux VM で動作しており、その VM には `git-lfs` が入っていない。そのため `git status` が 4,028 ファイルを「変更あり」と誤表示する。**これは Mac 側の実態ではなく、リポジトリの異常ではない。** Mac のターミナルで `git status` を実行して確認されたい。

### 0.3 Assets の内訳

| ディレクトリ | サイズ | 備考 |
|---|---|---|
| `Assets/ImportedAssets/Assetstore` | 692 MB | BOXOPHOBIC Skybox Cubemap Extended ほか |
| `Assets/ImportedAssets/SyntyStudios` | 172 MB | |
| `Assets/ImportedAssets/Kenny` | 21 MB | |
| `Assets/ImportedAssets/Quaternius` | 3.1 MB | |
| `Assets/Materials` | 94 MB | Poly Haven 由来と推測 |
| `Assets/Screenshots` | 6.2 MB | |
| `Assets/CharacterController` | 2.7 MB | サードパーソンコントローラ一式 |
| `Assets/Animations` | 2.6 MB | `X Bot@Smoking.fbx`, `Smoking_cut.anim`, `Player.controller` |
| `Assets/Scenes` | 764 KB | |
| `Assets/Scripts` | 8 KB | `MenuController.cs` のみ |

**合計 約 887 MB のインポート済みアセット。これが WebGL ビルドサイズの最大リスク要因**（詳細は §13）。

### 0.4 既存シーン

| シーン | サイズ | 状態 |
|---|---|---|
| `Assets/Scenes/CafeScene.unity` | 362 KB | **最も作り込みが進んでいる**（最終更新 8/16） |
| `Assets/Scenes/VillageScene.unity` | 283 KB | 作りかけ |
| `Assets/Scenes/FactoryScene.unity` | 45 KB | ほぼ空 |
| `Assets/Scenes/SampleScene.unity` | 15 KB | 未使用 |
| `Assets/Scenes/temp.unity` | 45 KB | 未使用 |

CAFE / VILLAGE / FACTORY の**シーンファイルはすでに存在する**。設計上、これを前提にできる。

### 0.5 既存スクリプト

- `Assets/Scripts/MenuController.cs`（1 KB）
- `Assets/Animations/Smoking.cs`, `CigaretteSmokeController.cs` — **タバコ演出の実装がすでに着手済み**
- `Assets/CharacterController/.../Scripts/` — `CharacterControllerBase.cs`（Rigidbody + CapsuleCollider、13点レイキャストによる接地判定、スロープ・段差・コヨーテタイム・二段ジャンプ）、`CameraFollow.cs`, `CharacterAudio.cs`, `CharacterInput3rdPerson.cs` など

### 0.6 パッケージ構成（`Packages/manifest.json`）

`universal RP 17.5.0` / `inputsystem 1.20.0` / `probuilder 6.1.2` / `ai.navigation 2.0.14` / `timeline 1.8.12` / `visualscripting 1.9.12` / `ugui 2.5.0` / `test-framework 1.7.0` / `ai.assistant 2.9.0-pre.2` / `ai.inference 2.6.1` / `multiplayer.center` / `collab-proxy` / `com.coplaydev.unity-mcp`（GitHub 直参照）

**Cinemachine は未導入**（カメラ演出を行うなら追加検討）。

### 0.7 その他

- `.agents/skills/` に Unity 公式スキルが 21 個（`optimize-web`, `unity-cli`, `localization`, `ui-uitk` など）。**WebGL 最適化とビルド自動化に直接使える。**
- `CLAUDE.md` の記述が古い（Unity `6000.4.6f1` と記載されているが実際は `6000.5.7f1`、存在しない `MyWorldScene.unity` を参照）。Phase 0 で更新すべき。
- `README.md` は猫の SVG 1 行のみ。

---

## 1. Executive Summary

### 1.1 このプロジェクトが作るもの

**「Web サイトの中にゲームを埋め込む」のではなく、「3D ワールドそのものがポートフォリオである」Web 体験。**

訪問者は最初に言語（JA / EN）を選び、そのままカフェの中に立つ。店内には落ち着かない様子の人物が一人いる。近づいてタバコに火をつけてやると、彼は語り始める — それがこのポートフォリオ世界への導入になる。以降、村（VILLAGE = 自分自身・趣味・価値観）と工場（FACTORY = 作品・成果）を歩き回り、建物やモニュメントを選択して情報を発見していく。

### 1.2 設計の中核となる 3 つの判断

この設計書全体は、次の 3 つの判断から演繹されている。

**判断 1 — Unity は「世界」だけを描く。文字はすべて Web（DOM）が描く。**

Unity 内に日本語テキストを持つと、TMP の日本語フォントアトラス（数千グリフ）がビルドサイズとメモリを圧迫し、i18n 差し替えのたびに Unity ビルドが必要になり、スマホでの可読性調整も難しく、SEO には一切寄与しない。**セリフもプロジェクト説明も UI 文字列も、すべて Web 側の DOM でレンダリングする。** Unity が担うのは 3D 世界・キャラクター・カメラ・アニメーション・ライティング・サウンド、そして「今これがインタラクト可能だ」というワールド内の非文字表現（発光・アウトライン・アイコン）に限定する。

これ 1 つでフォント問題・多言語問題・レスポンシブ問題・アクセシビリティ問題・SEO 問題が同時に解ける。

**判断 2 — Unity インスタンスは一度ロードしたら絶対にアンマウントしない。**

Unity WebGL のロードは初回 20〜60 秒かかる。作品を 1 つ見るたびにページ遷移して再ロードする体験は成立しない。したがって「作品詳細を見る」は**オーバーレイ表示 + URL だけを History API で書き換える**方式にする。Unity の canvas は Next.js の layout に置き、ルート変更では children だけが差し替わる。

同時に、`/ja/projects/horror-game` に**直接アクセスした場合は静的 HTML ページを返す**。同じ URL に「ワールド内オーバーレイ」と「静的ページ」の 2 つのレンダリング経路を用意することで、SEO・SNS シェア・WebGL 非対応端末のフォールバックが同一 URL 空間で成立する。

**判断 3 — Unity リポジトリには一切手を触れず、Web は新規リポジトリにする。**

現行 `MyWorld` は Git 履歴も LFS 設定も `.gitignore` も既に健全。ここに手を入れる理由がない。Web は別リポジトリで新規に始める（詳細と根拠は §4）。移行作業ゼロ = 履歴破壊リスクゼロ。

### 1.3 完成させるための規模制限

「技術的に可能」を理由に複雑化しないため、**Phase 1 のスコープを次に固定する**。

- シーンは **CAFE 1 つだけ**。VILLAGE / FACTORY は空のまま触らない。
- インタラクト可能オブジェクトは **CAFE 内に 1 つだけ**（NPC のタバコ）。
- Web 側の情報パネルは **1 種類だけ**（対話ボックス）。プロジェクト詳細パネルは Phase 2。
- デプロイは **GitHub Pages への手動デプロイ 1 コマンド**。CI での Unity ビルドは行わない。

「CAFE に入り、NPC のタバコに火をつけ、彼が喋り、それが公開 URL で動く」— これが Phase 1 の完了定義である。

---

## 2. UX Architecture

### 2.1 全体の体験フロー

```
┌─────────────────────────────────────────────────────────────┐
│  1. 訪問  https://sh1n1230.github.io/                        │
│     └ 言語選択画面（JA / EN）                                 │
│        ・この画面の裏で Unity WebGL のロードが走る             │
│        ・言語ボタンのクリック = 音声再生に必要な user gesture  │
│        ・ロード進捗を控えめに表示（0-100%）                    │
└─────────────────────────────────────────────────────────────┘
                              ↓  /ja へ
┌─────────────────────────────────────────────────────────────┐
│  2. 入店演出（3-5 秒）                                        │
│     ・ドアが開く音 → 明転 → カフェの店内                       │
│     ・操作方法のヒントを 5 秒だけ表示（DOM オーバーレイ）       │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  3. 探索                                                     │
│     ・プレイヤーが店内を自由に歩く                             │
│     ・NPC は常時ソワソワしたループアニメーション                │
│     ・NPC への視線誘導：照明・BGM の定位・店の動線              │
└─────────────────────────────────────────────────────────────┘
                              ↓  近づく
┌─────────────────────────────────────────────────────────────┐
│  4. インタラクション可能を認知                                 │
│     ・距離 < 4m：タバコにごく淡い発光が立ち上がる               │
│     ・距離 < 2m：小さなアイコン（🔥）がワールド空間に浮かぶ      │
│     ・PC = ホバーでアウトライン + カーソル変化                  │
│       Mobile = 画面右下に大きな丸ボタンが出現                   │
└─────────────────────────────────────────────────────────────┘
                              ↓  クリック / タップ
┌─────────────────────────────────────────────────────────────┐
│  5. 火をつける                                                │
│     ・カメラが 0.6 秒でタバコにフレーミング（イージング）        │
│     ・着火 SE + パーティクル + 煙                              │
│     ・Unity → Web に CIGARETTE_LIT を送信                     │
└─────────────────────────────────────────────────────────────┘
                              ↓
┌─────────────────────────────────────────────────────────────┐
│  6. 対話（ポートフォリオ世界への導入）                          │
│     ・カメラが会話用の構図へ（NPC の肩越し）                    │
│     ・DOM の対話ボックスが下からせり上がる                      │
│     ・クリック / タップ / Space で次の行へ                     │
│     ・最終行の後、「世界へ出る」導線が提示される（Phase 2 で有効）│
└─────────────────────────────────────────────────────────────┘
```

### 2.2 「何をクリックすればいいか分からない」を防ぐ設計

**原則: インタラクト可能オブジェクトの総数を、1 シーンあたり 5〜9 個に厳密に制限する。**

これが最も効く対策であり、UI 的な工夫より優先する。CAFE は Phase 1 で **1 個**（タバコ）。

その上で、認知の手がかりを**距離に応じた 3 層**で与える。「常時全部が光っている」状態を避けつつ、「何もヒントがない」状態も避けるための構造。

| 層 | 発火条件 | 表現 | 実装場所 |
|---|---|---|---|
| **L1 環境的誘導** | 常時 | 照明・色・動線・アニメーション・サウンドの定位。UI ではない。 | Unity（レベルデザイン） |
| **L2 近接ヒント** | プレイヤー距離 < 4m | オブジェクトのリムライト / 淡い発光が距離に応じてフェードイン。距離 < 2m で小さなワールド空間アイコン。 | Unity |
| **L3 照準ヒント** | ホバー（PC）/ 近接最優先対象（Mobile） | アウトライン（URP Renderer Feature）+ カーソル変化 + 短いラベル | Unity（アウトライン）/ Web（カーソル・ラベル） |

**ラベルは文字なのに Unity ではなく Web で描く**（判断 1）。Unity は `HOVER_CHANGED { id, screenPos }` を送るだけ。Web が対応するラベル文字列を i18n データから引き、canvas 上の該当座標に DOM チップとして描画する。

### 2.3 PC とスマートフォンの入力設計

**PC**

| 操作 | 割当 |
|---|---|
| 移動 | WASD / 矢印キー |
| カメラ回転 | **右ドラッグ**（ポインタロックは使わない） |
| インタラクト | 左クリック（対象にホバー中）または `E` |
| 対話送り | クリック / Space / Enter |
| 閉じる | Esc |

> **ポインタロックを使わない理由**: カーソルが見えていることが「クリックできる対象がある」という認知の前提になる。またブラウザのポインタロックは Esc で解除される・許可プロンプトが出る・iOS では非対応など摩擦が大きい。右ドラッグ回転は FPS 的な没入感は落ちるが、ポートフォリオという文脈では**確実性を優先する**。

**Mobile**

| 操作 | 割当 |
|---|---|
| 移動 | 画面左半分の仮想スティック |
| カメラ回転 | 画面右半分のスワイプ |
| インタラクト | 対象をタップ、**または**近接時に出る右下の丸ボタン |
| 対話送り | 画面のどこでもタップ |

> **仮想スティックは Unity 内（Input System の On-Screen Stick）に置く。** DOM で作って毎フレーム `SendMessage` で軸を送る案もあるが、60fps で JS→wasm 呼び出しを回すのは無駄で、入力遅延とデバッグ困難を招く。**「文字は DOM、入力は Unity」**と割り切る。

> **近接時の丸ボタンが必須な理由**: モバイルには hover が存在しない。小さな 3D オブジェクトを正確にタップさせるのは失敗率が高いので、「近づけば必ず押せる大きなボタンが出る」という保険を必ず用意する。これは L3 の Mobile 版に相当する。

### 2.4 情報の表示方式 — 比較と結論

| 方式 | 没入感 | 実装コスト | SEO | シェア可能性 | 判定 |
|---|---|---|---|---|---|
| Unity 内 UI（uGUI / UI Toolkit） | 高 | **高**（日本語フォント・レスポンシブ・リンクすべて自前） | ✗ | ✗ | ✗ |
| 別 Web ページへ遷移 | 低（Unity 再ロード） | 低 | ◎ | ◎ | ✗ |
| **オーバーレイ / モーダル（DOM）+ URL 同期** | **中〜高** | **低** | **◎（静的ページ併設）** | **◎** | **✅ 採用** |
| サイドパネル（DOM） | 中 | 低 | ◎ | ◎ | ○（デスクトップの詳細表示で併用） |

**結論: DOM オーバーレイ + `history.pushState` による URL 同期。**

- ワールド内で対象を選択 → URL が `/ja/projects/horror-game` に変わり、オーバーレイが開く。Unity は背後で動き続ける（ただし入力は無効化、演出用にカメラだけ寄る）。
- ブラウザの戻るボタンでオーバーレイが閉じる（`popstate` を購読）。
- その URL を**直接開いた場合は、静的 HTML ページ**（Next.js の SSG）が表示される。ここには Unity は載せず、テキスト・画像・リンクのみ。「3D ワールドで見る」ボタンから `/ja` に入れる。
- レイアウト: デスクトップ = 右サイドパネル（画面の 40%、3D が左に見え続ける）／モバイル = ボトムシート（画面の 70%、上スワイプで全画面）。

### 2.5 ナビゲーション / 迷子対策（Phase 2 以降）

- **CAFE は狭い密室なので迷子にならない。** これは意図的な設計であり、Phase 1 でナビゲーション UI を作らない理由でもある。
- VILLAGE / FACTORY を作る段階で、Web 側 HUD に以下を追加する:
  - 左上: 現在エリア名 + 「発見済み 3 / 8」のカウンタ（探索の動機付け）
  - 右上: エリア切替（CAFE / VILLAGE / FACTORY）とワールドマップ
  - ワールドマップは **DOM の 2D 平面図**で十分。Unity でミニマップカメラを回すのはコストに見合わない。
- 「全部見た」状態を明示する完了表現を用意する。ポートフォリオは網羅性が価値なので、取りこぼしの認知は重要。

---

## 3. Overall Architecture

### 3.1 システム全体図

```
┌────────────────────────────────────────────────────────────────────────┐
│                              BROWSER                                    │
│                                                                         │
│  ┌───────────────────────────────────────────────────────────────────┐ │
│  │  Next.js App (Static Export)          https://sh1n1230.github.io/  │ │
│  │                                                                    │ │
│  │  ┌──────────────────────────────────────────────────────────────┐ │ │
│  │  │  app/[lang]/layout.tsx   ← Unity canvas はここに常駐          │ │ │
│  │  │                                                              │ │ │
│  │  │  ┌────────────────────────┐   ┌──────────────────────────┐  │ │ │
│  │  │  │  <canvas id="unity">   │   │  DOM Overlay Layer       │  │ │ │
│  │  │  │                        │   │  ・LoadingScreen          │  │ │ │
│  │  │  │   Unity WebGL          │   │  ・LanguageSelect         │  │ │ │
│  │  │  │   Instance             │   │  ・DialogueBox            │  │ │ │
│  │  │  │   (never unmounted)    │   │  ・ProjectPanel           │  │ │ │
│  │  │  │                        │   │  ・HUD / HoverLabel       │  │ │ │
│  │  │  └───────────┬────────────┘   └────────────┬─────────────┘  │ │ │
│  │  │              │                              │                │ │ │
│  │  │              │      ┌───────────────────┐   │                │ │ │
│  │  │              └─────▶│   Bridge Client   │◀──┘                │ │ │
│  │  │                     │  (TypeScript)     │                    │ │ │
│  │  │                     │  ・envelope schema │                    │ │ │
│  │  │                     │  ・send queue      │                    │ │ │
│  │  │                     │  ・zod validation  │                    │ │ │
│  │  │                     └───────────────────┘                    │ │ │
│  │  └──────────────────────────────────────────────────────────────┘ │ │
│  │                                                                    │ │
│  │  Static Routes (SSG, Unity なし)                                   │ │
│  │  /ja  /en  /ja/projects/[slug]  /ja/about  /ja/contact  ...        │ │
│  │  → SEO・SNS シェア・WebGL 非対応端末のフォールバック                │ │
│  │                                                                    │ │
│  │  Content (build 時に静的埋め込み)                                   │ │
│  │  content/projects/*.ts   content/dialogue/*.ts   content/ui/*.ts   │ │
│  └───────────────────────────────────────────────────────────────────┘ │
└────────────────────────────────────────────────────────────────────────┘
                                    ▲
                                    │ static files (HTML/JS/CSS + Unity build)
                                    │
┌───────────────────────────────────┴────────────────────────────────────┐
│  GitHub Pages  (repo: Sh1n1230.github.io / branch: gh-pages)           │
│  ・カスタム HTTP ヘッダ設定不可 → Decompression Fallback を有効化       │
│  ・公開サイト上限 1 GB / 帯域 100 GB/月（ソフト）                       │
└────────────────────────────────────────────────────────────────────────┘
                                    ▲
                                    │ 手動デプロイ（1 コマンド、orphan force push）
                                    │
┌───────────────────────────────────┴────────────────────────────────────┐
│  ローカル Mac                                                           │
│                                                                         │
│  ┌────────────────────────────┐        ┌─────────────────────────────┐ │
│  │ repo: MyWorld (既存・不変) │        │ repo: Sh1n1230.github.io    │ │
│  │  Unity 6000.5.7f1 / URP    │        │  (新規)                     │ │
│  │  Assets/ Packages/ ...     │        │  app/ components/ content/  │ │
│  │  Git LFS 有効               │        │  public/unity/ ← gitignore  │ │
│  └──────────────┬─────────────┘        └──────────▲──────────────────┘ │
│                 │                                  │                    │
│                 │  Unity Editor で WebGL ビルド     │                    │
│                 └──────────────────────────────────┘                    │
│                    (成果物を public/unity/ にコピー)                     │
└────────────────────────────────────────────────────────────────────────┘
```

### 3.2 責務の分割

| | Unity | Web |
|---|---|---|
| **持つもの** | 3D 世界、プレイヤー、カメラ、NPC、アニメーション、ライティング、SFX/BGM、当たり判定、演出、**ID** | **すべての文章**、i18n、ルーティング、外部リンク、静的ページ、レスポンシブ、Unity のロード管理 |
| **持たないもの** | 文章、翻訳、URL、プロジェクトのメタデータ | 3D、ゲームロジック、フレーム単位の入力 |
| **境界の原則** | Unity は「どのオブジェクトが選ばれたか」を **ID 文字列**で伝えるだけ。その ID が何を意味するかは知らない。 | Web は ID を受け取り、コンテンツを引き、描画する。3D 空間の内部状態は知らない。 |

この境界を守る限り、**文章の修正・翻訳の追加・リンクの差し替えに Unity の再ビルドは不要**になる。運用コストが桁で変わるので、ここは妥協しない。

---

## 4. Repository / Git Architecture

### 4.1 4 案の比較

前提として、**現行リポジトリはすでに健全**である（§0.2）。`.gitignore` も `.gitattributes` も LFS も適切に設定済みで、修正すべき点はない。したがって評価軸は「移行リスク」と「運用のしやすさ」に絞られる。

| 観点 | 案 A/B<br>既存 repo に `web/` 追加 | 案 C<br>Unity を `unity/` へ移動 | **案 D<br>Web を別 repo（推奨）** |
|---|---|---|---|
| Git 履歴の保全 | ✅ 無傷 | △ `git mv` で追随はするが全パス変更 | ✅ 無傷（触らない） |
| 移行作業 | ほぼゼロ | **大**（Unity 再オープン・`Library/` 全再生成で数十分・IDE/MCP のパス再設定） | **ゼロ** |
| リポジトリのクローン負荷 | 重い（LFS 864 MB を Web 作業でも引く） | 重い | ✅ Web repo は数 MB |
| GitHub Pages | ✗ 巨大 repo が Pages のソースになる | ✗ 同左 | ✅ Web repo をそのまま Pages に |
| CI/CD | Actions の checkout が LFS を引き帯域を消費 | 同左 | ✅ Web の CI は軽量・高速 |
| Claude Code / Cowork | △ 6.2 GB の作業ツリー | △ 同左 | ✅ Web repo は探索が速く文脈が汚れない |
| Unity ↔ Web の同時変更 | ✅ 1 コミットで完結 | ✅ | △ 2 コミットに分かれる |
| 将来の保守性 | △ 性質の違う 2 つが同居 | ○ | ✅ 関心の分離 |

### 4.2 結論: 案 D（2 リポジトリ構成）

```
github.com/Sh1n1230/MyWorld            ← 既存。今回は一切変更しない。
├── Assets/  Packages/  ProjectSettings/
├── .gitignore  .gitattributes  (現状のまま)
└── docs/ARCHITECTURE.md               ← 本書のみ追加（任意）

github.com/Sh1n1230/Sh1n1230.github.io ← 新規作成
├── app/
│   ├── page.tsx                       ← 言語選択
│   └── [lang]/
│       ├── layout.tsx                 ← Unity canvas 常駐
│       ├── page.tsx                   ← ワールド
│       ├── projects/[slug]/page.tsx   ← 静的詳細ページ
│       ├── about/page.tsx
│       └── contact/page.tsx
├── components/
│   ├── unity/                         ← canvas, loader hook
│   ├── overlay/                       ← DialogueBox, ProjectPanel, HUD
│   └── ui/
├── lib/
│   ├── bridge/                        ← イベント schema / client / queue
│   └── i18n/
├── content/
│   ├── projects/*.ts
│   ├── dialogue/*.ts
│   └── ui/{ja,en}.ts
├── public/
│   ├── unity/                         ← ★ .gitignore 対象（ビルド成果物）
│   ├── media/
│   └── .nojekyll
├── scripts/deploy.sh
├── docs/EVENT_SCHEMA.md               ← Unity 側と共有する契約
├── next.config.mjs
└── package.json
```

**「Unity ↔ Web の同時変更が 2 コミットに分かれる」という案 D 唯一の弱点は、実際にはほぼ問題にならない。** 両者の接点はイベント名と ID 文字列だけであり（§3.2 の境界原則）、変更頻度が低い。その契約を `docs/EVENT_SCHEMA.md` として Web repo に置き、Unity 側には対応する `WebBridgeEvents.cs` の定数を置いて、変更時は両方を更新する運用にする。

### 4.3 Unity 側に必要な変更（Phase 1 で行う、ただし本書承認後）

現状の Git 設定に**修正は不要**。追加のみ。

`.gitignore` に追記（WebGL ビルド出力用のディレクトリを掘る場合）:

```gitignore
# WebGL build output (公式テンプレートの /[Bb]uild/ で既にカバー済みだが明示)
/WebGLBuild/
```

> 公式テンプレートに `/[Bb]uild/` `/[Bb]uilds/` があるため、ビルド先を `Build/` にする限り**追記は不要**。ビルド先ディレクトリ名は `Build/WebGL/` に固定することを推奨する。

`.gitattributes` — **変更不要**。必要な拡張子（FBX/PNG/EXR/WAV/HDR/PSD/TGA/DLL/TTF/PDF 等）はすべて LFS 指定済み。

### 4.4 現状で LFS 化されていない大きなファイルの確認方法

Mac 側で以下を実行して、LFS 対象外の大きいファイルがないか確認されたい（本セッションでは Git 操作を実行しない方針のため未実行）。

```bash
cd ~/Unity/MyWorld
# 1) 追跡中で LFS ポインタでない大きいファイル上位 30
git ls-files -z | xargs -0 ls -l 2>/dev/null | sort -k5 -rn | head -30
# 2) LFS 管理下のファイル数と総量
git lfs ls-files | wc -l
git lfs ls-files -s | tail -5
# 3) 使用量（GitHub 側）
#    https://github.com/settings/billing で Git LFS の使用量を確認
```

### 4.5 移行手順（実行は承認後）

案 D は「移行」がほぼ存在しない。実行すべきは新規作成だけ。

```
Step 1. GitHub で新規 repo `Sh1n1230.github.io` を作成（Public、README なし）
Step 2. ローカルの適当な場所（例: ~/dev/portfolio-web）に clone
Step 3. Next.js プロジェクトを初期化（--ts --app --no-src-dir）
Step 4. public/.nojekyll を作成、.gitignore に public/unity/ を追加
Step 5. 初回コミット & push
Step 6. Settings > Pages で Source = "Deploy from a branch" / Branch = gh-pages / root
Step 7. MyWorld 側は何もしない（docs/ARCHITECTURE.md を置くなら 1 コミットのみ）
```

### 4.6 デプロイ方式（GitHub Pages 制約への対応）

GitHub Pages は **カスタム HTTP ヘッダを設定できない**。これが Unity WebGL 配信に 2 つの影響を与える。

1. `Content-Encoding: gzip` / `br` を返せない → **Decompression Fallback を有効にする必要がある**（§13.2）
2. `Content-Type: application/wasm` を返せない → WebAssembly のストリーミングコンパイルが使えず、初回起動がやや遅くなる（動作はする）

また、GitHub Pages の制約として **公開サイト 1 GB / 帯域 100 GB 月（ソフト）/ デプロイ 10 分タイムアウト / GitHub 自体の 100 MB ファイルサイズ上限**がある。

**したがってデプロイは「orphan ブランチへの force push」方式を採る。**

```
main ブランチ      : ソースのみ。バイナリは一切入らない（public/unity/ は gitignore）
gh-pages ブランチ  : 常に「1 コミットだけ」。デプロイのたびに履歴ごと上書き
```

これにより、WebGL ビルド（1 回 30〜60 MB 想定）を何度デプロイしても**リポジトリ履歴が肥大しない**。`npx gh-pages -d out --no-history` か、以下のような短いスクリプトで実現する（実装は Phase 1）。

```bash
# scripts/deploy.sh の概念（実装は後日）
#  1. Unity 側で WebGL ビルド → MyWorld/Build/WebGL/
#  2. rsync で public/unity/ へコピー
#  3. next build  (output: 'export' → out/)
#  4. touch out/.nojekyll
#  5. gh-pages ブランチへ単一コミットとして force push
```

**GitHub Actions で Unity をビルドしない理由**: Unity のセルフホストでない CI ビルドには (a) Unity ライセンスの secret 登録、(b) 6 GB 超の作業ツリーの checkout、(c) LFS 帯域の消費（無料枠 10 GiB/月に対し 1 回のフルチェックアウトで約 0.9 GiB）が必要になる。**月 10 回のビルドで枠を使い切る。** ポートフォリオ 1 個のためにこのコストは正当化できない。ローカルビルド + 手動デプロイで十分であり、そもそも Unity のビルドは Editor で行うのだから、そのまま流すのが自然。

---

## 5. Unity Architecture

### 5.1 シーン構成

```
Bootstrap.unity          （新規・ほぼ空）
  └ BridgeRoot (prefab, DontDestroyOnLoad)
      ├ WebBridge            … JS との送受信の単一窓口
      ├ AppState             … 現在の言語 / シーン / 対話状態
      ├ SceneFlow            … additive ロード / アンロード
      ├ InputModeController  … pointer / touch の切替
      └ AudioDirector        … BGM のクロスフェード

CafeScene.unity          （既存・additive でロード）
  ├ Environment            … ProBuilder 内装 + 家具 + ライティング
  ├ Player                 … 既存 Player.prefab（CharacterControllerBase）
  ├ CameraRig              … CameraFollow + FocusCameraTransition
  ├ Interactables
  │   └ Cigarette          … Interactable (id = "cafe.npc.smoker.cigarette")
  ├ NPC_Smoker             … Animator (Idle_Restless / Light / Talk)
  └ CafeSequence           … このシーン固有の進行管理

VillageScene.unity       （Phase 3。今は触らない）
FactoryScene.unity       （Phase 3。今は触らない）
```

**Bootstrap を分ける理由**: `DontDestroyOnLoad` オブジェクトをどのシーンに置くかで悩まなくなり、CAFE シーンを Editor から単体再生してもブリッジが必ず初期化される構造にできる（Editor 実行時は WebBridge がスタブモードで動く）。

### 5.2 スクリプト構成（`Assets/Scripts/` 配下に新設）

```
Assets/Scripts/
├── Portfolio.Runtime.asmdef        ← アセンブリ分離（コンパイル時間短縮）
├── Bridge/
│   ├── WebBridge.cs                ← 送受信の単一窓口。GameObject 名 "WebBridge" 固定
│   ├── WebBridgeEvents.cs          ← イベント名の定数（Web の schema.ts と 1:1）
│   ├── BridgeEnvelope.cs           ← { v, id, type, payload, ts } の DTO
│   └── Plugins/PortfolioBridge.jslib
├── Core/
│   ├── AppState.cs                 ← language, currentScene, flags
│   ├── SceneFlow.cs
│   └── InputModeController.cs      ← pointer / touch
├── Interaction/
│   ├── Interactable.cs             ← abstract。id, label key, 有効半径
│   ├── InteractableRegistry.cs     ← シーン内の一覧を保持
│   ├── PointerInteractor.cs        ← PC: レイキャスト + hover + click
│   ├── ProximityInteractor.cs      ← Mobile: 最近傍 1 個を候補に
│   └── HighlightController.cs      ← 距離ベースの発光 / アウトライン
├── CameraSystem/
│   ├── CameraRig.cs
│   └── FocusTransition.cs          ← 0.6s イージングで対象へ寄る / 戻る
└── Cafe/
    ├── CafeSequence.cs             ← 入店 → 探索 → 着火 → 対話 の状態機械
    ├── NpcSmoker.cs                ← Animator 制御 + 反応
    └── CigaretteInteractable.cs    ← Interactable の実装
```

既存の `Assets/Animations/Smoking.cs` と `CigaretteSmokeController.cs` は、`Cafe/` 配下に整理・統合することを推奨（Phase 1 の作業に含める）。

### 5.3 Interactable の設計

```csharp
// 概念のみ。実装は承認後。
public abstract class Interactable : MonoBehaviour
{
    [SerializeField] string id;          // "cafe.npc.smoker.cigarette"
    [SerializeField] string labelKey;    // "cafe.cigarette.label" → 文字列は Web が持つ
    [SerializeField] float  hintRadius;  // 4m: 発光開始
    [SerializeField] float  focusRadius; // 2m: アイコン表示 & 操作可能

    public abstract void OnActivate();   // 実際の演出
}
```

**`labelKey` は「文字列そのもの」ではなく「キー」であること**が重要。Unity は文章を一切持たない（判断 1）。

### 5.4 CAFE のゲーム進行（状態機械）

```
Intro ──▶ Explore ──▶ Focusing ──▶ Lighting ──▶ Dialogue ──▶ Outro
  │          ▲            │            │            │
  │          └────────────┘            │            │
  │        （対象から離れる）           │            │
  └ 入店演出 3s              カメラ寄せ 0.6s   着火演出 1.2s   Web が対話 UI を出す
```

各遷移で Web にイベントを送る（§7.3）。Web はそれを受けて DOM の表示を切り替える。**Unity 側は「Web が今どんな UI を出しているか」を知らない**——これが疎結合を保つ鍵。

### 5.5 Cinemachine を入れるか

現状未導入。**Phase 1 では入れない。** `FocusTransition.cs` で「現在のカメラ位置 → 目標位置・注視点」を `SmoothStep` で 0.6 秒補間するだけで CAFE の要求は満たせる（実質 40 行）。VILLAGE / FACTORY で複数のカメラ演出が必要になった時点で Cinemachine 3.x の導入を再検討する。パッケージ 1 つ分のビルドサイズと学習コストを Phase 1 では払わない。

---

## 6. Web Architecture

### 6.1 フレームワーク選定

| 候補 | 判定 | 理由 |
|---|---|---|
| Vanilla HTML/CSS/JS | ✗ | ルーティング・i18n・状態管理を全部自作することになる。初期は速いが Phase 2 で破綻。 |
| Vite + React + React Router | △ | 軽快で良いが SSG がなく、静的詳細ページ（SEO・フォールバック）を別途用意する必要がある。 |
| Astro | △ | SEO は最良だが、MPA 基盤のため「Unity canvas をルート遷移で保持し続ける」構成に逆らう。 |
| **Next.js (App Router) + TypeScript, `output: 'export'`** | **✅** | **layout に canvas を置くだけで「遷移してもアンマウントされない」が自然に実現する**。同時に SSG で静的詳細ページも生成でき、判断 2 の 2 経路設計がフレームワーク機能だけで成立する。GitHub Pages への静的エクスポートも公式サポート。 |

**採用: Next.js (App Router) + TypeScript + Tailwind CSS + zod。静的エクスポート。**

`next.config.mjs` の要点:

```js
// 概念。実装は承認後。
export default {
  output: 'export',           // 静的書き出し（GitHub Pages 必須）
  trailingSlash: true,        // 静的ホストでの /path/ 解決を安定させる
  images: { unoptimized: true }, // Image Optimization は静的エクスポートで使えない
  // basePath は不要（user site repo = ルート配信のため）
};
```

`public/.nojekyll` を必ず置く（Jekyll が `_next/` をアンダースコア始まりとして無視するのを防ぐ）。

### 6.2 ルーティング

| URL | 内容 | Unity |
|---|---|---|
| `/` | 言語選択。裏で Unity のプリロードを開始 | ロード開始 |
| `/ja`, `/en` | 3D ワールド（CAFE） | **常駐** |
| `/ja/projects/[slug]` | 直接アクセス = 静的 HTML。ワールド内からの遷移 = オーバーレイ | 直接なし / 内部は常駐 |
| `/ja/about`, `/ja/contact` | 静的ページ | なし |
| `/ja/no-webgl` | WebGL 非対応・低メモリ端末向けの案内 + 静的ポートフォリオ導線 | なし |

**言語は Web 側で持つ**（URL パスとして）。理由: (a) `/ja` `/en` が URL に出ることで SEO の hreflang とシェアが成立する、(b) 言語切替で Unity を再ロードしたくない、(c) 選択画面の待ち時間を Unity のプリロードに使える。切替時は `SET_LANGUAGE` を Unity に送るだけ（Unity 側の文字は音声・アニメ選択にしか影響しない）。

### 6.3 Unity のロードとライフサイクル

```
app/[lang]/layout.tsx
  └ <UnityProvider>                       ← React Context
      ├ <UnityCanvas />                   ← 一度だけマウント、以降絶対に外さない
      ├ <LoadingScreen />                 ← progress < 1 の間だけ表示
      ├ <OverlayHost />                   ← DialogueBox / ProjectPanel / HoverLabel
      └ {children}                        ← ルートごとに差し替わる部分
```

**`react-unity-webgl` を使うか自前で書くか**: 自前の薄いフック（`useUnityInstance`）を推奨する。Unity が出力する `Build/*.loader.js` を `<script>` で読み込み、`createUnityInstance()` を呼ぶだけで本質的には 60 行程度。依存を 1 つ減らせ、ロード進捗・エラー・メモリ設定を自分で制御でき、Next.js の SSR/dynamic import の癖に振り回されない。`react-unity-webgl` は良いライブラリだが、Unity のバージョン追随を外部依存に握られるのは、長期運用するポートフォリオでは避けたい。

**必須の防御**:
- `typeof window === 'undefined'` ガード（静的エクスポート時のビルドで落ちないように）
- WebGL2 コンテキスト取得失敗 → `/[lang]/no-webgl` へ誘導
- `navigator.deviceMemory < 4` の場合は警告を出してから続行を選ばせる
- `unityInstance.Quit()` はページ離脱時のみ

### 6.4 UI コンポーネント（Phase 1 で必要な最小集合）

| コンポーネント | 役割 | Phase |
|---|---|---|
| `LanguageSelect` | JA/EN 選択。**この画面が音声の user gesture を兼ねる** | 1 |
| `LoadingScreen` | 進捗バー + 世界観のある待ち画面 | 1 |
| `DialogueBox` | NPC のセリフ。下からせり上がる。クリック送り | 1 |
| `InteractionPrompt` | Mobile の丸ボタン / PC のホバーラベル | 1 |
| `ControlsHint` | 初回 5 秒だけの操作説明 | 1 |
| `ProjectPanel` | 作品詳細のサイドパネル / ボトムシート | 2 |
| `Hud` | エリア名・発見カウンタ・エリア切替 | 3 |
| `WorldMap` | 2D 平面図 | 3 |

---

## 7. Unity ↔ Web Communication

### 7.1 方式の比較

| 方式 | 方向 | 判定 |
|---|---|---|
| `unityInstance.SendMessage(obj, method, str)` | Web → Unity | ✅ 採用。公式 API、追加実装ゼロ、文字列 1 本で完結 |
| `.jslib` から `window` の関数を呼ぶ | Unity → Web | ✅ 採用。公式の唯一の正攻法 |
| `postMessage` | 双方向 | ✗ Unity を iframe に入れる場合のみ必要。今回は同一ドキュメントに canvas を置くので不要 |
| `Application.ExternalEval` | Unity → Web | ✗ 非推奨・廃止済み |
| WebSocket / HTTP | 双方向 | ✗ サーバーが不要な構成なので過剰 |

**iframe を使わない理由**: Unity を iframe に隔離するとメモリ分離やクラッシュ隔離の利点はあるが、`postMessage` の往復・フォーカス管理・スマホでのビューポート問題・オーバーレイの z-index 制御がすべて面倒になる。同一ドキュメントに `<canvas>` を置き、その上に DOM を重ねる方が単純で確実。

### 7.2 メッセージ封筒（Envelope）

**すべてのメッセージを 1 つの型に統一する。** 種類ごとに関数を生やすと、`SendMessage` の受け口が増えて Unity 側の GameObject 名・メソッド名の管理が破綻する。

```ts
// lib/bridge/schema.ts の概念
type Envelope = {
  v: 1;                 // プロトコルバージョン
  id: string;           // メッセージ ID（相関・重複排除用）
  type: string;         // "CIGARETTE_LIT" など
  payload: unknown;     // type ごとの構造
  ts: number;           // 発行時刻（epoch ms）
};
```

- **Web → Unity**: `unityInstance.SendMessage("WebBridge", "Receive", JSON.stringify(envelope))`
- **Unity → Web**: `.jslib` の `PortfolioEmit(ptr)` → `window.dispatchEvent(new CustomEvent("unity:message", { detail: JSON.parse(str) }))`

Web 側は `zod` で `Envelope` をパースし、`type` ごとの payload スキーマで検証する。Unity 側は `JsonUtility` で受け、未知の `type` は **warn して無視**（前方互換）。

### 7.3 イベント一覧（Draft — 契約は `docs/EVENT_SCHEMA.md` に置く）

**Unity → Web**

| type | payload | 用途 | Phase |
|---|---|---|---|
| `BRIDGE_READY` | `{ unityVersion, protocolVersions:number[] }` | ハンドシェイク。**これが来るまで Web は送信をキューする** | 1 |
| `WORLD_LOADED` | `{ scene: "cafe" }` | シーンの初期化完了。ローディング画面を消す | 1 |
| `HOVER_CHANGED` | `{ id \| null, labelKey, screenPos:{x,y} }` | ホバーラベルの表示位置 | 1 |
| `INTERACTABLE_IN_RANGE` | `{ id, labelKey }` / `{ id: null }` | Mobile の丸ボタンの出し入れ | 1 |
| `OBJECT_SELECTED` | `{ id, kind }` | 汎用の選択通知 | 1 |
| `CIGARETTE_LIT` | `{ npcId }` | 着火成功 | 1 |
| `DIALOGUE_STARTED` | `{ dialogueId, speakerId }` | 対話 UI を開く | 1 |
| `DIALOGUE_ENDED` | `{ dialogueId }` | 対話 UI を閉じる | 1 |
| `INPUT_MODE_CHANGED` | `{ mode: "pointer" \| "touch" }` | HUD の切替 | 1 |
| `ERROR` | `{ code, message }` | Unity 側の異常を Web に通知 | 1 |
| `PROJECT_SELECTED` | `{ projectId }` | 作品パネルを開く | 2 |
| `AREA_CHANGED` | `{ area }` | エリア移動 | 3 |

**Web → Unity**

| type | payload | 用途 | Phase |
|---|---|---|---|
| `SET_LANGUAGE` | `{ lang: "ja" \| "en" }` | 音声・アニメの言語差分 | 1 |
| `SET_INPUT_MODE` | `{ mode }` | Web が検出したデバイス種別を強制 | 1 |
| `SET_PAUSED` | `{ paused: boolean }` | オーバーレイ表示中の入力停止 | 1 |
| `ADVANCE_DIALOGUE` | `{ dialogueId, lineIndex }` | 対話送り（**進行の主導権は Web 側**） | 1 |
| `CLOSE_INFO` | `{}` | パネルを閉じた → カメラを戻す | 1 |
| `SET_QUALITY` | `{ level: "low" \| "med" \| "high" }` | 端末に応じた品質 | 2 |
| `LOAD_SCENE` | `{ scene }` | エリア切替 | 3 |
| `FOCUS_OBJECT` | `{ id }` | ワールドマップから対象へ移動 | 3 |

**対話の進行を Web が主導する理由**: セリフの本文と行数を持っているのは Web 側なので、「次の行があるか」を知っているのも Web 側。Unity には「今どの行か」だけ伝えれば、口パクや表情の同期が取れる。

### 7.4 ハンドシェイクとエラー処理

```
Web                                   Unity
 │  createUnityInstance() 開始          │
 │─────────────────────────────────────▶│
 │  （この間の送信はすべてキューに積む）  │  Bootstrap 初期化
 │                                      │  WebBridge.Awake()
 │◀──────── BRIDGE_READY ───────────────│
 │  protocolVersions に 1 が含まれるか確認│
 │───────── SET_LANGUAGE ──────────────▶│
 │───────── SET_INPUT_MODE ────────────▶│
 │  （キューをフラッシュ）                │
 │◀──────── WORLD_LOADED ───────────────│
 │  ローディング画面を消す                │
```

| 異常 | 挙動 |
|---|---|
| `BRIDGE_READY` が 60 秒来ない | ローディング画面にエラー表示 + リロード導線 + `/[lang]/no-webgl` へのリンク |
| プロトコルバージョン不一致 | Web がコンソールに警告。Unity 再ビルドが必要な旨を開発者向けに表示 |
| 未知の `type` | 双方向とも warn して無視（**絶対に例外を投げない**） |
| payload の検証失敗（zod） | warn して無視。UI は変化させない |
| Unity 側の例外 | `ERROR` イベントで Web へ通知。Web はトーストを出すのみで世界は止めない |

### 7.5 バージョニング

`v: 1` を封筒に含める。破壊的変更（既存イベントの payload 変更・削除）を行うときのみインクリメントする。イベントの**追加**はバージョンを上げない（未知の type は無視される設計なので安全）。Unity 側 `BRIDGE_READY` で `protocolVersions: [1]` を申告し、Web が対応範囲を判定する。

---

## 8. Scene Architecture

### 8.1 3 エリアの関係

```
                    ┌──────────────┐
                    │  Bootstrap   │  常駐（DontDestroyOnLoad）
                    └───────┬──────┘
                            │ additive load
        ┌───────────────────┼───────────────────┐
        ▼                   ▼                   ▼
   ┌─────────┐        ┌──────────┐       ┌───────────┐
   │  CAFE   │───────▶│  VILLAGE │◀─────▶│  FACTORY  │
   │ 導入    │  出口   │  自分自身 │        │  作品     │
   └─────────┘        └──────────┘       └───────────┘
    Phase 1             Phase 3            Phase 3

   ・CAFE → VILLAGE は一方向（導入は一度だけ）
   ・VILLAGE ↔ FACTORY は相互に行き来可能
   ・訪問済みフラグは Web 側の localStorage に保持し、
     再訪時は CAFE をスキップする選択肢を提示する
```

### 8.2 エリアごとの性格の違い

| | CAFE | VILLAGE | FACTORY |
|---|---|---|---|
| 目的 | 導入・世界観の提示 | 人となり（趣味・好み・価値観） | 成果（作品・実績） |
| 広さ | 狭い（迷わない） | 中 | 中 |
| インタラクタブル数 | 1（Phase 1） | 4〜6 | 5〜8 |
| 情報の密度 | 低（対話のみ） | 中（軽い紹介） | 高（技術・役割・期間・リンク） |
| 表示方式 | 対話ボックス | 小さめのカード | サイドパネル（フル情報） |
| 進行 | 線形（スクリプト） | 自由探索 | 自由探索 |

**CAFE だけ線形なのは意図的**。最初の 60 秒で「クリックすると何かが起きる」ことを確実に学習させる訓練区間として機能させる。ここでインタラクションの作法を覚えれば、以降の自由探索が破綻しない。

### 8.3 シーンのロード方針

- **1 つの WebGL ビルドに全シーンを含める**（別ビルドに分けない）。Addressables / AssetBundle は Phase 3 で必要になるまで導入しない。
- エリア切替は `SceneManager.LoadSceneAsync(additive)` + 前のシーンのアンロード。切替中は Web 側がフェード用オーバーレイを出す。
- `Assets/Scenes/SampleScene.unity` と `temp.unity` は**ビルド設定から外す**（`Scenes In Build` に入れない）。

---

## 9. Data Architecture

### 9.1 どこで管理するか — 比較

| 候補 | 判定 |
|---|---|
| Unity ScriptableObject | ✗ 文章を持たせると Unity 再ビルドが必要になり、翻訳の差分レビューも不可能 |
| ヘッドレス CMS（Contentful 等） | ✗ 1 人運用のポートフォリオに対して過剰。外部依存とコストと障害点が増える |
| Markdown / MDX | △ 長文には良いが、構造化フィールド（technologies, period, links）の型安全性が弱い |
| JSON | ○ 素直だが型チェックが実行時のみになる |
| **TypeScript（`content/**/*.ts`）+ zod** | **✅ 採用**。エディタ補完・コンパイル時型チェック・build 時に静的埋め込み・追加ランタイムゼロ |

**採用: コンテンツは Web リポジトリの TypeScript ファイルが唯一の正本。Git がそのまま CMS になる。**

将来、作品数が 30 を超えて長文が増えたら MDX（本文だけ）+ TS（メタデータ）のハイブリッドに移行できる。その時に考えればよい。

### 9.2 データモデル

```ts
// content/projects/_types.ts の概念
type Localized<T> = { ja: T; en: T };

type Project = {
  id: string;                    // "unity-horror-game" — Unity 側の ID と一致させる
  slug: string;                  // URL 用
  area: 'factory' | 'village';
  order: number;
  title: Localized<string>;
  summary: Localized<string>;     // 1〜2 行。パネルの見出し
  description: Localized<string>; // 本文
  role: Localized<string[]>;
  technologies: string[];         // 翻訳不要（"Unity", "C#"）
  period: { start: string; end?: string };
  media: { type: 'image'|'video'|'youtube'; src: string; alt: Localized<string> }[];
  links: { label: Localized<string>; href: string; kind: 'github'|'demo'|'article' }[];
  featured?: boolean;
};

type DialogueLine = {
  id: string;                    // "cafe.intro.01"
  speaker: 'npc_smoker' | 'player';
  text: Localized<string>;
  emotion?: string;              // Unity のアニメ切替に使う
  pauseMs?: number;
};
```

### 9.3 ID の命名規約（Unity と Web の唯一の接点）

```
<area>.<category>.<name>[.<part>]

cafe.npc.smoker
cafe.npc.smoker.cigarette
village.building.favorite-games
factory.exhibit.unity-horror-game
```

**この文字列だけが Unity と Web をつなぐ。** Unity 側は `Interactable.id` にこれを入れ、Web 側は `content/` のキーとして使う。命名がずれると無反応になるだけで例外は出ないので、Phase 1 で **ID の一覧を `docs/EVENT_SCHEMA.md` に併記**し、Web 側に「未知の ID を受け取ったら console.warn する」防御を入れる。

### 9.4 データの流れ

```
content/*.ts  ──(next build で静的埋め込み)──▶  JS bundle  ──▶  DOM に描画
                                                    ▲
                                                    │ id
                                        Unity ──────┘
                                     （文章は一切持たない）
```

Unity がコンテンツを **fetch する必要はない**。Unity は ID を投げるだけで、対応する文章の取得と描画は Web 側で完結する。

### 9.5 保存する状態

| 状態 | 保持場所 | 永続化 |
|---|---|---|
| 言語 | URL パス（`/ja`）+ `localStorage` | ✅ |
| 訪問済みエリア / 発見済みオブジェクト | Web の `localStorage` | ✅ |
| 現在のシーン / 対話の進行 | Web の React state + Unity の `AppState` | ✗（セッション限り） |
| プレイヤー位置 | Unity のみ | ✗ |

---

## 10. CAFE TODO（Unity 側）

> Phase 1 の完了定義: **「入店 → NPC を発見 → 近づく → タバコに火をつける → NPC が話し始める」が公開 URL 上で動く。**
> Claude Code が担当するのは Bridge/Interaction の枠組み、榛さんが担当するのはシーン・アニメ・演出。

### 10.1 準備（Claude Code 支援可）

- [ ] `CLAUDE.md` を現状に合わせて更新（Unity 6000.5.7f1、実在するシーン名、CAFE 中心の記述へ）
- [ ] `Assets/Scripts/Portfolio.Runtime.asmdef` を作成
- [ ] `Build Settings > Scenes In Build` を `Bootstrap` → `CafeScene` の 2 つだけに整理（`SampleScene` / `temp` を外す）
- [ ] Build Target を **Web (WebGL)** に切替（初回はシェーダ再インポートで時間がかかる）

### 10.2 Bridge 基盤（Claude Code が設計・実装可）

- [ ] `BridgeEnvelope.cs` — `{ v, id, type, payload, ts }` の DTO
- [ ] `WebBridgeEvents.cs` — イベント名定数（Web の `schema.ts` と 1:1 対応）
- [ ] `Plugins/PortfolioBridge.jslib` — `PortfolioEmit(ptr)` を実装
- [ ] `WebBridge.cs` — `Receive(string json)` の受け口、`Emit<T>(type, payload)` の送信、Editor 実行時のスタブ
- [ ] `Bootstrap.unity` の作成と `BridgeRoot.prefab` の組み立て
- [ ] Editor 上で `BRIDGE_READY` / `WORLD_LOADED` がログに出ることを確認

### 10.3 インタラクション基盤（Claude Code が設計・実装可）

- [ ] `Interactable.cs`（abstract）+ `InteractableRegistry.cs`
- [ ] `PointerInteractor.cs` — マウス位置からのレイキャスト、hover の enter/exit、click
- [ ] `ProximityInteractor.cs` — 最近傍 1 個を候補にする（Mobile 用）
- [ ] `InputModeController.cs` — `Touchscreen.current != null` などで判定、`INPUT_MODE_CHANGED` を送信
- [ ] `HighlightController.cs` — 距離 4m でフェードイン、2m でアイコン表示
- [ ] URP の Renderer Feature でアウトライン（または Emission プロパティの差し替えで簡易実装）

### 10.4 カメラ（Claude Code が設計・実装可）

- [ ] `FocusTransition.cs` — 現在位置 → 目標位置/注視点を 0.6s で補間、`CLOSE_INFO` で復帰
- [ ] 対話中の構図（NPC の肩越し / 顔が見える位置）を Transform として CafeScene に配置

### 10.5 CAFE 固有（榛さんが主担当、Claude Code は状態機械を支援）

- [ ] `CafeSequence.cs` — Intro / Explore / Focusing / Lighting / Dialogue / Outro の状態機械
- [ ] NPC のアニメーション 3 種を Animator に配線
  - [ ] `Idle_Restless`（ソワソワ・落ち着かない。ループ）
  - [ ] `Light`（火をつけてもらう反応）
  - [ ] `Talk`（会話中のループ。既存 `Smoking_cut.anim` を活用）
- [ ] `CigaretteInteractable.cs` — 着火時の演出（SE + パーティクル + 煙）
- [ ] 既存 `Smoking.cs` / `CigaretteSmokeController.cs` を `Assets/Scripts/Cafe/` へ整理・統合
- [ ] 入店演出（ドア音 → フェードイン 3s）
- [ ] 店内のライティングを WebGL 前提で調整（ベイク中心、リアルタイムライトは最小限）
- [ ] NPC への視線誘導（照明・家具の配置・BGM の定位）
- [ ] プレイヤーの移動範囲を Collider で制限（店の外に出られないように）

### 10.6 WebGL ビルド（Claude Code が手順書化）

**Phase 0（計測用・ホスティング非依存）** — 詳細手順は `PHASE0_GUIDE.html`

- [ ] Build Profiles で Web に切替（887 MB の再インポートで 30 分〜2 時間かかる）
- [ ] Scene List を `CafeScene.unity` の 1 つだけに
- [ ] Publishing: Compression = **Brotli** / **Decompression Fallback = ON** / Name Files As Hashes = OFF
- [ ] Other Settings は**既定のまま触らない**（Managed Stripping を High にすると Visual Scripting が壊れうる。最適化は Phase 1 以降）
- [ ] Development Build = **OFF**
- [ ] ビルド → `.data.unityweb` と `Build/` 全体のサイズを実測
- [ ] ローカルサーバー（`python3 -m http.server`）+ 同一 Wi-Fi の iPhone で実機計測

**Phase 1 以降（最適化）**

- [ ] ホスティング確定後、圧縮設定を最終形へ（ヘッダ設定可なら Brotli + Fallback OFF）
- [ ] Managed Stripping = High を試し、壊れないことを確認
- [ ] Initial Memory Size を 256 MB 程度に設定（大きすぎると iOS Safari で落ちる）
- [ ] `.agents/skills/optimize-web` を参照してサイズ削減（目標: 圧縮後合計 < 30 MB）

### 10.7 Phase 1 でやらないこと（明示）

- ❌ VILLAGE / FACTORY のシーン作業
- ❌ Addressables / AssetBundle
- ❌ Cinemachine の導入
- ❌ セーブデータ / 進行度システム
- ❌ Unity 内での文字表示（TMP 日本語フォント）
- ❌ 過度なテクスチャ / メッシュ最適化（まず実測してから）

---

## 11. Web TODO

### 11.1 セットアップ

- [ ] GitHub で `Sh1n1230.github.io` を新規作成（Public）
- [ ] `npx create-next-app@latest --ts --app --tailwind --no-src-dir`
- [ ] `next.config.mjs` に `output: 'export'` / `trailingSlash: true` / `images.unoptimized`
- [ ] `public/.nojekyll` を作成
- [ ] `.gitignore` に `public/unity/` を追加（**重要: ビルド成果物を main に入れない**）
- [ ] `zod` を導入
- [ ] Settings > Pages で `gh-pages` ブランチを公開元に設定

### 11.2 Bridge クライアント

- [ ] `lib/bridge/schema.ts` — `Envelope` と各 payload の zod スキーマ
- [ ] `lib/bridge/client.ts` — `send()` / `on()` / 送信キュー / `BRIDGE_READY` ハンドシェイク
- [ ] `lib/bridge/types.ts` — イベント名の union 型
- [ ] `docs/EVENT_SCHEMA.md` — Unity と共有する契約（ID 一覧を含む）
- [ ] 未知 type / 検証失敗時に **warn して無視**する挙動のテスト

### 11.3 Unity ローダー

- [ ] `components/unity/useUnityInstance.ts` — loader.js の動的読み込み、`createUnityInstance`、進捗、エラー
- [ ] `components/unity/UnityCanvas.tsx` — canvas + リサイズ + devicePixelRatio 対応
- [ ] WebGL2 サポート検出、失敗時は `/[lang]/no-webgl` へ
- [ ] `app/[lang]/layout.tsx` に組み込み（**children の外に置く**）

### 11.4 画面

- [ ] `app/page.tsx` — 言語選択（裏で Unity のプリロード開始）
- [ ] `components/overlay/LoadingScreen.tsx` — 進捗バー + 世界観のある待ち画面
- [ ] `components/overlay/DialogueBox.tsx` — セリフ表示・クリック送り・話者名・タイプライタ表示
- [ ] `components/overlay/InteractionPrompt.tsx` — PC のホバーラベル / Mobile の丸ボタン
- [ ] `components/overlay/ControlsHint.tsx` — 初回 5 秒
- [ ] `app/[lang]/no-webgl/page.tsx` — 静的ポートフォリオへの導線

### 11.5 コンテンツ / i18n

- [ ] `content/ui/{ja,en}.ts` — UI 文字列
- [ ] `content/dialogue/cafe.ts` — CAFE の導入セリフ（**ここがポートフォリオの第一印象。文章の質が最重要**）
- [ ] `lib/i18n/` — `lang` の解決と型付き `t()`
- [ ] `generateStaticParams` で `/ja` `/en` を生成

### 11.6 デプロイ

- [ ] `scripts/deploy.sh` — Unity ビルドのコピー → `next build` → `.nojekyll` → `gh-pages` へ orphan force push
- [ ] 初回デプロイして公開 URL で動作確認
- [ ] **実機確認**: iPhone Safari / Android Chrome / Mac Safari / Windows Chrome

### 11.7 Phase 2 以降

- [ ] `components/overlay/ProjectPanel.tsx` + `app/[lang]/projects/[slug]/page.tsx`（同一 URL・2 経路）
- [ ] `content/projects/*.ts`
- [ ] OGP 画像・`hreflang`・`sitemap.xml`・構造化データ
- [ ] `Hud` / `WorldMap`

---

## 12. Development Roadmap

| Phase | ゴール | 主な作業 | 目安 |
|---|---|---|---|
| **Phase 0**<br>基盤確認 | 「Unity WebGL が GitHub Pages 上で動く」ことだけを確認する | CAFE を**現状のまま**そのまま WebGL ビルド → Next.js の最小構成に載せてデプロイ。ブリッジもオーバーレイもなし。 | 1〜2 日 |
| **Phase 1**<br>CAFE 体験 | 入店 → 着火 → 対話が公開 URL で成立 | §10 / §11 のすべて | 2〜3 週 |
| **Phase 2**<br>ポートフォリオ化 | 作品情報が見られる | FACTORY に展示物 3 個、`ProjectPanel`、静的詳細ページ、SEO、OGP | 2〜3 週 |
| **Phase 3**<br>世界の拡張 | VILLAGE / FACTORY の作り込み | エリア切替、HUD、ワールドマップ、発見カウンタ、残りの展示物 | 4 週+ |
| **Phase 4**<br>磨き込み | 品質 | パフォーマンス最適化、演出、アクセシビリティ、独自ドメイン | 継続 |

### Phase 0 を独立させる理由

**このプロジェクトの最大の未知数は「Unity WebGL が GitHub Pages 上で、実機のスマホで、許容できる速度で動くか」である。** ここが破綻すると、その後の設計がすべて無意味になる。だから**ブリッジもオーバーレイも作る前に、まず現状の CAFE をそのままビルドして公開し、iPhone で開いてみる**。

具体的に測るもの:

1. 圧縮後の総ビルドサイズ（目標 < 30 MB、許容 < 60 MB）
2. iPhone Safari での初回ロード時間（目標 < 30 秒）
3. iPhone Safari でメモリ不足のクラッシュが起きないか
4. 実効フレームレート（目標 30fps 以上）

**もし Phase 0 でサイズが 100 MB を超えるようなら、設計ではなくアセットの選定を先に見直す必要がある。** 887 MB のインポート済みアセットを抱えている現状、これは十分にありうる。

---

## 13. Risks

### 13.1 ビルドサイズ 🔴 最重要

| 項目 | 内容 |
|---|---|
| **リスク** | `Assets/ImportedAssets/` に 887 MB のアセットがある。CAFE から参照されているものだけがビルドに含まれるとはいえ、Synty / Asset Store のパックは高解像度テクスチャを含み、数個使うだけで数十 MB になる。 |
| **影響** | ロードが 2 分を超えると訪問者は離脱する。ポートフォリオとして致命的。 |
| **対策** | ① Phase 0 で**必ず実測**する。② `Assets/Resources/` フォルダを作らない（中身が全部ビルドに入る）。③ テクスチャの Max Size を CAFE で使うものだけ 1024 以下に落とす。④ Crunch 圧縮を有効化。⑤ `.agents/skills/optimize-web` を活用。⑥ Editor の Build Report でサイズ内訳を確認。 |

### 13.2 GitHub Pages のヘッダ制約 🟡

| 項目 | 内容 |
|---|---|
| **リスク** | GitHub Pages は `Content-Encoding` / `Content-Type: application/wasm` を設定できない。Brotli / Gzip の**ネイティブ展開が使えず**、Decompression Fallback（JS 展開）が必須になる。ローダーが大きくなり、起動が遅くなる。 |
| **対策** | ① 圧縮形式は **Gzip + Decompression Fallback ON**（Brotli の JS 展開は Gzip より明確に遅いため、Brotli の転送量メリットが相殺されやすい）。② それでも遅いなら **Cloudflare Pages へ移行**すれば `Content-Encoding: br` を設定でき、劇的に改善する。**移行は静的ファイルを置き換えるだけなので、この設計はホスティングにロックインされない。** |
| **判断** | まず GitHub Pages で始め、Phase 0 の実測で許容できなければ Cloudflare Pages に切り替える。設計変更は不要。 |

### 13.3 iOS Safari のメモリ 🟡

| 項目 | 内容 |
|---|---|
| **リスク** | iOS Safari は WebGL のメモリ上限が厳しく、超えるとタブが無言でリロードされる。ユーザーには「壊れた」としか見えない。 |
| **対策** | ① Initial Memory Size を控えめ（256 MB）に。② テクスチャ解像度を抑える。③ SharedArrayBuffer / マルチスレッドを使わない（COOP/COEP ヘッダが必要で GitHub Pages では設定不可）。④ `navigator.deviceMemory` で低メモリ端末を検出し、`/[lang]/no-webgl` の静的ポートフォリオを案内する**フォールバック導線を必ず用意する**。 |

### 13.4 オーディオの自動再生 🟢

| 項目 | 内容 |
|---|---|
| **リスク** | ブラウザは user gesture なしに音を鳴らせない。BGM が無音のまま始まる。 |
| **対策** | **言語選択画面のボタンクリックを user gesture として使う**設計になっている（§2.1）。UX 上の待ち時間と技術的要件が一致している。 |

### 13.5 「何をクリックすればいいか分からない」 🟡

| 項目 | 内容 |
|---|---|
| **リスク** | ポートフォリオとして最悪の失敗は「訪問者が何も発見できずに閉じる」こと。 |
| **対策** | ① インタラクタブルを 1 シーン 5〜9 個に制限。② 距離ベース 3 層ヒント（§2.2）。③ CAFE を線形の訓練区間にする。④ Phase 2 以降で発見カウンタ（3/8）を出す。⑤ **必ず第三者に実機で触ってもらい、無言で観察する**（自分では絶対に分からない）。 |

### 13.6 Git LFS の帯域 🟢

| 項目 | 内容 |
|---|---|
| **現状** | GitHub Free の LFS 無料枠は**ストレージ 10 GiB / 帯域 10 GiB 月**（従量課金制に移行済み）。現在の LFS 使用量は約 864 MB なので、**当面は余裕がある**。 |
| **リスク** | GitHub Actions で Unity リポジトリを checkout すると 1 回あたり約 0.9 GiB の帯域を消費し、月 10 回で枠を使い切る。 |
| **対策** | **CI で Unity をビルドしない**（§4.6）。ローカルビルド + 手動デプロイなら LFS 帯域を消費しない。 |

### 13.7 スコープの膨張 🔴

| 項目 | 内容 |
|---|---|
| **リスク** | このプロジェクトは「作りたいこと」が無限に湧く構造をしている。VILLAGE の建物を増やし、演出を凝り、最適化に凝り始めると、**公開されないまま止まる**。 |
| **対策** | ① Phase 0 → Phase 1 の完了定義を絶対に動かさない。② **Phase 1 が終わったら、未完成でも一度公開する**。③ 「後でやる」リストを §14.2 に明示的に置き、そこに逃がす。 |

### 13.8 SEO 🟢

| 項目 | 内容 |
|---|---|
| **リスク** | 3D ワールドの中身は検索エンジンに一切見えない。Unity canvas だけのサイトは実質インデックスされない。 |
| **対策** | 静的 HTML ページ（`/ja/projects/[slug]`, `/ja/about`）を Next.js の SSG で生成し、そこに全テキストを置く。**判断 1（文章は Web が持つ）のおかげで、SEO 対応が追加コストゼロで手に入る。** |

---

## 14. Decisions

### 14.1 今すぐ決めるべきこと（Phase 0/1 の前提になる）

| # | 決定事項 | 内容 | 状態 |
|---|---|---|---|
| D1 | リポジトリ構成 | **案 D: Web は別リポジトリ。MyWorld は不変** | ✅ 合意済み |
| D2 | Web フレームワーク | **Next.js App Router + TypeScript + 静的エクスポート** | ✅ 合意済み |
| D3 | ホスティング | **保留 — Phase 0 の `.data` サイズで自動決定**<br>25 MiB 未満 → Cloudflare Pages ／ 超過 → Netlify ／ 手間最小を優先するなら GitHub Pages | ⏸ Phase 0 待ち |
| D4 | Unity 圧縮設定 | **D3 に連動**<br>ヘッダ設定可 → Brotli + Fallback OFF ／ GitHub Pages → Gzip + Fallback ON<br>**Phase 0 のみ Brotli + Fallback ON**（どこでも動く中立設定） | ⏸ D3 に連動 |
| D5 | テキストの所在 | **すべて Web（DOM）。Unity は ID のみ** | ✅ 合意済み |
| D6 | 情報表示 | **DOM オーバーレイ + URL 同期。Unity は再ロードしない** | ✅ 合意済み |
| D7 | 言語切替 | **Web 側。`/ja` `/en` のパスルーティング** | ✅ 合意済み |
| D8 | 通信方式 | **SendMessage + .jslib、単一 Envelope、未知 type は無視、BRIDGE_READY 握手** | ✅ 合意済み |
| D9 | データ管理 | **コンテンツは TypeScript のみ（zod 不要）。zod はブリッジ受信部のみ** | ✅ 簡素化して合意 |
| D10 | Phase 1 のスコープ | **CAFE のみ、インタラクタブル 1 個、対話 UI 1 種** | ✅ 合意済み |
| D11 | Phase 0 の実施 | **設計実装より先に、現状 CAFE をビルドして実機計測** | ✅ 合意済み・実行中 |

> **D9 の訂正（v1.1）**: 当初「TypeScript + zod」としていたが、自分で書くコンテンツはビルド時に型検査が完了するため zod は不要。zod が必要なのは、実行時に Unity から届く（＝ビルド時に検査できない）ブリッジのメッセージのみ。§9.1 の比較表もこれに合わせて読むこと。

### 14.2 後回しでよいこと（今決めない）

| 項目 | いつ決めるか |
|---|---|
| VILLAGE の建物構成・デザイン | Phase 3 の着手時 |
| FACTORY の展示物の見せ方（PC / 額縁 / ホログラム） | Phase 2 の着手時 |
| Cinemachine を入れるか | 複数のカメラ演出が必要になった時 |
| Addressables / AssetBundle | ビルドサイズが実測で問題になった時 |
| 独自ドメイン | Phase 2 以降。`sh1n1230.github.io` で始めて問題ない |
| アナリティクス（GA / Plausible） | 公開後 |
| Contact フォームの実装方式 | Phase 2。まずは mailto / GitHub / X へのリンクで十分 |
| Cloudflare Pages への移行 | Phase 0 の計測結果次第 |
| BGM / SE の最終的な作り込み | Phase 4 |
| ローディング画面の演出の作り込み | Phase 4 |
| 訪問者の進行度保存 | Phase 3 |

### 14.3 明示的に「やらない」と決めること

- ❌ Unity 内での日本語テキスト表示（TMP フォントアトラス）
- ❌ CI での Unity 自動ビルド
- ❌ Unity を iframe に隔離する構成
- ❌ ヘッドレス CMS
- ❌ サーバーサイド（API / DB）
- ❌ マルチプレイヤー / リアルタイム機能
- ❌ ページ遷移で Unity をアンマウントする構成

---

## 15. Immediate Next Steps

**この順番で進める。各ステップは前のステップの結果に依存する。**

### Step 1 — 本書のレビュー（榛さん）

特に §14.1 の D1〜D11 に同意できるかを確認する。**D1（別リポジトリ）と D5（テキストは全部 Web）** が最も影響が大きい判断なので、ここだけは納得してから進むこと。

### Step 2 — Phase 0: 現状 CAFE を WebGL でビルドして実測（榛さん、Claude Code が手順支援）

**まだ何も新規実装しない。** 今ある `CafeScene` をそのまま Web にビルドする。

1. Unity で Build Target を **Web (WebGL)** に切替（シェーダ再インポートで時間がかかる）
2. Publishing Settings: Compression = **Gzip**、**Decompression Fallback = ON**
3. Player Settings: IL2CPP / Managed Stripping = High / Exceptions = Explicitly Thrown Only
4. `Build Settings > Scenes In Build` を `CafeScene` だけにしてビルド → `Build/WebGL/`
5. **圧縮後の総サイズを記録する**

### Step 3 — Phase 0: 最小の Web でデプロイ（Claude Code が実装）

Next.js の最小構成（言語選択もブリッジもなし。canvas を出すだけ）を作り、GitHub Pages に公開する。

### Step 4 — Phase 0: 実機で計測（榛さん）

**iPhone Safari で開く。** これが本プロジェクト最大の判断ポイント。

| 指標 | 目標 | 許容 | これを超えたら |
|---|---|---|---|
| 圧縮後サイズ | < 30 MB | < 60 MB | アセット削減 or Cloudflare Pages |
| iPhone 初回ロード | < 30 s | < 60 s | 同上 |
| iPhone でのクラッシュ | なし | — | メモリ設定・テクスチャ見直し |
| フレームレート | 30fps+ | 24fps+ | ライティング・ポリゴン見直し |

### Step 5 — 計測結果を受けて設計を微調整

サイズが想定を大きく超えていた場合、**Phase 1 に進む前に**アセット戦略（どのパックを使うか、テクスチャ解像度、ライトのベイク方針）を見直す。設計書のこの節に結果を追記する。

### Step 6 — Phase 1 着手

§10 / §11 の TODO に従って進める。着手順は次のとおり:

1. `docs/EVENT_SCHEMA.md` の確定（Unity と Web の契約を先に固める）
2. Unity 側 Bridge 基盤（`WebBridge` + `.jslib`）
3. Web 側 Bridge クライアント + ハンドシェイク
4. **Editor と Web の間で `BRIDGE_READY` の往復が通ることを確認**（ここが通れば残りは応用）
5. インタラクション基盤（Interactable / Interactor / Highlight）
6. `CafeSequence` の状態機械
7. Web の対話 UI
8. 演出の作り込み
9. 実機確認 → 公開

---

## 付録 A — 用語

| 用語 | 意味 |
|---|---|
| Envelope | Unity ↔ Web で交換されるメッセージの統一形式 `{ v, id, type, payload, ts }` |
| Interactable | ワールド内のクリック可能オブジェクト。ID を持つが文章は持たない |
| Decompression Fallback | サーバーが `Content-Encoding` を返せない場合に、Unity のローダーが JS で展開する機能 |
| orphan branch force push | 履歴を持たない単一コミットとしてブランチを毎回上書きするデプロイ手法 |
| L1 / L2 / L3 | インタラクション可能性を伝える 3 層のヒント（環境 / 近接 / 照準） |

## 付録 B — 参照した外部情報

- GitHub Pages limits — https://docs.github.com/en/pages/getting-started-with-github-pages/github-pages-limits
- Unity Manual: Deploy a Web application — https://docs.unity3d.com/Manual/webgl-deploying.html
- Git LFS billing（無料枠 10 GiB / 従量課金） — https://docs.github.com/billing/managing-billing-for-git-large-file-storage/about-billing-for-git-large-file-storage
