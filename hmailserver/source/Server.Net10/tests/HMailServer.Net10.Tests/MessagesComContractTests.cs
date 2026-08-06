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

        AssertContract(
            typeof(IInterfaceAttachment),
            "0CD0DDFF-2D30-41BE-9845-D37EADB1A007",
            new[] { "get_Filename", "get_Size", "SaveAs", "Delete" });
        AssertContract(
            typeof(IInterfaceAttachments),
            "BED37911-1180-4840-A831-196C6771EF54",
            new[] { "get_Item", "get_Count", "Clear", "Add" });
        AssertContract(
            typeof(IInterfaceRecipients),
            "9B47C955-4462-48E3-91FE-C5E1CFEC80E0",
            new[] { "get_Item", "get_Count" });
        AssertContract(
            typeof(IInterfaceRecipient),
            "65D57DF8-68A1-4358-BB98-C3B33595B699",
            new[] { "get_Address", "get_IsLocalUser", "get_OriginalAddress" });
        AssertContract(
            typeof(IInterfaceMessageHeader),
            "FF69E250-CBFD-4AB6-9440-39599478365D",
            new[] { "get_Name", "set_Name", "get_Value", "set_Value", "Delete" });
        AssertContract(
            typeof(IInterfaceMessageHeaders),
            "1ADE0B5E-536C-4707-8385-32A7F6F92500",
            new[] { "get_Item", "get_Count", "get_ItemByName" });
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
        AssertComClass<Attachments>(
            "63FF738A-982B-41E6-87C7-BA4AA9622B30",
            "hMailServer.Attachments.1",
            typeof(IInterfaceAttachments));
        AssertComClass<Attachment>(
            "B65A156A-54D1-4803-80CE-273F44AE935F",
            "hMailServer.Attachment.1",
            typeof(IInterfaceAttachment));
        AssertComClass<Recipients>(
            "B5B9C42D-64F1-443F-AA0D-FABB2DD9317B",
            "hMailServer.Recipients.1",
            typeof(IInterfaceRecipients));
        AssertComClass<Recipient>(
            "45B82F51-8445-4F3A-BC9E-137FC04BFE2A",
            "hMailServer.Recipient.1",
            typeof(IInterfaceRecipient));
        AssertComClass<MessageHeaders>(
            "AE360CD2-BB40-4B39-83A6-84516C865365",
            "hMailServer.MessageHeaders.1",
            typeof(IInterfaceMessageHeaders));
        AssertComClass<MessageHeader>(
            "983EE030-380D-4E39-850D-AA543F3C1CB9",
            "hMailServer.MessageHeader.1",
            typeof(IInterfaceMessageHeader));
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
        var attachmentsError = Assert.ThrowsExactly<COMException>(() => _ = new Attachments().Count);
        var attachmentError = Assert.ThrowsExactly<COMException>(() => _ = new Attachment().Filename);
        var recipientsError = Assert.ThrowsExactly<COMException>(() => _ = new Recipients().Count);
        var recipientError = Assert.ThrowsExactly<COMException>(() => _ = new Recipient().Address);
        var headersError = Assert.ThrowsExactly<COMException>(() => _ = new MessageHeaders().Count);
        var headerError = Assert.ThrowsExactly<COMException>(() => _ = new MessageHeader().Name);
        var accountError = Assert.ThrowsExactly<COMException>(() => _ = new Account().Messages);
        var folderError = Assert.ThrowsExactly<COMException>(() => _ = new IMAPFolder().Messages);

        Assert.AreEqual(EAccessDenied, messagesError.ErrorCode);
        Assert.AreEqual(EAccessDenied, messageError.ErrorCode);
        Assert.AreEqual(EAccessDenied, attachmentsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, attachmentError.ErrorCode);
        Assert.AreEqual(EAccessDenied, recipientsError.ErrorCode);
        Assert.AreEqual(EAccessDenied, recipientError.ErrorCode);
        Assert.AreEqual(EAccessDenied, headersError.ErrorCode);
        Assert.AreEqual(EAccessDenied, headerError.ErrorCode);
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
        Assert.AreEqual(DispEBadIndex, pendingAdd.ErrorCode);
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
    public void AuthorizedMessage_ExposesReadOnlyMimeContentThroughConfiguredContentSource()
    {
        var contentSource = new FixedMessageContentSource(
            new Dictionary<long, byte[]>
            {
                [1000] = """
From: Sender <sender@example.test>
To: Ada <ada@example.test>
Cc: Support <support@example.test>
Subject: Hello world
Date: Wed, 01 Jul 2026 01:02:03 +0000
X-Test: one
Content-Type: multipart/mixed; boundary="outer"

--outer
Content-Type: text/plain; charset=utf-8

Plain body
--outer
Content-Type: text/html; charset=utf-8

<p>HTML body</p>
--outer
Content-Type: application/octet-stream
Content-Disposition: attachment; filename="report.txt"
Content-Transfer-Encoding: base64

SGVsbG8=
--outer--
"""u8.ToArray(),
                [2000] = """
Subject: Charset
Content-Type: text/plain; charset=iso-8859-1

Body
"""u8.ToArray()
            });
        IInterfaceMessage message = Message.CreateAuthorized(
            Snapshot(1000, 100, 50, "0001.eml", 2, "sender@example.test", 4097, 3, 33, Date(2026, 7, 1), 77),
            contentSource);

        Assert.AreEqual("Hello world", message.Subject);
        Assert.AreEqual("Sender <sender@example.test>", message.From);
        Assert.AreEqual("Wed, 01 Jul 2026 01:02:03 +0000", message.Date);
        Assert.AreEqual("Ada <ada@example.test>", message.To);
        Assert.AreEqual("Support <support@example.test>", message.CC);
        StringAssert.Contains(message.Body, "Plain body");
        StringAssert.Contains(message.HTMLBody, "HTML body");
        Assert.AreEqual("one", message.get_HeaderValue("x-test"));
        Assert.IsTrue(message.HasBodyType("text/plain"));
        Assert.IsTrue(message.HasBodyType("text/html"));
        Assert.IsTrue(message.HasBodyType("application/octet-stream"));
        Assert.IsFalse(message.HasBodyType("image/png"));

        var headers = message.Headers;
        Assert.IsTrue(headers.Count >= 6);
        Assert.AreEqual("Subject", headers.get_ItemByName("subject").Name);
        Assert.AreEqual("Hello world", headers.get_ItemByName("subject").Value);
        var pendingHeaderValue = Assert.ThrowsExactly<COMException>(() => headers.get_ItemByName("subject").Value = "changed");
        var pendingHeaderDelete = Assert.ThrowsExactly<COMException>(headers.get_ItemByName("subject").Delete);
        Assert.AreEqual(ENotImplemented, pendingHeaderValue.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingHeaderDelete.ErrorCode);

        var recipients = message.Recipients;
        Assert.AreEqual(2, recipients.Count);
        Assert.AreEqual("ada@example.test", recipients[0].Address);
        Assert.AreEqual("support@example.test", recipients[1].OriginalAddress);
        Assert.IsFalse(recipients[0].IsLocalUser);

        var attachments = message.Attachments;
        Assert.AreEqual(1, attachments.Count);
        Assert.AreEqual("report.txt", attachments[0].Filename);
        Assert.AreEqual(5, attachments[0].Size);
        var pendingAttachmentSave = Assert.ThrowsExactly<COMException>(() => attachments[0].SaveAs("out.txt"));
        var pendingAttachmentDelete = Assert.ThrowsExactly<COMException>(attachments[0].Delete);
        var pendingAttachmentClear = Assert.ThrowsExactly<COMException>(attachments.Clear);
        var pendingAttachmentAdd = Assert.ThrowsExactly<COMException>(() => attachments.Add("new.txt"));
        Assert.AreEqual(ENotImplemented, pendingAttachmentSave.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAttachmentDelete.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAttachmentClear.ErrorCode);
        Assert.AreEqual(ENotImplemented, pendingAttachmentAdd.ErrorCode);

        IInterfaceMessage simple = Message.CreateAuthorized(
            Snapshot(2000, 100, 50, "0002.eml", 2, "sender@example.test", 1024, 0, 0, Date(2026, 7, 2), 78),
            contentSource);
        Assert.AreEqual("iso-8859-1", simple.Charset);
    }

    [TestMethod]
    public void AccountMessages_UsesConfiguredRuntimeForSelectedAccount()
    {
        var store = new FixedMessageAdministrationStore(
            new[]
            {
                Snapshot(1000, 100, 50, "account.eml", 2, "sender@example.test", 2048, 0, 1, Date(2026, 7, 1), 10),
                Snapshot(2000, 200, 60, "outside.eml", 2, "outside@example.test", 2048, 0, 1, Date(2026, 7, 1), 20)
            });
        MessageAdministrationRuntimeHost.Configure(store);
        var account = Account.CreateAuthorized(
            new AccountAdministrationSnapshot(100, 10, "admin@example.test", true, 2));

        var messages = account.Messages;
        var repeatedMessages = account.Messages;

        Assert.AreNotSame(messages, repeatedMessages);
        Assert.AreEqual(1, store.AccountReadCount);
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

    [TestMethod]
    public void AddStagesFolderScopedDraftAndSavePublishesInsertedIdentity()
    {
        MessageAdministrationSnapshot? inserted = null;
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[] { Snapshot(10, 100, 20, "one.eml", 2, "one@example.test", 1024, 0, 0, Date(2026, 1, 1), 1) },
            accountId: 100,
            folderId: 20,
            insert: message =>
            {
                inserted = message;
                return 11;
            });

        var draft = messages.Add();

        Assert.AreEqual(0, draft.ID);
        Assert.AreEqual(string.Empty, draft.Filename);

        draft.Subject = "Hello";
        draft.From = "sender@example.test";
        draft.set_HeaderValue("X-Test", "value");

        Assert.AreEqual(1, messages.Count);
        draft.Save();

        Assert.AreEqual(2, messages.Count);
        Assert.AreEqual(11, draft.ID);
        Assert.IsNotNull(inserted);
        Assert.AreEqual(0, inserted.Id);
        Assert.AreEqual(100, inserted.AccountId);
        Assert.AreEqual(20, inserted.FolderId);
        Assert.AreEqual("sender@example.test", inserted.FromAddress);
        Assert.IsTrue(inserted.FileName.EndsWith(".eml", StringComparison.OrdinalIgnoreCase));
        Assert.AreEqual("sender@example.test", messages.get_ItemByDBID(11).FromAddress);
    }

    [TestMethod]
    public void FailedInsert_MapsToEFailAndRetainsDraftWithoutPublishing()
    {
        var fail = true;
        IInterfaceMessages messages = Messages.CreateAuthorized(
            Array.Empty<MessageAdministrationSnapshot>(),
            accountId: 100,
            folderId: 20,
            insert: _ => fail
                ? throw new InvalidOperationException("Simulated store failure.")
                : 1);

        var draft = messages.Add();
        draft.Subject = "Hello";

        var saveFailure = Assert.ThrowsExactly<COMException>(draft.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual(0, messages.Count);
        Assert.AreEqual(0, draft.ID);

        draft.Subject = "Other";
        fail = false;
        draft.Save();

        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(1, draft.ID);
    }
    [TestMethod]
    public void ExistingRowSave_PersistsStagedFromAndReplacesCollectionSnapshot()
    {
        MessageAdministrationSnapshot? updated = null;
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[] { Snapshot(10, 100, 20, "one.eml", 2, "one@example.test", 1024, 0, 0, Date(2026, 1, 1), 1) },
            accountId: 100,
            folderId: 20,
            insert: _ => 11,
            update: message =>
            {
                updated = message;
                return true;
            });

        var existing = messages[0];
        existing.From = "sender@example.test";
        existing.Save();

        Assert.IsNotNull(updated);
        Assert.AreEqual(10, updated.Id);
        Assert.AreEqual(100, updated.AccountId);
        Assert.AreEqual(20, updated.FolderId);
        Assert.AreEqual("sender@example.test", updated.FromAddress);
        Assert.AreEqual("sender@example.test", messages.get_ItemByDBID(10).FromAddress);
    }

    [TestMethod]
    public void FailedUpdate_MapsToEFailAndRetainsStagedStateWithoutReplacingSnapshot()
    {
        var failUpdate = true;
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[] { Snapshot(10, 100, 20, "one.eml", 2, "one@example.test", 1024, 0, 0, Date(2026, 1, 1), 1) },
            accountId: 100,
            folderId: 20,
            update: _ => failUpdate
                ? throw new InvalidOperationException("Simulated store failure.")
                : true);

        var existing = messages[0];
        existing.From = "changed@example.test";

        var saveFailure = Assert.ThrowsExactly<COMException>(existing.Save);

        Assert.AreEqual(unchecked((int)0x80004005), saveFailure.ErrorCode);
        Assert.AreEqual("one@example.test", messages[0].FromAddress);

        failUpdate = false;
        existing.From = "other@example.test";
        existing.Save();

        Assert.AreEqual("other@example.test", messages[0].FromAddress);
    }
    [TestMethod]
    public void DeleteByDBID_RemovesOnlyMatchingSnapshotAndTreatsUnknownAsNoOp()
    {
        var deletedIds = new List<long>();
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[]
            {
                Snapshot(10, 100, 20, "one.eml", 2, "one@example.test", 1024, 0, 0, Date(2026, 1, 1), 1),
                Snapshot(11, 100, 20, "two.eml", 2, "two@example.test", 512, 0, 0, Date(2026, 1, 2), 2)
            },
            accountId: 100,
            folderId: 20,
            delete: messageId =>
            {
                deletedIds.Add(messageId);
                return true;
            });

        messages.DeleteByDBID(10);

        Assert.AreEqual(1, messages.Count);
        Assert.AreEqual(11, messages[0].ID);

        messages.DeleteByDBID(999);
        Assert.AreEqual(1, messages.Count);
        CollectionAssert.AreEqual(new[] { 10L }, deletedIds);
    }

    [TestMethod]
    public void FailedDelete_MapsToEFailAndRetainsSnapshot()
    {
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[] { Snapshot(10, 100, 20, "one.eml", 2, "one@example.test", 1024, 0, 0, Date(2026, 1, 1), 1) },
            accountId: 100,
            folderId: 20,
            delete: _ => false);

        var deleteFailure = Assert.ThrowsExactly<COMException>(() => messages.DeleteByDBID(10));

        Assert.AreEqual(unchecked((int)0x80004005), deleteFailure.ErrorCode);
        Assert.AreEqual(1, messages.Count);
    }

    [TestMethod]
    public void Clear_EmptiesAllMessages()
    {
        var clearCalls = 0;
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[]
            {
                Snapshot(10, 100, 20, "one.eml", 2, "one@example.test", 1024, 0, 0, Date(2026, 1, 1), 1),
                Snapshot(11, 100, 20, "two.eml", 2, "two@example.test", 512, 0, 0, Date(2026, 1, 2), 2)
            },
            accountId: 100,
            folderId: 20,
            clear: () => clearCalls++);

        messages.Clear();

        Assert.AreEqual(1, clearCalls);
        Assert.AreEqual(0, messages.Count);
    }

    [TestMethod]
    public void DeleteWithoutConfiguredDelegate_RemainsNotImplemented()
    {
        IInterfaceMessages messages = Messages.CreateAuthorized(
            new[] { Snapshot(10, 100, 20, "one.eml", 2, "one@example.test", 1024, 0, 0, Date(2026, 1, 1), 1) });

        var pendingDelete = Assert.ThrowsExactly<COMException>(() => messages.DeleteByDBID(10));
        var pendingClear = Assert.ThrowsExactly<COMException>(messages.Clear);

        Assert.AreEqual(unchecked((int)0x80004001), pendingDelete.ErrorCode);
        Assert.AreEqual(unchecked((int)0x80004001), pendingClear.ErrorCode);
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
        public int AccountReadCount { get; private set; }

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetAccountMessagesAsync(
            int accountId,
            CancellationToken cancellationToken)
        {
            AccountReadCount++;
            return ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(
                messages.Where(message => message.AccountId == accountId).OrderBy(message => message.Id).ToArray());
        }

        public ValueTask<IReadOnlyList<MessageAdministrationSnapshot>> GetFolderMessagesAsync(
            int folderId,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult<IReadOnlyList<MessageAdministrationSnapshot>>(
                messages.Where(message => message.FolderId == folderId).OrderBy(message => message.Uid).ToArray());
    }

    private sealed class FixedMessageContentSource(IReadOnlyDictionary<long, byte[]> contentById)
        : IMessageAdministrationContentSource
    {
        public ValueTask<byte[]?> TryLoadMessageAsync(
            MessageAdministrationSnapshot message,
            CancellationToken cancellationToken) =>
            ValueTask.FromResult(contentById.GetValueOrDefault(message.Id));
    }
}
