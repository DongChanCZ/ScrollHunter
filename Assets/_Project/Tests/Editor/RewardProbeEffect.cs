// Editor 검사 전용. 플레이용 패시브나 에셋을 추가하지 않는다.
public class RewardProbeEffect : RunRewardEffect
{
    public static int Applied, Removed;
    public static RunRewardEffect Received;
    public override void Apply(Player player, CostSystem cost) { Applied++; Received = this; }
    public override void Remove() { Removed++; }
}

