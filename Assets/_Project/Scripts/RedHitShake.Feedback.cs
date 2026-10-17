using UnityEngine;

public partial class RedHitShake
{
    [Header("피격·기절 화면 효과 (시험값)")]
    [SerializeField] private Color healthHitColor = new Color(1f, .12f, .08f, 1f);
    [SerializeField] private Color shieldHitColor = new Color(.78f, .8f, .84f, 1f);
    [SerializeField] private Color stunScreenColor = new Color(.64f, .38f, 1f, 1f);
    [SerializeField, Min(.01f)] private float hitScreenDuration = .32f;
    [SerializeField, Min(.01f)] private float hitScreenCooldown = .35f;
    [SerializeField] private Vector2 healthHitOpacity = new Vector2(.28f, .52f);
    [SerializeField, Range(0f, 1f)] private float shieldHitOpacity = .22f;
    [SerializeField, Min(.01f)] private float shieldEdgeFadeIn = .12f;
    [SerializeField, Min(.01f)] private float shieldEdgeHold = .15f;
    [SerializeField, Min(.01f)] private float shieldEdgeFadeOut = .35f;
    [SerializeField, Range(0f, 1f)] private float stunScreenOpacity = .38f;
    [SerializeField, Min(.1f)] private float stunPulsePeriod = 1.8f;
    [SerializeField] private int screenFeedbackOrder = 90;
    private MainMenuUI feedbackMenu;
    private BattleFlow feedbackFlow;
    private CombatScreenOverlay hitOverlay, stunOverlay;
    private float hitScreenAge, hitScreenStrength, hitScreenGate, stunAge;
    private bool lastHitWasHealth;

    private void BuildScreenFeedback()
    {
        var root = new GameObject("Combat screen feedback", typeof(RectTransform), typeof(Canvas));
        root.transform.SetParent(transform, false);
        var overlayCanvas = root.GetComponent<Canvas>();
        overlayCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        overlayCanvas.sortingOrder = screenFeedbackOrder;
        stunOverlay = MakeScreenOverlay(root.transform, "Stun haze");
        hitOverlay = MakeScreenOverlay(root.transform, "Damage pulse");
    }

    private CombatScreenOverlay MakeScreenOverlay(Transform parent, string objectName)
    {
        var graphic = new GameObject(objectName, typeof(RectTransform), typeof(CanvasRenderer), typeof(CombatScreenOverlay)).GetComponent<CombatScreenOverlay>();
        graphic.transform.SetParent(parent, false);
        graphic.rectTransform.anchorMin = Vector2.zero;
        graphic.rectTransform.anchorMax = Vector2.one;
        graphic.rectTransform.sizeDelta = Vector2.zero;
        graphic.raycastTarget = false;
        graphic.color = Color.clear;
        return graphic;
    }

    private void OnScreenDamage(int hpLost, int absorbed)
    {
        if ((feedbackMenu != null && !feedbackMenu.ScreenFeedbackEnabled) || (hpLost <= 0 && absorbed <= 0)) return;
        bool health = hpLost > 0;
        // 같은 순간의 흡수 효과보다 실제 HP 손실을 우선하며, 연타는 잔광을 계속 재시작하지 않는다.
        if (hitScreenGate > 0f && (!health || lastHitWasHealth)) return;
        lastHitWasHealth = health;
        hitScreenStrength = health ? Mathf.Lerp(healthHitOpacity.x, healthHitOpacity.y,
            Mathf.Clamp01(hpLost / Mathf.Max(1f, player.MaxHp) * 5f)) : shieldHitOpacity;
        hitScreenAge = 0f;
        hitScreenGate = health ? hitScreenCooldown : shieldEdgeFadeIn + shieldEdgeHold + shieldEdgeFadeOut;
    }

    private void AdvanceScreenFeedback(float dt)
    {
        if (hitOverlay == null || stunOverlay == null) return;
        bool allowed = feedbackMenu == null || feedbackMenu.ScreenFeedbackEnabled;
        bool menu = feedbackFlow != null && feedbackFlow.MenuBlocksInput;
        bool battle = feedbackFlow == null || feedbackFlow.State == BattleFlowState.Fighting;
        if (!allowed || (!battle && (player == null || player.IsAlive))) { ClearScreenFeedback(); return; }
        hitScreenGate = Mathf.Max(0f, hitScreenGate - dt);
        hitScreenAge += dt;
        var hitColor = lastHitWasHealth ? healthHitColor : shieldHitColor;
        float envelope = lastHitWasHealth ? Mathf.Pow(Mathf.Clamp01(1f - hitScreenAge / hitScreenDuration), 2f)
            : Mathf.SmoothStep(0f, 1f, hitScreenAge / shieldEdgeFadeIn)
                * (1f - Mathf.SmoothStep(0f, 1f, (hitScreenAge - shieldEdgeFadeIn - shieldEdgeHold) / shieldEdgeFadeOut));
        hitOverlay.EdgeOnly = !lastHitWasHealth;
        hitColor.a = menu ? 0f : hitScreenStrength * envelope;
        hitOverlay.color = hitColor;
        bool stunned = battle && player != null && player.IsAlive && player.IsStunned;
        if (stunned) stunAge += dt; else stunAge = 0f;
        var stunColor = stunScreenColor;
        stunColor.a = stunned && !menu ? stunScreenOpacity * (.9f + .1f * Mathf.Sin(stunAge * Mathf.PI * 2f / stunPulsePeriod)) : 0f;
        stunOverlay.color = stunColor;
        stunOverlay.Wave = stunned ? stunAge / stunPulsePeriod : 0f;
    }

    private void ClearScreenFeedback()
    {
        hitScreenAge = hitScreenDuration;
        hitScreenStrength = hitScreenGate = stunAge = 0f;
        lastHitWasHealth = false;
        if (hitOverlay != null) hitOverlay.color = Color.clear;
        if (stunOverlay != null) stunOverlay.color = Color.clear;
    }
}
