using System.Collections;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

// シーン開始時に画面を真っ暗にし、効果音を鳴らしてからフェードインする導入演出。
// オーバーレイ用の Canvas は実行時に自分で組み立てるので、シーン側に UI を用意する必要はない。
// DevSettings.SkipIntro が true のときは何も生成せず即座に自己無効化する。
public class SceneIntroSequence : MonoBehaviour
{
    [Header("Audio")]
    // 入店の効果音。未設定でも演出は成立する（無音のまま暗転して明ける）。
    [SerializeField] AudioClip introClip = null;
    [SerializeField] [Range(0f, 1f)] float introVolume = 1f;
    // 空なら実行時に AudioSource を自前で追加する。
    [SerializeField] AudioSource audioSource;

    [Header("Timing")]
    // 暗転したまま保持する秒数。
    [SerializeField] float holdBlackDuration = 2f;
    // true なら holdBlackDuration を無視して introClip の長さだけ暗転を保持する。
    [SerializeField] bool holdForClipLength = false;
    // 明るくなるまでの秒数。
    [SerializeField] float fadeDuration = 1.5f;
    [SerializeField] AnimationCurve fadeCurve = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    [Header("Overlay")]
    [SerializeField] Color overlayColor = Color.black;

    [Header("Lock")]
    // 演出中だけ無効化したいコンポーネント（入力スクリプトなど）を入れておく。
    [SerializeField] MonoBehaviour[] disableDuringIntro = null;

    [Header("Events")]
    public UnityEvent onIntroComplete;

    // 他システムから演出中かどうかを見るためのグローバルフラグ。
    public static bool IsPlaying { get; private set; }

    GameObject overlayRoot;
    CanvasGroup overlayGroup;
    bool skipped;

    // Awake で暗転させる。最初の描画は全 Awake / Start の後なので、1 フレームも明るい絵は出ない。
    void Awake() {
        IsPlaying = false;

        if (DevSettings.SkipIntro) {
            skipped = true;
            enabled = false;
            return;
        }

        BuildOverlay();
        overlayGroup.alpha = 1f;
        SetLocked(true);
        IsPlaying = true;
    }

    void Start() {
        // skipped のときは Awake で無効化済みなので普通は呼ばれないが、
        // 外から再度 enabled にされた場合に暗転なしで走り出さないよう明示的に弾く。
        if (skipped) return;
        StartCoroutine(PlayIntro());
    }

    void OnDestroy() {
        IsPlaying = false;
    }

    IEnumerator PlayIntro() {
        if (introClip != null) {
            if (audioSource == null) {
                audioSource = gameObject.AddComponent<AudioSource>();
                audioSource.playOnAwake = false;
                // 入店 SE は空間内の一点ではなく画面全体の演出なので 2D で鳴らす。
                audioSource.spatialBlend = 0f;
            }
            audioSource.PlayOneShot(introClip, introVolume);
        }

        float hold = holdForClipLength && introClip != null ? introClip.length : holdBlackDuration;
        // timeScale が 0 でも演出が進むよう、通しで unscaled time を使う。
        if (hold > 0f) yield return new WaitForSecondsRealtime(hold);

        if (fadeDuration > 0f) {
            float elapsed = 0f;
            while (elapsed < fadeDuration) {
                elapsed += Time.unscaledDeltaTime;
                overlayGroup.alpha = 1f - fadeCurve.Evaluate(Mathf.Clamp01(elapsed / fadeDuration));
                yield return null;
            }
        }

        overlayGroup.alpha = 0f;
        SetLocked(false);
        IsPlaying = false;
        Destroy(overlayRoot);

        onIntroComplete.Invoke();
    }

    void BuildOverlay() {
        overlayRoot = new GameObject("IntroOverlay");
        overlayRoot.transform.SetParent(transform, false);

        Canvas canvas = overlayRoot.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        // 他のどの UI よりも手前に出す。
        canvas.sortingOrder = short.MaxValue;
        // 暗転中のクリックを下の UI に通さないためにレイキャスタが要る。
        overlayRoot.AddComponent<GraphicRaycaster>();

        overlayGroup = overlayRoot.AddComponent<CanvasGroup>();
        overlayGroup.interactable = false;
        overlayGroup.blocksRaycasts = true;

        GameObject fill = new GameObject("Black");
        fill.transform.SetParent(overlayRoot.transform, false);
        Image image = fill.AddComponent<Image>();
        image.color = overlayColor;

        RectTransform rect = image.rectTransform;
        rect.anchorMin = Vector2.zero;
        rect.anchorMax = Vector2.one;
        rect.offsetMin = Vector2.zero;
        rect.offsetMax = Vector2.zero;
    }

    void SetLocked(bool locked) {
        if (disableDuringIntro == null) return;
        foreach (MonoBehaviour behaviour in disableDuringIntro) {
            if (behaviour != null) behaviour.enabled = !locked;
        }
    }
}
