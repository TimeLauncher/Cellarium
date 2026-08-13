using System.Collections.Generic;
using UnityEngine;

// 게임 설정 값 저장소 (기획서 (7) '설정 기능' + 기타 메모 '게임 전체 화면 플레이 기능').
//
// 여기는 '값과 적용'만 책임진다. 화면(UI)은 SettingsMenu가 따로 담당한다 —
// 디자인이 아직 안 나왔어도 설정 자체는 동작해야 하고, 게임을 켤 때마다
// 저장해둔 값이 UI 없이도 그대로 적용돼야 하기 때문이다.
//
// 저장은 PlayerPrefs를 쓴다. 이 프로젝트의 슬롯 세이브(Assets/new cs/Save/)는
// '플레이 진행'을 저장하는 별개 시스템이라 설정과 섞지 않는다.
//
// ★ 볼륨을 나눠 쓰는 이유
//   AudioListener.volume 하나로 처리하면 BGM과 효과음을 따로 못 줄인다.
//   마스터만 AudioListener에 걸고, BGM은 MusicManager가, 효과음은 ProximitySound 등이
//   각자 Changed 이벤트를 받아 자기 볼륨에 곱한다.
public static class GameSettings
{
    const string KeyMaster = "cellarium.volume.master";
    const string KeyBgm = "cellarium.volume.bgm";
    const string KeySfx = "cellarium.volume.sfx";
    const string KeyFullscreen = "cellarium.screen.fullscreen";
    const string KeyResIndex = "cellarium.screen.resIndex";
    const string KeyBrightness = "cellarium.screen.brightness";

    // 설정이 바뀔 때마다 호출된다. MusicManager·ProximitySound가 여기에 붙어 자기 볼륨을 갱신한다.
    public static event System.Action Changed;

    static float master = 1f;
    static float bgm = 0.5f;
    static float sfx = 1f;
    static bool fullscreen = true;
    static int resIndex = -1;      // -1 = 아직 고른 적 없음(현재 해상도 유지)
    static float brightness = 1f;  // 1 = 원본, 낮을수록 어두움

    static bool loaded;

    // ── 값 ────────────────────────────────────────────────────────────

    public static float MasterVolume
    {
        get { EnsureLoaded(); return master; }
        set { EnsureLoaded(); master = Mathf.Clamp01(value); Apply(); }
    }

    public static float BgmVolume
    {
        get { EnsureLoaded(); return bgm; }
        set { EnsureLoaded(); bgm = Mathf.Clamp01(value); Apply(); }
    }

    public static float SfxVolume
    {
        get { EnsureLoaded(); return sfx; }
        set { EnsureLoaded(); sfx = Mathf.Clamp01(value); Apply(); }
    }

    public static bool Fullscreen
    {
        get { EnsureLoaded(); return fullscreen; }
        set { EnsureLoaded(); fullscreen = value; Apply(); }
    }

    // 기획서 UI의 '화면 밝기'. 후처리(Post Processing)가 프로젝트에 없어서
    // 화면 위에 검은 판을 덮는 방식으로 구현한다 (ScreenBrightness).
    public static float Brightness
    {
        get { EnsureLoaded(); return brightness; }
        set { EnsureLoaded(); brightness = Mathf.Clamp(value, 0.2f, 1f); Apply(); }
    }

    public static int ResolutionIndex
    {
        get { EnsureLoaded(); return Mathf.Clamp(resIndex, 0, ResolutionList.Count - 1); }
        set { EnsureLoaded(); resIndex = Mathf.Clamp(value, 0, ResolutionList.Count - 1); Apply(); }
    }

    // ── 해상도 목록 ───────────────────────────────────────────────────

    static List<Vector2Int> resolutions;

    // 기획서 UI가 "2560X1440[16:9]" 형태라 16:9만 추린다.
    // Screen.resolutions는 빌드·모니터마다 제각각이고 에디터에선 거의 비어 있어서,
    // 표준 목록을 기준으로 두고 모니터가 감당 못 하는 것만 걸러낸다.
    static readonly Vector2Int[] standard16x9 =
    {
        new Vector2Int(1280, 720),
        new Vector2Int(1600, 900),
        new Vector2Int(1920, 1080),
        new Vector2Int(2560, 1440),
        new Vector2Int(3840, 2160),
    };

