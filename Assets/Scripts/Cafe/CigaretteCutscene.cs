using System.Collections;
using UnityEngine;

namespace Portfolio.Cafe
{
    /// <summary>
    /// カフェの着火演出で使うカメラワーク。docs/ARCHITECTURE.md §6.4。
    ///
    /// カットが 1 本しかないので Cinemachine も Timeline も使わず、ここで直接補間する
    /// （SceneIntroSequence が「実行時に自前で組む」流儀なのと一貫させている）。
    /// 尺と寄りの強さは Inspector の数値で調整する。
    ///
    /// 使い方:
    ///   1. Inspector で cam（未設定なら Camera.main）と disableDuringCutscene（CameraFollow など）を繋ぐ
    ///   2. CafeSequence から FocusOn / FollowSmoke / Release を呼ぶ
    /// </summary>
    public class CigaretteCutscene : MonoBehaviour
    {
        [Header("References")]
        [Tooltip("動かすカメラ。未設定なら Camera.main。")]
        [SerializeField] Camera cam = null;

        [Tooltip("演出中だけ止めるコンポーネント。CameraFollow など、カメラを毎フレーム動かすもの。")]
        [SerializeField] Behaviour[] disableDuringCutscene = null;

        [Header("Framing")]
        [Tooltip("寄ったときの、対象から見たカメラ位置のオフセット（対象のローカル基準）。")]
        [SerializeField] Vector3 focusOffset = new Vector3(0.55f, 0.15f, 1.1f);

        [Tooltip("煙を追うときの、対象から見たカメラ位置のオフセット。")]
        [SerializeField] Vector3 smokeOffset = new Vector3(0.9f, 0.1f, 1.6f);

        [Tooltip("注視点の上下オフセット。顔を狙うときに使う。")]
        [SerializeField] float lookAtHeight = 0f;

        Coroutine running;
        Vector3 homePosition;
        Quaternion homeRotation;
        bool held;

        Camera Cam => cam != null ? cam : Camera.main;

        /// <summary>対象に寄る。duration 秒かけて補間し、そのまま保持する。</summary>
        public void FocusOn(Transform target, float duration)
        {
            Move(target, focusOffset, duration, follow: false);
        }

        /// <summary>対象（煙）を追う。duration 秒かけて引きつつ、対象を注視し続ける。</summary>
        public void FollowSmoke(Transform target, float duration)
        {
            Move(target, smokeOffset, duration, follow: true);
        }

        /// <summary>元のカメラに戻す。戻し終わると CameraFollow 等が復帰する。</summary>
        public void Release(float duration)
        {
            if (!held) return;
            Stop();
            running = StartCoroutine(ReleaseRoutine(duration));
        }

        void Move(Transform target, Vector3 offset, float duration, bool follow)
        {
            if (target == null)
            {
                Debug.LogWarning("[CigaretteCutscene] target が null です。カメラは動かしません。");
                return;
            }
            var c = Cam;
            if (c == null)
            {
                Debug.LogWarning("[CigaretteCutscene] カメラが見つかりません。");
                return;
            }

            Stop();
            Hold();
            running = StartCoroutine(MoveRoutine(c, target, offset, duration, follow));
        }

        void Hold()
        {
            if (held) return;
            var c = Cam;
            if (c == null) return;

            homePosition = c.transform.position;
            homeRotation = c.transform.rotation;
            SetHelpersEnabled(false);
            held = true;
        }

        void Stop()
        {
            if (running != null)
            {
                StopCoroutine(running);
                running = null;
            }
        }

        void SetHelpersEnabled(bool value)
        {
            if (disableDuringCutscene == null) return;
            for (int i = 0; i < disableDuringCutscene.Length; i++)
            {
                if (disableDuringCutscene[i] != null) disableDuringCutscene[i].enabled = value;
            }
        }

        static Vector3 DesiredPosition(Transform target, Vector3 offset)
        {
            // 対象の向きを基準にしたオフセット。対象が回っても構図が崩れない。
            return target.position + target.rotation * offset;
        }

        Quaternion DesiredRotation(Transform target, Vector3 from)
        {
            Vector3 lookAt = target.position + Vector3.up * lookAtHeight;
            Vector3 dir = lookAt - from;
            if (dir.sqrMagnitude < 0.0001f) return Quaternion.identity;
            return Quaternion.LookRotation(dir, Vector3.up);
        }

        IEnumerator MoveRoutine(Camera c, Transform target, Vector3 offset, float duration, bool follow)
        {
            Vector3 fromPos = c.transform.position;
            Quaternion fromRot = c.transform.rotation;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = duration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));

                Vector3 toPos = DesiredPosition(target, offset);
                c.transform.position = Vector3.Lerp(fromPos, toPos, k);
                c.transform.rotation = Quaternion.Slerp(fromRot, DesiredRotation(target, c.transform.position), k);
                yield return null;
            }

            // 補間が終わったら、追従指定のものだけ対象を見続ける。
            while (follow)
            {
                c.transform.position = DesiredPosition(target, offset);
                c.transform.rotation = DesiredRotation(target, c.transform.position);
                yield return null;
            }

            running = null;
        }

        IEnumerator ReleaseRoutine(float duration)
        {
            var c = Cam;
            if (c == null)
            {
                held = false;
                yield break;
            }

            Vector3 fromPos = c.transform.position;
            Quaternion fromRot = c.transform.rotation;

            float t = 0f;
            while (t < duration)
            {
                t += Time.deltaTime;
                float k = duration <= 0f ? 1f : Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t / duration));
                c.transform.position = Vector3.Lerp(fromPos, homePosition, k);
                c.transform.rotation = Quaternion.Slerp(fromRot, homeRotation, k);
                yield return null;
            }

            SetHelpersEnabled(true);
            held = false;
            running = null;
        }

        void OnDisable()
        {
            // 演出中にシーンが変わっても CameraFollow を止めたままにしない。
            Stop();
            if (held)
            {
                SetHelpersEnabled(true);
                held = false;
            }
        }
    }
}
