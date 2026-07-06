namespace Probe.DbEditor.Utilities;

public static class ValueDisplay
{
    public static string Format(object? value)
    {
        if (value is null || value is DBNull)
        {
            return "NULL";
        }

        if (value is byte[] bytes)
        {
            return $"<binary {bytes.Length} bytes>";
        }

        return Convert.ToString(value, System.Globalization.CultureInfo.InvariantCulture) ?? "";
    }
}
