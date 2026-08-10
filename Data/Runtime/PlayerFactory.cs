using Wooduduk.Data.DataSO;
using Wooduduk.Data.Static;

namespace Wooduduk.Data.Runtime
{
    public static class PlayerFactory
    {
        public static UserRuntimeData Create(UserData userData, GameBalanceDataSO balance)
        {
            var b = balance._data;

            return new UserRuntimeData
            {
                _userId = userData._userId,
                _userNick = userData._userNick,

                // 생존
                _maxTemp = b._survival._maxBodyTemp,
                _currentTemp = b._survival._maxBodyTemp,
                _isDead = false,

                // 런 데이터
                _currentWood = 0,
                _currentScore = 0,
                _survivalTime = 0f,

                // 진행 상태
                _settlementCount = 0,
                _comboCount = 0,
                _maxCombo = 0,
                _currentSpinCount = 0,

                // 특수 상태
                _failShieldCount = 0,

                // 장비
                _equippedWeaponId = userData._equippedWeaponId,
                _equippedRouletteSkinId = userData._equippedRouletteSkinId,
            };
        }
    }
}
