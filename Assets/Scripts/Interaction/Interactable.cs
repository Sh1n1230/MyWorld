using UnityEngine;

// ワールド内で選択できるものの基底クラス。docs/ARCHITECTURE.md §5.3 の契約に従う。
//
// このクラスは「文字」を一切持たない（判断1 / D5）。持つのは id と labelKey だけで、
// 実際に表示する文章は Web 側（DOM）が labelKey から引く。
public abstract class Interactable : MonoBehaviour
{
    [Header("Identity")]
    // Web と共有する一意な ID。命名規約は docs §9.3。例: "cafe.npc.smoker.cigarette"
    [SerializeField] string id = "";
    // 表示ラベルの「キー」。文字列そのものではない。例: "cafe.cigarette.label"
    [SerializeField] string labelKey = "";

    [Header("Radii")]
    // ここまで近づくとヒント表現（発光）が立ち上がる。docs §2.2 の L2。
    [SerializeField] float hintRadius = 4f;
    // ここまで近づくと操作できる。docs §2.2 の L3。
    [SerializeField] float focusRadius = 2f;

    [Header("Anchor")]
    // 距離・視線・画面座標の基準点。未設定なら自分の transform。
    [SerializeField] Transform focusPoint = null;

    public string Id => string.IsNullOrEmpty(id) ? name : id;
    public string LabelKey => labelKey;
    public float HintRadius => hintRadius;
    public float FocusRadius => focusRadius;
    public Transform FocusPoint => focusPoint != null ? focusPoint : transform;
    public Vector3 FocusPosition => FocusPoint.position;

    // 今このオブジェクトを選択できるか。着火済みで対象外にしたい場合などに override する。
    public virtual bool CanActivate => isActiveAndEnabled;

    // 実際の演出。Interactor から呼ばれる。
    public abstract void OnActivate();

    // ヒント表現の強さ。hintRadius で 0、focusRadius 以内で 1。
    // HighlightController（未実装）が発光量に使う想定。
    public float HintStrength(Vector3 from) {
        if (hintRadius <= focusRadius) return 0f;
        float distance = Vector3.Distance(from, FocusPosition);
        return Mathf.Clamp01(Mathf.InverseLerp(hintRadius, focusRadius, distance));
    }

    // 候補になった / 外れたときの通知。ハイライト演出のフックとして用意しておく。
    public virtual void OnFocusEnter() { }
    public virtual void OnFocusExit() { }

    protected virtual void OnEnable() {
        InteractableRegistry.Register(this);
    }

    protected virtual void OnDisable() {
        InteractableRegistry.Unregister(this);
    }

    protected virtual void OnDrawGizmosSelected() {
        Gizmos.color = new Color(0.3f, 0.9f, 1f, 0.35f);
        Gizmos.DrawWireSphere(FocusPosition, hintRadius);
        Gizmos.color = new Color(0.3f, 1f, 0.5f, 0.7f);
        Gizmos.DrawWireSphere(FocusPosition, focusRadius);
    }
}
