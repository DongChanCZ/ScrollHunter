from pathlib import Path
import os,shutil,json,hashlib
work=Path(__file__).resolve().parent;root=work.parents[2]
dest=Path('C:/Users/MBC-501-19/Desktop/ScrollHunter_Trailer_KO');dest.mkdir(exist_ok=False)
for name in ['ScrollHunter_Trailer_KO_60s.mp4','ScrollHunter_Trailer_KO_60s.srt']:
    shutil.copyfile(work/name,dest/name)
    assert hashlib.sha256((work/name).read_bytes()).digest()==hashlib.sha256((dest/name).read_bytes()).digest()
shutil.copyfile(root/'Docs/CREDITS.md',dest/'Game_Asset_Credits.txt')
(dest/'영상 안내.txt').write_text('Scroll Hunter 트레일러 초안\n\n60초 / 1920×1080 / 30fps / H.264 + AAC\n한글 자막이 영상 안에 포함되어 있습니다. SRT는 자막 편집용이며 중복 표시되지 않게 주의하세요.\n\n실제 게임 화면을 촬영용 전투 세팅으로 녹화해 편집했습니다.\n일러스트: 게임 내 Intro05_Traveler.\n음악: bosslevelaudio - Evil Awaits (CC0), 게임 내 기존 음원.\n효과음: 게임의 실제 재생 시점을 기록한 뒤 기존 음원을 재배치했습니다.\n기존 게임 에셋 출처는 Game_Asset_Credits.txt 참조.\n게임 사양·배포 ZIP·실제 저장 파일은 변경하지 않았습니다.\n\n유튜브 업로드 뒤 itch.io 게임 페이지의 영상 항목에 연결하면 됩니다.\n아직 업로드하지 않았습니다.\n',encoding='utf-8-sig')
archive=Path(os.environ['TEMP'])/'ScrollHunter_TrailerSource_20261017';archive.mkdir(exist_ok=False)
for name in ['pilot','take01','edit']:
    source=(work/name).resolve();assert source.parent==work
    shutil.move(str(source),str(archive/name))
for name in ['render.py','TrailerCaptureTemporary.cs','edit-plan.json','ScrollHunter_Trailer_KO_60s.srt']:
    shutil.copyfile(work/name,archive/name)
# 최종 영상은 바탕화면에 검증된 사본이 있으므로 저장소에는 중복하지 않는다.
source=work/'ScrollHunter_Trailer_KO_60s.mp4';shutil.move(str(source),str(archive/source.name))
report=dict(video=str(dest/'ScrollHunter_Trailer_KO_60s.mp4'),subtitles=str(dest/'ScrollHunter_Trailer_KO_60s.srt'),sourceArchive=str(archive),bytes=(dest/'ScrollHunter_Trailer_KO_60s.mp4').stat().st_size,sha256=hashlib.sha256((dest/'ScrollHunter_Trailer_KO_60s.mp4').read_bytes()).hexdigest(),duration=60,width=1920,height=1080,fps=30)
(work/'delivery.json').write_text(json.dumps(report,ensure_ascii=False,indent=2),encoding='utf8')
print(json.dumps(report,ensure_ascii=False))
