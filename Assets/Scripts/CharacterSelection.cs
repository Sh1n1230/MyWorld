using UnityEngine;

/// <summary>
/// PlayerChoose で選んだキャラの見た目をシーンをまたいで保持する。
/// static なのでシーン遷移でも生き残る（Material はアセット参照なのでアンロードされない）。
/// </summary>
public static class CharacterSelection
{
    /// <summary>選択されたキャラの識別子。docs D5 に従い表示文字列ではなく id。</summary>
    public static string Id { get; private set; }

    /// <summary>選択されたキャラのマテリアル。未選択なら null。</summary>
    public static Material Material { get; private set; }

    public static bool HasSelection => Material != null;

    public static void Select(string id, Material material)
    {
        Id = id;
        Material = material;
    }

    public static void Clear()
    {
        Id = null;
        Material = null;
    }
}
