using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using Naninovel;
using Sirenix.OdinInspector;
using UnityEngine;
using Vortex.NaniExtensions.CustomVariablesDashboard;
using Vortex.Sdk.Quests.QuestsLogics;
using ExpressionEvaluator = Naninovel.ExpressionEvaluator;
using UniTask = Cysharp.Threading.Tasks.UniTask;
#if UNITY_EDITOR
using UnityEditor;
using NExpr = Naninovel.Expression;
#endif

namespace Vortex.NaniExtensions.QuestSystem.Logics
{
    /// <summary>
    /// Сравнение по выражению Naninovel — аналог <see cref="NaniVariableValueComparation"/> для произвольной формулы:
    /// переменные, операторы сравнения, логические <c>&amp;&amp;</c>, <c>||</c>, <c>!</c>, скобки и функции Naninovel
    /// (<c>Day&gt;=3 || (Flag &amp;&amp; Money&gt;100)</c>). Выражение вычисляется вычислителем Naninovel — тем же, что
    /// <c>@if</c> и <c>if:</c> в сценариях.
    ///
    /// Результат обязан быть логическим. FALSE — квест провален! Ошибка разбора или вычисления, нелогический результат —
    /// ошибка в консоль и FALSE.
    ///
    /// Кнопка «Проверить формулу» в инспекторе проверяет синтаксис, существование функций, логический тип результата и
    /// объявление переменных в конфигурации Naninovel — без запуска игры.
    /// </summary>
    [Serializable]
    public class NaniExpressionComparation : QuestLogic, INaniExpressionReference
    {
        [SerializeField, HideLabel, OnValueChanged("ResetValidation")]
        [InfoBox("Выражение Naninovel: переменные, ==, !=, >, >=, <, <=, &&, ||, !, скобки, функции. " +
                 "Пример: Day>=3 || (Flag && Money>100)")]
        [InfoBox("$_validationMessage", InfoMessageType.Error, "@_validationState == 2")]
        [InfoBox("$_validationMessage", InfoMessageType.Warning, "@_validationState == 3")]
        [InfoBox("$_validationMessage", InfoMessageType.Info, "@_validationState == 1")]
        private string expression;

        string INaniExpressionReference.Expression => expression;

        public override async Cysharp.Threading.Tasks.UniTask<bool> Run(CancellationToken token)
        {
            if (!Engine.Initialized)
            {
                Debug.LogError(
                    $"[{nameof(NaniExpressionComparation)}] Naninovel не инициализирован — «{expression}» не вычислено");
                return false;
            }

            var value = ExpressionEvaluator.Evaluate(expression, LogError)?.GetValue();
            await UniTask.Yield();

            if (value is bool result)
                return result;
            if (value != null)
                LogError($"Результат «{expression}» не логический: {value}");
            return false;
        }

