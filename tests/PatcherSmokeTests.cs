using System;
using System.IO;
using System.Security.Cryptography;
using HOPE5000TitleFix;

internal static class PatcherSmokeTests
{
    private static int _passed;

    private static int Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.Error.WriteLine(
                "Usage: PatcherSmokeTests.exe <path-to-original-HOPE5000.exe>");
            return 2;
        }

        string source = Path.GetFullPath(args[0]);
        if (!File.Exists(source))
        {
            Console.Error.WriteLine("Source file not found: " + source);
            return 2;
        }

        string scratch = Path.Combine(
            Path.GetTempPath(),
            "HOPE5000TitleFixTests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(scratch);

        try
        {
            RunTests(source, scratch);
            Console.WriteLine("PASS: all " + _passed + " checks succeeded.");
            return 0;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine("FAIL: " + ex.Message);
            Console.Error.WriteLine(ex.ToString());
            return 1;
        }
        finally
        {
            try
            {
                Directory.Delete(scratch, true);
            }
            catch
            {
            }
        }
    }

    private static void RunTests(string source, string scratch)
    {
        string baselineHash = HashFile(source);
        string target = Path.Combine(scratch, "HOPE5000.exe");
        File.Copy(source, target, false);

        Inspection original = Patcher.Inspect(target);
        Check(original.CurrentTitle == string.Empty, "source title must be empty");
        Check(original.AssemblyFullName.StartsWith("HOPE5000,"), "assembly name must be HOPE5000");
        Check(!original.BackupExists, "new copy must not have a backup");
        Check(original.ExeSha256 == baselineHash, "source hash must match");

        ExpectFailure(delegate { Patcher.ValidateTitle(""); }, "不能为空");
        ExpectFailure(delegate { Patcher.ValidateTitle("line\r\nbreak"); }, "换行符");
        ExpectFailure(delegate { Patcher.ValidateTitle(new string('x', 121)); }, "120");

        string firstTitle = "HOPE5000";
        PatchResult first = Patcher.Patch(target, firstTitle);
        Check(first.BackupCreated, "first patch must create a backup");
        Check(first.OriginalSha256 == baselineHash, "backup hash must equal baseline");
        Check(File.Exists(target + ".original.bak"), "backup file must exist");
        Check(File.Exists(target + ".titlefix.json"), "metadata file must exist");
        Check(HashFile(target + ".original.bak") == baselineHash, "backup bytes must be original");

        Inspection firstInspection = Patcher.Inspect(target);
        Check(firstInspection.CurrentTitle == firstTitle, "first title must be readable");
        Check(firstInspection.BackupTitle == string.Empty, "backup title must stay empty");

        string secondTitle = "HOPE5000 测试 \"A\"";
        string patchedHash = HashFile(target);
        PatchResult second = Patcher.Patch(target, secondTitle);
        Check(!second.BackupCreated, "second patch must reuse the original backup");
        Check(second.OriginalSha256 == baselineHash, "second patch must keep original hash");
        Check(second.PatchedSha256 != patchedHash, "second patch must change the file hash");
        Check(Patcher.Inspect(target).CurrentTitle == secondTitle, "second title must be readable");

        string metadata = File.ReadAllText(target + ".titlefix.json");
        Check(metadata.Contains("\\\"A\\\""), "metadata must escape quotes");

        string patchedCopy = Path.Combine(scratch, "patched-copy.exe");
        File.Copy(target, patchedCopy, false);

        string prePatched = Path.Combine(scratch, "pre-patched.exe");
        File.Copy(patchedCopy, prePatched, false);
        ExpectFailure(
            delegate { Patcher.Patch(prePatched, "HOPE5000"); },
            "没有找到原始备份");

        string guardedTarget = Path.Combine(scratch, "guarded.exe");
        File.Copy(source, guardedTarget, false);
        File.Copy(patchedCopy, guardedTarget + ".original.bak", false);
        ExpectFailure(
            delegate { Patcher.Patch(guardedTarget, "HOPE5000"); },
            "备份中的窗口标题不是空值");

        RestoreResult restored = Patcher.Restore(target);
        Check(restored.RestoredSha256 == baselineHash, "restore must report baseline hash");
        Check(HashFile(target) == baselineHash, "restored file must be byte-identical");
        Check(Patcher.Inspect(target).CurrentTitle == string.Empty, "restored title must be empty");
        Check(!File.Exists(target + ".titlefix.json"), "metadata must be removed after restore");
        Check(File.Exists(target + ".original.bak"), "restore must keep the backup");

        string fake = Path.Combine(scratch, "NotHope.exe");
        File.Copy(typeof(PatcherSmokeTests).Assembly.Location, fake, false);
        ExpectFailure(
            delegate { Patcher.Inspect(fake); },
            "目标不是 HOPE5000");
    }

    private static void ExpectFailure(Action action, string expectedText)
    {
        try
        {
            action();
        }
        catch (Exception ex)
        {
            Check(
                ex.Message.IndexOf(expectedText, StringComparison.Ordinal) >= 0,
                "expected error containing \"" + expectedText + "\", got \"" + ex.Message + "\"");
            return;
        }

        throw new InvalidOperationException(
            "expected operation to fail with text \"" + expectedText + "\"");
    }

    private static void Check(bool condition, string message)
    {
        if (!condition)
        {
            throw new InvalidOperationException(message);
        }
        _passed++;
    }

    private static string HashFile(string path)
    {
        using (FileStream stream = File.OpenRead(path))
        using (SHA256 sha = SHA256.Create())
        {
            return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", string.Empty);
        }
    }
}
