using System.Collections.Generic;

namespace Wooduduk.Data.Static
{
    [System.Serializable]
    public class UserData
    {
        public string _userId;
        public string _userNick;
        public int _gold;   // 보유 재화
        public int _token;

        // 접속 시각 기록 (Unix ms, UTC).
        // Firebase Auth의 lastSignInTime은 캐시된 익명 세션에선 갱신 안 되므로 직접 기록.
        public long _createdAt;    // 최초 생성(가입) 시각
        public long _lastLoginAt;  // 최근 로그인 시각

        // 튜토리얼
        public bool _tutorialDone;  // 튜토리얼 완료 여부

        // 기록
        public int[] _bestScores = new int[3]; // Top 1, 2, 3
        public int _latestScore; // 최근 점수 기록
        public int _latestMaxCombo; // 최근 콤보 기록
        public int _latestSettlementCount; // 최근 정산 기록
        public int _gamesPlayed; // 플레이 횟수

        // 티어 시스템
        public int _axeTier; // 도끼 티어 (enum 예정 저장을 위해 int)
        public int _tierLp; // 티어 랭크포인트
        public int _tierProtection; // 강등 보호 횟수

        // 무기 (메타 progression)
        public List<string> _ownedWeaponIds;      // 보유(제작) 무기
        public string _equippedWeaponId;          // 들고 들어갈 무기

        // 룰렛
        public List<string> _rouletteSkinIds;   // 보유(제작) 스킨
        public string _equippedRouletteSkinId;  // 들고 들어갈 스킨
    }
}