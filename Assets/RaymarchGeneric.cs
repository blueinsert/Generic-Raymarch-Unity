using UnityEngine;

[ExecuteInEditMode]
[RequireComponent(typeof(Camera))]
[AddComponentMenu("Effects/Raymarch (Generic Complete)")]
public class RaymarchGeneric : SceneViewFilter
{
    public Transform SunLight;

    [SerializeField]
    private Shader _EffectShader;
    [SerializeField]
    private Texture2D _MaterialColorRamp;
    [SerializeField]
    private Texture2D _PerfColorRamp;
    [SerializeField]
    private float _RaymarchDrawDistance = 40;
    [SerializeField]
    private bool _DebugPerformance = false;

    public Material EffectMaterial
    {
        get
        {
            if (!_EffectMaterial && _EffectShader)
            {
                _EffectShader = new Shader();
                _EffectMaterial = new Material(_EffectShader);
                _EffectMaterial.hideFlags = HideFlags.HideAndDontSave;
            }

            return _EffectMaterial;
        }
    }
    private Material _EffectMaterial;

    public Camera CurrentCamera
    {
        get
        {
            if (!_CurrentCamera)
                _CurrentCamera = GetComponent<Camera>();
            return _CurrentCamera;
        }
    }
    private Camera _CurrentCamera;

    void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        //在scene视图中绘制由摄像机指向视椎体四个顶点的向量
        Matrix4x4 corners = GetFrustumCorners(CurrentCamera);
        Vector3 pos = CurrentCamera.transform.position;

        for (int x = 0; x < 4; x++) {
            //将向量从摄像机坐标系转到世界坐标系
            corners.SetRow(x, CurrentCamera.cameraToWorldMatrix * corners.GetRow(x));
            Gizmos.DrawLine(pos, pos + (Vector3)(corners.GetRow(x)));
        }

