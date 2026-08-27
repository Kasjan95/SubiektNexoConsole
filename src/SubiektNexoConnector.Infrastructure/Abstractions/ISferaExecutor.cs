using InsERT.Moria.Sfera;
using System.Runtime.CompilerServices;

namespace SubiektNexoConnector.Infrastructure.Abstractions;

/// <summary>
/// Executes an operation during an exclusive lease of the Sfera runtime.
/// The lease includes connection creation and disposal of <see cref="Uchwyt"/>.
/// </summary>
public interface ISferaExecutor
{
    T Execute<T>(
        Func<Uchwyt, T> operation,
        [CallerMemberName] string operationName = "",
        [CallerFilePath] string sourceFilePath = "");

    void Execute(
        Action<Uchwyt> operation,
        [CallerMemberName] string operationName = "",
        [CallerFilePath] string sourceFilePath = "");
}
