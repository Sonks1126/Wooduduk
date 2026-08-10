using System;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static;

namespace Wooduduk.Network.Firebase
{
    public class UserService
    {
        /// <summary>
        /// 신규 유저 데이터를 생성합니다.
        /// suggestedNickname이 있으면 우선 사용하고, 없으면 랜덤 닉네임을 생성합니다.
        /// </summary>
        public UserData CreateUserData(string userId, string suggestedNickname = "")
        {
            long now = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

            string finalNickname = suggestedNickname;

            // suggestedNickname이 없거나 빈 값이면 랜덤 닉네임 생성
            if (string.IsNullOrEmpty(finalNickname))
            {
                finalNickname = GenerateRandomNickname();
            }

            return new UserData
            {
                _userId = userId,
                _userNick = finalNickname,    
                _tutorialDone = false,
                _gold = 0,
                _token = 0,

                _createdAt = now,
                _lastLoginAt = now,

                _bestScores = new int[3],
                _latestScore = 0,

                _axeTier = 0,
                _tierLp = 0,
                _tierProtection = 0,
                _gamesPlayed = 0,

                _ownedWeaponIds = new List<string> { "W001" },
                _equippedWeaponId = "W001",

                _rouletteSkinIds = new List<string>(),
                _equippedRouletteSkinId = string.Empty,
            };
        }

        /// <summary>
        /// 랜덤 닉네임 생성 (김진왕#1234 형식)
        /// </summary>
        internal static string GenerateRandomNickname()
        {
            return $"김진왕#{UnityEngine.Random.Range(1000, 9999)}";
        }
    }
}