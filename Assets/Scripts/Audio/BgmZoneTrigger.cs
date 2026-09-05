using System.Collections.Generic;
using UnityEngine;

// 空間をトリガーにする BGM。Collider（isTrigger）の中にプレイヤーが入っている間だけ要求する。
// シーン既定の BgmSceneTrigger より priority を高くしておけば、
// 「入ったら店内BGM、出たら元のBGMに戻る」が自動で成立する。
[RequireComponent(typeof(Collider))]
public class BgmZoneTrigger : BgmTrigger
{
    [Header("Zone")]
    // 空ならタグ判定を行わない。プロジェクトにまだ Player タグが無いので既定は空。
    [SerializeField] string targetTag = "";

    // CharacterControllerBase を持つオブジェクトだけを受け付ける。
    // タグを切らなくてもプレイヤーだけ拾えるので、既定で有効にしてある。
    [SerializeField] bool requireCharacterController = true;

    // キャラが複数のコライダーを持っていても出入り判定が壊れないよう、実体を数える。
    readonly HashSet<Collider> occupants = new HashSet<Collider>();

    void Reset() {
        Collider collider = GetComponent<Collider>();
        if (collider != null) collider.isTrigger = true;
    }

    void OnTriggerEnter(Collider other) {
        if (!Accepts(other)) return;
        if (occupants.Add(other) && occupants.Count == 1) RequestBgm();
    }

    void OnTriggerExit(Collider other) {
        if (!occupants.Remove(other)) return;
        // ゾーン内で破棄されたコライダーが残っていると出たまま解除されないので掃除する。
        occupants.RemoveWhere(c => c == null);
        if (occupants.Count == 0) ReleaseBgm();
    }

    protected override void OnDisable() {
        occupants.Clear();
        base.OnDisable();
    }

    bool Accepts(Collider other) {
        if (!string.IsNullOrEmpty(targetTag) && !other.CompareTag(targetTag)) return false;
        if (requireCharacterController && other.GetComponentInParent<CharacterControllerBase>() == null) return false;
        return true;
    }
}
