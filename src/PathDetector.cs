using System;
using System.Collections.Generic;
using System.IO;

namespace HOPE5000TitleFix
{
    public static class PathDetector
    {
        public static string FindDefault()
        {
            List<string> candidates = new List<string>();
            string baseDirectory = AppDomain.CurrentDomain.BaseDirectory;

            Add(candidates, Path.Combine(baseDirectory, "HOPE5000.exe"));
            Add(candidates, Path.Combine(baseDirectory, "BIN", "HOPE5000.exe"));
            Add(candidates, @"D:\develop\HOPE5000\BIN\HOPE5000.exe");

            string programFiles = Environment.GetEnvironmentVariable("ProgramFiles");
            string programFilesX86 = Environment.GetEnvironmentVariable("ProgramFiles(x86)");
            if (!string.IsNullOrEmpty(programFiles))
            {
                Add(candidates, Path.Combine(programFiles, "HOPE5000", "BIN", "HOPE5000.exe"));
                Add(candidates, Path.Combine(programFiles, "HOPE5000", "HOPE5000.exe"));
            }
            if (!string.IsNullOrEmpty(programFilesX86))
            {
                Add(candidates, Path.Combine(programFilesX86, "HOPE5000", "BIN", "HOPE5000.exe"));
                Add(candidates, Path.Combine(programFilesX86, "HOPE5000", "HOPE5000.exe"));
            }

            Add(candidates, @"C:\HOPE5000\BIN\HOPE5000.exe");

            foreach (string candidate in candidates)
            {
                if (File.Exists(candidate))
                {
                    return Path.GetFullPath(candidate);
                }
            }

            return null;
        }

        private static void Add(List<string> candidates, string path)
        {
            if (!string.IsNullOrEmpty(path) &&
                !candidates.Exists(delegate(string item)
                {
                    return string.Equals(item, path, StringComparison.OrdinalIgnoreCase);
                }))
            {
                candidates.Add(path);
            }
        }
    }
}
