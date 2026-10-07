from pathlib import Path
root = Path(__file__).resolve().parents[4]
p = root / 'Assets/_Project/Editor/BattleEnvironmentSetup.cs'
before = p.read_bytes()
backup = Path(__file__).with_name('BattleEnvironmentSetup.cs.before')
assert before == backup.read_bytes(), '배치 도구가 동시 수정됨'
s = before.decode('utf-8-sig').replace('\r\n', '\n')
pairs = [
('static Material[] propMaterials;', 'static Material[] propMaterials;\n    static Material outdoorRockMaterial;\n    static readonly Color OutdoorRockTint = new Color(.52f,.67f,.78f);'),
('new Color(.73f,.88f,.87f), new Color(.73f,.88f,.87f), new Color(.75f,.90f,.87f),', 'new Color(.45f,.63f,.69f), new Color(.45f,.63f,.69f), new Color(.45f,.63f,.69f),'),
('new Color(.69f,.81f,.91f), new Color(.72f,.83f,.94f),', 'new Color(.69f,.81f,.91f), new Color(.52f,.67f,.78f),'),
('new Color(.79f,.90f,.94f), new Color(.70f,.84f,.91f), new Color(.75f,.86f,.95f),', 'new Color(.64f,.79f,.85f), new Color(.57f,.74f,.83f), new Color(.62f,.77f,.86f),'),
('        var root = new GameObject("BattleEnvironment");', '        // 바위는 보스 동굴에서도 쓰므로 일반 맵 재질만 따로 둔다.\n        outdoorRockMaterial = Material("P04_MossRocks_Outdoor", "Universal Render Pipeline/Lit");\n        outdoorRockMaterial.CopyPropertiesFromMaterial(propMaterials[3]);\n        outdoorRockMaterial.SetColor("_BaseColor", OutdoorRockTint);\n        var root = new GameObject("BattleEnvironment");'),
('Prop(a, 1, -6.8f, 6, 10, 40); Prop(a, 1, 6.8f, 8, 10, -40);', 'Prop(a, 1, -10.5f, 6, 10, 40); Prop(a, 1, 10.5f, 8, 10, -40);'),
('Prop(a, 1, -5.6f, 13, 10, 75); Prop(a, 1, 5.4f, 15, 11, 10);', 'Prop(a, 1, -12.5f, 13, 10, 75); Prop(a, 1, 12.5f, 15, 11, 10);'),
('Prop(a, 0, -11, 16, 12, 20); Prop(a, 0, 11, 17, 12, -50);', 'Prop(a, 0, -13.5f, 16, 12, 20); Prop(a, 0, 13.5f, 17, 12, -50);'),
('Prop(a, 4, 0, 9, 9.7f, 0);', 'Prop(a, 4, 0, 9, 9.7f, 0, 1.22f);'),
('static void Prop(Transform parent, int index, float x, float z, float height, float yaw)', 'static void Prop(Transform parent, int index, float x, float z, float height, float yaw, float width = 1f)'),
('r.sharedMaterial = propMaterials[index];', 'r.sharedMaterial = index == 3 && parent.name != "04_CaveInterior" ? outdoorRockMaterial : propMaterials[index];'),
('        o.transform.position += new Vector3(x - bounds.center.x, -.18f - Mathf.Max(0f, z - 3f) * .18f - bounds.min.y, z - bounds.center.z);', '        o.transform.position += new Vector3(x - bounds.center.x, -.18f - Mathf.Max(0f, z - 3f) * .18f - bounds.min.y, z - bounds.center.z);\n        o.transform.localScale = Vector3.Scale(o.transform.localScale, new Vector3(width, 1f, 1f));'),
]
for old, new in pairs:
    assert s.count(old) == 1, old
    s = s.replace(old, new)
assert p.read_bytes() == before
p.write_text(s, encoding='utf-8', newline='\n')
print('BattleEnvironmentSetup: 일반 맵 색·배치 동기화')
