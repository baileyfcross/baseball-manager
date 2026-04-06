namespace BaseballManager.Contracts.ImportDtos;

[AttributeUsage(AttributeTargets.Property, AllowMultiple = true)]
public sealed class CsvHeaderAttribute : Attribute
{
    public CsvHeaderAttribute(string headerName)
    {
        HeaderName = headerName;
    }

    public string HeaderName { get; }
}
