using System.Collections.Generic;
using System.Text;
using UnityEngine;

/// <summary>
/// 4단계 계측 (09 문서 4단계). 전투 동작과 밸런스는 전혀 건드리지 않고 집계만 한다.
///
/// 집계 정의는 09 문서 ②를 그대로 따른다. 헷갈리기 쉬운 것만 옮겨 적는다.
///  - 카드 사용: **입력이 수락되어 코스트와 카드가 나간 순간** 1회. 효과 적용 때 다시 세지 않는다
///  - 차단 시도: 차단 카드 입력이 **수락되면** 1회. 비캐스팅 대상에게 거절된 입력은 세지 않는다
///  - 차단 성공: **효과 적용 시 실제로 적 캐스팅을 취소한 경우**만 1회
///  - 방어 흡수: 방어도 부여량이 아니라 **실제 피격으로 방어도에서 깎인 피해량**의 합
///  - 코스트 부족 입력: 잠금·기절·대상 조건이 모두 통과하고 코스트만 모자라 거절된 키 누름
///
/// 시간은 <see cref="Time.time"/> 기준이라 배속이 반영되고 일시정지(timeScale 0) 동안은 멈춘다.
/// 승리 또는 패배가 확정되면 집계가 멈추고 요약은 **한 번만** 출력된다.
///
/// 「필요한 카드를 끌어오려고 쓴 사용」은 추정하지 않는다. 사용 순서와 시각만 남기고
/// 의도는 플레이어가 07 개발일지에 직접 메모한다.
/// </summary>
public class CombatMetrics : MonoBehaviour
{
    [SerializeField] private EnemyManager enemyManager;
    [SerializeField] private Player player;

    [Tooltip("카드 입력 한 건마다 로그를 남긴다. 끄면 전투 종료 요약만 출력한다.")]
    [SerializeField] private bool logEachInput = true;

    /// <summary>전투가 끝났는지. 끝나면 모든 집계가 멈춘다.</summary>
    public bool Ended { get; private set; }
    public int BattleNumber { get; private set; }

    [SerializeField] private string speedEntryFormat = "{0:0.00}s: {1:0.#}배";
    [SerializeField] private string speedSummaryFormat = "배속 이력(게임 초): {0}";
    [SerializeField] private string criticalHitLabel = " / 크리티컬";
    [SerializeField] private string criticalSummaryFormat = "크리티컬 {0}회 / 유효 피해 타격 {1}회 (사망 후 잔여 연출 제외)";
    [SerializeField] private string runModifiersFormat = "시작 조건: 충전 {0:0.0}/초 / 크리티컬 {1:0.#}% / {2}";
    [SerializeField] private string potionLogFormat = "[포션 {0:0.00}s] 실회복 {1:0} / 남은 {2}/{3}";
    [SerializeField] private string potionSummaryFormat = "포션 사용 {0}회 / 실제 회복 {1:0}";
    public int PotionUses { get; private set; }
    public float PotionHealing { get; private set; }
    public void RecordPotionUsed(float healed, int remaining, int maximum)
    {
        if (Ended) return;
        PotionUses++;
        PotionHealing += healed;
        Debug.Log(string.Format(potionLogFormat, Elapsed, healed, remaining, maximum), this);
    }
    private string runModifiers;
    public void RecordRunModifiers(float regeneration, float criticalChance, string passives, string resources = null)
    {
        if (!Ended) runModifiers = string.Format(runModifiersFormat, regeneration, criticalChance, passives)
            + (resources != null ? "\n" + resources : string.Empty);
    }

    private readonly List<string> battleSpeeds = new List<string>();

    private float startTime;
    private float endTime;
    private string pendingSummary;

    private int nextUseId = 1;
    private int totalUses;
    private int cancelledUses;

    // 카드별 사용 횟수. 덱 등록 순서대로 보이도록 키 순서를 따로 들고 있는다.
    private readonly List<string> cardOrder = new List<string>();
    private readonly Dictionary<string, int> cardUses = new Dictionary<string, int>();

    private int interruptAttempts;
    private int interruptSuccesses;

    private int shieldUses;
    private int shieldAbsorbed;

    private int costShortInputs;
    private float costWasted;

    public int DamageApplied { get; private set; }
    public int OverkillDamage { get; private set; }
    public int RecordedHits { get; private set; }
    public int CriticalHits { get; private set; }
    public int CriticalEligibleHits { get; private set; }

    /// <summary>전투 경과 시간(게임 내 초). 배속 반영, 일시정지 제외.</summary>
    public float Elapsed => (Ended ? endTime : Time.time) - startTime;

