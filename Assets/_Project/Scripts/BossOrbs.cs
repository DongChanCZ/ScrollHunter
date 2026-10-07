using System.Collections.Generic;
using UnityEngine;

/// <summary>A43. 두 오브의 생존 상태만 관리한다. 공격 강화는 Enemy가 시전 시작 때 저장한다.
/// 10/7 연출: 오브 데이터의 외형(BossOrbVisual)을 오브 자식으로 붙이고, 실제 파괴(Died)일 때만 분리된 파괴 연출(OrbBreakVfx)을 만든다.
/// 표시는 판정·활성화 시점을 늦추지 않는다.</summary>
public sealed class BossOrbs : MonoBehaviour
{
    [Header("임시 구체 표시 (외형 프리팹이 없을 때)")]
    [SerializeField] private float heightOffset = .6f;
    [SerializeField] private float previewSize = .6f;
    [SerializeField] private float activeSize = 1.1f;
    [SerializeField] private float barHeight = .85f;
    [SerializeField] private Color ruinColor = new Color(.45f, .08f, .16f);
    [SerializeField] private Color cycleColor = new Color(.15f, .22f, .65f);
    private Enemy boss;
    private EnemyManager manager;
    private Player player;
    private bool initialShown, initialPending;
    private readonly List<OrbBreakVfx> breaks = new List<OrbBreakVfx>();
    public Enemy Ruin { get; private set; }
    public Enemy Cycle { get; private set; }
    public bool RuinActive => Ruin != null && Ruin.IsAlive && Ruin.gameObject.activeSelf;
    public bool CycleActive => Cycle != null && Cycle.IsAlive && Cycle.gameObject.activeSelf;
    private bool CanSpawn => boss != null && boss.IsAlive && boss.gameObject.activeInHierarchy
        && (manager == null || !manager.CombatEnded) && (player == null || player.IsAlive);

    internal void Initialize(Enemy owner, EnemyManager enemies, Player target)
    {
        boss = owner; manager = enemies; player = target;
        RemoveEffects();   // 재시작: 이전 전투의 파괴 연출을 남기지 않는다
    }

    internal void BeginTransition()
    {
        if (initialShown || !CanSpawn) return;
        initialShown = initialPending = true;
        EnsureOrbs();
        ShowPreview(Ruin); ShowPreview(Cycle);
        Debug.Log("[보스 오브] 최초 소환 준비 — 전환 종료 때 활성화", this);
    }

    private void EnsureOrbs()
    {
        // 판정용 구체(충돌체·Enemy)는 그대로 두고, 외형 프리팹이 있으면 임시 구체 그림만 숨긴다.
        if (Ruin == null)
        {
            Ruin = boss.CreateOrb(boss.Data.RuinOrb, boss.transform.position + Vector3.left * manager.FormationSideOffset + Vector3.up * heightOffset,
                ruinColor, barHeight);
            Ruin.Died += RuinDestroyed;
            AttachVisual(Ruin);
        }
        if (Cycle == null)
        {
            Cycle = boss.CreateOrb(boss.Data.CycleOrb, boss.transform.position + Vector3.right * manager.FormationSideOffset + Vector3.up * heightOffset,
                cycleColor, barHeight);
            Cycle.Died += CycleDestroyed;
            AttachVisual(Cycle);
        }
    }

    private static void AttachVisual(Enemy orb)
    {
        if (orb.Data.OrbVisual == null) return;
        var visual = Instantiate(orb.Data.OrbVisual, orb.transform, false);
        visual.name = orb.Data.OrbVisual.name;
        orb.GetComponent<Renderer>().enabled = false;
    }

    private static BossOrbVisual Visual(Enemy orb) => orb != null ? orb.GetComponentInChildren<BossOrbVisual>(true) : null;

    private void ShowPreview(Enemy orb)
    {
        orb.HideOrb();
        orb.gameObject.SetActive(true);
        var visual = Visual(orb);
        // 외형이 있으면 판정 크기(activeSize)로 두고 성장은 시각 자식만 맡는다. 준비 중 충돌체는 꺼져 있다.
        orb.transform.localScale = Vector3.one * (visual != null ? activeSize : previewSize);
        if (visual != null) visual.BeginInitialSummon(boss.Data.PhaseTransitionSeconds);
    }

