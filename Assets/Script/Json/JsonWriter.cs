using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Text;
using System.Globalization;

namespace Ls.Json
{
    public class JsonWriter
    {
        private readonly StringBuilder builder = new StringBuilder();
        private readonly JsonWriterSettings settings;
        private int indentLevel = 0;

        public JsonWriter(JsonWriterSettings? setting = null)
        {
            settings = setting ?? new JsonWriterSettings();
        }

        public string Write(JsonValue value)
        {
            builder.Clear();
            WriteValue(value);
            return builder.ToString();
        }

        private void WriteValue(JsonValue value)
        {
            switch (value)
            {
                case JsonNull n:
                    WriteNull();
                    break;
                case JsonBool b:
                    WriteBool(b);
                    break;
                case JsonNumber n:
                    WriteNumber(n);
                    break;
                case JsonString str:
                    WriteString(str.Value);
                    break;
                case JsonArray a:
                    WriteArray(a);
                    break;
                case JsonObject obj:
                    WriteObject(obj);
                    break;
            }
        }

        private void WriteNull()
        {
            builder.Append("null");
        }

        private void WriteBool(JsonBool v)
        {
            string str = v.Value ? "true" : "false";
            builder.Append(str);
        }

        private void WriteNumber(JsonNumber v)
        {
            builder.Append(v.Value.ToString(CultureInfo.InvariantCulture));
        }

        private void WriteString(string str)
        {
            builder.Append('"');
            WriteEscapedString(str);
            builder.Append('"');
        }

        private void WriteEscapedString(string text)
        {
            foreach (char c in text)
            {
                switch (c)
                {
                    case '"':
                        builder.Append("\\\"");
                        break;

                    case '\\':
                        builder.Append("\\\\");
                        break;

                    case '\b':
                        builder.Append("\\b");
                        break;

                    case '\f':
                        builder.Append("\\f");
                        break;

                    case '\n':
                        builder.Append("\\n");
                        break;

                    case '\r':
                        builder.Append("\\r");
                        break;

                    case '\t':
                        builder.Append("\\t");
                        break;

                    default:
                        if (c < 0x20)
                        {
                            WriteUnicodeEscape(builder, c);
                        }
                        else
                        {
                            builder.Append(c);
                        }                        
                        break;
                }
            }
        }

        private  void WriteUnicodeEscape(StringBuilder builder, char c)
        {
            builder.Append("\\u");
            builder.Append(((int)c).ToString("X4"));
        }

        private void WriteArray(JsonArray array)
        {
            builder.Append('[');
            indentLevel++;
            WriteNewLine();
            for (int i = 0; i < array.Values.Count; i++)
            {
                WriteIndent();
                WriteValue(array.Values[i]);

                if (i != array.Values.Count - 1)
                {
                    builder.Append(',');
                    WriteNewLine();
                }
            }
            indentLevel--;
            WriteNewLine();
            WriteIndent();
            builder.Append(']');
        }

        private void WriteObject(JsonObject obj)
        {
            int index = 0;
            builder.Append('{');
            indentLevel++;
            WriteNewLine();
            foreach (var pair in obj.Values)
            {
                WriteIndent();
                WriteString(pair.Key);
                builder.Append(':');
                if (settings.Indented)
                {
                    builder.Append(' ');
                }
                WriteValue(pair.Value);
                if (index != obj.Values.Count - 1)
                {
                    builder.Append(',');
                    WriteNewLine();
                }
                index++;
            }
            indentLevel--;
            WriteNewLine();
            WriteIndent();
            builder.Append('}');
        }

        private void WriteIndent()
        {
            if (!settings.Indented)
                return;

            for (int i = 0; i < indentLevel; i++)
            {
                builder.Append(settings.IndentString);
            }
        }
        private void WriteNewLine()
        {
            if (settings.Indented)
            {
                builder.AppendLine();
            }
        }

    }
}

