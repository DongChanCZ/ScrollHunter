using System;
using UnityEngine;

// 기존 에셋의 0·1 인덱스를 유지한다.
public enum RewardStat
{
    CostRegeneration, CriticalChance, MaximumHp, StartingCost, MaximumCost,
    ShieldGain, InterruptStagger, PotionCapacity, Healing, Potions
}

/// <summary>획득별 복제본이 자신이 더한 값만 제거한다. 즉시 회복은 제거 시 되돌리지 않는다.</summary>
[CreateAssetMenu(menuName = "Scroll Hunter/Stat Reward")]
public class StatRewardEffect : RunRewardEffect
{
    [Serializable]
    public struct Bonus
    {
        public RewardStat stat;
        [Min(0f)] public float amount;
    }
    [SerializeField] private RewardStat stat;
    [SerializeField, Min(0f)] private float amount;
    [Tooltip("복합 패시브의 추가 효과. 방어도 증가는 0.2=20%, 치명타 확률은 5=5%p.")]
    [SerializeField] private Bonus[] additionalBonuses = Array.Empty<Bonus>();
    private Player recipient;
    private CostSystem resource;
    private int appliedCount;
    private int BonusCount => 1 + (additionalBonuses != null ? additionalBonuses.Length : 0);
    private Bonus GetBonus(int index) => index == 0 ? new Bonus { stat = stat, amount = amount } : additionalBonuses[index - 1];

    public override bool CanOffer(Player player, CostSystem cost)
    {
        for (int i = 0; i < BonusCount; i++)
        {
            Bonus b = GetBonus(i);
            if (!Enum.IsDefined(typeof(RewardStat), b.stat) || float.IsNaN(b.amount)
                || float.IsInfinity(b.amount) || b.amount <= 0f) return false;
            bool needsCost = b.stat == RewardStat.CostRegeneration || b.stat == RewardStat.StartingCost || b.stat == RewardStat.MaximumCost;
            if (needsCost ? cost == null : player == null || !player.IsAlive) return false;
            if ((b.stat == RewardStat.PotionCapacity || b.stat == RewardStat.Potions) && b.amount != Mathf.Floor(b.amount)) return false;
        }
        return true;
    }

    public override void Apply(Player player, CostSystem cost)
    {
        if (appliedCount > 0) return;
        if (!CanOffer(player, cost)) throw new InvalidOperationException("Stat reward has no valid recipient or amount.");
        recipient = player;
        resource = cost;
        while (appliedCount < BonusCount)
        {
            Change(GetBonus(appliedCount), false);
            appliedCount++;
        }
    }

    public override void Remove()
    {
        while (appliedCount > 0) Change(GetBonus(--appliedCount), true);
        recipient = null;
        resource = null;
    }

    private void Change(Bonus bonus, bool removing)
    {
        float value = removing ? -bonus.amount : bonus.amount;
        switch (bonus.stat)
        {
            case RewardStat.CostRegeneration: if (resource != null) resource.AddRegenerationBonus(value); break;
            case RewardStat.StartingCost: if (resource != null) resource.AddStartingCostBonus(value); break;
            case RewardStat.MaximumCost: if (resource != null) resource.AddMaximumCostBonus(value); break;
            case RewardStat.CriticalChance: if (recipient != null) recipient.AddCriticalChanceBonus(value); break;
            case RewardStat.MaximumHp: if (recipient != null) recipient.AddMaxHpBonus(value); break;
            case RewardStat.ShieldGain: if (recipient != null) recipient.AddShieldGainBonus(value); break;
            case RewardStat.InterruptStagger: if (recipient != null) recipient.AddInterruptStaggerBonus(value); break;
            case RewardStat.PotionCapacity: if (recipient != null) recipient.AddPotionCapacityBonus((int)value); break;
            case RewardStat.Healing: if (!removing && recipient != null) recipient.Heal(value); break;
            case RewardStat.Potions: if (!removing && recipient != null) recipient.RefillPotions((int)value); break;
        }
    }
}
