using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TestMain : MonoBehaviour
{
    [SerializeField]
    public int instanceCount = 10000;
    [SerializeField]
    public Mesh instanceMesh;
    [SerializeField]
    public Material instanceMaterial;
    [SerializeField]
    public int subMeshIndex = 0;
    [SerializeField]
    public ComputeShader cullingShader;

    private int cachedInstanceCount = -1;
    private int cachedSubMeshIndex = -1;
    private ComputeBuffer transfromBuffer;
    private ComputeBuffer argsBuffer;
    private uint[] args = new uint[5] { 0, 0, 0, 0, 0 };
    private int CSCullingID = -1;
    private ComputeBuffer posVisibleBuffer;

    private MaterialPropertyBlock materialBlock;

    void Start()
    {
        this.CSCullingID = this.cullingShader.FindKernel("CSCulling");
        this.materialBlock = new MaterialPropertyBlock();
        argsBuffer = new ComputeBuffer(1, args.Length * sizeof(uint), ComputeBufferType.IndirectArguments);

        UpdateBuffers();
    }

    void Update()
    {
        if (cachedInstanceCount != instanceCount || cachedSubMeshIndex != subMeshIndex)
            UpdateBuffers();

        this.ComputeView();

        // Render
        Graphics.DrawMeshInstancedIndirect(instanceMesh, subMeshIndex, instanceMaterial, new Bounds(Vector3.zero, new Vector3(100.0f, 100.0f, 100.0f)), argsBuffer, 0, this.materialBlock);
    }

    void ComputeView()
    {
        args[1] = 0;
        argsBuffer.SetData(args);

        this.cullingShader.SetBuffer(CSCullingID, "bufferWithArgs", argsBuffer);
        this.cullingShader.SetVector("cmrPos", Camera.main.transform.position);
        this.cullingShader.SetVector("cmrDir", Camera.main.transform.forward);
        this.cullingShader.SetFloat("cmrHalfFov", Camera.main.fieldOfView / 2);
        var m = GL.GetGPUProjectionMatrix(Camera.main.projectionMatrix, false) * Camera.main.worldToCameraMatrix;

        float[] mlist = new float[] {
                m.m00,m.m10,m.m20,m.m30,
               m.m01,m.m11,m.m21,m.m31,
                m.m02,m.m12,m.m22,m.m32,
                m.m03,m.m13,m.m23,m.m33
            };

        this.cullingShader.SetFloats("matrix_VP", mlist);
        this.cullingShader.Dispatch(CSCullingID, 400 / 16, 400 / 16, 1);

        this.materialBlock.SetInt("offsetIdx", 0);
    }

    void UpdateBuffers()
    {       
        if (instanceMesh != null)
            subMeshIndex = Mathf.Clamp(subMeshIndex, 0, instanceMesh.subMeshCount - 1);
        
        if (transfromBuffer != null)
            transfromBuffer.Release();

        if (this.posVisibleBuffer != null)
        {
            this.posVisibleBuffer.Release();
        }

        this.posVisibleBuffer = new ComputeBuffer(instanceCount, 16 * 4);
        this.transfromBuffer = new ComputeBuffer(instanceCount, 16 * 4);

        Matrix4x4[] transfroms = new Matrix4x4[instanceCount];
        for (int i = 0; i < instanceCount; i++)
        {
            float x = Random.Range(-100f, 100f);
            float y = Random.Range(-10f, 30f);
            float z = Random.Range(-100f, 100f);
            float w = Random.Range(0f, 360f);
            float scalex = Random.Range(0.1f, 5f);
            float scaley = Random.Range(0.1f, 5f);
            float scalez = Random.Range(0.1f, 5f);

            transfroms[i] = Matrix4x4.TRS(new Vector3(x, y, z), Quaternion.Euler(Random.Range(0f, 360f), w, Random.Range(0f, 360f)), new Vector3(scalex, scaley, scalez));

        }

        transfromBuffer.SetData(transfroms);

        this.cullingShader.SetBuffer(CSCullingID, "posAllBuffer", this.transfromBuffer);
        this.cullingShader.SetBuffer(CSCullingID, "posVisibleBuffer", this.posVisibleBuffer);

        this.instanceMaterial.SetBuffer("transformBuffer", posVisibleBuffer);
        
        if (instanceMesh != null)
        {
            args[0] = (uint)instanceMesh.GetIndexCount(subMeshIndex);
            args[1] = (uint)instanceCount;
            args[2] = (uint)instanceMesh.GetIndexStart(subMeshIndex);
            args[3] = (uint)instanceMesh.GetBaseVertex(subMeshIndex);
        }
        else
        {
            args[0] = args[1] = args[2] = args[3] = 0;
        }

        argsBuffer.SetData(args);

        cachedInstanceCount = instanceCount;
        cachedSubMeshIndex = subMeshIndex;
    }

    void OnDisable()
    {
        if (transfromBuffer != null)
            transfromBuffer.Release();
        transfromBuffer = null;


        if (this.posVisibleBuffer != null)
        {
            this.posVisibleBuffer.Release();
        }

        this.posVisibleBuffer = null;

        if (argsBuffer != null)
            argsBuffer.Release();
        argsBuffer = null;
    }
}
