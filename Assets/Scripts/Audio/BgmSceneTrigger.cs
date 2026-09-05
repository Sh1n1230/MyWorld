using UnityEngine;

// シーンが読み込まれている間ずっと BGM を要求し続けるトリガー。
// シーン既定の曲として使うので、priority は 0 のままにしておき、
// ゾーンや時間帯のトリガーに上書きさせるのが基本の使い方。
public class BgmSceneTrigger : BgmTrigger
{
    void OnEnable() {
        RequestBgm();
    }
}
