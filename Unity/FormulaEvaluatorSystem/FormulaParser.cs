using System;
using System.Globalization;

namespace Vortex.Unity.FormulaEvaluatorSystem
{
    public static class FormulaParser
    {
        public static bool TryEvaluate(string formula, double[] parameters, out double result, out string error)
        {
            result = 0;
            error = null;

            if (string.IsNullOrEmpty(formula))
            {
                error = "Empty formula";
                return false;
            }

            try
            {
                var substituted = SubstituteParameters(formula, parameters);
                var state = new ParserState(substituted);
                result = state.ParseExpression();
                state.SkipWhitespace();

                if (state.Pos < state.Input.Length)
                {
                    error = $"Unexpected character '{state.Input[state.Pos]}' at position {state.Pos}";
                    return false;
                }

                return true;
            }
            catch (Exception e)
            {
                error = e.Message;
                return false;
            }
        }

        public static double Evaluate(string formula, double[] parameters)
        {
            if (!TryEvaluate(formula, parameters, out var result, out var error))
                throw new FormulaParseException(error);
            return result;
        }

        public static int GetMaxSlotIndex(string formula)
        {
            if (string.IsNullOrEmpty(formula)) return -1;

            int max = -1;
            int i = 0;
            while (i < formula.Length)
            {
                if (formula[i] == '{')
                {
                    int end = formula.IndexOf('}', i + 1);
                    if (end > i + 1 && int.TryParse(formula.Substring(i + 1, end - i - 1), out int index))
                    {
                        if (index > max) max = index;
                    }

                    i = end >= 0 ? end + 1 : i + 1;
                }
                else
                    i++;
            }

            return max;
        }

        private static string SubstituteParameters(string formula, double[] parameters)
        {
            if (parameters == null || parameters.Length == 0)
                return formula;

            var result = formula;
            for (int i = parameters.Length - 1; i >= 0; i--)
            {
                var token = "{" + i + "}";
                var value = parameters[i].ToString("R", CultureInfo.InvariantCulture);
                // Wrap negative values in parentheses to avoid parse ambiguity
                if (parameters[i] < 0)
                    value = "(" + value + ")";
                result = result.Replace(token, value);
            }

            return result;
        }

        private struct ParserState
        {
            public readonly string Input;
            public int Pos;

            public ParserState(string input)
            {
                Input = input;
                Pos = 0;
            }

            public void SkipWhitespace()
            {
                while (Pos < Input.Length && char.IsWhiteSpace(Input[Pos]))
                    Pos++;
            }

            private char Peek()
            {
                SkipWhitespace();
                return Pos < Input.Length ? Input[Pos] : '\0';
            }

            private char Advance()
            {
                var c = Input[Pos];
                Pos++;
                return c;
            }

            public double ParseExpression()
            {
                var left = ParseTerm();
                while (true)
                {
                    var c = Peek();
                    if (c == '+') { Advance(); left += ParseTerm(); }
                    else if (c == '-') { Advance(); left -= ParseTerm(); }
                    else break;
                }

                return left;
            }

            private double ParseTerm()
            {
                var left = ParsePower();
                while (true)
                {
                    var c = Peek();
                    if (c == '*') { Advance(); left *= ParsePower(); }
                    else if (c == '/')
                    {
                        Advance();
                        var right = ParsePower();
                        left /= right;
                    }
                    else break;
                }

                return left;
            }

            private double ParsePower()
            {
                var baseVal = ParseUnary();
                if (Peek() == '^')
                {
                    Advance();
                    var exp = ParsePower(); // right-associative
                    return Math.Pow(baseVal, exp);
                }

                return baseVal;
            }

            private double ParseUnary()
            {
                if (Peek() == '-')
                {
                    Advance();
                    return -ParseUnary();
                }

                if (Peek() == '+')
                {
                    Advance();
                    return ParseUnary();
                }

                return ParseAtom();
            }

