#if UNITY_EDITOR
using System;
using UnityEngine;

// Editor プレビュー専用の文字列テーブル。
//
// 製品では文章は Web(DOM) が持つ（判断1 / D5）ので、これはビルドに一切含まれない。
// Web 側ができるまで Editor で会話の流れを確認するためだけのもの。
// ここに書いた文章は「正」ではない。正本は Web 側のコンテンツファイル。
[CreateAssetMenu(
    fileName = "DevStringTable",
    menuName = "Portfolio/Dev String Table (Editor Only)")]
public class DevStringTable : ScriptableObject
{
    [Serializable]
    public class Entry
    {
        // Interactable の labelKey、または id。
        public string key = "";
        // プロンプトに出す短いラベル。例: 「タバコに火をつける」
        public string label = "";
        // 選択したときに流す行。空なら対話ウィンドウは開かない。
        [TextArea(2, 5)] public string[] lines = new string[0];
    }

    [SerializeField] Entry[] entries = new Entry[0];

    public Entry Find(string key) {
        if (string.IsNullOrEmpty(key)) return null;
        for (int i = 0; i < entries.Length; i++) {
            if (entries[i] != null && entries[i].key == key) return entries[i];
        }
        return null;
    }

    // プロジェクト内の最初の 1 つを探す。無ければ null（プレビューはキー表示に落ちる）。
    public static DevStringTable Load() {
        string[] guids = UnityEditor.AssetDatabase.FindAssets("t:DevStringTable");
        if (guids.Length == 0) return null;

        string path = UnityEditor.AssetDatabase.GUIDToAssetPath(guids[0]);
        return UnityEditor.AssetDatabase.LoadAssetAtPath<DevStringTable>(path);
    }
}
#endif
