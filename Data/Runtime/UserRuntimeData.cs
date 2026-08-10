using System.Collections.Generic;

namespace Wooduduk.Data.Runtime
{
    [System.Serializable]
    public class ActiveMakgeolliEffect
    {
        public string _effectId;
        public int _remainingSpins;
    }

    public class UserRuntimeData
    {
        public string _userId;
        public string _userNick;

        // public bool _isGameStarted;     // 게임 시작 여부

        // 생존 (추위 체력)
        public float _maxTemp;
        public float _currentTemp;
        public bool _isDead;

        // 이번 런 정보(한 게임 기본 정보)
        public int _currentWood;        // 현재 보유 장작
        public int _currentScore;       // 게임 중 점수
        public float _survivalTime;         // 생존 시간

        // 진행 상태
        public int _settlementCount;        // 정산 횟수 (비용 계산용)
        public int _comboCount;             // 현재 콤보
        public int _maxCombo;               // 이번 게임 최고 콤보
        public int _currentSpinCount;       // 이번 정산 내 스핀 수

        // 실패 방어 잔여 횟수 (C003 보험 증서)
        public int _failShieldCount;

        // 장착 카드 ID 목록 (영구 효과)
        public List<string> _activeCardIds = new List<string>();

        // 활성 막걸리 효과 (스핀 카운트 기반 만료)
        public List<ActiveMakgeolliEffect> _activeMakgeolliEffects = new List<ActiveMakgeolliEffect>();

        // 장착 스킨 (표시용)
        public string _equippedWeaponId;
        public string _equippedRouletteSkinId;
    }
}
