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
| 폰트 | [ ] | [ ] | [ ] | [ ] | [ ] |
| 셰이더 / 툴 | [ ] | [ ] | [ ] | [ ] | [ ] |

---

## 사용 도구

| 도구 | 용도 |
|---|---|
| Unity 6.3 LTS (6000.3.18f1) | 엔진 |
| VARCO 3D | 3D 모델 생성 |
| [ ] | [ ] |

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
- 2026-09-29 스킬 연출(매직 커터·매직 스파크·매직 임팩트), 모두 OpenGameArt CC0: "lightning" — wreaderror, https://opengameart.org/content/lightning (보라 전류 4장) / "Weapon Slash - Effect" — Cethiel, https://opengameart.org/content/weapon-slash-effect (파랑 애니메이션 5, 6프레임을 한 줄로 배치) / "Explosion effects and more" — Soluna Software, https://opengameart.org/content/explosion-effects-and-more (Explosion03·Effect95). 원본 PNG 무가공, 색·크기·재생만 Unity에서 설정. 목록은 `Assets/_Project/VFX/OpenGameArt/CREDITS.txt`.
