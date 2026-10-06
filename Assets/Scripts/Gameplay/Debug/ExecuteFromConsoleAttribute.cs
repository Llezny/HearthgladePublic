using JetBrains.Annotations;

namespace Hearthglade.Gameplay.Debug
{
#if UNITY_2018_4_OR_NEWER
    [JetBrains.Annotations.MeansImplicitUse(
        JetBrains.Annotations.ImplicitUseKindFlags.Access |
        JetBrains.Annotations.ImplicitUseKindFlags.Assign |
        JetBrains.Annotations.ImplicitUseKindFlags.InstantiatedNoFixedConstructorSignature)]
#endif
    [System.AttributeUsage( System.AttributeTargets.Method ) ] 
    public class ExecuteFromConsoleAttribute : System.Attribute  
    {  
        public string name;  
        public string help;
        public ExecuteFromConsoleAttribute(string name, string help)  
        {  
            this.name = name;  
            this.help = help;
        }  
    }
}  