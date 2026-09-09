# Web — Unity ↔ Web ブリッジ

契約の正本は **`docs/EVENT_SCHEMA.md`**。Web リポジトリの `lib/bridge/` と 1:1 で対応する。
**片方だけ直すと例外は出ず、ただ無反応になる。**

## 構成

| ファイル | 役割 |
|---|---|
| `WebBridge.cs` | 唯一の窓口。`SendMessage("WebBridge", "Receive", json)` の受け口はこれ 1 つ |
| `WebEvents.cs` | イベント名・payload の型。`schema.ts` と対応させる |
| `Assets/Plugins/WebGL/PortfolioBridge.jslib` | Unity → Web。`CustomEvent("unity:message")` を投げる |

## 守ること

- **`WebBridge` はシーンに置かない。** `[RuntimeInitializeOnLoadMethod(BeforeSceneLoad)]` で
  自分を `WebBridge` という名前で生成する。この名前が `SendMessage` の宛先そのもの。
- **未知の `type` は warn して無視する。絶対に例外を投げない。** 落ちると Web から復旧できない。
- **文字を持たない**（D1）。載せるのは `id` と `labelKey` だけ。
  だから `SET_LANGUAGE` は存在しない — Unity は言語を知らない。
- **イベントは全 11 種。** 増やす前に「Web 側だけで解決できないか」を必ず疑うこと。

## エディタでの確認

`.jslib` は WebGL ビルドでしか動かないので、エディタでは代わりに
`Debug.Log("[WebBridge] → Web ...")` が出る。**Web が無くても配線を確認できる。**

受信を試すには、デバッグ用スクリプトから直接叩く:

```csharp
WebBridge.Instance.Receive("{\"v\":1,\"id\":\"t\",\"type\":\"SET_AUDIO_MUTED\",\"payload\":{\"muted\":false},\"ts\":0}");
```
