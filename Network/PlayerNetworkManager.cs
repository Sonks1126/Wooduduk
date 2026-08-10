#if false // [PIVOT-DISABLED 2026-06-22 한충희] 구 추출게임 코드 - 슬롯 피벗으로 컴파일 제외. 복구: 이 줄과 맨끝 #endif 삭제.
using System.Collections.Generic;
using Wooduduk.Network.NetworkData;

namespace Wooduduk.Network
{
    /// <summary>
    /// 서버에 존재하는 NetworkPlayer 관리
    /// </summary>
    public class PlayerNetworkManager
    {
        /// <summary>
        /// connection Id → NetworkPlayer
        /// </summary>
        private readonly Dictionary<int, NetworkPlayer> _players = new();
        /// <summary>
        /// Firebase UID → NetworkPlayer
        /// </summary>
        private readonly Dictionary<string, NetworkPlayer> _userPlayers = new();

        /// <summary>
        /// 플레이어 등록
        /// </summary>
        public void Register(int connectionId, string uid, NetworkPlayer player)
        {
            _players[connectionId] = player;
            _userPlayers[uid] = player;
        }

        /// <summary>
        /// 플레이어 제거
        /// </summary>
        public void Unregister(int connectionId, string uid)
        {
            _players.Remove(connectionId);

            if (!string.IsNullOrEmpty(uid))
                _userPlayers.Remove(uid);
        }

        /// <summary>
        /// ClientId로 조회
        /// </summary>
        public NetworkPlayer Get(int connectionId)
        {
            _players.TryGetValue(connectionId, out var player);
            return player;
        }

        /// <summary>
        /// UID로 조회
        /// </summary>
        public NetworkPlayer GetByUserId(string uid)
        {
            _userPlayers.TryGetValue(uid, out var player);
            return player;
        }
    }
}
#endif
