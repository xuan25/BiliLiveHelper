using System.Collections.Generic;
using System.Dynamic;

namespace Json
{
    public class JsonArray : DynamicObject
    {
        private List<object> list = new List<object>();

        public int Count
        {
            get
            {
                return list.Count;
            }
        }

        public void Add(object value)
        {
            list.Add(value);
        }

        public override bool TryGetIndex(GetIndexBinder binder, object[] indexes, out object result)
        {
            if (list.Count > (int)indexes[0])
                result = list[(int)indexes[0]];
            else
            {
                result = new JsonArray();
                while (list.Count < (int)indexes[0])
                    list.Add(null);
                list.Add(result);
            }
            return true;
        }

        public override bool TrySetIndex(SetIndexBinder binder, object[] indexes, object value)
        {
            if (list.Count > (int)indexes[0])
                list[(int)indexes[0]] = value;
            else
            {
                while (list.Count < (int)indexes[0])
                    list.Add(null);
                list.Add(value);
            }
            return true;
        }

    }
}
