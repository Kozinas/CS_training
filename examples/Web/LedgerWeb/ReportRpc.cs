using System.Security.Claims;
using Course.Web.Rpc;
using Grpc.Core;
using Microsoft.AspNetCore.Authorization;

namespace Course.Web;

[Authorize(Policy = "api")]
public sealed class ReportRpc(LedgerService ledger) : Reports.ReportsBase
{
    public override async Task<ReportReply> Get(ReportRequest request, ServerCallContext context)
    {
        string owner = context.GetHttpContext().User.FindFirstValue(ClaimTypes.NameIdentifier)!;
        var rows = await ledger.Report(owner, context.CancellationToken);
        var reply = new ReportReply();
        reply.Lines.AddRange(rows.Select(r => new ReportLine { Category = r.Category, Count = r.Count, AmountMinor = r.AmountMinor }));
        return reply;
    }
}
