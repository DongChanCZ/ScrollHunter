using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 3체를 관리하고, 발동이 겹치지 않도록 최소 간격을 강제한다.
/// 타겟 개념은 하나뿐이다. 분류를 보면 대상이 정해지므로 차단 전용 타겟을 두지 않는다.
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [SerializeField] private Player player;
    [SerializeField] private DeckSystem deck;

    [Tooltip("4단계 계측. 비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private CombatMetrics metrics;

    [Tooltip("비워두면 씬에서 자동으로 찾는다. 화면 배치 좌→우로 정렬된다.")]
    [SerializeField] private List<Enemy> enemies = new List<Enemy>();

    [Tooltip("적끼리 발동이 겹치지 않도록 강제하는 최소 간격(초)")]
    [SerializeField] private float minFireGap = 0.5f;

    [Header("전투 시작 배치")]
    [SerializeField] private Vector3 formationCenter = new Vector3(0f, 1f, 0f);
    [Min(0f)] [SerializeField] private float sideOffset = 3.2f;

    /// <summary>적 1~3번 자리의 가운데(2번 자리). 구역 전체를 덮는 연출의 기준점.</summary>
    public Vector3 FormationCenter => formationCenter;
    public float FormationSideOffset => sideOffset;

    [Header("캐스팅 3색")]
    [SerializeField] private Color greenColor = new Color(0.25f, 0.80f, 0.35f);
    [SerializeField] private Color orangeColor = new Color(1.00f, 0.60f, 0.10f);
    [SerializeField] private Color redColor = new Color(0.90f, 0.15f, 0.15f);

    [Header("타겟 조작")]
    [SerializeField] private KeyCode targetLeftKey = KeyCode.LeftArrow;
    [SerializeField] private KeyCode targetRightKey = KeyCode.RightArrow;

    [Tooltip("마우스 클릭으로 적을 고를 때 쓰는 레이 길이")]
    [SerializeField] private float clickRayDistance = 200f;

    private float lastFireTime = float.NegativeInfinity;
    private Camera cam;
    private bool combatEnded;

    public IReadOnlyList<Enemy> Enemies => enemies;

    /// <summary>현재 타겟. 죽으면 자동으로 다음 생존 적으로 옮겨간다.</summary>
    public Enemy CurrentTarget { get; private set; }

    public bool CombatEnded => combatEnded;
    public bool SkillInputBlocked
    {
        get
        {
            foreach (Enemy enemy in enemies)
                if (enemy != null && enemy.IsAlive && enemy.IsPhaseTransitioning) return true;
            return false;
        }
    }

    /// <summary>등록된 적 수. 계측 요약이 처치 수와 함께 쓴다.</summary>
    public int EnemyCount => enemies.FindAll(e => e == null || !e.IsBossOrb).Count;
    public int OrbsDestroyed { get; private set; }
    public void RecordOrbDestroyed() => OrbsDestroyed++;

    /// <summary>죽은 적 수.</summary>
    public int DeadCount
    {
        get
        {
            int dead = 0;
            for (int i = 0; i < enemies.Count; i++)
                if (enemies[i] == null || (!enemies[i].IsBossOrb && !enemies[i].IsAlive)) dead++;
            return dead;
        }
    }

    private BattleFlow flow;

    private void Awake()
    {
        flow = FindFirstObjectByType<BattleFlow>();
        cam = Camera.main;
        if (player == null) player = FindFirstObjectByType<Player>();
        if (deck == null) deck = FindFirstObjectByType<DeckSystem>();
        if (metrics == null) metrics = FindFirstObjectByType<CombatMetrics>();

        if (enemies.Count == 0)
        {
            enemies.AddRange(FindObjectsByType<Enemy>(FindObjectsSortMode.InstanceID));
        }

        // 방향키 이동 순서를 화면 배치 좌→우로 맞춘다.
        enemies.Sort((a, b) =>
        {
            if (a == null) return 1;
            if (b == null) return -1;
            return a.transform.position.x.CompareTo(b.transform.position.x);
        });

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            enemies[i].Initialize(this, player);
            enemies[i].RefreshBarColor();
        }

        CurrentTarget = FirstAlive();   // 전투 시작 시 1번 적 자동 지정
    }

    public void WaitForBattle()
    {
        combatEnded = true;
        CurrentTarget = null;
        foreach (Enemy enemy in enemies)
            if (enemy != null) enemy.SetEncounterActive(false);
    }

    public void BeginBattle(IList<Enemy> combatants, Enemy centerEnemy = null)
    {
        foreach (Enemy enemy in enemies)
            if (enemy != null) enemy.SetEncounterActive(false);
        enemies = new List<Enemy>(combatants);
        OrbsDestroyed = 0;
        // 3체는 지정한 강적을 중앙에, 나머지는 등록 순서대로 좌우에 둔다.
        if (enemies.Count == 3 && centerEnemy != null && enemies.Remove(centerEnemy))
            enemies.Insert(1, centerEnemy);
        for (int i = 0; i < enemies.Count; i++)
        {
            float offset = enemies.Count == 1 ? 0f : enemies.Count == 2
                ? (i == 0 ? -sideOffset : sideOffset) : (i - 1) * sideOffset;
            enemies[i].transform.position = formationCenter + Vector3.right * offset;
        }
        enemies.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
        combatEnded = false;
        lastFireTime = float.NegativeInfinity;
        foreach (Enemy enemy in enemies)
        {
            enemy.Initialize(this, player);
            enemy.BeginBattle();
        }
        CurrentTarget = FirstAlive();
    }

    internal void RegisterOrb(Enemy orb)
    {
        if (!enemies.Contains(orb)) enemies.Add(orb);
        enemies.Sort((a, b) => a.transform.position.x.CompareTo(b.transform.position.x));
    }

    public void ClearBossOrbs()
    {
        foreach (Enemy enemy in enemies)
        {
            if (enemy == null || enemy.IsBossOrb) continue;
            var orbs = enemy.GetComponent<BossOrbs>();
            if (orbs != null) orbs.Clear();
        }
    }

    private void Update()
    {
        if (combatEnded || (metrics != null && metrics.Ended) || (player != null && !player.IsAlive)) return;

        // 타겟이 죽었으면 자동으로 다음 생존 적으로
        if (CurrentTarget == null || !CurrentTarget.IsAlive) CurrentTarget = FirstAlive();

        if (CurrentTarget == null)
        {
            EndCombat();
            return;
        }

        if (Input.GetKeyDown(targetLeftKey)) StepTarget(-1);
        else if (Input.GetKeyDown(targetRightKey)) StepTarget(1);

        if (Input.GetMouseButtonDown(0)
            && !(UnityEngine.EventSystems.EventSystem.current != null
                && UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject())) TryClickTarget();
    }

    private Enemy FirstAlive()
    {
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && enemies[i].IsAlive) return enemies[i];
        return null;
    }

    /// <summary>좌우로 한 칸. 죽은 적은 건너뛴다.</summary>
    private void StepTarget(int direction)
    {
        int count = enemies.Count;
        if (count == 0) return;

        int start = enemies.IndexOf(CurrentTarget);
        if (start < 0) start = 0;

        for (int step = 1; step <= count; step++)
        {
            int i = ((start + direction * step) % count + count) % count;
            if (enemies[i] != null && enemies[i].IsAlive)
            {
                SelectTarget(enemies[i]);
                return;
            }
        }
    }

    private void TryClickTarget()
    {
        if (cam == null) return;

        Ray ray = cam.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, clickRayDistance)) return;

        Enemy clicked = hit.collider.GetComponentInParent<Enemy>();
        SelectTarget(clicked);
    }

    // 클릭·방향키 공통 경로. 시전·채널링 중 입력은 무시하고 저장하지 않는다.
    private void SelectTarget(Enemy target)
    {
        if ((flow != null && flow.IsCountingDown) || (deck != null && deck.IsCasting)) return;
        if (target != null && target.IsAlive) CurrentTarget = target;
    }

    /// <summary>살아있는 적 전체. 광역 카드가 쓴다.</summary>
    public List<Enemy> GetAliveEnemies()
    {
        var alive = new List<Enemy>();
        for (int i = 0; i < enemies.Count; i++)
            if (enemies[i] != null && enemies[i].IsAlive) alive.Add(enemies[i]);
        return alive;
    }

    /// <summary>적이 죽은 뒤 전원 사망인지 확인한다.</summary>
    public void NotifyEnemyDied()
    {
        if (combatEnded) return;

        // 죽은 적이 한 프레임이라도 타겟으로 남지 않게 여기서 바로 옮긴다.
        if (CurrentTarget == null || !CurrentTarget.IsAlive) CurrentTarget = FirstAlive();

        if (CurrentTarget == null) EndCombat();
    }

    private void EndCombat()
    {
        combatEnded = true;
        CurrentTarget = null;
        Debug.Log($"[{nameof(EnemyManager)}] 승리", this);

        // 방어도는 전투 종료 시 소멸한다.
        if (player != null) player.ClearShield();

        if (metrics != null) metrics.ReportVictory();
    }

    public Color GetCastColor(CastColor tone)
    {
        if (tone == CastColor.Orange) return orangeColor;
        if (tone == CastColor.Red) return redColor;
        return greenColor;
    }

    /// <summary>발동 허가. 직전 발동에서 minFireGap이 지나지 않았으면 false.</summary>
    public bool TryFire(Enemy requester)
    {
        if (Time.time - lastFireTime < minFireGap) return false;
        lastFireTime = Time.time;
        return true;
    }
}
