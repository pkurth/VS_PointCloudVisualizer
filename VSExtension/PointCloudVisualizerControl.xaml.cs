namespace VSExtension
{
    using System.Diagnostics.CodeAnalysis;
    using System.Windows;
    using System.Windows.Controls;

    using EnvDTE;

    using System;
    using System.Collections.Generic;
    using System.Collections.ObjectModel;

    using SharpGL;
    using SharpGL.SceneGraph;
    using SharpGL.VertexBuffers;
    using SharpGL.Shaders;
    using SharpGL.Enumerations;
    using GlmNet;
    using System.Windows.Input;

    public enum PointCloudPrecisionType
    {
        Float,
        Double,
        Unknown
    };

    public class PointCloudVisualizationData
    {
        public int size;
        public string positionPtr;
        public string normalPtr;
        public string uvMemberPtr;

        public PointCloudPrecisionType precision = PointCloudPrecisionType.Float;

        public int dimension;
    }

    class ObservedVariable : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler PropertyChanged;
        private void NotifyPropertyChanged([System.Runtime.CompilerServices.CallerMemberName] string propertyName = "")
        {
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(propertyName));
        }

        public PointCloudVisualizationData MemberData { get; set; }

        protected string name = string.Empty;
        public string Name
        {
            get { return name; }
            set
            {
                if (value != name)
                {
                    name = value;
                    NotifyPropertyChanged("Name");
                }
            }
        }

        public ObservedVariable() { }
        public ObservedVariable(string name) { Name = name; }
    }

    class DrawingAxis
    {
        public vec3 Axis;
        string axisName;

        public int Id { get; set; }

        public string AxisName
        {
            get
            {
                return axisName;
            }
            set 
            {
                switch (value)
                {
                    case "Right":
                        Axis = new vec3(1, 0, 0);
                        break;
                    case "Left":
                        Axis = new vec3(-1, 0, 0);
                        break;
                    case "Up":
                        Axis = new vec3(0, 1, 0);
                        break;
                    case "Down":
                        Axis = new vec3(0, -1, 0);
                        break;
                    case "Forward":
                        Axis = new vec3(0, 0, -1);
                        break;
                    case "Backward":
                        Axis = new vec3(0, 0, 1);
                        break;
                }
                axisName = value;
            }
        }

        public DrawingAxis(string axis, int id)
        {
            AxisName = axis;
            Id = id;
        }
    }

    /// <summary>
    /// Interaction logic for PointCloudVisualizerControl.
    /// </summary>
    public partial class PointCloudVisualizerControl : UserControl
    {
        ObservableCollection<ObservedVariable> Variables { get; set; }
        int currentlyObservedVariable = -1;

        ObservableCollection<DrawingAxis> xAxisCollection;
        ObservableCollection<DrawingAxis> yAxisCollection;
        ObservableCollection<DrawingAxis> zAxisCollection;

        VertexBufferArray vao;
        VertexBuffer vbo;
        int numVertices = 0;

        ShaderProgram shader;

        mat4 projectionMatrix;
        mat4 viewMatrix;
        mat4 modelMatrix;
        mat4 coordinateSystemMatrix;

        Point lastMousePos;
        vec3 cameraPos;

        public static PointCloudVisualizerControl Instance;

        /// <summary>
        /// Initializes a new instance of the <see cref="PointCloudVisualizerControl"/> class.
        /// </summary>
        public PointCloudVisualizerControl()
        {
            Instance = this;

            DebugHandler.DebuggerEvents.OnEnterBreakMode += DebuggerEvents_OnEnterBreakMode;

            Variables = new ObservableCollection<ObservedVariable>();

            this.InitializeComponent();


            listBox.SelectionChanged += ListBox_SelectionChanged;

            listBox.ItemsSource = Variables;

            openGLControl.OpenGLInitialized += OpenGL_Initialize;
            openGLControl.Resized += OpenGL_Resize;
            openGLControl.OpenGLDraw += OpenGL_Draw;
            openGLControl.MouseLeftButtonDown += OpenGL_MouseDown;
            openGLControl.MouseLeftButtonUp += OpenGL_MouseUp;
            openGLControl.MouseMove += OpenGL_MouseMove;

            ResetAt(new ObservedVariable(), Variables.Count);
            SelectItem(0);

            {
                xAxisCollection = new ObservableCollection<DrawingAxis>();
                xAxisCollection.Add(new DrawingAxis("Right", 1));
                xAxisCollection.Add(new DrawingAxis("Left", 2));
                xAxisCollection.Add(new DrawingAxis("Up", 3));
                xAxisCollection.Add(new DrawingAxis("Down", 4));
                xAxisCollection.Add(new DrawingAxis("Forward", 5));
                xAxisCollection.Add(new DrawingAxis("Backward", 6));

                comboBoxXAxis.ItemsSource = xAxisCollection;
                comboBoxXAxis.DisplayMemberPath = "AxisName";
                comboBoxXAxis.SelectedValuePath = "Id";
                comboBoxXAxis.SelectedValue = "1";
                comboBoxXAxis.SelectionChanged += Axis_SelectionChanged;
            }

            {
                yAxisCollection = new ObservableCollection<DrawingAxis>();
                yAxisCollection.Add(new DrawingAxis("Right", 1));
                yAxisCollection.Add(new DrawingAxis("Left", 2));
                yAxisCollection.Add(new DrawingAxis("Up", 3));
                yAxisCollection.Add(new DrawingAxis("Down", 4));
                yAxisCollection.Add(new DrawingAxis("Forward", 5));
                yAxisCollection.Add(new DrawingAxis("Backward", 6));

                comboBoxYAxis.ItemsSource = yAxisCollection;
                comboBoxYAxis.DisplayMemberPath = "AxisName";
                comboBoxYAxis.SelectedValuePath = "Id";
                comboBoxYAxis.SelectedValue = "3";
                comboBoxYAxis.SelectionChanged += Axis_SelectionChanged;
            }

            {
                zAxisCollection = new ObservableCollection<DrawingAxis>();
                zAxisCollection.Add(new DrawingAxis("Right", 1));
                zAxisCollection.Add(new DrawingAxis("Left", 2));
                zAxisCollection.Add(new DrawingAxis("Up", 3));
                zAxisCollection.Add(new DrawingAxis("Down", 4));
                zAxisCollection.Add(new DrawingAxis("Forward", 5));
                zAxisCollection.Add(new DrawingAxis("Backward", 6));

                comboBoxZAxis.ItemsSource = zAxisCollection;
                comboBoxZAxis.DisplayMemberPath = "AxisName";
                comboBoxZAxis.SelectedValuePath = "Id";
                comboBoxZAxis.SelectedValue = "6";
                comboBoxZAxis.SelectionChanged += Axis_SelectionChanged;
            }

            coordinateSystemMatrix = new mat4(1);
            UpdateCoordinateSystemMatrix();
        }

        private void UpdateCoordinateSystemMatrix()
        {
            vec3 x = ((DrawingAxis)comboBoxXAxis.SelectedItem).Axis;
            vec3 y = ((DrawingAxis)comboBoxYAxis.SelectedItem).Axis;
            vec3 z = ((DrawingAxis)comboBoxZAxis.SelectedItem).Axis;

            coordinateSystemMatrix[0] = new vec4(x, 0);
            coordinateSystemMatrix[1] = new vec4(y, 0);
            coordinateSystemMatrix[2] = new vec4(z, 0);
        }

        private void OpenGL_Initialize(object sender, OpenGLEventArgs args)
        {
            openGLControl.OpenGLVersion = SharpGL.Version.OpenGLVersion.OpenGL3_3;
            OpenGL gl = openGLControl.OpenGL;

            vao = new VertexBufferArray();
            vao.Create(gl);
            vao.Bind(gl);

            vbo = new VertexBuffer();
            vbo.Create(gl);
            vbo.Bind(gl);
            
            gl.EnableVertexAttribArray(0);
            gl.VertexAttribPointer(0, 3, OpenGL.GL_FLOAT, false, 8 * 4, new System.IntPtr(0));
            gl.EnableVertexAttribArray(1);
            gl.VertexAttribPointer(1, 2, OpenGL.GL_FLOAT, false, 8 * 4, new System.IntPtr(12));
            gl.EnableVertexAttribArray(2);
            gl.VertexAttribPointer(2, 3, OpenGL.GL_FLOAT, false, 8 * 4, new System.IntPtr(20));

            gl.BindVertexArray(0);

            string vertexShaderSource =
                @"
                    #version 410

                    layout (location = 0) in vec3 in_position;
                    layout (location = 1) in vec2 in_uv;
                    layout (location = 2) in vec3 in_normal;

                    uniform mat4 mvp;
                    uniform mat4 m;

                    out vec3 normal;
                    out vec2 uv;

                    void main()
                    {
                        gl_Position = mvp * vec4(in_position, 1.0);
                        normal = (m * vec4(in_normal, 0.0)).xyz;
                        uv = in_uv;
                    }
                ";
            string fragmentShaderSource =
                @"
                    #version 410

                    layout (location = 0) out vec4 out_color;

                    in vec3 normal;
                    in vec2 uv;

                    void main()
                    {
                        out_color = vec4(1, 0, 0, 1);
                        if (length(normal) > 0.0)
                        {
                            out_color = vec4(abs(normal), 1);
                        }
                    }
                ";

            shader = new ShaderProgram();
            try
            {
                shader.Create(gl, vertexShaderSource, fragmentShaderSource, null);
            }
            catch (ShaderCompilationException e)
            {
                string error = e.CompilerOutput;
                string message = e.Message;
            }

            const float rads = (60.0f / 360.0f) * (float)Math.PI * 2.0f;
            projectionMatrix = glm.perspective(rads, (float)(openGLControl.ActualWidth / openGLControl.ActualHeight), 0.1f, 100.0f);

            modelMatrix = mat4.identity();

            cameraPos = new vec3(0.0f, 0.0f, 5.0f);
            viewMatrix = glm.lookAt(cameraPos, new vec3(0f), new vec3(0f, 1f, 0f));

            lastMousePos = new Point(-1, -1);

            gl.PointSize(5f);
            gl.ClearColor(0.2f, 0.3f, 0.8f, 1);
        }

        private void OpenGL_Resize(object sender, OpenGLEventArgs args)
        {
            const float rads = (60.0f / 360.0f) * (float)Math.PI * 2.0f;
            projectionMatrix = glm.perspective(rads, (float)(openGLControl.ActualWidth / openGLControl.ActualHeight), 0.1f, 100.0f);
        }

        private void OpenGL_Draw(object sender, OpenGLEventArgs args)
        {
            if (DebugHandler.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
            {
                OpenGL gl = openGLControl.OpenGL;
                gl.Clear(OpenGL.GL_COLOR_BUFFER_BIT | OpenGL.GL_DEPTH_BUFFER_BIT);

                //modelMatrix = glm.rotate(modelMatrix, glm.radians(5f), new vec3(0f, 1f, 0f));

                mat4 m = modelMatrix * coordinateSystemMatrix;
                mat4 mvp = projectionMatrix * viewMatrix * m;

                shader.Bind(gl);
                shader.SetUniformMatrix4(gl, "mvp", mvp.to_array());
                shader.SetUniformMatrix4(gl, "m", m.to_array());

                vao.Bind(gl);
                gl.DrawArrays(OpenGL.GL_POINTS, 0, numVertices);
            }
        }

        private void OpenGL_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(openGLControl);

                pos.X = pos.X / openGLControl.ActualWidth;
                pos.Y = pos.Y / openGLControl.ActualHeight;

                lastMousePos = pos;
            }
        }

        private void OpenGL_MouseUp(object sender, MouseEventArgs e)
        {
            lastMousePos.X = -1;
        }

        private void OpenGL_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Point pos = e.GetPosition(openGLControl);

                pos.X = pos.X / openGLControl.ActualWidth;
                pos.Y = pos.Y / openGLControl.ActualHeight;

                if (lastMousePos.X >= 0f)
                {
                    float deltaX = (float)(pos.X - lastMousePos.X);
                    float deltaY = (float)(pos.Y - lastMousePos.Y);

                    vec4 p = (glm.rotate(glm.radians(-deltaX * 50f), new vec3(0f, 1f, 0f)) * new vec4(cameraPos, 0f));
                    cameraPos = new vec3(p.x, p.y, p.z);
                    viewMatrix = glm.lookAt(cameraPos, new vec3(0f), new vec3(0f, 1f, 0f));
                }
                lastMousePos = pos;
            }
        }

        public void AddItem(string name, PointCloudVisualizationData memberData)
        {
            bool success = false;
            if (Variables.Count > 0)
            {
                var variable = Variables[Variables.Count - 1];
                if (variable.Name == null || variable.Name == "")
                {
                    variable = new ObservedVariable(name);
                    variable.MemberData = memberData;
                    ResetAt(variable, Variables.Count - 1);
                    SelectItem(Variables.Count - 1);
                    UpdateItem(Variables.Count - 1);
                    success = true;
                }
            }
            
            if (!success)
            {
                var variable = new ObservedVariable(name);
                variable.MemberData = memberData;
                ResetAt(variable, Variables.Count);
                SelectItem(Variables.Count - 1);
            }

            ResetAt(new ObservedVariable(), Variables.Count);
        }

        private void UpdateItem(int index)
        {
            numVertices = 0;

            if (index >= 0 && index < Variables.Count)
            {
                ObservedVariable variable = Variables[index];

                if (DebugHandler.Debugger.CurrentMode == dbgDebugMode.dbgBreakMode)
                {
                    PointCloud pointCloud = null;

                    if (variable.MemberData == null)
                    {
                        pointCloud = DebugHandler.Load(variable.Name);
                    }
                    else
                    {
                        pointCloud = DebugHandler.Load(variable.Name, variable.MemberData);
                    }

                    if (pointCloud != null)
                    {
                        OpenGL gl = openGLControl.OpenGL;

                        vao.Bind(gl);

                        vbo.Bind(gl);
                        gl.BufferData(OpenGL.GL_ARRAY_BUFFER, pointCloud.vertices, OpenGL.GL_STATIC_DRAW);
                        numVertices = pointCloud.numVertices;
                    }
                }
            }
        }

        private void ResetAt(ObservedVariable item, int index)
        {
            ((System.ComponentModel.INotifyPropertyChanged)item).PropertyChanged += ObservedVariable_NameChanged;
            if (index < Variables.Count)
                Variables.RemoveAt(index);
            Variables.Insert(index, item);
        }

        private void SelectItem(int index)
        {
            if (0 <= index && index < listBox.Items.Count)
            {
                object item = listBox.Items[index];
                listBox.SelectedItem = item;
                listBox.ScrollIntoView(item);
            }
            else
            {
                listBox.SelectedItem = null;
                numVertices = 0;
            }
        }

        private void SelectCurrentItem(object sender, KeyboardFocusChangedEventArgs e)
        {
            TextBox item = (TextBox)sender;

            ObservedVariable var = (ObservedVariable)item.DataContext;
            int index = Variables.IndexOf(var);
            SelectItem(index);
        }

        private void ListBox_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            ListBox list = (ListBox)sender; 
            ObservedVariable variable = (ObservedVariable)list.SelectedItem;
            int index = Variables.IndexOf(variable);

            currentlyObservedVariable = index;

            if (index < 0 || index >= listBox.Items.Count)
                return;

            UpdateItem(index);
        }

        private void Axis_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            UpdateCoordinateSystemMatrix();
        }

        private void ObservedVariable_NameChanged(object sender, System.ComponentModel.PropertyChangedEventArgs e)
        {
            ObservedVariable variable = (ObservedVariable)sender;
            int index = Variables.IndexOf(variable);

            if (index < 0 || index >= listBox.Items.Count)
                return;

            if (variable.Name == null || variable.Name == "")
            {
                if (index < listBox.Items.Count - 1)
                {
                    Variables.RemoveAt(index);
                    if (index > 0)
                    {
                        SelectItem(index - 1);
                    }
                }
            }
            else
            {
                int next_index = index + 1;
                if (next_index == Variables.Count)
                {
                    ResetAt(new ObservedVariable(), next_index);
                }
                SelectItem(index);
                UpdateItem(index);
            }
        }

        private void DebuggerEvents_OnEnterBreakMode(dbgEventReason Reason, ref dbgExecutionAction ExecutionAction)
        {
            UpdateItem(currentlyObservedVariable);
        }
    }
}