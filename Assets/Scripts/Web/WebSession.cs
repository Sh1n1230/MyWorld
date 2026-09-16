using System;
using UnityEngine;

namespace Portfolio.Web
{
    /// <summary>
    /// **Web から預かった「この訪問者は誰で、どこまで見たか」。docs/EVENT_SCHEMA.md §10。**
    ///
    /// 保存の実体は Web の localStorage で、**Unity は一切保存しない。**
    /// PlayerPrefs（WebGL では IndexedDB）を使えば Unity 側でも持てるが、
    /// そうすると同じことを覚えている場所が 2 つになり、「はじめから」で
    /// 両方消さないと食い違う。言語・アンケートを既に localStorage が持っている以上、
    /// **記憶の在り処は Web に一本化する。**
    ///
    /// Unity は起動のたびに RESTORE_SESSION を受け取り、それに合わせて状態を作り直すだけ。
    /// つまりここは受け取った内容の置き場で、ここ自身は何も決めない。
    /// </summary>
    public static class WebSession
    {
        static readonly string[] s_empty = new string[0];

        /// <summary>選択済みのキャラ id。未選択（「はじめから」を含む）なら null。</summary>
        public static string CharacterId { get; private set; }

        /// <summary>見終わった sequenceId の一覧。null にはならない。</summary>
        public static string[] Seen { get; private set; }

        /// <summary>RESTORE_SESSION を一度でも受け取ったか。Web がいない Editor 単体では false のまま。</summary>
        public static bool Received { get; private set; }

        /// <summary>RESTORE_SESSION を受け取るたびに発火する。**再訪でも「はじめから」でも必ず来る。**</summary>
        public static event Action Restored;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        static void ResetStatics()
        {
            CharacterId = null;
            Seen = s_empty;
            Received = false;
            Restored = null;
        }

        /// <summary>この sequenceId の演出を既に見終わっているか。</summary>
        public static bool HasSeen(string sequenceId)
        {
            if (string.IsNullOrEmpty(sequenceId)) return false;
            var seen = Seen;
            if (seen == null) return false;
            for (int i = 0; i < seen.Length; i++)
            {
                if (seen[i] == sequenceId) return true;
            }
            return false;
        }

        /// <summary>WebBridge が RESTORE_SESSION を受けたときに呼ぶ。</summary>
        public static void Apply(RestoreSessionPayload payload)
        {
            if (payload == null) return;

            CharacterId = string.IsNullOrEmpty(payload.characterId) ? null : payload.characterId;
            Seen = payload.seen != null ? payload.seen : s_empty;
            Received = true;

            if (Restored != null) Restored();
        }
    }
}
