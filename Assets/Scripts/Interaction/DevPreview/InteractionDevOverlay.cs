#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

// Editor 実行時だけ出る、インタラクションのプレビュー表示。
//
// 製品では操作案内も台詞も Web(DOM) が描く（判断1 / D5）。これは Web 側ができるまでの間、
// Editor で当たり判定と会話の流れを目で確認するための代用品で、#if UNITY_EDITOR により
// ビルドには一切含まれない。WebBridge（docs §10.2）ができたら、このファイルは
// InteractionSignals を購読する立場を WebBridge に譲って役目を終える。
//
// 表示する文章は DevStringTable（同じく Editor 専用）から引く。
// テーブルが無ければ labelKey / id をそのまま出す。
public class InteractionDevOverlay : MonoBehaviour
{
    const int PromptFontSize = 30;
    const int BodyFontSize = 32;
    const int SpeakerFontSize = 28;
    const float PromptHeight = 0.45f;
    const float CharactersPerSecond = 40f;

    static InteractionDevOverlay instance;

    DevStringTable table;
    InputCharacter inputCharacter;
    InputAction advanceAction;

    RectTransform canvasRect;
    CanvasGroup promptGroup;
    RectTransform promptRect;
    Text promptLabel;

    GameObject dialogueRoot;
    Text speakerLabel;
    Text bodyLabel;
    Text continueLabel;

