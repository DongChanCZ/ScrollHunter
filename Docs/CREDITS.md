# CREDITS

**프로젝트** Scroll Hunter (가칭) — Soul Wanderer 재설계 | **제작** [ 이름 ] | **기간** [ ]

<!-- 빌드 폴더 및 프로젝트 루트에 함께 넣으세요. 포트폴리오는 공개물이므로 라이선스 확인은 필수입니다. -->

---

## 제작 범위

| 항목 | 담당 |
|---|---|
| 기획 (시스템, 수치, 레벨) | 본인 |
| 프로그래밍 | 본인 |
| 3D 모델 | 생성형 AI (VARCO 3D) + 본인 후처리 |
| VFX / SFX / BGM | 외부 무료 에셋 (아래 명시) |
| UI | [ ] |

### 1차 프로젝트와의 관계

이 프로젝트는 **Soul Wanderer**([기간], 팀 [O]명, 본인 기획 담당)의 전투 시스템을
재설계한 것입니다. 코드와 에셋은 전부 신규 작성했으며, 계승한 것은 설계 의도입니다.

1차 프로젝트는 팀에 아트 인원이 없어 기존 에셋 팩만으로 제작되었습니다.
이번 재설계에서는 설계를 먼저 확정하고 그에 맞는 에셋을 생성하는 순서로 전환했습니다.

---

## 사용 에셋

