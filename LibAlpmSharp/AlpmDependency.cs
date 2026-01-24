using System;
using System.Runtime.InteropServices;
using LibAlpmSharp.Interop;

namespace LibAlpmSharp;

/// <summary>
/// Represents a package dependency.
/// </summary>
public sealed class AlpmDependency
{
    /// <summary>
    /// Gets the name of the provider to satisfy this dependency.
    /// </summary>
    public string Name { get; }

    /// <summary>
    /// Gets the version of the provider to match against (optional).
    /// </summary>
    public string? Version { get; }

    /// <summary>
    /// Gets a description of why this dependency is needed (optional).
    /// </summary>
    public string? Description { get; }

    /// <summary>
    /// Gets how the version should match against the provider.
    /// </summary>
    public AlpmDepMod Modifier { get; }

    /// <summary>
    /// Initializes a new instance of the <see cref="AlpmDependency"/> class.
    /// </summary>
    /// <param name="dependPtr">Pointer to the native alpm_depend_t structure.</param>
    internal unsafe AlpmDependency(AlpmDepend* dependPtr)
    {
        if (dependPtr == null)
            throw new ArgumentException("Dependency pointer cannot be null", nameof(dependPtr));

        Name = Marshal.PtrToStringUTF8((IntPtr)dependPtr->name) ?? string.Empty;
        Version = dependPtr->version != null ? Marshal.PtrToStringUTF8((IntPtr)dependPtr->version) : null;
        Description = dependPtr->desc != null ? Marshal.PtrToStringUTF8((IntPtr)dependPtr->desc) : null;
        Modifier = dependPtr->mod;
    }

    /// <summary>
    /// Returns a string that represents the current dependency.
    /// </summary>
    /// <returns>A string that represents the current dependency.</returns>
    public override string ToString()
    {
        if (string.IsNullOrEmpty(Version))
            return Name;

        string modStr = Modifier switch
        {
            AlpmDepMod.ALPM_DEP_MOD_EQ => "=",
            AlpmDepMod.ALPM_DEP_MOD_GE => ">=",
            AlpmDepMod.ALPM_DEP_MOD_LE => "<=",
            AlpmDepMod.ALPM_DEP_MOD_GT => ">",
            AlpmDepMod.ALPM_DEP_MOD_LT => "<",
            _ => ""
        };

        return $"{Name}{modStr}{Version}";
    }
}
