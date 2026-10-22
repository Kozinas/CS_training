using System.Data.Common;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace Course.Data.Tests;

internal sealed class QueryCounter : DbCommandInterceptor
{
    public List<string> Commands { get; } = [];
    public override InterceptionResult<DbDataReader> ReaderExecuting(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result)
    { Commands.Add(command.CommandText); return result; }
    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command,
        CommandEventData eventData, InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
    { Commands.Add(command.CommandText); return ValueTask.FromResult(result); }
}
