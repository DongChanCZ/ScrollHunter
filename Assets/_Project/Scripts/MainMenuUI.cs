using System;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.Audio;
using UnityEngine.UI;

/// <summary>시작 메뉴 표시·설정. 전투 진행과 계승 데이터는 기존 컴포넌트가 처리한다.</summary>
public class MainMenuUI : MonoBehaviour
{
    [SerializeField] private GameObject titlePanel, controlsPanel, optionsPanel, inheritancePanel;
    [SerializeField] private Button controlsOpen, controlsClose, optionsOpen, optionsCancel, optionsApply, quitButton, inheritanceOpen;
    [SerializeField] private Button modeButton, resolutionPrevious, resolutionNext;
    [SerializeField] private TMP_Text modeLabel, resolutionLabel;
    [SerializeField] private Slider[] volumeSliders;
    [SerializeField] private TMP_Text[] volumeLabels;
    [SerializeField] private AudioMixer mixer;
    [SerializeField] private string[] mixerParameters = { "MusicVolume", "SkillVolume", "EnemyVolume" };
    [SerializeField] private string preferencesPrefix = "ScrollHunter.Options.";
    [SerializeField] private string fullscreenText = "전체화면 (테두리 없음)", windowedText = "창모드";
    [SerializeField] private string resolutionFormat = "{0} × {1}", volumeFormat = "{0:0}%";
    [SerializeField] private KeyCode closeKey = KeyCode.Escape;
    [SerializeField] private Vector2Int[] windowSizes = { new Vector2Int(1280,720), new Vector2Int(1366,768), new Vector2Int(1600,900), new Vector2Int(1920,1080) };
    [SerializeField, Range(0f,1f)] private float defaultVolume = 1f;
    [SerializeField] private float muteDecibels = -80f;
    [Header("스크롤 문자")]
    [SerializeField] private RawImage background;
    [SerializeField, Min(.1f)] private float glowPeriod = 5f;
    [SerializeField, Min(0f)] private float glowOffDelay = 4f;
    [SerializeField] private string pulseProperty = "_Pulse";
    [SerializeField] private Shader runeMaskShader;
    private Material glowMaterial;
    private RenderTexture runeLight;
    private float glowTime;
    private readonly List<Vector2Int> resolutions = new List<Vector2Int>();
    private int resolutionIndex;
    private bool windowed;
    public bool OptionsVisible => optionsPanel != null && optionsPanel.activeSelf;

    private void Awake()
    {
        controlsOpen.onClick.AddListener(OpenControls);
        controlsClose.onClick.AddListener(ClosePanels);
        optionsOpen.onClick.AddListener(OpenOptions);
        optionsCancel.onClick.AddListener(ClosePanels);
        optionsApply.onClick.AddListener(ApplyOptions);
        inheritanceOpen.onClick.AddListener(ClosePanels);
        quitButton.onClick.AddListener(Quit);
        modeButton.onClick.AddListener(() => { windowed = !windowed; UpdateDisplayLabels(); });
        resolutionPrevious.onClick.AddListener(() => ChangeResolution(-1));
        resolutionNext.onClick.AddListener(() => ChangeResolution(1));
        for (int i = 0; i < volumeSliders.Length; i++)
        {
            int index = i;
            volumeSliders[i].onValueChanged.AddListener(value => UpdateVolumeLabel(index));
        }
        if (background != null && background.material != null)
        {
            glowMaterial = new Material(background.material);
            background.material = glowMaterial;
            BuildRuneLight();
        }
        BuildResolutions();
        ClosePanels();
    }

    private void BuildRuneLight()
    {
        if (background.texture == null || runeMaskShader == null) return;
        runeLight = new RenderTexture(background.texture.width, background.texture.height, 0, RenderTextureFormat.ARGB32, RenderTextureReadWrite.Linear)
        {
            name = "Title rune light", useMipMap = true, autoGenerateMips = false,
            filterMode = FilterMode.Trilinear, wrapMode = TextureWrapMode.Clamp
        };
        runeLight.Create();
        var previous = RenderTexture.active;
        var maskMaterial = new Material(runeMaskShader);
        maskMaterial.SetVector("_MaskThreshold", glowMaterial.GetVector("_MaskThreshold"));
        try
        {
            // 문자 마스크를 한 번 생성하고 밉맵의 퍼진 빛을 재사용한다.
            Graphics.Blit(background.texture, runeLight, maskMaterial, 0);
            runeLight.GenerateMips();
            glowMaterial.SetTexture("_RuneLight", runeLight);
        }
        finally { RenderTexture.active = previous; Destroy(maskMaterial); }
    }

    private void Start()
    {
        ReadSavedOptions();
        ApplyAudio(); // AudioMixer parameters are applied after Awake.
        ApplyDisplay();
    }

    private void Update()
    {
        if (!titlePanel.activeInHierarchy) { ClosePanels(); return; }
        if (Input.GetKeyDown(closeKey)) ClosePanels();
        glowTime += Time.unscaledDeltaTime;
        if (glowMaterial != null) glowMaterial.SetFloat(pulseProperty, GlowAt(glowTime));
    }