    private void Awake()
    {
        if (enemyManager == null) enemyManager = FindFirstObjectByType<EnemyManager>();
        if (player == null) player = FindFirstObjectByType<Player>();
    }

    private void Start()
    {
        if (BattleNumber == 0 && FindFirstObjectByType<BattleFlow>() == null) BeginBattle(1);
    }

    public void WaitForBattle()
    {
        // 시작 전에는 종료 시와 같은 입력 차단을 쓰되 승패·요약은 만들지 않는다.
        Ended = true;
        startTime = endTime = Time.time;
    }

    public void BeginBattle(int number)
    {
        FlushPendingSummary(); // 이전 전투 스냅샷은 새 집계 초기화 전에 마감.
        BattleNumber = number;
        Ended = false;
        startTime = Time.time;
        endTime = 0f;
        nextUseId = 1;
        totalUses = cancelledUses = interruptAttempts = interruptSuccesses = 0;
        shieldUses = shieldAbsorbed = costShortInputs = 0;
        costWasted = 0f;
        DamageApplied = OverkillDamage = RecordedHits = CriticalHits = CriticalEligibleHits = 0;
        battleSpeeds.Clear();
        runModifiers = null;
        PotionUses = 0;
        PotionHealing = 0f;
        cardOrder.Clear();
        cardUses.Clear();
    }

    public void RecordBattleSpeed(float speed)
    {
        if (Ended) return;
        battleSpeeds.Add(string.Format(speedEntryFormat, Elapsed, speed));
    }

    // ── 입력 계측 ──────────────────────────────────────────────

    /// <summary>
    /// 입력이 수락됐다. 코스트와 카드가 나간 순간에 한 번만 부른다.
    /// 반환하는 순서 번호로 나중에 결과를 이어 붙인다.
    /// </summary>
    /// <returns>순서 번호. 전투가 이미 끝났으면 0.</returns>
    public int RecordUseAccepted(string slotLabel, SkillData card, string targetLabel,
                                 float costBefore, float costAfter)
    {
        if (Ended || card == null) return 0;

        int id = nextUseId++;
        totalUses++;

        string name = card.DisplayName;
        if (!cardUses.ContainsKey(name))
        {
            cardUses[name] = 0;
            cardOrder.Add(name);
        }
        cardUses[name]++;

        if (card.Category == SkillCategory.Interrupt) interruptAttempts++;
        if (card.Category == SkillCategory.Shield) shieldUses++;

        if (logEachInput)
        {
            Debug.Log("[#" + id + " 입력 " + Elapsed.ToString("F2") + "s / " + slotLabel
                      + " / " + name + " / 코스트 " + card.Cost.ToString("0.#")
                      + " / " + targetLabel
                      + " / 보유 " + costBefore.ToString("F2") + "→" + costAfter.ToString("F2") + "]", this);
        }
        return id;
    }

    /// <summary>
    /// 같은 순서 번호에 효과 적용·차단 실패 결과를 이어 붙인다.
    /// 이 카드는 입력 시점에 이미 Ended가 아니었기에 수락된 것이므로(useId&gt;0),
    /// 그 결과를 적용하는 시점에 Ended로 바뀌어 있어도(예: 이 카드가 마지막 적을 잡아 승리 확정)
    /// 기록은 남긴다. 안 그러면 승패를 가른 바로 그 입력의 결과가 로그에서 빠진다.
    /// </summary>
    public void RecordUseResult(int useId, string resultLabel)
    {
        if (useId <= 0) return;
        if (!logEachInput) return;

        Debug.Log("[#" + useId + " 결과 " + Elapsed.ToString("F2") + "s / " + resultLabel + "]", this);
    }

    /// <summary>
    /// 기절로 캐스팅이 끊겼다. 사용 횟수에는 이미 들어가 있고 결과만 취소로 구분한다.
    /// RecordUseResult와 같은 이유로 Ended 여부와 무관하게 기록한다.
    /// </summary>
    public void RecordUseCancelled(int useId, string resultLabel)
    {
        cancelledUses++;
        if (!logEachInput || useId <= 0) return;

        Debug.Log("[#" + useId + " 취소 " + Elapsed.ToString("F2") + "s / " + resultLabel + "]", this);
    }

