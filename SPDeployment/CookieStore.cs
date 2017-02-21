using System;
using System.Collections.Generic;
using System.Data.SQLite;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;

namespace SPDeployment
{
    internal class CookieStore
    {
        private static readonly Dictionary<Uri, CookieContainer> s_cookieCache = new Dictionary<Uri, CookieContainer>();

        public static CookieContainer GetCookieContainer(Uri uri)
        {
            if (!s_cookieCache.ContainsKey(uri))
            {
                lock (s_cookieCache)
                {
                    if (!s_cookieCache.ContainsKey(uri))
                    {
                        CookieContainer cookies = new CookieContainer();

                        var cookieFile = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "Google\\Chrome\\User Data\\Default\\Cookies");

                        var connString = "Data Source=" + cookieFile + ";pooling=false";
                        using (var conn = new SQLiteConnection(connString))
                        using (var cmd = conn.CreateCommand())
                        {
                            cmd.CommandText = $"select * from cookies where host_key='{uri.DnsSafeHost}'";
                            conn.Open();
                            var reader = cmd.ExecuteReader();

                            while (reader.Read())
                            {
                                var val = DecryptValue((byte[])reader["encrypted_value"]);

                                var c = new Cookie(reader["name"]?.ToString(), val, reader["path"]?.ToString(), reader["host_key"]?.ToString());
                                c.Secure = (long)reader["secure"] == 1 ? true : false;
                                c.HttpOnly = (long)reader["httponly"] == 1 ? true : false;
                                cookies.Add(c);
                            }
                        }

                        s_cookieCache.Add(uri, cookies);
                    }
                }
            }

            return s_cookieCache[uri];
        }

        private static string DecryptValue(byte[] encValue)
        {
            return Encoding.Default.GetString(ProtectedData.Unprotect(encValue, null, DataProtectionScope.CurrentUser));
        }
    }
}
