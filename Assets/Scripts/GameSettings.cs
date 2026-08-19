using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.SceneManagement;

// 게임 설정 값 저장소 (기획서 (7) '설정 기능' + 기타 메모 '게임 전체 화면 플레이 기능').
//
// 여기는 '값·저장·적용'만 책임진다. 화면(UI)은 따로다 —
//   · Assets/new cs/Option/DisplaySettings.cs, AudioSettings.cs  (팀원 제작, 실제 게임에서 쓰는 설정창)
//   · Assets/Scripts/SettingsMenu.cs                              (디자인 나오기 전 임시 UI, SampleScene2 전용)
// 어느 쪽을 쓰든 값은 전부 여기로 모인다. 그래야 게임을 껐다 켜도 설정이 유지되고,
// 같은 항목을 두 시스템이 각자 따로 적용해서 두 겹으로 걸리는 일이 없다.
//
// 저장은 PlayerPrefs. 이 프로젝트의 슬롯 세이브(Assets/new cs/Save/)는
// '플레이 진행'을 저장하는 별개 시스템이라 설정과 섞지 않는다.
//
// ★ 볼륨을 나눠 쓰는 이유
//   마스터 하나로 처리하면 BGM과 효과음을 따로 못 줄인다.
//   BGM은 MusicManager가, 효과음은 ProximitySound 등이 Changed 이벤트를 받아 자기 볼륨에 곱한다.
//   마스터는 AudioMixer가 있으면 그쪽(AudioSettings)이, 없으면 AudioListener가 맡는다.
public static class GameSettings
{
    const string KeyMaster = "cellarium.volume.master";
    const string KeyBgm = "cellarium.volume.bgm";
    const string KeySfx = "cellarium.volume.sfx";
    const string KeyBrightness = "cellarium.screen.brightness";
    const string KeyResW = "cellarium.screen.width";
    const string KeyResH = "cellarium.screen.height";
    const string KeyMode = "cellarium.screen.mode";

    // 설정이 바뀔 때마다 호출된다. MusicManager·ProximitySound가 여기에 붙어 자기 볼륨을 갱신한다.
    public static event System.Action Changed;

    static float master = 1f;
    static float bgm = 0.5f;
    static float sfx = 1f;

    // 0.5 = 원본. 팀원 설정창의 밝기 슬라이더(0~100, 기본 50)와 같은 기준이다.
    // 0.5보다 낮으면 어두워지고, 높으면 밝아진다(밝게는 URP 후처리가 있는 씬에서만 가능 — 아래 ApplyBrightness 참고).
    static float brightness = 0.5f;

    static int resW, resH;
    static FullScreenMode screenMode = FullScreenMode.FullScreenWindow;

    static bool loaded;

    // AudioMixer로 마스터를 조절하는 UI(AudioSettings)가 살아 있으면 true.
    // 그때는 AudioListener를 건드리지 않는다 — 둘 다 걸면 볼륨이 두 번 깎인다.
    static bool externalMaster;

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

    // 0~1 (0.5 = 원본). 기획서 UI의 '화면 밝기'.
    public static float Brightness
    {
        get { EnsureLoaded(); return brightness; }
        set { EnsureLoaded(); brightness = Mathf.Clamp01(value); Apply(); }
    }

    public static bool Fullscreen
    {
        get { EnsureLoaded(); return screenMode != FullScreenMode.Windowed; }
        set { EnsureLoaded(); screenMode = value ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed; Apply(); }
    }

    public static FullScreenMode ScreenMode
    {
        get { EnsureLoaded(); return screenMode; }
        set { EnsureLoaded(); screenMode = value; Apply(); }
    }

    public static Vector2Int Resolution
    {
        get { EnsureLoaded(); return new Vector2Int(resW, resH); }
    }

    // 팀원 설정창이 고른 해상도를 그대로 저장한다.
    // (그쪽은 Screen.resolutions에서 목록을 만들기 때문에 아래 표준 목록의 인덱스로는 표현이 안 된다)
    public static void SetResolution(int width, int height, FullScreenMode mode)
    {
        EnsureLoaded();
        if (width > 0 && height > 0) { resW = width; resH = height; }
        screenMode = mode;
        Apply();
    }

