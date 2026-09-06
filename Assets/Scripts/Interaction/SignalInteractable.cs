using UnityEngine;
using UnityEngine.Events;

// 一番単純な Interactable。選択されたことを通知して、Inspector で繋いだ処理を呼ぶだけ。
//
// 台詞や説明文はここに持たない（判断1 / D5）。何を喋るかは Web が id / labelKey から決める。
// CAFE のタバコのように固有演出があるものは、これではなく Interactable を直接継承して
// Assets/Scripts/Cafe/ に置く（docs §5.2 / §10.5）。
public class SignalInteractable : Interactable
{
    [Header("Activation")]
    // 一度きりにする（着火など、二度目が無いもの）。
    [SerializeField] bool oneShot = false;
    // 着火演出・アニメーション再生など、シーン側で繋ぐ処理。
    public UnityEvent onActivate;

    bool activated;

    public bool Activated => activated;

    public override bool CanActivate {
        get {
            if (!base.CanActivate) return false;
            return !(oneShot && activated);
        }
    }

    public override void OnActivate() {
        activated = true;
        onActivate.Invoke();
    }
}
