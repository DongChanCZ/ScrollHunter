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