    internal void ActivateInitial()
    {
        if (!initialPending || !CanSpawn) return;
        initialPending = false;
        Activate(Ruin, false); Activate(Cycle, false);
    }

    internal void RestoreOne()
    {
        if (!CanSpawn || boss.PhaseNumber != 3) return;
        EnsureOrbs();
        if (!RuinActive) Activate(Ruin, true);
        else if (!CycleActive) Activate(Cycle, true);
    }

    private void Activate(Enemy orb, bool respawn)
    {
        orb.transform.localScale = Vector3.one * activeSize;
        orb.BeginBattle();
        // 표시만: 최초는 전환 동안 자란 구체의 완성 맥동, 재소환은 활성 상태에서 시각 요소만 짧게 성장.
        var visual = Visual(orb);
        if (visual != null) { if (respawn) visual.BeginRespawn(); else visual.Complete(); }
        orb.GetComponent<Collider>().enabled = true;
        if (manager != null) manager.RegisterOrb(orb);
        Debug.Log("[보스 오브] " + orb.Data.DisplayName + " 활성화 / HP " + orb.CurrentHp, this);
    }

    private void RuinDestroyed() => OnOrbDestroyed(Ruin);
    private void CycleDestroyed() => OnOrbDestroyed(Cycle);
    private void OnOrbDestroyed(Enemy orb)
    {
        SpawnBreak(orb);   // Died는 실제 피해로 HP가 0이 될 때만 온다(정리 제거 HideOrb는 Died 없음)
        if (!CanSpawn) return;
        if (manager != null) manager.RecordOrbDestroyed();
        int before = boss.CurrentHp;
        boss.TakeDamage(boss.Data.OrbDestructionDamage);
        Debug.Log("[보스 오브] " + orb.Data.DisplayName + " 파괴 / 보스 고정 피해 "
            + (before - boss.CurrentHp) + (boss.IsInvulnerable ? " (전환 무적)" : ""), this);
        // 카드/채널링의 기존 종료 경로가 마지막 효과 로그 뒤에 승리 요약을 마감한다.
    }

    /// <summary>오브는 꺼진 직후 비활성화되므로 파괴 연출은 오브 밖에 만든다. 시작 크기는 지금 보이는 구체 크기.</summary>
    private void SpawnBreak(Enemy orb)
    {
        if (orb == null || orb.Data.OrbBreak == null) return;
        var visual = Visual(orb);
        var fx = Instantiate(orb.Data.OrbBreak, orb.transform.position, Quaternion.identity);
        var effect = fx.GetComponent<OrbBreakVfx>();
        if (effect == null) { Destroy(fx); return; }
        effect.Begin(visual != null ? visual.WorldScale : orb.transform.lossyScale.x);
        breaks.RemoveAll(b => b == null);
        breaks.Add(effect);
    }

    public int ActiveBreakCount { get { breaks.RemoveAll(b => b == null); return breaks.Count; } }

    /// <summary>보스 사망·전투 종료: 남은 오브를 파괴 연출 없이 지운다. 이미 시작한 파괴 연출은 실제 시간으로 마저 끝낸다.</summary>
    public void Clear()
    {
        initialShown = initialPending = false;
        if (Ruin != null) Ruin.HideOrb();
        if (Cycle != null) Cycle.HideOrb();
        foreach (var b in breaks) if (b != null) b.PlayOutUnscaled();
    }

    /// <summary>재시작·전투 편성 끄기: 재생 중인 파괴 연출까지 즉시 지운다.</summary>
    public void RemoveEffects()
    {
        foreach (var b in breaks) if (b != null) Destroy(b.gameObject);
        breaks.Clear();
    }

    private void OnDisable() => Clear();
    private void OnDestroy()
    {
        RemoveEffects();
        if (Ruin != null) { Ruin.Died -= RuinDestroyed; Destroy(Ruin.gameObject); }
        if (Cycle != null) { Cycle.Died -= CycleDestroyed; Destroy(Cycle.gameObject); }
    }
}
