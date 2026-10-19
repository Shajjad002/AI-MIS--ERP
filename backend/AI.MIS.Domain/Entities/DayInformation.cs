namespace AI.MIS.Domain.Entities;

public sealed class DayInformation : BaseEntity
{
    public string ProgramCode { get; set; } = string.Empty;
    public DateTime? SystemDay { get; set; }
}
