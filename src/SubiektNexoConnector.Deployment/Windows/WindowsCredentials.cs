using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Security;
using System.Security.Principal;
using System.Windows;
using System.Windows.Interop;

namespace SubiektNexoConnector.Deployment.Windows;

public sealed class WindowsCredentials : IDisposable
{
    public string Account { get; }
    public string Sid => ((SecurityIdentifier)new NTAccount(Account).Translate(typeof(SecurityIdentifier))).Value;
    private readonly SecureString _password;
    private WindowsCredentials(string account, SecureString password) { Account = account; _password = password; }

    public T Use<T>(Func<string, T> action)
    {
        var pointer = Marshal.SecureStringToGlobalAllocUnicode(_password);
        try { return action(Marshal.PtrToStringUni(pointer)!); }
        finally { Marshal.ZeroFreeGlobalAllocUnicode(pointer); }
    }
    public void Dispose() => _password.Dispose();

    public static WindowsCredentials Prompt(Window owner, string purpose)
    {
        var info = new CredUiInfo
        {
            Size = Marshal.SizeOf<CredUiInfo>(),
            Parent = new WindowInteropHelper(owner).Handle,
            Caption = "SubiektNexoConnector — poświadczenia",
            Message = purpose
        };
        uint package = 0;
        bool save = false;
        var result = CredUIPromptForWindowsCredentials(ref info, 0, ref package, IntPtr.Zero, 0,
            out var buffer, out var size, ref save, 1 /* password credentials; never Windows Hello */);
        if (result == 1223) throw new OperationCanceledException("Anulowano podawanie poświadczeń.");
        if (result != 0) throw new Win32Exception((int)result);
        IntPtr username = IntPtr.Zero, domain = IntPtr.Zero, password = IntPtr.Zero;
        uint u = 0, d = 0, p = 0;
        try
        {
            CredUnPackAuthenticationBuffer(0, buffer, size, IntPtr.Zero, ref u, IntPtr.Zero, ref d, IntPtr.Zero, ref p);
            if (u is 0 or > 32768 || d > 32768 || p is 0 or > 32768)
                throw new InvalidOperationException("Nieobsługiwany format poświadczeń Windows.");
            username = Marshal.AllocHGlobal(checked((int)u * 2));
            domain = Marshal.AllocHGlobal(checked((int)d * 2));
            password = Marshal.AllocHGlobal(checked((int)p * 2));
            if (!CredUnPackAuthenticationBuffer(0, buffer, size, username, ref u, domain, ref d, password, ref p))
                throw new Win32Exception(Marshal.GetLastWin32Error());
            var name = Marshal.PtrToStringUni(username) ?? "";
            var domainName = Marshal.PtrToStringUni(domain) ?? "";
            if (domainName.Length > 0 && !name.Contains('\\') && !name.Contains('@')) name = domainName + "\\" + name;
            var secret = new SecureString();
            for (var index = 0; index < p - 1; index++) secret.AppendChar((char)Marshal.ReadInt16(password, index * 2));
            secret.MakeReadOnly();
            if (secret.Length == 0) { secret.Dispose(); throw new InvalidOperationException("Wymagane jest hasło konta; PIN nie wystarczy."); }
            return new WindowsCredentials(name, secret);
        }
        finally
        {
            ZeroFree(username, u * 2); ZeroFree(domain, d * 2); ZeroFree(password, p * 2);
            for (var index = 0; index < size; index++) Marshal.WriteByte(buffer, index, 0);
            Marshal.FreeCoTaskMem(buffer);
        }
    }
    private static void ZeroFree(IntPtr buffer, uint size)
    {
        if (buffer == IntPtr.Zero) return;
        for (var index = 0; index < size; index++) Marshal.WriteByte(buffer, index, 0);
        Marshal.FreeHGlobal(buffer);
    }
    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct CredUiInfo { public int Size; public IntPtr Parent; public string Message; public string Caption; public IntPtr Banner; }
    [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern uint CredUIPromptForWindowsCredentials(ref CredUiInfo info, uint error, ref uint package,
        IntPtr input, uint inputSize, out IntPtr output, out uint outputSize, [MarshalAs(UnmanagedType.Bool)] ref bool save, uint flags);
    [DllImport("credui.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool CredUnPackAuthenticationBuffer(uint flags, IntPtr buffer, uint size,
        IntPtr user, ref uint userSize, IntPtr domain, ref uint domainSize, IntPtr password, ref uint passwordSize);
}
