# 할아버지와 로봇 — 곳간·우물 프로토타입

Unity **6000.6.8f1**, 기존 URP/Input System/Pipeline 버전을 사용합니다. 진입 씬은 `ai-hack-test/Assets/Scenes/WellPrototype.unity`입니다.

처음에는 농장의 무너진 곳간이 열리며, 잔해 정리 → 위치·형태 맞추기 → 재건 순서로 진행합니다. HUD의 곳간/우물 버튼으로 전환합니다. 엑셀 기반 조립 조건과 지팡이 자료는 [곳간 퍼즐 안내](house-puzzle.md)에 설명되어 있습니다.

## 실행

1. 저장소 루트에서 `powershell -ExecutionPolicy Bypass -File tools/start-robot-bridge.ps1`로 중계를 실행합니다. 기존 Codex CLI 로그인과 네트워크 연결이 필요합니다. 로그인은 `codex login`으로 직접 진행합니다.
2. Unity에서 진입 씬을 열고 Play를 누릅니다. 현재 변경은 Play Mode 검증 대상으로, 빌드는 생성하지 않습니다. 기존 Windows 실행 파일은 이 변경 전 버전입니다.
3. 중계가 없거나 CLI가 실패하면 대체 모드가 표시됩니다. `-FallbackOnly` 옵션으로 서버를 실행하면 대체 파서만 사용합니다.

| 입력 | 동작 |
|---|---|
| WASD | 카메라 기준 할아버지 이동 |
| Q | 현재 단계의 배치 단서를 3초간 회상 |
| E | 부탁 입력창 |
| R / Shift+R 유지 | 양방향 일정 각속도 연속 회전 |
| + / − | 화면이 맵 바닥 안쪽에 머무르는 범위의 줌 |
| Enter / Ctrl+Enter / Esc | 조합 확정·줄바꿈 / 부탁 전송 / 입력창 닫기 |
| 중단 / 초기화 버튼 | 현재 요청 중단 / 스테이지 초기화 |

로봇은 직접 조작하지 않습니다. 물체 위에는 ID가, 바닥에는 노드 ID가 표시됩니다. 대체 파서는 `B1을 N1에 놓아`, `T1을 A1에 옮겨`, `P1을 90도로 돌려`, `B1을 집어`, `N1로 이동`처럼 명시적인 한 가지 부탁을 처리합니다. Codex는 같은 계약 안에서 자연어를 해석합니다. 응답이 모호하면 질문하고, 실패 시 남은 행동을 중단합니다.

## 퍼즐과 데이터

Q로 큰 돌·작은 돌의 순서와 위치, 지지대, 도르래 방향을 확인하세요. 잘못 놓은 물체는 임시 노드 A1~A4로 치울 수 있습니다. 위에 물체가 있거나 부품을 받치는 물체는 위쪽 부품부터 옮겨야 합니다. 운반 중 중단해도 들고 있는 물체는 유지됩니다.

스테이지와 물체 정의는 `Assets/Data`에 있습니다. 에셋은 `PrototypeCatalog.asset`의 TextAsset/프리팹/재질/소리 참조를 사용하므로 빌드에서도 동작합니다. `well_fixture.json`은 재구성 검증용 두 번째 배치이며 별도 게임 콘텐츠가 아닙니다.

- Sim: 퍼즐 상태와 배치·순서·방향 판정.
- Game: 스테이지 수명주기, 공개 상태, 명령 계약, 검증·이동·실행.
- Presentation: 쿼터뷰, UI Toolkit HUD, 회상·툰 표현.
- Data: JSON DTO와 에셋 카탈로그. Editor: Unity API를 통한 에셋·씬 생성 및 검증.

