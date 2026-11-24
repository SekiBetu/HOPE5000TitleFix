using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Text;
using dnlib.DotNet;
using dnlib.DotNet.Emit;
using dnlib.DotNet.Writer;

namespace HOPE5000TitleFix
{
    public sealed class Inspection
    {
        public string ExePath { get; set; }
        public string AssemblyFullName { get; set; }
        public string CurrentTitle { get; set; }
        public uint TitleOffset { get; set; }
        public string ExeSha256 { get; set; }
        public bool BackupExists { get; set; }
        public string BackupPath { get; set; }
        public string BackupTitle { get; set; }
        public string BackupSha256 { get; set; }
        public string BackupError { get; set; }
        public bool MetadataExists { get; set; }
        public string MetadataPath { get; set; }

        public bool HasCustomTitle
        {
            get { return !string.IsNullOrEmpty(CurrentTitle); }
        }
    }

    public sealed class PatchResult
    {
        public string TargetPath { get; set; }
        public string OldTitle { get; set; }
        public string NewTitle { get; set; }
        public string AssemblyFullName { get; set; }
        public bool BackupCreated { get; set; }
        public string BackupPath { get; set; }
        public string OriginalSha256 { get; set; }
        public string PatchedSha256 { get; set; }
        public string MetadataPath { get; set; }
        public string[] Warnings { get; set; }
    }

    public sealed class RestoreResult
    {
        public string TargetPath { get; set; }
        public string RestoredFrom { get; set; }
        public string RestoredSha256 { get; set; }
        public string[] Warnings { get; set; }
    }

    public static class Patcher
    {
        private const string ViewModelTypeName =
            "HOPE5000.ViewModels.HT8.SmartProgrammingViewModel";
        private const string MethodName = "ClickRange";
        private const string BackupSuffix = ".original.bak";
        private const string MetadataSuffix = ".titlefix.json";
        private const int MaxTitleLength = 120;

        public static void ValidateTitle(string title)
        {
            if (string.IsNullOrWhiteSpace(title))
            {
                throw new ArgumentException("窗口标题不能为空。");
            }
            if (title.Length > MaxTitleLength)
            {
                throw new ArgumentException(
                    "窗口标题过长，请控制在 " + MaxTitleLength + " 个字符以内。");
            }
            if (title.IndexOf('\0') >= 0 ||
                title.IndexOf('\r') >= 0 ||
                title.IndexOf('\n') >= 0)
            {
                throw new ArgumentException("窗口标题不能包含换行符或 NUL 字符。");
            }
        }

        public static Inspection Inspect(string exePath)
        {
            string path = NormalizeExePath(exePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("文件不存在：" + path, path);
            }

            AssemblyName assemblyName = FileSafety.ReadHope5000AssemblyName(path);
            TitleLocation title = ReadTitleLocation(path);
            Inspection result = new Inspection();
            result.ExePath = path;
            result.AssemblyFullName = assemblyName.FullName;
            result.CurrentTitle = title.Title;
            result.TitleOffset = title.Offset;
            result.ExeSha256 = FileSafety.ComputeSha256(path);
            result.BackupPath = GetBackupPath(path);
            result.BackupExists = File.Exists(result.BackupPath);
            result.MetadataPath = GetMetadataPath(path);
            result.MetadataExists = File.Exists(result.MetadataPath);

            if (result.BackupExists)
            {
                try
                {
                    TitleLocation backupTitle = ReadTitleLocation(result.BackupPath);
                    result.BackupTitle = backupTitle.Title;
                    result.BackupSha256 = FileSafety.ComputeSha256(result.BackupPath);
                }
                catch (Exception ex)
                {
                    result.BackupError = ex.Message;
                }
            }

            return result;
        }

        public static PatchResult Patch(string exePath, string newTitle)
        {
            ValidateTitle(newTitle);
            string path = NormalizeExePath(exePath);
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("文件不存在：" + path, path);
            }

            FileSafety.EnsureHope5000NotRunning();
            Inspection before = Inspect(path);
            string backupPath = GetBackupPath(path);
            bool backupCreated = false;

