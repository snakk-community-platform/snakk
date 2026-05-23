namespace Snakk.Web.Services;

public class OAuthProvidersOptions
{
    public bool Google { get; set; }
    public bool GitHub { get; set; }
    public bool Discord { get; set; }
    public bool Facebook { get; set; }
    public bool Microsoft { get; set; }
    public bool Steam { get; set; }
    public bool HasAny => Google || GitHub || Discord || Facebook || Microsoft || Steam;
}
