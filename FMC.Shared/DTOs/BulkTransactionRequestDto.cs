namespace FMC.Shared.DTOs;

public class BulkTransactionRequestDto
{
    public bool IsCredit { get; set; }
    public List<BulkTransactionRowDto> Rows { get; set; } = new();
}
