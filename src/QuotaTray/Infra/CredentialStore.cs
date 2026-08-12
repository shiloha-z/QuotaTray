using System.Runtime.InteropServices;
using System.Text;

namespace QuotaTray.Infra;

internal static class CredentialTargets
{
    public const string ChatGptCookies = "AgentUsageChecker/ChatGptCookies";
    public const string ZenJwt = "AgentUsageChecker/ZenJwt";
}

internal static class CredentialStore
{
    private const int MaxBlobBytes = 2560;
    private const int MaxParts = 64;

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct NativeCredential
    {
        public int Flags;
        public int Type;
        public IntPtr TargetName;
        public IntPtr Comment;
        public long LastWritten;
        public uint CredentialBlobSize;
        public IntPtr CredentialBlob;
        public uint Persist;
        public uint AttributeCount;
        public IntPtr Attributes;
        public IntPtr TargetAlias;
        public IntPtr UserName;
    }

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredRead(string target, int type, int flags, out IntPtr credential);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredWrite(ref NativeCredential credential, uint flags);

    [DllImport("advapi32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern bool CredDelete(string target, int type, int flags);

    [DllImport("advapi32.dll")]
    private static extern void CredFree(IntPtr buffer);

    private const int TypeGeneric = 1;
    private const uint PersistLocalMachine = 2;

    public static void Save(string target, string value)
    {
        var bytes = Encoding.Unicode.GetBytes(value);

        for (var i = 0; i < MaxParts; i++)
        {
            DeleteTarget(target + "#" + i);
        }

        if (bytes.Length <= MaxBlobBytes)
        {
            WritePart(target, bytes);
            DeleteTarget(target + "#count");
            return;
        }

        var partCount = (bytes.Length + MaxBlobBytes - 1) / MaxBlobBytes;
        for (var i = 0; i < partCount; i++)
        {
            var length = Math.Min(MaxBlobBytes, bytes.Length - i * MaxBlobBytes);
            var part = new byte[length];
            Array.Copy(bytes, i * MaxBlobBytes, part, 0, length);
            WritePart(target + "#" + i, part);
        }

        WritePart(target + "#count", Encoding.Unicode.GetBytes(partCount.ToString()));
    }

    public static string? Read(string target)
    {
        var countText = ReadPart(target + "#count");
        if (countText is not null && int.TryParse(countText, out var partCount))
        {
            var builder = new StringBuilder();
            for (var i = 0; i < partCount; i++)
            {
                var part = ReadPart(target + "#" + i);
                if (part is null)
                {
                    return null;
                }

                builder.Append(part);
            }

            return builder.ToString();
        }

        return ReadPart(target);
    }

    /// <summary>ADR-002 存量迁移：历史版本曾持久化 cookie 串 / "ok:workspaceId" 等内容，
    /// 现仅保留 "ok" 标记。值非 "ok" 时删除旧内容（含遗留 #n 分段）并重写标记，
    /// 覆盖从不重新登录的用户；重新登录路径由 Save 的分段清理兜底。</summary>
    public static void MigrateToMarker(string target)
    {
        var value = Read(target);
        if (value is null || value == "ok")
        {
            return;
        }

        Delete(target);
        Save(target, "ok");
        Logger.Log($"CREDENTIAL migrated {target}: legacy content -> marker");
    }

    public static void Delete(string target)
    {
        for (var i = 0; i < MaxParts; i++)
        {
            DeleteTarget(target + "#" + i);
        }

        DeleteTarget(target + "#count");
    }

    private static void DeleteTarget(string target)
    {
        CredDelete(target, TypeGeneric, 0);
    }

    private static void WritePart(string target, byte[] bytes)
    {
        var credential = new NativeCredential
        {
            Type = TypeGeneric,
            TargetName = Marshal.StringToCoTaskMemUni(target),
            CredentialBlobSize = (uint)bytes.Length,
            CredentialBlob = Marshal.AllocCoTaskMem(bytes.Length),
            Persist = PersistLocalMachine,
            UserName = Marshal.StringToCoTaskMemUni(Environment.UserName),
        };
        Marshal.Copy(bytes, 0, credential.CredentialBlob, bytes.Length);

        try
        {
            if (!CredWrite(ref credential, 0))
            {
                throw new InvalidOperationException($"CredWrite failed: {Marshal.GetLastWin32Error()}");
            }
        }
        finally
        {
            Marshal.FreeCoTaskMem(credential.TargetName);
            Marshal.FreeCoTaskMem(credential.CredentialBlob);
            Marshal.FreeCoTaskMem(credential.UserName);
        }
    }

    private static string? ReadPart(string target)
    {
        if (!CredRead(target, TypeGeneric, 0, out var ptr))
        {
            return null;
        }

        try
        {
            var credential = Marshal.PtrToStructure<NativeCredential>(ptr);
            if (credential.CredentialBlob == IntPtr.Zero || credential.CredentialBlobSize == 0)
            {
                return null;
            }

            var bytes = new byte[credential.CredentialBlobSize];
            Marshal.Copy(credential.CredentialBlob, bytes, 0, bytes.Length);
            return Encoding.Unicode.GetString(bytes);
        }
        finally
        {
            CredFree(ptr);
        }
    }
}
