from pathlib import Path
p=Path(__file__).resolve().parents[3]/'Assets/_Project/Tests/Editor/TutorialChecks.cs'
b=p.read_bytes();s=b.decode('utf-8-sig').replace('\r\n','\n')
old='        int passed = 0;\n        Action<bool, string> check'
new='        float finishingHold = (float)Get(f, "finishingEffectHold");\n        int passed = 0;\n        Action<bool, string> check'
assert old in s;s=s.replace(old,new,1)
old='        try\n        {\n            fresh();'
new='        try\n        {\n            // Function checks advance combat synchronously; real-frame tests keep the ending hold.\n            Set(f, "finishingEffectHold", 0f);\n            fresh();'
assert old in s;s=s.replace(old,new,1)
old='        finally { f.BeginTutorial(false); }'
new='        finally { Set(f, "finishingEffectHold", finishingHold); f.BeginTutorial(false); }'
assert old in s;s=s.replace(old,new,1)
assert p.read_bytes()==b
p.write_text(s,encoding='utf-8',newline='\n')
print('TutorialChecks: ending hold restored in finally')
