using System.Reflection;
using System.Runtime.InteropServices;
using HMailServer.ComInterop;
using HMailServer.Core.Abstractions;

namespace HMailServer.Net10.Tests;

[TestClass]
public sealed class MessagesComContractTests
{
    private const int EAccessDenied = unchecked((int)0x80070005);
    private const int ENotImplemented = unchecked((int)0x80004001);
    private const int DispEBadIndex = unchecked((int)0x8002000B);

    [TestMethod]
    public void Interfaces_PreserveLegacyIidsDispatchIdsAndCompleteVtableOrder()
    {
        AssertContract(
            typeof(IInterfaceMessages),
            "1259E989-465E-4B63-BB0B-4DB7F6244ACE",
            new[] { "get_Item", "get_Count", "get_ItemByDBID", "DeleteByDBID", "Add", "Clear" });
        Assert.AreEqual(
            0,
            typeof(IInterfaceMessages).GetProperty("Item")?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            5,
            typeof(IInterfaceMessages).GetMethod(nameof(IInterfaceMessages.Clear))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);

        AssertContract(
            typeof(IInterfaceMessage),
            "8C054031-7B42-485C-BF79-3D94A7B9605F",
            new[]
            {
                "get_ID", "get_Filename", "get_Subject", "set_Subject", "get_From", "set_From",
                "get_Date", "set_Date", "get_Body", "set_Body", "get_HTMLBody", "set_HTMLBody",
                "get_Attachments", "Save", "get_To", "AddRecipient", "get_FromAddress",
                "set_FromAddress", "get_State", "get_Size", "ClearRecipients", "get_CC",
                "get_Recipients", "get_HeaderValue", "set_HeaderValue", "HasBodyType",
                "get_EncodeFields", "set_EncodeFields", "get_Flag", "set_Flag", "get_InternalDate",
                "get_Headers", "RefreshContent", "get_DeliveryAttempt", "get_Charset", "set_Charset",
                "Copy", "get_UID"
            });
        Assert.AreEqual(
            28,
            typeof(IInterfaceMessage).GetProperty(nameof(IInterfaceMessage.UID))
                ?.GetCustomAttribute<DispIdAttribute>()?.Value);
        Assert.AreEqual(
            UnmanagedType.Struct,
            typeof(IInterfaceMessage).GetProperty(nameof(IInterfaceMessage.InternalDate))
                ?.GetMethod?.ReturnParameter.GetCustomAttribute<MarshalAsAttribute>()?.Value);
    }

    [TestMethod]
    public void ComClasses_PreserveLegacyIdentitiesAndDefaultInterfaces()
    {
        AssertComClass<Messages>(
            "C04047AD-45A4-48EA-907E-2C270C95409C",
            "hMailServer.Messages.1",
            typeof(IInterfaceMessages));
        AssertComClass<Message>(
            "61B2C7D7-3814-441F-9574-EE2CC9829447",
            "hMailServer.Message.1",
            typeof(IInterfaceMessage));
    }

    [TestMethod]
    public void MessageFlagEnum_PreservesLegacyValuesAndGuid()
    {
        Assert.AreEqual(new Guid("90745436-4C3F-11D9-AD17-A0BCEA20CD09"), typeof(ComMessageFlag).GUID);
        var values = Enum.GetNames<ComMessageFlag>()
            .ToDictionary(
                static name => name,
                static name => Convert.ToInt32(Enum.Parse<ComMessageFlag>(name)));

        Assert.AreEqual(1, values[nameof(ComMessageFlag.Seen)]);
        Assert.AreEqual(2, values[nameof(ComMessageFlag.Deleted)]);
        Assert.AreEqual(4, values[nameof(ComMessageFlag.Flagged)]);
        Assert.AreEqual(8, values[nameof(ComMessageFlag.Answered)]);
        Assert.AreEqual(16, values[nameof(ComMessageFlag.Draft)]);
        Assert.AreEqual(32, values[nameof(ComMessageFlag.Recent)]);
        Assert.AreEqual(64, values[nameof(ComMessageFlag.VirusScan)]);
        Assert.AreEqual(128, values[nameof(ComMessageFlag.Spam)]);
    }

