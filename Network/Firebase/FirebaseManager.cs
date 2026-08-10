using Firebase;
using Firebase.Auth;
using Firebase.Extensions;
using UnityEngine;
using System;

namespace Wooduduk.Network.Firebase
{
    public class FirebaseManager : MonoBehaviour
    {
        public static FirebaseManager Instance { get; private set; }

        private FirebaseAuth _auth;
        private bool _isDeletingAccount = false; // 탈퇴 진행 중 플래그

        public string UserId => _auth?.CurrentUser?.UserId;
        public bool IsDeletingAccount => _isDeletingAccount; // SaveManager가 참조함

        private void Awake()
        {
            if (Instance == null)
            {
                Instance = this;
                DontDestroyOnLoad(gameObject);
            }
            else
            {
                Destroy(gameObject);
                return;
            }
        }

        private void Start()
        {
            InitializeFirebase();
        }

        private void InitializeFirebase()
        {
            FirebaseApp.CheckAndFixDependenciesAsync().ContinueWithOnMainThread(task =>
            {
                DependencyStatus status = task.Result;
                if (status != DependencyStatus.Available)
                {
                    Debug.LogError($"Firebase 초기화 실패 : {status}");
                    return;
                }

                Debug.Log("Firebase 초기화 성공");
                _auth = FirebaseAuth.DefaultInstance;

                // ★ 자동 로그인 확인 (무조건 익명 로그인하지 않음)
                CheckExistingLogin();
            });
        }

        // =========================================================
        // 1. 기존 로그인 기록 확인 (자동 로그인)
        // =========================================================
        private void CheckExistingLogin()
        {
            FirebaseUser currentUser = _auth.CurrentUser;

            // 2-1. 이미 로그인된 유저라면 → 토큰 검증 후 자동 로그인
            if (currentUser != null)
            {
                Debug.Log($"[FirebaseManager] 캐시된 유저 발견: {currentUser.UserId}, 토큰 검증 중...");

                currentUser.ReloadAsync().ContinueWithOnMainThread(reloadTask =>
                {
                    if (reloadTask.IsFaulted || reloadTask.IsCanceled)
                    {
                        Debug.LogWarning("[FirebaseManager] 캐시된 유저 무효 → 로그인 UI 표시");
                        _auth.SignOut();
                        AccountEvents.RaiseNeedLogin();
                    }
                    else
                    {
                        Debug.Log($"[FirebaseManager] 자동 로그인 성공: {currentUser.UserId}");
                        AccountEvents.RaiseLoginSucceeded(currentUser.UserId, string.Empty);
                    }
                });
            }
            // 2-2. 로그인 기록이 없다면 → 로그인 선택 UI 표시
            else
            {
                Debug.Log("[FirebaseManager] 로그인 기록 없음 → 로그인 UI 표시");
                AccountEvents.RaiseNeedLogin();
            }
        }

        // =========================================================
        // 2. 게스트(익명) 로그인 - 버튼 클릭 시에만 호출
        // =========================================================
        /// <summary>
        /// 로그인 UI에서 [게스트로 시작하기] 버튼을 눌렀을 때 호출됩니다.
        /// </summary>
        public void SignInAsGuest()
        {
            if (_auth.CurrentUser != null)
            {
                Debug.LogWarning("[FirebaseManager] 이미 로그인된 상태입니다.");
                return;
            }

            Debug.Log("[FirebaseManager] 게스트(익명) 로그인 시작...");
            _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[FirebaseManager] 게스트 로그인 실패: {task.Exception}");
                    return;
                }

                string uid = _auth.CurrentUser.UserId;
                string nickname = UserService.GenerateRandomNickname();
                Debug.Log($"[FirebaseManager] 게스트 로그인 성공: {uid}, 닉네임: {nickname}");

