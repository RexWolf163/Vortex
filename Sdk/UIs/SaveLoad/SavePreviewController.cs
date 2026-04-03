using UnityEngine;
using Vortex.Core.Extensions.LogicExtensions;

namespace Vortex.Sdk.UIs.SaveLoad
{
    public static class SavePreviewController
    {
        private const string SavePreviewKey = "SavePreview";

        /// <summary>
        /// Получение превью для сейва
        /// </summary>
        /// <param name="guid"></param>
        /// <returns></returns>
        public static Texture2D GetPreview(string guid)
        {
            if (!PlayerPrefs.HasKey(GetSaveKey(guid)))
                return null;
            var data = PlayerPrefs.GetString(GetSaveKey(guid));
            var texture = new Texture2D(1, 1);
            return !texture.Base64ToTexture(data) ? null : texture;
        }

        /// <summary>
        /// Ключ сохранения картинки-превью для данного сохранения
        /// </summary>
        /// <returns></returns>
        public static string GetSaveKey(string guid) => $"{SavePreviewKey}_{guid}";

        /// <summary>
        /// Сохранения превью для сейва
        /// </summary>
        /// <param name="preview"></param>
        public static void SavePreview(Texture2D preview, string guid)
        {
            var data = preview.TextureToBase64(TextureEncodingRules.JPEGMedium);
            PlayerPrefs.SetString(GetSaveKey(guid), data);
        }
    }
}