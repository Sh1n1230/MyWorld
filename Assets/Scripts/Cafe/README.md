# Cafe — カフェの演出

`docs/ARCHITECTURE.md` §6.3 / §6.4。状態機械の全体像は `docs/EVENT_SCHEMA.md` §4。

| ファイル | 役割 |
|---|---|
| `CafeSequence.cs` | 進行の状態機械。`entering → explore → focusing → lighting → smoke → title → free` |
| `CigaretteCutscene.cs` | 煙を追うカメラ。Cinemachine も Timeline も使わず、ここで直接補間する |
| `CigaretteSmokeController.cs` | 喫煙パーティクルの制御 |

## セットアップ（Editor 作業）

`docs/EVENT_SCHEMA.md` §9 に手順がある。特に忘れやすいのは 2 つ:

1. タバコの `SignalInteractable` の **`Notify Web On Select` のチェックを外す**。
   外さないと `OBJECT_SELECTED` と `DIALOGUE_REQUESTED` が二重に飛ぶ。
2. `CigaretteCutscene` の `Disable During Cutscene` に **`CameraFollow`** を入れる。
   入れ忘れると演出中もカメラ追従が働いてカメラが暴れる。

## 守ること

**Unity は「Web が今どんな UI を出しているか」を知らない。**
対話の進行の主導権は Web にある（行数を知っているのは Web だけ）。
`CafeSequence` は `DIALOGUE_REQUESTED` を投げたら、`END_DIALOGUE` が返るまで待つだけ。
