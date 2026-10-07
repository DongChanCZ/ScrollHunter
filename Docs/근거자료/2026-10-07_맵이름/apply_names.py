from pathlib import Path
import hashlib

p = Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter\Assets\_Project\Scripts\BattleFlow.cs')
raw = p.read_bytes()
assert hashlib.sha256(raw).hexdigest().upper() == '9C09194672DA3A4CBFE4AE003E5AE1C8F08362D50A8AD33D0592AF3949EE3B9C', 'Concurrent edit'
text = raw.decode('utf-8').replace('\r\n', '\n')
for old, new in [
    ('        public string label;', '        [Tooltip("상단에 표시할 맵 이름")]\n        public string label;'),
    ('[SerializeField] private string progressFormat = "전투 {0} / {1}  ·  {2}";', '[SerializeField] private string tutorialMapName = "오솔길 입구";'),
    ('progressText.text = "튜토리얼 · 깡패";', 'progressText.text = tutorialMapName;'),
    ('progressText.text = string.Format(progressFormat, BattleNumber, BattleCount, encounters[BattleNumber - 1].label);', 'progressText.text = encounters[BattleNumber - 1].label;'),
]:
    assert text.count(old) == 1, old
    text = text.replace(old, new)
assert p.read_bytes() == raw
p.write_bytes(text.replace('\n', '\r\n').encode('utf-8'))
print('BattleFlow: display configured map names')
