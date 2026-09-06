using UnityEngine;
using UnityEngine.InputSystem;

// PC 用。マウス位置からレイキャストして hover を追い、左クリックで選択する。
// docs/ARCHITECTURE.md §2.3（インタラクト = ホバー中の左クリック または E）/ §5.2。
//
// ProximityInteractor と併用する前提。こちらは「見えているものをクリックする」経路で、
// 向こうは「近づいて E」の経路。どちらも同じ InteractionSignals に流れる。
//
// 注意: この経路は対象にコライダーが必要（ProximityInteractor は不要）。
[DefaultExecutionOrder(2)]
public class PointerInteractor : MonoBehaviour
{
    [Header("References")]
    // レイを飛ばすカメラ。未設定なら Camera.main。
    [SerializeField] Camera targetCamera = null;
    // 距離判定の起点。プレイヤー側の基準点を入れる。未設定なら自分の transform。
    [SerializeField] Transform playerOrigin = null;
    // 選択の許可を共有する相手。停止中はこちらも止める。未設定なら自動で探す。
    [SerializeField] ProximityInteractor proximityInteractor = null;

    [Header("Raycast")]
    [SerializeField] float maxRayDistance = 30f;
    [SerializeField] LayerMask rayMask = ~0;
    // 遠くの対象にホバーはできるが、選択は focusRadius 以内に限る。
    [SerializeField] bool requireFocusRadiusToClick = true;

    Interactable hovered;

    public Interactable Hovered => hovered;

    void Awake() {
        if (proximityInteractor == null) proximityInteractor = GetComponentInParent<ProximityInteractor>();
    }

    void OnEnable() {
        InteractableRegistry.Removed += OnInteractableRemoved;
    }

    void OnDisable() {
        InteractableRegistry.Removed -= OnInteractableRemoved;
        SetHovered(null, Vector2.zero);
    }

    void Update() {
        Mouse mouse = Mouse.current;
        // タッチ端末やゲームパッドのみの環境ではこの経路は無効。ProximityInteractor が担当する。
        if (mouse == null) return;

        if ((proximityInteractor != null && proximityInteractor.IsPaused) || SceneIntroSequence.IsPlaying) {
            SetHovered(null, Vector2.zero);
            return;
        }

        Camera cam = targetCamera != null ? targetCamera : Camera.main;
        if (cam == null) return;

        Vector2 screenPos = mouse.position.ReadValue();
        Interactable found = Raycast(cam, screenPos);
        SetHovered(found, screenPos);

        if (hovered == null || !mouse.leftButton.wasPressedThisFrame) return;
        if (requireFocusRadiusToClick && !IsWithinFocusRadius(hovered)) return;

        Interactable selected = hovered;
        InteractionSignals.RaiseObjectSelected(selected);
        selected.OnActivate();
    }

    Interactable Raycast(Camera cam, Vector2 screenPos) {
        Ray ray = cam.ScreenPointToRay(screenPos);

        RaycastHit hit;
        if (!Physics.Raycast(ray, out hit, maxRayDistance, rayMask, QueryTriggerInteraction.Collide)) {
            return null;
        }

        // 子のコライダーに当たっても親の Interactable を拾えるようにする。
        Interactable interactable = hit.collider.GetComponentInParent<Interactable>();
        if (interactable == null || !interactable.CanActivate) return null;
        return interactable;
    }

    bool IsWithinFocusRadius(Interactable interactable) {
        Transform origin = playerOrigin != null ? playerOrigin : transform;
        return Vector3.Distance(origin.position, interactable.FocusPosition) <= interactable.FocusRadius;
    }

    void SetHovered(Interactable next, Vector2 screenPos) {
        if (hovered == next) {
            // 同じ対象でもカーソルは動くので、位置だけは更新して送り続ける。
            if (hovered != null) InteractionSignals.RaiseHoverChanged(hovered, screenPos);
            return;
        }

        if (hovered != null) hovered.OnFocusExit();
        hovered = next;
        if (hovered != null) hovered.OnFocusEnter();

        InteractionSignals.RaiseHoverChanged(hovered, screenPos);
    }

    void OnInteractableRemoved(Interactable removed) {
        if (hovered == removed) SetHovered(null, Vector2.zero);
    }
}
