using System.Runtime.InteropServices;
using UnityEngine;

// ホバー中はカーソルを手の形にする。docs/ARCHITECTURE.md §4.2 の L3。
//
// WebGL では canvas の CSS の cursor を切り替える（PortfolioBridge.jslib）。OS のカーソルを
// そのまま使うので、画像を持たずに済み、Web ページのリンクと同じ見た目になる。
// Web には何も送らない — HOVER_CHANGED は作らないと決めてある（EVENT_SCHEMA.md §5）。
// Editor では何もしない。
public static class HoverCursor
{
#if UNITY_WEBGL && !UNITY_EDITOR
    [DllImport("__Internal")]
    static extern void PortfolioSetPointerCursor(int pointer);
#endif

    static bool pointer;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Install() {
        pointer = false;
        InteractionSignals.HoverChanged += OnHoverChanged;
    }

    static void OnHoverChanged(Interactable target, Vector2 screenPos) {
        bool next = target != null;
        if (next == pointer) return;
        pointer = next;
#if UNITY_WEBGL && !UNITY_EDITOR
        PortfolioSetPointerCursor(pointer ? 1 : 0);
#endif
    }
}
