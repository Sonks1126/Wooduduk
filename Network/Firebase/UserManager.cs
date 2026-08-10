using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static;
using Wooduduk.Network.Firebase.Leaderboard;

namespace Wooduduk.Network.Firebase
{
    /// <summary>
    /// 유저의 현재 세션 데이터(CurrentUserData)를 관리하고, 
    /// 아이템 보유 여부 확인, 장착, 데이터 갱신 및 중복 보상 처리를 총괄하는 매니저입니다.
    /// </summary>
    public class UserManager : MonoBehaviour
    {
        public static UserManager Instance { get; private set; }

        // 현재 메모리에 로드된 유저의 전체 데이터
        public UserData CurrentUserData { get; private set; }

        private void Awake()
        {
            // 싱글톤 패턴: 씬 전환 시에도 파괴되지 않게 설정
            if (Instance == null) { Instance = this; DontDestroyOnLoad(gameObject); }
            else { Destroy(gameObject); return; }
        }

        private void OnEnable()
        {
            // 서버로부터 데이터 로드가 완료되었을 때 호출되는 이벤트 구독
            AccountEvents.OnUserDataLoaded += SetUserData;
            // 로그아웃 시 데이터 초기화 구독
            AccountEvents.OnLogout += ClearCurrentUser;
        }

        private void OnDisable()
        {
            // 메모리 누수 방지를 위해 이벤트 구독 해제
            AccountEvents.OnUserDataLoaded -= SetUserData;
            AccountEvents.OnLogout -= ClearCurrentUser;
        }

        #region --- 데이터 라이프사이클 ---

        /// <summary>
        /// 서버/DB에서 불러온 데이터를 세션에 적용합니다.
        /// </summary>
        private void SetUserData(UserData userData)
        {
            if (userData == null) return;
            CurrentUserData = userData;

            // 데이터 로드 직후, 혹시 모를 비정상적인 장착 상태를 검증합니다.
            ValidateEquipment();

            // 유저 데이터 확정 즉시 리더보드 사전 로드 (패널 열기 전에 준비)
            LeaderboardCache.Clear();
            LeaderboardService.Load(userData);

            Debug.Log($"[UserManager] 유저 데이터 적용: {userData._userId}");
        }

        /// <summary>
        /// 로그아웃 시 메모리 내 유저 데이터를 비웁니다.
        /// </summary>
        private void ClearCurrentUser(UserData user)
        {
            string uid = user != null ? user._userId : CurrentUserData != null ? CurrentUserData._userId : null;

            CurrentUserData = null;
        }

        /// <summary>
        /// 안전하게 유저 데이터를 가져옵니다. (데이터 로드 여부를 bool로 반환)
        /// </summary>
        public bool TryGetUser(out UserData user)
        {
            user = CurrentUserData;
            return user != null;
        }

        #endregion

        #region --- 아이템 및 재화 관리 ---

        /// <summary>
        /// 무기를 획득 처리합니다. 중복 시 재화를 환불합니다.
        /// </summary>
        public bool AddWeapon(string weaponId, int refundGold, out bool isNewAcquisition)
        {
            return ProcessItemAcquisition(weaponId, refundGold, out isNewAcquisition, true);
        }

        /// <summary>
        /// 룰렛 스킨을 획득 처리합니다. 중복 시 재화를 환불합니다.
        /// </summary>
        public bool AddRouletteSkin(string skinId, int refundGold, out bool isNewAcquisition)
        {
            return ProcessItemAcquisition(skinId, refundGold, out isNewAcquisition, false);
        }

        /// <summary>
        /// 무기와 스킨 획득 로직을 통합한 내부 메서드입니다.
        /// </summary>
        private bool ProcessItemAcquisition(string itemId, int refundGold, out bool isNewAcquisition, bool isWeapon)
        {
            isNewAcquisition = false;
            if (string.IsNullOrEmpty(itemId) || !HasCurrentUser()) return false;

            EnsureUserDataLists();

            // 대상 리스트 참조
            var list = isWeapon ? CurrentUserData._ownedWeaponIds : CurrentUserData._rouletteSkinIds;
            string listFieldName = isWeapon ? "_ownedWeaponIds" : "_rouletteSkinIds";

            // 1. 중복 보유 시: 환불 로직
            if (list.Contains(itemId))
            {
                CurrentUserData._gold += refundGold;
                SaveField("_gold", CurrentUserData._gold);
                Debug.Log($"[UserManager] 중복 획득 ({itemId})! {refundGold} 골드 환불됨.");
                return false;
            }

            // 2. 신규 획득 시: 리스트 추가 후 동기화
            list.Add(itemId);
            isNewAcquisition = true;
            SaveField(listFieldName, SaveManager.ToFirebaseList(list));
            return true;
        }

