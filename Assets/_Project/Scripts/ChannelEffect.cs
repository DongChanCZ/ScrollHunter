using UnityEngine;

/// <summary>종료 이유와 무관하게 유지 효과는 End에서 해제한다.</summary>
public enum ChannelEndReason
{
    Completed,
    Stunned,
    CombatEnded,
    DeckReset,
    Disabled,
    Error,
}

/// <summary>효과가 사용할 전투 참조. 타겟 정책은 개별 효과가 정의한다.</summary>
public sealed class ChannelContext
{
    public SkillData Skill { get; }
    public Player Player { get; }
    public EnemyManager Enemies { get; }

    public ChannelContext(SkillData skill, Player player, EnemyManager enemies)
    {
        Skill = skill;
        Player = player;
        Enemies = enemies;
    }
}

/// <summary>
/// 채널링 효과 연결점. 사용마다 에셋을 복제하므로 타이머 등은 복제본 필드에 보관한다.
/// Begin: 즉시 시작. Tick: 진행한 게임 시간만 전달. End: 모든 종료 경로에서 한 번 정리.
/// 피해/방어/무적/면역/틱 간격은 이 기반 클래스가 자동으로 부여하지 않는다.
/// </summary>
public abstract class ChannelEffect : ScriptableObject
{
    [SerializeField, TextArea] private string description = "";
    public virtual string Describe(SkillData skill) => description;

    public abstract void Begin(ChannelContext context);
    public abstract void Tick(ChannelContext context, float deltaTime);
    public abstract void End(ChannelContext context, ChannelEndReason reason);
}
