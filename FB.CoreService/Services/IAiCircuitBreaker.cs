namespace FB.CoreService.Services;

public interface IAiCircuitBreaker
{
    bool AllowRequest();

    void RecordSuccess();

    void RecordFailure();
}
