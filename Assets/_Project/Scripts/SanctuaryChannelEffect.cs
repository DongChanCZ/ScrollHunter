using UnityEngine;

/// <summary>시작 즉시 무적·기절 면역. 모든 종료 경로에서 함께 해제.</summary>
[CreateAssetMenu(fileName = "Channel_Sanctuary", menuName = "ScrollHunter/Channel Sanctuary")]
public class SanctuaryChannelEffect : ChannelEffect
{
    public override bool IsValidFor(SkillData skill)
        => skill.Category == SkillCategory.Shield && !skill.IsAreaOfEffect
            && skill.Damage == 0 && skill.ShieldAmount == 0;

    public override void Begin(ChannelContext context)
    {
        if (context.Player == null) throw new System.InvalidOperationException("Sanctuary requires Player.");
        context.Player.SetSanctuary(true);
    }

    public override void Tick(ChannelContext context, float deltaTime) { }

    public override void End(ChannelContext context, ChannelEndReason reason)
    {
        if (context.Player != null) context.Player.SetSanctuary(false);
    }
}
