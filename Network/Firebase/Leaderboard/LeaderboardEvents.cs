using System;
using Wooduduk.Data.Result;

namespace Wooduduk.Network.Firebase.Leaderboard
{
    public static class LeaderboardEvents
    {
        public static event Action<LeaderboardViewData> OnLoaded;

        public static void RaiseLoaded(LeaderboardViewData data)
        {
            OnLoaded?.Invoke(data);
        }
    }
}