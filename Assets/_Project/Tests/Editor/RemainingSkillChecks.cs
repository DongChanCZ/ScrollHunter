using System;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

/// <summary>저장된 신규 3종·보상·무적 종료 검사. Play 중 실행하고 첫 전투로 복귀.</summary>
public static class RemainingSkillChecks
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
    private static SkillData[] original;

    [MenuItem("Tools/Scroll Hunter/Check Remaining Skills (Play)")]
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
        original = deck.CopyStartingDeck().ToArray();
        var judgment = Load("Skill_SK08_Judgment");
        var blizzard = Load("Skill_SK09_Blizzard");
        var sanctuary = Load("Skill_SK14_Sanctuary");
        var cards = new[] { judgment, blizzard, sanctuary };
        var pool = (BattleRewardOption[])Get(flow, "rewardPool");
        var ui = UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var rewardUI = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var frame = (GameObject)Get(ui, "sanctuaryFrame");
        float chance = (float)Get(player, "criticalChance");
        bool logging = (bool)Get(metrics, "logEachInput");
        var randomState = UnityEngine.Random.state;
        passed = 0;
        try
        {
            Set(player, "criticalChance", 0f); Set(metrics, "logEachInput", false);
            Check(pool.Count(o => o.Card != null) == 6 && pool.Count(o => o.Effect != null) == 10, "six reward cards and ten passives");
            Check(judgment.Cost == 6 && judgment.CastTime == 2.75f && judgment.Damage == 365 && judgment.HitCount == 1 && !judgment.IsAreaOfEffect && !judgment.IsChanneling, "SK08 saved specification");
            Check(blizzard.Cost == 5 && blizzard.CastTime == 1 && blizzard.Damage == 20 && blizzard.HitCount == 4 && blizzard.IsAreaOfEffect && !blizzard.IsChanneling, "SK09 saved specification");
            Check(sanctuary.Cost == 4 && sanctuary.CastTime == 2.5f && sanctuary.Category == SkillCategory.Shield && sanctuary.IsChanneling && sanctuary.HasValidChannel, "SK14 saved specification");
            Check(info.DescribeCard(sanctuary).Contains("무적") && !info.DescribeCard(sanctuary).Contains("방어도 +0"), "sanctuary tooltip explains protection");
            Check(frame != null && frame.GetComponentsInChildren<Image>(true).Length == 4 && frame.GetComponentsInChildren<Image>(true).All(i => !i.raycastTarget), "gold frame never intercepts input");

            Reset(judgment); Check(deck.TryUseSlot(0) && cost.Current == 4 && deck.GetHandCard(0) == original[4], "judgment input pays and cycles");
            Step(2.74f); Check(metrics.RecordedHits == 0, "judgment waits until completion");
            typeof(EnemyManager).GetProperty("CurrentTarget").SetValue(enemies, c);
            Step(.011f); Check(b.CurrentHp == b.MaxHp && c.CurrentHp == c.MaxHp - 365 && metrics.RecordedHits == 1, "judgment effect-time target / 365");
            Check(!deck.IsLocked && (int)Get(metrics, "totalUses") == 1, "judgment no extra lock or use");
            Reset(judgment); Set(player, "criticalChance", 100f); deck.TryUseSlot(0); Step(2.75f);
            Check(metrics.CriticalHits == 1 && metrics.DamageApplied + metrics.OverkillDamage == 548, "judgment critical rounded once to 548");
            Set(player, "criticalChance", 0f);

            Reset(blizzard, true); deck.TryUseSlot(0); Step(.99f);
            Check(cost.Current == 5 && metrics.RecordedHits == 0, "blizzard pays now / waits 1s");
            Step(.011f); Check(a.CurrentHp == a.MaxHp-80 && b.CurrentHp == b.MaxHp-80 && c.CurrentHp == c.MaxHp-80 && metrics.RecordedHits == 12 && (int)Get(metrics,"totalUses") == 1, "blizzard 4 hits each / one use");
            Reset(blizzard, true); deck.TryUseSlot(0); a.TakeDamage(a.CurrentHp); enemies.NotifyEnemyDied(); Step(1f);
            Check(metrics.RecordedHits == 8 && b.CurrentHp == b.MaxHp-80 && c.CurrentHp == c.MaxHp-80, "blizzard excludes targets dead before effect");
            Reset(blizzard); SetHp(b, 10); deck.TryUseSlot(0); Step(1f);
            Check(metrics.RecordedHits == 8 && metrics.DamageApplied == 90 && metrics.OverkillDamage == 70 && metrics.CriticalEligibleHits == 5 && c.CurrentHp == c.MaxHp-80, "blizzard lethal remainder is visual-only / no retarget");
            Reset(blizzard); enemies.BeginBattle(new[]{b}); SetHp(b,10); deck.TryUseSlot(0); Step(1f); int hits=metrics.RecordedHits; Step(4f);
            Check(enemies.CombatEnded && hits==4 && metrics.RecordedHits==hits && !deck.TryUseSlot(1), "blizzard final kill retains visuals / no post-victory damage");
            Reset(blizzard,true); Set(player,"criticalChance",50f); UnityEngine.Random.InitState(2718);
            int expected=0; for(int i=0;i<12;i++) if(UnityEngine.Random.value*100f<50f) expected++;
            UnityEngine.Random.InitState(2718); deck.TryUseSlot(0); Step(1f);
            Check(metrics.CriticalEligibleHits==12 && metrics.CriticalHits==expected && metrics.DamageApplied==240+10*expected, "independent critical draw for every target and hit");
            Set(player,"criticalChance",0f);

            foreach(var card in new[]{judgment,blizzard})
            {
                Reset(card); deck.TryUseSlot(0); player.ApplyStun(2); Step(.1f);
                Check(!deck.IsCasting && metrics.RecordedHits==0 && cost.Current==10-card.Cost && deck.GetHandCard(0)==original[4], "stun cancellation no refund: "+card.name);
                Reset(card); deck.TryUseSlot(0); Time.timeScale=0; Step(10f);
                Check(deck.CastRemaining==card.CastTime && metrics.RecordedHits==0 && !deck.TryUseSlot(1), "pause blocks progress: "+card.name);
                Time.timeScale=1; Step(card.CastTime); Check(metrics.RecordedHits>0, "resume applies effect: "+card.name);
            }

            Reset(sanctuary); player.ApplyStun(2);
            Check(!deck.TryUseSlot(0) && cost.Current==10 && deck.GetHandCard(0)==sanctuary && !player.IsInvulnerable, "cannot use sanctuary while stunned");
            Reset(sanctuary); typeof(CostSystem).GetProperty("Current").SetValue(cost,3f);
            Check(!deck.TryUseSlot(0) && cost.Current==3 && !player.IsInvulnerable, "insufficient cost grants no protection");
            Reset(sanctuary); player.AddShield(40); Check(deck.TryUseSlot(0), "sanctuary accepted");
            Check(cost.Current==6 && deck.GetHandCard(0)==original[4] && player.IsInvulnerable && player.IsStunImmune, "instant protection / input consumption");
            Call(ui,"UpdateSanctuary"); Check(frame.activeSelf, "gold border appears");
            player.TakeDamage(150); player.ApplyStun(2);
            Check(player.CurrentHp==500 && player.Shield==40 && !player.IsStunned && metrics.InvulnerabilityPrevented==125 && (int)Get(metrics,"shieldAbsorbed")==0, "125 prevented separately / shield and HP preserved");
            Call(b,"Fire"); Check(!player.IsStunned && player.CurrentHp==500 && metrics.InvulnerabilityPrevented==158, "actual orange Fire cannot damage or stun");
            Check(!deck.TryUseSlot(1) && cost.Current==6, "channel blocks other cards");
            Step(2.49f); Check(player.IsInvulnerable && deck.IsChanneling, "protection lasts full duration");
            Step(.011f); Call(ui,"UpdateSanctuary");
            Check(!player.IsInvulnerable && !player.IsStunImmune && !frame.activeSelf && !deck.IsLocked, "duration ends / no extra lock / border off");
            player.TakeDamage(150); player.ApplyStun(2);
            Check(player.CurrentHp==415 && player.Shield==0 && player.IsStunned && (int)Get(metrics,"shieldAbsorbed")==40, "normal damage and stun return after expiry");
            Check(metrics.InvulnerabilityPrevented==158, "normal damage not counted as invulnerability");

            Reset(sanctuary); deck.TryUseSlot(0); info.TogglePause(); Step(10f);
            Check(player.IsInvulnerable && deck.CastRemaining==2.5f && !deck.TryUseSlot(1), "paused sanctuary duration frozen");
            info.Resume(); Step(2.5f); Check(!player.IsInvulnerable && deck.TryUseSlot(1), "resumed sanctuary expires and accepts next input");
            foreach(ChannelEndReason reason in Enum.GetValues(typeof(ChannelEndReason)))
            {
                Reset(sanctuary); deck.TryUseSlot(0); Call(deck,"FinishCast",reason);
                Check(!player.IsInvulnerable && !player.IsStunImmune && !deck.IsCasting, "cleanup: "+reason);
            }
            Reset(sanctuary); deck.TryUseSlot(0); deck.enabled=false;
            Check(!player.IsInvulnerable && !deck.IsCasting, "component disable cleans protection"); deck.enabled=true;
            Reset(sanctuary); deck.TryUseSlot(0); flow.RestartRun();
            Check(!player.IsInvulnerable && !player.IsStunImmune && cost.Current==3 && metrics.InvulnerabilityPrevented==0, "restart clears protection and per-battle metric");
            Reset(sanctuary); deck.TryUseSlot(0); Win(); Call(ui,"UpdateSanctuary");
            Check(!player.IsInvulnerable && !frame.activeSelf && flow.State==BattleFlowState.BetweenBattles, "victory cleans active sanctuary");
            Reset(sanctuary); deck.TryUseSlot(0); Set(player,"currentHp",0f); Call(player,"Update"); Call(flow,"Update");
            Check(flow.State==BattleFlowState.Defeat && !player.IsInvulnerable && !deck.IsCasting, "forced defeat cleans sanctuary");
            Reset(sanctuary); deck.TryUseSlot(0); player.TakeDamage(150); Step(2.5f); Win();
            Check(metrics.InvulnerabilityPrevented==125, "ended battle retains prevention total");

            foreach(var card in cards)
            {
                var option=pool.Single(o=>o.Card==card);
                Check(option.CanOffer(original.ToList(),player,cost), "reward eligible: "+card.name);
                Set(flow,"rewardPool",new[]{option}); flow.RestartRun(); Win();
                Check(flow.RewardChoiceCount==1 && flow.GetRewardChoice(0).Card==card, "reward offered: "+card.name);
                var buttons=(Button[])Get(rewardUI,"choiceButtons"); buttons[0].onClick.Invoke();
                Check(flow.SelectedReward==card, "UI selects reward: "+card.name);
                ((Button[])Get(rewardUI,"deckButtons"))[0].onClick.Invoke();
                Check(flow.RewardResolved && flow.DeckCount==8 && flow.GetDeckCard(0)==card, "UI replaces same deck slot: "+card.name);
                ((Button)Get(flow,"nextButton")).onClick.Invoke();
                Check(flow.BattleNumber==2 && deck.GetHandCard(0)==card, "acquired card enters next battle: "+card.name);
                typeof(CostSystem).GetProperty("Current").SetValue(cost,10f);
                Check(deck.TryUseSlot(0), "acquired card usable: "+card.name); Step(card.CastTime);
                Check(card==sanctuary ? !player.IsInvulnerable : metrics.RecordedHits>0, "acquired effect works: "+card.name);
                Win(); Check(flow.RewardChoiceCount==0, "owned card excluded: "+card.name);
                flow.RestartRun(); Check(Enumerable.Range(0,8).All(i=>flow.GetDeckCard(i)==original[i]), "restart restores original deck: "+card.name);
            }
            Set(flow,"rewardPool",pool); flow.RestartRun(); Win();
            Check(flow.RewardChoiceCount==3 && Enumerable.Range(0,3).Count(i=>flow.GetRewardChoice(i).Card!=null)==2, "full pool still offers two cards plus one passive");
            return "Remaining skill checks passed: "+passed;
        }
        finally
        {
            deck.enabled=true; Set(flow,"rewardPool",pool); Set(player,"criticalChance",chance);
            Set(metrics,"logEachInput",logging); UnityEngine.Random.state=randomState;
            Time.timeScale=1; flow.RestartRun(); Call(ui,"UpdateSanctuary");
        }
    }

    private static void Reset(SkillData card,bool three=false)
    {
        flow.RestartRun(); enemies.BeginBattle(three?new[]{a,b,c}:new[]{b,c});
        var trial=original.ToArray();trial[0]=card;deck.SetDeck(trial);
        typeof(CostSystem).GetProperty("Current").SetValue(cost,10f);
    }
    private static void Win() { foreach(var e in enemies.GetAliveEnemies()) e.TakeDamage(e.CurrentHp); enemies.NotifyEnemyDied(); Call(flow,"Update"); }
    private static SkillData Load(string name)=>AssetDatabase.LoadAssetAtPath<SkillData>(Folder+name+".asset");
    private static void SetHp(Enemy e,int hp)=>typeof(Enemy).GetProperty("CurrentHp").SetValue(e,hp);
    private static void Step(float dt)=>Call(deck,"AdvanceCast",dt);
    private static object Get(object obj,string name)=>obj.GetType().GetField(name,Hidden).GetValue(obj);
    private static void Set(object obj,string name,object value)=>obj.GetType().GetField(name,Hidden).SetValue(obj,value);
    private static void Call(object obj,string name,params object[] args)=>obj.GetType().GetMethod(name,Hidden).Invoke(obj,args);
    private static void Check(bool condition,string label) { if(!condition) throw new Exception(label);passed++; }
}
