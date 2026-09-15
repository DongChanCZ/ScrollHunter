using UnityEngine;

/// <summary>
/// 피해 계산은 여기 한 곳에서만 한다. 다른 곳에서 직접 계산하지 말 것.
/// </summary>
public static class DamageFormula
{
    /// <summary>방어력 공식의 상수. 방어력 20 → 감소율 20/120.</summary>
    public const float DefenseConstant = 100f;

    /// <summary>
    /// 최종피해 = FLOOR( 피해량 × 타수 × 100 / (대상 방어력 + 100) × 크리배율 + 0.5 )
    ///
    /// 곱셈을 먼저 하고 마지막에 한 번만 정수화한다.
    /// (1 - 방어력/(방어력+100)) 형태로 쓰면 부동소수점 오차로 25가 24가 된다.
    ///
    /// 반올림에 Mathf.RoundToInt를 쓰지 않는다. 그쪽은 은행가 반올림이라
    /// .5에서 짝수로 붙는다(2.5 → 2). FloorToInt(x + 0.5f)는 항상 위로 붙는다(2.5 → 3).
    /// </summary>
    /// <param name="damage">피해량(절대값). 0 이하면 피해가 없는 카드이므로 0을 돌려준다.</param>
    /// <param name="hitCount">타수. 1 미만은 1로 본다.</param>
    /// <param name="targetDefense">대상의 방어력.</param>
    /// <param name="critMultiplier">크리티컬 시 1.8, 아니면 1.0.</param>
    public static int Compute(int damage, int hitCount, float targetDefense, float critMultiplier = 1f)
    {
        int hits = Mathf.Max(1, hitCount);
        float raw = damage * hits * DefenseConstant / (targetDefense + DefenseConstant) * critMultiplier;

        if (raw <= 0f) return 0;

        // 최소 피해 1. 방어력이 아무리 높아도 0이 되지 않는다.
        return Mathf.Max(1, Mathf.FloorToInt(raw + 0.5f));
    }
}
