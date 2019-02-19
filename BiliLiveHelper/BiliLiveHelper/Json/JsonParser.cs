using System.Collections.Generic;
using System.IO;
using System.Text;

namespace Json
{
    class JsonParser
    {
        private static object ToNumber(string num)
        {
            if (num.Contains("."))
            {
                double.TryParse(num, out double result);
                return result;
            }
            else
            {
                long.TryParse(num, out long result);
                return result;
            }
        }

        private static object ParseValue(StringReader stringReader)
        {
            while (stringReader.Peek() == ' ' || stringReader.Peek() == '\r' || stringReader.Peek() == '\n')
                stringReader.Read();
            if (stringReader.Peek() == '\"')
            {
                stringReader.Read();
                StringBuilder stringBuilder = new StringBuilder();
                while (stringReader.Peek() != -1)
                {
                    if (stringReader.Peek() == '\\')
                    {
                        stringBuilder.Append((char)stringReader.Read());
                        stringBuilder.Append((char)stringReader.Read());
                    }
                    else if (stringReader.Peek() == '\"')
                    {
                        string value = stringBuilder.ToString();
                        while (stringReader.Peek() != ',' && stringReader.Peek() != '}' && stringReader.Peek() != ']')
                            stringReader.Read();
                        if (stringReader.Peek() == ',')
                            stringReader.Read();
                        return value;
                    }
                    else
                        stringBuilder.Append((char)stringReader.Read());
                }
                return stringBuilder.ToString();
            }
            else if (stringReader.Peek() == '{')
            {
                JsonObject jsonObject = ParseObject(stringReader);
                while (stringReader.Peek() != -1 && stringReader.Read() != ',') ;
                return jsonObject;
            }
            else if (stringReader.Peek() == '[')
            {
                JsonArray jsonArray = ParseArray(stringReader);
                while (stringReader.Peek() != -1 && stringReader.Read() != ',') ;
                return jsonArray;
            }
            else
            {
                StringBuilder stringBuilder = new StringBuilder();
                while (stringReader.Peek() != -1)
                {
                    if (stringReader.Peek() == '\\')
                    {
                        stringBuilder.Append((char)stringReader.Read());
                        stringBuilder.Append((char)stringReader.Read());
                    }
                    else if (stringReader.Peek() == ',')
                    {
                        string value = stringBuilder.ToString();
                        stringReader.Read();
                        return ToNumber(value);
                    }
                    else if (stringReader.Peek() == '}' || stringReader.Peek() == ']')
                        return ToNumber(stringBuilder.ToString());
                    else
                        stringBuilder.Append((char)stringReader.Read());
                }
                return stringBuilder.ToString();
            }
        }

        private static KeyValuePair<string, object> ParseKeyValuePaire(StringReader stringReader)
        {
            StringBuilder stringBuilder = new StringBuilder();
            while (stringReader.Peek() != -1 && stringReader.Read() != '\"') ;
            while (stringReader.Peek() > -1)
            {
                if (stringReader.Peek() == '\\')
                {
                    stringBuilder.Append((char)stringReader.Read());
                    stringBuilder.Append((char)stringReader.Read());
                }
                else if (stringReader.Peek() == '\"')
                {
                    stringReader.Read();
                    while (stringReader.Peek() != -1 && stringReader.Read() != ':') ;
                    string key = stringBuilder.ToString();
                    object value = ParseValue(stringReader);
                    return new KeyValuePair<string, object>(key, value);
                }
                else
                {
                    stringBuilder.Append((char)stringReader.Read());
                }
            }
            return new KeyValuePair<string, object>("UNKNOW", null);
        }

        private static JsonObject ParseObject(StringReader stringReader)
        {
            stringReader.Read();
            JsonObject jsonObject = new JsonObject();
            while (stringReader.Peek() > -1)
            {
                if (stringReader.Peek() == '{')
                    ParseObject(stringReader);
                else if (stringReader.Peek() == '[')
                    ParseArray(stringReader);
                else if (stringReader.Peek() == '\"')
                {
                    KeyValuePair<string, object> keyValuePair = ParseKeyValuePaire(stringReader);
                    jsonObject.Add(keyValuePair.Key, keyValuePair.Value);
                }
                else if (stringReader.Peek() == '}')
                {
                    stringReader.Read();
                    return jsonObject;
                }
                else
                    stringReader.Read();
            }
            return jsonObject;
        }

        private static JsonArray ParseArray(StringReader stringReader)
        {
            stringReader.Read();
            JsonArray jsonArray = new JsonArray();
            while (stringReader.Peek() > -1)
            {
                if (stringReader.Peek() == ']')
                {
                    stringReader.Read();
                    return jsonArray;
                }
                else
                    jsonArray.Add(ParseValue(stringReader));
            }
            return jsonArray;
        }

        public static object Parse(string json)
        {
            StringReader stringReader = new StringReader(json.Trim());
            if (stringReader.Peek() == -1)
                return null;
            else if (stringReader.Peek() == '{')
                return ParseObject(stringReader);
            else if (stringReader.Peek() == '[')
                return ParseArray(stringReader);
            else
            {
                return null;
            }
        }
    }
}
