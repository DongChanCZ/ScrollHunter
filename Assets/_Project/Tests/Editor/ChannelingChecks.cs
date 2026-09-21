using System;
using System.Collections.Generic;
using System.Reflection;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>저장 씬/에셋 대신 임시 씬과 메모리 데이터를 사용하는 회귀 검사.</summary>
public static class ChannelingChecks
{
    private const BindingFlags Private = BindingFlags.Instance | BindingFlags.NonPublic;
    private static int passed;

    [MenuItem("Tools/Scroll Hunter/Check Channeling")]
    public static void RunMenu() { Debug.Log(Run()); }

    public static string Run()
    {
        if (EditorApplication.isPlaying) throw new InvalidOperationException("Stop Play mode before running checks.");
        Scene original = SceneManager.GetActiveScene();
        Scene testScene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Additive);
        SceneManager.SetActiveScene(testScene);
        float originalScale = Time.timeScale;
        passed = 0;
        try
        {
            using (var f = new Fixture(false, 0f))
            {
                Check(f.Deck.TryUseSlot(0) && f.Player.Shield == 99, "instant effect");
                Check(!f.Deck.IsCasting && f.Deck.IsLocked, "instant minimum GCD");
                f.Step(0.4f);
                Check(!f.Deck.IsLocked, "instant GCD release");
            }
            using (var f = new Fixture(false, 1f))
            {
                f.Deck.TryUseSlot(0); f.Step(0.5f);
                Check(f.Player.Shield == 0 && f.Deck.IsLocked, "normal cast waits");
                f.Step(0.6f);
                Check(f.Player.Shield == 99 && !f.Deck.IsCasting, "normal cast completes once");
            }
            using (var f = new Fixture(true, 1f))
            {
                Check(f.Deck.TryUseSlot(0), "channel accepted");
                Check(ChannelingProbeEffect.Begins == 1 && f.Player.Shield == 7, "effect begins immediately");
                Check(f.Cost.Current == 8f && f.Deck.GetHandCard(0) == f.Next, "one cost and same-slot cycle");
                Check(!f.Deck.TryUseSlot(1) && f.Cost.Current == 8f, "other skills blocked");
                f.Step(0.25f);
                Check(ChannelingProbeEffect.Ticks == 1 && f.Player.Shield == 7, "effect maintained during channel");
                Time.timeScale = 0f; f.Step(0.5f);
                Check(f.Deck.CastRemaining == 0.75f && ChannelingProbeEffect.Ticks == 1, "pause stops channel");
                Check(!f.Deck.TryUseSlot(1), "pause blocks input");
                Time.timeScale = 1f; f.Step(2f);
                Check(Mathf.Abs(ChannelingProbeEffect.Elapsed - 1f) < 0.0001f, "last tick clamped to duration");
                Check(ChannelingProbeEffect.Ends == 1 && ChannelingProbeEffect.Reason == ChannelEndReason.Completed, "normal channel cleanup once");
                Check(f.Player.Shield == 0 && !f.Deck.IsLocked, "no completion Shield effect or lingering lock");
                Check((int)Get(f.Metrics, "totalUses") == 1, "ticks do not count as extra uses");
                Check(f.Deck.GetHandCard(0) == f.Next && f.Cost.Current == 8f, "no second cycle or refund");
                f.Step(1f); f.Deck.SetDeck(new[] { f.Card, f.Next });
                Check(ChannelingProbeEffect.Ends == 1, "ended channel stays ended");
                SetProperty(f.Cost, "Current", 10f);
                f.Deck.TryUseSlot(0); f.Step(0.1f);
                Check(f.Effect.LocalTicks == 0 && ChannelingProbeEffect.LastLocalTicks == 1, "per-use state is separate from asset");
            }
            using (var f = new Fixture(true, 1f))
            {
                f.Deck.TryUseSlot(0); f.Step(0.2f); f.Player.ApplyStun(2f); f.Step(0.1f);
                Check(!f.Deck.IsCasting && f.Player.Shield == 0 && f.Deck.LockRemaining == 0f, "stun cancels and cleans");
                Check(ChannelingProbeEffect.Reason == ChannelEndReason.Stunned && (int)Get(f.Metrics, "cancelledUses") == 1, "stun reason and count");
                Check(f.Cost.Current == 8f && f.Deck.GetHandCard(0) == f.Next, "stun no refund or extra cycle");
                Check(!f.Deck.TryUseSlot(1), "stunned input ignored");
                SetProperty(f.Player, "StunRemaining", 0f);
                Check(f.Deck.TryUseSlot(1), "input immediately after stun");
            }
            using (var f = new Fixture(false, 1f))
            {
                f.Deck.TryUseSlot(0); f.Player.ApplyStun(2f); f.Step(1f);
                Check(f.Player.Shield == 0 && !f.Deck.IsCasting && f.Cost.Current == 8f, "normal cast stun regression");
            }
            using (var f = new Fixture(true, 0.1f))
            {
                f.Deck.TryUseSlot(0); f.Step(0.1f);
                Check(!f.Deck.IsCasting && f.Deck.IsLocked && f.Player.Shield == 0, "short channel respects minimum GCD");
                f.Step(0.31f);
                Check(!f.Deck.IsLocked, "short channel GCD ends");
            }
            foreach (var reason in new[] { ChannelEndReason.CombatEnded, ChannelEndReason.DeckReset, ChannelEndReason.Disabled })
            {
                using (var f = new Fixture(true, 1f))
                {
                    f.Deck.TryUseSlot(0);
                    if (reason == ChannelEndReason.CombatEnded)
                    { SetProperty(f.Metrics, "Ended", true); Time.timeScale = 0f; f.Step(0f); }
                    else if (reason == ChannelEndReason.DeckReset) f.Deck.SetDeck(new[] { f.Next });
                    else Call(f.Deck, "OnDisable");
                    Check(ChannelingProbeEffect.Ends == 1 && ChannelingProbeEffect.Reason == reason && f.Player.Shield == 0, "cleanup " + reason);
                    f.Step(2f);
                    Check(ChannelingProbeEffect.Ticks == 0 && ChannelingProbeEffect.Ends == 1, "no later effects " + reason);
                    Time.timeScale = 1f;
                }
            }
            using (var f = new Fixture(true, 1f))
            {
                f.Deck.TryUseSlot(0);
                Set(f.Player, "currentHp", 0f); Time.timeScale = 0f; f.Step(0f);
                Check(ChannelingProbeEffect.Reason == ChannelEndReason.CombatEnded && f.Player.Shield == 0, "defeat cleanup while paused");
                Check(!f.Deck.TryUseSlot(1), "no use after defeat");
                Time.timeScale = 1f;
            }
            using (var f = new Fixture(true, 1f))
            {
                ChannelingProbeEffect.EndCombatOnTick = true;
                f.Deck.TryUseSlot(0); f.Step(0.1f);
                Check(!f.Deck.IsCasting && f.Player.Shield == 0 && ChannelingProbeEffect.Reason == ChannelEndReason.CombatEnded, "effect-triggered combat end");
            }
            foreach (float duration in new[] { 0f, -1f, float.NaN, float.PositiveInfinity })
            {
                using (var f = new Fixture(true, duration))
                {
                    Check(!f.Deck.TryUseSlot(0) && f.Cost.Current == 10f && f.Deck.GetHandCard(0) == f.Card, "invalid duration rejected " + duration);
                }
            }
            using (var f = new Fixture(true, 1f))
            {
                Set(f.Card, "channelEffect", null);
                Check(f.Deck.GetSlotState(0) == SlotState.InvalidConfiguration && !f.Deck.TryUseSlot(0), "missing effect rejected");
                Check(f.Cost.Current == 10f && f.Deck.GetHandCard(0) == f.Card, "invalid configuration consumes nothing");
            }
            return "Channeling checks passed: " + passed;
        }
        finally
        {
            Time.timeScale = originalScale;
            SceneManager.SetActiveScene(original);
            EditorSceneManager.CloseScene(testScene, true);
        }
    }

    private static void Check(bool ok, string label)
    {
        if (!ok) throw new Exception("Channeling check failed: " + label);
        passed++;
    }
    private static object Get(object obj, string field) => obj.GetType().GetField(field, Private).GetValue(obj);
    private static void Set(object obj, string field, object value) => obj.GetType().GetField(field, Private).SetValue(obj, value);
    private static void SetProperty(object obj, string name, object value) => obj.GetType().GetProperty(name).SetValue(obj, value);
    private static void Call(object obj, string method, params object[] args) => obj.GetType().GetMethod(method, Private).Invoke(obj, args);

    private sealed class Fixture : IDisposable
    {
        public readonly GameObject Root;
        public readonly Player Player;
        public readonly CostSystem Cost;
        public readonly CombatMetrics Metrics;
        public readonly DeckSystem Deck;
        public readonly SkillData Card;
        public readonly SkillData Next;
        public readonly ChannelingProbeEffect Effect;

        public Fixture(bool channel, float duration)
        {
            Time.timeScale = 1f;
            ChannelingProbeEffect.ResetCounters();
            Root = new GameObject("Channeling check (temporary)");
            Root.SetActive(false); // Awake/Update cannot find or alter the user's scene.
            Player = Root.AddComponent<Player>();
            Cost = Root.AddComponent<CostSystem>();
            Metrics = Root.AddComponent<CombatMetrics>();
            Deck = Root.AddComponent<DeckSystem>();
            SetProperty(Cost, "Current", 10f);
            Set(Metrics, "logEachInput", false);
            Set(Deck, "costSystem", Cost);
            Set(Deck, "player", Player);
            Set(Deck, "metrics", Metrics);
            Effect = ScriptableObject.CreateInstance<ChannelingProbeEffect>();
            Card = ScriptableObject.CreateInstance<SkillData>();
            Next = ScriptableObject.CreateInstance<SkillData>();
            Set(Card, "displayName", "Channel test");
            Set(Card, "cost", 2f);
            Set(Card, "category", SkillCategory.Shield);
            Set(Card, "shieldAmount", 99);
            Set(Card, "activation", channel ? SkillActivation.Channeling : SkillActivation.Cast);
            Set(Card, "castTime", duration);
            Set(Card, "channelEffect", Effect);
            Set(Next, "category", SkillCategory.Shield);
            Deck.SetDeck(new[] { Card, Next, Next, Next, Next, Next, Next, Next });
        }
        public void Step(float elapsed) { Call(Deck, "AdvanceCast", elapsed); }
        public void Dispose()
        {
            Deck.SetDeck(null);
            UnityEngine.Object.DestroyImmediate(Root);
            UnityEngine.Object.DestroyImmediate(Card);
            UnityEngine.Object.DestroyImmediate(Next);
            UnityEngine.Object.DestroyImmediate(Effect);
        }
    }
}

// Editor 검사 전용. 실제 덱/보상 풀에 등록하지 않는다.
public sealed class ChannelingProbeEffect : ChannelEffect
{
    public static int Begins, Ticks, Ends, LastLocalTicks;
    public static float Elapsed;
    public static ChannelEndReason Reason;
    public static bool EndCombatOnTick;
    public int LocalTicks;
    public static void ResetCounters()
    {
        Begins = Ticks = Ends = LastLocalTicks = 0;
        Elapsed = 0f; EndCombatOnTick = false;
    }
    public override void Begin(ChannelContext context) { Begins++; context.Player.AddShield(7); }
    public override void Tick(ChannelContext context, float deltaTime)
    {
        Ticks++; LocalTicks++; LastLocalTicks = LocalTicks; Elapsed += deltaTime;
        if (EndCombatOnTick)
            typeof(Player).GetField("currentHp", BindingFlags.Instance | BindingFlags.NonPublic).SetValue(context.Player, 0f);
    }
    public override void End(ChannelContext context, ChannelEndReason reason)
    { Ends++; Reason = reason; context.Player.ClearShield(); }
}