    /// <summary>새 시작 덱의 타격별 피해. 사망 뒤 잔여 연출은 실피해 0으로 구분.</summary>
    public void RecordHit(int useId, SkillData card, string target, int hitIndex,
        int displayed, int actual, int overkill, bool visualOnly, bool critical = false)
    {
        if (useId <= 0) return;
        DamageApplied += actual;
        OverkillDamage += overkill;
        RecordedHits++;
        if (!visualOnly && displayed > 0)
        {
            CriticalEligibleHits++;
            if (critical) CriticalHits++;
        }
        if (logEachInput) Debug.Log("[#" + useId + " 타격 " + Elapsed.ToString("F2") + "s / "
            + card.DisplayName + " / " + hitIndex + "타 / " + target + " / 표시 " + displayed
            + " / 실피해 " + actual + " / 과잉 " + overkill + (critical ? criticalHitLabel : "")
            + (visualOnly ? " / 잔여 연출" : "") + "]", this);
    }

    /// <summary>효과 적용 시 실제로 적 캐스팅을 취소했다.</summary>
    public void RecordInterruptSuccess()
    {
        if (Ended) return;
        interruptSuccesses++;
    }

    /// <summary>
    /// 잠금·기절·대상 조건은 통과했는데 코스트만 모자라 거절됐다.
    /// 키를 누른 순간에만 부른다. 누르고 있는 프레임 수로 세지 않는다.
    /// </summary>
    public void RecordCostShortInput()
    {
        if (Ended) return;
        costShortInputs++;
    }

    // ── 전투 중 누적 ──────────────────────────────────────────

    /// <summary>실제 피격에서 방어도가 흡수한 양. 방어도를 부여할 때가 아니라 맞을 때 부른다.</summary>
    public void RecordShieldAbsorbed(int amount)
    {
        if (Ended || amount <= 0) return;
        shieldAbsorbed += amount;
    }

    /// <summary>상한을 넘겨 버려진 충전량. 상한에 머문 시간과는 다른 값이다.</summary>
    public void RecordCostWasted(float amount)
    {
        if (Ended || amount <= 0f) return;
        costWasted += amount;
    }

    // ── 종료 ──────────────────────────────────────────────────

    public void ReportVictory() => Report(true);
    public void ReportDefeat() => Report(false);

    /// <summary>전투 종료 정리를 마친 호출자가 한 번 출력. 새 전투 값과 섞이지 않는다.</summary>
    public void FlushPendingSummary()
    {
        if (pendingSummary == null) return;
        string summary = pendingSummary;
        pendingSummary = null;
        Debug.Log(summary, this);
    }

    private void Report(bool won)
    {
        if (Ended) return;

        Ended = true;
        endTime = Time.time;

        var sb = new StringBuilder();
        sb.AppendLine("──────── 전투 종료 요약 ────────");
        sb.AppendLine("전투 번호: " + BattleNumber);
        sb.AppendLine("승패: " + (won ? "승리" : "패배"));
        sb.AppendLine("전투 시간: " + Elapsed.ToString("F2") + "초 (게임 내, 배속 반영·일시정지 제외)");
        sb.AppendLine(string.Format(speedSummaryFormat, string.Join(" → ", battleSpeeds)));
        if (runModifiers != null) sb.AppendLine(runModifiers);
        sb.AppendLine("남은 HP: " + (player != null ? player.CurrentHp.ToString("F0") + " / " + player.MaxHp.ToString("F0") : "-"));
        sb.AppendLine("처치 수: " + (enemyManager != null ? enemyManager.DeadCount + " / " + enemyManager.EnemyCount : "-"));
        sb.AppendLine();

        sb.AppendLine("카드 사용 " + totalUses + "회 (기절 취소 " + cancelledUses + "회 포함)");
        for (int i = 0; i < cardOrder.Count; i++)
        {
            string name = cardOrder[i];
            sb.AppendLine("  " + name + " : " + cardUses[name] + "회");
        }
        if (cardOrder.Count == 0) sb.AppendLine("  (없음)");
        sb.AppendLine();

        sb.AppendLine("타격별 기록 " + RecordedHits + "건 / 실피해 " + DamageApplied + " / 과잉 피해 " + OverkillDamage);
        sb.AppendLine(string.Format(criticalSummaryFormat, CriticalHits, CriticalEligibleHits));
        sb.AppendLine("차단 시도 " + interruptAttempts + "회 / 성공 " + interruptSuccesses + "회");
        sb.AppendLine("방어 카드 사용 " + shieldUses + "회 / 실제 방어도 흡수 " + shieldAbsorbed);
        sb.AppendLine(string.Format(potionSummaryFormat, PotionUses, PotionHealing));
        sb.AppendLine("코스트 부족으로 거절된 입력 " + costShortInputs + "회");
        sb.AppendLine("상한 초과로 버린 코스트 " + costWasted.ToString("F2"));
        sb.AppendLine("────────────────────────────");

        // 승패·시간·집계는 지금 고정하고, 마지막 행동 로그 뒤에 출력한다.
        pendingSummary = sb.ToString();
    }
}
