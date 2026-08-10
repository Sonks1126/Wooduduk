using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;

namespace Wooduduk.Network.Firebase.Matchmaking
{
    /// <summary>
    /// Room 데이터를 관리합니다.
    /// Room 생성, 참가, 상태 변경, 실시간 데이터 동기화를 담당합니다.
    /// RTDB 입출력만 담당합니다.
    /// </summary>
    public class RoomRepository
    {
        /// <summary>Firebase Root Reference</summary>
        private readonly DatabaseReference _database;

        /// <summary>구독 해제용 핸들러 보관 (key: subscribeKey, value: handler)</summary>
        private readonly Dictionary<string, EventHandler<ValueChangedEventArgs>> _handlers = new();

        public RoomRepository()
        {
            _database = FirebaseDatabase.DefaultInstance.RootReference;
        }

        #region ===== 방 =====

        /// <summary>
        /// Firebase Push key로 unique한 roomId를 생성합니다.
        /// 시간순 정렬되는 unique key입니다.
        /// </summary>
        public string GenerateRoomId()
        {
            return _database.Child("rooms").Push().Key;
        }

        /// <summary>
        /// 방 생성과 첫 플레이어 등록을 원자적으로 처리합니다.
        /// 빈 방이 노출되는 타이밍을 제거하여 레이스 컨디션을 방지합니다.
        /// </summary>
        public void CreateRoomWithPlayer(string roomId, RoomData room, RoomPlayerData firstPlayer, Action<bool> callback)
        {
            // ★ 문자열이 아니라 Dictionary로 저장 (객체 구조 유지)
            var playerDict = new Dictionary<string, object>
            {
                ["uid"] = firstPlayer.uid,
                ["nick"] = firstPlayer.nick,
                ["tier"] = firstPlayer.tier,
                ["ready"] = firstPlayer.ready,
                ["isGhost"] = firstPlayer.isGhost
            };

            var updates = new Dictionary<string, object>
            {
                [$"rooms/{roomId}/state"] = room.state,
                [$"rooms/{roomId}/stateAndTier"] = room.stateAndTier,
                [$"rooms/{roomId}/hostUid"] = room.hostUid,
                [$"rooms/{roomId}/players/{firstPlayer.uid}"] = playerDict,  // ★ Dictionary
                [$"activeRooms/{firstPlayer.uid}"] = roomId
            };

            // onDisconnect 설정
            var playerRef = _database.Child("rooms").Child(roomId).Child("players").Child(firstPlayer.uid);
            var liveRef = _database.Child("rooms").Child(roomId).Child("live").Child(firstPlayer.uid);
            var activeRef = _database.Child("activeRooms").Child(firstPlayer.uid);

            playerRef.OnDisconnect().RemoveValue();
            liveRef.OnDisconnect().RemoveValue();
            activeRef.OnDisconnect().RemoveValue();

            _database.UpdateChildrenAsync(updates)
                .ContinueWithOnMainThread(task =>
                {
                    bool success = !(task.IsCanceled || task.IsFaulted);
                    if (!success)
                        Debug.LogError($"[RoomRepository] CreateRoomWithPlayer 실패: {task.Exception?.Message}");
                    else
                        Debug.Log($"[RoomRepository] 원자적 방 생성 완료: {roomId}");

                    callback?.Invoke(success);
                });
        }

        /// <summary>새로운 Room을 생성합니다.</summary>
        public void CreateRoom(string roomId, RoomData room, Action<bool> callback)
        {
            string json = JsonUtility.ToJson(room);

            _database
                .Child("rooms")
                .Child(roomId)
                .SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(task =>
                {
                    bool success = !(task.IsCanceled || task.IsFaulted);
                    if (!success)
                        Debug.LogError($"[RoomRepository] CreateRoom 실패: {roomId}");

                    callback?.Invoke(success);
                });
        }

