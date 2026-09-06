using System;
using System.Collections.Generic;
using UnityEngine;

// シーン内の Interactable 一覧を保持する。docs/ARCHITECTURE.md §5.2。
//
// Interactor は毎フレーム Physics クエリを投げる代わりにこのリストを走査する。
// 対象にコライダーが要らない（ProximityInteractor の場合）のはこのため。
public static class InteractableRegistry
{
    static readonly List<Interactable> items = new List<Interactable>();

    public static IReadOnlyList<Interactable> All => items;

    // 対象が無効化・破棄されたときの通知。Interactor が注目中の参照を落とすために使う。
    public static event Action<Interactable> Removed;

    // ドメインリロードを切っていても Play のたびに空から始められるようにする。
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics() {
        items.Clear();
        Removed = null;
    }

    public static void Register(Interactable interactable) {
        if (interactable == null || items.Contains(interactable)) return;
        items.Add(interactable);
    }

    public static void Unregister(Interactable interactable) {
        if (interactable == null) return;
        if (!items.Remove(interactable)) return;
        if (Removed != null) Removed(interactable);
    }

    public static Interactable FindById(string id) {
        if (string.IsNullOrEmpty(id)) return null;
        for (int i = 0; i < items.Count; i++) {
            if (items[i] != null && items[i].Id == id) return items[i];
        }
        return null;
    }

    // origin から forward 方向の扇形の中で、最も「正面かつ近い」ものを返す。
    // angleWeight を上げると正面にあるものを、下げると近いものを優先する。
    public static Interactable FindBestInFront(
        Vector3 origin, Vector3 forward, float maxAngle, float angleWeight,
        Func<Interactable, Vector3, bool> extraFilter) {

        Vector3 flatForward = forward;
        flatForward.y = 0f;
        if (flatForward.sqrMagnitude < 0.0001f) return null;
        flatForward.Normalize();

        Interactable best = null;
        float bestScore = float.MaxValue;

        for (int i = 0; i < items.Count; i++) {
            Interactable candidate = items[i];
            if (candidate == null || !candidate.CanActivate) continue;

            Vector3 toTarget = candidate.FocusPosition - origin;
            float distance = toTarget.magnitude;
            if (distance > candidate.FocusRadius) continue;

            Vector3 flat = toTarget;
            flat.y = 0f;
            // 真上・真下にあるものは正面判定ができないので対象外にする。
            if (flat.sqrMagnitude < 0.0001f) continue;

            float angle = Vector3.Angle(flatForward, flat.normalized);
            if (angle > maxAngle) continue;

            if (extraFilter != null && !extraFilter(candidate, origin)) continue;

            float angleScore = angle / maxAngle;
            float distanceScore = distance / Mathf.Max(candidate.FocusRadius, 0.001f);
            float score = angleScore * angleWeight + distanceScore * (1f - angleWeight);

            if (score < bestScore) {
                bestScore = score;
                best = candidate;
            }
        }

        return best;
    }
}
