# Joker Hand 기획서 인덱스

기획 기준 버전 **0.2** · 최초 사본 기준일 **2026-09-15**

[Google Drive 기획서 폴더](https://drive.google.com/drive/folders/1fegXSCeXbonvlz4YPeQ-uE3gRwb4fOVf)

편집용 문서 5종을 실제 Google Docs 형식으로 만들었다. 프로젝트에는 Google Docs를 여는 `.url` 바로가기와 같은 내용의 `.md` 읽기용 사본을 보관한다. Google Docs 문서 본체는 클라우드에 있으며 이 폴더의 바로가기가 본문을 저장하는 것은 아니다.

## 문서 목록

| 번호 | Google Docs | 로컬 사본 | 주요 내용 |
|---|---|---|---|
| 00 | [게임 개요와 핵심 루프](https://docs.google.com/document/d/1lX5IUVlKuw1VK0h-uhiWC4fRFIG6AZm0v3D2gNZWr9Y) | [00_Game_Overview.md](./00_Game_Overview.md) | 게임 목표, 상태 정의, 핵심·성장 루프, 범위와 용어 |
| 01 | [대전 시스템과 화면 명세](https://docs.google.com/document/d/1ZCsPXgiHvVHjejG_hgm131xprKsXz1OlbVUVcZr01oA) | [01_Match_System.md](./01_Match_System.md) | 카드와 상태 전이, 잠금·시간 초과, 공개 정보, UI와 기권 |
| 02 | [족보와 점수 시스템](https://docs.google.com/document/d/1WPzZx-Jx82CLlVoN8Dl5BELzhvxld4xl7Vl_bodN8ZA) | [02_Scoring_System.md](./02_Scoring_System.md) | 족보 순서, 숫자 보정, 점수표, 배율·버림·동점 |
| 03 | [조커 상세 기획](https://docs.google.com/document/d/1CfJwgKh8VxAxKt5ECpwFdQSK7p4TPbaVNEFuLEER5VI) | [03_Joker_Catalog.md](./03_Joker_Catalog.md) | 6종 상세 조건·수치·예외, 무료 세트, 검수 조건 |
| 04 | [성장과 AI 및 개발 관리](https://docs.google.com/document/d/1tnwD3k2Z0tlZIs8axBJAnlffXd3Ubl4z81vlza_zip4) | [04_Progression_AI_Development.md](./04_Progression_AI_Development.md) | 성장·보상·매칭·AI, 구현 상태, 변경 이력, 미결정 15항목 |

## 기획을 확인하는 순서

1. 처음 읽을 때는 00을 읽고 관련 상세 문서로 이동한다.
2. `확정 규칙`, `테스트 기준`, `방향 확정`, `구현 선택`, `미결정`을 구분한다.
3. 구현 상태는 04에서 확인한다. 정책 확정만으로 기능이 구현됐다고 보지 않는다.
4. 미결정 항목은 04의 O-01~O-15에서 확인한다. 새로운 결정을 기존 확정 사항인 것처럼 기록하지 않는다.
5. 코드보다 이후의 사용자 결정이 우선한다. 오래된 자료에서 개인 카드 5장이나 무늬 배수 규칙을 가져오지 않는다.

## 변경과 동기화

Google Docs와 Markdown은 **자동 동기화되지 않는다**. 기획을 바꾸기 전 연결된 Google Docs의 최신 내용을 읽고 로컬 사본과 대조한다. 편집 후 해당 사본·문서 버전·변경 이력과 `google_docs_manifest.json`의 확인 정보를 함께 갱신한다. 오프라인 사본만 사용한 경우 최신 Google Docs를 확인했다고 표현하지 않는다.

규칙과 수치는 문서 01~03을 기준으로 관리한다. 이후 사용자가 결정한 사항이 있으면 그 결정을 먼저 반영한다. 기존 `Docs/Prototype.md`는 실행 안내서이며 전체 기획서의 대체물이 아니다.

## 자료 기준

- 사용자 제공 원본: Requirements_v0.1.pdf, Game_Design_v0.1.docx, Design_Review_v0.1.docx.
- 이후 이 작업에서 사용자가 확정한 대전 구조, 점수, 조커, UI, AI 및 성장 정책.
- 구현 상태 확인: Assets/JokerHand의 코드와 Docs/Prototype.md.
- 현재 기획서의 규칙 ID와 예외·검수 항목은 구현과 논의를 추적하기 위해 부여했다.
- 원본 자료는 변경하지 않았다. 제작 과정의 Word·PDF·페이지 이미지 중간 파일은 기획서 본체로 사용하지 않는다.

## 현재 문서 확인 결과

5종의 모든 본문·표 셀 내용을 Google Docs 읽기 결과와 대조했다. 실제 제목 계층과 표 구조, 본문 Arial·검정 서식, 순서 목록이 변환 후 유지되는 것을 확인했다. 내보내기 전 렌더링은 기본 렌더러의 LibreOffice 부재로 Word의 비표시 PDF 변환을 사용하여 전체 30쪽을 시각 검수했다. 문서 제작은 기존 게임 코드를 변경하거나 새 밸런스 수치를 확정하지 않았다.

