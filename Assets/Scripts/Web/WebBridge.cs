using System;
using System.Runtime.InteropServices;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace Portfolio.Web
{
    /// <summary>
    /// Unity ↔ Web の唯一の窓口。契約は docs/EVENT_SCHEMA.md。
    ///
    /// Web → Unity は SendMessage("WebBridge", "Receive", json) の 1 経路だけ。
    /// 受け口を type ごとに増やすと GameObject 名とメソッド名の管理が破綻するので、
    /// 封筒（Envelope）に包んでこの 1 つで受ける。
    ///
    /// Unity → Web は .jslib の PortfolioEmit → window の CustomEvent("unity:message")。
    ///
    /// 重要な約束:
    ///   - 未知の type は warn して無視する。**絶対に例外を投げない**（前方互換のため）。
    ///   - このクラスは文字を一切持たない（D1）。持つのは id と labelKey だけ。
    ///   - Web が今どんな UI を出しているかを Unity は知らない。
    /// </summary>
    [DefaultExecutionOrder(-100)]
    public class WebBridge : MonoBehaviour
    {
        // SendMessage の宛先になるので、この名前は Web 側と一致していなければならない。
        public const string GameObjectName = "WebBridge";
        public const int ProtocolVersion = 1;

#if UNITY_WEBGL && !UNITY_EDITOR
        [DllImport("__Internal")]
        static extern void PortfolioEmit(string json);
#endif

        public static WebBridge Instance { get; private set; }

        // ---- Web → Unity の通知。CafeSequence など、演出側がここを購読する ----

        /// <summary>ADVANCE_DIALOGUE。進行の主導権は Web 側にある（行数を知っているのは Web だから）。</summary>
        public static event Action<AdvanceDialoguePayload> DialogueAdvanced;
        /// <summary>END_DIALOGUE。Unity はこれを受けて次の演出（着火など）へ進む。</summary>
        public static event Action<EndDialoguePayload> DialogueEnded;
        /// <summary>SET_PAUSED。ポーズの実体は ApplyPaused が処理するので、通常は購読不要。</summary>
        public static event Action<bool> PausedChanged;
        /// <summary>SET_AUDIO_MUTED。</summary>
        public static event Action<bool> AudioMutedChanged;

#if !UNITY_WEBGL || UNITY_EDITOR
        /// <summary>
        /// **開発専用の出口。** Editor で Play しているときに、本来 .jslib が受け取るはずの封筒を
        /// そのまま渡す。EditorWebLink がこれを購読して中継（sh1n1230.dev/scripts/dev-bridge.mjs）へ流し、
        /// 開発中の Web ページと直結させる。WebGL ビルドにはこの経路は存在しない。
        /// </summary>
        public static event Action<string> DevEmitted;
#endif

        bool paused;
        bool muted;
        // BeforeSceneLoad で生成されるため最初のシーンでは sceneLoaded も Start も両方走る。
        // 同じ SCENE_READY を二重に送らないための番人。
        string lastSceneEmitted;

        public bool IsPaused => paused;
        public bool IsMuted => muted;

        // Enter Play Mode の domain reload を切っていても、前回の購読者が残らないようにする。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            DialogueAdvanced = null;
            DialogueEnded = null;
            PausedChanged = null;
            AudioMutedChanged = null;
#if !UNITY_WEBGL || UNITY_EDITOR
            DevEmitted = null;
#endif
            Instance = null;
        }

        // シーンに置き忘れても必ず存在するように、起動時に自分で作る。
        // 名前が SendMessage の宛先そのものなので、手で置くと typo で無反応になり得る。
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        static void Bootstrap()
        {
            if (Instance != null) return;
            var go = new GameObject(GameObjectName);
            go.AddComponent<WebBridge>();
            DontDestroyOnLoad(go);
        }

        void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }
            Instance = this;
            DontDestroyOnLoad(gameObject);

#if UNITY_WEBGL && !UNITY_EDITOR
            // 既定は音声 OFF（docs/ARCHITECTURE.md §3.4）。Web が SET_AUDIO_MUTED で解除する。
            muted = true;
#else
            // Editor / 非 WebGL には解除してくれる Web がいないので鳴らしておく。
            muted = false;
