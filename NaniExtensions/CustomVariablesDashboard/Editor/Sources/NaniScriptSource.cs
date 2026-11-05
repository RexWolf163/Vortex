using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Model;

namespace Vortex.NaniExtensions.CustomVariablesDashboard.Editor.Sources
{
    /// <summary>
    /// Источник связей «сценарии <c>.nani</c>». Эвристический построчный разбор (не компилятор Naninovel):
    /// <list type="bullet">
    /// <item><c>@set</c> — левая часть присваивания — запись, идентификаторы правой части — чтение;</item>
    /// <item>параметр <c>if:</c> любой команды, <c>@if</c>, <c>@elseif</c> — сравнения и чтения;</item>
    /// <item>команды в строках текста (<c>[set …]</c>) разбираются так же; комментарии и метки пропускаются.</item>
    /// </list>
    /// Переменная в выражении — идентификатор вне строкового литерала, за которым не идёт <c>(</c> (вызов функции), и не
    /// <c>true</c>/<c>false</c>.
    /// </summary>
    internal static class NaniScriptSource
    {
        private enum TokenKind
        {
            Identifier,
            Number,
            String,
            Operator,
            OpenParen,
            CloseParen,
            Comma
        }

        private readonly struct Token
        {
            public readonly TokenKind Kind;
            public readonly string Text;
            public readonly int Start;
            public readonly int End;

            public Token(TokenKind kind, string text, int start, int end)
            {
                Kind = kind;
                Text = text;
                Start = start;
                End = end;
            }
        }

        private static readonly Regex Assignment =
            new(@"^\s*([A-Za-z_][A-Za-z0-9_]*)\s*(\+\+|--|\+=|-=|\*=|/=|=(?!=))\s*(.*)$", RegexOptions.Singleline);

        /// <summary>Операторы сравнения в условии; одиночный <c>=</c> в условии Naninovel — тоже равенство</summary>
        private static readonly HashSet<string> Comparisons = new(StringComparer.Ordinal)
            { "==", "=", "!=", ">", ">=", "<", "<=" };

        private static readonly HashSet<string> Arithmetic = new(StringComparer.Ordinal) { "+", "-", "*", "/", "%" };

        public static void Collect(string path, string text, List<VariableLink> links)
        {
            var lines = text.Split('\n');
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r').TrimStart();
                if (line.Length == 0 || line[0] == ';' || line[0] == '#')
                    continue;

                var location = new NaniLocation(path, i + 1);
                if (line[0] == '@')
                    ProcessCommand(line.Substring(1), location, links);
                else
                    foreach (var inline in GetInlineCommands(line))
                        ProcessCommand(inline, location, links);
            }
        }

        /// <summary>Команды, встроенные в строку текста: содержимое неэкранированных <c>[…]</c></summary>
        private static IEnumerable<string> GetInlineCommands(string line)
        {
            for (var i = 0; i < line.Length; i++)
            {
                if (line[i] != '[' || (i > 0 && line[i - 1] == '\\'))
                    continue;
                var end = line.IndexOf(']', i + 1);
                if (end < 0)
                    yield break;
                yield return line.Substring(i + 1, end - i - 1);
                i = end;
            }
        }

        private static void ProcessCommand(string body, NaniLocation location, List<VariableLink> links)
        {
            var parameters = SplitParameters(body);
            if (parameters.Count == 0)
                return;

            var command = parameters[0];
            string nameless = null;
            for (var i = 1; i < parameters.Count; i++)
            {
                var (name, value) = SplitNamed(parameters[i]);
                if (name == null)
                    nameless ??= value;
                else if (name.Equals("if", StringComparison.OrdinalIgnoreCase))
                    ProcessCondition(value, location, links);
            }

            if (nameless == null)
                return;

            if (command.Equals("set", StringComparison.OrdinalIgnoreCase))
                foreach (var assignment in nameless.Split(';'))
                    ProcessAssignment(assignment, location, links);
            else if (command.Equals("if", StringComparison.OrdinalIgnoreCase)
                     || command.Equals("elseif", StringComparison.OrdinalIgnoreCase))
                ProcessCondition(nameless, location, links);
        }

        /// <summary>
        /// Разбиение команды по пробелам вне кавычек и вне фигурных скобок (<c>if:{a=5 &amp;&amp; b&gt;=70}</c> — один
        /// параметр); первый элемент — имя команды
        /// </summary>
        private static List<string> SplitParameters(string body)
        {
            var result = new List<string>();
            var start = -1;
            var quote = '\0';
            var braces = 0;
            for (var i = 0; i <= body.Length; i++)
            {
                var c = i < body.Length ? body[i] : ' ';
                if (quote != '\0')
                {
                    if (c == quote && body[i - 1] != '\\')
                        quote = '\0';
                    continue;
                }

                if (c is '"' or '{' or '}')
                {
                    if (c == '"')
                        quote = c;
                    else
                        braces = Math.Max(0, braces + (c == '{' ? 1 : -1));
                    if (start < 0)
                        start = i;
                    continue;
                }

                if (char.IsWhiteSpace(c) && braces > 0 && i < body.Length)
                    continue;

                if (char.IsWhiteSpace(c))
                {
                    if (start >= 0)
                        result.Add(body.Substring(start, i - start));
                    start = -1;
                }
                else if (start < 0)
                    start = i;
            }

            return result;
        }

