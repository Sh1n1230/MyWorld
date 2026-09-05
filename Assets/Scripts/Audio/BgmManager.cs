using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Audio;

// BGM の再生を一手に引き受ける DontDestroyOnLoad のシングルトン。
//
// 各トリガーは「この曲を鳴らしてほしい」というリクエストを優先度つきで登録するだけで、
// 実際にどれを鳴らすかはこのクラスが決める。優先度が最大のリクエストが採用され、
// 同率なら後から登録されたものが勝つ。リクエストが取り下げられれば自動的に次点へ戻るので、
// 「カフェに入ったら店内BGM、出たら街のBGMに戻る」といった入れ子が自然に成立する。
//
// AudioSource を 2 本持ち、曲の切り替えは常にクロスフェードで行う。
// シーンに置かなくても初回アクセス時に自動生成されるため、事前準備は不要。
public class BgmManager : MonoBehaviour
{
    [SerializeField] [Range(0f, 1f)] float masterVolume = 1f;
    // 後から AudioMixer を導入したときの出力先。未設定なら既定の出力。
    [SerializeField] AudioMixerGroup outputMixerGroup = null;

    static BgmManager instance;
    static bool quitting;

    public static BgmManager Instance {
        get {
            if (instance != null) return instance;
            // 終了処理中やエディットモードで新しい GameObject を作らない。
            if (quitting || !Application.isPlaying) return null;

            instance = FindAnyObjectByType<BgmManager>();
            if (instance == null) {
                instance = new GameObject("BgmManager").AddComponent<BgmManager>();
            }
            return instance;
        }
    }

    public float MasterVolume {
        get { return masterVolume; }
        set { masterVolume = Mathf.Clamp01(value); }
    }

    // 現在鳴っているトラック。何も鳴っていなければ null。
    public BgmTrack CurrentTrack { get; private set; }

    class Request
    {
        public Object owner;
        public BgmTrack track;
        public int priority;
        public int sequence;
    }

    readonly List<Request> requests = new List<Request>();
    int sequenceCounter;

    AudioSource sourceA;
    AudioSource sourceB;
    AudioSource activeSource;   // フェードインしている（＝これから鳴らす）方
    AudioSource retiringSource; // フェードアウトしている方

    // プレイリストの再生順。Shuffle のときは 1 周ごとに並べ替える。
    readonly List<int> playOrder = new List<int>();
    int orderPosition;
    int lastClipIndex = -1;
    bool awaitingNextClip;
    float nextClipTime;

    float activeGain;           // activeSource のフェード進捗 0..1
    float retireStartVolume;
    float fadeDuration;
    float fadeElapsed;
    bool fading;

    void Awake() {
        if (instance != null && instance != this) {
            Destroy(gameObject);
            return;
        }

        instance = this;
        transform.SetParent(null);
        DontDestroyOnLoad(gameObject);

        sourceA = CreateSource();
        sourceB = CreateSource();
        activeSource = sourceA;
    }

    void OnApplicationQuit() {
        quitting = true;
    }

    void OnDestroy() {
        if (instance == this) instance = null;
    }

    AudioSource CreateSource() {
        AudioSource source = gameObject.AddComponent<AudioSource>();
        source.playOnAwake = false;
        source.loop = true;
        // BGM は空間内の一点から鳴るものではないので 2D 固定。
        source.spatialBlend = 0f;
        source.volume = 0f;
        source.outputAudioMixerGroup = outputMixerGroup;
        return source;
    }

    // --- リクエスト API -------------------------------------------------

    // owner ごとに 1 つのリクエストを保持する。同じ owner から再登録すると上書きされる。
    public void AddRequest(Object owner, BgmTrack track, int priority, float fade) {
        if (owner == null) return;

        RemoveRequestInternal(owner);
        requests.Add(new Request {
            owner = owner,
            track = track,
            priority = priority,
            sequence = ++sequenceCounter,
        });
        Refresh(fade);
    }

    public void RemoveRequest(Object owner, float fade) {
        if (RemoveRequestInternal(owner)) Refresh(fade);
    }

    bool RemoveRequestInternal(Object owner) {
        for (int i = requests.Count - 1; i >= 0; i--) {
            if (requests[i].owner == owner) {
                requests.RemoveAt(i);
                return true;
            }
        }
        return false;
    }

    // トリガーを介さず直接鳴らしたいとき用。最優先のリクエストとして扱われる。
    public void PlayImmediate(BgmTrack track, float fade) {
        AddRequest(this, track, int.MaxValue, fade);
    }

    public void ClearImmediate(float fade) {
        RemoveRequest(this, fade);
    }

    void Refresh(float fade) {
        Request best = null;
        for (int i = requests.Count - 1; i >= 0; i--) {
            Request request = requests[i];
            // owner が破棄済みならリクエストも消える（シーン遷移でトリガーが消えた場合など）。
            if (request.owner == null) {
                requests.RemoveAt(i);
                continue;
            }
            if (best == null
                || request.priority > best.priority
                || (request.priority == best.priority && request.sequence > best.sequence)) {
                best = request;
            }
        }

        BgmTrack next = best != null ? best.track : null;
        if (fade < 0f) {
            // 負のフェード時間は「トラックの既定値を使う」の意味。
            BgmTrack reference = next != null ? next : CurrentTrack;
            fade = reference != null ? reference.defaultFadeDuration : 0f;
        }
        Switch(next, fade);
    }