            if (File.Exists(backupPath))
            {
                ValidateBackup(backupPath);
            }
            else
            {
                if (!string.IsNullOrEmpty(before.CurrentTitle))
                {
                    throw new InvalidOperationException(
                        "当前文件的窗口标题已经是“" + before.CurrentTitle +
                        "”，但没有找到原始备份。为避免把已修改文件误当成原版备份，" +
                        "工具已停止操作。请使用原始 HOPE5000.exe 重试。");
                }

                try
                {
                    File.Copy(path, backupPath, false);
                    backupCreated = true;
                    ValidateBackup(backupPath);
                }
                catch (Exception ex)
                {
                    if (backupCreated)
                    {
                        FileSafety.TryDelete(backupPath);
                    }
                    throw new IOException(
                        "无法创建原始备份。请确认 HOPE5000 已退出，" +
                        "目标目录可写且安全软件没有拦截。“" + backupPath + "”", ex);
                }
            }

            string originalHash = FileSafety.ComputeSha256(backupPath);
            string tempPath = FileSafety.CreateTempPath(path, "titlefix-new");
            bool targetReplaced = false;

            try
            {
                WritePatchedAssembly(path, tempPath, newTitle);
                TitleLocation tempTitle = ReadTitleLocation(tempPath);
                if (!string.Equals(tempTitle.Title, newTitle, StringComparison.Ordinal))
                {
                    throw new InvalidOperationException(
                        "临时文件的标题验证失败。原始文件没有被修改。");
                }

                FileSafety.EnsureHope5000NotRunning();
                FileSafety.ReplaceFile(tempPath, path);
                tempPath = null;
                targetReplaced = true;

                Inspection after;
                try
                {
                    after = Inspect(path);
                    if (!string.Equals(after.CurrentTitle, newTitle, StringComparison.Ordinal))
                    {
                        throw new InvalidOperationException("补丁后的标题与预期不一致。");
                    }
                }
                catch (Exception verifyError)
                {
                    string rollbackError = TryRestoreFromBackup(path, backupPath);
                    targetReplaced = false;
                    string suffix = rollbackError == null
                        ? "已自动从原始备份还原。"
                        : "自动还原也失败，请手动用备份覆盖：" + rollbackError;
                    throw new InvalidOperationException(
                        "补丁写入后的验证失败。" + suffix, verifyError);
                }

                targetReplaced = false;
                string patchedHash = FileSafety.ComputeSha256(path);
                List<string> warnings = new List<string>();
                string metadataPath = GetMetadataPath(path);
                try
                {
                    WriteMetadata(
                        metadataPath, path, newTitle, backupPath,
                        originalHash, patchedHash);
                }
                catch (Exception metadataError)
                {
                    warnings.Add(
                        "补丁已成功，但写入说明文件失败：" + metadataError.Message);
                }

                PatchResult result = new PatchResult();
                result.TargetPath = path;
                result.OldTitle = before.CurrentTitle;
                result.NewTitle = newTitle;
                result.AssemblyFullName = before.AssemblyFullName;
                result.BackupCreated = backupCreated;
                result.BackupPath = backupPath;
                result.OriginalSha256 = originalHash;
                result.PatchedSha256 = patchedHash;
                result.MetadataPath = metadataPath;
                result.Warnings = warnings.ToArray();
                return result;
            }
            catch
            {
                if (targetReplaced)
                {
                    TryRestoreFromBackup(path, backupPath);
                }
                throw;
            }
            finally
            {
                FileSafety.TryDelete(tempPath);
            }
        }

        public static RestoreResult Restore(string exePath)
        {
            string path = NormalizeExePath(exePath);
            string backupPath = GetBackupPath(path);
            if (!File.Exists(backupPath))
            {
                throw new FileNotFoundException(
                    "未找到原始备份：" + backupPath + "。无法执行还原。", backupPath);
            }

            FileSafety.EnsureHope5000NotRunning();
            ValidateBackup(backupPath);
            string backupHash = FileSafety.ComputeSha256(backupPath);
            string restoreTemp = FileSafety.CreateTempPath(path, "titlefix-restore");

            try
            {
                File.Copy(backupPath, restoreTemp, true);
                if (!string.Equals(
                    FileSafety.ComputeSha256(restoreTemp),
                    backupHash,
                    StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "还原临时文件校验失败，目标文件没有被修改。");
                }

                TitleLocation restoreTitle = ReadTitleLocation(restoreTemp);
                if (!string.IsNullOrEmpty(restoreTitle.Title))
                {
                    throw new InvalidOperationException(
                        "备份中的标题不是原始空值，已停止还原。");
                }

                FileSafety.EnsureHope5000NotRunning();
                FileSafety.ReplaceFile(restoreTemp, path);
                restoreTemp = null;

                if (!File.Exists(path))
                {
                    throw new InvalidOperationException("还原后未找到目标文件。");
                }

                string restoredHash = FileSafety.ComputeSha256(path);
                if (!string.Equals(
                    restoredHash, backupHash, StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "还原后的文件哈希与备份不一致。");
                }

                TitleLocation restoredTitle = ReadTitleLocation(path);
                if (!string.IsNullOrEmpty(restoredTitle.Title))
                {
                    throw new InvalidOperationException(
                        "还原后的标题仍非空，请检查目标文件。");
                }

                List<string> warnings = new List<string>();
                string metadataPath = GetMetadataPath(path);
                try
                {
                    if (File.Exists(metadataPath))
                    {
                        File.Delete(metadataPath);
                    }
                }
                catch (Exception metadataError)
                {
                    warnings.Add(
                        "文件已还原，但说明文件未能删除：" + metadataError.Message);
                }

                RestoreResult result = new RestoreResult();
                result.TargetPath = path;
                result.RestoredFrom = backupPath;
                result.RestoredSha256 = restoredHash;
                result.Warnings = warnings.ToArray();
                return result;
            }
            finally
            {
                FileSafety.TryDelete(restoreTemp);
            }
        }

