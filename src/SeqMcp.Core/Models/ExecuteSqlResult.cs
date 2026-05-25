namespace SeqMcp.Core.Models;

public record ExecuteSqlResult(
    string Query,
    string Result,
    int RowCount);
