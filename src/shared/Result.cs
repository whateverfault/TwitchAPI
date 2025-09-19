namespace TwitchAPI.shared;

public class Result<TV, TE> {
    public TV? Value { get; }
    public TE? Error { get; }

    public bool Ok => Error == null;
    
    
    public Result(TV? value, TE? error) {
        Value = value;
        Error = error;
    }
}