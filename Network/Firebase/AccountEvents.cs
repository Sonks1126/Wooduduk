using Wooduduk.Data.Static;
using System;

namespace Wooduduk.Network.Firebase
{
    public static class AccountEvents
    {
        /// <summary>
        /// 로그인 성공
        /// Raise : FirebaseManager
        /// Listen : SaveManager
        /// </summary>
        public static event Action<string, string> OnLoginSucceeded;
        public static void RaiseLoginSucceeded(string uid, string nickname)
        {
            OnLoginSucceeded?.Invoke(uid, nickname);
        }

        /// <summary>
        /// 유저 데이터 로드 완료
        /// Raise : SaveManager
        /// Listen : UserManager, TitleRouter 등
        /// </summary>
        public static event Action<UserData> OnUserDataLoaded;
        public static void RaiseUserDataLoaded(UserData data)
        {
            OnUserDataLoaded?.Invoke(data);
        }

        /// <summary>
        /// 로그아웃
        /// Raise : FirebaseManager
        /// Listen : SaveManager, UserManager
        /// </summary>
        public static event Action<UserData> OnLogout;
        public static void RaiseLogout(UserData data)
        {
            OnLogout?.Invoke(data);
        }

        /// <summary>
        /// ★ 로그인 기록이 없어 로그인 선택 UI가 필요한 경우
        /// Raise : FirebaseManager
        /// Listen : LobbyUIManager (로그인 패널 활성화)
        /// </summary>
        public static event Action OnNeedLogin;
        public static void RaiseNeedLogin()
        {
            OnNeedLogin?.Invoke();
        }
    }
}