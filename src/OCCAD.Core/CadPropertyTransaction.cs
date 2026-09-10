using System.ComponentModel;

namespace OCCAD;

/// <summary>
/// Applies one editable property to a set of document entities as one logical
/// transaction. Either every entity reaches the requested value and one history
/// entry is recorded, or every entity is restored to its captured state.
/// </summary>
public static class CadPropertyTransaction
{
    public static bool TryApply(
        CadWorkspace workspace,
        IEnumerable<CadEntity> entities,
        string propertyName,
        object? value,
        out Exception? error)
    {
        ArgumentNullException.ThrowIfNull(workspace);
        ArgumentNullException.ThrowIfNull(entities);
        ArgumentException.ThrowIfNullOrWhiteSpace(propertyName);

        var targets = entities
            .Distinct()
            .Where(workspace.Document.Entities.Contains)
            .ToArray();
        if (targets.Length == 0)
        {
            error = null;
            return false;
        }

        var descriptors = new PropertyDescriptor[targets.Length];
        var anyChange = false;
        for (var index = 0; index < targets.Length; index++)
        {
            var descriptor = TypeDescriptor
                .GetProperties(targets[index], true)
                .Find(propertyName, ignoreCase: false);
            if (descriptor is null)
            {
                error = new InvalidOperationException(
                    $"Property '{propertyName}' is not available on '{targets[index].EntityType}'.");
                return false;
            }
            if (descriptor.IsReadOnly)
            {
                error = new InvalidOperationException(
                    $"Property '{propertyName}' is read-only on '{targets[index].EntityType}'.");
                return false;
            }

            descriptors[index] = descriptor;
            anyChange |= !Equals(descriptor.GetValue(targets[index]), value);
        }

        if (!anyChange)
        {
            error = null;
            return true;
        }

        var before = workspace.CaptureEntityStates(targets);
        var wasModified = workspace.IsModified;
        var historyState = workspace.History.CurrentStateId;
        Exception? failure = null;

        using (workspace.Document.BeginChangeSet())
        {
            try
            {
                for (var index = 0; index < targets.Length; index++)
                    descriptors[index].SetValue(targets[index], value);

                workspace.RecordEntityStateChange(
                    targets,
                    before,
                    $"Property {propertyName}");
            }
            catch (Exception exception)
            {
                var failures = new List<Exception> { exception };

                for (var index = 0; index < targets.Length; index++)
                {
                    try
                    {
                        targets[index].RestoreState(before[index]);
                    }
                    catch (Exception restoreFailure)
                    {
                        failures.Add(restoreFailure);
                    }
                }

                failure = failures.Count == 1
                    ? exception
                    : new AggregateException(
                        "Property apply and rollback both failed.",
                        failures);
            }
        }

        if (failure is not null)
        {
            if (!wasModified &&
                workspace.History.CurrentStateId == historyState)
                workspace.MarkSaved();

            error = failure;
            return false;
        }

        error = null;
        return true;
    }
}
