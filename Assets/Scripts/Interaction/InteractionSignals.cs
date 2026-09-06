using System;
using UnityEngine;

// Interactor が出す通知の集約点。docs/ARCHITECTURE.md §7.3 のイベントと 1:1 で対応する。
//
// ここはまだ Unity 内部のイベントでしかない。WebBridge（§10.2、未実装）ができたら、
// WebBridge がこの 3 つを購読して Envelope に詰め替えて Web に送る。
// Interactor 側は「誰が聞いているか」を知らない — これが疎結合を保つ鍵（§5.4）。
public static class InteractionSignals
{
    // HOVER_CHANGED { id, labelKey, screenPos }。外れたときは interactable が null。
    public static event Action<Interactable, Vector2> HoverChanged;
    // INTERACTABLE_IN_RANGE { id, labelKey }。範囲外に出たときは null。
    public static event Action<Interactable> InRangeChanged;
    // OBJECT_SELECTED { id, kind }。実際に選択されたとき。
    public static event Action<Interactable> ObjectSelected;

    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        HoverChanged = null;
        InRangeChanged = null;
        ObjectSelected = null;
    }

    public static void RaiseHoverChanged(Interactable interactable, Vector2 screenPos) {
        if (HoverChanged != null) HoverChanged(interactable, screenPos);
    }

    public static void RaiseInRangeChanged(Interactable interactable) {
        if (InRangeChanged != null) InRangeChanged(interactable);
    }

    public static void RaiseObjectSelected(Interactable interactable) {
        if (ObjectSelected != null) ObjectSelected(interactable);
    }
}
