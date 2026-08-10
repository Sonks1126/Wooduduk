#if false // [PIVOT-DISABLED 2026-06-22 한충희] 구 추출게임 코드 - 슬롯 피벗으로 컴파일 제외. 복구: 이 줄과 맨끝 #endif 삭제.
using FishNet.Connection;
using FishNet.Object;
using FishNet.Object.Synchronizing;
using UnityEngine;
using Wooduduk.Network;

namespace Wooduduk.Network.NetworkData
{
    public partial class NetworkPlayer : NetworkBehaviour
    {
        // 서버에서 매치 배정 시 세팅, 클라에 자동 동기화
        private readonly SyncVar<string> _matchId = new();
        public string MatchId => _matchId.Value;

        // DebugRuntimeUI(인게임)용 - 내 매치 상태만
        public DebugStatusData LastDebugStatus;
        // ServerMonitorWindow(에디터)용 - 전체 매치 상태
        public DebugFullStatus LastFullStatus;

        // GameServerManager.RegisterPlayer에서 호출
        public void SetMatchId(string matchId)
        {
            _matchId.Value = matchId;
        }

        // 클라 → 서버: 디버그 커맨드 전달 (DebugRuntimeUI, ServerMonitorWindow 공용)
        [ServerRpc]
        public void SendDebugCommandServerRpc(ServerDebugCommandType type, string matchId, string userId = null, int intParam = 0)
        {
            GameServerManager.Instance.ExecuteDebugCommand(type, matchId, userId, intParam);
        }

        // 클라 → 서버: 내 매치 상태 요청 (DebugRuntimeUI에서 0.5초마다 호출)
        [ServerRpc]
        public void RequestDebugStatusServerRpc()
        {
            var server = GameServerManager.Instance;
            if (server == null) return;

            var session = server.MatchRegistry?.GetMatch(_matchId.Value);
            if (session == null) return;

            int playerCount = 0;
            int botCount = 0;
            float myHp = 0f;
            float myMaxHp = 0f;

            foreach (var p in session.Players.Values)
            {
                if (p._isBot) botCount++;
                else
                {
                    playerCount++;
                    if (p._userId == _userId.Value)
                    {
                        myHp = p._currentHp;
                        myMaxHp = p._currentMaxHp;
                    }
                }
            }

            bool myInvincible = false;
            int myWood = 0;
            Vector3 myPosition = Vector3.zero;
            if (session.Players.TryGetValue(_userId.Value, out var myPlayer))
            {
                myInvincible = myPlayer._isInvincible;
                myWood = myPlayer._collectedWood;
                myPosition = myPlayer._position;
            }

            var status = new DebugStatusData
            {
                PlayerCount = playerCount,
                BotCount = botCount,
                MyHp = myHp,
                MyMaxHp = myMaxHp,
                TreeCount = session.Trees.Count,
                TickMs = server.LastTickMs,
                ExtractionOpened = session.State._extractionOpened,
                IsInvincible = myInvincible,
                MyWood = myWood,
                MyPosition = myPosition,
                ElapsedTime = session.State._elapsedTime,
                SessionTime = session.Config._sessionTime,
                ExtractionOpenTime = session.Config._extractionOpenTime
            };

            TargetReceiveDebugStatus(Owner, status);
        }

        // 서버 → 클라: 내 매치 상태 수신
        [TargetRpc]
        private void TargetReceiveDebugStatus(NetworkConnection conn, DebugStatusData status)
        {
            LastDebugStatus = status;
        }

        // 클라 → 서버: 전체 매치 상태 요청 (ServerMonitorWindow에서 0.5초마다 호출)
        [ServerRpc]
        public void RequestFullDebugStatusServerRpc()
        {
            var server = GameServerManager.Instance;
            if (server == null) return;

            var allMatches = server.MatchRegistry?.AllMatches;
            if (allMatches == null) return;

            var matchList = new DebugMatchInfo[allMatches.Count];
            int mi = 0;

            foreach (var session in allMatches.Values)
            {
                var players = new DebugPlayerInfo[session.Players.Count];
                int pi = 0;

                foreach (var p in session.Players.Values)
                {
                    players[pi++] = new DebugPlayerInfo
                    {
                        UserId = p._userId,
                        Nick = p._userNick ?? "",
                        Hp = p._currentHp,
                        MaxHp = p._currentMaxHp,
                        IsDead = p._isDead,
                        IsInvincible = p._isInvincible,
                        IsBot = p._isBot,
                        Wood = p._collectedWood,
                        Position = p._position,
                        KillCount = p._killCount,
                        WeaponId = p._equippedWeaponId ?? "",
                        TraitId = p._equippedTraitId ?? "",
                        MoveSpeed = p._currentMoveSpeed,
                        AttackDamage = p._currentAttackDamage,
                        AttackRange = p._currentAttackRange,
                        AttackInterval = p._currentAttackInterval
                    };
                }

                matchList[mi++] = new DebugMatchInfo
                {
                    MatchId = session.State._matchId,
                    ElapsedTime = session.State._elapsedTime,
                    AliveCount = session.State._alivePlayerCount,
                    ExtractionOpened = session.State._extractionOpened,
                    IsEnded = session.State._isEnded,
                    ZoneRadius = session.FireZone._currentRadius,
                    TreeCount = session.Trees.Count,
                    MaxPlayers = session.MaxPlayers,
                    CurrentPlayers = session.CurrentPlayers,
                    Players = players
                };
            }

            var full = new DebugFullStatus
            {
                Matches = matchList,
                TickMs = server.LastTickMs
            };

            TargetReceiveFullDebugStatus(Owner, full);
        }

        // 서버 → 클라: 전체 매치 상태 수신
        [TargetRpc]
        private void TargetReceiveFullDebugStatus(NetworkConnection conn, DebugFullStatus status)
        {
            LastFullStatus = status;
        }
    }

    // DebugRuntimeUI용 - 내 매치의 요약 상태
    public struct DebugStatusData
    {
        public int PlayerCount;
        public int BotCount;
        public float MyHp;
        public float MyMaxHp;
        public int TreeCount;
        public float TickMs;
        public bool ExtractionOpened;
        public bool IsInvincible;
        public int MyWood;
        public Vector3 MyPosition;
        public float ElapsedTime;        // 매치 경과 시간(초)
        public float SessionTime;        // 매치 전체 시간(초)
        public float ExtractionOpenTime; // 탈출 개방 시각(초)
    }

    // ServerMonitorWindow용 - 플레이어 1명의 상세 정보
    public struct DebugPlayerInfo
    {
        public string UserId;
        public string Nick;
        public float Hp;
        public float MaxHp;
        public bool IsDead;
        public bool IsInvincible;
        public bool IsBot;
        public int Wood;
        public Vector3 Position;
        public int KillCount;
        public string WeaponId;
        public string TraitId;
        public float MoveSpeed;
        public float AttackDamage;
        public float AttackRange;
        public float AttackInterval;
    }

    // ServerMonitorWindow용 - 매치 1개의 전체 상태
    public struct DebugMatchInfo
    {
        public string MatchId;
        public float ElapsedTime;
        public int AliveCount;
        public bool ExtractionOpened;
        public bool IsEnded;
        public float ZoneRadius;
        public int TreeCount;
        public int MaxPlayers;
        public int CurrentPlayers;
        public DebugPlayerInfo[] Players;
    }

    // ServerMonitorWindow용 - 서버 전체 상태 (모든 매치 포함)
    public struct DebugFullStatus
    {
        public DebugMatchInfo[] Matches;
        public float TickMs;
    }
}
#endif
