using Wooduduk.Data.Core.TSVConvert;
using Wooduduk.Data.Static;
using System;
using System.Collections.Generic;
using UnityEngine;
using Wooduduk.Data.Static.StaticEnum;

namespace Wooduduk.Data.DataSO
{
    public abstract class BaseSheetDataSO : ScriptableObject, ISheetData
    {
        public abstract string ID { get; set; }
        public abstract void SetData(Dictionary<string, string> dict);

        // ===== 기본 파서 =====

        protected float ParseFloat(Dictionary<string, string> dict, string key)
            => dict.TryGetValue(key, out string raw) && float.TryParse(raw, out float v) ? v : 0f;

        protected int ParseInt(Dictionary<string, string> dict, string key)
            => dict.TryGetValue(key, out string raw) && int.TryParse(raw, out int v) ? v : 0;

        protected long ParseLong(Dictionary<string, string> dict, string key)
            => dict.TryGetValue(key, out string raw) && long.TryParse(raw, out long v) ? v : 0L;

        protected string ParseString(Dictionary<string, string> dict, string key)
            => dict.TryGetValue(key, out string raw) ? raw.Trim() : "Unknown";

        protected bool ParseBool(Dictionary<string, string> dict, string key)
        {
            if (!dict.TryGetValue(key, out string raw)) return false;
            return raw.Trim().ToLower() is "true" or "1" or "yes";
        }

        protected T ParseEnum<T>(Dictionary<string, string> dict, string key) where T : struct, Enum
        {
            if (!dict.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
                return default;
            return Enum.TryParse(raw.Trim(), true, out T result) ? result : default;
        }

        protected List<string> ParseStringList(Dictionary<string, string> dict, string key)
        {
            if (!dict.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
                return new List<string>();

            var list = new List<string>();
            foreach (var s in raw.Split(','))
            {
                var trimmed = s.Trim();
                if (!string.IsNullOrEmpty(trimmed)) list.Add(trimmed);
            }
            return list;
        }

        // ===== 효과 파서 =====
        // 포맷 : EffectType|Target|TargetSymbol|Value1|Value2|Duration
        // 여러 효과는 , 로 구분. TargetSymbol 없으면 빈칸(||)으로 둔다.

        protected List<EffectData> ParseEffects(Dictionary<string, string> dict, string key)
        {
            var result = new List<EffectData>();

            if (!dict.TryGetValue(key, out string raw) || string.IsNullOrWhiteSpace(raw))
                return result;

            if (raw.StartsWith("#")) // 시트에서 임시 비활성화용 주석
                return result;

            foreach (var effectPart in raw.Split(','))
            {
                if (string.IsNullOrWhiteSpace(effectPart)) continue;

                var parts = effectPart.Trim().Split('|');

                var effect = new EffectData();
                effect._effectType = parts.Length > 0 ? parts[0].Trim() : "";

                string targetRaw = parts.Length > 1 ? parts[1].Trim() : "Self";
                effect._target = Enum.TryParse(targetRaw, true, out EffectTarget t) ? t : EffectTarget.SELF;

                effect._targetSymbolId = parts.Length > 2 ? parts[2].Trim() : "";

                effect._value1 = parts.Length > 3 && float.TryParse(parts[3], out float v1) ? v1 : 0f;
                effect._value2 = parts.Length > 4 && float.TryParse(parts[4], out float v2) ? v2 : 0f;
                effect._durationSpin = parts.Length > 5 && int.TryParse(parts[5], out int d) ? d : 0;

                if (effect._durationSpin == -1)
                    effect._durationSpin = int.MaxValue; // -1 = 영구

                result.Add(effect);
            }

            return result;
        }
    }
}