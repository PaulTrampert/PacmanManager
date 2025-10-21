namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_package_operation_t : uint
    {
        ALPM_PACKAGE_INSTALL = 1,
        ALPM_PACKAGE_UPGRADE,
        ALPM_PACKAGE_REINSTALL,
        ALPM_PACKAGE_DOWNGRADE,
        ALPM_PACKAGE_REMOVE,
    }
}
