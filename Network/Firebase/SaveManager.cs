using System;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Tutorial;

namespace Wooduduk.Network.Firebase
{
    public class SaveManager : MonoBehaviour
    {
        public static SaveManager Instance { get; private set; }

        private UserRepository _repository;
        private UserService _service;
        private bool _isHardExit;

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
                _repository = new UserRepository();
                _service = new UserService();
            }
            else
            {
                Destroy(gameObject);
            }
        }

        private void OnEnable()
        {
            AccountEvents.OnLoginSucceeded += LoadUserData;
            AccountEvents.OnLogout += HandleLogout;
            TutorialDirector.OnTutorialCompleted += HandleTutorialCompleted;
        }

        private void OnDisable()
        {
            AccountEvents.OnLoginSucceeded -= LoadUserData;
            AccountEvents.OnLogout -= HandleLogout;
            TutorialDirector.OnTutorialCompleted -= HandleTutorialCompleted;
        }

        public void LoadUserData(string userId, string nickname)
        {
            Debug.Log($"[SaveManager] LoadUserData 시작: {userId}, 제안된 닉네임: {nickname}");

            _repository.GetUserData(userId, snapshot =>
            {
                UserData userData;

                if (snapshot == null || !snapshot.Exists)
                {
                    // ★ 신규 유저 (자동으로 _tutorialDone = false 상태로 생성됨)
                    Debug.Log($"[SaveManager] 신규 유저 생성: {userId}");
                    userData = _service.CreateUserData(userId, nickname);
                    SaveUserData(userData);
                }
                else
                {
                    // ★ 기존 유저 — 로드 후 누락 필드 보정
                    Debug.Log($"[SaveManager] 기존 유저 로드: {userId}");
                    userData = JsonUtility.FromJson<UserData>(snapshot.GetRawJsonValue());
                    userData = EnsureDefaults(userData, userId);

                    // 데이터베이스에 닉네임이 없는데 Firebase에서 닉네임이 넘어왔다면 업데이트
                    if (string.IsNullOrEmpty(userData._userNick) && !string.IsNullOrEmpty(nickname))
                    {
                        userData._userNick = nickname;
                        UpdateUserFields(userId, new Dictionary<string, object>
                        {                
                            { "_userNick", nickname }          
                        });
                    }

                    long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
                    userData._lastLoginAt = now;
                    _repository.UpdateFields(userId, new Dictionary<string, object>
                    {
                        { "_lastLoginAt", now }
                    });
                }

                Debug.Log($"[SaveManager] UserData 로드 완료 — " + $"Gold:{userData._gold} " + $"Best:{userData._bestScores[0]} " + $"Tier:{userData._axeTier}");

                // TitleRouter가 이 이벤트를 받아 _tutorialDone 여부로 씬을 분기함
                AccountEvents.RaiseUserDataLoaded(userData);
            });
        }

        private UserData EnsureDefaults(UserData data, string userId)
        {
            if (data == null)
                return _service.CreateUserData(userId);

            if (string.IsNullOrEmpty(data._userId))
                data._userId = userId;

            if (data._bestScores == null || data._bestScores.Length < 3)
                data._bestScores = new int[3];

            if (data._ownedWeaponIds == null)
                data._ownedWeaponIds = new List<string>();

            if (data._rouletteSkinIds == null)
                data._rouletteSkinIds = new List<string>();

            return data;
        }

        public void SaveUserData(UserData userData)
        {
            // ★★ 탈퇴 처리 중일 경우 절대 저장하지 않음 (삭제한 데이터를 다시 덮어쓰는 것 방지)
            if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsDeletingAccount)
            {
                Debug.Log("[SaveManager] 탈퇴 처리 중이므로 저장 스킵");
                return;
            }

            if (userData == null) return;
            if (string.IsNullOrEmpty(userData._userId)) return;

            string json = JsonUtility.ToJson(userData, true);
            Debug.Log($"[SaveManager] 저장할 JSON:\n{json}");

            if (string.IsNullOrEmpty(json) || json == "{}")
            {
                Debug.LogError("[SaveManager] 직렬화 실패! UserData 클래스에 [Serializable]이 있나요?");
                return;
            }

            _repository.SaveUserData(userData._userId, json);
        }

        private void HandleTutorialCompleted()
        {
            if (UserManager.Instance == null || !UserManager.Instance.TryGetUser(out var user))
                return;

            user._tutorialDone = true;

            _repository.UpdateFields(user._userId, new Dictionary<string, object>
        {        { "_tutorialDone", true }    });

            Debug.Log("[SaveManager] 튜토리얼 완료 → _tutorialDone 즉시 저장");
        }

        private void HandleLogout(UserData user)
        {
            if (user == null) return;
            _repository.UpdateFields(user._userId, new Dictionary<string, object>
            {
                { "_equippedWeaponId", user._equippedWeaponId },
                { "_equippedRouletteSkinId", user._equippedRouletteSkinId },
            });
        }

        public void UpdateUserFields(string userId, Dictionary<string, object> fields)
        {
            if (string.IsNullOrEmpty(userId)) return;
            _repository.UpdateFields(userId, fields);
        }

        public static List<object> ToFirebaseList(List<string> src)
        {
            var list = new List<object>();
            if (src != null) foreach (var s in src) list.Add(s);
            return list;
        }

        // 데이터 삭제 부분 (탈퇴 시 호출됨)
        public void DeleteUserData(string userId, Action onComplete, Action<string> onError)
        {
            // ★ 기존에 return만 하면 onComplete가 안 불려서 FirebaseManager 탈퇴 로직이 멈추는 버그 수정
            if (string.IsNullOrEmpty(userId))
            {
                onComplete?.Invoke();
                return;
            }
            _repository.DeleteUserData(userId, onComplete, onError);
        }

        public void DeleteAndRecreate(string userId)
        {
            _repository.DeleteUserData(userId, () =>
            {
                UserData fresh = _service.CreateUserData(userId);
                SaveUserData(fresh);
                AccountEvents.RaiseUserDataLoaded(fresh);
                Debug.Log("[SaveManager] 삭제 후 기본값으로 재생성 완료");
            });
        }

        #region 강제 종료 저장

        private void OnApplicationPause(bool pauseStatus)
        {
            if (!pauseStatus) return;
            _isHardExit = false; // 복귀 가능한 일시정지
            ForceSaveSession(soft: true);
        }

        private void OnApplicationQuit()
        {
            _isHardExit = true;
            ForceSaveSession(soft: false);
        }

        private void ForceSaveSession(bool soft)
        {
            if (FirebaseManager.Instance != null && FirebaseManager.Instance.IsDeletingAccount) return;
            if (UserManager.Instance == null || !UserManager.Instance.TryGetUser(out var user)) return;

            var fields = new Dictionary<string, object>
    {
        { "_gold", user._gold },
        { "_token", user._token },
        { "_tutorialDone", user._tutorialDone },
        { "_equippedWeaponId", user._equippedWeaponId },
        { "_equippedRouletteSkinId", user._equippedRouletteSkinId },
        { "_ownedWeaponIds", user._ownedWeaponIds },
        { "_rouletteSkinIds", user._rouletteSkinIds },
    };

            if (soft) // 일반 일시정지에서는 최근 플레이 기록도 살림
            {
                fields["_latestScore"] = user._latestScore;
                fields["_latestMaxCombo"] = user._latestMaxCombo;
                fields["_latestSettlementCount"] = user._latestSettlementCount;
                fields["_gamesPlayed"] = user._gamesPlayed;
                fields["_bestScores"] = new List<int>
            { user._bestScores[0], user._bestScores[1], user._bestScores[2] };
                fields["_axeTier"] = user._axeTier;
                fields["_tierLp"] = user._tierLp;
                fields["_tierProtection"] = user._tierProtection;
            }

            _repository.UpdateFields(user._userId, fields);
        }

#if UNITY_EDITOR
        private void OnDestroy()
        {
            if (Instance == this)
            {
                Debug.Log("[SaveManager] 에디터 정지 — 최종 저장 시도");
                ForceSaveSession(soft: false);
            }
        }
#endif

        #endregion
    }
}