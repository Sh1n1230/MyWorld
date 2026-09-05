using UnityEngine;

// 時間帯をトリガーにする BGM。startHour 以上 endHour 未満の間だけ要求する。
// startHour > endHour の場合は日をまたぐ区間として扱う（例: 22時〜5時）。
public class BgmTimeTrigger : BgmTrigger
{
    public enum TimeSource
    {
        // 実時間。プレイヤーの端末の時計をそのまま使う。
        SystemClock,
        // シーン読み込みからの経過時間を dayLengthSeconds で 24 時間に圧縮する。
        SceneElapsed,
        // ゲーム側が ExternalHour に書き込んだ時刻を使う。独自の時間システムと繋ぐとき用。
        External,
    }

    [Header("Time")]
    [SerializeField] TimeSource timeSource = TimeSource.SystemClock;

    [SerializeField] [Range(0f, 24f)] float startHour = 18f;
    [SerializeField] [Range(0f, 24f)] float endHour = 6f;

    // SceneElapsed のときのゲーム内 1 日の実秒数。
    [SerializeField] float dayLengthSeconds = 1200f;

    // 判定の間隔（秒）。毎フレーム見る必要はないので既定は 1 秒。
    [SerializeField] float checkInterval = 1f;

    // TimeSource.External を選んだときにゲーム側から書き込む時刻（0〜24）。
    public static float ExternalHour;

    float nextCheckTime;

    void OnEnable() {
        nextCheckTime = 0f;
        Evaluate();
    }

    void Update() {
        if (Time.unscaledTime < nextCheckTime) return;
        nextCheckTime = Time.unscaledTime + Mathf.Max(0.1f, checkInterval);
        Evaluate();
    }

    void Evaluate() {
        if (IsInWindow(CurrentHour())) RequestBgm();
        else ReleaseBgm();
    }

    public float CurrentHour() {
        switch (timeSource) {
            case TimeSource.SceneElapsed:
                return Mathf.Repeat(Time.timeSinceLevelLoad / Mathf.Max(1f, dayLengthSeconds) * 24f, 24f);
            case TimeSource.External:
                return Mathf.Repeat(ExternalHour, 24f);
            default:
                return (float)System.DateTime.Now.TimeOfDay.TotalHours;
        }
    }

    bool IsInWindow(float hour) {
        // 始点と終点が同じなら 24 時間ずっと有効とみなす。
        if (Mathf.Approximately(startHour, endHour)) return true;
        if (startHour < endHour) return hour >= startHour && hour < endHour;
        return hour >= startHour || hour < endHour;
    }
}
