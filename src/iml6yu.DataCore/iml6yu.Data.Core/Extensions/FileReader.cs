using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace iml6yu.Data.Core.Extensions
{
    public static class FileReader
    {

        public static async Task<T> ReadJsonContentAsync<T>(this string path, Encoding encoding, CancellationToken cancellationToken)
            where T : class
        {
            T entity = default;
            if (string.IsNullOrEmpty(path))
                throw new ArgumentNullException(nameof(path));

            path = path.GetAbsolutePath();
            if (!File.Exists(path))
                throw new FileNotFoundException(path);

            var content = await File.ReadAllTextAsync(path, encoding, cancellationToken);
            await Task.Run(() =>
            {
                entity = JsonSerializer.Deserialize<T>(content, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    AllowTrailingCommas = true,
                    ReadCommentHandling = JsonCommentHandling.Skip,
                });
            });
            return entity;
        }

        public static string GetAbsolutePath(this string path)
        {
            //不是绝对路径
            if (!Path.IsPathFullyQualified(path))
                //在当前程序目录中查找对应文件
                path = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, path);
            return path;
        }
    }
}
