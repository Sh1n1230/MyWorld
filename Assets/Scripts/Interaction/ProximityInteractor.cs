using UnityEngine;
using UnityEngine.InputSystem;

// プレイヤーに付ける。正面かつ focusRadius 以内にある Interactable を 1 つだけ候補にし、
// E で選択する。docs/ARCHITECTURE.md §5.2 / §2.3。
//
// Mobile では「近接時に出る右下の丸ボタン」の出し入れがこの候補に連動する（§2.3）。
// ボタン自体は Web 側の DOM なので、ここは INTERACTABLE_IN_RANGE を出すだけでよい。
[DefaultExecutionOrder(2)]
public class ProximityInteractor : MonoBehaviour
{
    [Header("References")]
    // 向きの基準。3rd person では見た目のメッシュだけが進行方向を向くので、
    // 未設定なら子の Visuals3rdPerson を自動で探す。
    [SerializeField] Transform facingTransform = null;
    // 距離と視線の起点。未設定なら自分の transform ＋ originHeight。
    [SerializeField] Transform originPoint = null;
    [SerializeField] float originHeight = 1.2f;
    // 一時停止中に無効化する入力スクリプトなど。SceneIntroSequence と同じ使い方。
    [SerializeField] MonoBehaviour[] disableWhilePaused = null;
    // 一時停止時に移動入力をゼロへ戻すため。未設定なら自分と親から探す。
    [SerializeField] CharacterControllerBase characterController = null;

    [Header("Detection")]
    // 正面からこの角度以内にあるものだけを候補にする（片側の角度）。
    [SerializeField, Range(5f, 180f)] float maxAngle = 70f;
    // 遠くても正面にあるものと、近いが端にあるもののどちらを優先するかの重み。
    [SerializeField, Range(0f, 1f)] float angleWeight = 0.6f;
    // 壁越しに反応しないよう視線を通す。
    // 既定は false。obstacleMask を Everything のままにすると、CAFE では椅子やテーブルが
    // 遮蔽物と判定されて手前の対象にも反応しなくなる（実測済み）。
    // 使う場合は壁だけを含む専用レイヤーを用意して obstacleMask に設定すること。
    [SerializeField] bool checkLineOfSight = false;
    [SerializeField] LayerMask obstacleMask = ~0;

    InputCharacter inputCharacter;
    InputAction interactAction;
    Interactable current;
    int pauseCount;
    int inputBlockedFrame = -1;

    // Web からの SET_PAUSED、および対話中に true になる。
    public bool IsPaused => pauseCount > 0;
    public Interactable Current => current;
    public Transform FacingTransform => facingTransform != null ? facingTransform : transform;
    public Vector3 OriginPosition =>
        originPoint != null ? originPoint.position : transform.position + Vector3.up * originHeight;

    void Awake() {
        inputCharacter = new InputCharacter();

        if (facingTransform == null) {
            Visuals3rdPerson visuals = GetComponentInChildren<Visuals3rdPerson>();
            if (visuals != null) facingTransform = visuals.transform;
        }
        if (characterController == null) {
            characterController = GetComponentInParent<CharacterControllerBase>();
        }
    }

    void OnEnable() {
        interactAction = inputCharacter.Character.Interact;
        interactAction.Enable();

        InteractableRegistry.Removed += OnInteractableRemoved;

#if UNITY_EDITOR
        // Editor 実行時だけ、Web の代わりに画面表示するプレビューを立てる（判断1 の例外ではない。
        // ビルドには含まれない）。
        InteractionDevOverlay.EnsureExists();
#endif
    }

    void OnDisable() {
        InteractableRegistry.Removed -= OnInteractableRemoved;
        SetCurrent(null);

        if (interactAction != null) interactAction.Disable();

        if (pauseCount > 0) {
            pauseCount = 0;
            ApplyPause(false);
        }
    }

    void OnDestroy() {
        inputCharacter.Dispose();
    }

    void Update() {
        if (IsPaused || SceneIntroSequence.IsPlaying) {
            // 停止中は候補を持たない。E は停止させている側（対話 UI 等）が読む。
            SetCurrent(null);
            return;
        }

        SetCurrent(InteractableRegistry.FindBestInFront(
            OriginPosition, FacingTransform.forward, maxAngle, angleWeight, PassesLineOfSight));

        // 停止が解けたフレームの E をそのまま拾うと即座に選び直してしまうので無視する。
        if (Time.frameCount == inputBlockedFrame) return;

        if (current != null && interactAction.WasPressedThisFrame()) {
            Interactable selected = current;
            InteractionSignals.RaiseObjectSelected(selected);
            selected.OnActivate();
        }
    }

    // 対話やオーバーレイ表示の間だけ移動を止める。ネストしても壊れないよう参照カウントで持つ。
    public void Pause() {
        pauseCount++;
        if (pauseCount == 1) {
            SetCurrent(null);
            ApplyPause(true);
        }
    }

    public void Resume() {
        if (pauseCount == 0) return;
        pauseCount--;
        if (pauseCount == 0) {
            ApplyPause(false);
            inputBlockedFrame = Time.frameCount;
        }
    }

    void ApplyPause(bool paused) {
        if (paused && characterController != null) {
            // 入力スクリプトを止めるだけだと最後の移動ベクトルが残り続けるので明示的にゼロにする。
            characterController.InputMoveVector(Vector3.zero);
        }

        if (disableWhilePaused == null) return;
        foreach (MonoBehaviour behaviour in disableWhilePaused) {
            if (behaviour != null) behaviour.enabled = !paused;
        }
    }

    bool PassesLineOfSight(Interactable candidate, Vector3 origin) {
        if (!checkLineOfSight) return true;

        Vector3 toTarget = candidate.FocusPosition - origin;
        float distance = toTarget.magnitude;
        if (distance < 0.01f) return true;

        RaycastHit[] hits = Physics.RaycastAll(
            origin, toTarget / distance, distance, obstacleMask, QueryTriggerInteraction.Ignore);

        foreach (RaycastHit hit in hits) {
            Transform hitTransform = hit.collider.transform;
            // 自分自身と対象本体のコライダーは遮蔽物とみなさない。
            if (hitTransform.IsChildOf(transform)) continue;
            if (hitTransform.IsChildOf(candidate.transform)) continue;
            if (characterController != null && hitTransform.IsChildOf(characterController.transform)) continue;
            return false;
        }

        return true;
    }

    void SetCurrent(Interactable next) {
        if (current == next) return;

        if (current != null) current.OnFocusExit();
        current = next;
        if (current != null) current.OnFocusEnter();

        InteractionSignals.RaiseInRangeChanged(current);
    }

    void OnInteractableRemoved(Interactable removed) {
        if (current == removed) SetCurrent(null);
    }
}
