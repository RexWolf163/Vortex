#if UNITY_EDITOR
using System;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;

namespace Vortex.Unity.EditorTools.Elements
{
    internal static class PropertyClipboard
    {
        internal static string Serialize(SerializedProperty property)
        {
            var json = new StringBuilder();
            json.Append("{");
            json.Append($"\"propertyType\":\"{property.propertyType}\",");
            json.Append($"\"path\":\"{property.propertyPath}\",");

            switch (property.propertyType)
            {
                case SerializedPropertyType.Integer:
                    json.Append($"\"value\":{property.intValue}");
                    break;
                case SerializedPropertyType.Boolean:
                    json.Append($"\"value\":{(property.boolValue ? "true" : "false")}");
                    break;
                case SerializedPropertyType.Float:
                    json.Append($"\"value\":{property.floatValue.ToString(System.Globalization.CultureInfo.InvariantCulture)}");
                    break;
                case SerializedPropertyType.String:
                    json.Append($"\"value\":\"{EscapeJson(property.stringValue ?? "")}\"");
                    break;
                case SerializedPropertyType.ObjectReference:
                    var instanceId = property.objectReferenceValue != null
                        ? property.objectReferenceValue.GetInstanceID()
                        : 0;
                    json.Append($"\"value\":{instanceId}");
                    break;
                case SerializedPropertyType.Enum:
                    json.Append($"\"value\":{property.enumValueIndex}");
                    break;
                default:
                    var so = property.serializedObject;
                    json.Append($"\"value\":\"{EscapeJson(EditorJsonUtility.ToJson(so.targetObject))}\"");
                    break;
            }

            json.Append("}");
            return json.ToString();
        }

        internal static void Deserialize(SerializedProperty property, string data)
        {
            if (string.IsNullOrEmpty(data)) return;
            if (!data.StartsWith("{") || !data.Contains("\"propertyType\"")) return;

            try
            {
                switch (property.propertyType)
                {
                    case SerializedPropertyType.Integer:
                        var intMatch = Regex.Match(data, "\"value\":([-\\d]+)");
                        if (intMatch.Success && int.TryParse(intMatch.Groups[1].Value, out var iv))
                            property.intValue = iv;
                        break;
                    case SerializedPropertyType.Boolean:
                        property.boolValue = data.Contains("\"value\":true");
                        break;
                    case SerializedPropertyType.Float:
                        var floatMatch = Regex.Match(data, "\"value\":([-\\d.eE+]+)");
                        if (floatMatch.Success && float.TryParse(floatMatch.Groups[1].Value,
                                System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out var fv))
                            property.floatValue = fv;
                        break;
                    case SerializedPropertyType.String:
                        var strMatch = Regex.Match(data, "\"value\":\"(.*?)\"\\s*}");
                        if (strMatch.Success)
                            property.stringValue = UnescapeJson(strMatch.Groups[1].Value);
                        break;
                    case SerializedPropertyType.Enum:
                        var enumMatch = Regex.Match(data, "\"value\":([-\\d]+)");
                        if (enumMatch.Success && int.TryParse(enumMatch.Groups[1].Value, out var ei))
                            property.enumValueIndex = ei;
                        break;
                    case SerializedPropertyType.ObjectReference:
                        var refMatch = Regex.Match(data, "\"value\":([-\\d]+)");
                        if (refMatch.Success && int.TryParse(refMatch.Groups[1].Value, out var id) && id != 0)
                            property.objectReferenceValue = EditorUtility.InstanceIDToObject(id);
                        break;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[PropertyClipboard] Paste failed: {e.Message}");
            }
        }

        private static string EscapeJson(string s) =>
            s.Replace("\\", "\\\\").Replace("\"", "\\\"").Replace("\n", "\\n").Replace("\r", "\\r");

        private static string UnescapeJson(string s) =>
            s.Replace("\\n", "\n").Replace("\\r", "\r").Replace("\\\"", "\"").Replace("\\\\", "\\");
    }
}
#endif
