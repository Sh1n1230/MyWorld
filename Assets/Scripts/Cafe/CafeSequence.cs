using System.Collections;
using UnityEngine;
using Portfolio.Web;

namespace Portfolio.Cafe
{
    /// <summary>
    /// カフェの進行を仕切る状態機械。docs/ARCHITECTURE.md §6.3 / docs/EVENT_SCHEMA.md §4。
    ///
    ///   entering → explore → focusing → (Web が対話 6〜8 行) → (男をもう一度選ぶ = 火を貸す)
    ///            → lighting → smoke → title → free
    ///
    /// 各段階で SEQUENCE_STATE を Web に送る。**Unity は Web が今どんな UI を出しているかを知らない。**
    /// 対話の進行の主導権は Web にあるので、ここは END_DIALOGUE が返ってくるまで待つ。
    ///
    /// 対話が閉じても着火しない。カメラは男に寄せたまま focusing に留まり、
    /// 訪問者が男をクリック（または E）したときに火がつく。新しい状態は増やしていない —
    /// Web は「focusing で対話が出ていない」ことから、火を貸す場面だと判断できる。
    /// focusing に入ってから解放までは、プレイヤーの移動を止める（カメラが固定されているため）。
    ///
    /// セットアップ:
    ///   - 男の SignalInteractable は「notifyWebOnSelect のチェックを外す」こと。
    ///     ここが DIALOGUE_REQUESTED を送るので、OBJECT_SELECTED と二重になる。
    ///   - このコンポーネントはカフェのシーンに 1 つ置く。
    /// </summary>
    public class CafeSequence : MonoBehaviour
    {
        [Header("Identity (docs/EVENT_SCHEMA.md §6)")]
        [SerializeField] string sequenceId = "cafe.intro";
        [SerializeField] string cigaretteInteractableId = "cafe.npc.smoker.cigarette";
        [SerializeField] string dialogueId = "cafe.intro";
        [SerializeField] string speakerId = "cafe.npc.smoker";

        [Header("References")]
        [SerializeField] CigaretteCutscene cutscene = null;
        [Tooltip("男の Interactable。着火が始まったら無効にして、光りもプロンプトも止める。")]
        [SerializeField] Interactable npcInteractable = null;
        [Tooltip("移動を止める相手。未設定ならシーンから探す。")]
        [SerializeField] ProximityInteractor player = null;
        [Tooltip("寄る対象。NPC の顔かタバコの位置に空オブジェクトを置いて指す。")]
        [SerializeField] Transform focusTarget = null;
        [Tooltip("煙を追うときの対象。パーティクルの少し上に空オブジェクトを置いて指す。")]
        [SerializeField] Transform smokeTarget = null;
        [SerializeField] CigaretteSmokeController smoke = null;
        [Tooltip("着火時に再生する NPC の Animator。未設定ならスキップする。")]
        [SerializeField] Animator npcAnimator = null;
        [SerializeField] string npcLightTrigger = "Light";

        [Header("Timing (秒)")]
        [SerializeField] float focusDuration = 0.6f;
        [SerializeField] float lightingDuration = 1.2f;
        [SerializeField] float smokeDuration = 2.5f;
        [SerializeField] float titleHold = 2.5f;
        [SerializeField] float releaseDuration = 0.8f;

        string state;
        bool started;
        bool dialogueClosed;
        bool movementLocked;

        void OnEnable()
        {
            InteractionSignals.ObjectSelected += OnObjectSelected;
            WebBridge.DialogueEnded += OnDialogueEnded;
        }

        void OnDisable()
        {
            InteractionSignals.ObjectSelected -= OnObjectSelected;
            WebBridge.DialogueEnded -= OnDialogueEnded;
        }

        void Start()
        {
            // **一度見た人には、この一連を二度やらない。** WELCOME TO MY WORLD は
            // 初回だけのものなので、再訪者は最初から free（自由行動）で始める
            // （docs/EVENT_SCHEMA.md §10）。もう一度見たい人は Web の「はじめから」。
            if (WebSession.HasSeen(sequenceId))
            {
                started = true;
                if (npcInteractable != null) npcInteractable.enabled = false;
                SetState(SequenceState.Free);
                return;
            }

            SetState(SequenceState.Entering);
        }

        void Update()
        {
            // 入店演出（SceneIntroSequence）が終わったら explore に入る。
            // DevSettings.SkipIntro が有効なときは最初から IsPlaying が false なので、そのまま通る。
            if (!started && !SceneIntroSequence.IsPlaying)
            {
                started = true;
                SetState(SequenceState.Explore);
            }
        }

        void OnObjectSelected(Interactable interactable)
        {
            if (interactable == null) return;
            if (interactable.Id != cigaretteInteractableId) return;

            if (state == SequenceState.Explore)
            {
                StartCoroutine(FocusThenAskForDialogue());
            }
            else if (state == SequenceState.Focusing && dialogueClosed)
            {
                // 対話を終えたあと、もう一度男を選んだ = 火を貸した。
                StartCoroutine(LightThenTitle());
            }
        }

        IEnumerator FocusThenAskForDialogue()
        {
            SetState(SequenceState.Focusing);
            LockMovement(true);

            if (cutscene != null && focusTarget != null) cutscene.FocusOn(focusTarget, focusDuration);
            yield return new WaitForSeconds(focusDuration);

            // ここから先の主導権は Web にある。対話の行数を知っているのは Web だけ。
            dialogueClosed = false;
            WebBridge.EmitDialogueRequested(dialogueId, speakerId);
        }

        void OnDialogueEnded(EndDialoguePayload payload)
        {
            if (payload == null) return;
            if (payload.dialogueId != dialogueId) return;
            if (state != SequenceState.Focusing) return;
            if (dialogueClosed) return;

            // ここでは着火しない。訪問者が男を選ぶのを待つ（OnObjectSelected）。
            dialogueClosed = true;
        }

        IEnumerator LightThenTitle()
        {
            // 着火。ここから先はもう選べないので、光りとプロンプトを止める。
            if (npcInteractable != null) npcInteractable.enabled = false;
            SetState(SequenceState.Lighting);
            if (npcAnimator != null && !string.IsNullOrEmpty(npcLightTrigger))
            {
                npcAnimator.SetTrigger(npcLightTrigger);
            }
            if (smoke != null) smoke.SetTipSmokeActive(true);
            yield return new WaitForSeconds(lightingDuration);

            // 煙をカメラが追う
            SetState(SequenceState.Smoke);
            if (smoke != null) smoke.StartMouthSmoke();
            if (cutscene != null && smokeTarget != null) cutscene.FollowSmoke(smokeTarget, smokeDuration);
            yield return new WaitForSeconds(smokeDuration);

            // タイトル（文字は Web が出す。Unity はタイミングだけ伝える）
            SetState(SequenceState.Title);
            yield return new WaitForSeconds(titleHold);

            // 解放
            if (cutscene != null) cutscene.Release(releaseDuration);
            yield return new WaitForSeconds(releaseDuration);
            LockMovement(false);
            SetState(SequenceState.Free);
        }

        void OnDestroy()
        {
            LockMovement(false);
        }

        void LockMovement(bool locked)
        {
            if (movementLocked == locked) return;
            if (player == null) player = FindAnyObjectByType<ProximityInteractor>();
            if (player == null) return;
            movementLocked = locked;
            if (locked) player.LockMovement();
            else player.UnlockMovement();
        }

        void SetState(string next)
        {
            if (state == next) return;
            state = next;
            WebBridge.EmitSequenceState(sequenceId, next);
        }
    }
}