#endif
            AudioListener.volume = muted ? 0f : 1f;

            InteractionSignals.InRangeChanged += OnInRangeChanged;
            InteractionSignals.ObjectSelected += OnObjectSelected;
            // HoverChanged は購読しない。HOVER_CHANGED イベントは作らないと決めてある
            // （PC の hover は Unity 内のアウトライン表現だけで足りる）。
            CharacterSelection.Selected += OnCharacterSelected;
            SceneManager.sceneLoaded += OnSceneLoaded;
        }

        void OnDestroy()
        {
            InteractionSignals.InRangeChanged -= OnInRangeChanged;
            InteractionSignals.ObjectSelected -= OnObjectSelected;
            CharacterSelection.Selected -= OnCharacterSelected;
            SceneManager.sceneLoaded -= OnSceneLoaded;
            if (Instance == this) Instance = null;
        }

        void Start()
        {
            Emit(WebEventType.BridgeReady, new BridgeReadyPayload {
                protocolVersions = new[] { ProtocolVersion },
                unityVersion = Application.unityVersion,
            });

            // 生成タイミングによっては最初のシーンの sceneLoaded を取り逃すので、
            // Start 時点で読み込まれているシーンも通知する（重複は EmitSceneReady が弾く）。
            EmitSceneReady(SceneManager.GetActiveScene().name);
        }

        // ------------------------------------------------------------------
        // Unity → Web
        // ------------------------------------------------------------------

        /// <summary>
        /// 封筒に包んで Web へ送る。payload は [Serializable] なクラスであること。
        /// JsonUtility はジェネリックを扱えないので、封筒は文字列連結で組む
        /// （type と id はこちらが生成する ASCII なのでエスケープ不要。payload は JsonUtility 任せ）。
        /// </summary>
        public void Emit(string type, object payload)
        {
            string payloadJson = payload != null ? JsonUtility.ToJson(payload) : "{}";
            long ts = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
            string json =
                "{\"v\":" + ProtocolVersion +
                ",\"id\":\"" + Guid.NewGuid().ToString("N") + "\"" +
                ",\"type\":\"" + type + "\"" +
                ",\"payload\":" + payloadJson +
                ",\"ts\":" + ts + "}";

#if UNITY_WEBGL && !UNITY_EDITOR
            PortfolioEmit(json);
#else
            // エディタと非 WebGL ビルドでは .jslib が無いので、配線の確認用にログへ出す。
            Debug.Log("[WebBridge] → Web  " + json);
            // 開発専用リンク（EditorWebLink）がここを拾って中継に流す。
            // ログと違い、こちらは実際に Web の DOM まで届く。
            if (DevEmitted != null) DevEmitted(json);
#endif
        }

        static void EmitStatic(string type, object payload)
        {
            if (Instance == null)
            {
                Debug.LogWarning("[WebBridge] Instance がありません。" + type + " を送れませんでした。");
                return;
            }
            Instance.Emit(type, payload);
        }

        void OnSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            EmitSceneReady(scene.name);
        }

        void EmitSceneReady(string sceneName)
        {
            string id = ToWebSceneId(sceneName);
            if (id == null) return;   // Village / Factory など、Web が知らないシーンは通知しない。
            if (id == lastSceneEmitted) return;
            lastSceneEmitted = id;
            Emit(WebEventType.SceneReady, new SceneReadyPayload { scene = id });
        }

        static string ToWebSceneId(string sceneName)
        {
            if (sceneName == "PlayerChoose") return WebSceneId.PlayerChoose;
            if (sceneName == "Cafe") return WebSceneId.Cafe;
            return null;
        }

        void OnInRangeChanged(Interactable interactable)
        {
            Emit(WebEventType.InteractableInRange, new InteractableInRangePayload {
                id = interactable != null ? interactable.Id : null,
                labelKey = interactable != null ? interactable.LabelKey : null,
            });
        }

        void OnObjectSelected(Interactable interactable)
        {
            if (interactable == null) return;
            // 選択後の進行を Unity 側の演出が引き取るものは、代わりに DIALOGUE_REQUESTED を送る。
            // 両方送ると Web 側が二重に反応する。docs/EVENT_SCHEMA.md §4。
            if (!interactable.NotifyWebOnSelect) return;
            Emit(WebEventType.ObjectSelected, new ObjectSelectedPayload { id = interactable.Id });
        }

        void OnCharacterSelected(string characterId)
        {
            Emit(WebEventType.CharacterSelected, new CharacterSelectedPayload { characterId = characterId });
        }

        /// <summary>演出（CafeSequence）が特定の対話を要求する。</summary>
        public static void EmitDialogueRequested(string dialogueId, string speakerId)
        {
            EmitStatic(WebEventType.DialogueRequested, new DialogueRequestedPayload {
                dialogueId = dialogueId,
                speakerId = speakerId,
            });
        }

        /// <summary>演出の進行段階を通知する。値は SequenceState を使うこと。</summary>
        public static void EmitSequenceState(string sequenceId, string state)
        {
            EmitStatic(WebEventType.SequenceState, new SequenceStatePayload {
                sequenceId = sequenceId,
                state = state,
            });
        }

        // ------------------------------------------------------------------
        // Web → Unity
        // ------------------------------------------------------------------

        /// <summary>
        /// SendMessage("WebBridge", "Receive", json) の受け口。**これ 1 つだけ。**
        /// 何が来ても例外を投げないこと。落ちると Web 側から復旧できない。
        /// </summary>
        public void Receive(string json)
        {
            EnvelopeHeader header;
            try
            {
                header = JsonUtility.FromJson<EnvelopeHeader>(json);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[WebBridge] 封筒を解釈できませんでした: " + e.Message + " / " + json);
                return;
            }

            if (header == null || string.IsNullOrEmpty(header.type))
            {
                Debug.LogWarning("[WebBridge] type の無いメッセージを無視しました: " + json);
                return;
            }

            if (header.v != ProtocolVersion)
            {
                Debug.LogWarning("[WebBridge] プロトコルバージョン不一致 (received=" + header.v +
                                 ", expected=" + ProtocolVersion + ")。Unity の再ビルドが必要かもしれません。");
                // バージョンが違っても処理は試みる。破壊的変更のときだけ v が上がる約束なので、
                // 止めるより「動くところまで動かす」方が復旧しやすい。
            }

            try
            {
                Dispatch(header.type, json);
            }
            catch (Exception e)
            {
                // ここで throw すると .jslib の呼び出し元まで巻き込む。必ず握り潰す。
                Debug.LogWarning("[WebBridge] " + header.type + " の処理で例外: " + e);
            }
        }

        void Dispatch(string type, string json)
        {
            switch (type)
            {
                case WebEventType.SetPaused:
                {
                    var env = JsonUtility.FromJson<SetPausedEnvelope>(json);
                    if (env == null || env.payload == null) return;
                    ApplyPaused(env.payload.paused);
                    return;
                }
                case WebEventType.SetAudioMuted:
                {
                    var env = JsonUtility.FromJson<SetAudioMutedEnvelope>(json);
                    if (env == null || env.payload == null) return;
                    ApplyAudioMuted(env.payload.muted);
                    return;
                }
                case WebEventType.AdvanceDialogue:
                {
                    var env = JsonUtility.FromJson<AdvanceDialogueEnvelope>(json);
                    if (env == null || env.payload == null) return;
                    if (DialogueAdvanced != null) DialogueAdvanced(env.payload);
                    return;
                }
                case WebEventType.EndDialogue:
                {
                    var env = JsonUtility.FromJson<EndDialogueEnvelope>(json);
                    if (env == null || env.payload == null) return;
                    if (DialogueEnded != null) DialogueEnded(env.payload);
                    return;
                }
                default:
                    // 前方互換。Web が新しいイベントを送ってきても Unity は落ちない。
                    Debug.LogWarning("[WebBridge] 未知の type を無視しました: " + type);
                    return;
            }
        }

        // ------------------------------------------------------------------
        // 適用
        // ------------------------------------------------------------------

        void ApplyPaused(bool value)
        {
            if (paused == value) return;   // 二重 Pause を防ぐ（ProximityInteractor は参照カウント式）。
            paused = value;

            var interactors = FindObjectsByType<ProximityInteractor>();
            for (int i = 0; i < interactors.Length; i++)
            {
                if (value) interactors[i].Pause();
                else interactors[i].Resume();
            }

            if (PausedChanged != null) PausedChanged(value);
        }

        void ApplyAudioMuted(bool value)
        {
            muted = value;
            AudioListener.volume = value ? 0f : 1f;
            if (AudioMutedChanged != null) AudioMutedChanged(value);
        }
    }
}