                AccountEvents.RaiseLoginSucceeded(uid, nickname);
            });
        }

        // =========================================================
        // 2-1. 게스트 로그인 + 닉네임 직접 입력 (아이디 생성 버튼용)
        // =========================================================
        /// <summary>
        /// 닉네임 입력 UI 에서 입력 완료 후 호출됩니다.
        /// </summary>
        public void SignInWithCustomNickname(string nickname)
        {
            if (_auth.CurrentUser != null)
            {
                Debug.LogWarning("[FirebaseManager] 이미 로그인된 상태입니다.");
                return;
            }

            Debug.Log($"[FirebaseManager] 커스텀 닉네임 로그인 시작... (닉네임: {nickname})");
            _auth.SignInAnonymouslyAsync().ContinueWithOnMainThread(task =>
            {
                if (task.IsCanceled || task.IsFaulted)
                {
                    Debug.LogError($"[FirebaseManager] 커스텀 닉네임 로그인 실패: {task.Exception}");
                    return;
                }

                string uid = _auth.CurrentUser.UserId;
                Debug.Log($"[FirebaseManager] 커스텀 닉네임 로그인 성공: {uid}");

                // ★ 입력된 닉네임 전달 (빈 값이 아님)
                AccountEvents.RaiseLoginSucceeded(uid, nickname);
            });
        }

        // =========================================================
        // 3. 구글 로그인 / 연동 (기존 유지)
        // =========================================================
        public void LoginWithGoogle(string idToken)
        {
            Credential credential = GoogleAuthProvider.GetCredential(idToken, null);
            _auth.SignInWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError(task.Exception);
                    return;
                }
                string uid = _auth.CurrentUser.UserId;
                AccountEvents.RaiseLoginSucceeded(uid, "");
            });
        }

        public void LinkGoogleAccount(string idToken)
        {
            FirebaseUser currentUser = _auth.CurrentUser;
            if (currentUser == null) return;

            Credential credential = GoogleAuthProvider.GetCredential(idToken, null);
            currentUser.LinkWithCredentialAsync(credential).ContinueWithOnMainThread(task =>
            {
                if (task.IsFaulted)
                {
                    Debug.LogError(task.Exception);
                    return;
                }
                Debug.Log("[FirebaseManager] 구글 계정 연동 성공");
            });
        }

        // =========================================================
        // 4. 로그아웃
        // =========================================================
        public void Logout()
        {
            if (_auth == null) return;
            _auth.SignOut();
            Debug.Log("[FirebaseManager] 로그아웃 완료");

            // 로그아웃 시 다시 로그인 선택 UI로 돌아감
            AccountEvents.RaiseNeedLogin();
        }

        // =========================================================
        // 5. 계정 탈퇴 (설정 메뉴에서 호출 - DB 먼저 삭제!)
        // =========================================================
        // =========================================================
        // ★★★ 계정 탈퇴 (익명 전용, 클라이언트 직접 삭제 + 토큰 갱신 안전장치) ★★★
        // =========================================================
        /// <summary>
        /// 익명 계정 전용 탈퇴 처리.
        /// 삭제 직전 토큰을 강제 갱신하여 requires-recent-login 에러를 방지합니다.
        /// 순서: DB 데이터 삭제 → 토큰 갱신 → Auth 계정 삭제 → SignOut
        /// </summary>
        public void RequestAccountDeletion(Action onDeleted, Action<string> onError)
        {
            if (_auth?.CurrentUser == null)
            {
                onError?.Invoke("로그인이 필요합니다.");
                return;
            }

            var user = _auth.CurrentUser;

            // ★ 안전장치: 익명 계정이 아니면 (구글 등 연동된 경우) 별도 처리 필요할 수 있음
            if (!user.IsAnonymous)
            {
                Debug.LogWarning("[FirebaseManager] 익명 계정이 아닙니다. 삭제 정책을 확인하세요.");
                // 필요하면 여기서 onError로 막을 수도 있음
            }

            _isDeletingAccount = true;
            string uid = user.UserId;

            Debug.Log($"[FirebaseManager] 계정 탈퇴 시작 - UID: {uid}");

            // 1단계: DB 데이터 먼저 삭제 (Auth가 살아있어야 권한이 있음)
            if (SaveManager.Instance == null)
            {
                _isDeletingAccount = false;
                onError?.Invoke("SaveManager를 찾을 수 없습니다.");
                return;
            }

            SaveManager.Instance.DeleteUserData(
              uid,
              () =>
              {
                  Debug.Log("[FirebaseManager] DB 데이터 삭제 완료 - 토큰 갱신 시작");

                  user.TokenAsync(true).ContinueWithOnMainThread(tokenTask =>
                  {
                      if (tokenTask.IsFaulted || tokenTask.IsCanceled)
                      {
                          _isDeletingAccount = false;
                          Debug.LogError($"[FirebaseManager] 토큰 갱신 실패: {tokenTask.Exception}");
                          onError?.Invoke("인증 갱신에 실패했습니다. 다시 시도해주세요.");
                          return;
                      }

                      Debug.Log("[FirebaseManager] 토큰 갱신 완료 - Auth 계정 삭제 시작");

                      user.DeleteAsync().ContinueWithOnMainThread(deleteTask =>
                      {
                          if (deleteTask.IsCanceled)
                          {
                              _isDeletingAccount = false;
                              onError?.Invoke("작업이 취소되었습니다.");
                              return;
                          }

                          if (deleteTask.IsFaulted)
                          {
                              _isDeletingAccount = false;
                              Debug.LogError($"[FirebaseManager] Auth 계정 삭제 실패: {deleteTask.Exception}");

                              string msg = "계정 삭제에 실패했습니다.";

                              if (deleteTask.Exception != null)
                              {
                                  foreach (var ex in deleteTask.Exception.Flatten().InnerExceptions)
                                  {
                                      if (ex is FirebaseException firebaseException)
                                      {
                                          Debug.LogError($"Firebase ErrorCode : {firebaseException.ErrorCode}");
                                      }
                                  }
                              }

                              onError?.Invoke(msg);
                              return;
                          }

                          _auth.SignOut();
                          _isDeletingAccount = false;

                          Debug.Log("[FirebaseManager] 탈퇴 완료 - 모든 데이터 삭제됨");
                          onDeleted?.Invoke();

                          AccountEvents.RaiseNeedLogin();
                      });
                  });
              },
              error =>
              {
                  _isDeletingAccount = false;
                  Debug.LogError($"[FirebaseManager] DB 삭제 실패 : {error}");
                  onError?.Invoke(error);
              });
        }
    }
}