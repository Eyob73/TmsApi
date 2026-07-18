namespace TmsApi.Api;

public class TmsDatabaseException : Exception
{
    public TmsDatabaseException(string message) : base(message) { }
}
