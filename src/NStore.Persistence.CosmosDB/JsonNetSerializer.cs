namespace NStore.Persistence.DocumentDb
{
    using Newtonsoft.Json;
    using System;
    using System.Collections.Generic;
    using System.Text;

    public class JsonNetSerializer : ISerializer
    {
        public object Deserialize(string text, Type t)
        {
            return JsonConvert.DeserializeObject(text, t);
        }

        public string Serialize(object o)
        {
            return JsonConvert.SerializeObject(o);
        }
    }
}