| 분류 | 에셋명 | 제작자 | 출처 URL | 라이선스 | 표시 의무 |
|---|---|---|---|---|---|
| VFX | [ ] | [ ] | [ ] | [ ] | [ ] |
| SFX | [ ] | [ ] | [ ] | [ CC0 ] | 불필요 |
| BGM | [ ] | [ ] | [ ] | [ ] | [ ] |
| 폰트 (일반 UI) | Gmarket Sans Medium | Gmarket | [공식 배포](https://corp.gmarket.com/fonts/), `Assets/_Project/Fonts/GmarketSansOTF/GmarketSansMedium.otf` | SIL Open Font License (공식 안내) | 라이선스 원문·배포물 동봉 확인 전 |
| 폰트 (피해 숫자) | NEXON Maplestory Bold | NEXON | 사용자 제공 `Assets/_Project/Fonts/NEXON_Maplestory/TTF/Maplestory Bold.ttf` | 제공 원문 확인 전 | 확인 전 |
| 폰트 (시작 제목) | Cinzel Bold | Natanael Gama / The Cinzel Project Authors | [공식 저장소](https://github.com/NDISCOVER/Cinzel), `Assets/_Project/Fonts/Cinzel/Cinzel-Bold.ttf` | SIL Open Font License 1.1 | 저작권·라이선스 원문 `Assets/_Project/Fonts/Cinzel/OFL.txt` 동봉 |
| 셰이더 / 툴 | [ ] | [ ] | [ ] | [ ] | [ ] |

---

## 사용 도구

| 도구 | 용도 |
|---|---|
| Unity 6.3 LTS (6000.3.18f1) | 엔진 |
| VARCO 3D | 적 이미지·3D 시안 생성, 약탈자 손가락 리깅 |
| Blender 5.1.2 | 궁병 손 메시·UV 보정, 모델·리깅·임시 자세 검사 |
| [ ] | [ ] |

## 생성형 3D 시안 (2026-10-01, 게임 적용 전)

- VARCO `ScrollHunter` 워크플로에서 약탈자·궁병 이미지와 3D 생성. 약탈자는 같은 서비스에서 손가락 리깅. 궁병은 Blender로 손 분리·UV 보정 후 임시 뼈대로 옷 자세 검사.
- 원본·수정본·실제 입력은 `Docs/근거자료/2026-10-01_적모델링/`에 보존. [약탈자 생성 기록](근거자료/2026-10-01_적모델링/01_약탈자_생성프롬프트.txt)·[궁병 생성 기록](근거자료/2026-10-01_적모델링/03_궁병_생성프롬프트.txt), 검사·가공 범위는 [07](07_개발일지.md#d12-close).
- 두 모델은 최종 채택·Unity 적용 전. 생성물의 배포·표기 조건 확인 결과는 아직 미기록이며 CC0 에셋으로 분류하지 않는다. 애니메이션 자료는 아직 생성·반입하지 않음.

## 무료 UI 시험 (2026-09-28)

- RPG GUI construction kit v1.0 — Matjaž Lamut (Lamoot). https://opengameart.org/content/rpg-gui-construction-kit-v10 — CC BY 3.0: https://creativecommons.org/licenses/by/3.0/ . 원본 PNG의 프레임·버튼 영역을 Sprite로 분리하고 크기·색을 조정. 원본 ReadMe는 `Assets/_Project/UI/FantasyTrial/Lamoot-License.txt`에 보존.
- FANTASY-parchment-set — Melissa Krautheim (MELLE). https://opengameart.org/content/fantasy-parchment-set — CC0: https://creativecommons.org/publicdomain/zero/1.0/ . `parchment2.png`는 1차 다음 카드 장식용. 후속 UI에서는 미사용, 원본 보존.
- 이번 시험에서 유료 에셋·유료 샘플 사용 없음. 배포 시 위 출처·라이선스·변경 사항을 크레딧에 유지.
- 스킬 아이콘 11종 — Lorc, Game-icons.net. CC BY 3.0: https://creativecommons.org/licenses/by/3.0/ . 흰색/투명 PNG 원본 사용, Unity에서 크기·표시색 조정. 파일·원본 링크는 `Assets/_Project/UI/FantasyTrial/Icons/CREDITS.txt`.
- 2026-09-28 후속 교체 — 스킬 아이콘 14종(home 병합 후 SK08·09·14 3종 추가): Painterly Spell Icons part 1~4 — J. W. Bjerk (eleazzaar), www.jwbjerk.com/art, https://opengameart.org/content/painterly-spell-icons-part-1 (part-2·3·4 동일 경로) — CC BY 3.0 선택 적용(작가 제공 CC-BY-SA·GPL 복수 라이선스 중): https://creativecommons.org/licenses/by/3.0/ . 원본 256px PNG 무가공, Unity에서 표시 크기만 조정. 파일별 출처·작가 README는 `Assets/_Project/UI/PainterlyIcons/`. 위 Lorc 아이콘은 SkillData 연결에서 빠졌고 파일은 보존.
- 2026-09-28 후속 — 정보창 테두리·손패 프레임: Fantasy UI Borders — Kenney, https://kenney.nl/assets/fantasy-ui-borders — CC0: https://creativecommons.org/publicdomain/zero/1.0/ . `panel-border-002`·`panel-border-014`·`panel-002`(Double) 사용, 9-slice 테두리 설정·색만 조정. 원본 License는 `Assets/_Project/UI/KenneyBorders/Kenney-License.txt`.
- 손패 상하 음영 `Assets/_Project/UI/HandSlot/SlotShade.png`는 이번 작업에서 Unity로 생성한 단순 그라데이션(외부 에셋 아님).
- 2026-09-28 생츄어리 신성한 빛 연출: Light Masks — Kenney, https://kenney.nl/assets/light-masks — CC0 (`streaks_composed_e·g·h` 빛기둥, `ring_b` 마법진 고리). Particle Pack — Kenney, https://kenney.nl/assets/particle-pack — CC0 (`circle_05` 광원·빛 입자, `star_06` 반짝임, `magic_02` 마법진 문양). 원본 PNG 무가공, Unity에서 크기·회전·색·투명도와 Animator 맥동만 적용. 라이선스 원문은 `Assets/_Project/UI/SanctuaryVfx/`.
- 2026-09-28 스킬 연출(파이어볼·라이트닝 스피어·매직아머): Particle Pack — Kenney, https://kenney.nl/assets/particle-pack — CC0 (`fire_01·02`, `flare_01`, `scorch_01`, `circle_02`, `smoke_04`, `spark_01·02·03·05·06` + 생츄어리용 `circle_05`·`star_06` 재사용). 원본 PNG 무가공, Unity 파티클 시스템·UI Animator로 크기·색·수명만 설정. 라이선스 원문은 `Assets/_Project/VFX/Textures/`.
- 2026-09-28 스킬 연출 추가(아이스애로우·파이어 필라·침묵): 같은 Kenney Particle Pack(CC0)의 `flame_05·06`, `trace_01·02·06`, `magic_01` 추가 + 기존 텍스처 재사용. 원본 PNG 무가공.
- 아이스애로우 화살 꼬리 `Assets/_Project/VFX/Textures/TrailSoft.png`는 이번 작업에서 Unity로 생성한 단순 그라데이션(외부 에셋 아님).
- 2026-09-29 스킬 연출(매직 커터·매직 스파크·매직 임팩트), 모두 OpenGameArt CC0: "lightning" — wreaderror, https://opengameart.org/content/lightning (보라 전류 4장) / "Weapon Slash - Effect" — Cethiel, https://opengameart.org/content/weapon-slash-effect (파랑 애니메이션 5) / "Explosion effects and more" — Soluna Software, https://opengameart.org/content/explosion-effects-and-more (Explosion03·Effect95). 가공: 전류는 밝기에서 투명도 생성, 베기는 6프레임을 한 줄로 결합. 색·크기·재생은 Unity에서 설정. 에셋 목록은 `Assets/_Project/VFX/OpenGameArt/CREDITS.txt`, 가공 내역은 이 기록을 따른다.
- 2026-09-29 스킬 연출(다크 이럽션·제압·블리자드·저지먼트): Kenney Particle Pack(CC0)의 `twirl_02`·`dirt_01`·`dirt_03`·`scorch_02`·`circle_05` 추가(원본 PNG 무가공) + 기존 OpenGameArt lightning 재사용. 마법진은 OpenGameArt CC0 "4 Summoning Circles" — Luke.RUSTLTD, https://opengameart.org/content/4-summoning-circles (circle7·circle4, 검은 선을 흰 선+투명도로 변환·512 축소·선 두께 보강). 색·크기·재생은 Unity에서 설정. 목록은 `Assets/_Project/VFX/OpenGameArt/CREDITS.txt`.

## 전체 UI 디자인 (2026-10-01)

- 제목 폰트 후속: Cinzel 공식 `fonts/ttf/Cinzel-Bold.ttf`를 무가공 사용(SHA256 `0C23EC565DB45C5508EE95889C60AD87DEBD167CA07167A43A5D68572B4E2EAC`). 같은 폴더의 제목용 TMP SDF·Shadow 재질은 Unity에서 생성. 배포 시 OFL 원문 함께 포함. Noto Serif KR Black 전투 표시는 시험 후 사용자 요청으로 원복·에셋 제거(07#d12-fonts).

- 신규 — Fantasy Icon Pack by Ravenmore (Ravenmore Icon Pack v2.0) — Ravenmore (Krzysztof "Ravenmore" Dycha), https://ravenmore.itch.io/ · 배포 원본 https://opengameart.org/content/fantasy-icon-pack-by-ravenmore-0 (`RavenmoreIconPack.02.2014.zip`) — CC-BY 3.0: https://creativecommons.org/licenses/by/3.0/ . `potionRed.png`(전투 HUD 포션 칸)·`upg_shieldSmall.png`(전투 HUD 방어도 방패) 2종만 사용. 512px 원본 이미지 무가공, Unity에서 표시 크기·색 곱만 적용. 배포 페이지의 라이선스·표기 안내와 CC BY 3.0 원문은 `Assets/_Project/UI/Ravenmore/Ravenmore-License.txt`. 다른 조건의 itch.io 판은 사용하지 않음.
- 기존 출처 추가 사용 — Kenney Fantasy UI Borders(CC0, 위 2026-09-28 항목): `divider-fade-004.png`를 제목·능력치 창 구분선으로 좌우 반전 2개씩 사용(원본 무가공, 색·크기만). 기존 `panel-border-002`는 설명 패널·능력치 창·적 정보창·시전 바·시작 화면 조작 안내·튜토리얼 안내 테두리로 사용.
- 기존 출처 가공 — RPG GUI construction kit v1.0(Lamoot, CC BY 3.0, 위 2026-09-28 항목) 아틀라스에서 영역만 잘라 Sprite로 분리: `GaugeFrame`(긴 금테 막대 → HP·코스트·시전 게이지 테두리), `StudLeft`·`StudRight`(삼각 금테 → HP 게이지 양 끝), `StudDownLarge`(아래 방향 문양 → 타겟 마커), `FramedTileLarge`(금테 나무 칸 → 포션 칸). 기존 `Button` 판은 설명 패널·능력치 창·시전 바·결과/시작/튜토리얼 판에 변경 전 청흑색 색조로 재사용.
- 기존 출처 합성 — `Assets/_Project/UI/FantasyTrial/ButtonChamfer.png`: 같은 팩 `Button` 판 버튼의 중앙 프레임(금색 띠·안쪽 어두운 선·회색 나무결) 픽셀만 써서 좌우 끝 장식 없이 네 변을 같은 두께로 두르고 모서리를 7px 사선으로 깎아 새로 구성한 9-분할 버튼(가공물, CC BY 3.0 표기 유지). 2026-10-01 시작 버튼 시험 후 공통 조작 버튼으로 확대. 후속은 같은 이미지를 재사용하며 추가 가공 없음(07#d12-common-buttons).
- Unity에서 생성(외부 에셋 아님): `Assets/_Project/UI/HudGauge/GaugeFill.png`(게이지 채움용 흰색 세로 그라데이션·잔결), `Vignette.png`(전체 화면 가장자리 음영). 글자 재질 `GmarketSansMedium SDF - HUD Shadow/Outline.mat`은 기존 폰트의 그림자·외곽선 설정(폰트 변경 없음).
- 레퍼런스 게임(Torchlight II·Ravenswatch·Hand of Fate 2·Grim Dawn) 화면 이미지는 추출·사용하지 않음.