    // ── 해상도 목록 (SettingsMenu 임시 UI용) ──────────────────────────

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

    public static int ResolutionIndex
    {
        get { EnsureLoaded(); return NearestResolutionIndex(resW, resH); }
        set
        {
            EnsureLoaded();
            List<Vector2Int> list = ResolutionList;
            int i = Mathf.Clamp(value, 0, list.Count - 1);
            resW = list[i].x;
            resH = list[i].y;
            Apply();
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
        externalMaster = false;
        cachedAdjustments = null;
        cachedVolume = null;
    }

    // 게임을 켜자마자(첫 씬 로드 전) 저장해둔 설정을 적용한다.
    // 설정 UI를 한 번도 열지 않아도 지난번에 고른 해상도·볼륨으로 시작해야 하기 때문.
    [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
    static void Boot()
    {
        EnsureLoaded();
        Apply();

        // 밝기를 어떤 방식으로 걸지는 씬마다 다르다(URP 후처리가 켜진 씬 / 아닌 씬).
        // 씬이 바뀌면 다시 판단해야 한다.
        SceneManager.sceneLoaded -= OnSceneLoaded;
        SceneManager.sceneLoaded += OnSceneLoaded;

        Application.quitting -= Flush;
        Application.quitting += Flush;
    }

    static void OnSceneLoaded(Scene scene, LoadSceneMode mode)
    {
        cachedVolume = null;
        cachedAdjustments = null;
        ApplyBrightness();
    }

    static void EnsureLoaded()
    {
        if (loaded) return;
        loaded = true; // Apply → EnsureLoaded 재진입을 막기 위해 먼저 세운다

        master = PlayerPrefs.GetFloat(KeyMaster, 1f);
        bgm = PlayerPrefs.GetFloat(KeyBgm, 0.5f);
        sfx = PlayerPrefs.GetFloat(KeySfx, 1f);
        brightness = PlayerPrefs.GetFloat(KeyBrightness, 0.5f);

        // 고른 적이 없으면 지금 화면 그대로 시작한다
        resW = PlayerPrefs.GetInt(KeyResW, 0);
        resH = PlayerPrefs.GetInt(KeyResH, 0);
        if (resW <= 0 || resH <= 0) { resW = Screen.width; resH = Screen.height; }

        int savedMode = PlayerPrefs.GetInt(KeyMode, -1);
        screenMode = savedMode >= 0 ? (FullScreenMode)savedMode : Screen.fullScreenMode;
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

        // AudioMixer 쪽이 마스터를 맡고 있으면 여기선 손대지 않는다
        if (!externalMaster) AudioListener.volume = master;

        ApplyScreen();
        ApplyBrightness();

        Save();
        Changed?.Invoke();
    }

    static void ApplyScreen()
    {
        // 에디터에서는 Screen.SetResolution이 무시된다 — 빌드에서만 실제로 바뀐다.
        if (resW <= 0 || resH <= 0) return;

        if (Screen.width != resW || Screen.height != resH || Screen.fullScreenMode != screenMode)
            Screen.SetResolution(resW, resH, screenMode);
    }

    // ── 밝기 ──────────────────────────────────────────────────────────
    //
    // 방식이 두 가지다.
    //   ① URP 후처리(ColorAdjustments.postExposure) — 팀원이 Heart A00에 붙여둔 방식. 어둡게도 밝게도 된다.
    //   ② 화면 위에 검은 판 덮기(ScreenBrightness) — 어둡게만 된다.
    // ①은 그 씬 카메라에 Post Processing이 켜져 있고 Volume이 있어야만 실제로 그려진다.
    // 지금은 Heart A00에만 그 둘이 갖춰져 있어서, 나머지 씬은 ②로 넘어간다.
    // 앞으로 다른 씬에도 Volume + 카메라 Post Processing을 켜면 자동으로 ①로 바뀐다.

    static Volume cachedVolume;
    static ColorAdjustments cachedAdjustments;

    // 설정창이 인스펙터로 물고 있는 Volume을 알려주면 씬을 훑지 않아도 된다 (DisplaySettings).
    // 그 Volume이 있는 씬을 벗어나면 파괴되므로, 그때는 아래 스캔으로 자동으로 되돌아간다.
    public static void SetBrightnessVolume(Volume volume)
    {
        if (volume == null) return;

        VolumeProfile shared = volume.sharedProfile;
        if (shared == null || !shared.Has<ColorAdjustments>()) return;

        if (volume.profile != null && volume.profile.TryGet(out ColorAdjustments ca))
        {
            cachedVolume = volume;
            cachedAdjustments = ca;
            ApplyBrightness();
        }
    }

    static void ApplyBrightness()
    {
        if (ApplyBrightnessViaVolume())
            ScreenBrightness.Apply(0.5f); // 검은 판은 투명하게 — 두 겹으로 어두워지지 않게
        else
            ScreenBrightness.Apply(brightness);
    }

    static bool ApplyBrightnessViaVolume()
    {
        if (!PostProcessingEnabledOnCamera()) return false;

        if (cachedVolume == null || cachedAdjustments == null)
        {
            cachedVolume = null;
            cachedAdjustments = null;

            Volume[] volumes = Object.FindObjectsByType<Volume>(FindObjectsSortMode.None);
            foreach (Volume v in volumes)
            {
                if (!v.isActiveAndEnabled) continue;

                // sharedProfile로 먼저 훑는다 — profile은 접근하는 순간 사본을 만들기 때문에
                // 쓰지도 않을 Volume까지 복제되는 걸 피한다.
                VolumeProfile shared = v.sharedProfile;
                if (shared == null || !shared.Has<ColorAdjustments>()) continue;

                if (v.profile != null && v.profile.TryGet(out ColorAdjustments ca))
                {
                    cachedVolume = v;
                    cachedAdjustments = ca;
                    break;
                }
            }
        }

        if (cachedAdjustments == null) return false;

        cachedAdjustments.postExposure.overrideState = true;
        cachedAdjustments.postExposure.value = Mathf.Lerp(-2f, 2f, brightness);
        return true;
    }

    static bool PostProcessingEnabledOnCamera()
    {
        Camera cam = Camera.main;
        if (cam == null) return false;

        UniversalAdditionalCameraData data = cam.GetComponent<UniversalAdditionalCameraData>();
        return data != null && data.renderPostProcessing;
    }

    // AudioMixer로 마스터 볼륨을 직접 거는 UI가 등록/해제할 때 부른다 (AudioSettings).
    public static void SetExternalMasterVolume(bool external)
    {
        externalMaster = external;

        // 믹서가 맡기로 했으면 AudioListener는 원상복구해둔다
        if (external) AudioListener.volume = 1f;
        else Apply();
    }

    // 값만 써둔다. 디스크 기록(PlayerPrefs.Save)은 Flush에서 한다 —
    // 슬라이더를 드래그하면 Apply가 매 프레임 불리는데 그때마다 디스크에 쓰면 끊긴다.
    public static void Save()
    {
        PlayerPrefs.SetFloat(KeyMaster, master);
        PlayerPrefs.SetFloat(KeyBgm, bgm);
        PlayerPrefs.SetFloat(KeySfx, sfx);
        PlayerPrefs.SetFloat(KeyBrightness, brightness);
        PlayerPrefs.SetInt(KeyResW, resW);
        PlayerPrefs.SetInt(KeyResH, resH);
        PlayerPrefs.SetInt(KeyMode, (int)screenMode);
    }

    public static void Flush()
    {
        Save();
        PlayerPrefs.Save();
    }

    // 설정 초기화 (필요하면 설정 UI에 '기본값' 버튼으로 연결)
    public static void ResetToDefaults()
    {
        EnsureLoaded();
        master = 1f;
        bgm = 0.5f;
        sfx = 1f;
        brightness = 0.5f;
        resW = 1920;
        resH = 1080;
        screenMode = FullScreenMode.FullScreenWindow;
        Apply();
    }
}
