using UnityEngine;

/// <summary>
/// ホバー中に対象を拡大表示する。HoverEventTrigger の
/// onHoverEnter → ScaleUp / onHoverExit → ScaleDown を繋いで使う。
/// </summary>
public class HoverScaleEffect : MonoBehaviour
{
    [Tooltip("拡大する Transform。未設定ならこの GameObject。")]
    public Transform target;

    [Tooltip("ホバー中の倍率。")]
    public float hoverScale = 1.2f;

    [Tooltip("拡大／縮小にかける秒数。0 なら即座に切り替わる。")]
    public float duration = 0.15f;

    [Tooltip("補間カーブ。0→1 を想定。")]
    public AnimationCurve ease = AnimationCurve.EaseInOut(0f, 0f, 1f, 1f);

    Vector3 _baseScale;
    Coroutine _running;

    void Awake()
    {
        if (target == null) target = transform;
        _baseScale = target.localScale;
    }

    public void ScaleUp()
    {
        StartScaleTo(_baseScale * hoverScale);
    }

    public void ScaleDown()
    {
        StartScaleTo(_baseScale);
    }

    void StartScaleTo(Vector3 to)
    {
        if (!isActiveAndEnabled)
        {
            target.localScale = to;
            return;
        }

        if (_running != null) StopCoroutine(_running);

        if (duration <= 0f)
        {
            target.localScale = to;
            return;
        }

        _running = StartCoroutine(ScaleRoutine(to));
    }

    System.Collections.IEnumerator ScaleRoutine(Vector3 to)
    {
        Vector3 from = target.localScale;
        for (float t = 0f; t < duration; t += Time.unscaledDeltaTime)
        {
            target.localScale = Vector3.LerpUnclamped(from, to, ease.Evaluate(t / duration));
            yield return null;
        }
        target.localScale = to;
        _running = null;
    }

    void OnDisable()
    {
        // 途中で無効化されても元のスケールに戻しておく
        if (_running != null)
        {
            StopCoroutine(_running);
            _running = null;
        }
        if (target != null) target.localScale = _baseScale;
    }
}
