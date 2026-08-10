# 우드득 (Woodduk) — Unity Game Client Code

Unity 기반 멀티플레이 게임 프로젝트 **「우드득」**의 기술문서 참고용 코드 저장소입니다.

본 저장소는 전체 Unity 프로젝트가 아니라, 기술문서에서 설명한 **직접 구현한 C# 스크립트 중심**으로 구성했습니다.

## 🔗 GitHub

- Repository: https://github.com/Sonks1126/Scripts

## 🎮 프로젝트 개요

우드득은 별도의 게임 서버 없이 **Firebase Realtime Database(RTDB)**를 활용해 멀티플레이 기능을 구현한 프로젝트입니다.

기술문서에서는 다음 시스템을 중심으로 설명합니다.

- Firebase RTDB 기반 매칭
- 실시간 플레이어 상태 동기화
- 사망 및 랭크 정산
- 데이터 관리
- Unity Editor 기반 디버그 툴

## 👨‍💻 주요 구현

### 1. 매칭

`FirebaseRoomMatchSource`와 `MatchmakingFlow`를 통해 Firebase RTDB 기반 매칭을 구성했습니다.

Firebase RTDB의 단일 필드 쿼리 제약을 해결하기 위해 `stateAndTier` 복합키를 사용했습니다.

```text
stateAndTier = "waiting_3"
```

### 2. 원자적 방 생성

방 생성 과정에서 방 정보와 플레이어 정보가 순차적으로 저장될 때 발생할 수 있는 Race Condition을 해결하기 위해 `UpdateChildrenAsync()`를 활용한 원자적 기록을 적용했습니다.

### 3. 실시간 동기화

`RoomLiveSync`를 통해 Firebase의 `live/{uid}` 데이터를 활용하고, Firebase `ValueChanged` 이벤트를 이용해 다른 플레이어의 상태 변화를 수신합니다.

### 4. 사망 / 랭크 정산

`GameController.HandleDeath()`를 중심으로 사망 데이터를 기록하고, `ServerValue.Timestamp`와 멱등성 가드를 활용하여 결과 정산을 처리합니다.

### 5. 디버그 툴

`SaveManagerDebugWindow` 등 Unity Editor 전용 도구를 통해 Firebase 데이터와 저장 상태를 확인하고 개발 중 문제를 추적할 수 있도록 구성했습니다.

## 📂 저장소 구성

```text
Scripts/
├── Firebase/
├── Network/
├── Data/
├── Matchmaking/
├── Game/
└── ...
```

※ 실제 폴더 구성은 저장소에서 확인할 수 있습니다.

## 📚 관련 기술문서

프로젝트의 전체적인 구조와 문제 해결 과정은 별도의 기술문서에서 설명합니다.

주요 내용:

- Firebase RTDB 구조
- `stateAndTier` 복합키 설계
- Atomic Write를 통한 Race Condition 해결
- 실시간 데이터 동기화
- 사망 / 랭크 정산
- 이벤트 파이프라인
- 타팀 연동 API
- Firebase Debug Tool

## 🔐 공개 저장소 주의사항

본 저장소에는 기술문서 참고를 위한 코드만 포함하며, Firebase API Key, 서비스 계정 파일, 비밀번호 등 민감한 인증 정보는 포함하지 않습니다.
