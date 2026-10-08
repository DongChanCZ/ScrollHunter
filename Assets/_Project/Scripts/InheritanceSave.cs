using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

/// <summary>계승만 저장한다. 저장 성공 전에는 현재 계승을 바꾸지 않는다.</summary>
public sealed class InheritanceSave
{
    [Serializable] public class Passive { public string id; public int stacks; }
    [Serializable] public class Data
    {
        public int version;
        public string[] cards;
        public List<Passive> passives;
    }
    private readonly string path;
    private readonly BattleRewardOption[] catalog;
    private readonly List<SkillData> baseDeck;
    private Data data = Empty();
    private static Data Empty() => new Data { version = 1, cards = new string[DeckSystem.DeckSize], passives = new List<Passive>() };
    public bool LoadFailed { get; private set; }
    public bool UsedBackup { get; private set; }
    public bool HasAny => data.passives.Count > 0 || Array.Exists(data.cards, id => !string.IsNullOrEmpty(id));

    public InheritanceSave(string path, BattleRewardOption[] catalog, List<SkillData> baseDeck)
    {
        this.path = path;
        this.catalog = catalog ?? Array.Empty<BattleRewardOption>();
        this.baseDeck = new List<SkillData>(baseDeck);
        if (!File.Exists(path)) return;
        Data loaded;
        if (TryRead(path, out loaded)) data = loaded;
        else if (TryRead(path + ".bak", out loaded)) { data = loaded; UsedBackup = true; }
        else LoadFailed = true;
    }

    public BattleRewardOption Find(string id)
    {
        if (string.IsNullOrEmpty(id)) return null;
        foreach (var option in catalog)
            if (option != null && option.InheritanceId == id) return option;
        return null;
    }
    public int Stacks(RunRewardEffect effect)
    {
        foreach (var passive in data.passives)
            if (Find(passive.id).Effect == effect) return passive.stacks;
        return 0;
    }
    public IEnumerable<Passive> Passives => data.passives;
    public bool IsInheritedSlot(int index) => index >= 0 && index < data.cards.Length && !string.IsNullOrEmpty(data.cards[index]);
    public List<SkillData> BuildDeck() => BuildDeck(data);
    private List<SkillData> BuildDeck(Data value)
    {
        var result = new List<SkillData>(baseDeck);
        for (int i = 0; i < value.cards.Length; i++)
            if (!string.IsNullOrEmpty(value.cards[i])) result[i] = Find(value.cards[i]).Card;
        return result;
    }
    public bool CanInherit(BattleRewardOption option)
    {
        if (option == null || !option.Inheritable || Find(option.InheritanceId) != option) return false;
        if (option.Card != null) return option.Card.HasValidChannel && !BuildDeck().Contains(option.Card);
        return option.Effect != null && (option.Effect.MaxStacks == 0 || Stacks(option.Effect) < option.Effect.MaxStacks);
    }
    public bool TryInherit(BattleRewardOption option, int slot)
    {
        if (!CanInherit(option)) return false;
        var next = JsonUtility.FromJson<Data>(JsonUtility.ToJson(data));
        if (option.Card != null)
        {
            if (slot < 0 || slot >= next.cards.Length) return false;
            next.cards[slot] = option.InheritanceId;
        }
        else
        {
            Passive entry = next.passives.Find(p => p.id == option.InheritanceId);
            if (entry == null) next.passives.Add(new Passive { id = option.InheritanceId, stacks = 1 });
            else entry.stacks++;
        }
        return TryWrite(next);
    }
    public bool TryReset() => TryWrite(Empty(), false);

    private bool TryRead(string filename, out Data value)
    {
        value = null;
        try
        {
            value = Newtonsoft.Json.JsonConvert.DeserializeObject<Data>(File.ReadAllText(filename));
            return Valid(value);
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException
            || error is ArgumentException || error is Newtonsoft.Json.JsonException)
        { return false; }
    }
    private bool Valid(Data value)
    {
        if (value == null || value.version != 1 || value.cards == null || value.cards.Length != DeckSystem.DeckSize
            || value.passives == null || baseDeck.Count != DeckSystem.DeckSize) return false;
        foreach (string id in value.cards)
            if (!string.IsNullOrEmpty(id) && (Find(id) == null || Find(id).Card == null || !Find(id).Inheritable)) return false;
        var deck = BuildDeck(value);
        if (deck.Contains(null) || new HashSet<SkillData>(deck).Count != deck.Count) return false;
        var seen = new HashSet<string>();
        foreach (Passive entry in value.passives)
        {
            if (entry == null || entry.stacks <= 0 || !seen.Add(entry.id)) return false;
            var option = Find(entry.id);
            if (option == null || option.Effect == null || !option.Inheritable
                || (option.Effect.MaxStacks > 0 && entry.stacks > option.Effect.MaxStacks)) return false;
        }
        return true;
    }
    private bool TryWrite(Data next, bool preserveBackup = true)
    {
        if (!Valid(next)) return false;
        string temp = path + ".tmp";
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path));
            File.WriteAllText(temp, JsonUtility.ToJson(next, true));
            // 초기화한 계승이 이전 백업에서 되살아나지 않게 백업도 지운다.
            if (!preserveBackup && File.Exists(path + ".bak")) File.Delete(path + ".bak");
            if (File.Exists(path)) File.Replace(temp, path, preserveBackup ? path + ".bak" : null);
            else File.Move(temp, path);
            data = next;
            LoadFailed = UsedBackup = false;
            return true;
        }
        catch (Exception error) when (error is IOException || error is UnauthorizedAccessException || error is ArgumentException)
        { return false; }
    }
}