        /// <summary> 
        /// 골드를 차감합니다. 보유량이 부족하면 false를 반환합니다. 
        /// </summary>
        public bool SpendGold(int amount)
        {
            if (!HasCurrentUser() || CurrentUserData._gold < amount) return false;
            CurrentUserData._gold -= amount;
            SaveField("_gold", CurrentUserData._gold);
            return true;
        }

        /// <summary> 
        /// 토큰을 차감합니다. 보유량이 부족하면 false를 반환합니다. 
        /// </summary>
        public bool SpendTokens(int amount)
        {
            if (!HasCurrentUser() || CurrentUserData._token < amount) return false;
            CurrentUserData._token -= amount;
            SaveField("_token", CurrentUserData._token);
            return true;
        }

        /// <summary> 
        /// 토큰을 증가시킵니다. 
        /// </summary>
        public bool AddTokens(int amount)
        {
            if (!HasCurrentUser()) return false;
            CurrentUserData._token += amount;
            SaveField("_token", CurrentUserData._token);
            return true;
        }

        #endregion

        #region --- 보유 여부 및 장착 ---

        /// <summary> 무기 ID를 리스트에서 찾아 보유 여부를 반환합니다. </summary>
        public bool IsWeaponOwned(string weaponId) => ContainsId(CurrentUserData?._ownedWeaponIds, weaponId);

        /// <summary> 스킨 ID를 리스트에서 찾아 보유 여부를 반환합니다. </summary>
        public bool IsRouletteOwned(string rouletteId) => ContainsId(CurrentUserData?._rouletteSkinIds, rouletteId);

        /// <summary> 무기를 장착합니다. 미보유 시 장착 불가능. </summary>
        public bool EquipWeapon(string weaponId)
        {
            if (string.IsNullOrEmpty(weaponId) || !IsWeaponOwned(weaponId)) return false;
            CurrentUserData._equippedWeaponId = weaponId;
            SaveField("_equippedWeaponId", weaponId);
            return true;
        }

        /// <summary> 룰렛 스킨을 장착합니다. 미보유 시 장착 불가능. </summary>
        public bool EquipRouletteSkin(string skinId)
        {
            if (string.IsNullOrEmpty(skinId) || !IsRouletteOwned(skinId)) return false;
            CurrentUserData._equippedRouletteSkinId = skinId;
            SaveField("_equippedRouletteSkinId", skinId);
            return true;
        }

        #endregion

        #region --- 내부 유틸리티 ---

        /// <summary> null 체크와 리스트 포함 여부를 한 번에 처리하는 내부 유틸리티 </summary>
        private bool ContainsId(List<string> list, string id)
        {
            return !string.IsNullOrEmpty(id) && list != null && list.Contains(id);
        }

        /// <summary>
        /// 서버 데이터 로드 시, '장착 중인 아이템'이 '보유 목록'에 없는 경우를 찾아 초기화합니다.
        /// 데이터 오염(데이터 꼬임)을 방지하는 안전장치입니다.
        /// </summary>
        private void ValidateEquipment()
        {
            if (CurrentUserData == null) return;

            // 장착 무기 검증
            if (!string.IsNullOrEmpty(CurrentUserData._equippedWeaponId) && !IsWeaponOwned(CurrentUserData._equippedWeaponId))
            {
                CurrentUserData._equippedWeaponId = null;
                SaveField("_equippedWeaponId", null);
            }

            // 장착 스킨 검증
            if (!string.IsNullOrEmpty(CurrentUserData._equippedRouletteSkinId) && !IsRouletteOwned(CurrentUserData._equippedRouletteSkinId))
            {
                CurrentUserData._equippedRouletteSkinId = null;
                SaveField("_equippedRouletteSkinId", null);
            }
        }

        /// <summary> 특정 필드만 Firebase에 업데이트하여 전체 덮어쓰기 위험을 방지합니다. </summary>
        private void SaveField(string field, object value)
        {
            if (CurrentUserData == null || SaveManager.Instance == null) return;
            SaveManager.Instance.UpdateUserFields(CurrentUserData._userId, new Dictionary<string, object> { { field, value } });
        }

        private bool HasCurrentUser() => CurrentUserData != null;

        /// <summary> 리스트가 null일 경우 초기화하여 NullReference 예외를 방지합니다. </summary>
        private void EnsureUserDataLists()
        {
            if (CurrentUserData == null) return;
            if (CurrentUserData._rouletteSkinIds == null) CurrentUserData._rouletteSkinIds = new List<string>();
            if (CurrentUserData._ownedWeaponIds == null) CurrentUserData._ownedWeaponIds = new List<string>();
        }

        #endregion
    }
}