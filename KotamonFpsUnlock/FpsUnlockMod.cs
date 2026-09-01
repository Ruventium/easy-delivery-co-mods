using System.Runtime.InteropServices;
using MelonLoader;

[assembly: MelonInfo(typeof(KotamonFpsUnlock.FpsUnlockMod), "Kotamon FPS Unlock", "1.0.0", "OpenCode")]
[assembly: MelonGame("KotaMota Games", "Kotamon")]

namespace KotamonFpsUnlock;

public sealed class FpsUnlockMod : MelonMod
{
    private const int TargetFrameRate = 240;

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate nint ResolveInternalCall([MarshalAs(UnmanagedType.LPStr)] string name);

    [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
    private delegate void SetInt(int value);

    public override void OnPreSupportModule()
    {
        MelonLogger.Msg("Applying 240 FPS settings before support-module initialization.");
        nint gameAssembly = System.Runtime.InteropServices.NativeLibrary.Load("GameAssembly.dll");
        nint resolveAddress = System.Runtime.InteropServices.NativeLibrary.GetExport(gameAssembly, "il2cpp_resolve_icall");
        var resolve = Marshal.GetDelegateForFunctionPointer<ResolveInternalCall>(resolveAddress);

        Set(resolve, "UnityEngine.QualitySettings::set_vSyncCount(System.Int32)", 0);
        Set(resolve, "UnityEngine.Application::set_targetFrameRate(System.Int32)", TargetFrameRate);
        MelonLogger.Msg($"VSync disabled; target frame rate set to {TargetFrameRate} FPS.");
    }

    private static void Set(ResolveInternalCall resolve, string name, int value)
    {
        nint address = resolve(name);
        if (address == 0)
            throw new MissingMethodException($"IL2CPP internal call not found: {name}");

        Marshal.GetDelegateForFunctionPointer<SetInt>(address)(value);
    }
}
