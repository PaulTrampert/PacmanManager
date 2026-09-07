using System.Runtime.InteropServices;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

/// <summary>
/// Compares pacman version strings the way pacman itself does.
/// </summary>
/// <remarks>
/// A pacman version is <c>[epoch:]pkgver[-pkgrel]</c>, and ordering it is not string ordering:
/// <c>1.10</c> is newer than <c>1.9</c>, <c>1.0-2</c> is newer than <c>1.0-1</c>, and an epoch
/// outranks everything to its right. Rather than restate those rules, this hands both strings to
/// libalpm's own <c>alpm_pkg_vercmp</c>, which is the same comparison a pacman client will make
/// about the same two packages. It needs no handle, so unlike the rest of this binding it is a
/// static function rather than a member of <see cref="ILibAlpm"/>.
/// </remarks>
public static class AlpmVersion
{
    /// <summary>
    /// Compares two version strings.
    /// </summary>
    /// <param name="a">The first version.</param>
    /// <param name="b">The second version.</param>
    /// <returns>
    /// A negative number when <paramref name="a"/> is older than <paramref name="b"/>, zero when
    /// they describe the same version, and a positive number when <paramref name="a"/> is newer.
    /// </returns>
    /// <exception cref="ArgumentException">Either version is null or empty.</exception>
    public static int Compare(string a, string b)
    {
        ArgumentException.ThrowIfNullOrEmpty(a);
        ArgumentException.ThrowIfNullOrEmpty(b);

        var aPtr = IntPtr.Zero;
        var bPtr = IntPtr.Zero;
        try
        {
            aPtr = Marshal.StringToHGlobalAnsi(a);
            bPtr = Marshal.StringToHGlobalAnsi(b);

            unsafe
            {
                return NativeMethods.alpm_pkg_vercmp((byte*)aPtr.ToPointer(), (byte*)bPtr.ToPointer());
            }
        }
        finally
        {
            if (aPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(aPtr);
            }

            if (bPtr != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(bPtr);
            }
        }
    }

    /// <summary>
    /// Whether <paramref name="candidate"/> would upgrade <paramref name="current"/>.
    /// </summary>
    /// <param name="candidate">The version being offered.</param>
    /// <param name="current">The version already there.</param>
    /// <returns>True when <paramref name="candidate"/> is strictly newer.</returns>
    /// <exception cref="ArgumentException">Either version is null or empty.</exception>
    public static bool IsNewerThan(string candidate, string current) => Compare(candidate, current) > 0;
}
