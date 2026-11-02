namespace RankingRon.Exceptions;

public class UnableToRetrieveDataException : Exception
{
    public UnableToRetrieveDataException()
    {
    }

    public UnableToRetrieveDataException(string message) : base(message)
    {
    }

    public UnableToRetrieveDataException(string message, Exception innerException) : base(message, innerException)
    {
    }
}
