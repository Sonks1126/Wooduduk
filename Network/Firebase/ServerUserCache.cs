using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static;

namespace Wooduduk.Network.Firebase
{
    /// <summary>
    /// 서버에서 userId로 UserData를 찾기 위한 임시 캐시입니다.
    ///
    /// 멀티 서버에서는 UserManager.Instance.CurrentUserData에 의존하면 안 됩니다.
    /// 그래서 로그인/입장 시점에 서버가 UserData를 캐싱해두고,
    /// 런 종료 저장 시 이 캐시에서 꺼내 씁니다.
    ///
    /// TODO:
    /// - 로그인 완료 시 Cache(uid, userData) 호출 연결
    /// - 로그아웃/연결 종료 시 Remove(uid) 호출 연결
    /// - 전용 서버 구조에서는 Firebase에서 직접 로드하는 방식으로 교체 가능
    /// </summary>
    public static class ServerUserCache
    {
        private static readonly Dictionary<string, UserData> _cache = new();

        public static void Cache(string uid, UserData data)
        {
            if (string.IsNullOrEmpty(uid) || data == null)
                return;

            _cache[uid] = data;
            Debug.Log($"[ServerUserCache] Cache: {uid}");
        }

        public static UserData GetUserData(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return null;

            return _cache.TryGetValue(uid, out UserData data) ? data : null;
        }

        public static void Remove(string uid)
        {
            if (string.IsNullOrEmpty(uid))
                return;

            if (_cache.Remove(uid))
                Debug.Log($"[ServerUserCache] Remove: {uid}");
        }

        public static void Clear()
        {
            _cache.Clear();
            Debug.Log("[ServerUserCache] Clear");
        }
    }
}