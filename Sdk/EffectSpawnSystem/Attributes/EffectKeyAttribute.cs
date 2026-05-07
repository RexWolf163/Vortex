using System;
using UnityEngine;

namespace Vortex.Sdk.EffectSpawnSystem.Attributes
{
    /// <summary>
    /// Inspector-атрибут для строкового поля, хранящего ключ эффекта из зарегистрированного
    /// <c>EffectsCatalog</c>. Drawer рисует popup со всеми доступными ключами.
    ///
    /// В рантайме поле — обычная <c>string</c>; преобразование в спаун — через
    /// <c>EffectSpawn.Spawn(target, key)</c>.
    /// </summary>
    /// <example>
    /// <code>
    /// [EffectKey] [SerializeField] private string explosion;
    ///
    /// private void OnHit() => EffectSpawn.Spawn(transform, explosion);
    /// </code>
    /// </example>
    [AttributeUsage(AttributeTargets.Field | AttributeTargets.Property)]
    public class EffectKeyAttribute : PropertyAttribute
    {
    }
}
