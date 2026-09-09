using System.Collections;
using UnityEngine;
using Portfolio.Web;

namespace Portfolio.Cafe
{
    /// <summary>
    /// カフェの進行を仕切る状態機械。docs/ARCHITECTURE.md §6.3 / docs/EVENT_SCHEMA.md §4。
    ///
    ///   entering → explore → focusing → (Web が対話 6〜8 行) → lighting → smoke → title → free
    ///
    /// 各段階で SEQUENCE_STATE を Web に送る。**Unity は Web が今どんな UI を出しているかを知らない。**
    /// 対話の進行の主導権は Web にあるので、ここは END_DIALOGUE が返ってくるまで待つ。
    ///
    /// セットアップ:
    ///   - タバコの SignalInteractable は「notifyWebOnSelect のチェックを外す」こと。
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
            if (state != SequenceState.Explore) return;   // 二度目以降は無視する。

            StartCoroutine(FocusThenAskForDialogue());
        }

        IEnumerator FocusThenAskForDialogue()
        {
            SetState(SequenceState.Focusing);

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

            dialogueClosed = true;
            StartCoroutine(LightThenTitle());
        }

        IEnumerator LightThenTitle()
        {
            // 着火
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
            SetState(SequenceState.Free);
        }

        void SetState(string next)
        {
            if (state == next) return;
            state = next;
            WebBridge.EmitSequenceState(sequenceId, next);
        }
    }
}
