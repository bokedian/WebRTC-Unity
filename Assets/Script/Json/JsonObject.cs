using System.Collections.Generic;

namespace Ls.Json
{
    public class JsonObject : JsonValue
    {
        public Dictionary<string, JsonValue> Values { get; } = new Dictionary<string, JsonValue>();
    }

    public class JsonArray : JsonValue
    {
        public List<JsonValue> Values { get; } = new List<JsonValue>();
    }

    public class JsonString : JsonValue
    {
        public string Value;
        public JsonString(string v)
        {
            Value = v;
        }
    }

    public class JsonNumber : JsonValue
    {
        public double Value;

        public JsonNumber(double v)
        {
            Value = v;
        }
    }

    public class JsonBool : JsonValue
    {
        public bool Value;
        public JsonBool(bool v)
        {
            Value = v;
        }
    }

    public class JsonNull : JsonValue
    {

    }
}

