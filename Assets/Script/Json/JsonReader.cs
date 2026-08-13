using System;
using System.Text;

namespace Ls.Json
{
    public class JsonReader
    {
        private readonly string _json;
        public int index { get; private set; }
        public int line { get; private set; }
        public int column { get; private set; }
        public JsonReader(string s)
        {
            _json = s;
            index = 0;
            line = 1;
            column = 1;
        }

        public bool End => index >= _json.Length;

        public char Peek()
        {
            if (End)
            {
                return '\0';
            }
            return _json[index];
        }

        public char Read()
        {
            if (End)
            {
                return '\0';
            }
            char c = _json[index++];
            if (c == '\n')
            {
                line++;
                column = 1;
            }
            else
            {
                column++;
            }
            return c;
        }

        public void SkipWhiteSpace()
        {
            while (!End)
            {
                char c = Peek();
                if (!char.IsWhiteSpace(c))
                {
                    break;
                }
                Read();
            }
        }

        public bool Match(string text)
        {
            if (index + text.Length > _json.Length) return false;
            for (int i = 0; i < text.Length; i++)
            {
                if (text[i] != _json[index + i]) return false;
            }
            return true;
        }

        public bool ReadIfMatch(string text)
        {
            if (!Match(text))
                return false;

            for (int i = 0; i < text.Length; i++)
            {
                Read();
            }

            return true;
        }

        public string ReadWhile(Func<char, bool> predicate)
        {
            StringBuilder sb = new();

            while (!End && predicate(Peek()))
            {
                sb.Append(Read());
            }

            return sb.ToString();
        }

        public void ThrowError(string message)
        {
            throw new Exception(
                $"Json Parse Error (Line {line}, Column {column}) : {message}");
        }

        public void Expect(char expected)
        {
            char actual = Read();

            if (actual != expected)
            {
                ThrowError($"Expected '{expected}', but got '{actual}'");
            }
        }

        public bool TryRead(char c)
        {
            if (Peek() != c)
                return false;

            Read();
            return true;
        }

    }
}

