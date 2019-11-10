namespace VSExtension
{
    using System;
    using System.Runtime.InteropServices;
    using Microsoft.VisualStudio.Shell;

    /// <summary>
    /// This class implements the tool window exposed by this package and hosts a user control.
    /// </summary>
    /// <remarks>
    /// In Visual Studio tool windows are composed of a frame (implemented by the shell) and a pane,
    /// usually implemented by the package implementer.
    /// <para>
    /// This class derives from the ToolWindowPane class provided from the MPF in order to use its
    /// implementation of the IVsUIElementPane interface.
    /// </para>
    /// </remarks>
    [Guid("ff429683-5b40-4d01-83c4-beaf5411d9f0")]
    public class PointCloudVisualizer : ToolWindowPane
    {
        /// <summary>
        /// Initializes a new instance of the <see cref="PointCloudVisualizer"/> class.
        /// </summary>
        public PointCloudVisualizer() : base(null)
        {
            this.Caption = "PointCloudVisualizer";

            // This is the user control hosted by the tool window; Note that, even if this class implements IDisposable,
            // we are not calling Dispose on this object. This is because ToolWindowPane calls Dispose on
            // the object returned by the Content property.
            this.Content = new PointCloudVisualizerControl();
        }
    }
}
