namespace SubiektNexoConnector.Deployment.Lifecycle;

public sealed record DeploymentStep(string Id, string Title, Func<CancellationToken, Task> Execute,
    Func<CancellationToken, Task<bool>>? Verify = null, bool Skip = false, bool Optional = false);

public sealed class DeploymentOrchestrator(DeploymentStore store)
{
    public event EventHandler<StepRecord>? Progress;

    public async Task RunAsync(DeploymentState state, IEnumerable<DeploymentStep> steps,
        CancellationToken cancellationToken = default)
    {
        state.Status = OperationStatus.Running;
        store.Save(state);
        try
        {
            foreach (var step in steps)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var record = state.Steps.SingleOrDefault(s => s.Id == step.Id);
                if (record is null) { record = new StepRecord { Id = step.Id, Title = step.Title }; state.Steps.Add(record); }
                if (step.Skip) { record.Status = StepStatus.Skipped; store.Save(state); Progress?.Invoke(this, record); continue; }
                // Completed is not sufficient: reconcile the actual resource on every resume.
                if (record.Status == StepStatus.Completed && step.Verify is not null
                    && await step.Verify(cancellationToken)) { Progress?.Invoke(this, record); continue; }
                record.Status = StepStatus.Running;
                record.ErrorCode = null;
                store.Save(state);
                Progress?.Invoke(this, record);
                try
                {
                    await step.Execute(cancellationToken);
                    if (step.Verify is not null && !await step.Verify(cancellationToken))
                        throw new InvalidOperationException($"Nie udało się potwierdzić wyniku: {step.Title}.");
                    record.Status = StepStatus.Completed;
                    store.Save(state);
                    Progress?.Invoke(this, record);
                }
                catch (Exception error)
                {
                    if (step.Optional && !cancellationToken.IsCancellationRequested)
                    {
                        record.Status = StepStatus.Skipped;
                        record.ErrorCode = $"{error.GetType().Name}:0x{error.HResult:X8}";
                        store.Save(state); Progress?.Invoke(this, record);
                        continue;
                    }
                    record.Status = StepStatus.Failed;
                    record.ErrorCode = $"{error.GetType().Name}:0x{error.HResult:X8}";
                    store.Save(state); Progress?.Invoke(this, record);
                    throw;
                }
            }
            state.Status = state.Mode switch
            {
                "uninstall" => OperationStatus.Removed,
                "uninstall-local" => OperationStatus.PendingServerRemoval,
                _ => OperationStatus.Ready
            };
        }
        catch (OperationCanceledException) { state.Status = OperationStatus.Cancelled; throw; }
        catch { state.Status = OperationStatus.NeedsRepair; throw; }
        finally { store.Save(state); }
    }
}
