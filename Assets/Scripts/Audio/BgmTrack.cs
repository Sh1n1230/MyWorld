using UnityEngine;

// 「1 つの BGM」を表すアセット。clips に複数入れればプレイリストとして順に流れる。
// 同じ曲を複数のシーンやゾーンから参照しても音量やループ設定がばらけないよう、
// 曲そのものではなく「鳴らし方」ごとアセットにしてある。
// Project ウィンドウで Create > Audio > BGM Track から作成する。
[CreateAssetMenu(fileName = "BgmTrack", menuName = "Audio/BGM Track")]
public class BgmTrack : ScriptableObject
{
    public enum PlayMode
    {
        // clips に並べた順に再生する。
        Sequential,
        // 毎周ランダムに並べ替える。同じ曲が続けて鳴らないようにしてある。
        Shuffle,
    }

    // 1 曲だけなら要素 1 つでよい。空要素は無視される。
    public AudioClip[] clips;

    public PlayMode playMode = PlayMode.Sequential;

    // 全曲を再生し終えたら先頭に戻る。false なら鳴り終わって無音になる。
    public bool loop = true;

    // 曲と曲の間に挟む無音（秒）。
    public float gapBetweenClips = 0f;

    [Range(0f, 1f)]
    public float volume = 1f;

    // 1 曲目の再生開始位置（秒）。イントロを飛ばしたいときに使う。
    public float startTime = 0f;

    // トリガー側でフェード時間を指定しなかったときに使われる既定値（秒）。
    public float defaultFadeDuration = 1.5f;

    public bool HasClips {
        get {
            if (clips == null) return false;
            foreach (AudioClip clip in clips) {
                if (clip != null) return true;
            }
            return false;
        }
    }
}
