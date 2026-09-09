# VISION — 行き先

> Status: **構想。決定ではない。** 2026-09-09 記
> **このファイルの内容を根拠に実装してはいけない。** 決定事項は `ARCHITECTURE.md` にしか書かれていない。
> ここに書いてあるのは「なぜこれを作るのか」と「どこへ向かうのか」であって、「何をどう作るか」ではない。

---

## 0. なぜ分けてあるのか

設計書に「決まったこと」と「やりたいこと」が混ざると、実装するとき（自分も、Claude Code も）**どれが決定でどれが願望か判別できなくなる**。v1 の設計書は実際にその状態になっていた。

だから正本は 2 本に分けてある。

| | 中身 | 変わったら |
|---|---|---|
| `ARCHITECTURE.md` | **決定のみ。** 実装はここだけを見る | 決め直したときだけ |
| `VISION.md`（この文書） | 構想・方向性・まだ決めていないこと | いつ変わってもよい |

構想が変わっても正本は汚れない。それがこの分割の目的。

---

## 1. 一行で言うと

> **A portfolio you don't browse, but enter.**
> 技術を展示するのではなく、技術と人間の関係そのものを体験させるポートフォリオ。

見終わった人に「Unity を触れる学生」と思わせるだけでは弱い。

> **「この人はゲーム・Web・AI・セキュリティ・メディアアートを横断して技術を作っている。そして、その技術が人間や社会にどう影響するかまで考えている。」**

ここまで届かせたい。

---

## 2. 4 つの層

```
                        AI × HUMANITY
                              │
                ┌─────────────┴─────────────┐
              WORLD                       THOUGHT
                │                            │
      ┌─────────┼─────────┐                  │
    CAFE     VILLAGE   FACTORY               │
   出会う      知る       見る                 │
                │          │                 │
                │        WORKS               │
                │     Unity / Web            │
                │     p5.js / ML             │
                │          │                 │
                └────┬─────┘                 │
                     ▼                       │
                    LAB ────────────────────┘
              Security / AI / Data
```

| 層 | 中身 |
|---|---|
| **Layer 1 — World** | CAFE（導入・世界観）/ VILLAGE（自分自身・趣味・価値観）/ FACTORY（作品・成果） |
| **Layer 2 — Works** | Unity / Web / p5.js / ML / Spotify |
| **Layer 3 — Lab** | Security / AI / Data — 「作品」ではなく「動いている実験」 |
| **Layer 4 — Thought** | AI 倫理 / 教育 DX / 自動運転 / アルゴリズム採用・警察 / プライバシー |

**THOUGHTS は第 5 のエリアではない。世界全体を貫いている層である。** 思想を別ページにまとめる必要すらなく、NPC の会話・展示・Lab・ケーススタディに分散して存在させられる。

---

## 3. 思想を「主張」ではなく「体験」にする

「AI 倫理に興味があります」で終わらせない。

```
問い → 調査 → 自分の立場 → 実験 → コード → 作品 → 結果
```

ここまで行って初めて、**思想が実装される**。

### 3.1 根にある問い

> **人工知能と人間の境界線はどこにあるのか。**

そこから、

- AI にどこまで判断させるのか
- 人間は何を担当すべきか
- AI に責任はあるのか
- AI の判断を人間はどう扱うべきか

### 3.2 分析の型

「AI は危険だから規制しよう」とは言わない。代わりに毎回この順で見る。

```
Technology → Benefit → Risk → Who gains? → Who loses? → Who decides? → Who is responsible?
```

### 3.3 ケーススタディ候補

| テーマ | 問い |
|---|---|
| **採用 AI（Amazon）** | 過去の人間の判断を学習した AI は、その過去の偏りを再生産するのではないか |
| **アルゴリズム警察（米国）** | 「AI が間違えた」で終わらせず、**その出力を人間が制度の中でどう使ったか**を見る |
| **自動運転** | トロッコ問題にしない。開発者 → メーカー → モデル → 車両 → 運転者 → 歩行者 と**責任が分散する**問題として見る |
| **教育 DX** | 格差を縮めるのか、それとも「技術を使える人 / 使えない人」という新しい格差を作るのか |
| **プライバシー** | このサイト自身が訪問者データを扱う（§5） |

**カフェの NPC が最後に呟く「決めたのは機械じゃない。誰かが決めたんだ」が、この全部の根に繋がっている。**

---

## 4. 思想と作品を接続する

```
AI Ethics      →  Algorithmic Hiring   →  AI Harness
Privacy        →  Visitor Analytics    →  Cookie / Data Design
Security       →  Security Case        →  security-checker
Education DX   →  Digital Divide       →  Interactive Visualization
```

「この問題について私はこう考える。その考えからこの技術を作った。」まで持っていく。

---

## 5. このサイト自身を実験対象にする

一番面白くなり得るのはここ。

### Security Lab

既存の `security-checker` を持ってきて、**自分のポートフォリオ自身を自分のセキュリティチェッカーで検査する**。

