// Unity → Web の唯一の出口。docs/EVENT_SCHEMA.md §2。
//
// Unity 側は封筒（Envelope）を JSON 文字列にして PortfolioEmit を呼ぶだけ。
// ここでは中身を解釈せず、そのまま CustomEvent に載せて window に投げる。
// Web 側は window.addEventListener("unity:message", ...) で受け、zod で検証する。
mergeInto(LibraryManager.library, {

  PortfolioEmit: function (ptr) {
    var json = UTF8ToString(ptr);
    var detail;
    try {
      detail = JSON.parse(json);
    } catch (e) {
      // ここで例外を投げると Unity の呼び出し元まで巻き込むので、必ず握り潰す。
      console.warn("[WebBridge] Unity から不正な JSON を受け取りました:", json);
      return;
    }
    if (typeof window === "undefined") return;
    window.dispatchEvent(new CustomEvent("unity:message", { detail: detail }));
  },

  // ホバー中のカーソルを手の形にする（docs/ARCHITECTURE.md §4.2 の L3）。
  // イベントではない — canvas の CSS を触るだけで、Web のアプリ側は関与しない。
  PortfolioSetPointerCursor: function (pointer) {
    var canvas = Module["canvas"];
    if (!canvas) return;
    canvas.style.cursor = pointer ? "pointer" : "";
  },

});
