#if UNITY_EDITOR
using System.Collections;
using System.Text;
using UnityEngine;
using UnityEngine.Networking;

namespace Portfolio.Web
{
    /// <summary>
    /// **開発専用。Editor の Play モードと、開発中の Web ページを直結させる。**
    ///
    ///   Unity Editor (Play)  ──POST /unity──▶  中継  ──SSE──▶  localhost:3000/ja?editor=1
    ///                        ◀──GET /unity/poll──     ◀──POST──
    ///
    /// 中継は Web リポジトリ側の `scripts/dev-bridge.mjs`（`npm run dev:bridge`）。
    ///
    /// ---------------------------------------------------------------------------
    /// なぜ要るのか
    /// ---------------------------------------------------------------------------
    /// 本番の通信路は `.jslib` と `SendMessage` の 2 本だけで、これは**ブラウザの中に Unity が
    /// 居ること**が前提（docs/EVENT_SCHEMA.md §2）。Editor で動かしている間はブラウザが居ないので、
    /// Web 側の見た目や文言を 1 行直すたびに WebGL ビルド（実測 8 分）が要る。
    /// その 8 分を無くすためだけに、開発中だけ通る 3 本目の経路を用意する。
    ///
    /// ---------------------------------------------------------------------------
    /// 契約を壊さないための約束
    /// ---------------------------------------------------------------------------
    /// - **封筒は素通しする。** ここは WebBridge が作った JSON をそのまま運ぶだけで、
    ///   組み立ても解釈もしない。だから契約（EVENT_SCHEMA.md）の写しがここに増えない。
    /// - **WebGL ビルドには入らない。** ファイル全体が `#if UNITY_EDITOR`。
    /// - 繋ぎ先は 127.0.0.1 固定。
    ///
    /// 切りたいときは Tools → Dev Settings → Web Link（Editor ↔ Web）。
    /// </summary>
    public class EditorWebLink : MonoBehaviour
    {
        const string BaseUrl = "http://127.0.0.1:8787";
        const string PrefKey = "DevSettings.EditorWebLink";

        /// <summary>中継が居ないときに毎フレーム叩きに行かないための間隔。</summary>
        const float RetrySeconds = 3f;

        static bool linkEnabled = true;
        static bool connected;

        public static bool Enabled
        {
            get => UnityEditor.EditorPrefs.GetBool(PrefKey, true);
            set => UnityEditor.EditorPrefs.SetBool(PrefKey, value);
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        static void Bootstrap()
        {
            linkEnabled = Enabled;
            connected = false;
            if (!linkEnabled) return;

            var go = new GameObject("EditorWebLink");
            go.hideFlags = HideFlags.DontSave;   // シーンに保存されないようにする
            go.AddComponent<EditorWebLink>();
            DontDestroyOnLoad(go);
        }

        void OnEnable()
        {
            WebBridge.DevEmitted += OnEmitted;
            StartCoroutine(PollLoop());
        }

        void OnDisable()
        {
            WebBridge.DevEmitted -= OnEmitted;
        }

        // ---- Unity → Web ----

        void OnEmitted(string json)
        {
            if (!linkEnabled) return;
            StartCoroutine(PostToBridge(json));
        }

        IEnumerator PostToBridge(string json)
        {
            using (var req = new UnityWebRequest(BaseUrl + "/unity", UnityWebRequest.kHttpVerbPOST))
            {
                req.uploadHandler = new UploadHandlerRaw(Encoding.UTF8.GetBytes(json));
                req.downloadHandler = new DownloadHandlerBuffer();
                req.SetRequestHeader("Content-Type", "application/json");
                req.timeout = 5;
                yield return req.SendWebRequest();
                // 失敗しても黙る。中継が居ないだけで、Editor 単体の動作は止めない。
                // 接続状態のログは PollLoop 側で一度だけ出す。
            }
        }

        // ---- Web → Unity ----

        IEnumerator PollLoop()
        {
            while (linkEnabled)
            {
                using (var req = UnityWebRequest.Get(BaseUrl + "/unity/poll"))
                {
                    req.timeout = 10;
                    yield return req.SendWebRequest();

                    if (req.result != UnityWebRequest.Result.Success)
                    {
                        if (connected)
                        {
                            connected = false;
                            Debug.LogWarning("[EditorWebLink] 中継が切れました。npm run dev:bridge を起動してください。");
                        }
                        yield return new WaitForSecondsRealtime(RetrySeconds);
                        continue;
                    }

                    if (!connected)
                    {
                        connected = true;
                        Debug.Log("[EditorWebLink] 中継に繋がりました: " + BaseUrl
                                  + "  → ブラウザで http://localhost:3000/ja?editor=1 を開いてください。");
                    }

                    PollResponse res = null;
                    try
                    {
                        res = JsonUtility.FromJson<PollResponse>(req.downloadHandler.text);
                    }
                    catch
                    {
                        // 中継の応答が壊れていても Play を止めない。
                    }

                    if (res != null && res.messages != null)
                    {
                        foreach (var message in res.messages)
                        {
                            if (string.IsNullOrEmpty(message)) continue;
                            // **本番と同じ入口に入れる。** SendMessage 経由かどうかの違いしかない。
                            if (WebBridge.Instance != null) WebBridge.Instance.Receive(message);
                        }
                    }
                }
            }
        }

        [System.Serializable]
        class PollResponse
        {
            public string[] messages;
        }

        // ---- Editor メニュー ----

        const string MenuPath = "Tools/Dev Settings/Web Link（Editor ↔ Web）";

        [UnityEditor.MenuItem(MenuPath)]
        static void ToggleLink()
        {
            Enabled = !Enabled;
            UnityEditor.Menu.SetChecked(MenuPath, Enabled);
            Debug.Log("[EditorWebLink] " + (Enabled ? "有効にしました（次の Play から）" : "無効にしました"));
        }

        [UnityEditor.MenuItem(MenuPath, true)]
        static bool ToggleLinkValidate()
        {
            UnityEditor.Menu.SetChecked(MenuPath, Enabled);
            return true;
        }
    }
}
#endif