    public float GlowAt(float seconds)
    {
        float duration = Mathf.Max(.1f, glowPeriod);
        float elapsed = Mathf.Repeat(seconds, duration + Mathf.Max(0f, glowOffDelay));
        if (elapsed >= duration) return 0f;
        float wave = .5f - .5f * Mathf.Cos(elapsed / duration * Mathf.PI * 2f);
        return Mathf.SmoothStep(0f, 1f, wave);
    }

    private Vector2Int NativeSize()
    {
        var r = Screen.currentResolution;
        return r.width > 0 && r.height > 0 ? new Vector2Int(r.width, r.height) : windowSizes[windowSizes.Length - 1];
    }

    private void BuildResolutions()
    {
        var native = NativeSize();
        resolutions.Clear();
        resolutions.AddRange(Screen.resolutions.Select(r => new Vector2Int(r.width, r.height)));
        resolutions.AddRange(windowSizes.Where(r => r.x <= native.x && r.y <= native.y));
        resolutions.Add(native);
        var unique = resolutions.Distinct().OrderBy(r => r.x).ThenBy(r => r.y).ToArray();
        resolutions.Clear(); resolutions.AddRange(unique);
    }

    private void ReadSavedOptions()
    {
        windowed = PlayerPrefs.GetInt(preferencesPrefix + "Windowed", 0) == 1;
        var native = NativeSize();
        var saved = new Vector2Int(PlayerPrefs.GetInt(preferencesPrefix + "Width", native.x), PlayerPrefs.GetInt(preferencesPrefix + "Height", native.y));
        resolutionIndex = resolutions.IndexOf(saved);
        if (resolutionIndex < 0) resolutionIndex = resolutions.IndexOf(native);
        for (int i = 0; i < volumeSliders.Length; i++)
        {
            float value = PlayerPrefs.GetFloat(preferencesPrefix + mixerParameters[i], defaultVolume);
            volumeSliders[i].SetValueWithoutNotify(float.IsNaN(value) || float.IsInfinity(value) ? defaultVolume : Mathf.Clamp01(value));
            UpdateVolumeLabel(i);
        }
        UpdateDisplayLabels();
    }

    public void OpenControls()
    {
        if (!titlePanel.activeInHierarchy) return;
        ClosePanels(); inheritancePanel.SetActive(false);
        controlsPanel.SetActive(true); controlsPanel.transform.SetAsLastSibling();
    }

    public void OpenOptions()
    {
        if (!titlePanel.activeInHierarchy) return;
        ClosePanels(); inheritancePanel.SetActive(false); ReadSavedOptions();
        optionsPanel.SetActive(true); optionsPanel.transform.SetAsLastSibling();
    }

    public void ClosePanels()
    {
        controlsPanel.SetActive(false); optionsPanel.SetActive(false);
    }

    private void ChangeResolution(int direction)
    {
        resolutionIndex = (resolutionIndex + direction + resolutions.Count) % resolutions.Count;
        UpdateDisplayLabels();
    }

    private void UpdateDisplayLabels()
    {
        modeLabel.text = windowed ? windowedText : fullscreenText;
        var r = resolutions[resolutionIndex];
        resolutionLabel.text = string.Format(resolutionFormat, r.x, r.y);
    }

    private void UpdateVolumeLabel(int index) => volumeLabels[index].text = string.Format(volumeFormat, volumeSliders[index].value * 100f);

    public void ApplyOptions()
    {
        if (!OptionsVisible || !titlePanel.activeInHierarchy) return;
        var r = resolutions[resolutionIndex];
        PlayerPrefs.SetInt(preferencesPrefix + "Windowed", windowed ? 1 : 0);
        PlayerPrefs.SetInt(preferencesPrefix + "Width", r.x);
        PlayerPrefs.SetInt(preferencesPrefix + "Height", r.y);
        for (int i = 0; i < volumeSliders.Length; i++) PlayerPrefs.SetFloat(preferencesPrefix + mixerParameters[i], volumeSliders[i].value);
        PlayerPrefs.Save();
        ApplyAudio(); ApplyDisplay(); ClosePanels();
    }

    private void ApplyAudio()
    {
        if (mixer == null) return;
        for (int i = 0; i < volumeSliders.Length; i++)
        {
            float v = volumeSliders[i].value;
            mixer.SetFloat(mixerParameters[i], v <= 0f ? muteDecibels : Mathf.Max(muteDecibels, 20f * Mathf.Log10(v)));
        }
    }

    private void ApplyDisplay()
    {
        // Game View는 창모드 전환을 재현하지 않는다. 에디터 창/검사 해상도는 바꾸지 않는다.
#if !UNITY_EDITOR
        var r = resolutions[resolutionIndex];
        Screen.SetResolution(r.x, r.y, windowed ? FullScreenMode.Windowed : FullScreenMode.FullScreenWindow);
#endif
    }

    public void Quit()
    {
        if (!titlePanel.activeInHierarchy || OptionsVisible || controlsPanel.activeSelf || inheritancePanel.activeSelf) return;
#if UNITY_EDITOR
        UnityEditor.EditorApplication.isPlaying = false;
#else
        Application.Quit();
#endif
    }

    private void OnDestroy()
    {
        if (glowMaterial != null) Destroy(glowMaterial);
        if (runeLight != null) { runeLight.Release(); Destroy(runeLight); }
    }
}
