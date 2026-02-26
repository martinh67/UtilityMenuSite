namespace UtilityMenuSite.Core.Exceptions;

public class SeatLimitExceededException : Exception
{
    public SeatLimitExceededException(string message) : base(message) { }
}