        private static void LogError(string message) => Debug.LogError($"[{nameof(NaniExpressionComparation)}] {message}");

#if UNITY_EDITOR
        /// <summary>Строковые литералы — вырезаются перед поиском имён переменных</summary>
        private static readonly Regex StringLiteral = new(@"""(?:\\.|[^""\\])*""|'(?:\\.|[^'\\])*'");

        /// <summary>Идентификатор, за которым не идёт «(» (вызов функции)</summary>
        private static readonly Regex Identifier = new(@"(?<![\w.])([A-Za-z_]\w*)(?!\w)(?!\s*\()");

        /// <summary>Результат последней проверки: 0 — не проверялось, 1 — корректно, 2 — ошибка, 3 — предупреждение</summary>
        private int _validationState;

        private string _validationMessage;

        protected override string GetEditorLabel() => $"Nani expression: {expression ?? "[NOT SET]"}";

        private void ResetValidation() => _validationState = 0;

        [Button("Проверить формулу")]
        private void Validate()
        {
            _validationState = TryValidate(expression, out _validationMessage, out var warning) ? warning ? 3 : 1 : 2;
        }

        /// <summary>
        /// Проверка без запуска игры: синтаксис (парсер Naninovel), существование функций, логический тип результата
        /// (пробное вычисление на начальных значениях переменных из конфига), объявление переменных в конфиге.
        /// </summary>
        private static bool TryValidate(string text, out string message, out bool warning)
        {
            warning = false;
            if (string.IsNullOrWhiteSpace(text))
            {
                message = "Выражение пустое.";
                return false;
            }

            if (Compiler.Syntax == null)
                Compiler.Initialize();

            var diagnostics = new List<NExpr.ParseDiagnostic>();
            var parser = new NExpr.Parser(new() { Syntax = Compiler.Syntax, HandleDiagnostic = diagnostics.Add });
            if (!parser.TryParse(text, out var parsed))
            {
                message = $"Синтаксическая ошибка: {diagnostics.FirstOrDefault()}";
                return false;
            }

            var declared = GetDeclaredVariables();
            var functions = ExpressionFunctions.Resolve().ToLookup(f => f.Id, StringComparer.OrdinalIgnoreCase);
            var evaluator = new NExpr.Evaluator(new()
            {
                ResolveVariable = name => declared.TryGetValue(name, out var initial)
                    ? Sample(initial)
                    : new NExpr.String(string.Empty),
                ResolveFunction = (name, _) => functions.Contains(name)
                    ? SampleOf(functions[name].First().Method.ReturnType)
                    : throw new NExpr.Error($"Функция «{name}» не найдена.")
            });

            try
            {
                var value = evaluator.Evaluate(parsed).GetValue();
                if (value is not bool)
                {
                    message =
                        $"Результат не логический ({value?.GetType().Name ?? "null"}): нужно сравнение или логическое выражение.";
                    return false;
                }
            }
            catch (NExpr.Error e)
            {
                message = $"Ошибка вычисления: {e.Message}";
                return false;
            }

            var undeclared = Identifier.Matches(StringLiteral.Replace(text, string.Empty))
                .Select(m => m.Groups[1].Value)
                .Where(n => !n.Equals("true", StringComparison.OrdinalIgnoreCase)
                            && !n.Equals("false", StringComparison.OrdinalIgnoreCase)
                            && !n.StartsWith(Compiler.ScriptConstantPrefix, StringComparison.OrdinalIgnoreCase)
                            && !declared.ContainsKey(n))
                .Distinct()
                .ToList();
            if (undeclared.Count > 0)
            {
                warning = true;
                message = $"Формула корректна, но не объявлены в Custom Variables: {string.Join(", ", undeclared)}. " +
                          "Опечатка или переменная задаётся только через @set.";
                return true;
            }

            message = "Формула корректна.";
            return true;
        }

        /// <summary>Объявленные переменные: имя → начальное значение из конфигурации Naninovel</summary>
        private static Dictionary<string, string> GetDeclaredVariables()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var guid in AssetDatabase.FindAssets("t:CustomVariablesConfiguration"))
            {
                var config =
                    AssetDatabase.LoadAssetAtPath<CustomVariablesConfiguration>(AssetDatabase.GUIDToAssetPath(guid));
                if (config == null)
                    continue;
                foreach (var predefine in config.PredefinedVariables)
                    if (!string.IsNullOrEmpty(predefine.Name))
                        result.TryAdd(predefine.Name, predefine.Expression);
            }

            return result;
        }

        /// <summary>Пробное значение переменной для проверки типов: по начальному значению из конфига</summary>
        private static NExpr.IOperand Sample(string initial)
        {
            var text = initial?.Trim() ?? string.Empty;
            if (bool.TryParse(text, out var flag))
                return new NExpr.Boolean(flag);
            if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
                return new NExpr.Numeric(number);
            return new NExpr.String(text.Trim('"'));
        }

        /// <summary>Пробный результат функции по типу её возвращаемого значения</summary>
        private static NExpr.IOperand SampleOf(Type type)
        {
            if (type == typeof(bool))
                return new NExpr.Boolean(false);
            if (type == typeof(string))
                return new NExpr.String(string.Empty);
            return new NExpr.Numeric(0);
        }
#endif
    }
}