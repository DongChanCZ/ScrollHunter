from pathlib import Path
import hashlib
p=Path(r'C:\Users\MBC-501-19\Documents\ScrollHunter\Assets\_Project\Scripts\CombatInfoUI.cs')
raw=p.read_bytes()
assert hashlib.sha256(raw).hexdigest().upper()=='259A74C8685C897EC6C4B057DA2EA788BA182C6F429C51FCB8AFD842CC93C929'
text=raw.decode('utf-8').replace('\r\n','\n')
old=r'{0}</color></b></size>\n'
assert text.count(old)==5
text=text.replace(old,r'{0}</color></b></size>\n<size=80%> </size>\n')
old=r'private string sectionBreak = "\n<size=40%> </size>";'
assert text.count(old)==1
text=text.replace(old,r'private string sectionBreak = "\n<size=80%> </size>";')
p.write_bytes(text.replace('\n','\r\n').encode('utf-8'))
print('CombatInfoUI: title gap added; section spacer 40% -> 80%; font sizes unchanged')
