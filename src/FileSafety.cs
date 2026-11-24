using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;

namespace HOPE5000TitleFix
{
    internal static class FileSafety
    {
        internal static string NormalizePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                throw new ArgumentException("请选择 HOPE5000.exe 文件。", "path");
            }

            string trimmed = path.Trim();
            if (trimmed.Length >= 2 && trimmed[0] == '"' && trimmed[trimmed.Length - 1] == '"')
            {
                trimmed = trimmed.Substring(1, trimmed.Length - 2).Trim();
            }

            if (trimmed.Length == 0)
            {
                throw new ArgumentException("路径不能为空。", "path");
            }

            return Path.GetFullPath(trimmed);
        }

        internal static AssemblyName ReadHope5000AssemblyName(string path)
        {
            AssemblyName assemblyName;
            try
            {
                assemblyName = AssemblyName.GetAssemblyName(path);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "无法读取目标程序集，请确认这是一个有效的 .NET 可执行文件。", ex);
            }

            if (!string.Equals(assemblyName.Name, "HOPE5000", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "目标不是 HOPE5000 程序集（读取到的程序集名为“" +
                    assemblyName.Name + "”）。");
            }

            return assemblyName;
        }

        internal static void EnsureHope5000NotRunning()
        {
            List<string> names = new List<string>();
            Process[] processes = Process.GetProcesses();
            try
            {
                foreach (Process process in processes)
                {
                    try
                    {
                        if (process.ProcessName.StartsWith("HOPE5000", StringComparison.OrdinalIgnoreCase) &&
                            !names.Contains(process.ProcessName))
                        {
                            names.Add(process.ProcessName);
                        }
                    }
                    catch
                    {
                    }
                    finally
                    {
                        process.Dispose();
                    }
                }
            }
            finally
            {
                for (int i = 0; i < processes.Length; i++)
                {
                    processes[i] = null;
                }
            }

            if (names.Count > 0)
            {
                throw new InvalidOperationException(
                    "检测到 HOPE5000 正在运行（" + string.Join("、", names.ToArray()) +
                    "）。请先从托盘和任务管理器完全退出程序，再执行操作。");
            }
        }

        internal static string ComputeSha256(string path)
        {
            using (FileStream stream = new FileStream(
                path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
            using (SHA256 sha256 = SHA256.Create())
            {
                byte[] hash = sha256.ComputeHash(stream);
                return BitConverter.ToString(hash).Replace("-", string.Empty);
            }
        }

        internal static void ReplaceFile(string sourcePath, string targetPath)
        {
            Exception replaceError = null;
            try
            {
                if (File.Exists(targetPath))
                {
                    File.Replace(sourcePath, targetPath, null, true);
                    return;
                }
            }
            catch (IOException ex)
            {
                replaceError = ex;
            }
            catch (PlatformNotSupportedException ex)
            {
                replaceError = ex;
            }
            catch (UnauthorizedAccessException ex)
            {
                replaceError = ex;
            }

            try
            {
                File.Copy(sourcePath, targetPath, true);
                File.Delete(sourcePath);
            }
            catch (Exception copyError)
            {
                string detail = replaceError == null
                    ? copyError.Message
                    : replaceError.Message + "；" + copyError.Message;
                throw new IOException(
                    "无法替换目标文件。请检查文件只读属性、权限以及安全软件拦截。" +
                    "详细信息：" + detail, copyError);
            }
        }

        internal static string CreateTempPath(string targetPath, string suffix)
        {
            string directory = Path.GetDirectoryName(targetPath);
            if (string.IsNullOrEmpty(directory))
            {
                throw new InvalidOperationException("无法确定目标文件所在目录。");
            }

            return Path.Combine(
                directory,
                Path.GetFileName(targetPath) + "." + suffix + "." +
                Guid.NewGuid().ToString("N") + ".tmp");
        }

        internal static void TryDelete(string path)
        {
            if (string.IsNullOrEmpty(path))
            {
                return;
            }

            try
            {
                if (File.Exists(path))
                {
                    File.Delete(path);
                }
            }
            catch
            {
            }
        }
    }
}