    public static List<Vector2Int> ResolutionList
    {
        get
        {
            if (resolutions != null) return resolutions;

            resolutions = new List<Vector2Int>();

            int maxW = Display.main != null ? Display.main.systemWidth : 1920;
            int maxH = Display.main != null ? Display.main.systemHeight : 1080;

            foreach (Vector2Int r in standard16x9)
                if (r.x <= maxW && r.y <= maxH) resolutions.Add(r);

            // 모니터가 720p보다 작은 특수한 경우에도 목록이 비지 않게
            if (resolutions.Count == 0) resolutions.Add(new Vector2Int(maxW, maxH));

            return resolutions;
        }
    }

    public static string ResolutionLabel(int index)
    {
        List<Vector2Int> list = ResolutionList;
        if (index < 0 || index >= list.Count) return "-";
        return $"{list[index].x}X{list[index].y}[16:9]";
    }

    // ── 저장 / 불러오기 / 적용 ────────────────────────────────────────

    // static 필드는 도메인 리로드를 끄면 플레이 모드를 나갔다 들어와도 값이 남는다.
    // Changed에 지난 판의 MusicManager가 그대로 붙어 있으면 이미 파괴된 오브젝트를 건드려
    // MissingReferenceException이 난다. (PlayerInputLock과 같은 이유·같은 방식)
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
    static void ResetStatics()
    {
        Changed = null;
        loaded = false;
        resolutions = null;
    }

    // 게임을 켜자마자(첫 씬 로드 전) 저장해둔 설정을 적용한다.
    // 설정 UI를 한 번도 열지 않아도 지난번에 고른 해상도·볼륨으로 시작해야 하기 때문.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        EnsureLoaded();
        Apply();
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true; // Apply → EnsureLoaded 재진입을 막기 위해 먼저 세운다

        master = PlayerPrefs.GetFloat(KeyMaster, 1f);
        bgm = PlayerPrefs.GetFloat(KeyBgm, 0.5f);
        sfx = PlayerPrefs.GetFloat(KeySfx, 1f);
        fullscreen = PlayerPrefs.GetInt(KeyFullscreen, 1) == 1;
        brightness = PlayerPrefs.GetFloat(KeyBrightness, 1f);

        // 고른 적이 없으면 지금 화면 크기와 가장 가까운 항목을 기본으로 잡는다
        resIndex = PlayerPrefs.GetInt(KeyResIndex, -1);
        if (resIndex < 0) resIndex = NearestResolutionIndex(Screen.width, Screen.height);
    }

    static int NearestResolutionIndex(int w, int h)
    {
        List<Vector2Int> list = ResolutionList;
        int best = 0;
        int bestDiff = int.MaxValue;

        for (int i = 0; i < list.Count; i++)
        {
            int diff = Mathf.Abs(list[i].x - w) + Mathf.Abs(list[i].y - h);
            if (diff < bestDiff) { bestDiff = diff; best = i; }
        }
        return best;
    }

    // 값이 바뀔 때마다 화면·소리에 즉시 반영하고 저장한다.
    // (기획서 UI엔 'S 저장' 키가 있지만, 즉시 저장해도 동작이 달라지지 않고
    //  저장을 깜빡해 설정이 날아가는 사고만 없어진다)
    public static void Apply()
    {
        EnsureLoaded();

        AudioListener.volume = master;
        ApplyScreen();
        ScreenBrightness.Apply(brightness);

        Save();
        Changed?.Invoke();
    }

    static void ApplyScreen()
    {
        // 에디터에서는 Screen.SetResolution이 무시된다 — 빌드에서만 실제로 바뀐다.
        Vector2Int r = ResolutionList[ResolutionIndex];
        FullScreenMode mode = fullscreen ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed;

        if (Screen.width != r.x || Screen.height != r.y || Screen.fullScreenMode != mode)
            Screen.SetResolution(r.x, r.y, mode);
    }

    public static void Save()
    {
        PlayerPrefs.SetFloat(KeyMaster, master);
        PlayerPrefs.SetFloat(KeyBgm, bgm);
        PlayerPrefs.SetFloat(KeySfx, sfx);
        PlayerPrefs.SetInt(KeyFullscreen, fullscreen ? 1 : 0);
        PlayerPrefs.SetInt(KeyResIndex, resIndex);
        PlayerPrefs.SetFloat(KeyBrightness, brightness);
        PlayerPrefs.Save();
    }

    // 설정 초기화 (필요하면 설정 UI에 '기본값' 버튼으로 연결)
    public static void ResetToDefaults()
    {
        EnsureLoaded();
        master = 1f;
        bgm = 0.5f;
        sfx = 1f;
        fullscreen = true;
        brightness = 1f;
        resIndex = NearestResolutionIndex(1920, 1080);
        Apply();
    }
}