```
Security Score
  CSP           ✓
  HSTS          ✓
  Cookie        ✓
  Headers       ✓
  Dependencies  ⚠
```

「Security の作品を展示する」のではなく、**このサイト自身が Security の実験対象になる**。

### Privacy / Visitor Analytics

アンケート（`ARCHITECTURE.md` §3.1）は、それ自体がプライバシーのテーマになる。

```
Thought → Privacy → Visitor Analytics → 実装 → 自己監査
```

「データをどう扱うべきか」を主張するのではなく、**自分のサイトが実際に訪問者データを扱い、その扱い方を公開する**。

Phase 1 で「回答はこのブラウザにのみ保存されます」と書いてあるものが、Phase 3 でサーバに送られるようになったとき、**その文言を正直に書き換える**。この書き換えの履歴そのものが展示物になり得る。

### AI Lab

```
Source Code → LLM → Security 分析 → アーキテクチャ分析 → Ethics / Safety Harness → Report
```

「AI を呼び出した」ではなく、**AI をどうシステムとして制御するか**を見せる。

---

## 6. 手持ちの素材

現在 GitHub にあるもの。FACTORY / LAB / Experiments に配分する候補。

| リポジトリ | 配置候補 |
|---|---|
| `project-penguin` — 何もしないとペンギンの氷が溶けていく。現実の消費行動と連動する環境シミュレーション | **CAFE に先出し**（Phase 1）→ FACTORY |
| `security-checker` | LAB / Security |
| `keiba-ai` / `ml-competition-with-claude-template` / `tech-ocean-student-cup-2026` | FACTORY / ML |
| `SpotifyEmbedded` | Experiments（§7） |
| `RandomNote` — 溜めたメモをランダムに提示して受動的に復習する iOS アプリ | FACTORY |
| `AIDE` — AI と共同開発するための統合環境 | LAB / AI |
| `prohairesis` — AI エージェントをローカルで動かす基盤。効果を復元可能にすることで自由度を最大化する | LAB / AI |
| `ptcg-ai-bot` / `Torched` / `CPMI` | 未定 |

---

## 7. Experiments（p5.js）

FACTORY とは別枠。**見るのではなく、その場で触れる**作品群。

generative art / heartbeat / swirling / grid / particle / interaction / visualization。

Unity と違って軽量なものを大量に載せられるので、**サイト全体の「遊び」の部分**になる。Web 側で直接動かす。

### Spotify × p5.js

```
Spotify Data → Audio Features → p5.js → Generative Art

Tempo        → animation speed
Energy       → movement
Danceability → particle count
Valence      → visual parameters
```

**音楽 × データ × p5.js × メディアアート**として一つの作品にできる。

---

## 8. 技術の役割分担（増やすときの指針）

```
Unity     = 3D World
p5.js     = Generative / Media Art
Rive      = UI Animation（必要になったら）
Three.js  = Unity でも p5.js でも足りなくなったら
```

**使う理由が発生した技術だけ勉強する。** 現時点で不要: Kubernetes / Terraform / Redis / GraphQL / Docker Compose / Microservices。Hono も API が必要になってから。

---

## 9. 学習の優先順位

| 順 | 何を | いつ |
|---|---|---|
| 1 | HTML / CSS / JS / TypeScript / React / Next.js — 特に DOM, async/await, fetch, HTTP, JSON, routing, state, localStorage | **Phase 1** |
| 2 | Unity WebGL ↔ JS — `createUnityInstance`, WebGL Template, `SendMessage`, `.jslib`, WebAssembly, compression, memory | **Phase 1** |
| 3 | SEO / OGP / 静的生成 / アクセシビリティ | Phase 2 |
| 4 | SQL / PostgreSQL / Supabase / RLS | Phase 3 |
| 5 | Security — HTTP / Cookie / CSP / CORS / CSRF / XSS / SQLi / 認証 / 認可 | Phase 3〜 |
| 6 | AI — LLM API / structured output / tool calling / agent / evaluation / guardrails / harness | Phase 5 |

---

## 10. まだ決めていないこと

`ARCHITECTURE.md` に書かれていないものは全部これに該当するが、特に大きいもの:

- NPC の具体的な台詞（6〜8 行の中身。**Phase 1 の実装は仮の 7 行で進める**）
- NPC と思想をどう接続するか
- VILLAGE の具体的な建物 / FACTORY の展示方法
- p5.js 作品の配置と見せ方
- Security Lab / AI Lab の具体的な形
- THOUGHTS のケーススタディ本文
- ドメインの最終決定（`sh1n1230.dev` が第一候補）

---

## 11. 一番大事なルール

> **CAFE を完成させるまで、新しい世界を作らない。**

この構想は無限に広がる。Unity / p5.js / Rive / Spotify / AI / Security / ML / DB / Analytics / Survey / Ethics / Philosophy — いくらでも増える。

**このプロジェクトの最大の失敗は、技術的な失敗ではなく「公開されないこと」である。**
