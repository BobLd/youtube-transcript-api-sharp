using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;

namespace YoutubeTranscriptApi.Tests
{
    public static class Helpers
    {
        public static IDictionary<string, IReadOnlyList<Cookie>> dict_from_cookiejar(this CookieContainer container)
        {
            var imvokAttr = BindingFlags.NonPublic | BindingFlags.GetField | BindingFlags.Instance;
            // https://stackoverflow.com/questions/13675154/how-to-get-cookies-info-inside-of-a-cookiecontainer-all-of-them-not-for-a-spe
            var table = (Hashtable)container.GetType().InvokeMember("m_domainTable", imvokAttr, null, container, null);

            var dict = new Dictionary<string, IReadOnlyList<Cookie>>();

            foreach (string key in table.Keys)
            {
                var cookieCollections = new List<Cookie>();
                var item = table[key];

                foreach (CookieCollection cc in (ICollection)item.GetType().GetProperty("Values", imvokAttr).GetMethod.Invoke(item, null))
                {
                    foreach (Cookie cookie in cc)
                    {
                        cookieCollections.Add(cookie);
                    }
                }

                dict[key] = cookieCollections;
            }

            return dict;
        }
    }
}
