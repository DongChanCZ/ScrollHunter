using System;
using System.Linq;
using System.Reflection;
using TMPro;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

public static class CardCycleChecks
{
    private const BindingFlags Hidden = BindingFlags.Instance | BindingFlags.NonPublic;
    private static object Get(object o,string n) => o.GetType().GetField(n,Hidden).GetValue(o);
    private static void Set(object o,string n,object v) => o.GetType().GetField(n,Hidden).SetValue(o,v);
    private static void Call(object o,string n,params object[] a) => o.GetType().GetMethod(n,Hidden).Invoke(o,a);
    public static string Run()
    {
        if (!Application.isPlaying) throw new InvalidOperationException("Play required");
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var story=UnityEngine.Object.FindFirstObjectByType<StoryUI>();
        var cost=UnityEngine.Object.FindFirstObjectByType<CostSystem>();
        var fx=UnityEngine.Object.FindFirstObjectByType<UseFeedbackUI>();
        var ui=UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var reward=UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var save=Get(f,"inheritance"); bool te=t.enabled,se=story.enabled; int passed=0;
        Action<bool,string> check=(ok,label)=>{if(!ok)throw new Exception("CardCycle: "+label);passed++;};
        try
        {
            t.enabled=false; story.enabled=false;
            Set(f,"inheritance",new InheritanceSave(System.IO.Path.Combine(Application.temporaryCachePath,"Cycle-"+Guid.NewGuid()+".json"),(BattleRewardOption[])Get(f,"rewardPool"),d.CopyStartingDeck()));
            f.ReturnToTitle();f.StartRun();
            var label=(TMP_Text)Get(reward,"instruction");
            check(f.State==BattleFlowState.Preparing && label.text.Contains(BattleRewardUI.CycleHint),"preparation explains same slot and retained cards");
            var headings=(TMP_Text[])Get(reward,"deckSlotLabels");
            check(headings.Take(4).Select((h,i)=>h.text.Contains("손패 "+"QWER"[i])).All(x=>x),"initial hand has four key labels");
            check(headings.Skip(4).Select((h,i)=>h.text.Contains("대기 "+(i+1))).All(x=>x),"queue ranks 1-4");
            var initial=d.CopyStartingDeck(); f.BeginOrderEdit();f.MoveOrderCard(0,5);
            check(label.text.Contains(BattleRewardUI.CycleHint) && f.GetOrderCard(5)==initial[0],"editing retains explanation");
            f.CancelOrderEdit();check(f.GetOrderCard(0)==initial[0],"cancel retains order");
            f.RestartRun();
            var hand=initial.Take(4).ToArray();var queue=new System.Collections.Generic.Queue<SkillData>(initial.Skip(4));
            foreach(int slot in new[]{0,1,0,3,2,0,1,3})
            {
                // Mixed slot use proves unused cards do not shift and the queue wraps in order.
                d.EndBattle();cost.EnsureTutorialCost(cost.Max);
                var old=hand[slot];var incoming=queue.Dequeue();queue.Enqueue(old);hand[slot]=incoming;
                // E is interrupt: restore a casting target before acceptance.
                var enemies=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
                if(enemies.CurrentTarget!=null && !enemies.CurrentTarget.IsCasting)Call(enemies.CurrentTarget,"BeginNextCast");
                check(d.TryUseSlot(slot),"accept slot "+slot);
                check(Enumerable.Range(0,4).All(i=>d.GetHandCard(i)==hand[i]) && d.PeekNext()==queue.Peek(),"same slot, other slots unchanged, tail cycle");
                check(fx.UsedIconSprite(slot)==old.Icon,"outgoing icon is used card");
                var view=((Array)Get(fx,"slots")).GetValue(slot);var image=(Image)view.GetType().GetField("incomingIcon").GetValue(view);
                check(image.sprite==incoming.Icon && !image.raycastTarget,"incoming ghost matches next card and cannot intercept clicks");
                float current=cost.Current;check(!d.TryUseSlot(slot)&&cost.Current==current,"lock rejects extra input without spend");
                Call(fx,"Tick",.18f);
                check(image.enabled && image.rectTransform.position!=ui.NextCardIcon.rectTransform.position,"incoming icon travels");
                Call(fx,"Tick",.4f);check(!image.enabled&&fx.ActiveSlotCount==0,"ghost clears without affecting queue");
            }
            f.RestartRun();cost.EnsureTutorialCost(cost.Max);check(d.TryUseSlot(0),"setup return to title");
            f.ReturnToTitle();Call(fx,"Update");check(fx.ActiveSlotCount==0,"leaving combat clears animation");
            t.enabled=true;f.BeginTutorial(false);
            for(int i=0;i<4;i++){Set(t,"consumedFrame",-1);t.AdvanceGuide();}
            Set(t,"consumedFrame",-1);check(d.TryUseSlot(0),"tutorial cycle pending");
            t.Skip();check(!(bool)Get(t,"pendingCycleGuide")&&(int)Get(t,"cycleGuideStep")==0,"skip clears pending explanation");
            f.BeginTutorial(false);
            for(int i=0;i<4;i++){Set(t,"consumedFrame",-1);t.AdvanceGuide();}
            Set(t,"consumedFrame",-1);d.TryUseSlot(0);Call(d,"AdvanceCast",.4f);t.TickCombat();
            t.Skip();check(!t.ShowingGuide&&(int)Get(t,"cycleGuideStep")==0,"skip clears visible explanation");
        }
        finally {f.ReturnToTitle();Set(f,"inheritance",save);t.enabled=te;story.enabled=se;}
        return "Card cycle checks passed: "+passed+". Isolated save; function tests, not a player balance run.";
    }
}
