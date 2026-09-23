using System;
using UnityEngine;

public enum RewardStat { CostRegeneration, CriticalChance }

/// <summary>획득별 복제본이 자신이 더한 값만 제거한다. 원본 에셋과 기본 능력치는 바꾸지 않는다.</summary>
[CreateAssetMenu(menuName = "Scroll Hunter/Stat Reward")]
public class StatRewardEffect : RunRewardEffect
{
    [SerializeField] private RewardStat stat;
    [SerializeField, Min(0f)] private float amount;
    private Player recipient;
    private CostSystem resource;
    private bool applied;

    public override bool CanOffer(Player player, CostSystem cost) => amount > 0f
        && (stat == RewardStat.CostRegeneration ? cost != null : player != null);

    public override void Apply(Player player, CostSystem cost)
    {
        if (applied) return;
        if (!CanOffer(player, cost)) throw new InvalidOperationException("Stat reward has no valid recipient or amount.");
        recipient = player;
        resource = cost;
        if (stat == RewardStat.CostRegeneration) resource.AddRegenerationBonus(amount);
        else recipient.AddCriticalChanceBonus(amount);
        applied = true;
    }

    public override void Remove()
    {
        if (!applied) return;
        if (stat == RewardStat.CostRegeneration)
        {
            if (resource != null) resource.AddRegenerationBonus(-amount);
        }
        else if (recipient != null) recipient.AddCriticalChanceBonus(-amount);
        applied = false;
        recipient = null;
        resource = null;
    }
}
