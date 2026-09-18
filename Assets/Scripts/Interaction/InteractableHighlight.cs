using UnityEngine;

// インタラクト可能な対象の縁取り。docs/ARCHITECTURE.md §4.2。
//
// 次のどちらかの間だけ縁取りを出す:
//   - 操作できる距離に入り、E の候補になっている（ProximityInteractor の InRangeChanged）
//   - マウスでホバーしている（PointerInteractor の HoverChanged）
//
// Interactable と同じ GameObject に付ける。対象の Renderer（子を含む）の末尾に
// highlightMaterial の複製を 1 枚足して重ねるだけで、元のマテリアルには触らない。
// 縁取りが消えているときは足したマテリアルを外す（見えないのに描画し続けないため）。
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

    [Header("Outline")]
    [SerializeField] Color outlineColor = Color.white;
    [SerializeField, Range(0f, 8f)] float outlineWidth = 2.5f;
    // 0→1 にかかるおおよその秒数。
    [SerializeField] float fadeSeconds = 0.2f;

    static readonly int OutlineColorId = Shader.PropertyToID("_OutlineColor");
    static readonly int OutlineStrengthId = Shader.PropertyToID("_OutlineStrength");
    static readonly int OutlineWidthId = Shader.PropertyToID("_OutlineWidth");

    Interactable interactable;
    Material instance;
    bool attached;
    bool hovered;
    bool inRange;
    float strength;

    void Awake() {
        interactable = GetComponent<Interactable>();
        if (targets == null || targets.Length == 0) targets = CollectRenderers();
        if (highlightMaterial != null) instance = new Material(highlightMaterial);
    }

    void OnEnable() {
        InteractionSignals.HoverChanged += OnHoverChanged;
        InteractionSignals.InRangeChanged += OnInRangeChanged;
    }

    void OnDisable() {
        InteractionSignals.HoverChanged -= OnHoverChanged;
        InteractionSignals.InRangeChanged -= OnInRangeChanged;
        hovered = false;
        inRange = false;
        strength = 0f;
        SetAttached(false);
    }

    void OnDestroy() {
        if (instance != null) Destroy(instance);
    }

    void Update() {
        if (instance == null) return;

        bool active = interactable.isActiveAndEnabled && interactable.CanActivate;
        float target = active && (hovered || inRange) ? 1f : 0f;
        float step = fadeSeconds > 0f ? Time.deltaTime / fadeSeconds : 1f;
        strength = Mathf.MoveTowards(strength, target, step);

        bool visible = strength > 0.001f;
        SetAttached(visible);
        if (!visible) return;

        instance.SetColor(OutlineColorId, outlineColor);
        instance.SetFloat(OutlineStrengthId, strength);
        instance.SetFloat(OutlineWidthId, outlineWidth);
    }

    void OnHoverChanged(Interactable target, Vector2 screenPos) {
        hovered = target == interactable;
    }

    // ProximityInteractor は対話などで止まると候補を null にするので、その間は自然に消える。
    void OnInRangeChanged(Interactable target) {
        inRange = target == interactable;
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