이는 [CoreDawn의 구조](https://github.com/Jun1188/CoreDawn/tree/Master/Assets/Scripts)를 이 프로토타입에 맞게 축소한 것입니다. 공장·전투 시스템은 포함하지 않습니다.

할아버지 모델은 사용자가 제공한 `grand_9.fbx`를 `Assets/Art/Models/Grandfather`에 복사했습니다. 생성 도구가 높이를 1.9단위로 맞추고 바닥 중심을 피벗에 맞춥니다. Mixamo 본·스킨은 휴머노이드 아바타로 매핑하며, 추가 제공된 `Idle.fbx`와 `grand_9walk.fbx`(프로젝트에서는 `Walk.fbx`)도 각각 휴머노이드로 임포트해 본 계층 차이를 리타게팅합니다. WASD 이동 시작/종료 시 `Walk`/`Idle` 트리거를 보내 0.15초 전환합니다. 채팅 중이거나 장애물에 막히면 Idle이며, 루트 모션은 끕니다. 외형은 별도 프리팹이므로 이동 루트와 교체 가능합니다.

본체 FBX에는 UV와 본·스킨이 있으며, 색상 자료는 추가 제공된 `GRAND`의 별도 `.fbm` 폴더에서 연결합니다. UV 좌표와 색상 이미지/머티리얼 연결은 서로 다른 데이터입니다. 캐릭터는 기본 URP/Lit과 Color/Normal/Metallic/Roughness 자료를 사용하고, 맵·소품은 툰 재질을 사용합니다. 툰 셰이더도 UV 기반 BaseMap 텍스처를 지원합니다.

할아버지 프리팹의 `GrandfatherLocomotion`이 Animator/컨트롤러를 직접 보관하고 시작 시 Idle을 적용합니다. 이동 상태 변화는 Walk/Idle 트리거로 전달하며 가감속·회전 보간·정지 유예와 실제 이동 속도에 따른 WalkRate를 사용합니다. HUD는 IMGUI에서 UI Toolkit/UIDocument로 교체했습니다. 표준 TextField에 IME를 연결하고, flex/wrap/overflow로 패널을 배치합니다. 조합 확정 Enter와 전송을 분리해 Ctrl+Enter 또는 버튼으로 전송합니다.

`Grandpa Robot > Refresh HUD and Stage Preview`로 편집용 스테이지를 갱신할 수 있습니다. Hierarchy에 Environment/Parts/Nodes/Memory/Characters가 저장되며, Play 시작 때 JSON 데이터로 재구성됩니다. 미리보기를 직접 편집한 내용은 JSON으로 역저장되지 않습니다. 원인 분석과 검증 한계는 [문제 원인 점검](root-cause-review.md)에 기록했습니다.

우물은 24×20, 곳간은 30×26 바닥에 마당·흙길·풀밭·무너진 돌·울타리 등을 배치합니다. 배경도 JSON 객체이며 비상호작용/이동 차단 여부를 별도로 지정합니다. 카메라는 모든 화면 모서리의 바닥 투영점이 맵 경계 안에 들어오도록 줌아웃 한계를 매 회전 각도·화면 비율에서 계산합니다. 곳간은 지붕 높이를 고려해 주시점을 올립니다. 전체 외곽을 한 화면에 담는 디오라마 카메라와 달리, 화면에는 맵 안쪽이 보이고 바깥쪽 장식은 일부 잘릴 수 있습니다.

## AI 중계

`GET http://127.0.0.1:8765/health`, `POST /interpret`를 제공합니다. 요청에는 requestId, stageRunId, prompt, 공개 snapshot만 포함합니다. 정답·회상·선행 조건은 전송하지 않습니다. 응답은 status(plan/clarify), message, actions, provider와 원래 요청 ID들을 포함합니다.

행동은 `move_to`, `pick_up`, `place`, `rotate`, `say`, `push`, `adjust`이며 최대 12개입니다. 각 행동 필드는 type, objectId, targetId, rotation, text입니다. rotate는 상대 회전이 아니라 절대 방향(0/90/180/270도)입니다. 실행 시 Unity가 다시 상태를 검사합니다. 이전 스테이지나 중단한 요청의 응답은 무시합니다.

Codex는 [공식 비대화형 실행과 JSON Schema 출력](https://learn.chatgpt.com/docs/non-interactive-mode)을 사용합니다. 프로젝트 밖 임시 디렉터리, 읽기 전용 sandbox, 사용자 설정 제외, 셸·앱·브라우저·이미지 도구 비활성화로 실행합니다. 도구 호출 이벤트가 있으면 결과를 거절합니다. 기존 CLI 로그인만 사용하고 OPENAI_API_KEY는 자식 프로세스 환경에서 제외합니다. CLI는 로컬에서 실행되지만 모델 추론에는 인터넷이 필요합니다. 요청 제한시간은 30초입니다.

중계는 loopback에만 바인딩하고 플레이어 문장·모델 응답·CLI stderr·인증 정보를 로그에 출력하지 않습니다. 별도 API 공급자를 추가할 때는 서버 환경변수에서 키를 읽고 Unity 빌드에 넣지 않습니다.

## 재생성·검증

씬 생성은 `unity run ai-hack-test --timeout 300 -- -executeMethod GrandpaRobot.Editor.PrototypeBuilder.Generate`입니다. 기존 Editor가 열려 있다면 `Grandpa Robot > Generate Prototype` 메뉴 또는 연결된 Pipeline 명령으로 호출합니다. 재생성은 프로토타입 씬·프리팹을 다시 만들므로 수동 편집을 저장한 뒤 사용하세요.

규칙·참조 검증은 `GrandpaRobot.Editor.PrototypeChecks.Run`입니다. Python 검증은 `python -m unittest discover -s tools -p test_robot_bridge.py -v`입니다. 현재 작업에서는 빌드를 생성하지 않으며 플레이 검증은 사용자가 Editor Play Mode에서 수행합니다.

이전 Windows 빌드의 selftest.json과 캡처는 농장 잔해 정리 단계 추가 전의 기록입니다. 이번 씬에는 자동 플레이 검사 컴포넌트를 연결하지 않습니다.

음성, 신뢰도 성장, 대기·걷기 이외의 애니메이션, 동적 물리 도르래, 수로, 저장·불러오기는 이번 버전에서 제외합니다.
