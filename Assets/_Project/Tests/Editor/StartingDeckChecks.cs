using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

/// <summary>저장 카드로 효과·시점·중단을 확인한다. Play 검사 후 실제 첫 전투로 복귀.</summary>
public static class StartingDeckChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private const string Folder = "Assets/_Project/Data/ScrollHunter/SkillData/";
    private static int passed;
    private static BattleFlow flow;
    private static DeckSystem deck;
    private static EnemyManager enemies;
    private static Player player;
    private static CostSystem cost;
    private static CombatMetrics metrics;
    private static Enemy a, b, c;
    private static SkillData fire;

    [MenuItem("Tools/Scroll Hunter/Check Starting Deck (Play)")]
    public static void RunMenu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Battle Play에서 실행하세요.");
        flow = UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        deck = UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        enemies = UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        player = UnityEngine.Object.FindFirstObjectByType<Player>();
        cost = UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        metrics = UnityEngine.Object.FindFirstObjectByType<CombatMetrics>();
        var all = UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include, FindObjectsSortMode.None);
        a = all.Single(e => e.Data.name == "Enemy_A_Green");
        b = all.Single(e => e.Data.name == "Enemy_B_Orange");
        c = all.Single(e => e.Data.name == "Enemy_C_Red");
        fire = Load("Skill_SK01_Fireball");
        var pillar = Load("Skill_SK04_FirePillar");
        var silence = Load("Skill_Silence");
        var armor = Load("Skill_CrystalShield");
        var spark = Load("Skill_SK06_MagicSpark");
        var spear = Load("Skill_SK05_LightningSpear");
        var cutter = Load("Skill_SK03_MagicCutter");
        var ice = Load("Skill_SK02_IceArrow");
        var ui = UnityEngine.Object.FindFirstObjectByType<DamageNumberUI>();
        bool wasLogging = (bool)Get(metrics, "logEachInput");
        passed = 0;
        try
        {
            Set(metrics, "logEachInput", false);
            var actualDeck = ((System.Collections.Generic.List<SkillData>)Get(deck, "startingDeck")).ToArray();
            var expected = new[] { fire, pillar, silence, armor, spark, spear, cutter, ice };
            Check(actualDeck.SequenceEqual(expected), "saved starting order");
            Check(expected.Sum(card => card.Cost) == 24f, "starting deck cost 24");
            foreach (SkillData card in expected) Check(card.HasValidChannel, "valid asset " + card.DisplayName);

            flow.RestartRun();
            Check(player.MaxHp == 500f && player.Defense == 20f && cost.Current == 3f && cost.Max == 10f
                && Near((float)Get(cost, "regenPerSecond"), 0.8f), "player and cost baseline");
            Check(Near((float)Get(deck, "minGcd"), 0.4f) && Near(deck.InterruptStagger, 2.5f), "GCD and interrupt configuration");
            foreach (Enemy e in new[] { a, b, c })
            {
                EnemyAttack green = e.Data.Find(CastColor.Green);
                Check(e.MaxHp == 300 && e.Data.Defense == 0 && green.Damage == 30
                    && green.CastTime == 2.5f && green.StaggerAfterCast == 1f, "enemy green / HP / defense " + e.name);
            }
            var orange = b.Data.Find(CastColor.Orange); var red = c.Data.Find(CastColor.Red);
            Check(orange.Damage == 40 && orange.CastTime == 3f && orange.StaggerAfterCast == 1.5f
                && orange.StunSeconds == 2f, "orange saved values");
            Check(red.Damage == 150 && red.CastTime == 4f && red.StaggerAfterCast == 2f, "red saved values");
            Check(DamageFormula.Compute(30,1,20) == 25 && DamageFormula.Compute(40,1,20) == 33
                && DamageFormula.Compute(150,1,20) == 125 && DamageFormula.Compute(0,1,0) == 0
                && DamageFormula.Compute(1,1,9999) == 1, "damage rounding / zero / minimum");
            Reset(silence); b.TryInterrupt(2.5f); Call(b, "BeginNextCast");
            Check(b.CastColor == CastColor.Green, "interrupted orange followed by green");
            int greenCount = (int)Get(b, "greenCount"); b.TryInterrupt(2.5f);
            Check((int)Get(b, "greenCount") == greenCount, "interrupted green does not count as fired");
            Reset(fire); Call(c, "Fire"); Call(c, "BeginNextCast");
            Check(c.CastColor == CastColor.Green, "red followed by green");
            Call(c, "Fire"); Call(c, "BeginNextCast"); Check(c.CastColor == CastColor.Green, "red needs second green");
            Call(c, "Fire"); Call(c, "BeginNextCast"); Check(c.CastColor == CastColor.Red, "red returns after two greens");

            var zeroHits = UnityEngine.Object.Instantiate(cutter);
            try
            {
                Set(zeroHits, "hitCount", 0); Reset(zeroHits); deck.TryUseSlot(0);
                Check(b.CurrentHp == 270 && metrics.RecordedHits == 1, "zero hit count normalized to one");
            }
            finally { deck.SetDeck(null); UnityEngine.Object.DestroyImmediate(zeroHits); }
            var zeroChannel = UnityEngine.Object.Instantiate(ice);
            try
            {
                Set(zeroChannel, "hitCount", 0); Reset(zeroChannel); deck.TryUseSlot(0); Step(2.8f);
                Check(b.CurrentHp == 265 && metrics.RecordedHits == 1, "channel zero count normalized to one");
            }
            finally { deck.SetDeck(null); UnityEngine.Object.DestroyImmediate(zeroChannel); }

            Reset(fire);
            Check(deck.TryUseSlot(0), "fire accepted");
            Check(b.CurrentHp == 270 && !deck.IsCasting && Near(deck.LockRemaining, 0.4f), "fire immediate 30 / GCD");
            Check(cost.Current == 9f && (int)Get(metrics, "totalUses") == 1, "fire pays once");
            Check(!deck.TryUseSlot(1), "minimum GCD rejects input");
            Step(0.4f); Check(!deck.IsLocked, "minimum GCD ends");

            Reset(pillar, true);
            Check(deck.TryUseSlot(0) && cost.Current == 6f, "pillar cost");
            Check(deck.GetHandCard(0) == fire, "input cycles same slot");
            Step(0.99f); Check(a.CurrentHp == 300 && b.CurrentHp == 300 && c.CurrentHp == 300, "pillar waits for cast");
            a.TakeDamage(300); enemies.NotifyEnemyDied(); Step(0.011f);
            Check(a.CurrentHp == 0 && b.CurrentHp == 250 && c.CurrentHp == 250, "pillar alive targets only");
            Check(metrics.RecordedHits == 2 && metrics.DamageApplied == 100, "pillar records two targets / one use");
            Reset(pillar, true); deck.TryUseSlot(0); Step(1f);
            Check(a.CurrentHp == 250 && b.CurrentHp == 250 && c.CurrentHp == 250, "pillar three targets 150");

            Reset(spear); deck.TryUseSlot(0); Step(0.79f);
            Check(b.CurrentHp == 300 && cost.Current == 6f, "spear cost and pre-cast");
            Step(0.011f); Check(b.CurrentHp == 165 && c.CurrentHp == 300, "spear single 135");

            Reset(cutter); deck.TryUseSlot(0);
            Check(b.CurrentHp == 240 && metrics.RecordedHits == 2 && ui.ActiveCount == 2, "cutter two immediate numbers");
            Reset(cutter); SetHp(b, 20); deck.TryUseSlot(0);
            Check(!b.IsAlive && c.CurrentHp == 300 && ui.ActiveCount == 2, "cutter remaining visual never retargets");
            Check(metrics.DamageApplied == 20 && metrics.OverkillDamage == 40 && metrics.RecordedHits == 2, "cutter actual / overkill split");
            Reset(cutter); enemies.BeginBattle(new[] { b }); SetHp(b, 20); deck.TryUseSlot(0);
            Check(enemies.CombatEnded && metrics.RecordedHits == 2 && metrics.DamageApplied == 20
                && ui.ActiveCount == 2, "last enemy retains both visuals without later damage");

            Reset(ice); Check(deck.TryUseSlot(0) && cost.Current == 5f, "ice input pays once");
            Call(enemies, "StepTarget", 1); Check(enemies.CurrentTarget == c, "selection can change");
            Step(0.49f); Check(b.CurrentHp == 300 && c.CurrentHp == 300, "ice no early damage");
            Step(0.01f); Check(b.CurrentHp == 265 && c.CurrentHp == 300, "ice fixed input target first hit");
            for (int i = 0; i < 4; i++) Step(0.5f);
            Check(b.CurrentHp == 125 && metrics.RecordedHits == 5 && deck.IsChanneling
                && Near(deck.LockRemaining, 0.3f), "ice five hits then tail");
            Check(!deck.TryUseSlot(1), "ice tail blocks other cards");
            Step(0.301f); Check(!deck.IsCasting && !deck.IsLocked && metrics.RecordedHits == 5, "ice tail ends without sixth hit");
            Check(cost.Current == 5f && (int)Get(metrics, "totalUses") == 1 && deck.GetHandCard(0) == fire, "ice no repeat cost / cycle / uses");
            Reset(ice); deck.TryUseSlot(0); Step(2.8f);
            Check(b.CurrentHp == 125 && metrics.RecordedHits == 5 && !deck.IsLocked, "large frame catches five ice hits");

            Reset(ice); SetHp(b, 30); deck.TryUseSlot(0); Step(0.5f);
            Check(!b.IsAlive && !deck.IsCasting && Near(deck.LockRemaining, 0.3f) && c.CurrentHp == 300, "target death replaces channel with 0.3 recovery");
            Check(!deck.TryUseSlot(1), "death recovery rejects input"); Step(0.301f);
            Check(!deck.IsLocked && metrics.RecordedHits == 1, "death recovery ends and no more hits");
            Reset(ice); deck.TryUseSlot(0); b.TakeDamage(300); enemies.NotifyEnemyDied(); Step(0.1f);
            Check(!deck.IsCasting && Near(deck.LockRemaining, 0.3f), "external early death retains input minimum GCD");
            Step(0.301f); Check(deck.TryUseSlot(1), "input allowed after early death GCD");
            Reset(ice); SetHp(b, 30); deck.TryUseSlot(0); Step(1f);
            Check(!deck.IsLocked && metrics.RecordedHits == 1, "large frame accounts recovery elapsed after kill");

            Reset(spark, true); deck.TryUseSlot(0); Step(0.249f);
            Check(a.CurrentHp == 300 && b.CurrentHp == 300 && c.CurrentHp == 300, "spark no early hit");
            Step(0.001f); Check(a.CurrentHp == 295 && b.CurrentHp == 295 && c.CurrentHp == 295, "spark first 5 to all");
            for (int i = 0; i < 7; i++) Step(0.25f);
            Check(a.CurrentHp == 260 && b.CurrentHp == 260 && c.CurrentHp == 260
                && metrics.RecordedHits == 24 && Near(deck.LockRemaining, 0.2f), "spark eight hits each / tail");
            Step(0.201f); Check(!deck.IsLocked && metrics.RecordedHits == 24 && cost.Current == 7f, "spark completes no extra hit or cost");
            Reset(spark); SetHp(b, 5); deck.TryUseSlot(0); Step(2.2f);
            Check(!b.IsAlive && c.CurrentHp == 260 && metrics.RecordedHits == 9 && !deck.IsLocked, "spark continues on survivors");

            foreach (SkillData card in new[] { pillar, spear, ice, spark, armor, silence })
            {
                Reset(card); deck.TryUseSlot(0); player.ApplyStun(2f); Step(0.1f);
                Check(!deck.IsCasting && deck.LockRemaining == 0f && b.CurrentHp == 300
                    && player.Shield == 0 && cost.Current == 10f - card.Cost
                    && deck.GetHandCard(0) == fire, "pre-effect stun: " + card.DisplayName);
                Check(!deck.TryUseSlot(1), "stunned input rejected: " + card.DisplayName);
                typeof(Player).GetProperty("StunRemaining").SetValue(player, 0f);
                Check(deck.TryUseSlot(1), "no extra lock after stun: " + card.DisplayName);
            }
            foreach (SkillData card in new[] { ice, spark })
            {
                Reset(card); deck.TryUseSlot(0); Step(0.5f); int hp = b.CurrentHp;
                player.ApplyStun(2f); Step(1f);
                Check(hp < 300 && b.CurrentHp == hp && !deck.IsCasting, "partial channel damage retained: " + card.DisplayName);
                Reset(card); deck.TryUseSlot(0); Step(0.2f); float remain = deck.CastRemaining;
                Time.timeScale = 0f; Step(5f);
                Check(deck.CastRemaining == remain && metrics.RecordedHits == 0 && !deck.TryUseSlot(1), "pause freezes channel: " + card.DisplayName);
                Time.timeScale = 1f; Step(card.CastTime);
                Check(!deck.IsCasting && metrics.RecordedHits > 0, "resume completes channel: " + card.DisplayName);
                Reset(card); deck.TryUseSlot(0); Step(0.5f); flow.RestartRun(); Step(5f);
                Check(!deck.IsCasting && b.CurrentHp == 300 && cost.Current == 3f && metrics.RecordedHits == 0, "restart cancels old channel: " + card.DisplayName);
                Reset(card); deck.TryUseSlot(0); Step(0.5f); player.TakeDamage(10000); Step(1f);
                Check(!deck.IsCasting && metrics.Ended && !deck.TryUseSlot(1), "defeat cancels channel: " + card.DisplayName);
                Reset(card); enemies.BeginBattle(new[] { b }); SetHp(b, 1); deck.TryUseSlot(0); Step(card.CastTime);
                int hits = metrics.RecordedHits; Step(10f);
                Check(enemies.CombatEnded && !deck.IsCasting && deck.LockRemaining == 0f
                    && metrics.RecordedHits == hits && hits == 1, "victory cancels remaining hits: " + card.DisplayName);
            }

            Reset(armor); Check(armor.DisplayName == "매직아머" && deck.TryUseSlot(0) && player.Shield == 0, "armor renamed / delayed");
            Step(0.2f); Check(player.Shield == 120 && Near(deck.LockRemaining, 0.2f), "armor 120 and GCD");
            player.TakeDamage(150); Check(player.Shield == 0 && player.CurrentHp == 495, "armor red absorbs 120 / HP5");
            Reset(silence); deck.TryUseSlot(0); Step(0.2f);
            Check(b.IsStaggered && Near((float)Get(b, "staggerTimer"), 2.5f), "orange interrupt 2.5");
            Reset(silence); Call(enemies, "StepTarget", 1); deck.TryUseSlot(0); Step(0.2f);
            Check(c.IsCasting && !c.IsStaggered && cost.Current == 8f, "red failure consumes");
            Reset(silence); b.TryInterrupt(2.5f);
            Check(!deck.TryUseSlot(0) && cost.Current == 10f && deck.GetHandCard(0) == silence, "noncasting rejects without cost or cycle");
            return "Starting deck checks passed: " + passed;
        }
        finally { Time.timeScale = 1f; Set(metrics, "logEachInput", wasLogging); flow.RestartRun(); }
    }

    private static void Reset(SkillData card, bool three = false)
    {
        flow.RestartRun();
        enemies.BeginBattle(three ? new[] { a, b, c } : new[] { b, c });
        typeof(CostSystem).GetProperty("Current").SetValue(cost, 10f);
        deck.SetDeck(new[] { card, fire, fire, fire, fire, fire, fire, fire });
    }
    private static SkillData Load(string name) => AssetDatabase.LoadAssetAtPath<SkillData>(Folder + name + ".asset");
    private static void SetHp(Enemy e, int hp) => typeof(Enemy).GetProperty("CurrentHp").SetValue(e, hp);
    private static bool Near(float a, float b) => Mathf.Abs(a - b) < 0.0001f;
    private static void Check(bool ok, string label) { if (!ok) throw new Exception(label); passed++; }
    private static void Step(float dt) => Call(deck, "AdvanceCast", dt);
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Hidden).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Hidden).SetValue(obj, value);
    private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Hidden).Invoke(obj, args);
}
