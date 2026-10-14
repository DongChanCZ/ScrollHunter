using System;
using System.Linq;
using System.Reflection;
using UnityEngine;

/// <summary>3인전 말단의 표시·기본 피해·실피해·재시작 범위 검사. 임시 계승 저장에서 실행.</summary>
public static class UnderlingChecks
{
    const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    static object Call(object o,string n,params object[] args)=>o.GetType().GetMethod(n,Hidden).Invoke(o,args);
    static void Set(object o,string n,object value)=>o.GetType().GetField(n,Hidden).SetValue(o,value);

    public static string Run()
    {
        if(!Application.isPlaying)throw new InvalidOperationException("Play required");
        var flow=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var manager=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var tutorial=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var player=UnityEngine.Object.FindFirstObjectByType<Player>();
        var info=UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var encounters=(BattleFlow.Encounter[])typeof(BattleFlow).GetField("encounters",Hidden).GetValue(flow);
        bool enabled=tutorial.enabled;int passed=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("Underlings: "+label);passed++;};
        try
        {
            tutorial.enabled=false;flow.RestartRun();
            check(encounters.Select(x=>x.supportingUnderlings).SequenceEqual(new[]{false,false,true,false}),"only ABC configured");
            foreach(int index in new[]{0,2,0,2,1,3})
            {
                Call(flow,"BeginBattle",index,true,null);
                if(index==2)check(manager.CurrentTarget==encounters[2].centerEnemy,"leader still initial target");
                foreach(var enemy in manager.Enemies)
                {
                    bool expected=index==2 && enemy!=encounters[2].centerEnemy;
                    check(enemy.IsUnderling==expected,"role in battle "+index);
                    check(enemy.DisplayName==enemy.Data.DisplayName+(expected?" (말단)":""),"name in battle "+index);
                    if(index==3)continue;
                    int[] baseDamage=enemy.Data.name=="Enemy_A_Green"?new[]{20}:enemy.Data.name=="Enemy_B_Orange"?new[]{25,40}:new[]{30,240};
                    check(enemy.Data.Attacks.Select(a=>a.Damage).SequenceEqual(baseDamage),"original data preserved "+enemy.Data.name);
                    for(int i=0;i<enemy.Data.Attacks.Count;i++)
                    {
                        var attack=enemy.Data.Attacks[i];
                        int wanted=expected ? (baseDamage[i]+1)/2 : baseDamage[i];
                        Set(enemy,"current",attack);Set(enemy,"resting",false);Set(enemy,"staggerTimer",0f);
                        check(enemy.CurrentAttackDamage==wanted,"raw damage "+enemy.DisplayName+"/"+i);
                        check(info.DescribeEnemy(enemy).Contains(enemy.DisplayName) && info.DescribeEnemy(enemy).Contains(wanted.ToString()),"tooltip "+enemy.DisplayName+"/"+i);
                        player.BeginBattle(true);float hp=player.CurrentHp;
                        Call(enemy,"Fire");
                        check(hp-player.CurrentHp==DamageFormula.Compute(wanted,1,player.Defense),"actual damage after defense "+enemy.DisplayName+"/"+i);
                        check(Mathf.Abs(player.StunRemaining-attack.StunSeconds)<.001f,"stun unchanged");
                    }
                }
            }
            Call(flow,"BeginBattle",2,true,null);
            manager.BeginBattle(encounters[0].enemies);
            check(manager.Enemies.All(e=>!e.IsUnderling && e.DisplayName==e.Data.DisplayName),"standalone preview clears role");
            return "Underling checks passed: "+passed;
        }
        finally {tutorial.enabled=enabled;flow.RestartRun();}
    }
}
