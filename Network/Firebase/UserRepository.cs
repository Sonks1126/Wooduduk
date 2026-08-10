using Firebase.Database;
using Firebase.Extensions;
using System;
using System.Collections.Generic;
using UnityEngine;
using static System.Net.WebRequestMethods;

namespace Wooduduk.Network.Firebase
{
    public class UserRepository
    {
        private DatabaseReference _db;

        public UserRepository()
        {
            _db = FirebaseDatabase.DefaultInstance.RootReference;
        }

        public void GetUserData(string userId, Action<DataSnapshot> callback)
        {
            _db.Child("Users").Child(userId).GetValueAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted || task.IsCanceled)
                {
                    Debug.LogError($"[UserRepository] 유저 로드 실패: {userId}");
                    callback?.Invoke(null);
                    return;
                }
                callback?.Invoke(task.Result);
            });
        }

        public void SaveUserData(string userId, string json)
        {
            if (string.IsNullOrEmpty(userId) || string.IsNullOrEmpty(json))
            {
                Debug.LogError($"[UserRepository] 저장 실패: 빈 데이터. uid={userId}");
                return;
            }

            Debug.Log($"[UserRepository] 저장 요청 전송: uid={userId} (Json 길이: {json.Length})");

            _db.Child("Users").Child(userId).SetRawJsonValueAsync(json)
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsCompleted)
                        Debug.Log($"[UserRepository] ✅ 저장 성공: {userId}");
                    else if (task.IsCanceled)
                        Debug.LogError($"[UserRepository] ❌ 저장 취소됨: {userId}");
                    else
                        Debug.LogError($"[UserRepository] ❌ 저장 실패: {userId} | {task.Exception?.GetBaseException()?.Message}");
                });
        }

        public void UpdateFields(string userId, Dictionary<string, object> fields)
        {
            if (string.IsNullOrEmpty(userId) || fields == null || fields.Count == 0) return;

            Debug.Log($"[UserRepository] 필드 갱신 요청: uid={userId}, 필드수={fields.Count}");
            _db.Child("Users").Child(userId).UpdateChildrenAsync(fields)
                .ContinueWithOnMainThread(task =>
                {
                    if (!task.IsCompleted)
                        Debug.LogError($"[UserRepository] 필드 갱신 실패: {task.Exception?.GetBaseException()?.Message}");
                });
        }

        // 데이터 삭제
        public void DeleteUserData(string userId, Action onComplete = null, Action<string> onError = null)
        {
            _db.Child("Users").Child(userId).RemoveValueAsync()
                .ContinueWithOnMainThread(task =>
                {
                    if (task.IsFaulted || task.IsCanceled)
                    {
                        string msg = task.Exception?.GetBaseException()?.Message ?? "삭제 실패";
                        Debug.LogError($"[UserRepository] 삭제 실패: {msg}");
                        onError?.Invoke(msg);  // ← 에러 콜백 호출
                    }
                    else
                    {
                        Debug.Log($"[UserRepository] 삭제 완료: {userId}");
                        onComplete?.Invoke();
                    }
                });
        }
    }
}