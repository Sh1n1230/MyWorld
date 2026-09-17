using UnityEngine;

// インタラクト可能な対象のヒント表現。docs/ARCHITECTURE.md §4.2 の L2 / L3。
//
//   L2 近接ヒント … プレイヤーが hintRadius に入ると輪郭が淡く光り、focusRadius で最大になる
//   L3 照準ヒント … マウスでホバーしている間だけ縁取りが出る
//
// Interactable と同じ GameObject に付ける。対象の Renderer（子を含む）の末尾に
// highlightMaterial の複製を 1 枚足して重ねるだけで、元のマテリアルには触らない。
// どちらも 0 のときは足したマテリアルを外す（見えないのに描画し続けないため）。
//
// Interactable が無効（CanActivate == false）の間は何も出さない。
// 演出で対象外にしたいときは Interactable を disable すればよい。
[RequireComponent(typeof(Interactable))]
public class InteractableHighlight : MonoBehaviour
{
    [Header("Material")]
    // Portfolio/InteractableHighlight シェーダーのマテリアル。
    // Shader.Find ではなく参照で持つのは、ビルドからシェーダーが落ちないようにするため。
    [SerializeField] Material highlightMaterial = null;

    [Header("Targets")]
    // 重ねる Renderer。空なら子のすべての MeshRenderer / SkinnedMeshRenderer。
    [SerializeField] Renderer[] targets = null;

    [Header("L2 Rim")]
    [SerializeField] Color rimColor = new Color(1f, 0.86f, 0.62f, 1f);
    [SerializeField, Range(0f, 1f)] float rimMax = 0.6f;

    [Header("L3 Outline")]
    [SerializeField] Color outlineColor = Color.white;
    [SerializeField, Range(0f, 8f)] float outlineWidth = 2.5f;

    [Header("Fade")]
    // 0→1 にかかるおおよその秒数。
    [SerializeField] float fadeSeconds = 0.25f;

    static readonly int RimColorId = Shader.PropertyToID("_RimColor");
    static readonly int RimStrengthId = Shader.PropertyToID("_RimStrength");
    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineStrengthId = Shader.PropertyToID("_OutlineStrength");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    Interactable interactable;
    ProximityInteractor player;
    Material instance;
    bool attached;
    bool hovered;
    float rim;
    float outline;

    void Awake() {
        interactable = GetComponent<Interactable>();
        if (targets == null || targets.Length == 0) targets = CollectRenderers();
        if (highlightMaterial != null) instance = new Material(highlightMaterial);
    }

    void OnEnable() {
        InteractionSignals.HoverChanged += OnHoverChanged;
    }

    void OnDisable() {
        InteractionSignals.HoverChanged -= OnHoverChanged;
        hovered = false;
        rim = 0f;
        outline = 0f;
        SetAttached(false);
    }

    void OnDestroy() {
        if (instance != null) Destroy(instance);
    }

    void Update() {
        if (instance == null) return;

        bool active = interactable.isActiveAndEnabled && interactable.CanActivate;
        float rimTarget = active ? rimMax * ProximityStrength() : 0f;
        float outlineTarget = active && hovered ? 1f : 0f;

        float step = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        rim = Mathf.MoveTowards(rim, rimTarget, step);
        outline = Mathf.MoveTowards(outline, outlineTarget, step);

        bool visible = rim > 0.001f || outline > 0.001f;
        SetAttached(visible);
        if (!visible) return;

        instance.SetColor(RimColorId, rimColor);
        instance.SetFloat(RimStrengthId, rim);
        instance.SetColor(OutlineColorId, outlineColor);
        instance.SetFloat(OutlineStrengthId, outline);
        instance.SetFloat(OutlineWidthId, outlineWidth);
    }

    float ProximityStrength() {
        if (player == null) {
            player = FindAnyObjectByType<ProximityInteractor>();
            if (player == null) return 0f;
        }
        // 対話などで止まっている間は光らせない。話している相手が光り続けると読む邪魔になる。
        if (player.IsPaused) return 0f;
        return interactable.HintStrength(player.OriginPosition);
    }

    void OnHoverChanged(Interactable target, Vector2 screenPos) {
        hovered = target == interactable;
    }

    void SetAttached(bool attach) {
        if (attached == attach || instance == null) return;
        attached = attach;

        foreach (Renderer r in targets) {
            if (r == null) continue;
            Material[] current = r.sharedMaterials;
            if (attach) {
                Material[] next = new Material[current.Length + 1];
                current.CopyTo(next, 0);
                next[current.Length] = instance;
                r.sharedMaterials = next;
            } else {
                int index = System.Array.IndexOf(current, instance);
                if (index < 0) continue;
                Material[] next = new Material[current.Length - 1];
                for (int i = 0, j = 0; i < current.Length; i++) {
                    if (i != index) next[j++] = current[i];
                }
                r.sharedMaterials = next;
            }
        }
    }

    Renderer[] CollectRenderers() {
        var list = new System.Collections.Generic.List<Renderer>();
        foreach (Renderer r in GetComponentsInChildren<Renderer>(true)) {
            if (r is MeshRenderer || r is SkinnedMeshRenderer) list.Add(r);
        }
        return list.ToArray();
    }
}
