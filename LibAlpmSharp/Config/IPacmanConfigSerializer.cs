using LibAlpmSharp.Interop;

namespace LibAlpmSharp.Config;

public interface IPacmanConfigSerializer
{
    string Serialize(PacmanConfig config);
    
    string Serialize(PacmanRepositoryConfig config);
    string Serialize(AlpmSigLevel sigLevel);
}