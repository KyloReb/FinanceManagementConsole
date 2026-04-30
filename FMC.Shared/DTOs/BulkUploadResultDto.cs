namespace FMC.Shared.DTOs;

public class BulkUploadResultDto
{
    public int TotalRows { get; set; }
    public int Submitted { get; set; }
    public int Failed { get; set; }
    public List<BulkTransactionRowDto> Rows { get; set; } = new();
}