    // --- 再生 -----------------------------------------------------------

    void Switch(BgmTrack track, float fade) {
        if (track == CurrentTrack) return;

        // 前のクロスフェードがまだ終わっていなければ、古い方を即座に畳んで空きを作る。
        AudioSource incoming = activeSource == sourceA ? sourceB : sourceA;
        if (retiringSource != null && retiringSource != activeSource) {
            StopSource(retiringSource);
        }

        retiringSource = activeSource;
        retireStartVolume = activeSource != null ? activeSource.volume : 0f;
        activeSource = incoming;

        CurrentTrack = track;
        activeGain = 0f;

        awaitingNextClip = false;
        lastClipIndex = -1;

        if (track != null && track.HasClips) {
            BuildPlayOrder(track);
            PlayCurrentOrderEntry(track.startTime);
        } else {
            StopSource(activeSource);
        }

        fadeDuration = Mathf.Max(0f, fade);
        fadeElapsed = 0f;
        fading = true;
    }

    static void StopSource(AudioSource source) {
        if (source == null) return;
        source.Stop();
        source.clip = null;
        source.volume = 0f;
    }

    void Update() {
        if (fading) {
            fadeElapsed += Time.unscaledDeltaTime;
            float t = fadeDuration <= 0f ? 1f : Mathf.Clamp01(fadeElapsed / fadeDuration);
            activeGain = t;

            if (retiringSource != null) retiringSource.volume = retireStartVolume * (1f - t);

            if (t >= 1f) {
                fading = false;
                StopSource(retiringSource);
                retiringSource = null;
            }
        }

        AdvancePlaylist();

        // フェード中でなくても毎フレーム適用しておくと、masterVolume の変更が即座に反映される。
        if (activeSource != null && CurrentTrack != null) {
            activeSource.volume = CurrentTrack.volume * masterVolume * activeGain;
        }
    }

    // --- プレイリスト -----------------------------------------------------

    void BuildPlayOrder(BgmTrack track) {
        playOrder.Clear();
        for (int i = 0; i < track.clips.Length; i++) {
            if (track.clips[i] != null) playOrder.Add(i);
        }

        if (track.playMode == BgmTrack.PlayMode.Shuffle && playOrder.Count > 1) {
            // Fisher-Yates
            for (int i = playOrder.Count - 1; i > 0; i--) {
                int j = Random.Range(0, i + 1);
                int tmp = playOrder[i];
                playOrder[i] = playOrder[j];
                playOrder[j] = tmp;
            }
            // 周回のつなぎ目で同じ曲が 2 回続かないようにする。
            if (playOrder[0] == lastClipIndex) {
                playOrder[0] = playOrder[playOrder.Count - 1];
                playOrder[playOrder.Count - 1] = lastClipIndex;
            }
        }

        orderPosition = 0;
    }

    void PlayCurrentOrderEntry(float offset) {
        if (orderPosition < 0 || orderPosition >= playOrder.Count) {
            StopSource(activeSource);
            return;
        }

        int clipIndex = playOrder[orderPosition];
        AudioClip clip = CurrentTrack.clips[clipIndex];
        lastClipIndex = clipIndex;

        activeSource.clip = clip;
        // 1 曲だけならソース側のループに任せる。継ぎ目が出ず、毎フレームの判定も不要になる。
        activeSource.loop = CurrentTrack.loop && playOrder.Count == 1;
        activeSource.Play();

        if (offset > 0f) {
            activeSource.time = Mathf.Clamp(offset, 0f, Mathf.Max(0f, clip.length - 0.01f));
        }

        awaitingNextClip = false;
    }

    // 曲が鳴り終わったら次へ送る。AudioSource 側のループに任せている場合は何もしない。
    void AdvancePlaylist() {
        if (CurrentTrack == null || playOrder.Count == 0) return;
        if (activeSource == null || activeSource.loop) return;
        // ポーズ中は isPlaying が false になるので、曲が終わったと誤検知しないようにする。
        if (AudioListener.pause) return;

        if (awaitingNextClip) {
            if (Time.unscaledTime >= nextClipTime) StepToNextClip();
            return;
        }

        if (activeSource.clip == null || activeSource.isPlaying) return;

        if (CurrentTrack.gapBetweenClips > 0f) {
            awaitingNextClip = true;
            nextClipTime = Time.unscaledTime + CurrentTrack.gapBetweenClips;
            return;
        }

        StepToNextClip();
    }

    void StepToNextClip() {
        orderPosition++;

        if (orderPosition >= playOrder.Count) {
            if (!CurrentTrack.loop) {
                StopSource(activeSource);
                awaitingNextClip = false;
                return;
            }
            // 1 周したので並べ替え直す（Sequential ならそのままの順番）。
            BuildPlayOrder(CurrentTrack);
        }

        PlayCurrentOrderEntry(0f);
    }
}
