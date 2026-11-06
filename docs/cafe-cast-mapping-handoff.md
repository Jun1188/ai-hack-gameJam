# 손님 페르소나·모델 매핑 인계 패킷 — 2026-11-06

## 완료 내용

- 앞선 developer→판교 회사원, japan→학생 연결을 유지했다.
- `Downloads/gyeongsang.zip`의 FBX 3개와 텍스처 4개를 새 `Incoming/cus_gyeongsang/`에 복사했다. 원본 ZIP은 수정하지 않았다. 전용 재질·Idle/Walk Controller·Appearance Prefab을 생성했다.
- 경상도는 후드·안경·배낭의 20~30대 남성으로 조정했다. 할머니 설정과 손주 관련 문구는 남성 손님·가족 이야기로 맞췄다. 사투리·주문·진행 ID는 유지했다.
- 어르신은 김계란 원본 모델, 충청도·강원도·동남아·일본·시각장애인 손님은 Black 남성 모델을 공유한다. 남성 공용 그룹은 20~30대로 맞췄다.
- 전라도·서양·중국·바리스타 지망생·트레이너는 기존 Black/Female 변형을 공유하며 20~30대 여성으로 맞췄다. 제주만 노년 여성 설정을 유지한 임시 여성 모델 연결이다.
- 여성 추첨 확률 하한 40%, 미쿠 5%를 유지한다. 현재 캐스트의 여성 확률은 44.12%이므로 균등 추첨을 유지하고, 여성 후보가 적을 때만 가중치를 높인다. 짧은 표본에서의 실제 등장 비율을 강제하는 할당제는 아니다.
- 18명 모두 ready 카탈로그 항목과 연결된다. 자기 원본 모델이 없는 손님은 12명이며, 김계란의 자기 원본 모델은 어르신과 공유한다.

## 수정 파일

- `ai-hack-test/Assets/CafeSim/Data/personas.json`: 승인된 나이·성별·외형 조정.
- 새 `Data/CustomerAppearanceProfiles.json` 및 `.meta`: 재생성 후 적용할 설정.
- 새 `Data/CustomerAppearanceMappings.json` 및 `.meta`: 명시적 공용 매핑.
- `Data/CustomerDeliveryManifest.json`: 3개 신규 ZIP을 포함한 원본 경로·SHA-256 기록. developer/japan 항목은 이전 요청에서 추가했다.
- `Editor/CafeCustomerCatalogImport.cs`: 공용 매핑 정의를 읽고 재생성·이름 추정보다 우선 적용.
- `Editor/CafeCustomerDelivery.cs`: 경상도 및 기존 여성 변형의 준비 경로 추가.
- `Runtime/CafeQueueSim.cs`: 여성 확률 하한, 단일·여성 없는 캐스트 호환.
- `Resources/CafeCustomers/CustomerCatalog.json`: 생성 결과.
- 새 `Resources/CafeCustomers/Incoming/cus_gyeongsang/`, `shared_adult_female/` 및 메타. 후자는 기존 Black/Female·Idle·Walk·텍스처를 재사용하는 Prefab/Material/Controller만 포함한다.
- `Resources/CafeKnowledge/conversation.json`, `situations.json`: 경상도의 손주 표현을 가족으로 조정.
- 새 `tools/apply-cafe-appearance-profiles.py`: 원본 엑셀을 수정하지 않고 승인된 설정을 재적용.
- `tools/apply-cafe-persona-branches.py`: 데이터 재생성 후 위 프로필 적용.
- `tools/cafe-additional-customers-prepare.cs`: 경상도 포함 세 모델만 준비, 변경된 PrepareFamily 인수 대응.
- 새 `tools/cafe-cast-mapping-check.cs`: 실제 카탈로그 선택·Spawn·컵 그립·추첨 분포 및 격리 미리보기 검사.
- `tools/cafe-persona-branch-check.cs`: 승인된 공유를 반영하고 미쿠 독점·분기 독립성을 계속 확인.
- `docs/cafe-customer-assets.md`, 새 `docs/images/cafe-cast-mapping-preview.png`, 이 인계 파일.

커밋·스테이징·푸시는 하지 않았다. 다른 작업의 HUD·메뉴·서빙·CafeCoworker 변경은 함께 존재하며 건드리거나 되돌리지 않았다. 위 목록은 이번 작업 범위이며 전체 Git 변경 목록이 아니다.

## 검증 결과

- 연결된 Unity 6000.6.8f1 Editor의 변경 코드 로드·검사 스크립트 컴파일/실행 성공.
- 18명 모두 ready 모델 선택, 실제 `CafeCustomerAppearance.Spawn` 경로(폴백 미사용), Animator Controller의 Idle/Walk, 컵 그립 참조 통과.
- 50,000회 캐스트 추첨: 여성 44.30%, 미쿠 4.97%. 현재 이론 확률 44.12% / 5%에 부합.
- 여성 1명/남성 9명 캐스트를 50,000회 추첨: 여성 39.94%, 이론 하한 40%에 부합. 단일 인물 및 남성 전용 캐스트도 통과.
- 기존 접수 검사: 즉시 접수·활성 인원 수별 접수 시간·서빙/시간초과 후 승격, 72개 페르소나/메뉴 연결, 96개 후속 답변 통과.
- 분기 회귀 검사: 미쿠·김계란 독립 대사·후속 답변·스폰·컵 그립, 장부 진행 분리, 원래 16명 엔딩 조건 통과.
- 경상도·공용 여성·어르신 실제 Spawn 결과를 격리된 Editor 미리보기에서 렌더하고 확인. 미리보기 왼쪽부터 해당 순서.
- 경상도 원본 7개 파일의 SHA-256 일치, 프로필 적용 멱등성, `git diff --check` 통과.
- package manifest/lock SHA-256은 시작과 동일. 씬·빌드 설정을 변경하거나 새 빌드를 생성하지 않았다.

미검증: 본편 Play/연속 보행·턴·컵과 의상 간섭·실제 서빙·HUD 조작·빌드·다른 PC. 참조와 격리 Spawn 검사는 실제 플레이 체감을 증명하지 않는다.

## Editor 인계 상태

최종 확인 시 Play 종료(`false`), 컴파일 중 아님(`false`), 활성 씬 `Assets/CafeCoworker/Scenes/CafeServiceRegression.unity`, 로드된 씬 1개, 활성 씬 미저장 변경 없음(`isDirty=false`). 이 작업은 활성 씬을 열거나 저장하거나 바꾸지 않았다. 해당 상태는 확인 시점의 스냅샷이며 다른 채팅/사용자가 이후 변경할 수 있다. 프로젝트 에셋은 저장했고 Git 변경은 미커밋 상태다.

## 남은 사항

- 사용자 추가 예정인 **20대 여성 모델**을 받으면 전라도·서양·중국·바리스타 지망생·트레이너의 공용 여성 연결을 새 모델로 바꾼다. 별도 성격·주문·진행은 유지한다.
- **할머니 모델**은 제주 손님에 연결한다. 경상도는 새 남성 전용 모델을 유지한다.
- 본편 Play 검증은 사용자 또는 이를 담당하는 채팅에서 수행한다. 현재 편집 상태를 다시 확인한 후 진행한다.

결과 이미지: `docs/images/cafe-cast-mapping-preview.png`. 매핑 표: `docs/cafe-customer-assets.md`의 최신 공용 매핑 절. 인계와 검증을 위해 별도 빌드는 필요하지 않다.
