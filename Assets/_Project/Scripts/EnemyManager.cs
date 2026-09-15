using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 적 3체를 관리하고, 발동이 겹치지 않도록 최소 간격을 강제한다.
/// 3색 Color도 여기 한 곳에서만 정의한다.
/// </summary>
public class EnemyManager : MonoBehaviour
{
    [SerializeField] private Player player;

    [Tooltip("비워두면 씬에서 자동으로 찾는다.")]
    [SerializeField] private List<Enemy> enemies = new List<Enemy>();

    [Tooltip("적끼리 발동이 겹치지 않도록 강제하는 최소 간격(초)")]
    [SerializeField] private float minFireGap = 0.5f;

    [Header("캐스팅 3색")]
    [SerializeField] private Color greenColor = new Color(0.25f, 0.80f, 0.35f);
    [SerializeField] private Color orangeColor = new Color(1.00f, 0.60f, 0.10f);
    [SerializeField] private Color redColor = new Color(0.90f, 0.15f, 0.15f);

    private float lastFireTime = float.NegativeInfinity;

    public IReadOnlyList<Enemy> Enemies => enemies;

    private void Awake()
    {
        if (player == null) player = FindFirstObjectByType<Player>();

        if (enemies.Count == 0)
        {
            enemies.AddRange(FindObjectsByType<Enemy>(FindObjectsSortMode.InstanceID));
        }

        for (int i = 0; i < enemies.Count; i++)
        {
            if (enemies[i] == null) continue;
            enemies[i].Initialize(this, player);
            enemies[i].RefreshBarColor();
        }
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
