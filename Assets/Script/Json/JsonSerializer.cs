using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Ls.Json
{
    public static class JsonSerializer
    {
        public static T Deserialize<T>(string json)
        {
            return (T)Deserialize(json, typeof(T));
        }

        public static object Deserialize(string json, Type type)
        {
            JsonReader reader = new JsonReader(json);

            JsonParser parser = new JsonParser(reader);

            JsonValue value = parser.Parse();

            return JsonMapper.ToObject(value, type);
        }

        public static string Serialize(object? obj)
        {
            JsonValue value = JsonMapper.FromObject(obj);

            JsonWriter writer = new JsonWriter();

            return writer.Write(value);
        }

        public static string Serialize(object? obj,JsonWriterSettings? settings)
        {
            JsonValue value = JsonMapper.FromObject(obj);
            JsonWriter writer = new JsonWriter(settings);
            return writer.Write(value);
        }
    }
}

