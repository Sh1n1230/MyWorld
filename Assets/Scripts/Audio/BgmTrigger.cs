using UnityEngine;

// BGM リクエストの登録・取り下げを面倒みる基底クラス。
// 「いつ鳴らすか」の条件だけを派生クラスが決め、優先度やフェードの扱いはここで共通化する。
public abstract class BgmTrigger : MonoBehaviour
{
    [Header("BGM")]
    [SerializeField] protected BgmTrack track = null;

    // 大きいほど優先。ゾーンをシーン既定より手前に出したいときに上げる。
    [SerializeField] protected int priority = 0;

    // 負の値なら BgmTrack 側の defaultFadeDuration を使う。
    [SerializeField] protected float fadeInDuration = -1f;
    [SerializeField] protected float fadeOutDuration = -1f;

    bool requesting;

    protected bool IsRequesting { get { return requesting; } }

    protected void RequestBgm() {
        if (requesting) return;
        BgmManager manager = BgmManager.Instance;
        if (manager == null) return;

        manager.AddRequest(this, track, priority, fadeInDuration);
        requesting = true;
    }

    protected void ReleaseBgm() {
        if (!requesting) return;
        requesting = false;

        BgmManager manager = BgmManager.Instance;
        if (manager == null) return;

        manager.RemoveRequest(this, fadeOutDuration);
    }

    // シーンのアンロードでトリガーが消えた場合もここを通るので、リクエストは必ず取り下げられる。
    protected virtual void OnDisable() {
        ReleaseBgm();
    }
}
