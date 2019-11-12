using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

using EnvDTE;
using EnvDTE80;
using Microsoft.VisualStudio.Shell;
using System.Globalization;

namespace VSExtension
{
    class PointCloud
    {
        public float[] vertices; // Interleaved. 3 floats positions, 2 floats uvs, 3 floats normals, etc
        public int numVertices;

        public PointCloud(int size)
        {
            vertices = new float[size * 8];
            numVertices = size;
        }

        virtual public bool IsUserDefined()
        {
            return false;
        }
    }

    class DebugHandler
    {
        private DTE2 dte;
        private Debugger debugger;
        private DebuggerEvents debuggerEvents;

        private static DebugHandler Instance { get; set; }

        public static void Initialize(PointCloudVisualizerPackage package)
        {
            DTE2 dte = package.GetService(typeof(DTE)) as DTE2;
            Instance = new DebugHandler(dte);
        }

        private DebugHandler(DTE2 dte)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            this.dte = dte;
            this.debugger = dte.Debugger;
            this.debuggerEvents = this.dte.Events.DebuggerEvents;
        }

        public static Debugger Debugger
        {
            get { return Instance.debugger; }
        }

        public static DebuggerEvents DebuggerEvents
        {
            get { return Instance.debuggerEvents; }
        }

        static Expression[] GetExpressions(string name, char separator = ';')
        {
            var expr = Debugger.GetExpression(name);
            if (expr.IsValidValue)
                return new Expression[] { expr };

            string[] subnames = name.Split(separator);
            Expression[] exprs = new Expression[subnames.Length];
            for (int i = 0; i < subnames.Length; ++i)
            {
                exprs[i] = Debugger.GetExpression(subnames[i]);
            }

            return exprs;
        }

        static bool AllValidValues(Expression[] exprs)
        {
            foreach (Expression e in exprs)
                if (!e.IsValidValue)
                    return false;
            return true;
        }

        static PointCloud GetPointCloud(int size, string positionPtr, string normalsPtr)
        {
            Debugger debugger = Instance.debugger;

            PointCloud result = new PointCloud(size);

            for (int i = 0; i < size; ++i)
            {
                if (positionPtr != null)
                {
                    string xName = positionPtr + "[" + (i * 3 + 0) + "]";
                    string yName = positionPtr + "[" + (i * 3 + 1) + "]";
                    string zName = positionPtr + "[" + (i * 3 + 2) + "]";

                    float x, y, z;
                    TryLoadFloat(debugger, xName, out x);
                    TryLoadFloat(debugger, yName, out y);
                    TryLoadFloat(debugger, zName, out z);

                    result.vertices[i * 8 + 0] = x;
                    result.vertices[i * 8 + 1] = y;
                    result.vertices[i * 8 + 2] = z;
                }
                else
                {
                    result.vertices[i * 8 + 0] = 0;
                    result.vertices[i * 8 + 1] = 0;
                    result.vertices[i * 8 + 2] = 0;
                }

                result.vertices[i * 8 + 3] = 0;
                result.vertices[i * 8 + 4] = 0;

                if (normalsPtr != null)
                {
                    string xName = normalsPtr + "[" + (i * 3 + 0) + "]";
                    string yName = normalsPtr + "[" + (i * 3 + 1) + "]";
                    string zName = normalsPtr + "[" + (i * 3 + 2) + "]";

                    float x, y, z;
                    TryLoadFloat(debugger, xName, out x);
                    TryLoadFloat(debugger, yName, out y);
                    TryLoadFloat(debugger, zName, out z);

                    result.vertices[i * 8 + 5] = x;
                    result.vertices[i * 8 + 6] = y;
                    result.vertices[i * 8 + 7] = z;
                }
                else
                {
                    result.vertices[i * 8 + 5] = 0;
                    result.vertices[i * 8 + 6] = 0;
                    result.vertices[i * 8 + 7] = 0;
                }
            }

            return result;
        }

        public static PointCloud Load(string name, PointCloudVisualizationData memberData)
        {
            int size = memberData.size;

            string positionPtr = "((float*)" + memberData.positionPtr + ")";

            return GetPointCloud(size, positionPtr, null);
        }

        public static PointCloud Load(string name)
        {
            ThreadHelper.ThrowIfNotOnUIThread();

            Expression[] exprs = GetExpressions(name);
            if (exprs.Length < 1 || !AllValidValues(exprs))
            {
                return null;
            }

            string expressionName = exprs[0].Name;
            string expressionType = exprs[0].Type;

            if (expressionType != "point_cloud")
            {
                return null;
            }

            expressionName = "(" + expressionName + ")";

            Debugger debugger = Instance.debugger;
            int size = LoadInt(debugger, expressionName + ".size");
            string positionPtr = "((float*)" + expressionName + ".positions)";
            string normalsPtr = "((float*)" + expressionName + ".normals)";

            return GetPointCloud(size, positionPtr, normalsPtr);
        }

        static int ParseInt(string val, bool isHex)
        {
            return isHex
                 ? int.Parse(val.Substring(2), NumberStyles.HexNumber)
                 : int.Parse(val);
        }

        static float ParseFloat(string s)
        {
            return float.Parse(s, CultureInfo.InvariantCulture);
        }

        static double ParseDouble(string s)
        {
            return double.Parse(s, CultureInfo.InvariantCulture);
        }

        static int LoadInt(Debugger debugger, string name)
        {
            Expression expr = debugger.GetExpression(name);
            return expr.IsValidValue
                 ? Math.Max(ParseInt(expr.Value, debugger.HexDisplayMode), 0)
                 : 0;
        }

        static bool TryLoadFloat(Debugger debugger, string name, out float result)
        {
            result = 0.0f;
            Expression expr = debugger.GetExpression("(float)" + name);
            if (!expr.IsValidValue)
                return false;
            result = ParseFloat(expr.Value);
            return true;
        }
    }
}
