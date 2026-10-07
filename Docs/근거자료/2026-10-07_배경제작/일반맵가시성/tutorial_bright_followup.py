from pathlib import Path

root = Path(__file__).resolve().parents[4]
docs = root / 'Docs'
paths = [root/'Assets/_Project/Editor/BattleEnvironmentSetup.cs',
         docs/'06_에셋_파이프라인.md', docs/'07_개발일지.md',
         docs/'10_결정사항_로그.md', docs/'12_기획세션_인계프롬프트.md',
         root/'AGENTS.md', root/'CLAUDE.md']
before = {p: p.read_bytes() for p in paths}
texts = {p: b.decode('utf-8-sig').replace('\r\n', '\n') for p,b in before.items()}

def replace(p, old, new):
    assert texts[p].count(old) == 1, (p, old)
    texts[p] = texts[p].replace(old, new, 1)

replace(paths[0], 'new Color(.64f,.79f,.85f)', 'new Color(.86f,.96f,.86f)')
replace(paths[1], '일반 바위는 `P04_MossRocks_Outdoor`,',
        '튜토리얼 후속은 하늘·풀밭 배경을 더 밝고 따뜻하게 조정. 나무·바위는 앞선 가시성 보정 유지. 일반 바위는 `P04_MossRocks_Outdoor`,')
replace(paths[2], '다음: 수정된 일반 맵에서 적 몸·무기·공격이 잘 구분되는지 직접 확인 후 새 빌드 점검.',
        '- 튜토리얼 밝기 후속: 사용자 요청으로 숲 초입 배경만 더 밝고 따뜻하게 조정. `01_ForestEdge` 색 보정 RGB (0.64, 0.79, 0.85) → (0.86, 0.96, 0.86). 나무·바위·다른 맵·전역 조명 유지. 배치 도구와 재질 저장값 동기화.\n'
        '- 후속 화면: `26_tutorial_bright_final.png`(1920×1080). 안내창을 임시로 닫아 중앙 깡패·게이지·HUD 식별 확인 후 Play 종료. 씬 추가 저장·새 빌드 없음. 위 배경31 검사는 이 밝기 후속 전 결과.\n\n'
        '다음: 수정된 일반 맵에서 적 몸·무기·공격이 잘 구분되는지 직접 확인 후 새 빌드 점검.')
replace(paths[3], '- 오솔길 나무 6개를 양옆으로 이동.',
        '- 튜토리얼은 후속 요청으로 하늘·풀밭을 더 밝고 따뜻하게 조정. 나무·바위는 가시성 보정 유지.\n- 오솔길 나무 6개를 양옆으로 이동.')
replace(paths[4], '숲 초입·오솔길·동굴 입구만 조정, 보스 배경·전투 수치 유지.',
        '숲 초입·오솔길·동굴 입구만 조정. 후속 요청으로 튜토리얼 하늘·풀밭은 더 밝고 따뜻하게 보정. 보스 배경·전투 수치 유지.')
assert before[paths[5]] == before[paths[6]], '인계 파일 상이함'
old = '수치·새 빌드·커밋 변경 없음.\n'
new = '수치·새 빌드·커밋 변경 없음. 후속 사용자 요청으로 튜토리얼 배경만 더 밝고 따뜻하게 조정(`01_ForestEdge` RGB 0.86/0.96/0.86), 소품·다른 맵 유지. 배치 도구·재질 동기화, 1920×1080 화면 확인 후 Play 종료. 배경31은 밝기 후속 전 결과.\n'
for p in paths[5:]: replace(p, old, new)
for p in paths: assert p.read_bytes() == before[p], str(p)+' 동시 수정됨'
for p in paths: p.write_text(texts[p], encoding='utf-8', newline='\n')
print('튜토리얼 밝기: 배치 도구·06·07·10·12·AGENTS·CLAUDE 반영')
