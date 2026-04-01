#if UNITY_EDITOR
using UnityEditor;
using UnityEngine.UIElements;

public class CoreAssetsPreferences : SettingsProvider
{
    private const string PrefsKey = "CoreAssetsAutoCreate_Enabled";
    private static bool isEnabled;

    [SettingsProvider]
    public static SettingsProvider CreateMyCustomSettingsProvider()
    {
        var provider = new CoreAssetsPreferences("Vortex/Editor", SettingsScope.User)
        {
            keywords = new[] { "vortex" }
        };
        return provider;
    }

    public static bool GetCoreAssetAutoCreationMode() => EditorPrefs.GetBool(PrefsKey, true);

    private CoreAssetsPreferences(string path, SettingsScope scopes)
        : base(path, scopes)
    {
    }

    public override void OnActivate(string searchContext, VisualElement rootElement)
    {
        isEnabled = GetCoreAssetAutoCreationMode();
    }

    public override void OnGUI(string searchContext)
    {
        EditorGUILayout.BeginVertical();
        EditorGUILayout.LabelField(
            "Автоматический контроль ассетов для создания недостающих. Отключите, если ассетов слишком много и система начинает зависать.",
            EditorStyles.miniLabel
        );

        EditorGUI.BeginChangeCheck();

        isEnabled = EditorGUILayout.Toggle("CoreAssets Auto Creation", isEnabled);

        if (EditorGUI.EndChangeCheck())
        {
            EditorPrefs.SetBool(PrefsKey, isEnabled);
        }

        EditorGUILayout.EndVertical();
    }
}
#endif