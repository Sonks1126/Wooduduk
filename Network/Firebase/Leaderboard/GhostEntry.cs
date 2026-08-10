namespace Wooduduk.Network.Firebase.Leaderboard
{
    [System.Serializable]
    public class GhostEntry
    {
        public string _userId;
        public string _nick;

        public int _score;

        public int _maxCombo;
        public int _settlementCount;

        public int _tier;        // AxeTier 대신 int (단순 비교용)
        public int _gamesPlayed; // 나중 확장용
    }
}