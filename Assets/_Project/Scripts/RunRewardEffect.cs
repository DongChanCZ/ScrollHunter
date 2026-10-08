using System;
using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 덱 교체 없이 즉시 적용하는 런 보상. 지속 증가분과 일회 회복을 구분한다.
/// Apply는 획득별 복제본에서 호출된다. Remove는 부분 적용 실패·재시작·런 폐기 시에도
/// 자신이 부여한 상태만 해제해야 한다. 전투 시작 시 초기화되는 임시 상태와 구분할 것.
/// </summary>
public abstract class RunRewardEffect : ScriptableObject
{
    [SerializeField] private string displayName;
    [Tooltip("조건·주의 문구(예: 현재 HP는 회복하지 않음). 효과 수치는 DescribeEffect가 효과 데이터에서 표시하므로 적지 않는다.")]
    [SerializeField, TextArea] private string description;
    [Tooltip("같은 보상의 획득 상한. 0이면 상한 없음.")]
    [SerializeField, Min(0)] private int maxStacks;
    [SerializeField] private bool rareReward;
    [SerializeField, Range(0f, 1f)] private float offerChance = 0.01f;
    public bool IsRareReward => rareReward;
    public float OfferChance => Mathf.Clamp01(offerChance);
    public int MaxStacks => maxStacks;
    public string DisplayName => displayName;
    public string Description => description;
    public virtual bool CanOffer(Player player, CostSystem cost) => true;
    /// <summary>효과 목록. 수치는 효과 데이터에서 읽고, 문구·단위는 화면이 정한다(format(능력치, 수치)).</summary>
    public virtual string DescribeEffect(Func<RewardStat, float, string> format, string separator) => string.Empty;
    public abstract void Apply(Player player, CostSystem cost);
    public abstract void Remove();
}

/// <summary>카드 또는 즉시 획득 효과 중 하나만 연결한다. 화면은 종류별 구현을 알 필요가 없다.</summary>
[Serializable]
public class BattleRewardOption
{
    [SerializeField] private SkillData card;
    [SerializeField] private RunRewardEffect effect;
    [Tooltip("계승 저장 ID. 최초 연결 후 이름을 바꿔도 이 값은 유지한다.")]
    [SerializeField] private string inheritanceId;
    [SerializeField] private bool inheritable = true;
    public string InheritanceId => inheritanceId;
    public bool Inheritable => inheritable;
    public SkillData Card => card;
    public RunRewardEffect Effect => effect;
    public string DisplayName => card != null ? card.DisplayName : effect != null ? effect.DisplayName : string.Empty;
    public string Describe(CombatInfoUI info) => card != null ? info.DescribeCard(card)
        : effect != null ? effect.DisplayName + "\n" + effect.Description : string.Empty;

    public bool CanOffer(List<SkillData> deck, Player player, CostSystem cost)
    {
        if ((card != null) == (effect != null)) return false;
        return card != null ? card.HasValidChannel && !deck.Contains(card) : effect.CanOffer(player, cost);
    }
}
