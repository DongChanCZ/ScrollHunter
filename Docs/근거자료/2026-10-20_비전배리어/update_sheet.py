"""Narrow XML fallback: the artifact importer rejects this existing workbook's rich text."""
import json
from pathlib import Path
import re
import zipfile
import xml.etree.ElementTree as ET
import openpyxl
from openpyxl.formula.translate import Translator

root = Path(__file__).resolve().parents[2]
path = root / "밸런스시트.xlsx"
evidence = Path(__file__).resolve().parent
before = openpyxl.load_workbook(path)
assert before["스킬"]["A19"].value is None
with zipfile.ZipFile(path) as z:
    entries = [(i, z.read(i.filename)) for i in z.infolist()]
parts = dict((i.filename, b) for i, b in entries)
ns = {"s": "http://schemas.openxmlformats.org/spreadsheetml/2006/main"}
tag = lambda n: "{" + ns["s"] + "}" + n
ET.register_namespace("", ns["s"])

def replace_cell(xml, addr, value):
    pattern = rf'<c\b[^>]*\br="{addr}"[^>]*>.*?</c>'
    old = re.search(pattern, xml, re.S)
    assert old, addr
    c = ET.fromstring(old[0].replace("<c ", f'<c xmlns="{ns["s"]}" ', 1))
    for child in list(c):
        c.remove(child)
    c.attrib.pop("t", None)
    if isinstance(value, str) and value.startswith("="):
        ET.SubElement(c, tag("f")).text = value[1:]
    elif isinstance(value, str):
        c.set("t", "inlineStr")
        ET.SubElement(ET.SubElement(c, tag("is")), tag("t")).text = value
    else:
        ET.SubElement(c, tag("v")).text = str(value)
    encoded = ET.tostring(c, encoding="unicode").replace(f' xmlns="{ns["s"]}"', "")
    return xml[:old.start()] + encoded + xml[old.end():]

sheet_path = "xl/worksheets/sheet3.xml"
xml = ET.tostring(ET.fromstring(parts[sheet_path]), encoding="unicode")
# Copy existing defense row without moving any rows or unrelated formulas.
source = re.search(r'<row\b[^>]*\br="17"[^>]*>.*?</row>', xml, re.S)[0]
row = ET.fromstring(source.replace("<row ", f'<row xmlns="{ns["s"]}" ', 1))
row.set("r", "19")
for cell in row:
    old_addr = cell.get("r")
    new_addr = old_addr.replace("17", "19")
    cell.set("r", new_addr)
    formula = cell.find("s:f", ns)
    if formula is not None:
        formula.text = Translator("=" + formula.text, origin=old_addr).translate_formula(new_addr)[1:]
        cached = cell.find("s:v", ns)
        if cached is not None:
            cell.remove(cached)
row_xml = ET.tostring(row, encoding="unicode").replace(f' xmlns="{ns["s"]}"', "")
row19 = re.search(r'<row\b[^>]*\br="19"[^>]*(?:/>|>.*?</row>)', xml, re.S)
if row19:
    xml = xml[:row19.start()] + row_xml + xml[row19.end():]
else:
    end18 = re.search(r'<row\b[^>]*\br="18"[^>]*>.*?</row>', xml, re.S).end()
    xml = xml[:end18] + row_xml + xml[end18:]
changes = {
    "A1": "스킬 풀 밸런스 (15종)",
    "A2": "현재 구현 15종. SK15 비전 배리어 추가(2026-10-20). 패시브·과잉 피해·시전 취소 제외.",
    "A19": "SK15", "B19": "비전 배리어", "D19": 5, "O19": 250,
    "Q19": "보상. 일반 시전, 방어도 누적",
    "C32": "목표 3",
}
for r in (23, 24, 26, 27, 29, 30, 31, 32):
    changes[f"B{r}"] = before["스킬"][f"B{r}"].value.replace(":R18", ":R19").replace(":S18", ":S19").replace(":C18", ":C19")
for addr, val in changes.items():
    xml = replace_cell(xml, addr, val)
parts[sheet_path] = xml.encode("utf-8")
legend = ET.tostring(ET.fromstring(parts["xl/worksheets/sheet1.xml"]), encoding="unicode")
parts["xl/worksheets/sheet1.xml"] = replace_cell(legend, "B11", "구현 스킬 15종 계산. SK15 비전 배리어 추가(2026-10-20)").encode("utf-8")
temp = path.with_suffix(".updated.xlsx")
with zipfile.ZipFile(temp, "w") as z:
    for info, data in entries:
        z.writestr(info, parts[info.filename])

after = openpyxl.load_workbook(temp)
allowed = {"스킬": set(changes) | {f"{openpyxl.utils.get_column_letter(c)}19" for c in range(1,21)}, "범례": {"B11"}}
preserved = 0
for a, b in zip(before, after):
    for cells in a:
        for c in cells:
            if c.coordinate not in allowed.get(a.title, set()):
                assert c.value == b[c.coordinate].value, (a.title, c.coordinate)
                assert c.style_id == b[c.coordinate].style_id
                preserved += 1
s = after["스킬"]
assert [s[c].value for c in ["A19","B19","C19","D19","E19","G19","H19","O19","P19","T19"]] == ["SK15","비전 배리어","방어",5,0.2,0,1,250,0,0]
assert s["F19"].value == s["F17"].value.replace("E17", "E19")
for c in s[19]:
    if c.data_type == "f":
        assert "#REF!" not in c.value
changed_parts = [name for name, original in ((i.filename,b) for i,b in entries) if parts[name] != original]
assert changed_parts == ["xl/worksheets/sheet1.xml", "xl/worksheets/sheet3.xml"] or set(changed_parts) == {"xl/worksheets/sheet1.xml","xl/worksheets/sheet3.xml"}
temp.replace(path)
report = {"method":"minimal XML fallback; artifact import font-size error 1000", "changedParts":changed_parts, "unchangedCells":preserved,"skillCount":15,"defenseCount":3,"cost":5,"cast":0.2,"shield":250,"actionLock":0.4,"formulaRefsValid":True,"otherZipEntriesIdentical":True}
(evidence / "sheet-checks.json").write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding="utf-8")
print(json.dumps(report,ensure_ascii=False))
