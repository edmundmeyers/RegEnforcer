namespace RegEnforcer;

public class RegistryFixInfo
{
    public string Key { get; set; }
    public string ValueName { get; set; }
    public string Value { get; set; }    
    public object FoundValue { get; set; }      // Value currently in the registry-- opposed to value, which is what we expect to be there
}