        /// <summary>Именованный параметр <c>имя:значение</c> → (имя, значение без кавычек); безымянный → (null, значение)</summary>
        private static (string name, string value) SplitNamed(string parameter)
        {
            var colon = parameter.IndexOf(':');
            var quote = parameter.IndexOf('"');
            if (colon > 0 && (quote < 0 || colon < quote) && IsIdentifier(parameter.Substring(0, colon)))
                return (parameter.Substring(0, colon), Unquote(parameter.Substring(colon + 1)));
            return (null, Unquote(parameter));
        }

        private static string Unquote(string value) =>
            value.Length >= 2 && value[0] == '"' && value[^1] == '"' ? value.Substring(1, value.Length - 2) : value;

        private static bool IsIdentifier(string text)
        {
            if (text.Length == 0 || !(char.IsLetter(text[0]) || text[0] == '_'))
                return false;
            foreach (var c in text)
                if (!(char.IsLetterOrDigit(c) || c == '_'))
                    return false;
            return true;
        }

        private static void ProcessAssignment(string assignment, NaniLocation location, List<VariableLink> links)
        {
            if (string.IsNullOrWhiteSpace(assignment))
                return;

            var raw = assignment.Trim();
            var match = Assignment.Match(assignment);
            if (!match.Success)
            {
                AddOther(raw, location, links);
                return;
            }

            var name = match.Groups[1].Value;
            var op = match.Groups[2].Value;
            var right = match.Groups[3].Value.Trim();
            links.Add(new VariableLink(name, LinkSource.Nani, NaniVariableOperation.Write, op, right, raw, location));

            var tokens = Tokenize(right);
            for (var i = 0; i < tokens.Count; i++)
                if (IsVariable(tokens[i], tokens, i))
                    AddRead(tokens[i].Text, raw, location, links);
        }

        /// <summary>
        /// Условие: для каждой переменной — сравнение (оператор и другая сторона), одиночный флаг — сравнение
        /// <c>== true</c> (<c>!флаг</c> — <c>== false</c>), участие в арифметике или аргумент функции — чтение.
        /// </summary>
        private static void ProcessCondition(string expression, NaniLocation location, List<VariableLink> links)
        {
            if (string.IsNullOrWhiteSpace(expression))
                return;

            var raw = expression.Trim();
            var tokens = Tokenize(expression);
            var frames = new Stack<bool>(); // true — скобки вызова функции
            for (var i = 0; i < tokens.Count; i++)
            {
                var token = tokens[i];
                switch (token.Kind)
                {
                    case TokenKind.OpenParen:
                        frames.Push(i > 0 && tokens[i - 1].Kind == TokenKind.Identifier);
                        continue;
                    case TokenKind.CloseParen:
                        if (frames.Count > 0)
                            frames.Pop();
                        continue;
                }

                if (!IsVariable(token, tokens, i))
                    continue;

                if (frames.Contains(true))
                {
                    AddRead(token.Text, raw, location, links);
                    continue;
                }

                var next = i + 1 < tokens.Count ? tokens[i + 1] : default;
                var prev = i > 0 ? tokens[i - 1] : default;

                if (next.Kind == TokenKind.Operator && Comparisons.Contains(next.Text))
                    links.Add(new VariableLink(token.Text, LinkSource.Nani, NaniVariableOperation.Compare, next.Text,
                        OperandAfter(expression, tokens, i + 2), raw, location));
                else if (i > 0 && prev.Kind == TokenKind.Operator && Comparisons.Contains(prev.Text))
                    links.Add(new VariableLink(token.Text, LinkSource.Nani, NaniVariableOperation.Compare,
                        Flip(prev.Text), OperandBefore(expression, tokens, i - 2), raw, location));
                else if ((next.Kind == TokenKind.Operator && Arithmetic.Contains(next.Text))
                         || (i > 0 && prev.Kind == TokenKind.Operator && Arithmetic.Contains(prev.Text)))
                    AddRead(token.Text, raw, location, links);
                else
                    links.Add(new VariableLink(token.Text, LinkSource.Nani, NaniVariableOperation.Compare, "==",
                        i > 0 && prev.Kind == TokenKind.Operator && prev.Text == "!" ? "false" : "true", raw, location));
            }
        }

        private static bool IsVariable(Token token, List<Token> tokens, int index) =>
            token.Kind == TokenKind.Identifier
            && !(index + 1 < tokens.Count && tokens[index + 1].Kind == TokenKind.OpenParen)
            && !token.Text.Equals("true", StringComparison.OrdinalIgnoreCase)
            && !token.Text.Equals("false", StringComparison.OrdinalIgnoreCase);

