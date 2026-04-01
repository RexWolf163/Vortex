using Sirenix.OdinInspector;
using UnityEngine;

namespace Vortex.Unity.Extensions.Abstractions
{
    public abstract class SoData : ScriptableObject
    {
#if UNITY_EDITOR
        [TabGroup("Debug"), Button,
         InfoBox(
             "Эта кнопка выводит в консоль список полей попадающих под правила для копирования данных в модель SystemModel")]
        private void TestFields() => this.PrintFields();
#endif
    }
}