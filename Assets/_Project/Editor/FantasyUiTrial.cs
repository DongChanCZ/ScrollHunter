using UnityEngine;
using UnityEngine.UI;
using UnityEditor;
using UnityEditor.SceneManagement;
using TMPro;

// Editor-only layout authoring. Runtime icons follow the existing card rotation.
public static class FantasyUiTrial
{
    const string Dir = "Assets/_Project/UI/FantasyTrial/";
    static Image ImageChild(Transform parent, string name)
    {
        var child = parent.Find(name);
        var obj = child ? child.gameObject : new GameObject(name, typeof(RectTransform), typeof(Image));
        obj.transform.SetParent(parent, false);
        var image = obj.GetComponent<Image>();
        image.raycastTarget = false;
        return image;
    }
    static void Remove(Transform parent, string name)
    {
        var child = parent.Find(name);
        if (child) Object.DestroyImmediate(child.gameObject);
    }
    static void Rect(RectTransform r, Vector2 anchor, Vector2 pivot, Vector2 pos, Vector2 size)
    {
        r.anchorMin = r.anchorMax = anchor; r.pivot = pivot;
        r.anchoredPosition = pos; r.sizeDelta = size;
    }
    static void Stretch(RectTransform r, Vector2 min, Vector2 max)
    {
        r.anchorMin = Vector2.zero; r.anchorMax = Vector2.one;
        r.offsetMin = min; r.offsetMax = max;
    }
    [MenuItem("Tools/Scroll Hunter/Apply Fantasy UI Trial")]
    public static void Apply()
    {
        if (EditorApplication.isPlaying) throw new System.InvalidOperationException("Stop Play before applying UI.");
        var scene = UnityEngine.SceneManagement.SceneManager.GetActiveScene();
        if (scene.name != "Battle") throw new System.InvalidOperationException("Open Battle scene.");
        string[] icons = { "fireball", "ice-spear", "slicing-arrow", "fire-zone", "charged-arrow", "lightning-arc", "punch-blast", "caldera", "silence", "magic-swirl", "magic-shield" };
        string[] ids = { "SK01", "SK02", "SK03", "SK04", "SK05", "SK06", "SK07", "SK10", "SK11", "SK12", "SK13" };
        for (int i = 0; i < icons.Length; i++)
        {
            string path = Dir + "Icons/" + icons[i] + ".png";
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false; importer.alphaIsTransparency = true;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
            var sprite = AssetDatabase.LoadAssetAtPath<Sprite>(path);
            // The two original support cards predate SK file names.
            foreach (string guid in AssetDatabase.FindAssets("t:SkillData", new[] { "Assets/_Project/Data" }))
            {
                var skill = AssetDatabase.LoadAssetAtPath<SkillData>(AssetDatabase.GUIDToAssetPath(guid));
                bool match = skill.name.Contains(ids[i]);
                if (ids[i] == "SK11") match |= skill.Category == SkillCategory.Interrupt && skill.Damage == 0;
                if (ids[i] == "SK13") match |= skill.Category == SkillCategory.Shield;
                if (!match) continue;
                var data = new SerializedObject(skill);
                data.FindProperty("icon").objectReferenceValue = sprite;
                data.ApplyModifiedPropertiesWithoutUndo();
            }
        }
        var ui = Object.FindFirstObjectByType<CombatUI>();
        var so = new SerializedObject(ui);
        var hand = so.FindProperty("handSlots");
        var first = (Image)hand.GetArrayElementAtIndex(0).FindPropertyRelative("background").objectReferenceValue;
        var root = (RectTransform)first.transform.parent;
        root.sizeDelta = new Vector2(840, 195); root.anchoredPosition = new Vector2(-120, 60);
        for (int i = 0; i < hand.arraySize; i++)
        {
            var slot = hand.GetArrayElementAtIndex(i);
            var bg = (Image)slot.FindPropertyRelative("background").objectReferenceValue;
            bg.sprite = null; bg.type = Image.Type.Simple;
            Remove(bg.transform, "FantasyFrame"); Remove(bg.transform, "FantasyDivider");
            var icon = ImageChild(bg.transform, "SkillIcon");
            icon.preserveAspect = true; icon.transform.SetAsFirstSibling();
            Stretch(icon.rectTransform, new Vector2(24, 34), new Vector2(-24, -34));
            slot.FindPropertyRelative("skillIcon").objectReferenceValue = icon;
            var cover = (Image)slot.FindPropertyRelative("costCover").objectReferenceValue;
            if (cover) cover.transform.SetSiblingIndex(1);
            foreach (string key in new[] { "nameText", "keyLabel", "lockText", "costText" })
            {
                var text = (TMP_Text)slot.FindPropertyRelative(key).objectReferenceValue;
                text.color = Color.white; text.raycastTarget = false;
                if (key == "costText" || key == "keyLabel")
                {
                    bool left = key == "costText";
                    Rect(text.rectTransform, new Vector2(left ? 0 : 1, 1), new Vector2(left ? 0 : 1, 1), new Vector2(left ? 12 : -12, -6), new Vector2(48, 36));
                    text.fontSize = 30;
                }
                else
                {
                    Rect(text.rectTransform, new Vector2(.5f,.5f), new Vector2(.5f,.5f), Vector2.zero, new Vector2(165,70));
                    text.alignment = TextAlignmentOptions.Center;
                    text.fontSize = key == "lockText" ? 32 : 25;
                }
            }
            var row = (RectTransform)bg.transform.Find("TypeRow");
            Rect(row, new Vector2(.5f,0), new Vector2(.5f,0), new Vector2(0,5), new Vector2(175,27));
            ((TMP_Text)slot.FindPropertyRelative("typeText").objectReferenceValue).fontSize = 22;
        }
        so.FindProperty("slotNormalColor").colorValue = new Color(.12f,.135f,.16f,.97f);
        so.FindProperty("slotLockedColor").colorValue = new Color(.38f,.38f,.38f);
        so.FindProperty("costNormalColor").colorValue = Color.white;
        so.FindProperty("costShortColor").colorValue = new Color(1f,.30f,.30f);
        so.FindProperty("dealTypeColor").colorValue = new Color(.52f,.69f,1f);
        so.FindProperty("shieldTypeColor").colorValue = new Color(.25f,.82f,.94f);
        so.FindProperty("interruptTypeColor").colorValue = new Color(1f,.44f,.76f);
        var canvas = first.GetComponentInParent<Canvas>();
        Remove(canvas.transform, "FantasyHudDock");
        var next = (TMP_Text)so.FindProperty("nextCardText").objectReferenceValue;
        Remove(next.transform, "ScrollEmblem");
        next.color = new Color(.87f,.87f,.84f);
        Rect(next.rectTransform, new Vector2(.5f,0), new Vector2(.5f,.5f), new Vector2(430,78), new Vector2(240,40));
        next.fontSizeMin = 20; next.fontSizeMax = 24;
        var nextIcon = ImageChild(next.transform, "NextSkillIcon");
        nextIcon.preserveAspect = true;
        Rect(nextIcon.rectTransform, new Vector2(.5f,1), new Vector2(.5f,0), new Vector2(0,8), new Vector2(98,98));
        so.FindProperty("nextCardIcon").objectReferenceValue = nextIcon;
        var cast = so.FindProperty("castRoot").objectReferenceValue as GameObject;
        if (cast)
        {
            var cr = (RectTransform)cast.transform;
            cr.anchoredPosition = new Vector2(cr.anchoredPosition.x,285);
            Remove(cast.transform, "FantasyFrame");
        }
        so.ApplyModifiedPropertiesWithoutUndo();
        foreach (var image in Object.FindObjectsByType<Image>(FindObjectsInactive.Include, FindObjectsSortMode.None))
        {
            if (!image) continue;
            bool gauge = image.name == "HpBarBG" || image.name == "CastBarBG";
            if (gauge)
            {
                Remove(image.transform,"FantasyFrame");
                image.sprite = null; image.color = new Color(.075f,.085f,.10f,.88f);
                var r = image.rectTransform; bool hp = image.name == "HpBarBG";
                r.sizeDelta = new Vector2(0,hp ? 28 : 26);
                r.anchoredPosition = new Vector2(0,hp ? 38 : 0);
                foreach (Transform child in r)
                {
                    var fill = child.GetComponent<Image>();
                    if (fill) { fill.sprite = null; fill.rectTransform.offsetMin = Vector2.zero; fill.rectTransform.offsetMax = Vector2.zero; }
                    var text = child.GetComponent<TMP_Text>();
                    if (text) { text.fontSize = hp ? 24 : 23; text.enableAutoSizing = false; }
                }
            }
            if (image.name == "CardInfo")
            {
                Remove(image.transform,"FantasyFrame");
                image.sprite = AssetDatabase.LoadAssetAtPath<Sprite>(Dir+"Paper.asset");
                image.type = Image.Type.Sliced; image.color = new Color(1,.96f,.86f);
                Rect(image.rectTransform, Vector2.one, Vector2.one, new Vector2(-24,-115), new Vector2(520,330));
                var text = image.GetComponentInChildren<TMP_Text>(true);
                text.color = new Color(.18f,.12f,.07f); text.fontSize = 24; text.lineSpacing = 4;
                Stretch(text.rectTransform,new Vector2(26,22),new Vector2(-26,-22));
            }
            if (image.name == "TargetInfo")
            {
                Remove(image.transform,"FantasyFrame");
                image.sprite = null; image.color = new Color(.075f,.085f,.10f,.80f);
            }
        }
        LayoutRebuilder.ForceRebuildLayoutImmediate(root);
        AssetDatabase.SaveAssets(); EditorSceneManager.MarkSceneDirty(scene); EditorSceneManager.SaveScene(scene);
        Debug.Log("Square skill icons, parchment tooltip and clean gauges saved.");
    }
}

