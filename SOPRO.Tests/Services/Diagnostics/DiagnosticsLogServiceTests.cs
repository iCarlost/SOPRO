using SOPRO.Application.Services;

namespace SOPRO.Tests.Services.Diagnostics;

[TestClass]
public sealed class DiagnosticsLogServiceTests
{
    [TestMethod]
    public void SnapshotCapturesBytesAndExportUsesCapturedContent()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        var log = System.IO.Path.Combine(logs, "sopro-20261117.log");
        var original = new byte[] { 0x7B, 0x7D, 0x0A };
        File.WriteAllBytes(log, original);
        var service = new DiagnosticsLogService(logs);
        var capture = service.CaptureSnapshot();
        Assert.AreEqual(DiagnosticsOperationStatus.Success, capture.Status);
        Assert.AreEqual("{}\n", capture.Snapshot!.Entries.Single().PreviewText);
        File.WriteAllText(log, "changed");
        var destination = System.IO.Path.Combine(temp.Root, "export.jsonl");
        Assert.IsTrue(service.ExportEntry(capture.Snapshot, capture.Snapshot.Entries[0].Id, destination, false).Succeeded);
        CollectionAssert.AreEqual(original, File.ReadAllBytes(destination));
        Assert.AreEqual(DiagnosticsOperationStatus.AlreadyExists, service.ExportEntry(capture.Snapshot, capture.Snapshot.Entries[0].Id, destination, false).Status);
    }

    [TestMethod]
    public void DeleteRemovesOnlyValidDirectLogsAndPreservesLock()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        var valid = System.IO.Path.Combine(logs, "sopro-20261117.1.log");
        var unrelated = System.IO.Path.Combine(logs, "sopro-not-a-date.log");
        var lockFile = System.IO.Path.Combine(logs, "sopro-diagnostics.lock");
        File.WriteAllText(valid, "log"); File.WriteAllText(unrelated, "keep"); File.WriteAllText(lockFile, "lock");
        Directory.CreateDirectory(System.IO.Path.Combine(logs, "sopro-20261117.log"));
        Assert.AreEqual(DiagnosticsOperationStatus.Success, new DiagnosticsLogService(logs).DeleteLogs().Status);
        Assert.IsFalse(File.Exists(valid)); Assert.IsTrue(File.Exists(unrelated)); Assert.IsTrue(File.Exists(lockFile));
    }

    [TestMethod]
    public void BusyLockAndCancellationReturnTypedStatuses()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        using var held = new FileStream(System.IO.Path.Combine(logs, "sopro-diagnostics.lock"), FileMode.Create, FileAccess.ReadWrite, FileShare.None);
        var service = new DiagnosticsLogService(logs, TimeSpan.FromMilliseconds(30));
        Assert.AreEqual(DiagnosticsOperationStatus.Busy, service.CaptureSnapshot().Status);
        using var canceled = new CancellationTokenSource(); canceled.Cancel();
        Assert.AreEqual(DiagnosticsOperationStatus.Cancelled, service.DeleteLogs(canceled.Token).Status);
    }

    [TestMethod]
    public void InvalidUtf8FailsAndPrivacyAcknowledgementIsIdempotent()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        File.WriteAllBytes(System.IO.Path.Combine(logs, "sopro-20261117.log"), new byte[] { 0xFF });
        Assert.AreEqual(DiagnosticsOperationStatus.InvalidData, new DiagnosticsLogService(logs).CaptureSnapshot().Status);
        var notice = new DiagnosticsPrivacyNoticeStore(temp.Root);
        Assert.IsFalse(notice.IsAcknowledged); notice.Acknowledge(); notice.Acknowledge(); Assert.IsTrue(notice.IsAcknowledged);
    }

    [TestMethod]
    public void DeleteReportsPartialCountWhenOneFileFails()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        File.WriteAllText(System.IO.Path.Combine(logs, "sopro-20261116.log"), "one");
        File.WriteAllText(System.IO.Path.Combine(logs, "sopro-20261117.log"), "two");
        var store = new FailingDiagnosticsFileStore { FailDeleteFileName = "sopro-20261117.log" };
        var result = new DiagnosticsLogService(store, logs, TimeSpan.FromMilliseconds(50)).DeleteLogs();
        Assert.AreEqual(DiagnosticsOperationStatus.Partial, result.Status);
        Assert.AreEqual(1, result.DeletedCount);
        Assert.IsFalse(File.Exists(System.IO.Path.Combine(logs, "sopro-20261116.log")));
        Assert.IsTrue(File.Exists(System.IO.Path.Combine(logs, "sopro-20261117.log")));
        Assert.IsFalse(result.ToString().Contains("texto de fallo", StringComparison.Ordinal));
    }

    [TestMethod]
    public void CancellationAfterDeletionReportsPartialCount()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        File.WriteAllText(System.IO.Path.Combine(logs, "sopro-20261116.log"), "one");
        File.WriteAllText(System.IO.Path.Combine(logs, "sopro-20261117.log"), "two");
        using var cancellation = new CancellationTokenSource();
        var store = new FailingDiagnosticsFileStore { AfterDelete = _ => cancellation.Cancel() };
        var result = new DiagnosticsLogService(store, logs, TimeSpan.FromMilliseconds(50)).DeleteLogs(cancellation.Token);
        Assert.AreEqual(DiagnosticsOperationStatus.Partial, result.Status);
        Assert.AreEqual(1, result.DeletedCount);
    }

    [TestMethod]
    public void StoreFailuresReturnGenericStatusesWithoutFreeText()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        File.WriteAllText(System.IO.Path.Combine(logs, "sopro-20261117.log"), "{}");
        var failedRead = new DiagnosticsLogService(new FailingDiagnosticsFileStore { FailRead = true }, logs, TimeSpan.FromMilliseconds(50)).CaptureSnapshot();
        Assert.AreEqual(DiagnosticsOperationStatus.Failed, failedRead.Status);
        Assert.IsFalse(failedRead.ToString()!.Contains("texto de fallo", StringComparison.Ordinal));

        var store = new FailingDiagnosticsFileStore();
        var captured = new DiagnosticsLogService(store, logs, TimeSpan.FromMilliseconds(50)).CaptureSnapshot();
        var failedExport = new DiagnosticsLogService(new FailingDiagnosticsFileStore { FailWrite = true }, logs, TimeSpan.FromMilliseconds(50))
            .ExportEntry(captured.Snapshot!, captured.Snapshot!.Entries[0].Id, System.IO.Path.Combine(temp.Root, "export.log"), false);
        Assert.AreEqual(DiagnosticsOperationStatus.Failed, failedExport.Status);
        Assert.IsFalse(failedExport.ToString()!.Contains("texto de fallo", StringComparison.Ordinal));
    }

    [TestMethod]
    public void ReparseLogsDirectoryIsRejectedBeforeLockEnumerationOrDeletion()
    {
        using var temp = new DiagnosticsTempDirectory();
        var logs = Directory.CreateDirectory(System.IO.Path.Combine(temp.Root, "logs")).FullName;
        File.WriteAllText(System.IO.Path.Combine(logs, "sopro-20261117.log"), "keep");
        var store = new FailingDiagnosticsFileStore { ReparsePath = logs };
        var service = new DiagnosticsLogService(store, logs, TimeSpan.FromMilliseconds(50));

        Assert.AreEqual(DiagnosticsOperationStatus.InvalidDestination, service.CaptureSnapshot().Status);
        Assert.AreEqual(DiagnosticsOperationStatus.InvalidDestination, service.DeleteLogs().Status);
        Assert.AreEqual(0, store.LockCount);
        Assert.AreEqual(0, store.EnumerateCount);
        Assert.AreEqual(0, store.DeleteCount);
        Assert.IsTrue(File.Exists(System.IO.Path.Combine(logs, "sopro-20261117.log")));
    }
}
