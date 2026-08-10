using UnityEngine;
using Wooduduk.Data.Result;

namespace Wooduduk.Network.Firebase.Leaderboard
{
    /// <summary>
    /// 인메모리 캐시: 같은 데이터를 매번 서버에서 안 읽도록
    /// </summary>
    public static class LeaderboardCache
    {
        private static LeaderboardViewData _cachedData;
        private static string _cachedUserId;
        private static float _lastLoadTime;

        private const float CACHE_DURATION = 60f;

        public static bool TryGet(string userId, out LeaderboardViewData data)
        {
            data = _cachedData;

            if (_cachedData == null) return false;
            if (_cachedUserId != userId) return false;
            if (Time.time - _lastLoadTime > CACHE_DURATION) return false;

            return true;
        }

        public static void Save(string userId, LeaderboardViewData data)
        {
            _cachedData = data;
            _cachedUserId = userId;
            _lastLoadTime = Time.time;
        }

        public static void Clear()
        {
            _cachedData = null;
            _cachedUserId = null;
        }
    }
}