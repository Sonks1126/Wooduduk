using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Network.Firebase;

namespace Wooduduk.Data.Tier
{
    public class TierSaveService
    {
        public void SaveTierState(int axeTier, int lp, int protection)
        {
            if (UserManager.Instance == null || !UserManager.Instance.TryGetUser(out var user))
            {
                Debug.LogWarning("[TierSaveService] UserData가 없습니다.");
                return;
            }

            if (SaveManager.Instance == null)
            {
                Debug.LogError("[TierSaveService] SaveManager가 없습니다.");
                return;
            }

            // ★ 중요: 메모리 UserData도 즉시 갱신
            user._axeTier = axeTier;
            user._tierLp = lp;
            user._tierProtection = protection;

            Debug.Log(
                $"[TierSaveService] SaveTierState 요청 → " +
                $"axeTier={axeTier}, lp={lp}, protection={protection}, user={user._userId}"
            );

            SaveManager.Instance.UpdateUserFields(user._userId, new Dictionary<string, object>
            {
                { "_axeTier",        axeTier },
                { "_tierLp",         lp },
                { "_tierProtection", protection },
            });
        }

        public bool LoadTierState(out int axeTier, out int lp, out int protection)
        {
            axeTier = 0;
            lp = 0;
            protection = 0;

            if (UserManager.Instance == null || !UserManager.Instance.TryGetUser(out var user))
            {
                Debug.LogWarning("[TierSaveService] LoadTierState: UserData가 없습니다. 기본값(0/0/0) 반환.");
                return false;
            }

            axeTier = user._axeTier;
            lp = user._tierLp;
            protection = user._tierProtection;

            Debug.Log(
                $"[TierSaveService] LoadTierState 성공 → " +
                $"axeTier={axeTier}, lp={lp}, protection={protection}, user={user._userId}"
            );

            return true;
        }
    }
}