        /*
        // UNCOMMENT TO DEBUG RAY DIRECTIONS
        Gizmos.color = Color.red;
        int n = 10; // # of intervals
        for (int x = 1; x < n; x++) {
            float i_x = (float)x / (float)n;

            var w_top = Vector3.Lerp(corners.GetRow(0), corners.GetRow(1), i_x);
            var w_bot = Vector3.Lerp(corners.GetRow(3), corners.GetRow(2), i_x);
            for (int y = 1; y < n; y++) {
                float i_y = (float)y / (float)n;
                
                var w = Vector3.Lerp(w_top, w_bot, i_y).normalized;
                Gizmos.DrawLine(pos + (Vector3)w, pos + (Vector3)w * 1.2f);
            }
        }
        */
    }

    /// <summary>
    /// 在所有渲染完成之后调用
    /// </summary>
    [ImageEffectOpaque]
    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (!EffectMaterial)
        {
            //位块传输，简单的赋值
            Graphics.Blit(source, destination); // do nothing
            return;
        }

        // Set any custom shader variables here.  For example, you could do:
        // EffectMaterial.SetFloat("_MyVariable", 13.37f);
        // This would set the shader uniform _MyVariable to value 13.37
        //设置着色器参数
        //光线方向，世界坐标系
        EffectMaterial.SetVector("_LightDir", SunLight ? SunLight.forward : Vector3.down);

        // Construct a Model Matrix for the Torus
        Matrix4x4 MatTorus = Matrix4x4.TRS(
            Vector3.right * Mathf.Sin(Time.time) * 5, 
            Quaternion.identity,
            Vector3.one);
        MatTorus *= Matrix4x4.TRS(
            Vector3.zero, 
            Quaternion.Euler(new Vector3(0, 0, (Time.time * 200) % 360)), 
            Vector3.one);
        // Send the torus matrix to our shader
        //圆环的模型转世界矩阵，定义了圆环的运动
        EffectMaterial.SetMatrix("_MatTorus_InvModel", MatTorus.inverse);

        EffectMaterial.SetTexture("_ColorRamp_Material", _MaterialColorRamp);
        EffectMaterial.SetTexture("_ColorRamp_PerfMap", _PerfColorRamp);
        //rayMarch算法最大前进距离
        EffectMaterial.SetFloat("_DrawDistance", _RaymarchDrawDistance);
        //性能调试开关
        if(EffectMaterial.IsKeywordEnabled("DEBUG_PERFORMANCE") != _DebugPerformance) {
            if(_DebugPerformance)
                EffectMaterial.EnableKeyword("DEBUG_PERFORMANCE");
            else
                EffectMaterial.DisableKeyword("DEBUG_PERFORMANCE");
        }
        //视椎体顶点向量矩阵，摄像机坐标系
        EffectMaterial.SetMatrix("_FrustumCornersES", GetFrustumCorners(CurrentCamera));
        //摄像机转世界矩阵
        EffectMaterial.SetMatrix("_CameraInvViewMatrix", CurrentCamera.cameraToWorldMatrix);
        //摄像机位置，世界坐标系
        EffectMaterial.SetVector("_CameraWS", CurrentCamera.transform.position);

        CustomGraphicsBlit(source, destination, EffectMaterial, 0);
    }

    /// \brief Stores the normalized rays representing the camera frustum in a 4x4 matrix.  Each row is a vector.
    /// 
    /// The following rays are stored in each row (in eyespace, not worldspace):
    /// Top Left corner:     row=0
    /// Top Right corner:    row=1
    /// Bottom Right corner: row=2
    /// Bottom Left corner:  row=3
    /// <summary>
    /// 返回在摄像机坐标系中定义的由摄像机位置指向近裁剪面的四个顶点的四个向量组成的矩阵
    /// 第一行：指向左上顶点的向量
    /// 第二行：指向右上顶点的向量
    /// 第三行：指向右下顶点的向量
    /// 第四行：指向左下顶点的向量
    /// </summary>
    private Matrix4x4 GetFrustumCorners(Camera cam)
    {
        float camFov = cam.fieldOfView;
        float camAspect = cam.aspect;

        Matrix4x4 frustumCorners = Matrix4x4.identity;

        float fovWHalf = camFov * 0.5f;

        float tan_fov = Mathf.Tan(fovWHalf * Mathf.Deg2Rad);

        Vector3 toRight = Vector3.right * tan_fov * camAspect;
        Vector3 toTop = Vector3.up * tan_fov;

        Vector3 topLeft = (-Vector3.forward - toRight + toTop);
        Vector3 topRight = (-Vector3.forward + toRight + toTop);
        Vector3 bottomRight = (-Vector3.forward + toRight - toTop);
        Vector3 bottomLeft = (-Vector3.forward - toRight - toTop);

        frustumCorners.SetRow(0, topLeft);
        frustumCorners.SetRow(1, topRight);
        frustumCorners.SetRow(2, bottomRight);
        frustumCorners.SetRow(3, bottomLeft);

        return frustumCorners;
    }

    /// \brief Custom version of Graphics.Blit that encodes frustum corner indices into the input vertices.
    /// 
    /// In a shader you can expect the following frustum cornder index information to get passed to the z coordinate:
    /// Top Left vertex:     z=0, u=0, v=0
    /// Top Right vertex:    z=1, u=1, v=0
    /// Bottom Right vertex: z=2, u=1, v=1
    /// Bottom Left vertex:  z=3, u=1, v=0
    /// 
    /// \warning You may need to account for flipped UVs on DirectX machines due to differing UV semantics
    ///          between OpenGL and DirectX.  Use the shader define UNITY_UV_STARTS_AT_TOP to account for this.
    /// <summary>
    /// 使用fxMaterial渲染了一个gl定义的quad,结果图像赋给了dest
    /// </summary>
    static void CustomGraphicsBlit(RenderTexture source, RenderTexture dest, Material fxMaterial, int passNr)
    {
        RenderTexture.active = dest;//shader为什么能影响到这个？
        //原图像作为“_MainTex”参数传给着色器
        fxMaterial.SetTexture("_MainTex", source);

        GL.PushMatrix();
        GL.LoadOrtho(); // Note: z value of vertices don't make a difference because we are using ortho projection

        fxMaterial.SetPass(passNr);

        GL.Begin(GL.QUADS);

        // Here, GL.MultitexCoord2(0, x, y) assigns the value (x, y) to the TEXCOORD0 slot in the shader.
        // GL.Vertex3(x,y,z) queues up a vertex at position (x, y, z) to be drawn.  Note that we are storing
        // our own custom frustum information in the z coordinate.
        //坐标的z值标识了对应于哪个顶点
        GL.MultiTexCoord2(0, 0.0f, 0.0f);
        GL.Vertex3(0.0f, 0.0f, 3.0f); // BL

        GL.MultiTexCoord2(0, 1.0f, 0.0f);
        GL.Vertex3(1.0f, 0.0f, 2.0f); // BR

        GL.MultiTexCoord2(0, 1.0f, 1.0f);
        GL.Vertex3(1.0f, 1.0f, 1.0f); // TR

        GL.MultiTexCoord2(0, 0.0f, 1.0f);
        GL.Vertex3(0.0f, 1.0f, 0.0f); // TL
        
        GL.End();
        GL.PopMatrix();
    }

}
