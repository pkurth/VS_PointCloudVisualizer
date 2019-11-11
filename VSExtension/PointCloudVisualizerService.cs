

namespace VSExtension
{
    using System;
    using System.Diagnostics;
    using System.Reflection;
    using System.Reflection.Emit;

    using Microsoft.VisualStudio;
    using Microsoft.VisualStudio.Debugger.Evaluation;
    using Microsoft.VisualStudio.Debugger.Interop;

    using System.Text.RegularExpressions;

    internal class PointCloudVisualizerService : SPointCloudVisualizerService, IVsCppDebugUIVisualizer
    {
        private readonly ModuleBuilder modBuilder;

        public PointCloudVisualizerService()
        {
            var thisAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            var asmName = new AssemblyName(thisAssemblyName + ".Dynamic");

            AssemblyBuilder asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            this.modBuilder = asmBuilder.DefineDynamicModule("TableRows");
        }

        PointCloudMemberData parseMembers(DkmSuccessEvaluationResult eval)
        {
            PointCloudMemberData members = new PointCloudMemberData();

            Regex pattern = new Regex(@"\[size\]=(\d+) \[positions\]=(0x[0-9A-Fa-f]+)");
            Match match = pattern.Match(eval.Value);

            members.size = int.Parse(match.Groups[1].Value);
            members.positionPtr = match.Groups[2].Value;

            return members;
        }

        public int DisplayValue(uint ownerHwnd, uint visualizerId, IDebugProperty3 debugProperty)
        {
            DkmSuccessEvaluationResult dkmEvalResult = DkmSuccessEvaluationResult.ExtractFromProperty(debugProperty);

            try
            {
                PointCloudVisualizerControl.Instance.AddItem(dkmEvalResult.FullName, parseMembers(dkmEvalResult));
            }
            catch (Exception e)
            {
                Debug.Fail("Visualization failed: " + e.Message);
                return e.HResult;
            }

            return VSConstants.S_OK;
        }
    }
}
