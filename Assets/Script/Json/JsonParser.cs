using System;
using System.Globalization;
using System.Text;

namespace Ls.Json
{
    public class JsonParser
    {
        private readonly JsonReader reader;

        public JsonParser(JsonReader r)
        {
            reader = r;
        }
        public JsonValue Parse()
        {
            reader.SkipWhiteSpace();
            JsonValue value = ParseValue();
            reader.SkipWhiteSpace();

            if (!reader.End)
            {
                reader.ThrowError("Unexcepted character");
            }
            return value;
        }

        private JsonValue ParseValue()
        {
            reader.SkipWhiteSpace();

            char c = reader.Peek();

            switch (c)
            {
                case '{':
                    return ParseObject();

                case '[':
                    return ParseArray();

                case '"':
                    return ParseString();

                case 't':
                case 'f':
                    return ParseBool();

                case 'n':
                    return ParseNull();

                default:
                    if (char.IsDigit(c) || c == '-')
                        return ParseNumber();

                    reader.ThrowError($"Unexpected character '{c}'.");
                    return null;
            }
        }

        private JsonObject ParseObject()
        {
            reader.Expect('{');
            JsonObject obj = new JsonObject();
            reader.SkipWhiteSpace();
            if (reader.TryRead('}'))
            {
                return obj;
            }
            while (true)
            {
                reader.SkipWhiteSpace();
                JsonString key = ParseString();

                reader.SkipWhiteSpace();
                reader.Expect(':');
                reader.SkipWhiteSpace();
                JsonValue v = ParseValue();
                if (obj.Values.ContainsKey(key.Value))
                {
                    reader.ThrowError($"Duplicate key {key.Value}");
                }
                obj.Values.Add(key.Value, v);
                reader.SkipWhiteSpace();
                if (reader.TryRead(','))
                {
                    continue;
                }
                reader.Expect('}');
                break;
            }
            return obj;
        }
        private JsonArray ParseArray()
        {
            reader.Expect('[');
            JsonArray array = new JsonArray();
            reader.SkipWhiteSpace();
            if (reader.TryRead(']'))
            {
                return array;
            }
            while (true)
            {
                JsonValue v = ParseValue();
                array.Values.Add(v);
                reader.SkipWhiteSpace();
                if (reader.TryRead(','))
                {
                    reader.SkipWhiteSpace();
                    continue;
                }
                reader.Expect(']');
                break;
            }
            return array;

        }
        private JsonString ParseString()
        {
            reader.Expect('"');
            StringBuilder sb = new StringBuilder();
            while (true)
            {
                if (reader.End)
                {
                    reader.ThrowError("Unexpected end of string");
                }
                char c = reader.Read();
                if (c == '"') break;
                if (c == '\\')
                {
                    sb.Append(ParseEscape());
                }
                else
                {
                    sb.Append(c);
                }

            }
            return new JsonString(sb.ToString());
        }
        /// <summary>
        /// 处理转义字符
        /// </summary>
        /// <returns></returns>
        private char ParseEscape()
        {
            if (reader.End)
            {
                reader.ThrowError("Unexpected end after '\\'");
            }
            char c = reader.Read();
            switch (c)
            {
                case '"':
                    return '"';
                case '\\':
                    return '\\';
                case '/':
                    return '/';
                case 'b':
                    return '\b';
                case 'f':
                    return '\f';
                case 'n':
                    return '\n';
                case 'r':
                    return '\r';
                case 't':
                    return '\t';
                case 'u':
                    return ParseUnicode();


            }
            reader.ThrowError($"Invalid escape character '\\{c}'.");

            return '\0';
        }
        /// <summary>
        /// 处理十六进制
        /// </summary>
        /// <returns></returns>
        private char ParseUnicode()
        {
            StringBuilder hex = new();
            for (int i = 0; i < 4; i++)
            {
                if (reader.End)
                    reader.ThrowError("Unexpected end in unicode.");

                hex.Append(reader.Read());
            }
            int code = Convert.ToInt32(hex.ToString(), 16);
            return (char)code;
        }
        private JsonBool ParseBool()
        {
            if (reader.ReadIfMatch("true"))
            {
                return new JsonBool(true);
            }

            if (reader.ReadIfMatch("false"))
            {
                return new JsonBool(false);
            }

            reader.ThrowError("Invalid boolean literal.");
            return null!;
        }
        private JsonNull ParseNull()
        {
            if (reader.ReadIfMatch("null"))
            {
                return new JsonNull();
            }

            reader.ThrowError("Invalid null literal.");
            return null!;
        }
        private JsonNumber ParseNumber()
        {
            string text = reader.ReadWhile(c =>
              char.IsDigit(c) ||
              c == '-' ||
              c == '+' ||
              c == '.' ||
              c == 'e' ||
              c == 'E');
            try
            {
                double v = double.Parse(text, CultureInfo.InvariantCulture);
                return new JsonNumber(v);
            }
            catch (FormatException)
            {
                reader.ThrowError($"Invalid number '{text}'");
                return null;
            }
        }
    }
}

