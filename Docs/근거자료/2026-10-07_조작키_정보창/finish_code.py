from pathlib import Path
import hashlib
root = Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter')
def update(relative, expected, before, after):
    path = root / relative
    raw = path.read_bytes()
    assert hashlib.sha256(raw).hexdigest().upper() == expected, f'Concurrent change: {path}'
    text = raw.decode('utf-8-sig').replace('\r\n', '\n')
    assert text.count(before) == 1
    path.write_bytes(text.replace(before, after).replace('\n', '\r\n').encode('utf-8'))
    print(relative)

update('Assets/_Project/Scripts/CombatInfoUI.cs', 'DA988D6933D8CC3FB60A488E52E1EDA3CB94A9B47C3D1AA2F18385446CCCB174',
       '피해 후 생존 대상에게 차단 판정\\n초록·주황: 성공 경직 <b>{2:0.##}초</b>\\n빨강·시전 종료: 피해만 적용\\n입력은 캐스팅 중인 대상에게만 가능',
       '캐스팅 중에만 사용. 피해 후 차단\\n초록·주황: 생존 시 경직 <b>{2:0.##}초</b>\\n빨강·시전 종료: 피해만 적용')

method = '''    [MenuItem("Tools/Scroll Hunter/Check Information Text")]
    public static void InformationMenu() => Debug.Log(RunInformation());

    // 저장된 14종 설명과 편성·보상 영역을 검사. 전투나 에셋은 바꾸지 않는다.
    public static string RunInformation()
    {
        var info = UnityEngine.Object.FindFirstObjectByType<CombatInfoUI>();
        var reward = UnityEngine.Object.FindFirstObjectByType<BattleRewardUI>();
        var cardText = (TMP_Text)Get(info, "cardText");
        var formation = ((TMP_Text[])Get(reward, "deckLabels"))[0];
        var candidates = (Array)Get(reward, "skillSlots");
        var candidate = (TMP_Text)candidates.GetValue(0).GetType().GetField("detail").GetValue(candidates.GetValue(0));
        passed = 0;
        Check((KeyCode)Get(info, "pauseKey") == KeyCode.A && (KeyCode)Get(info, "speedKey") == KeyCode.S, "A/S bindings");
        Check(string.Format((string)Get(info, "speedFormat"), 0.5f) == "속도 0.5배속"
            && string.Format((string)Get(info, "speedFormat"), 1f) == "속도 1배속", "speed labels");
        int cards = 0;
        foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/Data" }))
        {
            var card = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
            if (!card.name.Contains("SK") && card.name != "Skill_Silence" && card.name != "Skill_CrystalShield") continue;
            cards++;
            string effect = info.DescribeCardEffect(card);
            Check(effect.Contains("총합 피해") == (card.Damage > 0), card.DisplayName + " total only for damage");
            Check(!effect.Contains("최대 피해"), card.DisplayName + " no maximum label");
            foreach (var label in new[] { formation, candidate })
                Check(label.GetPreferredValues(effect, label.rectTransform.rect.width, 0).y <= label.rectTransform.rect.height,
                    card.DisplayName + " fits " + label.name);
            Check(cardText.GetPreferredValues(info.DescribeCard(card), cardText.rectTransform.rect.width, 0).y
                + ((GameObject)Get(info, "cardPanel")).GetComponent<RectTransform>().rect.height
                - cardText.rectTransform.rect.height <= (float)Get(info, "panelMaxHeight"), card.DisplayName + " fits battle panel");
        }
        Check(cards == 14, "current 14 cards");
        foreach (string file in new[] { "Skill_SK02_IceArrow", "Skill_SK06_MagicSpark", "Skill_SK12_Suppression", "Skill_SK09_Blizzard" })
        {
            var sample = UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<SkillData>("Assets/_Project/Data/ScrollHunter/SkillData/" + file + ".asset"));
            try
            {
                Set(sample, "damage", 7); Set(sample, "hitCount", 3);
                string effect = info.DescribeCardEffect(sample);
                Check(effect.Contains("총합 피해 <b>21</b>") && effect.Contains("적 1체") && effect.Contains("크리티컬/방어 적용 전"), file + " 7 x 3 = 21 per enemy");
            }
            finally { UnityEngine.Object.DestroyImmediate(sample); }
        }
        return "Information text: " + passed + " passed / " + cards + " cards";
    }

'''
update('Assets/_Project/Tests/Editor/CombatReadabilityChecks.cs', '9DE443172C2FD06483DBB88FF42C00FB9CD13CCAE63EB8D5EB50D393F7004E60',
       '    public static string Run()\n', method + '    public static string Run()\n')
