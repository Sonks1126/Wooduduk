using System;
using UnityEngine;
using UnityEngine.Serialization;
using Wooduduk.SFX;

namespace Wooduduk.Data.DataSO
{
    // 데이터 구동 카탈로그: 클립·볼륨·피치 전부 인스펙터 튜닝(코드 수정 불필요).
    // CreateAssetMenu 경로는 기존 TierConfig/MatchmakingConfig("GameData/...") 컨벤션을 따름.
    [CreateAssetMenu(fileName = "SoundCatalog", menuName = "GameData/SoundCatalog")]
    public sealed class SoundCatalogSO : ScriptableObject
    {
        [Header("콤보 마일스톤")] 
        public HitPitchConfig _comboPitch = new HitPitchConfig();

        [Serializable]
        public sealed class SfxEntry
        {
            public SfxId _id;
            public AudioClip[] _clips;                              // 여러 개면 무반복 랜덤
            [Range(0f, 1f)] public float _volume = 1f;
            public Vector2 _pitchRange = new Vector2(0.97f, 1.03f); // 미세 피치 변화
            public SfxLayer[] _layers;
            [Range(0f, 1f)] public float _spatialBlend = 1f; // 0=2D(UI·콤보 팡파레 등 비다이제틱), 1=3D
            public float _minDistance = 2f;                  // 풀볼륨 반경
            public float _maxDistance = 20f;                 // 감쇠 끝
        }
        [Serializable]
        public sealed class SfxLayer
        {
            public AudioClip[] _clips;                              // 여러 개면 무반복 랜덤
            [Range(0f, 1f)] public float _volume = 1f;
            public Vector2 _pitchRange = new Vector2(0.97f, 1.03f);
            [Min(0f)] public float _delay = 0f;                     // 발사 지연(초). 잔향 레이어용
        }

        [Serializable]
        public sealed class BgmEntry
        {
            public BgmId _id;
            public AudioClip[] _clips;            // 1개 = 무한 루프 / 여러 개 = 로테이션
            [Range(0f, 1f)] public float _volume = 0.8f;
            public bool _shuffle = true;          // ON: 무반복 랜덤 / OFF: 등록 순서 반복
        }
        [Serializable]
        public sealed class AmbientEntry
        {
            public AmbientId _id; 
            public AudioClip _clip; 
            [Range(0f, 1f)] public float _volume = 0.6f;
            [Range(0f, 1f)] public float _spatialBlend = 0f;  // 0=2D, 1=3D 위치기반. 모닥불=1, 숲(Map)=0
            public float _minDistance = 2.5f;                   // 3D 풀볼륨 반경
            public float _maxDistance = 15f;                  // 3D 감쇠 끝
        }
        [Serializable]
        public sealed class ReelConfig
        {
            [Header("회전")]
            public AudioClip[] _rollTickClips;                 // 번갈아(순차) 재생할 짧은 클릭들
            [Min(0.01f)] public float _tickInterval = 0.06f;   // 클릭 간격(초). 작을수록 빠른 회전감
            [Header("정지")]
            public AudioClip[] _stopClips;                     // 정지 (무반복 랜덤)
            [Range(0f, 1f)] public float _volume = 0.9f;
            public float _stopBasePitch = 1f;
            public float _stopPitchStep = 0.05f;               // 릴 인덱스마다 +피치(왼→오 상승감)
        }
        [Serializable]
        public sealed class HitPitchConfig
        {
            public float _basePitch = 1f;                     // 스윙 1개 기준 피치
            [FormerlySerializedAs("_pitchPerTier")]
            public float _pitchPerSwing = 0.06f;              // 스윙 1개 추가마다 +피치
            [Min(1)] public int _maxSwingSteps = 6;           // 사다리 상한(1~6 = 총 6계단)
        }

        [Header("타격 피치")] public HitPitchConfig _hitPitch = new HitPitchConfig();
        [Header("효과음")] public SfxEntry[] _sfx;
        [Header("배경음")] public BgmEntry[] _bgm;
        [Header("환경음")] public AmbientEntry[] _ambient;
        [Header("릴")] public ReelConfig _reel = new ReelConfig();

        // 선형 탐색
        public BgmEntry GetBgm(BgmId id)
        { if (_bgm == null) return null; for (int i = 0; i < _bgm.Length; i++) if (_bgm[i] != null && _bgm[i]._id == id) return _bgm[i]; return null; }
        public AmbientEntry GetAmbient(AmbientId id)
        { if (_ambient == null) return null; for (int i = 0; i < _ambient.Length; i++) if (_ambient[i] != null && _ambient[i]._id == id) return _ambient[i]; return null; }
        public SfxEntry GetSfx(SfxId id)
        { if (_sfx == null) return null; for (int i = 0; i < _sfx.Length; i++) if (_sfx[i] != null && _sfx[i]._id == id) return _sfx[i]; return null; }
    }
}