    [TestMethod]
    public void DirectActivation_PreservesLegacyAccessDeniedBoundary()
    {
        var messagesError = Assert.ThrowsExactly<COMException>(() => _ = new Messages().Count);
        var messageError = Assert.ThrowsExactly<COMException>(() => _ = new Message().ID);
        var accountError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Messages);
        var folderError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolder().Messages);

        Assert.AreEqual(EAccessDenied, messagesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, messageError.ErrorCode);
        Assert.AreEqual(EAccessDenied, accountError.ErrorCode);
        Assert.AreEqual(EAccessDenied, folderError.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedCollection_ExposesReadOnlyMessageMetadataAndLegacyLookupErrors()
    {
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[]
            {
                Snapshot(1000, 100, 50, "0001.eml", 2, "sender@example.test", 4097, 3, 33, Date(2026, 7, 1), 77),
                Snapshot(2000, 100, 50, "0002.eml", 2, "other@example.test", 1024, 0, 2, Date(2026, 7, 2), 78)
            });

        Assert.AreEqual(2, messages.Count);
        AssertMessage(messages[0], 1000, "0001.eml", "sender@example.test", 2, 4, 4, 77, Date(2026, 7, 1));
        AssertMessage(messages.get_ItemByDBID(2000), 2000, "0002.eml", "other@example.test", 2, 1, 1, 78, Date(2026, 7, 2));
        Assert.IsTrue(messages[0].get_Flag(ComMessageFlag.Seen));
        Assert.IsTrue(messages[0].get_Flag(ComMessageFlag.Recent));
        Assert.IsFalse(messages[0].get_Flag(ComMessageFlag.Deleted));

        var badIndex = Assert.ThrowsExactly<COMException>(() => _ = messages[2]);
        var badDatabaseId = Assert.ThrowsExactly<COMException>(() => _ = messages.get_ItemByDBID(3000));
        var pendingDelete = Assert.ThrowsExactly<COMException>(() => messages.DeleteByDBID(1000));
        var pendingAdd = Assert.ThrowsExactly<COMException>(() => messages.Add());
        var pendingClear = Assert.ThrowsExactly<COMException>(messages.Clear);

        Assert.AreEqual(DispEBadIndex, badIndex.ErrorCode);
        Assert.AreEqual(DispEBadIndex, badDatabaseId.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAdd.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingClear.ErrorCode);
    }

    [TestMethod]
    public void AuthorizedMessage_KeepsContentHeadersAttachmentsAndMutationUnavailable()
    {
        IInterfaceMessage message = Message.CreateAuthorized(
            Snapshot(1000, 100, 50, "0001.eml", 2, "sender@example.test", 4097, 3, 33, Date(2026, 7, 1), 77));

        var pendingSubject = Assert.ThrowsExactly<COMException>(() => _ = message.Subject);
        var pendingSubjectWrite = Assert.ThrowsExactly<COMException>(() => message.Subject = "changed");
        var pendingFrom = Assert.ThrowsExactly<COMException>(() => _ = message.From);
        var pendingFromWrite = Assert.ThrowsExactly<COMException>(() => message.From = "changed");
        var pendingDate = Assert.ThrowsExactly<COMException>(() => _ = message.Date);
        var pendingDateWrite = Assert.ThrowsExactly<COMException>(() => message.Date = "changed");
        var pendingBody = Assert.ThrowsExactly<COMException>(() => _ = message.Body);
        var pendingBodyWrite = Assert.ThrowsExactly<COMException>(() => message.Body = "changed");
        var pendingHtml = Assert.ThrowsExactly<COMException>(() => _ = message.HTMLBody);
        var pendingHtmlWrite = Assert.ThrowsExactly<COMException>(() => message.HTMLBody = "changed");
        var pendingAttachments = Assert.ThrowsExactly<COMException>(() => _ = message.Attachments);
        var pendingSave = Assert.ThrowsExactly<COMException>(message.Save);
        var pendingTo = Assert.ThrowsExactly<COMException>(() => _ = message.To);
        var pendingAddRecipient = Assert.ThrowsExactly<COMException>(
            () => message.AddRecipient("Ada", "ada@example.test"));
        var pendingFromAddressWrite = Assert.ThrowsExactly<COMException>(
            () => message.FromAddress = "changed@example.test");
        var pendingClearRecipients = Assert.ThrowsExactly<COMException>(message.ClearRecipients);
        var pendingCc = Assert.ThrowsExactly<COMException>(() => _ = message.CC);
        var pendingRecipients = Assert.ThrowsExactly<COMException>(() => _ = message.Recipients);
        var pendingHeaderValue = Assert.ThrowsExactly<COMException>(() => _ = message.get_HeaderValue("Subject"));
        var pendingHeaderValueWrite = Assert.ThrowsExactly<COMException>(
            () => message.set_HeaderValue("Subject", "changed"));
        var pendingHasBodyType = Assert.ThrowsExactly<COMException>(() => _ = message.HasBodyType("text/plain"));
        var pendingEncodeFields = Assert.ThrowsExactly<COMException>(() => _ = message.EncodeFields);
        var pendingEncodeFieldsWrite = Assert.ThrowsExactly<COMException>(() => message.EncodeFields = false);
        var pendingFlagWrite = Assert.ThrowsExactly<COMException>(
            () => message.set_Flag(ComMessageFlag.Seen, false));
        var pendingHeaders = Assert.ThrowsExactly<COMException>(() => _ = message.Headers);
        var pendingRefresh = Assert.ThrowsExactly<COMException>(message.RefreshContent);
        var pendingCharset = Assert.ThrowsExactly<COMException>(() => _ = message.Charset);
        var pendingCharsetWrite = Assert.ThrowsExactly<COMException>(() => message.Charset = "utf-8");
        var pendingCopy = Assert.ThrowsExactly<COMException>(() => message.Copy(50));

        foreach (var error in new[]
                 {
                     pendingSubject, pendingSubjectWrite, pendingFrom, pendingFromWrite, pendingDate, pendingDateWrite,
                     pendingBody, pendingBodyWrite, pendingHtml, pendingHtmlWrite, pendingAttachments, pendingSave,
                     pendingTo, pendingAddRecipient, pendingFromAddressWrite, pendingClearRecipients, pendingCc,
                     pendingRecipients, pendingHeaderValue, pendingHeaderValueWrite, pendingHasBodyType,
                     pendingEncodeFields, pendingEncodeFieldsWrite, pendingFlagWrite, pendingHeaders, pendingRefresh,
                     pendingCharset, pendingCharsetWrite, pendingCopy
                 })
        {
            Assert.AreEqual(ENotImplemented, error.ErrorCode);
        }
    }

    [TestMethod]
    public void AccountMessages_UsesConfiguredRuntimeForSelectedAccount()
    {
        MessageAdministrationRuntimeHost.Configure(
            new FixedMessageAdministrationStore(
                new[]
                {
                    Snapshot(1000, 100, 50, "account.eml", 2, "sender@example.test", 2048, 0, 1, Date(2026, 7, 1), 10),
                    Snapshot(2000, 200, 60, "outside.eml", 2, "outside@example.test", 2048, 0, 1, Date(2026, 7, 1), 20)
                }));
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var messages = account.Messages;

        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(1000L, messages[0].ID);
        Assert.AreEqual("account.eml", messages[0].Filename);
    }

    [TestMethod]
    public void ImapFolderMessages_UsesConfiguredRuntimeForSelectedFolder()
    {
        MessageAdministrationRuntimeHost.Configure(
            new FixedMessageAdministrationStore(
                new[]
                {
                    Snapshot(1000, 100, 50, "folder.eml", 2, "sender@example.test", 2048, 0, 1, Date(2026, 7, 1), 10),
                    Snapshot(2000, 100, 60, "outside.eml", 2, "outside@example.test", 2048, 0, 1, Date(2026, 7, 1), 20)
                }));
        var folders = IMAPFolders.CreateAuthorized(
            new[] { new ImapFolderAdministrationSnapshot(50, 100, -1, "Inbox", true, 42, "2026-07-01 00:00:00") });

        var messages = folders[0].Messages;

        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(1000L, messages[0].ID);
        Assert.AreEqual("folder.eml", messages[0].Filename);
    }

    private static MessageAdministrationSnapshot Snapshot(
        long id,
        int accountId,
        int folderId,
        string fileName,
        int state,
        string fromAddress,
        long sizeBytes,
        int currentNumberOfTries,
        int flags,
        DateTime internalDate,
        long uid) =>
        new(id, accountId, folderId, fileName, state, fromAddress, sizeBytes, currentNumberOfTries, flags, internalDate, uid);

    private static DateTime Date(int year, int month, int day) => new(year, month, day, 1, 2, 3);

    private static void AssertMessage(
        IInterfaceMessage message,
        long id,
        string fileName,
        string fromAddress,
        int state,
        int size,
        int deliveryAttempt,
        int uid,
        DateTime internalDate)
    {
        Assert.AreEqual(id, message.ID);
        Assert.AreEqual(fileName, message.Filename);
        Assert.AreEqual(fromAddress, message.FromAddress);
        Assert.AreEqual(state, message.State);
        Assert.AreEqual(size, message.Size);
        Assert.AreEqual(deliveryAttempt, message.DeliveryAttempt);
        Assert.AreEqual(uid, message.UID);
        Assert.AreEqual(internalDate, message.InternalDate);
    }

    private static void AssertContract(Type contract, string interfaceId, string[] methodNames)
    {
        Assert.AreEqual(new Guid(interfaceId), contract.GUID);
        Assert.AreEqual(ComInterfaceType.InterfaceIsDual, contract.GetCustomAttribute<InterfaceTypeAttribute>()?.Value);
        Assert.AreEqual(
            TypeLibTypeFlags.FDual | TypeLibTypeFlags.FNonExtensible | TypeLibTypeFlags.FDispatchable,
            contract.GetCustomAttribute<TypeLibTypeAttribute>()?.Value);
        CollectionAssert.AreEqual(
            methodNames,
            contract.GetMethods().OrderBy(static method => method.MetadataToken).Select(static method => method.Name).ToArray());
    }

    private static void AssertComClass<T>(string classId, string progId, Type defaultInterface)
    {
        var type = typeof(T);

        Assert.AreEqual(new Guid(classId), type.GUID);
        Assert.AreEqual(progId, type.GetCustomAttribute<ProgIdAttribute>()?.Value);
        Assert.AreEqual(ClassInterfaceType.None, type.GetCustomAttribute<ClassInterfaceAttribute>()?.Value);
        Assert.AreEqual(defaultInterface, type.GetCustomAttribute<ComDefaultInterfaceAttribute>()?.Value);
        Assert.IsNotNull(type.GetConstructor(Type.EmptyTypes));
    }

    private sealed class FixedMessageAdministrationStore(IReadOnlyList<MessageAdministrationSnapshot> messages)
        : IMessageAdministrationStore
    {
        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
            int accountId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(
                messages.Where(message => message.AccountId == accountId).OrderBy(message => message.Id).ToArray());

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(
                messages.Where(message => message.FolderId == folderId).OrderBy(message => message.Uid).ToArray());
    }
}
