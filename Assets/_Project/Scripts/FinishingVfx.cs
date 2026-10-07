using UnityEngine;

/// <summary>이미 발동한 표시만 전투 종료 뒤 실제 시간으로 재생한다.</summary>
public static class FinishingVfx
{
    public static void PlayUnscaled(GameObject effect)
    {
        if (effect == null) return;
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
        {
            var main = ps.main;
            main.useUnscaledTime = true;
        }
        var blast = effect.GetComponent<ScreenBlastVfx>();
        if (blast != null) blast.PlayOutUnscaled();
    }
    public static bool IsAlive(GameObject effect)
    {
        if (effect == null || !effect.activeInHierarchy) return false;
        var blast = effect.GetComponent<ScreenBlastVfx>();
        if (blast != null && blast.Remaining > 0f) return true;
        foreach (var ps in effect.GetComponentsInChildren<ParticleSystem>())
            if (ps.IsAlive(false)) return true;
        return false;
    }
}
