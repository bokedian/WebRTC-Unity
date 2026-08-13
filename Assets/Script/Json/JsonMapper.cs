using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.Reflection;
using UnityEngine;

namespace Ls.Json
{
    public static class JsonMapper
    {
        #region Json反序列化
        public static T ToObject<T>(JsonValue value)
        {
            return (T)ToObject(value, typeof(T));
        }

        public static object? ToObject(JsonValue value,Type type)
        {
            if(value is JsonNull)
            {
                if (!type.IsValueType)
                {
                    return null;
                }
                if (Nullable.GetUnderlyingType(type) != null)
                {
                    return null;
                }
                throw new Exception($"Cannot assign null to {type.Name}");
            }
            if (value is JsonString str)
            {
                if (type != typeof(string))
                {
                    throw new Exception($"Cannot assign string to {type.Name}");
                }
                return str.Value;
            }
            if (value is JsonBool b)
            {
                if (type != typeof(bool))
                {
                    throw new Exception($"Cannot assign bool to {type.Name}");
                }
                return b.Value;
            }
            if (value is JsonNumber number)
            {
                return Convert.ChangeType(number.Value, type);
            }
            if(value is JsonArray array)
            {
                if (type.IsArray)
                {
                    return MapArray(array, type);
                }
                if(type.IsGenericType && type.GetGenericTypeDefinition() == typeof(List<>))
                {
                    return MapList(array, type);
                }
            }
            if(value is JsonObject jsonObject)
            {
                return MapObject(jsonObject, type);
            }
            throw new Exception($"Unsupported mapping from {value.GetType().Name} to {type.Name}");
        }

        private static object MapObject(JsonObject jsonObject,Type type)
        {
            object instance = Activator.CreateInstance(type);
            FieldInfo[] fields = type.GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                if(!jsonObject.Values.TryGetValue(field.Name,out JsonValue value))
                {
                    continue;
                }
                object fieldValue = ToObject(value, field.FieldType);
                field.SetValue(instance, fieldValue);
            }
            return instance;
        }

        private static object MapArray(JsonArray jsonArray,Type type)
        {
            Type elementType = type.GetElementType();
            Array array = Array.CreateInstance(elementType, jsonArray.Values.Count);

            for (int i = 0; i < jsonArray.Values.Count; i++)
            {
                object value = ToObject(jsonArray.Values[i], elementType);
                array.SetValue(value, i);
            }

            return array;
        }

        private static object MapList(JsonArray jsonArray,Type type)
        {
            Type elementType = type.GetGenericArguments()[0];
            IList list = (IList)Activator.CreateInstance(type);
            foreach (var node in jsonArray.Values)
            {
                list.Add(ToObject(node, elementType));
            }
            return list;
        }
        #endregion

        #region Json序列化
        public static JsonValue FromObject(object? obj)
        {
            if (obj is null) return new JsonNull();
            Type type = obj.GetType();
            if(obj is string str)
            {
                return new JsonString(str);
            }
            if(obj is bool b)
            {
                return new JsonBool(b);
            }
            if (IsNumber(type))
            {
                return new JsonNumber(Convert.ToDouble(obj));
            }
            if (type.IsArray)
                return FromEnumerable((Array)obj);

            if (type.IsGenericType &&
                type.GetGenericTypeDefinition() == typeof(List<>))
            {
                return FromEnumerable((IList)obj);
            }

            return FromClass(obj);
        }
        private static bool IsNumber(Type type)
        {
            switch (Type.GetTypeCode(type))
            {
                case TypeCode.Byte:
                case TypeCode.SByte:
                case TypeCode.Int16:
                case TypeCode.UInt16:
                case TypeCode.Int32:
                case TypeCode.UInt32:
                case TypeCode.Int64:
                case TypeCode.UInt64:
                case TypeCode.Single:
                case TypeCode.Double:
                case TypeCode.Decimal:
                    return true;

                default:
                    return false;
            }
        }
        private static JsonArray FromEnumerable(IEnumerable enumerable)
        {
            JsonArray jsonArray = new JsonArray();

            foreach (object? item in enumerable)
            {
                jsonArray.Values.Add(FromObject(item));
            }

            return jsonArray;
        }

        private static JsonObject FromClass(object obj)
        {
            JsonObject jsonObject = new JsonObject();
            FieldInfo[] fields = obj.GetType().GetFields(BindingFlags.Instance | BindingFlags.Public);
            foreach (var field in fields)
            {
                object? value = field.GetValue(obj);
                JsonValue jsonValue = FromObject(value);
                jsonObject.Values.Add(field.Name, jsonValue);
            }
            return jsonObject;
        }
        #endregion
    }
}


