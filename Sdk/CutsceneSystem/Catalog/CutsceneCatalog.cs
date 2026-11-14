using System.Collections.Generic;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Sdk.CutsceneSystem.Models;

namespace Vortex.Sdk.CutsceneSystem.Catalog
{
    /// <summary>
    /// Единый индекс катсцен проекта. Регистрируется в <c>CutsceneController</c> сам, при старте.
    ///
    /// Ключ катсцены — имя её ассета. Дубли имён дают предупреждение (в инспекторе каталога и в лог при построении
    /// индекса); по ключу отдаётся первая катсцена с таким именем
    /// </summary>
    [CreateAssetMenu(fileName = "CutsceneCatalog", menuName = "Vortex/Cutscenes/Cutscene Catalog")]
    public class CutsceneCatalog : ScriptableObject
    {
        [SerializeField, InfoBox("Ключ катсцены = имя ассета.")]
        [InfoBox("$" + nameof(DuplicatesMessage), InfoMessageType.Warning, nameof(HasDuplicates))]
        private CutsceneData[] cutscenes = new CutsceneData[0];

        private Dictionary<string, CutsceneData> _byKey;
        private string[] _keys;
        private string _duplicatesMessage;

        /// <summary>
        /// Все ключи каталога в порядке объявления
        /// </summary>
        public IReadOnlyList<string> Keys
        {
            get
            {
                EnsureBuilt();
                return _keys;
            }
        }

        /// <summary>
        /// Катсцена по ключу или <c>null</c>, если ключ не найден
        /// </summary>
        public CutsceneData Get(string key)
        {
            if (string.IsNullOrEmpty(key))
                return null;
            EnsureBuilt();
            return _byKey.TryGetValue(key, out var cutscene) ? cutscene : null;
        }

        private bool HasDuplicates
        {
            get
            {
                EnsureBuilt();
                return _duplicatesMessage != null;
            }
        }

        private string DuplicatesMessage => _duplicatesMessage;

        private void OnEnable() => Invalidate();
        private void OnValidate() => Invalidate();

        private void Invalidate()
        {
            _byKey = null;
            _keys = null;
            _duplicatesMessage = null;
        }

        private void EnsureBuilt()
        {
            if (_byKey != null)
                return;

            _byKey = new Dictionary<string, CutsceneData>(cutscenes.Length);
            var keys = new List<string>(cutscenes.Length);
            List<string> duplicates = null;
            foreach (var cutscene in cutscenes)
            {
                if (cutscene == null)
                    continue;
                var key = cutscene.name;
                if (_byKey.ContainsKey(key))
                {
                    duplicates ??= new List<string>();
                    if (!duplicates.Contains(key))
                        duplicates.Add(key);
                    continue;
                }

                _byKey.Add(key, cutscene);
                keys.Add(key);
            }

            _keys = keys.ToArray();
            if (duplicates == null)
                return;

            _duplicatesMessage =
                $"Дубли ключей: {string.Join(", ", duplicates)}. По ключу отдаётся первая катсцена с таким именем.";
            Debug.LogWarning($"[CutsceneCatalog] '{name}': {_duplicatesMessage}", this);
        }
    }
}