        private static string NormalizeExePath(string exePath)
        {
            string path = FileSafety.NormalizePath(exePath);
            if (!string.Equals(
                Path.GetExtension(path), ".exe", StringComparison.OrdinalIgnoreCase))
            {
                throw new ArgumentException("目标文件必须是 .exe 可执行文件。");
            }
            return path;
        }

        private static void ValidateBackup(string backupPath)
        {
            TitleLocation backupTitle;
            try
            {
                backupTitle = ReadTitleLocation(backupPath);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "现有备份无法验证为 HOPE5000 原始文件，为安全起见已停止操作：" +
                    backupPath, ex);
            }

            if (!string.IsNullOrEmpty(backupTitle.Title))
            {
                throw new InvalidOperationException(
                    "备份中的窗口标题不是空值，可能不是原始文件。为安全起见已停止操作：" +
                    backupPath);
            }
        }

        private static TitleLocation ReadTitleLocation(string path)
        {
            ModuleDefMD module = null;
            try
            {
                FileSafety.ReadHope5000AssemblyName(path);
                module = ModuleDefMD.Load(path);
                MethodDef method = FindClickRangeMethod(module);
                return FindTitleInstruction(method);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "无法解析目标程序集。文件可能不是受支持的 HOPE5000 版本：" +
                    path, ex);
            }
            finally
            {
                if (module != null)
                {
                    module.Dispose();
                }
            }
        }

        private static void WritePatchedAssembly(
            string sourcePath, string destinationPath, string newTitle)
        {
            ModuleDefMD module = null;
            try
            {
                module = ModuleDefMD.Load(sourcePath);
                MethodDef method = FindClickRangeMethod(module);
                TitleLocation target = FindTitleInstruction(method);
                target.Instruction.Operand = newTitle;

                ModuleWriterOptions options = new ModuleWriterOptions(module);
                module.Write(destinationPath, options);
            }
            catch (InvalidOperationException)
            {
                throw;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "生成补丁文件失败。目标版本可能不兼容，原始文件没有被修改。", ex);
            }
            finally
            {
                if (module != null)
                {
                    module.Dispose();
                }
            }
        }

        private static MethodDef FindClickRangeMethod(ModuleDefMD module)
        {
            TypeDef targetType = null;
            foreach (TypeDef type in module.GetTypes())
            {
                if (string.Equals(type.FullName, ViewModelTypeName, StringComparison.Ordinal))
                {
                    targetType = type;
                    break;
                }
            }

            if (targetType == null)
            {
                throw new InvalidOperationException(
                    "未找到 " + ViewModelTypeName + " 类型，目标版本不兼容。");
            }

            foreach (MethodDef method in targetType.Methods)
            {
                if (string.Equals(method.Name.String, MethodName, StringComparison.Ordinal))
                {
                    if (method.Body == null)
                    {
                        throw new InvalidOperationException(MethodName + " 方法没有 IL 方法体。");
                    }
                    return method;
                }
            }

            throw new InvalidOperationException(
                "未找到 " + MethodName + " 方法，目标版本不兼容。");
        }

        private static TitleLocation FindTitleInstruction(MethodDef method)
        {
            IList<Instruction> instructions = method.Body.Instructions;
            for (int marker = 0; marker < instructions.Count; marker++)
            {
                if (instructions[marker].OpCode.Code != Code.Ldstr ||
                    !string.Equals(
                        instructions[marker].Operand as string,
                        "Title",
                        StringComparison.Ordinal))
                {
                    continue;
                }

                int limit = Math.Min(instructions.Count - 1, marker + 96);
                for (int index = marker + 1; index <= limit; index++)
                {
                    Instruction candidate = instructions[index];
                    if (candidate.OpCode.Code != Code.Ldstr ||
                        index + 1 >= instructions.Count ||
                        !IsDynamicInvoke(instructions[index + 1]) ||
                        !ContainsSetMember(instructions, marker + 1, index - 1))
                    {
                        continue;
                    }

                    TitleLocation location = new TitleLocation();
                    location.Title = candidate.Operand as string;
                    location.Offset = candidate.Offset;
                    location.Instruction = candidate;
                    return location;
                }
            }

            throw new InvalidOperationException(
                "未找到 ClickRange 中设置窗口标题的 IL 序列。目标版本可能不兼容。" +
                "为避免误修改，工具不会按固定地址强行打补丁。");
        }

        private static bool IsDynamicInvoke(Instruction instruction)
        {
            if (instruction.OpCode.Code != Code.Callvirt)
            {
                return false;
            }

            IMethod method = instruction.Operand as IMethod;
            return method != null &&
                string.Equals(method.Name.String, "Invoke", StringComparison.Ordinal);
        }

        private static bool ContainsSetMember(
            IList<Instruction> instructions, int start, int end)
        {
            for (int index = start; index <= end; index++)
            {
                IMethod method = instructions[index].Operand as IMethod;
                if (method != null &&
                    string.Equals(method.Name.String, "SetMember", StringComparison.Ordinal))
                {
                    return true;
                }
            }
            return false;
        }

        private static string TryRestoreFromBackup(string targetPath, string backupPath)
        {
            string tempPath = null;
            try
            {
                tempPath = FileSafety.CreateTempPath(targetPath, "titlefix-rollback");
                File.Copy(backupPath, tempPath, true);
                FileSafety.ReplaceFile(tempPath, targetPath);
                tempPath = null;
                return null;
            }
            catch (Exception ex)
            {
                return ex.Message;
            }
            finally
            {
                FileSafety.TryDelete(tempPath);
            }
        }

        private static string GetBackupPath(string path)
        {
            return path + BackupSuffix;
        }

        private static string GetMetadataPath(string path)
        {
            return path + MetadataSuffix;
        }

        private static void WriteMetadata(
            string metadataPath,
            string targetPath,
            string title,
            string backupPath,
            string originalHash,
            string patchedHash)
        {
            StringBuilder json = new StringBuilder();
            json.AppendLine("{");
            json.AppendLine("  \"schema\": 1,");
            json.AppendLine("  \"toolVersion\": \"1.0.0\",");
            json.AppendLine("  \"patchedAtUtc\": \"" +
                DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture) + "\",");
            json.AppendLine("  \"target\": \"" + EscapeJson(targetPath) + "\",");
            json.AppendLine("  \"windowTitle\": \"" + EscapeJson(title) + "\",");
            json.AppendLine("  \"backup\": \"" + EscapeJson(backupPath) + "\",");
            json.AppendLine("  \"originalSha256\": \"" + originalHash + "\",");
            json.AppendLine("  \"patchedSha256\": \"" + patchedHash + "\"");
            json.AppendLine("}");

            File.WriteAllText(
                metadataPath, json.ToString(), new UTF8Encoding(false));
        }

        private static string EscapeJson(string value)
        {
            if (value == null)
            {
                return string.Empty;
            }

            StringBuilder escaped = new StringBuilder(value.Length + 16);
            foreach (char character in value)
            {
                switch (character)
                {
                    case '\\':
                        escaped.Append("\\\\");
                        break;
                    case '"':
                        escaped.Append("\\\"");
                        break;
                    case '\r':
                        escaped.Append("\\r");
                        break;
                    case '\n':
                        escaped.Append("\\n");
                        break;
                    case '\t':
                        escaped.Append("\\t");
                        break;
                    default:
                        if (character < 0x20)
                        {
                            escaped.Append("\\u");
                            escaped.Append(((int)character).ToString("x4"));
                        }
                        else
                        {
                            escaped.Append(character);
                        }
                        break;
                }
            }
            return escaped.ToString();
        }

        private sealed class TitleLocation
        {
            public string Title;
            public uint Offset;
            public Instruction Instruction;
        }
    }
}
