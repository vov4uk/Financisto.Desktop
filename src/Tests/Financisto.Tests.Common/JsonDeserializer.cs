using Financisto.DataAccess.Data;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Financisto.Tests.Common
{
    public static class JsonDeserializer
    {
        public static List<T> Deserialize<T>(string json)
            where T : Entity, new()
        {
            return JsonConvert.DeserializeObject<List<T>>(json, new JsonSerializerSettings { ContractResolver = new ColumnJsonPropertyResolver<T>() });
        }
    }
}
