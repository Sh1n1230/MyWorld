using UnityEngine;
using UnityEngine.Events;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

/// <summary>
/// PlayerChoose のキャラに付ける。クリックでそのキャラのマテリアルを
/// <see cref="CharacterSelection"/> に記録し、次のシーンへ遷移する。
/// HoverEventTrigger と同じくコライダーのある GameObject に付けること。
/// </summary>
public class CharacterChoiceButton : MonoBehaviour, IPointerClickHandler
{
    [Tooltip("選ばれたキャラの識別子。文章ではなく id（docs D5）。")]
    public string characterId;

    [Tooltip("引き継ぐマテリアル。未設定なら sourceRenderer から拾う。")]
    public Material characterMaterial;

    [Tooltip("マテリアルの取得元。未設定なら子から最初の Renderer を探す。")]
    public Renderer sourceRenderer;

    [Tooltip("遷移先シーン名。Build Settings に登録されている必要がある。")]
    public string nextSceneName = "Cafe";

    [Tooltip("クリックから遷移までの待ち時間（秒）。選択の演出を見せるための間。")]
    public float delayBeforeLoad = 0.4f;

    [Tooltip("選択が確定した瞬間に呼ばれる。SE やアニメーションのトリガに。")]
    public UnityEvent onSelected;

    static bool s_selecting;

    void Awake()
    {
        if (sourceRenderer == null) sourceRenderer = GetComponentInChildren<Renderer>();
        if (characterMaterial == null && sourceRenderer != null)
            characterMaterial = sourceRenderer.sharedMaterial;
    }

    void OnEnable()
    {
        // シーンを戻ってきたときに選択ロックが残らないようにする
        s_selecting = false;
    }

    public void OnPointerClick(PointerEventData eventData)
    {
        Select();
    }

    /// <summary>UnityEvent からも呼べる選択処理。</summary>
    public void Select()
    {
        if (s_selecting) return;

        if (characterMaterial == null)
        {
            Debug.LogWarning($"[CharacterChoiceButton] {name}: マテリアルが取れないので選択を中止した。", this);
            return;
        }

        s_selecting = true;
        CharacterSelection.Select(
            string.IsNullOrEmpty(characterId) ? name : characterId,
            characterMaterial);

        onSelected?.Invoke();

        if (string.IsNullOrEmpty(nextSceneName))
        {
            s_selecting = false;
            return;
        }

        if (delayBeforeLoad > 0f) StartCoroutine(LoadAfterDelay());
        else SceneManager.LoadScene(nextSceneName);
    }

    System.Collections.IEnumerator LoadAfterDelay()
    {
        yield return new WaitForSecondsRealtime(delayBeforeLoad);
        SceneManager.LoadScene(nextSceneName);
    }
}
