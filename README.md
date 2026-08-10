# 우드득 — Unity Game Client

Unity 기반 멀티플레이 게임 프로젝트 **「우드득」**의 기술문서 참고용 코드 저장소입니다.

본 저장소는 전체 Unity 프로젝트가 아니라, 기술문서에서 설명한 직접 구현한 C# 스크립트 중심으로 구성했습니다.

## GitHub

- Repository: https://github.com/Sonks1126/Wooduduk

## 프로젝트 개요

우드득은 별도의 게임 서버 없이 **Firebase Realtime Database(RTDB)**를 활용해 매칭, 실시간 동기화, 게임 결과 정산을 구현한 프로젝트입니다.

초기에는 FishNet + EdgeGap 기반 서버 구조를 설계했으나, 게임 방향 변경에 따라 해당 구조를 비활성화하고 현재는 Firebase RTDB를 중심으로 전체 멀티플레이 흐름을 구성했습니다.

주요 시스템은 다음과 같습니다.

- Firebase RTDB 기반 매칭
- 플레이어 실시간 상태 동기화
- 사망 및 랭크 정산
- 계정 및 데이터 관리
- Unity Editor 기반 Firebase 디버그 툴

## 주요 구현

### 1. Firebase RTDB 기반 매칭

`FirebaseRoomMatchSource`와 `MatchmakingFlow`를 통해 Firebase RTDB 기반 매칭을 구성했습니다.

Firebase RTDB의 단일 필드 쿼리 제약을 해결하기 위해 `stateAndTier` 복합키를 사용했습니다.

```text
stateAndTier = "waiting_3"
```

상태와 티어를 하나의 검색 키로 구성하여 대기 중인 방을 효율적으로 탐색할 수 있도록 설계했습니다.

매칭 과정에서는 정확히 같은 티어를 우선 탐색한 뒤 티어 차이를 단계적으로 확장하는 방식으로 방을 검색합니다.

### 2. 원자적 방 생성

방 정보와 플레이어 정보가 순차적으로 저장될 경우, 저장 과정 중 빈 방이 다른 클라이언트에 노출될 수 있는 Race Condition이 발생할 수 있습니다.

이를 해결하기 위해 `UpdateChildrenAsync()`를 활용하여 방 정보, 플레이어 정보, `activeRooms` 등 관련 데이터를 하나의 업데이트 작업으로 원자적으로 기록했습니다.

이를 통해 방 생성 과정에서 불완전한 상태가 외부에 노출되는 문제를 방지했습니다.

### 3. 실시간 동기화

`RoomLiveSync`와 `RoomRepository`를 통해 플레이어의 실시간 상태를 Firebase RTDB에 기록하고 다른 플레이어의 상태 변화를 수신하도록 구성했습니다.

주요 흐름은 다음과 같습니다.

```text
Client
  ↓
rooms/{roomId}/live/{uid}
  ↓
Firebase RTDB
  ↓
ValueChanged
  ↓
Other Clients
```

실시간 상태는 일정 주기로 기록하며, Firebase의 `ValueChanged` 이벤트를 통해 변경 사항을 수신합니다.

### 4. 사망 및 랭크 정산

`GameController.HandleDeath()`를 중심으로 플레이어의 사망 정보를 기록하고, 모든 플레이어의 사망 정보가 충분히 수집되면 랭크를 정산합니다.

사망 시 `ServerValue.Timestamp`를 함께 기록하고 시간 순으로 정렬하여 생존 순서를 계산합니다.

중복 정산을 방지하기 위해 `_ranksWritten` 멱등성 가드를 사용했으며, 정산 결과는 `ranks/{uid}`에 기록합니다.

### 5. 이벤트 기반 게임 흐름

게임 시스템 간 직접적인 참조를 줄이기 위해 이벤트를 활용하여 주요 상태 변화와 시스템 간 흐름을 연결했습니다.

로그인 성공, 매칭 완료, 게임 시작, 사망, 결과 정산 등의 이벤트를 각 시스템이 필요한 시점에 수신하도록 구성했습니다.

이를 통해 각 시스템의 책임을 분리하고 기능 간 결합도를 낮추는 방향으로 설계했습니다.

### 6. Unity Editor Debug Tool

`SaveManagerDebugWindow`를 비롯한 Unity Editor 전용 디버그 툴을 구현하여 Firebase 데이터와 저장 상태를 확인할 수 있도록 구성했습니다.

게임을 실행하지 않고도 개발 과정에서 저장 데이터와 Firebase 상태를 확인하고 문제를 추적할 수 있도록 하였으며, 기능별로 코드를 분리하여 관리할 수 있도록 구성했습니다.

## 저장소 구성

```text
Scripts/
├── Network/
├── Data/
├── Game/
├── Editor/
└── ...
```

※ 실제 저장소의 폴더 구성은 GitHub에서 확인할 수 있습니다.

## 관련 기술문서

프로젝트의 전체적인 구조와 구현 과정은 별도의 기술문서에서 설명합니다.

주요 내용:

- Firebase RTDB 기반 멀티플레이 구조
- `stateAndTier` 복합키 설계
- `UpdateChildrenAsync()`를 활용한 Atomic Write
- Race Condition 해결
- 실시간 데이터 동기화
- 사망 및 랭크 정산
- 이벤트 파이프라인
- 타팀 연동 API
- Firebase Debug Tool

## 공개 저장소 주의사항

본 저장소에는 기술문서 참고를 위한 코드만 포함하며, 프로젝트 운영에 필요한 비공개 정보나 민감한 인증 정보는 포함하지 않습니다.
