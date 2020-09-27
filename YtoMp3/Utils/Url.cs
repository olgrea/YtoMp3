using System;
using System.Collections.Generic;

namespace YtoMp3.Utils
{
    public class Url
    {
        public static IReadOnlyDictionary<string, string> ParseQuery(string query)
        {
            if (string.IsNullOrEmpty(query)) 
            {
                throw new ArgumentNullException(nameof(query));
            }

            int idx = query.IndexOf("?");
            if (idx > 0)
            {
                query = query.Substring(idx + 1);
            }

            string[] parts = query.Split("&");
            var @params = new Dictionary<string, string>();
            foreach (string part in parts)
            {
                idx = part.IndexOf("=");
                if (idx > 0)
                {
                    string k = part.Substring(0, idx);
                    string val = part.Substring(idx + 1);
                    @params[k] = val;
                }
            }

            return @params;
        }

    }
}
