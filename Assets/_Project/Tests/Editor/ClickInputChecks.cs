using System.Linq;
using UnityEditor;
using UnityEngine;

// 물약·선택 범위 회귀. 전투를 초기화하므로 별도 Play에서 실행한다. 사용자 저장은 격리한다.
public static class ClickInputChecks
{
    [MenuItem("Tools/Scroll Hunter/Check Potion Click and Target Area (Play)")]
    public static void Menu() => Debug.Log(Run());

    public static string Run()
    {
        if (!Application.isPlaying) throw new System.InvalidOperationException("Play required.");
        var flags=System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic;
        System.Func<object,string,object> get=(o,n)=>o.GetType().GetField(n,flags).GetValue(o);
        System.Action<object,string,object> set=(o,n,v)=>o.GetType().GetField(n,flags).SetValue(o,v);
        System.Func<object,string,object[],object> call=(o,n,a)=>o.GetType().GetMethod(n,flags).Invoke(o,a);
        var f=UnityEngine.Object.FindFirstObjectByType<BattleFlow>();
        var p=UnityEngine.Object.FindFirstObjectByType<Player>();
        var d=UnityEngine.Object.FindFirstObjectByType<DeckSystem>();
        var m=UnityEngine.Object.FindFirstObjectByType<EnemyManager>();
        var info=UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var ui=UnityEngine.Object.FindFirstObjectByType<CombatUI>();
        var t=UnityEngine.Object.FindFirstObjectByType<TutorialFlow>();
        var menu=f.GetComponent<MainMenuUI>();
        var cam=Camera.main;
        var real=f.Inheritance;
        var temp=new InheritanceSave(System.IO.Path.Combine(Application.temporaryCachePath,"ClickInput-"+System.Guid.NewGuid().ToString("N")+".json"),(BattleRewardOption[])get(f,"rewardPool"),d.CopyStartingDeck());
        var random=UnityEngine.Random.state;
        var passed=new System.Collections.Generic.List<string>();
        System.Action<bool,string> check=(ok,label)=>{if(!ok)throw new System.Exception("Click input: "+label);passed.Add(label);};
        System.Action<int> fresh=index=>{
         menu.BackToTitle();t.Cancel();set(f,"menuInputFrame",-1);set(t,"consumedFrame",-1);set(f.GetComponent<StoryUI>(),"consumedFrame",-1);
         set(f,"runDeck",d.CopyStartingDeck());call(f,"BeginBattle",new object[]{index,true,null});
         Physics.SyncTransforms();Canvas.ForceUpdateCanvases();set(p,"potionInputFrame",-1);
        };
        var rect=ui.PotionClickArea;
        System.Func<Vector2> potionPoint=()=>RectTransformUtility.WorldToScreenPoint(null,rect.TransformPoint(rect.rect.center));
        System.Action clickPotion=()=>{set(p,"potionInputFrame",-1);call(info,"TryPotionClick",new object[]{potionPoint()});};
        System.Func<Vector2,Enemy> pick=pos=>(Enemy)call(m,"FindClickTarget",new object[]{pos});
        System.Func<Enemy,Rect> bodyRect=e=>{
         Bounds b=e.GetComponent<Collider>().bounds;
         foreach(var skin in e.GetComponentsInChildren<SkinnedMeshRenderer>())if(skin.enabled)b.Encapsulate(skin.bounds);
         var points=new System.Collections.Generic.List<Vector2>();
         for(int i=0;i<8;i++)points.Add(cam.WorldToScreenPoint(b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1))));
         return Rect.MinMaxRect(points.Min(v=>v.x),points.Min(v=>v.y),points.Max(v=>v.x),points.Max(v=>v.y));
        };
        int accepted=0,rejected=0;
        System.Action used=()=>accepted++,reject=()=>rejected++;
        try{
         set(f,"inheritance",temp);fresh(0);p.PotionUsed+=used;p.PotionRejected+=reject;
         check(rect.GetComponent<UnityEngine.UI.Image>().raycastTarget,"potion blocks UI raycast");
         check((bool)call(m,"PointerOverUI",new object[]{potionPoint()}),"potion click cannot target enemy");
         set(p,"currentHp",200f);clickPotion();
         check(p.CurrentHp==350f&&p.PotionsRemaining==2&&accepted==1,"click heals 150 consumes one and fires existing feedback");
         p.RequestPotionUse();check(p.CurrentHp==350f&&p.PotionsRemaining==2&&accepted==1,"same-frame Shift plus click only once");
         check(!(bool)call(info,"TryPotionClick",new object[]{new Vector2(-100,-100)}),"outside icon ignored");
         set(p,"currentHp",500f);clickPotion();check(p.PotionsRemaining==2&&rejected==1,"full HP rejects without consumption");
         set(p,"currentHp",100f);p.ApplyStun(2f);clickPotion();check(p.CurrentHp==100f&&p.PotionsRemaining==2&&rejected==2,"stun rejects");
         p.EndBattle();Time.timeScale=0f;clickPotion();check(p.CurrentHp==100f&&p.PotionsRemaining==2&&rejected==2,"pause ignores potion without rejection sound");
         Time.timeScale=1f;menu.OpenPauseMenu();clickPotion();check(p.CurrentHp==100f&&p.PotionsRemaining==2,"Esc ignores potion");menu.ResumeGame();set(f,"menuInputFrame",-1);
         set(t,"consumedFrame",Time.frameCount);clickPotion();check(p.PotionsRemaining==2,"tutorial close frame does not use potion");set(t,"consumedFrame",-1);
         clickPotion();clickPotion();check(p.CurrentHp==400f&&p.PotionsRemaining==0,"remaining potions work");clickPotion();check(p.CurrentHp==400f&&p.PotionsRemaining==0,"empty potion rejects");
         foreach(var state in new[]{BattleFlowState.Title,BattleFlowState.Preparing,BattleFlowState.CountingDown,BattleFlowState.BetweenBattles,BattleFlowState.Victory,BattleFlowState.Defeat,BattleFlowState.Story,BattleFlowState.Inheriting}){
          fresh(0);set(p,"currentHp",200f);set(f,"<State>k__BackingField",state);Time.timeScale=1f;clickPotion();check(p.CurrentHp==200f&&p.PotionsRemaining==3,"noncombat blocked "+state);
         }
         foreach(bool channel in new[]{false,true}){
          fresh(0);var skill=AssetDatabase.FindAssets("t:SkillData",new[]{"Assets/_Project/Data"}).Select(g=>AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(g))).First(x=>x.Category==SkillCategory.Deal&&x.CastTime>0&&x.IsChanneling==channel);
          var cards=d.CopyStartingDeck();cards.Remove(skill);cards.Insert(0,skill);d.SetDeck(cards.Take(8).ToList());
          UnityEngine.Object.FindFirstObjectByType<CostSystem>().EnsureTutorialCost(10);check(d.TryUseSlot(0)&&d.IsCasting,"skill starts "+channel);
          var target=m.CurrentTarget;var other=m.Enemies.First(x=>x!=target);
          call(m,"SelectTarget",new object[]{other});check(m.CurrentTarget==target,"cast/channel locks manual target "+channel);
          set(p,"currentHp",200f);clickPotion();check(p.CurrentHp==350f&&p.PotionsRemaining==2&&d.IsCasting,"potion during cast/channel "+channel);
         }
         fresh(0);
         foreach(int encounter in new[]{0,1,2,3}){
          fresh(encounter);foreach(var e in m.Enemies){
           Rect b=bodyRect(e);Vector2 point=new Vector2(b.xMax+12f*Screen.height/1080f,b.center.y);
           check(pick(point)==e,"outside body selectable "+e.name+" encounter "+encounter);
           check(pick(cam.WorldToScreenPoint(e.GetComponent<Collider>().bounds.center))==e,"body center stays correct "+e.name);
          }
         }
         fresh(0);var first=m.Enemies[0];var second=m.Enemies[1];
         second.transform.position=first.transform.position+Vector3.right*1.2f;Physics.SyncTransforms();
         check(pick(cam.WorldToScreenPoint(second.GetComponent<Collider>().bounds.center))==second,"overlap direct body wins");
         check(pick(new Vector2(-100,-100))==null,"empty background ignored");
         first.TakeDamage(first.CurrentHp);m.NotifyEnemyDied();check(m.CurrentTarget==second&&pick(cam.WorldToScreenPoint(first.transform.position))!=first,"dead target ignored and automatic move retained");
         fresh(0);var thug=UnityEngine.Object.FindObjectsByType<Enemy>(FindObjectsInactive.Include,FindObjectsSortMode.None).First(x=>x.name=="Enemy_Tutorial");
         m.BeginBattle(new[]{thug});Physics.SyncTransforms();var thugRect=bodyRect(thug);
         check(pick(new Vector2(thugRect.xMax+12f*Screen.height/1080f,thugRect.center.y))==thug,"tutorial body padding");
         fresh(3);var boss=m.CurrentTarget;boss.TakeDamage(1600);boss.TryInterrupt(2.5f);var orbs=boss.GetComponent<BossOrbs>();Physics.SyncTransforms();
         check(pick(cam.WorldToScreenPoint(orbs.Ruin.transform.position))==null,"preview orb untargetable");
         set(p,"currentHp",200f);clickPotion();check(p.CurrentHp==350f&&boss.IsPhaseTransitioning,"potion allowed during boss transition");
         call(boss,"AdvanceCombat",new object[]{2.5f});Physics.SyncTransforms();
         foreach(var orb in new[]{orbs.Ruin,orbs.Cycle}){
          var b=bodyRect(orb);check(pick(new Vector2(b.xMax+12f*Screen.height/1080f,b.center.y))==orb,"active orb padding "+orb.name);
         }
         orbs.Ruin.TakeDamage(400);check(pick(cam.WorldToScreenPoint(orbs.Ruin.transform.position))!=orbs.Ruin,"destroyed orb excluded");
         return "Click input checks: "+passed.Count+" passed ("+Screen.width+"x"+Screen.height+"); function/UI raycasts, physical input excluded.";
        }finally{
         p.PotionUsed-=used;p.PotionRejected-=reject;
         menu.BackToTitle();set(f,"inheritance",real);t.Cancel();UnityEngine.Random.state=random;
        }
    }
}
