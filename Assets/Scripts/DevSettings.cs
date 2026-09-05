using UnityEngine;

// 開発中だけ効くグローバルスイッチ置き場。
// 値は PlayerPrefs に持たせているので、ドメインリロードや Play モードの出入りでも維持される。
// 製品ビルドでは IsDevelopmentSession が false になり、全スイッチが強制的に無効化されるため、
// 開発中に ON のまま忘れてもリリースに漏れることはない。
public static class DevSettings
{
    const string k_SkipIntroKey = "DevSettings.SkipIntro";

    static bool loaded;
    static bool skipIntro;

    // エディタ実行中、または Development Build のときだけ true。
    public static bool IsDevelopmentSession {
        get { return Application.isEditor || Debug.isDebugBuild; }
    }

    // シーンの導入演出（カフェの入店フェードなど）を丸ごとスキップする。
    // エディタでは既定で ON  - 開発中は毎回見せられても邪魔になるだけなので。
    public static bool SkipIntro {
        get {
            if (!IsDevelopmentSession) return false;
            Load();
            return skipIntro;
        }
        set {
            skipIntro = value;
            loaded = true;
            PlayerPrefs.SetInt(k_SkipIntroKey, value ? 1 : 0);
            PlayerPrefs.Save();
        }
    }

    static void Load() {
        if (loaded) return;
        skipIntro = PlayerPrefs.GetInt(k_SkipIntroKey, Application.isEditor ? 1 : 0) != 0;
        loaded = true;
    }

#if UNITY_EDITOR
    const string k_SkipIntroMenu = "Tools/Dev Settings/Skip Scene Intro";

    [UnityEditor.MenuItem(k_SkipIntroMenu)]
    static void ToggleSkipIntro() {
        SkipIntro = !SkipIntro;
    }

    [UnityEditor.MenuItem(k_SkipIntroMenu, true)]
    static bool ToggleSkipIntroValidate() {
        UnityEditor.Menu.SetChecked(k_SkipIntroMenu, SkipIntro);
        return true;
    }
#endif
}
