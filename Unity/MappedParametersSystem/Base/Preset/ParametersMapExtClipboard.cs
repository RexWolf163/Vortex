#if UNITY_EDITOR
using System;
using System.Linq;
using Sirenix.OdinInspector;
using UnityEditor;
using UnityEngine;
using Vortex.Core.MappedParametersSystem.Base;

namespace Vortex.Unity.MappedParametersSystem.Base.Preset
{
    public partial class ParametersMapStorage
    {
        #region DTO

        [Serializable]
        internal class ParametersMapDTO
        {
            public string[] baseParams;
            public MappedParameterPresetDTO[] mappedParams;
        }

        [Serializable]
        internal class MappedParameterPresetDTO
        {
            public string name;
            public MappedParameterLinkDTO[] parents;
            public ParameterLinkCostLogic costLogic;
        }

        [Serializable]
        internal class MappedParameterLinkDTO
        {
            public string parent;
            public int cost;
        }

        #endregion

        /// <summary>
        /// Сериализует текущее состояние карты параметров в JSON и копирует в буфер обмена.
        /// </summary>
        [Button("To Clipboard"), HorizontalGroup("Импорт-Экспорт/clipboard"), BoxGroup("Импорт-Экспорт")]
        [Tooltip("Система импорта и экспорта данных через буфер памяти для быстрой смены схем параметров")]
        public void CopyToClipboardAsJson()
        {
            try
            {
                var dto = new ParametersMapDTO
                {
                    baseParams = baseParams ?? new string[0],
                    mappedParams = (mappedParams ?? new MappedParameterPreset[0])
                        .Select(p => new MappedParameterPresetDTO
                        {
                            name = p.name,
                            costLogic = p.CostLogic,
                            parents = p.Parents?.Select(link => new MappedParameterLinkDTO
                            {
                                parent = link.Parent,
                                cost = link.Cost
                            }).ToArray() ?? new MappedParameterLinkDTO[0]
                        })
                        .ToArray()
                };
                var json = JsonUtility.ToJson(dto, true);
                EditorGUIUtility.systemCopyBuffer = json;
                Debug.Log($"[ParametersMap] JSON скопирован в буфер обмена ({json.Length} символов).");
            }
            catch (Exception e)
            {
                Debug.LogError($"[ParametersMap] Ошибка при сериализации в JSON: {e.Message}");
            }
        }

        /// <summary>
        /// Загружает состояние из JSON-строки, находящейся в буфере обмена, перезаписывая текущие параметры.
        /// </summary>
        [Button("From Clipboard"), HorizontalGroup("Импорт-Экспорт/clipboard"), BoxGroup("Импорт-Экспорт")]
        [Tooltip("Система импорта и экспорта данных через буфер памяти для быстрой смены схем параметров")]
        public void LoadFromJson()
        {
            string json = EditorGUIUtility.systemCopyBuffer;
            if (string.IsNullOrWhiteSpace(json))
            {
                Debug.LogWarning("[ParametersMap] Буфер обмена пуст.");
                return;
            }

            try
            {
                var dto = JsonUtility.FromJson<ParametersMapDTO>(json);
                if (dto == null)
                {
                    Debug.LogError("[ParametersMap] Не удалось десериализовать JSON.");
                    return;
                }

                // Восстанавливаем baseParams
                baseParams = dto.baseParams ?? new string[0];

                // Восстанавливаем mappedParams
                mappedParams = dto.mappedParams?.Select(dtoPreset =>
                {
                    var links = dtoPreset.parents?.Select(dtoLink =>
                    {
                        var link = new MappedParameterLink
                        {
                            parent = dtoLink.parent,
                            cost = dtoLink.cost
                        };
                        return link;
                    }).ToArray() ?? new MappedParameterLink[0];

                    var preset = new MappedParameterPreset
                    {
                        name = dtoPreset.name,
                        costLogic = dtoPreset.costLogic,
                        parents = links
                    };
                    return preset;
                }).ToArray() ?? new MappedParameterPreset[0];

                // Перезапускаем валидацию
                validated = false;
                OnValidate();
                CheckErrors();
                EditorUtility.SetDirty(this);
                Debug.Log($"[ParametersMap] Загружено {mappedParams.Length} производных параметров.");
            }
            catch (System.Exception e)
            {
                Debug.LogError($"[ParametersMap] Ошибка десериализации: {e}");
            }
        }
    }
}

#endif