    Interactable prompted;
    readonly List<string> lines = new List<string>();
    int lineIndex;
    float revealed;
    int openedFrame = -1;
    ProximityInteractor pausedInteractor;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        instance = null;
    }

    // ProximityInteractor が Editor 実行時に呼ぶ。
    public static void EnsureExists() {
        if (instance != null) return;

        instance = FindAnyObjectByType<InteractionDevOverlay>();
        if (instance != null) return;

        GameObject go = new GameObject("InteractionDevOverlay (Editor Only)");
        instance = go.AddComponent<InteractionDevOverlay>();
    }

    bool DialogueOpen => dialogueRoot != null && dialogueRoot.activeSelf;

    void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }
        instance = this;

        table = DevStringTable.Load();
        inputCharacter = new InputCharacter();

        Build();
        promptGroup.alpha = 0f;
        dialogueRoot.SetActive(false);
    }

    void OnEnable() {
        if (inputCharacter == null) return;

        advanceAction = inputCharacter.Character.Interact;
        advanceAction.Enable();

        InteractionSignals.InRangeChanged += OnInRangeChanged;
        InteractionSignals.ObjectSelected += OnObjectSelected;
    }

    void OnDisable() {
        InteractionSignals.InRangeChanged -= OnInRangeChanged;
        InteractionSignals.ObjectSelected -= OnObjectSelected;

        if (advanceAction != null) advanceAction.Disable();
    }

    void OnDestroy() {
        if (inputCharacter != null) inputCharacter.Dispose();
        if (instance == this) instance = null;
    }

    void OnInRangeChanged(Interactable interactable) {
        prompted = interactable;
        if (prompted == null) return;

        promptLabel.text = "[E]  " + LabelFor(prompted);
    }

    void OnObjectSelected(Interactable interactable) {
        if (interactable == null) return;

        string[] preview = LinesFor(interactable);
        if (preview.Length == 0) {
            // 台詞が登録されていないものは、何が選ばれたかだけログに出す。
            // 製品ではここが OBJECT_SELECTED として Web に飛ぶ。
            Debug.Log("[InteractionDevOverlay] OBJECT_SELECTED id=" + interactable.Id, interactable);
            return;
        }

        lines.Clear();
        lines.AddRange(preview);
        lineIndex = 0;
        revealed = 0f;
        // 開いたフレームの E は「開くための入力」なので送りには使わない。
        openedFrame = Time.frameCount;

        speakerLabel.text = interactable.Id;
        ApplyReveal();
        dialogueRoot.SetActive(true);

        // 対話中はプレイヤーを止める。製品では Web からの SET_PAUSED がこの役目を持つ。
        pausedInteractor = FindAnyObjectByType<ProximityInteractor>();
        if (pausedInteractor != null) pausedInteractor.Pause();
    }

    void LateUpdate() {
        if (promptGroup == null) return;

        UpdateDialogue();

        bool showPrompt = prompted != null && !DialogueOpen;
        promptGroup.alpha = showPrompt ? 1f : 0f;
        if (showPrompt) UpdatePromptPosition();
    }

    void UpdateDialogue() {
        if (!DialogueOpen) return;

        string line = lines[lineIndex];

        if (revealed < line.Length) {
            revealed += Time.unscaledDeltaTime * CharactersPerSecond;
            ApplyReveal();
        }

        bool lineComplete = revealed >= line.Length;
        continueLabel.enabled = lineComplete && Mathf.Repeat(Time.unscaledTime, 1f) < 0.6f;

        if (Time.frameCount == openedFrame) return;
        if (advanceAction == null || !advanceAction.WasPressedThisFrame()) return;

        if (!lineComplete) {
            revealed = line.Length;
            ApplyReveal();
            return;
        }

        lineIndex++;
        if (lineIndex >= lines.Count) {
            CloseDialogue();
            return;
        }

        revealed = 0f;
        ApplyReveal();
    }

    void CloseDialogue() {
        dialogueRoot.SetActive(false);

        if (pausedInteractor != null) {
            pausedInteractor.Resume();
            pausedInteractor = null;
        }
    }

    void ApplyReveal() {
        string line = lines[lineIndex];
        int count = Mathf.Clamp(Mathf.FloorToInt(revealed), 0, line.Length);
        bodyLabel.text = line.Substring(0, count);
    }

    void UpdatePromptPosition() {
        Camera cam = Camera.main;
        if (cam == null) return;

        Vector3 world = prompted.FocusPosition + Vector3.up * PromptHeight;
        Vector3 screenPoint = cam.WorldToScreenPoint(world);
        if (screenPoint.z < 0f) {
            // カメラの後ろに回った対象は表示しない。
            promptGroup.alpha = 0f;
            return;
        }

        Vector2 local;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            canvasRect, screenPoint, null, out local);
        promptRect.anchoredPosition = local;
    }

    string LabelFor(Interactable interactable) {
        DevStringTable.Entry entry = FindEntry(interactable);
        if (entry != null && !string.IsNullOrEmpty(entry.label)) return entry.label;

        // テーブルが無いときは、Web に渡しているキーをそのまま見せる。
        return string.IsNullOrEmpty(interactable.LabelKey) ? interactable.Id : interactable.LabelKey;
    }

    string[] LinesFor(Interactable interactable) {
        DevStringTable.Entry entry = FindEntry(interactable);
        if (entry == null || entry.lines == null) return new string[0];

        List<string> result = new List<string>();
        foreach (string line in entry.lines) {
            if (!string.IsNullOrEmpty(line)) result.Add(line);
        }
        return result.ToArray();
    }

    DevStringTable.Entry FindEntry(Interactable interactable) {
        if (table == null) return null;

        DevStringTable.Entry entry = table.Find(interactable.LabelKey);
        if (entry != null) return entry;
        return table.Find(interactable.Id);
    }

    // ---- UI 構築（シーンに Canvas を用意しなくても動くよう実行時に組み立てる）----

    void Build() {
        Canvas canvas = CreateCanvas("DevOverlayCanvas", transform, 1000);
        canvasRect = canvas.GetComponent<RectTransform>();

        BuildPrompt();
        BuildDialogue();
    }

    void BuildPrompt() {
        GameObject root = new GameObject("Prompt");
        root.transform.SetParent(canvasRect, false);

        promptGroup = root.AddComponent<CanvasGroup>();
        promptGroup.interactable = false;
        promptGroup.blocksRaycasts = false;

        RectTransform rootRect = root.AddComponent<RectTransform>();
        rootRect.anchorMin = Vector2.zero;
        rootRect.anchorMax = Vector2.one;
        rootRect.offsetMin = Vector2.zero;
        rootRect.offsetMax = Vector2.zero;

        Image panel = CreatePanel(rootRect, "Panel", new Color(0f, 0f, 0f, 0.6f));
        promptRect = panel.rectTransform;
        promptRect.anchorMin = new Vector2(0.5f, 0.5f);
        promptRect.anchorMax = new Vector2(0.5f, 0.5f);
        promptRect.pivot = new Vector2(0.5f, 0.5f);

        HorizontalLayoutGroup layout = panel.gameObject.AddComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(20, 20, 10, 10);
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = false;

        ContentSizeFitter fitter = panel.gameObject.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        promptLabel = CreateText(promptRect, "Label", PromptFontSize, TextAnchor.MiddleCenter);
        // レイアウトに幅を測らせるため折り返しは切る。
        promptLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
    }

    void BuildDialogue() {
        Image panel = CreatePanel(canvasRect, "Dialogue", new Color(0.04f, 0.04f, 0.06f, 0.85f));
        dialogueRoot = panel.gameObject;

        RectTransform panelRect = panel.rectTransform;
        panelRect.anchorMin = new Vector2(0f, 0f);
        panelRect.anchorMax = new Vector2(1f, 0f);
        panelRect.pivot = new Vector2(0.5f, 0f);
        panelRect.offsetMin = new Vector2(160f, 60f);
        panelRect.offsetMax = new Vector2(-160f, 300f);

        speakerLabel = CreateText(panelRect, "Speaker", SpeakerFontSize, TextAnchor.MiddleLeft);
        speakerLabel.color = new Color(1f, 0.86f, 0.55f);
        RectTransform speakerRect = speakerLabel.rectTransform;
        speakerRect.anchorMin = new Vector2(0f, 1f);
        speakerRect.anchorMax = new Vector2(1f, 1f);
        speakerRect.pivot = new Vector2(0.5f, 1f);
        speakerRect.offsetMin = new Vector2(28f, -50f);
        speakerRect.offsetMax = new Vector2(-28f, -12f);

        bodyLabel = CreateText(panelRect, "Body", BodyFontSize, TextAnchor.UpperLeft);
        RectTransform bodyRect = bodyLabel.rectTransform;
        bodyRect.anchorMin = Vector2.zero;
        bodyRect.anchorMax = Vector2.one;
        bodyRect.offsetMin = new Vector2(28f, 44f);
        bodyRect.offsetMax = new Vector2(-28f, -58f);

        continueLabel = CreateText(panelRect, "Continue", SpeakerFontSize, TextAnchor.LowerRight);
        continueLabel.text = "▼ E";
        continueLabel.color = new Color(1f, 1f, 1f, 0.8f);
        RectTransform continueRect = continueLabel.rectTransform;
        continueRect.anchorMin = Vector2.zero;
        continueRect.anchorMax = Vector2.one;
        continueRect.offsetMin = new Vector2(28f, 10f);
        continueRect.offsetMax = new Vector2(-28f, -10f);
    }

    static Canvas CreateCanvas(string name, Transform parent, int sortingOrder) {
        GameObject root = new GameObject(name);
        root.transform.SetParent(parent, false);

        Canvas canvas = root.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = sortingOrder;

        CanvasScaler scaler = root.AddComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        return canvas;
    }

    static Image CreatePanel(Transform parent, string name, Color color) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Image image = go.AddComponent<Image>();
        image.color = color;
        image.raycastTarget = false;
        return image;
    }

    static Text CreateText(Transform parent, string name, int fontSize, TextAnchor anchor) {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);

        Text text = go.AddComponent<Text>();
        text.font = GetFont();
        text.fontSize = fontSize;
        text.alignment = anchor;
        text.color = Color.white;
        text.horizontalOverflow = HorizontalWrapMode.Wrap;
        text.verticalOverflow = VerticalWrapMode.Overflow;
        text.raycastTarget = false;

        Shadow shadow = go.AddComponent<Shadow>();
        shadow.effectColor = new Color(0f, 0f, 0f, 0.8f);
        shadow.effectDistance = new Vector2(2f, -2f);

        return text;
    }

    static Font cachedFont;

    // 日本語が出せる OS フォントを探す。Editor でしか動かないのでこれで十分。
    static Font GetFont() {
        if (cachedFont != null) return cachedFont;

        string[] candidates = {
            "Hiragino Sans", "Hiragino Kaku Gothic ProN", "YuGothic", "Yu Gothic",
            "Noto Sans CJK JP", "Noto Sans JP", "Meiryo", "MS Gothic", "Arial Unicode MS",
        };

        string[] installed = Font.GetOSInstalledFontNames();
        if (installed == null) installed = new string[0];

        foreach (string candidate in candidates) {
            foreach (string name in installed) {
                // OS によっては "Hiragino Sans W3" のように装飾が付くので前方一致で拾う。
                if (name != candidate && !name.StartsWith(candidate + " ")) continue;

                Font font = Font.CreateDynamicFontFromOSFont(name, 32);
                if (font != null) {
                    cachedFont = font;
                    return cachedFont;
                }
            }
        }

        cachedFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return cachedFont;
    }
}
#endif
