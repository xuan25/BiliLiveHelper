using System.Collections.Generic;
using System.Dynamic;

namespace Json
{
    public class JsonObject : DynamicObject
    {
        private Dictionary<string, object> dictionary = new Dictionary<string, object>();

        public int Count
        {
            get
            {
                return dictionary.Count;
            }
        }

        public void Add(string key, object value)
        {
            dictionary.Add(key.ToLower(), value);
        }

        public override bool TryGetMember(GetMemberBinder binder, out object result)
        {
            string name = binder.Name.ToLower();
            return dictionary.TryGetValue(name, out result);
        }

        public override bool TrySetMember(SetMemberBinder binder, object value)
        {
            dictionary[binder.Name.ToLower()] = value;
            return true;
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (dictionary.ContainsKey((string)indexes[0]))
                result = dictionary[(string)indexes[0]];
            else
                throw new System.NullReferenceException();
            return true;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (dictionary.ContainsKey((string)indexes[0]))
                dictionary[(string)indexes[0]] = value;
            else
                dictionary.Add((string)indexes[0], value);
            return true;
        }

    }
}
