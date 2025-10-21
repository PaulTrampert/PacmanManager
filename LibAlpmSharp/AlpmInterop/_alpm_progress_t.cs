namespace LibAlpmSharp.AlpmInterop
{
    [NativeTypeName("unsigned int")]
    public enum _alpm_progress_t : uint
    {
        ALPM_PROGRESS_ADD_START,
        ALPM_PROGRESS_UPGRADE_START,
        ALPM_PROGRESS_DOWNGRADE_START,
        ALPM_PROGRESS_REINSTALL_START,
        ALPM_PROGRESS_REMOVE_START,
        ALPM_PROGRESS_CONFLICTS_START,
        ALPM_PROGRESS_DISKSPACE_START,
        ALPM_PROGRESS_INTEGRITY_START,
        ALPM_PROGRESS_LOAD_START,
        ALPM_PROGRESS_KEYRING_START,
    }
}
