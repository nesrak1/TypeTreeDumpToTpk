using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace TypeTreeDumpToTpk
{
    static class ZipUtil
    {
        public static void DownloadAndUnpackZip(string url, string dest)
        {
            if (!Directory.Exists(dest))
            {
                using WebClient client = new WebClient();
                client.DownloadFile(url, dest + ".zip");
                ZipFile.ExtractToDirectory(dest + ".zip", dest);
            }
        }
    }
}
