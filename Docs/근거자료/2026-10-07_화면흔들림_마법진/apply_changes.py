from pathlib import Path
root = Path(__file__).resolve().parents[3]
out = Path(__file__).resolve().parent
names = ['Scripts/RedHitShake.cs','Scripts/BattleEnvironment.cs','Tests/Editor/EndingFeedbackChecks.cs','Tests/Editor/BattleEnvironmentChecks.cs']
paths = {n:root/'Assets/_Project'/n for n in names}
before = {n:p.read_bytes() for n,p in paths.items()}
texts = {n:b.decode('utf-8-sig').replace('\r\n','\n') for n,b in before.items()}
for n,b in before.items(): (out/(Path(n).name+'.before')).write_bytes(b)

texts[names[0]] = '''using UnityEngine;

/// <summary>무방어 빨강 피격에 화면과 HUD를 함께 흔든다. 전투 난수·판정은 유지한다.</summary>
[DisallowMultipleComponent, DefaultExecutionOrder(10000)]
public class RedHitShake : MonoBehaviour
{
    [SerializeField, Min(0.01f)] private float duration = 0.24f;
    [SerializeField, Min(0f)] private float amplitude = 16f; // 1080 높이 기준 화면 픽셀
    [SerializeField, Min(0f)] private float frequency = 24f;
    private Player player;
    private CombatMetrics metrics;
    private Camera view;
    private Canvas canvas;
    private RectTransform hud;
    private Matrix4x4 baseProjection;
    private bool applied;
    private Vector2 offset;
    private float remaining;
    public bool IsShaking => remaining > 0f;

    private void Awake()
    {
        player = FindFirstObjectByType<Player>();
        metrics = FindFirstObjectByType<CombatMetrics>();
        view = GetComponent<Camera>();
    }
    private void Start()
    {
        // 기존 Canvas의 레이아웃·참조를 유지한 채 표시용 부모만 만든다.
        canvas = FindFirstObjectByType<Canvas>();
        if (canvas == null || !canvas.isRootCanvas || canvas.renderMode != RenderMode.ScreenSpaceOverlay) return;
        hud = new GameObject("Screen shake content", typeof(RectTransform)).GetComponent<RectTransform>();
        hud.SetParent(canvas.transform, false);
        hud.anchorMin = Vector2.zero; hud.anchorMax = Vector2.one;
        hud.sizeDelta = Vector2.zero;
        CollectHud();
    }
    private void CollectHud()
    {
        if (hud == null) return;
        for (int i = 0; i < canvas.transform.childCount;)
        {
            Transform child = canvas.transform.GetChild(i);
            if (child == hud) { i++; continue; }
            child.SetParent(hud, false);
        }
    }
    private void OnEnable()
    {
        if (player == null) return;
        player.UnshieldedRedHit += Shake;
        player.BattleReset += Clear;
    }
    private void OnDisable()
    {
        if (player != null) { player.UnshieldedRedHit -= Shake; player.BattleReset -= Clear; }
        Clear();
    }
    private void Shake() => remaining = duration;
    // UI가 화면 좌표를 계산하기 전에 이전 표시 오프셋을 제거한다.
    private void Update() => RestoreView();
    private void LateUpdate() => Advance(metrics != null && metrics.Ended ? Time.unscaledDeltaTime : Time.deltaTime);
    private void Advance(float dt)
    {
        RestoreView();
        if (dt > 0f)
        {
            remaining = Mathf.Max(0f, remaining - dt);
            float phase = (duration - remaining) * frequency * Mathf.PI * 2f;
            offset = new Vector2(Mathf.Sin(phase), Mathf.Sin(phase * 1.3f))
                * (amplitude * remaining / duration * Screen.height / 1080f);
        }
        if (!IsShaking || view == null) return;
        CollectHud();
        baseProjection = view.projectionMatrix;
        var projection = baseProjection;
        // 원근 거리와 무관하게 배경·적·HUD를 같은 화면 픽셀만큼 이동한다.
        projection.m02 -= offset.x * 2f / view.pixelWidth;
        projection.m12 -= offset.y * 2f / view.pixelHeight;
        view.projectionMatrix = projection;
        if (hud != null) hud.anchoredPosition = offset / canvas.scaleFactor;
        applied = true;
    }
    private void RestoreView()
    {
        if (!applied) return;
        if (view != null) view.projectionMatrix = baseProjection;
        if (hud != null) hud.anchoredPosition = Vector2.zero;
        applied = false;
    }
    public void Clear()
    {
        RestoreView();
        offset = Vector2.zero;
        remaining = 0f;
    }
}
'''

def replace(n,a,b):
    assert texts[n].count(a)==1,(n,a)
    texts[n]=texts[n].replace(a,b,1)

n=names[1]
replace(n,'    int shownStage = -1;', '''    [SerializeField, Min(0f)] float circleDegreesPerSecond = 8f;
    [SerializeField, Min(.1f)] float circleGlowPeriod = 3f;
    [SerializeField, ColorUsage(true, true)] Color circleGlow = new Color(1.25f, .5f, 2f, 1f);
    Renderer circleRenderer;
    MaterialPropertyBlock circleProperties;
    Quaternion circleRotation;
    Player player;
    float circleAngle, glowTime;
    int shownStage = -1;''')