            private double ParseAtom()
            {
                SkipWhitespace();
                if (Pos >= Input.Length)
                    throw new FormulaParseException("Unexpected end of expression");

                var c = Input[Pos];

                // Number
                if (char.IsDigit(c) || c == '.')
                    return ParseNumber();

                // Parenthesized expression
                if (c == '(')
                {
                    Advance();
                    var value = ParseExpression();
                    SkipWhitespace();
                    if (Pos >= Input.Length || Input[Pos] != ')')
                        throw new FormulaParseException("Missing closing parenthesis");
                    Advance();
                    return value;
                }

                // Identifier: constant or function
                if (char.IsLetter(c) || c == '_')
                {
                    var name = ParseIdentifier();
                    SkipWhitespace();

                    // Function call
                    if (Pos < Input.Length && Input[Pos] == '(')
                    {
                        Advance();
                        return ParseFunctionCall(name);
                    }

                    // Named constant
                    return ResolveConstant(name);
                }

                throw new FormulaParseException($"Unexpected character '{c}' at position {Pos}");
            }

            private double ParseNumber()
            {
                int start = Pos;
                while (Pos < Input.Length && (char.IsDigit(Input[Pos]) || Input[Pos] == '.'))
                    Pos++;

                var numberStr = Input.Substring(start, Pos - start);
                if (!double.TryParse(numberStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
                    throw new FormulaParseException($"Invalid number '{numberStr}' at position {start}");
                return value;
            }

            private string ParseIdentifier()
            {
                int start = Pos;
                while (Pos < Input.Length && (char.IsLetterOrDigit(Input[Pos]) || Input[Pos] == '_'))
                    Pos++;
                return Input.Substring(start, Pos - start).ToLowerInvariant();
            }

            private double ParseFunctionCall(string name)
            {
                var args = new double[4];
                int argCount = 0;

                if (Peek() != ')')
                {
                    args[argCount++] = ParseExpression();
                    while (Peek() == ',')
                    {
                        Advance();
                        if (argCount >= args.Length)
                            throw new FormulaParseException($"Too many arguments for function '{name}'");
                        args[argCount++] = ParseExpression();
                    }
                }

                SkipWhitespace();
                if (Pos >= Input.Length || Input[Pos] != ')')
                    throw new FormulaParseException($"Missing closing parenthesis for function '{name}'");
                Advance();

                return EvalFunction(name, args, argCount);
            }

            private static double EvalFunction(string name, double[] args, int count)
            {
                switch (name)
                {
                    case "sqrt":
                        Expect(name, count, 1);
                        return Math.Sqrt(args[0]);
                    case "abs":
                        Expect(name, count, 1);
                        return Math.Abs(args[0]);
                    case "sin":
                        Expect(name, count, 1);
                        return Math.Sin(args[0]);
                    case "cos":
                        Expect(name, count, 1);
                        return Math.Cos(args[0]);
                    case "tan":
                        Expect(name, count, 1);
                        return Math.Tan(args[0]);
                    case "log":
                        Expect(name, count, 1);
                        return Math.Log(args[0]);
                    case "floor":
                        Expect(name, count, 1);
                        return Math.Floor(args[0]);
                    case "ceil":
                        Expect(name, count, 1);
                        return Math.Ceiling(args[0]);
                    case "round":
                        Expect(name, count, 1);
                        return Math.Round(args[0]);
                    case "min":
                        Expect(name, count, 2);
                        return Math.Min(args[0], args[1]);
                    case "max":
                        Expect(name, count, 2);
                        return Math.Max(args[0], args[1]);
                    case "pow":
                        Expect(name, count, 2);
                        return Math.Pow(args[0], args[1]);
                    case "clamp":
                        Expect(name, count, 3);
                        return Math.Max(args[1], Math.Min(args[2], args[0]));
                    default:
                        throw new FormulaParseException($"Unknown function '{name}'");
                }
            }

            private static void Expect(string funcName, int actual, int expected)
            {
                if (actual != expected)
                    throw new FormulaParseException(
                        $"Function '{funcName}' expects {expected} argument(s), got {actual}");
            }

            private static double ResolveConstant(string name)
            {
                switch (name)
                {
                    case "pi": return Math.PI;
                    case "e": return Math.E;
                    default:
                        throw new FormulaParseException($"Unknown constant '{name}'");
                }
            }
        }
    }

    public class FormulaParseException : Exception
    {
        public FormulaParseException(string message) : base(message)
        {
        }
    }
}