        /// <summary>Другая сторона сравнения справа: токены до <c>&amp;&amp;</c>, <c>||</c>, <c>)</c>, <c>,</c> или конца</summary>
        private static string OperandAfter(string expression, List<Token> tokens, int from)
        {
            var depth = 0;
            var to = from - 1;
            for (var i = from; i < tokens.Count; i++)
            {
                var t = tokens[i];
                if (t.Kind == TokenKind.OpenParen)
                    depth++;
                else if (t.Kind == TokenKind.CloseParen && depth-- == 0)
                    break;
                else if (depth == 0 && (t.Kind == TokenKind.Comma || t.Text is "&&" or "||"))
                    break;
                to = i;
            }

            return to < from ? string.Empty : expression.Substring(tokens[from].Start, tokens[to].End - tokens[from].Start).Trim();
        }

        /// <summary>Другая сторона сравнения слева: токены назад до <c>&amp;&amp;</c>, <c>||</c>, <c>(</c>, <c>,</c>, <c>!</c> или начала</summary>
        private static string OperandBefore(string expression, List<Token> tokens, int from)
        {
            var depth = 0;
            var to = from + 1;
            for (var i = from; i >= 0; i--)
            {
                var t = tokens[i];
                if (t.Kind == TokenKind.CloseParen)
                    depth++;
                else if (t.Kind == TokenKind.OpenParen && depth-- == 0)
                    break;
                else if (depth == 0 && (t.Kind == TokenKind.Comma || t.Text is "&&" or "||" or "!"))
                    break;
                to = i;
            }

            return to > from ? string.Empty : expression.Substring(tokens[to].Start, tokens[from].End - tokens[to].Start).Trim();
        }

        /// <summary><c>3 &lt; Day</c> читается как <c>Day &gt; 3</c></summary>
        private static string Flip(string op) => op switch
        {
            ">" => "<",
            "<" => ">",
            ">=" => "<=",
            "<=" => ">=",
            _ => op
        };

        private static void AddRead(string name, string raw, NaniLocation location, List<VariableLink> links) =>
            links.Add(new VariableLink(name, LinkSource.Nani, NaniVariableOperation.Read, null, null, raw, location));

        /// <summary>Нераспознанное выражение: каждый идентификатор-переменная — «прочее» с исходным текстом</summary>
        private static void AddOther(string raw, NaniLocation location, List<VariableLink> links)
        {
            var tokens = Tokenize(raw);
            for (var i = 0; i < tokens.Count; i++)
                if (IsVariable(tokens[i], tokens, i))
                    links.Add(new VariableLink(tokens[i].Text, LinkSource.Nani, NaniVariableOperation.Other, null, null, raw,
                        location));
        }

        private static List<Token> Tokenize(string expression)
        {
            var tokens = new List<Token>();
            var i = 0;
            while (i < expression.Length)
            {
                var c = expression[i];
                var start = i;
                if (char.IsWhiteSpace(c))
                {
                    i++;
                    continue;
                }

                if (c is '"' or '\'')
                {
                    i++;
                    while (i < expression.Length && !(expression[i] == c && expression[i - 1] != '\\'))
                        i++;
                    i = Math.Min(i + 1, expression.Length);
                    tokens.Add(new Token(TokenKind.String, expression.Substring(start, i - start), start, i));
                }
                else if (char.IsDigit(c) || (c == '.' && i + 1 < expression.Length && char.IsDigit(expression[i + 1])))
                {
                    while (i < expression.Length && (char.IsDigit(expression[i]) || expression[i] == '.'))
                        i++;
                    tokens.Add(new Token(TokenKind.Number, expression.Substring(start, i - start), start, i));
                }
                else if (char.IsLetter(c) || c == '_')
                {
                    while (i < expression.Length && (char.IsLetterOrDigit(expression[i]) || expression[i] == '_'))
                        i++;
                    tokens.Add(new Token(TokenKind.Identifier, expression.Substring(start, i - start), start, i));
                }
                else if (c is '(' or '{')
                    tokens.Add(new Token(TokenKind.OpenParen, c.ToString(), start, ++i));
                else if (c is ')' or '}')
                    tokens.Add(new Token(TokenKind.CloseParen, c.ToString(), start, ++i));
                else if (c == ',')
                    tokens.Add(new Token(TokenKind.Comma, ",", start, ++i));
                else
                {
                    var two = i + 1 < expression.Length ? expression.Substring(i, 2) : null;
                    var length = two is "==" or "!=" or ">=" or "<=" or "&&" or "||" ? 2 : 1;
                    i += length;
                    tokens.Add(new Token(TokenKind.Operator, expression.Substring(start, length), start, i));
                }
            }

            return tokens;
        }
    }
}