replace(n,'        Refresh(0f);\n    }', '''        circleRotation = magicCircle.localRotation;
        circleRenderer = magicCircle.GetComponent<Renderer>();
        circleProperties = new MaterialPropertyBlock();
        player = FindFirstObjectByType<Player>();
        Refresh(0f);
    }

    void OnEnable() { if (player) player.BattleReset += ResetCircleMotion; }
    void OnDisable()
    {
        if (player) player.BattleReset -= ResetCircleMotion;
        if (magicCircle) magicCircle.localRotation = circleRotation;
        if (circleRenderer) circleRenderer.SetPropertyBlock(null);
    }
    void ResetCircleMotion()
    {
        circleAngle = glowTime = 0f;
        if (magicCircle) magicCircle.localRotation = circleRotation;
    }''')
replace(n,'height * ImageAspect * cover, height * cover, 1f', 'height * ImageAspect * cover * 1.04f, height * cover * 1.04f, 1f')
replace(n,'            shownStage = stage;', '            shownStage = stage;\n            ResetCircleMotion();')
replace(n,'        magicCircle.localScale = new Vector3(diameter, diameter, 1f);', '''        magicCircle.localScale = new Vector3(diameter, diameter, 1f);
        if (stage == 3)
        {
            float dt = Mathf.Max(0f, deltaTime);
            circleAngle = Mathf.Repeat(circleAngle + dt * circleDegreesPerSecond, 360f);
            glowTime = Mathf.Repeat(glowTime + dt, circleGlowPeriod);
            // カ메라를 향한 평면의 로컬 -Z 회전은 화면에서 시계방향.
            magicCircle.localRotation = circleRotation * Quaternion.Euler(0, 0, -circleAngle);
            Color glow = circleGlow * (1f + .15f * Mathf.Sin(glowTime / circleGlowPeriod * Mathf.PI * 2f));
            glow.a = circleGlow.a;
            circleProperties.SetColor("_BaseColor", glow);
            circleRenderer.SetPropertyBlock(circleProperties);
        }'''.replace('カ메라','카메라'))

n=names[2]
replace(n,'bool tutorial=t.enabled;int passed=0;Vector3 home=shake.transform.localPosition;', '''bool tutorial=t.enabled;int passed=0;Vector3 home=shake.transform.localPosition;
        var camera=shake.GetComponent<Camera>();Matrix4x4 projection=camera.projectionMatrix;
        var hud=(RectTransform)Get(shake,"hud");''')
replace(n,'check(shake.transform.localPosition!=home,"camera displaced");\n            Vector3 stopped=shake.transform.localPosition;Call(shake,"Advance",0f);check(stopped==shake.transform.localPosition,"pause holds offset");', '''check(camera.projectionMatrix!=projection && hud!=null && hud.anchoredPosition.sqrMagnitude>1,"world and HUD displaced");
            check(shake.transform.localPosition==home,"camera pose preserved");
            Vector3 probe=home+shake.transform.forward*10;
            Vector3 shifted=camera.WorldToScreenPoint(probe);Vector2 hudOffset=hud.anchoredPosition*hud.GetComponentInParent<Canvas>().scaleFactor;
            Call(shake,"RestoreView");Vector3 neutral=camera.WorldToScreenPoint(probe);
            Call(shake,"Advance",0f);
            check(((Vector2)(shifted-neutral)-hudOffset).magnitude<.05f,"world and HUD same pixel offset");
            var stopped=camera.projectionMatrix;var stoppedHud=hud.anchoredPosition;
            Call(shake,"Advance",0f);check(stopped==camera.projectionMatrix&&stoppedHud==hud.anchoredPosition,"pause holds offset");''')
replace(n,'!shake.IsShaking && (shake.transform.localPosition-home).sqrMagnitude<1e-10f', '!shake.IsShaking && camera.projectionMatrix==projection && hud.anchoredPosition==Vector2.zero && (shake.transform.localPosition-home).sqrMagnitude<1e-10f')
replace(n,'!shake.IsShaking&&(shake.transform.localPosition-home).sqrMagnitude<1e-10f', '!shake.IsShaking&&camera.projectionMatrix==projection&&hud.anchoredPosition==Vector2.zero&&(shake.transform.localPosition-home).sqrMagnitude<1e-10f')

n=names[3]
replace(n,'            boss.TakeDamage(1600);', '''            var circleHome=circle.localRotation;
            env.Refresh(1f);
            check(Mathf.Abs(Mathf.DeltaAngle(circleHome.eulerAngles.z,circle.localEulerAngles.z)+8f)<.01f,"clockwise8 degrees per second");
            var block=new MaterialPropertyBlock();circle.GetComponent<Renderer>().GetPropertyBlock(block);
            Color glow=block.GetColor("_BaseColor");
            check(glow.b>glow.r&&glow.r>glow.g&&glow.b>1f,"purple additive glow");
            var pausedAngle=circle.localRotation;env.Refresh(0);
            circle.GetComponent<Renderer>().GetPropertyBlock(block);
            check(circle.localRotation==pausedAngle&&block.GetColor("_BaseColor")==glow,"pause holds rotation and glow");
            env.Refresh(.5f);
            check(Mathf.Abs(Mathf.DeltaAngle(circleHome.eulerAngles.z,circle.localEulerAngles.z)+12f)<.01f,"half-second rotation");
            boss.TakeDamage(1600);''')
replace(n,'            check(Mathf.Abs(circle.localScale.x-8)<.001f, "boss restart shrinks immediately");', '''            check(Mathf.Abs(circle.localScale.x-8)<.001f, "boss restart shrinks immediately");
            check(Quaternion.Angle(circleHome,circle.localRotation)<.01f,"restart resets rotation");''')
for n,p in paths.items(): assert p.read_bytes()==before[n],n+' changed concurrently'
for n,p in paths.items(): p.write_text(texts[n],encoding='utf-8',newline='\n')
print('Updated 2 runtime scripts + 2 existing checks; backups in evidence folder')
