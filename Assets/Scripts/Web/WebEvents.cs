using System;

// docs/EVENT_SCHEMA.md の型定義。Web リポジトリの lib/bridge/schema.ts と 1:1 で対応する。
//
// **片方だけ直すと、例外は出ずにただ無反応になる。** 変えるときは必ず両方を直すこと。
namespace Portfolio.Web
{
    // イベント名。文字列リテラルを散らさないためにここへ集約する。
    public static class WebEventType
    {
        // ---- Unity → Web（7 種）----
        public const string BridgeReady          = "BRIDGE_READY";
        public const string SceneReady           = "SCENE_READY";
        public const string CharacterSelected    = "CHARACTER_SELECTED";
        public const string InteractableInRange  = "INTERACTABLE_IN_RANGE";
        public const string ObjectSelected       = "OBJECT_SELECTED";
        public const string DialogueRequested    = "DIALOGUE_REQUESTED";
        public const string SequenceState        = "SEQUENCE_STATE";

        // ---- Web → Unity（4 種）----
        public const string SetPaused            = "SET_PAUSED";
        public const string SetAudioMuted        = "SET_AUDIO_MUTED";
        public const string AdvanceDialogue      = "ADVANCE_DIALOGUE";
        public const string EndDialogue          = "END_DIALOGUE";

        // 意図的に存在しないもの: SET_LANGUAGE / GOTO_SCENE / HOVER_CHANGED / ERROR。
        // 理由は docs/EVENT_SCHEMA.md §5。増やす前に必ず読むこと。
    }

    // SEQUENCE_STATE の state 値。docs/EVENT_SCHEMA.md §3.3。
    public static class SequenceState
    {
        public const string Entering = "entering";
        public const string Explore  = "explore";
        public const string Focusing = "focusing";
        public const string Lighting = "lighting";
        public const string Smoke    = "smoke";
        public const string Title    = "title";
        public const string Free     = "free";
    }

    // シーン名 → EVENT_SCHEMA 上の scene 値。
    public static class WebSceneId
    {
        public const string PlayerChoose = "player_choose";
        public const string Cafe         = "cafe";
    }

    // ---------------- Unity → Web の payload ----------------

    [Serializable]
    public class BridgeReadyPayload
    {
        public int[] protocolVersions;
        public string unityVersion;
    }

    [Serializable]
    public class SceneReadyPayload
    {
        public string scene;
    }

    [Serializable]
    public class CharacterSelectedPayload
    {
        public string characterId;
    }

    // 圏外に出たときは id / labelKey がともに null。
    [Serializable]
    public class InteractableInRangePayload
    {
        public string id;
        public string labelKey;
    }

    [Serializable]
    public class ObjectSelectedPayload
    {
        public string id;
    }

    [Serializable]
    public class DialogueRequestedPayload
    {
        public string dialogueId;
        public string speakerId;
    }

    [Serializable]
    public class SequenceStatePayload
    {
        public string sequenceId;
        public string state;
    }

    // ---------------- Web → Unity の封筒 ----------------
    //
    // JsonUtility はジェネリック型を扱えないので、受信側だけは type ごとに封筒クラスを作る。
    // 送信側は payload だけ JsonUtility で作り、封筒は文字列連結で組む（WebBridge.Emit）。

    // type と v だけを先に読むためのヘッダ。JsonUtility は未知のキー（payload）を無視する。
    [Serializable]
    public class EnvelopeHeader
    {
        public int v;
        public string id;
        public string type;
        public long ts;
    }

    [Serializable] public class SetPausedPayload       { public bool paused; }
    [Serializable] public class SetAudioMutedPayload   { public bool muted; }
    [Serializable] public class AdvanceDialoguePayload { public string dialogueId; public int lineIndex; public string emotion; }
    [Serializable] public class EndDialoguePayload     { public string dialogueId; }

    [Serializable] public class SetPausedEnvelope       { public int v; public string id; public string type; public SetPausedPayload payload;       public long ts; }
    [Serializable] public class SetAudioMutedEnvelope   { public int v; public string id; public string type; public SetAudioMutedPayload payload;   public long ts; }
    [Serializable] public class AdvanceDialogueEnvelope { public int v; public string id; public string type; public AdvanceDialoguePayload payload; public long ts; }
    [Serializable] public class EndDialogueEnvelope     { public int v; public string id; public string type; public EndDialoguePayload payload;     public long ts; }
}
