using System.Runtime.InteropServices;

namespace LibAlpmSharp.AlpmInterop
{
    [StructLayout(LayoutKind.Explicit)]
    public partial struct _alpm_question_t
    {
        [FieldOffset(0)]
        [NativeTypeName("alpm_question_type_t")]
        public _alpm_question_type_t type;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_any_t")]
        public _alpm_question_any_t any;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_install_ignorepkg_t")]
        public _alpm_question_install_ignorepkg_t install_ignorepkg;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_replace_t")]
        public _alpm_question_replace_t replace;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_conflict_t")]
        public _alpm_question_conflict_t conflict;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_corrupted_t")]
        public _alpm_question_corrupted_t corrupted;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_remove_pkgs_t")]
        public _alpm_question_remove_pkgs_t remove_pkgs;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_select_provider_t")]
        public _alpm_question_select_provider_t select_provider;

        [FieldOffset(0)]
        [NativeTypeName("alpm_question_import_key_t")]
        public _alpm_question_import_key_t import_key;
    }
}
