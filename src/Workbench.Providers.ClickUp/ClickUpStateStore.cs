using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;

namespace Workbench.Providers.ClickUp;

internal sealed class ClickUpStateStore
{
    private static readonly JsonSerializerOptions jsonOptions = new(JsonSerializerDefaults.Web)
    {
        WriteIndented = false,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        Converters = { new JsonStringEnumConverter() }
    };

    private readonly string plansDirectory;
    private readonly string receiptsDirectory;
    private readonly string pendingCreatesDirectory;
    private readonly string applyLockPath;
    private readonly SemaphoreSlim gate = new(1, 1);

    public ClickUpStateStore(string stateDirectory)
    {
        var root = Path.GetFullPath(stateDirectory);
        this.plansDirectory = Path.Combine(root, "plans");
        this.receiptsDirectory = Path.Combine(root, "receipts");
        this.pendingCreatesDirectory = Path.Combine(root, "pending-creates");
        this.applyLockPath = Path.Combine(root, "apply.lock");
        Directory.CreateDirectory(this.plansDirectory);
        Directory.CreateDirectory(this.receiptsDirectory);
        Directory.CreateDirectory(this.pendingCreatesDirectory);
    }

    public async Task<FileStream> AcquireApplyLeaseAsync(CancellationToken cancellationToken)
    {
        while (true)
        {
            cancellationToken.ThrowIfCancellationRequested();
            try
            {
                return new FileStream(
                    this.applyLockPath,
                    FileMode.OpenOrCreate,
                    FileAccess.ReadWrite,
                    FileShare.None,
                    bufferSize: 1,
                    FileOptions.Asynchronous | FileOptions.WriteThrough);
            }
            catch (IOException)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
            }
        }
    }

    public async Task SavePlanAsync(StoredPlan plan, CancellationToken cancellationToken)
    {
        await this.WriteAtomicAsync(this.PlanPath(plan.PlanId), plan, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredPlan?> LoadPlanAsync(string planId, CancellationToken cancellationToken)
    {
        return await this.ReadAsync<StoredPlan>(this.PlanPath(planId), cancellationToken).ConfigureAwait(false);
    }

    public async Task SaveReceiptAsync(
        string idempotencyKey,
        StoredReceipt receipt,
        CancellationToken cancellationToken)
    {
        await this.WriteAtomicAsync(this.ReceiptPath(idempotencyKey), receipt, cancellationToken).ConfigureAwait(false);
    }

    public async Task<StoredReceipt?> LoadReceiptAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await this.ReadAsync<StoredReceipt>(this.ReceiptPath(idempotencyKey), cancellationToken).ConfigureAwait(false);
    }

    public async Task SavePendingCreateAsync(
        string idempotencyKey,
        StoredPendingCreate pendingCreate,
        CancellationToken cancellationToken)
    {
        await this.WriteAtomicAsync(this.PendingCreatePath(idempotencyKey), pendingCreate, cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<StoredPendingCreate?> LoadPendingCreateAsync(
        string idempotencyKey,
        CancellationToken cancellationToken)
    {
        return await this.ReadAsync<StoredPendingCreate>(this.PendingCreatePath(idempotencyKey), cancellationToken)
            .ConfigureAwait(false);
    }

    public void DeletePendingCreate(string idempotencyKey)
    {
        var path = this.PendingCreatePath(idempotencyKey);
        if (File.Exists(path))
        {
            File.Delete(path);
        }
    }

    private async Task<T?> ReadAsync<T>(string path, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (!File.Exists(path))
            {
                return default;
            }

            var stream = File.OpenRead(path);
            try
            {
                return await JsonSerializer.DeserializeAsync<T>(stream, jsonOptions, cancellationToken).ConfigureAwait(false);
            }
            finally
            {
                await stream.DisposeAsync().ConfigureAwait(false);
            }
        }
        catch (JsonException)
        {
            throw new InvalidOperationException("ClickUp provider state is corrupt and cannot be used safely.");
        }
        finally
        {
            this.gate.Release();
        }
    }

    private async Task WriteAtomicAsync<T>(string path, T value, CancellationToken cancellationToken)
    {
        await this.gate.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var temporaryPath = path + "." + Guid.NewGuid().ToString("N") + ".tmp";
            try
            {
                var stream = new FileStream(
                    temporaryPath,
                    FileMode.CreateNew,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 4096,
                    FileOptions.WriteThrough | FileOptions.Asynchronous);
                try
                {
                    await JsonSerializer.SerializeAsync(stream, value, jsonOptions, cancellationToken).ConfigureAwait(false);
                    await stream.FlushAsync(cancellationToken).ConfigureAwait(false);
                }
                finally
                {
                    await stream.DisposeAsync().ConfigureAwait(false);
                }

                File.Move(temporaryPath, path, overwrite: true);
            }
            finally
            {
                if (File.Exists(temporaryPath))
                {
                    File.Delete(temporaryPath);
                }
            }
        }
        finally
        {
            this.gate.Release();
        }
    }

    private string PlanPath(string planId)
    {
        return Path.Combine(this.plansDirectory, HashName(planId) + ".json");
    }

    private string ReceiptPath(string idempotencyKey)
    {
        return Path.Combine(this.receiptsDirectory, HashName(idempotencyKey) + ".json");
    }

    private string PendingCreatePath(string idempotencyKey)
    {
        return Path.Combine(this.pendingCreatesDirectory, HashName(idempotencyKey) + ".json");
    }

    private static string HashName(string value)
    {
        return Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(value)));
    }
}
