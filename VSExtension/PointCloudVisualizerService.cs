

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
    using Microsoft.VisualStudio.Debugger.ComponentInterfaces;

    internal class PointCloudVisualizerService : SPointCloudVisualizerService, IVsCppDebugUIVisualizer
    {
        private readonly ModuleBuilder modBuilder;

        public PointCloudVisualizerService()
        {
            string thisAssemblyName = Assembly.GetExecutingAssembly().GetName().Name;
            AssemblyName asmName = new AssemblyName(thisAssemblyName + ".Dynamic");

            AssemblyBuilder asmBuilder = AppDomain.CurrentDomain.DefineDynamicAssembly(asmName, AssemblyBuilderAccess.RunAndCollect);
            this.modBuilder = asmBuilder.DefineDynamicModule("TableRows");
        }

        PointCloudVisualizationData parseMembers(DkmSuccessEvaluationResult eval)
        {
            PointCloudVisualizationData members = new PointCloudVisualizationData();

            {
                Regex pattern = new Regex(@"\[size\]=(\d+)");
                Match match = pattern.Match(eval.Value);
                if (match.Groups.Count > 1)
                {
                    members.size = int.Parse(match.Groups[1].Value);
                }
                else
                {
                    members.size = 0;
                }
            }
            {
                Regex pattern = new Regex(@"\[positions\]=(0x[0-9A-Fa-f]+)");
                Match match = pattern.Match(eval.Value);

                if (match.Groups.Count > 1)
                {
                    members.positionPtr = match.Groups[1].Value;
                }
                else
                {
                    members.positionPtr = null;
                }
            }
            {
                Regex pattern = new Regex(@"\[normals\]=(0x[0-9A-Fa-f]+)");
                Match match = pattern.Match(eval.Value);

                if (match.Groups.Count > 1)
                {
                    members.normalPtr = match.Groups[1].Value;
                }
                else
                {
                    members.normalPtr = null;
                }
            }
            {
                Regex pattern = new Regex(@"\[precision\]=(\w+)");
                Match match = pattern.Match(eval.Value);

                if (match.Groups.Count > 1)
                {
                    if (match.Groups[1].Value == "float")
                    {
                        members.precision = PointCloudPrecisionType.Float;
                    }
                    else if (match.Groups[1].Value == "double")
                    {
                        members.precision = PointCloudPrecisionType.Double;
                    }
                    else
                    {
                        members.precision = PointCloudPrecisionType.Unknown;
                    }
                }
                else
                {
                    members.precision = PointCloudPrecisionType.Float;
                }
            }
            {
                Regex pattern = new Regex(@"\[dimension\]=(\d+)");
                Match match = pattern.Match(eval.Value);

                if (match.Groups.Count > 1)
                {
                    members.dimension = int.Parse(match.Groups[1].Value);
                    if (members.dimension != 3 && members.dimension != 2)
                    {
                        members.dimension = 0;
                    }
                }
                else
                {
                    members.dimension = 3;
                }
            }


            return members;
        }

        public int DisplayValue(uint ownerHwnd, uint visualizerId, IDebugProperty3 debugProperty)
        {
            DEBUG_PROPERTY_INFO[] rawValue = new DEBUG_PROPERTY_INFO[1];
            debugProperty.GetPropertyInfo(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_RAW, 10, 10000, null, 0, rawValue);

            //IEnumDebugPropertyInfo2 propInfo;
            //Guid filter = Guid.Empty;
            //int enumResult = debugProperty.EnumChildren(enum_DEBUGPROP_INFO_FLAGS.DEBUGPROP_INFO_VALUE_RAW, 10, ref filter, enum_DBG_ATTRIB_FLAGS.DBG_ATTRIB_ACCESS_ALL, null, 10000, out propInfo);

            //uint bufferLength;
            //debugProperty.GetStringCharLength(out bufferLength);

            //ushort[] buffer = new ushort[bufferLength];
            //uint fetched;
            //debugProperty.GetStringChars(bufferLength, buffer, out fetched);

            DkmSuccessEvaluationResult dkmEvalResult = DkmSuccessEvaluationResult.ExtractFromProperty(debugProperty);
            //DkmInspectionContext inspectionContext = DkmInspectionContext.Create(
            //        dkmEvalResult.InspectionSession,
            //        dkmEvalResult.RuntimeInstance,
            //        dkmEvalResult.InspectionContext.Thread,
            //        1000,
            //        DkmEvaluationFlags.None,
            //        DkmFuncEvalFlags.None,
            //        10,
            //        dkmEvalResult.InspectionContext.Language,
            //        null);

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
