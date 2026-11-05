using System;
using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;
using Vortex.Core.SaveSystem.Abstraction;
using Vortex.Core.SaveSystem.Bus;
using Vortex.Sdk.UIs.SaveLoad.Handlers;
using Vortex.Unity.SaveSystem.Presets;
using Object = UnityEngine.Object;

namespace Vortex.Sdk.UIs.SaveLoad
{
    /// <summary>
    /// Превью сейва — метаблок <see cref="MetaKey"/> сводки. На каждом запросе сохранения снимает экран
    /// (<see cref="CameraCaptureHandler"/>) и записывает снимок в метаданные запроса; места сохранения превью не
    /// снимают. Снимок — кадр момента запроса, даже если сохранение исполнится позже из очереди.
    ///
    /// Снимок требует конца кадра: точка сохранения обязана дождаться его перед <c>SaveController.Save</c> —
    /// событие запроса (<c>OnSaveRequest</c>) вызывается синхронно внутри <c>Save</c>.
    ///
    /// Старая форма — превью в имени сейва через <see cref="Divider"/> — устарела, но читается для сейвов,
    /// записанных до перехода на метаданные.
    /// </summary>
    public static class SavePreviewController
    {
        /// <summary>
        /// Ключ метаблока превью
        /// </summary>
        public const string MetaKey = SaveSettings.PreviewMetaKey;

        private const string Divider = "||";

        [RuntimeInitializeOnLoadMethod]
        private static void Init()
        {
            SaveController.OnSaveRequest -= AddPreview;
            SaveController.OnSaveRequest += AddPreview;
        }

        /// <summary>
        /// Снимок экрана в метаданные запроса сохранения. Нет активных слоёв захвата — сейв без превью.
        /// Метаблок выключен в <see cref="SaveSettings"/> — снимок не делается: драйвер всё равно его отбросит.
        /// </summary>
        private static void AddPreview()
        {
            if (!SaveSettings.IsMetaEnabled(MetaKey))
                return;

            var texture = CameraCaptureHandler.Capture();
            if (texture == null)
                return;

            try
            {
                var base64 = EncodePreview(texture);
                if (base64 != null)
                    SaveController.AddMeta(MetaKey, base64);
            }
            finally
            {
                Object.Destroy(texture);
            }
        }

        /// <summary>
        /// Кодирование превью в строку метаблока
        /// </summary>
        public static string EncodePreview(Texture2D preview) =>
            preview.TextureToBase64(TextureEncodingRules.JPEGMedium);

        /// <summary>
        /// Получение превью для сейва: из метаданных, для старых сейвов — из имени
        /// </summary>
        /// <param name="summary"></param>
        /// <returns></returns>
        public static Texture2D GetPreview(SaveSummary summary)
        {
            var base64 = summary.GetMeta(MetaKey) ?? GetLegacyPreview(summary);
            if (base64 == null)
                return null;
            var texture = new Texture2D(1, 1);
            if (texture.Base64ToTexture(base64))
                return texture;

            // Битое превью: текстура никому не отдана — уничтожить здесь, иначе утечка на каждой перестройке списка
            Object.Destroy(texture);
            return null;
        }

        /// <summary>
        /// Получение чистого имени сейва. У новых сейвов имя уже чистое; у старых превью отрезается.
        /// </summary>
        /// <param name="summary"></param>
        /// <returns></returns>
        public static string GetSavePureName(this SaveSummary summary)
        {
            var ar = summary.Name.Split(Divider);
            return ar[0];
        }

        /// <summary>
        /// Формирование имени сейва с заложенными данными превью
        /// </summary>
        /// <param name="preview"></param>
        /// <param name="name"></param>
        [Obsolete("Превью пишется в метаданные сводки (метаблок \"" + MetaKey + "\") автоматически на начале " +
                  "сохранения. Превью в имени читается только для старых сейвов.")]
        public static string GetSaveNameWithPreview(Texture2D preview, string name) =>
            $"{name.Replace(Divider, "")}{Divider}{EncodePreview(preview)}";

        /// <summary>
        /// Превью старой формы — base64 в имени после <see cref="Divider"/>; null, если его нет.
        /// </summary>
        private static string GetLegacyPreview(SaveSummary summary)
        {
            var ar = summary.Name.Split(Divider);
            return ar.Length == 2 ? ar[1] : null;
        }
    }
}
