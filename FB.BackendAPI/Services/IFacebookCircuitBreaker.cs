namespace FB.BackendAPI.Services;

public interface IFacebookCircuitBreaker
{
    void ThrowIfOpen();

    void RecordSuccess();

    void RecordFailure(string? detail = null);
}
