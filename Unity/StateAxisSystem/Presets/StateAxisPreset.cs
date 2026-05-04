using System;
using System.Collections.Generic;
using UnityEngine;

namespace Vortex.Unity.StateAxisSystem.Presets
{
    /// <summary>
    /// Парный ассет генерируемого StateAxis-класса.
    /// Хранит имя оси, целевой namespace, упорядоченный список ключей и путь последнего
    /// сгенерированного <c>.cs</c>-файла (для удаления при ренейме / удалении пресета).
    ///
    /// Workflow:
    /// 1. Создать ассет через <c>Create → Vortex → StateAxis Preset</c>.
    /// 2. Заполнить Axis Name, Target Namespace, Keys.
    /// 3. Нажать Save в инспекторе → генерируется <c>{папка_ассета}/{AxisName}.cs</c>.
    ///    При смене Axis Name старый <c>.cs</c> удаляется.
    /// 4. Load — обратное действие: считать ключи из существующего сгенерированного класса
    ///    и записать в SO (для синхронизации после слияния веток).
    /// </summary>
    [CreateAssetMenu(fileName = "StateAxisPreset", menuName = "Vortex/StateAxis Preset")]
    public class StateAxisPreset : ScriptableObject
    {
        [SerializeField, Tooltip("Имя сгенерированного класса. PascalCase, валидный C#-идентификатор, без точек.")]
        private string axisName;

        [SerializeField, Tooltip("Namespace сгенерированного класса.")]
        private string targetNamespace;

        [SerializeField, Tooltip("Упорядоченный список ключей оси. Каждый ключ — валидный C#-идентификатор без точек.")]
        private string[] keys = Array.Empty<string>();

        [SerializeField, HideInInspector]
        private string lastGeneratedPath;

        public string AxisName => axisName;
        public string TargetNamespace => targetNamespace;
        public IReadOnlyList<string> Keys => keys;
        public string LastGeneratedPath => lastGeneratedPath;

#if UNITY_EDITOR
        // Editor-mutators: используются StateAxisPresetEditor / Postprocessor.
        // В рантайме изменение пресета смысла не имеет.
        internal void EditorSetAxisName(string n) => axisName = n;
        internal void EditorSetTargetNamespace(string ns) => targetNamespace = ns;
        internal void EditorSetKeys(string[] k) => keys = k ?? Array.Empty<string>();
        internal void EditorSetLastGeneratedPath(string p) => lastGeneratedPath = p;
#endif
    }
}
