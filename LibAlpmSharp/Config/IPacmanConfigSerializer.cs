using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public interface IPacmanConfigSerializer
{
    public string Serialize(PacmanConfig config);
    
    public string Serialize(PacmanRepositoryConfig config);
    string Serialize(AlpmSigLevel sigLevel);
}