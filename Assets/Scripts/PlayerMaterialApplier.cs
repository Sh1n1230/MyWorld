using UnityEngine;

/// <summary>
/// Player.prefab に付ける。PlayerChoose で選ばれたマテリアルを起動時に反映する。
/// 未選択（Cafe から直接 Play した場合など）はプレハブ既定の見た目のまま。
/// </summary>
public class PlayerMaterialApplier : MonoBehaviour
{
    [Tooltip("差し替える Renderer。空なら子の Renderer を全部対象にする。")]
    public Renderer[] targetRenderers;

    [Tooltip("差し替えるマテリアルスロット。")]
    public int materialIndex = 0;

    void Awake()
    {
        Apply();
    }

    public void Apply()
    {
        if (!CharacterSelection.HasSelection) return;

        var renderers = (targetRenderers != null && targetRenderers.Length > 0)
            ? targetRenderers
            : GetComponentsInChildren<Renderer>(true);

        foreach (var r in renderers)
        {
            if (r == null) continue;
            var mats = r.sharedMaterials;
            if (materialIndex < 0 || materialIndex >= mats.Length) continue;
            mats[materialIndex] = CharacterSelection.Material;
            r.sharedMaterials = mats;
        }
    }
}