        /// <summary>Room을 삭제합니다.</summary>
        public void DeleteRoom(string roomId)
        {
            _database
                .Child("rooms")
                .Child(roomId)
                .RemoveValueAsync()
                .ContinueWithOnMainThread(LogIfFailed("DeleteRoom"));
        }

        /// <summary>
        /// 대기 중(waiting)인 Room을 찾습니다.
        /// 없으면 null을 반환합니다.
        /// TODO: 동시 생성 race condition은 MVP에서 무시 (실패 시 그냥 대기방 추가됨)
        /// </summary>
        public void FindWaitingRoom(Action<string> callback)
        {
            _database
                .Child("rooms")
                .OrderByChild("state")
                .EqualTo("waiting")
                .LimitToFirst(1)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        Debug.LogError("[RoomRepository] FindWaitingRoom 실패");
                        callback?.Invoke(null);
                        return;
                    }

                    foreach (var child in task.Result.Children)
                    {
                        callback?.Invoke(child.Key);
                        return;
                    }

                    callback?.Invoke(null);
                });
        }

        /// <summary>Room 전체를 실시간 구독합니다.</summary>
        public void SubscribeRoom(string roomId, Action<DataSnapshot> callback)
        {
            string key = $"room_{roomId}";
            UnsubscribeByKey(key, _database.Child("rooms").Child(roomId));

            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                callback?.Invoke(args.Snapshot);
            };

            _database.Child("rooms").Child(roomId).ValueChanged += handler;
            _handlers[key] = handler;
        }

        public void UnsubscribeRoom(string roomId)
        {
            UnsubscribeByKey($"room_{roomId}", _database.Child("rooms").Child(roomId));
        }

        /// <summary>Room 상태를 구독합니다.</summary>
        public void SubscribeState(string roomId, Action<string> callback)
        {
            string key = $"state_{roomId}";
            var reference = _database.Child("rooms").Child(roomId).Child("state");
            UnsubscribeByKey(key, reference);

            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                if (!args.Snapshot.Exists) return;
                callback?.Invoke(args.Snapshot.Value.ToString());
            };

            reference.ValueChanged += handler;
            _handlers[key] = handler;
        }

        public void UnsubscribeState(string roomId)
        {
            UnsubscribeByKey($"state_{roomId}",
                _database.Child("rooms").Child(roomId).Child("state"));
        }

        // ===== 동기 시작 (startAt) — P1: 서버시간 동기 카운트다운 =====
        // 추가만(additive), 기존 방/라이브/사망 로직 무변경. rooms/{roomId}/startAt = 게임 시작 절대 서버시각(ms).

        // 호스트가 "게임 시작 절대 서버시각(ms)"을 기록 → 전원이 이 값에 맞춰 동시 시작.
        public void WriteStartAt(string roomId, long startAtMs)
        {
            _database.Child("rooms").Child(roomId).Child("startAt")
                .SetValueAsync(startAtMs)
                .ContinueWithOnMainThread(LogIfFailed("WriteStartAt"));
        }

        // rooms/{roomId}/startAt 구독. long 수신 시 콜백. (SubscribeState 패턴 미러)
        public void SubscribeStartAt(string roomId, Action<long> callback)
        {
            string key = $"startAt_{roomId}";
            var reference = _database.Child("rooms").Child(roomId).Child("startAt");
            UnsubscribeByKey(key, reference);

            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                if (!args.Snapshot.Exists) return;
                if (long.TryParse(args.Snapshot.Value.ToString(), out long startAtMs))
                    callback?.Invoke(startAtMs);
            };

            reference.ValueChanged += handler;
            _handlers[key] = handler;
        }

        public void UnsubscribeStartAt(string roomId)
        {
            UnsubscribeByKey($"startAt_{roomId}",
                _database.Child("rooms").Child(roomId).Child("startAt"));
        }

        // Firebase 서버-클라 시계 오프셋(ms). 절대 서버시각 = 로컬now + offset. (.info/serverTimeOffset)
        public void ReadServerTimeOffset(Action<double> callback)
        {
            FirebaseDatabase.DefaultInstance.GetReference(".info/serverTimeOffset")
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    double offset = 0;
                    if (!task.IsFaulted && !task.IsCanceled && task.Result.Exists)
                        double.TryParse(task.Result.Value.ToString(), out offset);
                    callback?.Invoke(offset);
                });
        }

        /// <summary>Room 정보를 한 번 조회합니다.</summary>
        public void GetRoom(string roomId, Action<RoomData> callback)
        {
            _database
                .Child("rooms")
                .Child(roomId)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                    {
                        callback?.Invoke(null);
                        return;
                    }

                    RoomData room =
                        JsonUtility.FromJson<RoomData>(task.Result.GetRawJsonValue());

                    callback?.Invoke(room);
                });
        }

        // 티어 기반 대기방 탐색.
        // 정확히 내 티어 → 아래 티어 → 위 티어 순으로 찾고
        // 셋 다 없으면 null 반환 (새 방 만들라는 신호).
        public void FindWaitingRoomByTier(int tier, Action<string> callback)
        {
            TryFindTier(tier, roomId =>
            {
                if (roomId != null) { callback(roomId); return; }

                TryFindTier(tier - 1, roomId2 =>
                {
                    if (roomId2 != null) { callback(roomId2); return; }

                    TryFindTier(tier + 1, callback);
                });
            });
        }

        // stateAndTier = "waiting_{tier}" 인 방 1개 찾기.
        // tier 범위 밖(0 미만, 6 초과)이면 바로 null.
        // 방에 실제 플레이어가 1명 이상 있는지 확인.
        // 강제 종료 후 players/{uid}만 onDisconnect로 삭제되고 방 자체는 남은 경우를 걸러내기 위해 사용.
        public void HasActivePlayers(string roomId, Action<bool> callback)
        {
            _database.Child("rooms").Child(roomId).Child("players")
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                    {
                        callback?.Invoke(false);
                        return;
                    }
                    callback?.Invoke(task.Result.ChildrenCount > 0);
                });
        }

        private void TryFindTier(int tier, Action<string> callback)
        {
            if (tier < 0 || tier > 6) { callback(null); return; }

            _database
                .Child("rooms")
                .OrderByChild("stateAndTier")
                .EqualTo($"waiting_{tier}")
                .LimitToFirst(5)
                .GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled) { callback(null); return; }

                    string candidateWithPlayers = null;
                    string candidateEmpty = null;

                    foreach (var child in task.Result.Children)
                    {
                        if (child.Child("players").ChildrenCount > 0)
                        {
                            candidateWithPlayers = child.Key;
                            break;
                        }
                        else if (candidateEmpty == null)
                        {
                            // ★ 삭제하지 말고 그냥 보관 (나중에 OnFindWaitingRoom 에서 처리)
                            candidateEmpty = child.Key;
                        }
                    }

                    // 플레이어 있는 방 우선, 없으면 빈 방이라도 반환
                    callback(candidateWithPlayers ?? candidateEmpty);
                });
        }

        // 기존 SetRoomState에 tier 파라미터 추가.
        // tier >= 0 이면 stateAndTier도 같이 업데이트.
        // "playing_3" 으로 바뀌면 대기방 목록에서 자동으로 제외됨.
        public void SetRoomState(string roomId, string state, int tier = -1)
        {
            _database
                .Child("rooms").Child(roomId).Child("state")
                .SetValueAsync(state)
                .ContinueWithOnMainThread(LogIfFailed("SetRoomState"));

            if (tier >= 0)
            {
                _database
                    .Child("rooms").Child(roomId).Child("stateAndTier")
                    .SetValueAsync($"{state}_{tier}")
                    .ContinueWithOnMainThread(LogIfFailed("SetRoomStateAndTier"));
            }
        }

        // 내가 현재 점유 중인 방 ID를 Firebase에 기록.
        // 앱이 비정상 종료되면 onDisconnect가 이 기록을 자동 삭제.
        public void TrackMyRoom(string uid, string roomId)
        {
            var r = _database.Child("activeRooms").Child(uid);
            r.SetValueAsync(roomId);
            r.OnDisconnect().RemoveValue(); // 연결 끊기면 추적 기록도 자동 제거
        }

        // 매칭 시작 전 호출. 이전 세션에서 남은 방이 있으면 삭제 완료 후 onDone 실행.
        // 1차: activeRooms/{uid}에 저장된 방 ID로 삭제.
        // 2차(fallback): 강제 종료로 activeRooms 저장 자체가 안 됐을 때,
        //               rooms/ 전체를 uid로 검색해서 내가 들어가 있는 방을 삭제.
        public void CleanupMyPreviousRoom(string uid, Action onDone)
        {
            _database.Child("activeRooms").Child(uid).GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                    {
                        // activeRooms 기록 없음 → rooms/ 전체 검색으로 fallback
                        FindAndDeleteRoomsByPlayer(uid, onDone);
                        return;
                    }

                    string oldRoomId = task.Result.Value?.ToString();
                    if (string.IsNullOrEmpty(oldRoomId))
                    {
                        FindAndDeleteRoomsByPlayer(uid, onDone);
                        return;
                    }

                    Debug.Log($"[RoomRepository] 잔존 방 삭제 (activeRooms): {oldRoomId}");
                    _database.Child("activeRooms").Child(uid).RemoveValueAsync();
                    // 방 삭제 완료 후 onDone — 완료 전에 FindWaitingRoom이 먼저 실행되는 걸 방지
                    _database.Child("rooms").Child(oldRoomId).RemoveValueAsync()
                        .ContinueWithOnMainThread(_ => onDone?.Invoke());
                });
        }

        // activeRooms 추적이 없을 때 rooms/ 전체에서 내 uid가 포함된 방을 찾아 삭제.
        // 강제 종료로 추적 기록이 저장 안 됐을 때의 안전망.
        private void FindAndDeleteRoomsByPlayer(string uid, Action onDone)
        {
            _database.Child("rooms").GetValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled || !task.Result.Exists)
                    {
                        onDone?.Invoke();
                        return;
                    }

                    var toDelete = new List<string>();
                    foreach (var room in task.Result.Children)
                    {
                        if (room.Child("players").HasChild(uid))
                            toDelete.Add(room.Key);
                    }

                    if (toDelete.Count == 0)
                    {
                        onDone?.Invoke();
                        return;
                    }

                    Debug.Log($"[RoomRepository] 잔존 방 {toDelete.Count}개 발견 (fallback 검색) → 삭제");
                    int remaining = toDelete.Count;
                    foreach (var roomId in toDelete)
                    {
                        _database.Child("rooms").Child(roomId).RemoveValueAsync()
                            .ContinueWithOnMainThread(_ =>
                            {
                                remaining--;
                                if (remaining == 0) onDone?.Invoke();
                            });
                    }
                });
        }

        #endregion

        #region ===== Player =====

        /// <summary>
        /// Room에 플레이어를 등록합니다.
        /// onDisconnect를 등록해서 연결이 끊기면 자동 삭제됩니다.
        /// </summary>
        public void JoinRoom(string roomId, RoomPlayerData player)
        {
            string json = JsonUtility.ToJson(player);

            var playerRef = _database
                .Child("rooms").Child(roomId)
                .Child("players").Child(player.uid);

            var liveRef = _database
                .Child("rooms").Child(roomId)
                .Child("live").Child(player.uid);

            playerRef.SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(LogIfFailed("JoinRoom"));

            // ★ 앱 종료/네트워크 끊김 시 자동 삭제 — 고스트가 아닐 때만
            if (!player.isGhost)
            {
                playerRef.OnDisconnect().RemoveValue();
                liveRef.OnDisconnect().RemoveValue();
            }
        }

        /// <summary>Room에서 플레이어를 제거합니다.</summary>
        public void LeaveRoom(string roomId, string uid)
        {
            _database.Child("rooms").Child(roomId)
                .Child("players").Child(uid)
                .RemoveValueAsync()
                .ContinueWithOnMainThread(LogIfFailed("LeaveRoom-players"));

            _database.Child("rooms").Child(roomId)
                .Child("live").Child(uid)
                .RemoveValueAsync()
                .ContinueWithOnMainThread(LogIfFailed("LeaveRoom-live"));
        }

        /// <summary>플레이어 목록을 실시간 구독합니다.</summary>
        public void SubscribePlayers(string roomId, Action<Dictionary<string, RoomPlayerData>> callback, Action onRoomDeleted = null)
        {
            string key = $"players_{roomId}";
            var reference = _database.Child("rooms").Child(roomId).Child("players");
            UnsubscribeByKey(key, reference);

            bool hadPlayers = false; // ★ 처음 빈 상태는 삭제가 아님

            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                if (!args.Snapshot.Exists)
                {
                    if (hadPlayers) onRoomDeleted?.Invoke(); // ★ 실제로 사라진 경우만
                    return;
                }

                hadPlayers = true;
                var result = new Dictionary<string, RoomPlayerData>();
                foreach (var child in args.Snapshot.Children)
                {
                    var player = JsonUtility.FromJson<RoomPlayerData>(child.GetRawJsonValue());
                    result.Add(child.Key, player);
                }
                callback?.Invoke(result);
            };

            reference.ValueChanged += handler;
            _handlers[key] = handler;
        }

        public void UnsubscribePlayers(string roomId)
        {
            UnsubscribeByKey($"players_{roomId}",
                _database.Child("rooms").Child(roomId).Child("players"));
        }

        #endregion

        #region ===== 실시간 =====

        /// <summary>자신의 실시간 플레이 데이터를 업데이트합니다.</summary>
        public void UpdateLive(string roomId, string uid, RoomLiveData live)
        {
            string json = JsonUtility.ToJson(live);

            _database
                .Child("rooms").Child(roomId)
                .Child("live").Child(uid)
                .SetRawJsonValueAsync(json);
            // 빈번한 호출이므로 콜백/에러 로그 생략
        }

        /// <summary>Room의 실시간 데이터를 구독합니다.</summary>
        public void SubscribeLive(string roomId, Action<Dictionary<string, RoomLiveData>> callback)
        {
            string key = $"live_{roomId}";
            var reference = _database.Child("rooms").Child(roomId).Child("live");
            UnsubscribeByKey(key, reference);

            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                var result = new Dictionary<string, RoomLiveData>();

                if (args.Snapshot.Exists)
                {
                    foreach (var child in args.Snapshot.Children)
                    {
                        var live = JsonUtility.FromJson<RoomLiveData>(child.GetRawJsonValue());
                        result.Add(child.Key, live);
                    }
                }

                callback?.Invoke(result);
            };

            reference.ValueChanged += handler;
            _handlers[key] = handler;
        }

        public void UnsubscribeLive(string roomId)
        {
            UnsubscribeByKey($"live_{roomId}",
                _database.Child("rooms").Child(roomId).Child("live"));
        }

        #endregion

        #region ===== 이벤트 =====

        /// <summary>
        /// 이벤트를 Room에 추가합니다.
        /// (예: Perfect, Destiny, Card, Makgeolli)
        /// </summary>
        public void PushEvent<T>(string roomId, string uid, T data) where T : class
        {
            string json = JsonUtility.ToJson(data);

            _database
                .Child("rooms").Child(roomId)
                .Child("events").Child(uid)
                .Push()
                .SetRawJsonValueAsync(json);
        }

        #endregion

        #region ===== 결과값 =====

        /// <summary>게임 종료 결과를 제출합니다.</summary>
        public void SubmitResult<T>(string roomId, string uid, T result) where T : class
        {
            string json = JsonUtility.ToJson(result);

            _database
                .Child("rooms").Child(roomId)
                .Child("result").Child(uid)
                .SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(LogIfFailed("SubmitResult"));
        }

        #endregion

        #region ===== 사망/순위 =====

        /// <summary>사망 기록을 Firebase에 씁니다. 타임스탬프는 서버 시각 기준.</summary>
        public void WriteDeath(string roomId, string uid, long score)
        {
            var data = new Dictionary<string, object>
            {
                { "score", (long)score },
                { "time", ServerValue.Timestamp }
            };
            _database.Child("rooms").Child(roomId).Child("deaths").Child(uid)
                .UpdateChildrenAsync(data)
                .ContinueWithOnMainThread(LogIfFailed("WriteDeath"));
        }

        /// <summary>deaths 노드 전체를 실시간 구독합니다.</summary>
        public void SubscribeDeaths(string roomId, Action<DataSnapshot> callback)
        {
            string key = $"deaths_{roomId}";
            var reference = _database.Child("rooms").Child(roomId).Child("deaths");
            UnsubscribeByKey(key, reference);

            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                callback?.Invoke(args.Snapshot);
            };

            reference.ValueChanged += handler;
            _handlers[key] = handler;
        }

        public void UnsubscribeDeaths(string roomId)
        {
            UnsubscribeByKey($"deaths_{roomId}",
                _database.Child("rooms").Child(roomId).Child("deaths"));
        }

        /// <summary>호스트가 최종 등수 맵을 Firebase에 씁니다.</summary>
        public void WriteRanks(string roomId, Dictionary<string, int> ranks)
        {
            var data = new Dictionary<string, object>();
            foreach (var kvp in ranks)
                data[kvp.Key] = (long)kvp.Value;
            _database.Child("rooms").Child(roomId).Child("ranks")
                .SetValueAsync(data)
                .ContinueWithOnMainThread(LogIfFailed("WriteRanks"));
        }

        /// <summary>특정 uid의 등수가 기록되면 콜백을 호출합니다.</summary>
        public void SubscribeRank(string roomId, string uid, Action<int> callback)
        {
            string key = $"rank_{roomId}_{uid}";
            var reference = _database.Child("rooms").Child(roomId).Child("ranks").Child(uid);
            UnsubscribeByKey(key, reference);

            EventHandler<ValueChangedEventArgs> handler = (sender, args) =>
            {
                if (!args.Snapshot.Exists) return;
                if (int.TryParse(args.Snapshot.Value.ToString(), out int rank))
                    callback?.Invoke(rank);
            };

            reference.ValueChanged += handler;
            _handlers[key] = handler;
        }

        public void UnsubscribeRank(string roomId, string uid)
        {
            UnsubscribeByKey($"rank_{roomId}_{uid}",
                _database.Child("rooms").Child(roomId).Child("ranks").Child(uid));
        }

        #endregion

        #region ===== 정리 =====

        /// <summary>
        /// 특정 방의 모든 구독을 해제합니다.
        /// 방을 떠날 때 반드시 호출하세요.
        /// </summary>
        public void UnsubscribeAll(string roomId)
        {
            UnsubscribeRoom(roomId);
            UnsubscribeState(roomId);
            UnsubscribePlayers(roomId);
            UnsubscribeLive(roomId);
            UnsubscribeDeaths(roomId);
        }

        private void UnsubscribeByKey(string key, DatabaseReference reference)
        {
            if (_handlers.TryGetValue(key, out var handler))
            {
                reference.ValueChanged -= handler;
                _handlers.Remove(key);
            }
        }

        private Action<System.Threading.Tasks.Task> LogIfFailed(string label)
        {
            return task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                    Debug.LogError($"[RoomRepository] {label} 실패: {task.Exception?.Message}");
            };
        }

        #endregion
    }
}