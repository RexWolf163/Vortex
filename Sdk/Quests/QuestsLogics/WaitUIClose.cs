using System;
using System.Linq;
using System.Threading;
using Cysharp.Threading.Tasks;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.Core.DatabaseSystem.Bus;
using Vortex.Core.UIProviderSystem.Bus;
using Vortex.Core.UIProviderSystem.Model;
using Vortex.Unity.DatabaseSystem.Attributes;

namespace Vortex.Sdk.Quests.QuestsLogics
{
    /// <summary>
    /// Ждёт закрытия интерфейсов, потом пропускает квест дальше. Пара к <see cref="CallUIOpen"/>. Режим
    /// переключается галочкой <see cref="whitelist"/>:
    /// <list type="bullet">
    /// <item><b>blacklist</b> (whitelist=false): ждём, пока закроются ВСЕ перечисленные UI (любой тип —
    /// статус читаем прямо с модели через Database).</item>
    /// <item><b>whitelist</b> (whitelist=true): ждём, пока закроются все Common-интерфейсы, КРОМЕ
    /// перечисленных — перечисленные разрешено держать открытыми (напр. перманентная карта).</item>
    /// </list>
    /// </summary>
    [Serializable]
    public class WaitUIClose : QuestLogic
    {
        [InfoBox("blacklist: ждать закрытия ЭТИХ. whitelist: ждать закрытия всех Common, КРОМЕ этих.")]
        [SerializeField, DbRecord(typeof(UserInterfaceData))]
        private string[] uis = Array.Empty<string>();

        [SerializeField, Tooltip("Вкл — whitelist (ждать закрытия всех Common, кроме списка). " +
                                 "Выкл — blacklist (ждать закрытия перечисленных).")]
        private bool whitelist;

        public override async UniTask<bool> Run(CancellationToken token)
        {
            try
            {
                await UniTask.WaitUntil(() => !StillOpen(), cancellationToken: token);
            }
            catch (OperationCanceledException)
            {
                // Отмена (новая игра/загрузка/прерывание) — квест не валим.
            }

            return true;
        }

        private bool StillOpen() => whitelist ? AnyNonWhitelistedOpen() : AnyListedOpen();

        // blacklist: открыт ли хоть один из списка (любой тип — статус прямо с модели, тот же инстанс, что в шине).
        private bool AnyListedOpen()
        {
            foreach (var id in uis)
                if (!string.IsNullOrWhiteSpace(id) && (Database.GetRecord<UserInterfaceData>(id)?.IsOpen ?? false))
                    return true;
            return false;
        }

        // whitelist: открыт ли хоть один Common НЕ из списка (перечисленные игнорируем — им можно быть открытыми).
        private bool AnyNonWhitelistedOpen()
        {
            foreach (var ui in UIProvider.GetOpenedUIs())
                if (!uis.Contains(ui.GuidPreset))
                    return true;
            return false;
        }

#if UNITY_EDITOR
        protected override string GetEditorLabel()
        {
            var count = uis?.Length ?? 0;
            return whitelist
                ? $"Wait UI closed (all except {count})"
                : $"Wait UI closed (these {count})";
        }
#endif
    }
}
