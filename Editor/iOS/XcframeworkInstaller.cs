using System;
using System.IO;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text;

namespace AdfactenAds.Editor
{
    /// <summary>
    /// Downloads the prebuilt AdfactenAds XCFramework from the Adfacten CDN, checks its SHA-256 and unpacks it.
    /// The zip is cached (Library/Adfacten) so later builds work offline. No Unity APIs, so it is unit tested.
    /// </summary>
    internal static class XcframeworkInstaller
    {
        public const string FrameworkName = "AdfactenAds.xcframework";

        /// <summary>Returns the path of a verified zip in <paramref name="cacheDir"/>, downloading it if needed.</summary>
        public static string EnsureZip(string cacheDir, string url, string sha256, Func<string, byte[]> download = null)
        {
            Directory.CreateDirectory(cacheDir);
            var zip = Path.Combine(cacheDir, Path.GetFileName(new Uri(url).AbsolutePath));
            if (File.Exists(zip) && Sha256Hex(File.ReadAllBytes(zip)) == sha256) return zip;
            var bytes = (download ?? Download)(url);
            var actual = Sha256Hex(bytes);
            if (actual != sha256)
                throw new InvalidDataException("Checksum mismatch for " + url + ": expected " + sha256 + ", got " + actual + ".");
            var tmp = zip + ".tmp";
            File.WriteAllBytes(tmp, bytes);
            if (File.Exists(zip)) File.Delete(zip);
            File.Move(tmp, zip);
            return zip;
        }

        /// <summary>
        /// Unpacks <paramref name="zipPath"/> into <paramref name="destDir"/> (replacing an older copy) and returns the
        /// path of AdfactenAds.xcframework.
        /// </summary>
        public static string Extract(string zipPath, string destDir)
        {
            var target = Path.Combine(destDir, FrameworkName);
            if (Directory.Exists(target)) Directory.Delete(target, true);
            Directory.CreateDirectory(destDir);
            var root = Path.GetFullPath(destDir);
            if (!root.EndsWith(Path.DirectorySeparatorChar.ToString())) root += Path.DirectorySeparatorChar;
            // ZipArchive rather than ZipFile: it is available with every API compatibility level.
            using (var stream = File.OpenRead(zipPath))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    var path = Path.GetFullPath(Path.Combine(destDir, entry.FullName));
                    if (!path.StartsWith(root, StringComparison.Ordinal))
                        throw new InvalidDataException("Unsafe path in " + zipPath + ": " + entry.FullName);
                    if (entry.FullName.EndsWith("/"))
                    {
                        Directory.CreateDirectory(path);
                        continue;
                    }
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    using (var input = entry.Open())
                    using (var output = File.Create(path))
                    {
                        input.CopyTo(output);
                    }
                }
            }
            if (!File.Exists(Path.Combine(target, "Info.plist")))
                throw new InvalidDataException(FrameworkName + " not found in " + zipPath + ".");
            return target;
        }

        public static string Sha256Hex(byte[] data)
        {
            using (var sha = SHA256.Create())
            {
                var hash = sha.ComputeHash(data);
                var sb = new StringBuilder(hash.Length * 2);
                foreach (var b in hash) sb.Append(b.ToString("x2"));
                return sb.ToString();
            }
        }

        // WebClient lives in System.dll, which every Unity API compatibility level references.
#pragma warning disable 618
        private static byte[] Download(string url)
        {
            using (var client = new WebClient())
            {
                return client.DownloadData(url);
            }
        }
#pragma warning restore 618
    }
}
