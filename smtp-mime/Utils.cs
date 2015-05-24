using System;
using System.Collections.Generic;
using System.IO;
using System.Text;

namespace SmtpMime
{
    /// <summary>
    /// Extension methods for <see cref="System.IO.FileInfo"/>.
    /// </summary>
    public static class FileInfoExtensions
    {
        /// <summary>
        /// Template for a file item in multipart/form-data format.
        /// </summary>
        public const string HeaderTemplate = 
            "--{0}\r\nContent-Disposition: attachment; filename=\"{1}\"\r\nContent-Type: {2}; name=\"{0}\"\r\nContent-Transfer-Encoding: base64\r\n\r\n";
        public const string HeaderTemplateBin = 
            "--{0}\r\nContent-Disposition: attachment; filename=\"{1}\"\r\nContent-Type: {2}; name=\"{0}\"\r\nContent-Transfer-Encoding: binary\r\n\r\n";

        /// <summary>
        /// Writes a file to a stream in multipart/form-data format.
        /// </summary>
        /// <param name="file">The file that should be written.</param>
        /// <param name="stream">The stream to which the file should be written.</param>
        /// <param name="mimeBoundary">The MIME multipart form boundary string.</param>
        /// <param name="mimeType">The MIME type of the file.</param>
        /// <param name="formKey">The name of the form parameter corresponding to the file upload.</param>
        /// <exception cref="System.ArgumentNullException">
        /// Thrown if any parameter is <see langword="null" />.
        /// </exception>
        /// <exception cref="System.ArgumentException">
        /// Thrown if <paramref name="mimeBoundary" />, <paramref name="mimeType" />,
        /// or <paramref name="formKey" /> is empty.
        /// </exception>
        /// <exception cref="System.IO.FileNotFoundException">
        /// Thrown if <paramref name="file" /> does not exist.
        /// </exception>
        public static void WriteMultipartFormData_Base64(this FileInfo file, EitherSecureStream stream, string mimeBoundary, string mimeType)
        {
            if (file == null)
                throw new ArgumentNullException("file");
            if (!file.Exists)
                throw new FileNotFoundException("Unable to find file to write to stream.", file.FullName);
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (mimeBoundary == null)
                throw new ArgumentNullException("mimeBoundary");
            if (mimeBoundary.Length == 0)
                throw new ArgumentException("MIME boundary may not be empty.", "mimeBoundary");
            if (mimeType == null)
                throw new ArgumentNullException("mimeType");
            if (mimeType.Length == 0)
                throw new ArgumentException("MIME type may not be empty.", "mimeType");
            
            var header = string.Format(HeaderTemplate, mimeBoundary, file.Name, mimeType);
            var headerbytes = Encoding.UTF8.GetBytes(header);
            stream.Send(headerbytes);
            var fileBytes = Encoding.ASCII.GetBytes(Convert.ToBase64String(File.ReadAllBytes(file.FullName)));
            stream.Send(fileBytes);
            var newlineBytes = Encoding.UTF8.GetBytes("\r\n");
            stream.Send(newlineBytes);
        }
        public static void WriteMultipartFormData_Bin(this FileInfo file, EitherSecureStream stream, string mimeBoundary, string mimeType)
        {
            if (file == null)
                throw new ArgumentNullException("file");
            if (!file.Exists)
                throw new FileNotFoundException("Unable to find file to write to stream.", file.FullName);
            if (stream == null)
                throw new ArgumentNullException("stream");
            if (mimeBoundary == null)
                throw new ArgumentNullException("mimeBoundary");
            if (mimeBoundary.Length == 0)
                throw new ArgumentException("MIME boundary may not be empty.", "mimeBoundary");
            if (mimeType == null)
                throw new ArgumentNullException("mimeType");
            if (mimeType.Length == 0)
                throw new ArgumentException("MIME type may not be empty.", "mimeType");

            var header = string.Format(HeaderTemplateBin, mimeBoundary, file.Name, mimeType);
            var headerbytes = Encoding.UTF8.GetBytes(header);
            stream.Send(headerbytes);
            using (var fileStream = new FileStream(file.FullName, FileMode.Open, FileAccess.Read))
            {
                var buffer = new byte[1024];
                var bytesRead = 0;
                while ((bytesRead = fileStream.Read(buffer, 0, buffer.Length)) != 0)
                {
                    stream.Send(buffer);
                }
                fileStream.Close();
            }
            var newlineBytes = Encoding.UTF8.GetBytes("\r\n");
            stream.Send(newlineBytes);
        }
    }
}
