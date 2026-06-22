using System.Runtime.InteropServices;

namespace HMailServer.ComInterop;

[ComVisible(true)]
[Guid("E5EDC050-0899-4A3B-BF4C-420212FC3895")]
[InterfaceType(ComInterfaceType.InterfaceIsDual)]
[TypeLibType(TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable)]
public interface IInterfaceAccount
{
    [DispId(1)]
    bool Active
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(2)]
    string ADDomain { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(3)]
    string Address { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(4)]
    int DomainID { get; set; }

    [DispId(5)]
    int ID { get; }

    [DispId(6)]
    bool IsAD
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(7)]
    string Password { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(8)]
    float Size { get; }

    [DispId(9)]
    void Save();

    [DispId(10)]
    string ADUsername { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(11)]
    void DeleteMessages();

    [DispId(13)]
    IInterfaceMessages Messages { get; }

    [DispId(15)]
    int MaxSize { get; set; }

    [DispId(16)]
    bool VacationMessageIsOn
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(17)]
    string VacationMessage { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(18)]
    string VacationSubject { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(19)]
    IInterfaceFetchAccounts FetchAccounts { get; }

    [DispId(20)]
    ComAdminLevel AdminLevel { get; set; }

    [DispId(21)]
    IInterfaceRules Rules { get; }

    [DispId(22)]
    [return: MarshalAs(UnmanagedType.VariantBool)]
    bool ValidatePassword([MarshalAs(UnmanagedType.BStr)] string password);

    [DispId(23)]
    void UnlockMailbox();

    [DispId(24)]
    IInterfaceIMAPFolders IMAPFolders { get; }

    [DispId(25)]
    int QuotaUsed { get; }

    [DispId(26)]
    bool ForwardEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(27)]
    string ForwardAddress { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(28)]
    bool ForwardKeepOriginal
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(29)]
    bool SignatureEnabled
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(30)]
    string SignaturePlainText { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(31)]
    string SignatureHTML { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(32)]
    object LastLogonTime { [return: MarshalAs(UnmanagedType.Struct)] get; }

    [DispId(33)]
    bool VacationMessageExpires
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(34)]
    string VacationMessageExpiresDate { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(35)]
    string PersonFirstName { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(36)]
    string PersonLastName { [return: MarshalAs(UnmanagedType.BStr)] get; [param: MarshalAs(UnmanagedType.BStr)] set; }

    [DispId(37)]
    void Delete();

    [DispId(38)]
    bool VacationMessageAbortSpamFlagged
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }

    [DispId(39)]
    bool ForwardAbortSpamFlagged
    {
        [return: MarshalAs(UnmanagedType.VariantBool)] get;
        [param: MarshalAs(UnmanagedType.VariantBool)] set;
    }
}
