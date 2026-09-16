using UnityEngine;
using UnityEngine.SceneManagement;
using Portfolio.Web;

/// <summary>
/// **再訪者に、もう一度キャラを選ばせないための橋渡し。docs/EVENT_SCHEMA.md §10。**
///
/// PlayerChoose のシーンに 1 つ置く。Web から RESTORE_SESSION が届いたとき、
/// その characterId と一致する <see cref="CharacterChoiceButton"/> を探して、
/// 人が押したのと同じ経路で押す。だから「選択の記録 → マテリアル引き継ぎ →
/// CHARACTER_SELECTED の送信 → カフェへの遷移」は 1 本のままで、ここで枝分かれしない。
///
/// characterId が空（＝初回、または「はじめから」）なら何もしない。**「はじめから」で
/// カフェから戻ってくる経路は Unity 側に作らない** — Web が localStorage を消して
/// ページを読み直せば、Unity も最初から立ち上がるので、そちらの方が経路が 1 本で済む。
/// その代償は Unity の再ロード（20〜60 秒）だが、めったに押されない操作なので引き受ける。
/// ここで「Web からシーンを切り替える」手段を作ると、意図的に落とした GOTO_SCENE が
/// 名前を変えて戻ってくることになる（docs/EVENT_SCHEMA.md §5）。
///
/// Web がいない Editor 単体では RESTORE_SESSION が来ないので、何も起きない
/// （＝いつもどおり自分で選ぶ）。
/// </summary>
public class CharacterAutoSelect : MonoBehaviour
{
    [Tooltip("自動選択のときは選択演出の間を詰める。0 なら即遷移。")]
    [SerializeField] float delayBeforeLoad = 0f;

    void OnEnable()
    {
        WebSession.Restored += Apply;
        // 購読より先に届いていることがある（RESTORE_SESSION は BRIDGE_READY の直後に来る）。
        if (WebSession.Received) Apply();
    }

    void OnDisable()
    {
        WebSession.Restored -= Apply;
    }

    void Apply()
    {
        var characterId = WebSession.CharacterId;
        if (string.IsNullOrEmpty(characterId)) return;   // 初回 / はじめから。自分で選んでもらう。
        if (CharacterSelection.HasSelection) return;     // もう選び終わっている。

        var buttons = FindObjectsByType<CharacterChoiceButton>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        foreach (var button in buttons)
        {
            var id = string.IsNullOrEmpty(button.characterId) ? button.name : button.characterId;
            if (id != characterId) continue;

            button.delayBeforeLoad = delayBeforeLoad;
            button.Select();
            return;
        }

        // 見つからないのは、Web に古い id が残っているとき（キャラを差し替えた後など）。
        // 止めずに、いつもどおり選んでもらう。
        Debug.LogWarning("[CharacterAutoSelect] 一致するキャラが無いので自動選択しません: " + characterId);
    }
}
