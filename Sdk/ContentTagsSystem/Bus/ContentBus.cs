using System.Collections.Generic;
using UnityEngine;
using Vortex.Sdk.ContentTagsSystem.Model;
using Vortex.Sdk.ContentTagsSystem.Settings;

namespace Vortex.Sdk.ContentTagsSystem.Bus
{
    /// <summary>
    /// Единственная точка ответа на вопрос «включён ли этот тег в текущем издании». API синхронное:
    /// набор активных тегов читается из ассета при первом обращении и дальше не меняется — ждать нечего,
    /// подписываться не на что.
    ///
    /// Сломанная конфигурация (нет ассета, пустое или неизвестное активное издание) даёт пустой набор:
    /// всё размеченное выключено. Это видно сразу и не даёт лишнему уехать в сборку издания.
    /// </summary>
    public static class ContentBus
    {
        private const string LogTag = "[ContentTags]";

        /// <summary>Теги активного издания.</summary>
        private static readonly HashSet<string> Active = new();

        /// <summary>Все объявленные теги — чтобы отличать «выключен» от «не существует».</summary>
        private static readonly HashSet<string> Declared = new();

        /// <summary>Ключи, о которых уже сообщили: запрос идёт из каждого включения объекта.</summary>
        private static readonly HashSet<string> Reported = new();

        private static bool _built;

        private static bool _hasSettings;

        private static string _activeBundle = string.Empty;

        /// <summary>Ключ активного издания. Пусто — конфигурация не прочитана или сломана.</summary>
        public static string ActiveBundle
        {
            get
            {
                Build();
                return _activeBundle;
            }
        }

        /// <summary>Активные теги. Для инструментов и диагностики.</summary>
        public static IReadOnlyCollection<string> ActiveTags
        {
            get
            {
                Build();
                return Active;
            }
        }

        /// <summary>Тег включён в текущем издании. Необъявленный тег — <c>false</c> и Error один раз на ключ.</summary>
        public static bool IsActive(string tag)
        {
            Build();

            if (Declared.Contains(tag))
                return Active.Contains(tag);

            // При сломанной конфигурации объявленных тегов нет вовсе — об этом уже сказано при чтении.
            if (_hasSettings && Reported.Add(tag ?? string.Empty))
                Debug.LogError($"{LogTag} Запрошен необъявленный тег «{tag}» — считается выключенным.");

            return false;
        }

        /// <summary>
        /// Сопоставление списка тегов с активным набором. Пустой список — <c>false</c> при любом режиме:
        /// трактовать незаполненную настройку как выполненное условие значило бы показывать контент везде.
        /// </summary>
        public static bool Matches(IReadOnlyList<string> tags, TagMatchMode mode)
        {
            if (tags == null || tags.Count == 0)
                return false;

            var active = 0;
            foreach (var tag in tags)
                if (IsActive(tag))
                    active++;

            return mode switch
            {
                TagMatchMode.All => active == tags.Count,
                TagMatchMode.Any => active > 0,
                TagMatchMode.None => active == 0,
                TagMatchMode.Single => active == 1,
                _ => false
            };
        }

        // Статика переживает выход из Play Mode при отключённом Domain Reload — состояние сбрасывается явно.
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetState()
        {
            Active.Clear();
            Declared.Clear();
            Reported.Clear();
            _activeBundle = string.Empty;
            _hasSettings = false;
            _built = false;
        }

        private static void Build()
        {
            if (_built)
                return;
            _built = true;

            var assets = Resources.LoadAll<ContentSettings>(string.Empty);
            if (assets == null || assets.Length == 0)
            {
                Debug.LogError($"{LogTag} ContentSettings не найден в Resources — всё размеченное выключено.");
                return;
            }

            if (assets.Length > 1)
                Debug.LogError($"{LogTag} В Resources несколько ContentSettings ({assets.Length}) — " +
                               $"взят «{assets[0].name}».");

            var settings = assets[0];
            _hasSettings = true;

            foreach (var problem in settings.Validate())
                Debug.LogError($"{LogTag} {problem}", settings);

            foreach (var tag in settings.Tags)
                if (tag != null && !string.IsNullOrEmpty(tag.Key))
                    Declared.Add(tag.Key);

            var bundle = FindBundle(settings);
            if (bundle == null)
                return;

            _activeBundle = bundle.Key;
            foreach (var tag in bundle.Tags)
                if (Declared.Contains(tag))
                    Active.Add(tag);

            Debug.Log($"{LogTag} Издание «{_activeBundle}», включённых тегов: {Active.Count}.");
        }

        private static ContentBundle FindBundle(ContentSettings settings)
        {
            if (string.IsNullOrEmpty(settings.ActiveBundle))
                return null;

            foreach (var bundle in settings.Bundles)
                if (bundle != null && bundle.Key == settings.ActiveBundle)
                    return bundle;

            return null;
        }

#if UNITY_EDITOR
        /// <summary>Перечитать конфигурацию. Для окна ревизии после правки ассета.</summary>
        internal static void EditorRebuild()
        {
            ResetState();
            Build();
        }
#endif
    }
}
