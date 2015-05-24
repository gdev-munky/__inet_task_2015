using System;
using System.Collections.Generic;
using System.Text;
using System.Web;

namespace SmtpMime
{
    public static class MimeType
    {
        /// <summary>
        /// Creates a multipart/form-data boundary.
        /// </summary>
        /// <returns>
        /// A dynamically generated form boundary for use in posting multipart/form-data requests.
        /// </returns>
        public static string CreateFormDataBoundary()
        {
            return "---------------------------" + DateTime.Now.Ticks.ToString("x");
        }
        public static byte[] GenerateEndBoundary(string boundary)
        {
            return Encoding.UTF8.GetBytes("--" + boundary + "--");
        }
        public static bool IsMimeTypeAnImage(string mimeType)
        {
            return mimeType.StartsWith("image/");
        }
    }
}
