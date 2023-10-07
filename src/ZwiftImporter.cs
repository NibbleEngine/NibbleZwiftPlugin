using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using NbCore;
using NbCore.Math;
using NbCore.Plugins;
using ComponentAce.Compression.Libs.zlib;
using NbCore.Platform.Windowing;

namespace NibbleZwiftPlugin
{
    public class ZwiftImporter
    {
        public static PluginBase PluginRef { get; set; }

        public static List<NbTexture> Textures;
        public static List<NbMaterial> Materials;

        public static void ClearState()
        {
            Textures = new List<NbTexture>();
            Materials = new List<NbMaterial>();
        }

        public static string ReadNullTerminatedString(BinaryReader br)
        {
            StringBuilder strb = new StringBuilder();

            Char c = '1';
            while (c != 0)
            {
                c = br.ReadChar();
                strb.Append(c);
            }

            return strb.ToString();
        }


        public static SceneGraphNode Import(string filepath) {


            //Create Scene Node
            SceneGraphNode scene = PluginRef.EngineRef.CreateLocatorNode("GDE_Scene");
            SceneComponent sc = new SceneComponent();
            scene.AddComponent<SceneComponent>(sc);

            
            FileStream fs = new FileStream(filepath, FileMode.Open, FileAccess.Read);
            BinaryReader br = new BinaryReader(fs);

            //Fetch counters
            br.BaseStream.Seek(0xC, SeekOrigin.Begin);
            int texture_count = (int) br.ReadUInt16();
            br.BaseStream.Seek(0x8, SeekOrigin.Begin);
            int material_count = (int) br.ReadUInt16();

            //Fetch Section Offsets
            br.BaseStream.Seek(0x18, SeekOrigin.Begin);
            uint material_section_offset = br.ReadUInt32();
            br.BaseStream.Seek(0x20, SeekOrigin.Begin);
            uint texture_section_offset = br.ReadUInt32();
            br.BaseStream.Seek(0x38, SeekOrigin.Begin);
            uint model_section_offset = br.ReadUInt32();


            
            //Fetch textures
            for (int i = 0; i < texture_count; i++)
            {
                br.BaseStream.Seek(texture_section_offset + 0x20 * i, SeekOrigin.Begin);
                uint texture_name_offset = br.ReadUInt32();
                br.BaseStream.Seek(texture_name_offset, SeekOrigin.Begin);
                string texture_name = ReadNullTerminatedString(br);
                PluginRef.Log("Found Texture: " + texture_name, LogVerbosityLevel.INFO);

                //TODO: Parse texture files
                Textures.Add(LoadTexture(texture_name));
            }


            //Fetch Materials
            for (int i = 0; i < material_count; i++)
            {
                br.BaseStream.Seek(material_section_offset + 0x170 * i, SeekOrigin.Begin);
                string material_name = ReadNullTerminatedString(br);
                PluginRef.Log("Found Material: " + material_name, LogVerbosityLevel.INFO);


                NbMaterial mat = new NbMaterial();
                mat.Name = material_name;
                
                //Parse texture ids
                br.BaseStream.Seek(material_section_offset + 0x170 * i + 0x110, SeekOrigin.Begin);

                for (int j = 0; j < 8; j++)
                {
                    int tex_index = br.ReadByte();

                    if (tex_index != 0xFF && Textures[tex_index] != null)
                    {
                        NbSampler _sampler = new NbSampler()
                        {
                            SamplerID = mat.Samplers.Count,
                            Texture = Textures[tex_index]
                        };

                        if (j == 0)
                        {
                            _sampler.Name = "Diffuse Map";
                            _sampler.ShaderBinding = "mpCustomPerMaterial.gDiffuseMap";
                        } else if (j == 1)
                        {
                            _sampler.Name = "Normal Map";
                            _sampler.ShaderBinding = "mpCustomPerMaterial.gNormalMap";
                        }

                        mat.Samplers.Add(_sampler);
                    }
                }

                //Get correct shader config
                NbShaderSource conf_vs = NbCore.Common.RenderState.engineRef.GetShaderSourceByFilePath("./Assets/Shaders/Source/Simple_VS.glsl");
                NbShaderSource conf_fs = NbCore.Common.RenderState.engineRef.GetShaderSourceByFilePath("./Assets/Shaders/Source/ubershader_fs.glsl");
                NbShaderMode conf_mode = NbShaderMode.DEFFERED;

                ulong conf_hash = NbShaderConfig.GetHash(conf_vs, conf_fs, null, null, null, conf_mode);

                NbShaderConfig conf = PluginRef.EngineRef.GetShaderConfigByHash(conf_hash);
                if (conf == null)
                {
                    conf = new NbShaderConfig(conf_vs, conf_fs, null, null, null, conf_mode);
                }

                //Compile Material Shader
                ulong shader_hash = PluginRef.EngineRef.CalculateShaderHash(conf, PluginRef.EngineRef.GetMaterialShaderDirectives(mat));

                NbShader shader = PluginRef.EngineRef.GetShaderByHash(shader_hash);
                if (shader == null)
                {
                    shader = new()
                    {
                        directives = PluginRef.EngineRef.GetMaterialShaderDirectives(mat)
                    };

                    shader.SetShaderConfig(conf);
                    PluginRef.EngineRef.CompileShader(shader);
                }

                mat.AttachShader(shader);
                Materials.Add(mat);
            }

            //Fetch model information
            br.BaseStream.Seek(model_section_offset, SeekOrigin.Begin);
            uint field_num = br.ReadUInt32();
            uint model_num = br.ReadUInt32();
            br.BaseStream.Seek(0x8, SeekOrigin.Current);

            for (int i = 0; i < model_num; i++)
            {
                br.BaseStream.Seek(model_section_offset + 0x10 + field_num * 0x8 * i, SeekOrigin.Begin);

                uint indices_section_offset = br.ReadUInt32();
                br.BaseStream.Seek(0x24, SeekOrigin.Current);
                uint vertices_offset = br.ReadUInt32();
                br.BaseStream.Seek(0x10, SeekOrigin.Current);
                uint vertices_count = br.ReadUInt32();


                //Get Model data

                NbMeshData _meshdata = new();
                
                //Get Index Stream offsets
                br.BaseStream.Seek(indices_section_offset, SeekOrigin.Begin);
                uint indices_stream0_offset = br.ReadUInt32();
                br.BaseStream.Seek(0x8, SeekOrigin.Current);
                uint indices_stream0_count = br.ReadUInt32();
                br.BaseStream.Seek(0x6, SeekOrigin.Current);
                int mat_id_stream0 = br.ReadByte();
                br.ReadByte();
                uint indices_stream1_offset = br.ReadUInt32();
                br.BaseStream.Seek(0x8, SeekOrigin.Current);
                uint indices_stream1_count = br.ReadUInt32();
                br.BaseStream.Seek(0x6, SeekOrigin.Current);
                int mat_id_stream1 = br.ReadByte();


                //Get index buffer of stream 0
                br.BaseStream.Seek(indices_stream0_offset, SeekOrigin.Begin);
                byte[] indices_stream0_data = br.ReadBytes((int)indices_stream0_count * 0x2);

                //Get index buffer of stream 1
                br.BaseStream.Seek(indices_stream1_offset, SeekOrigin.Begin);
                byte[] indices_stream1_data = br.ReadBytes((int)indices_stream1_count * 0x2);
                
                //Get vertex buffer of model
                br.BaseStream.Seek(vertices_offset, SeekOrigin.Begin);
                int stride = 0x24;
                byte[] vx_buffer = br.ReadBytes((int)vertices_count * 0x24);


                NbMeshBufferInfo[] vx_buffer_info = new NbMeshBufferInfo[5];

                //Vertices
                vx_buffer_info[0] = new()
                {
                    count = 3,
                    normalize = false,
                    offset = 0x0,
                    semantic = NbBufferSemantic.VERTEX,
                    sem_text = "vPosition",
                    stride = 0x24,
                    type = NbPrimitiveDataType.Float
                };

                //UVs
                vx_buffer_info[1] = new()
                {
                    count = 2,
                    normalize = false,
                    offset = 0x10,
                    semantic = NbBufferSemantic.UV,
                    sem_text = "uvPosition",
                    stride = 0x24,
                    type = NbPrimitiveDataType.Float
                };

                //Normals
                vx_buffer_info[2] = new()
                {
                    count = 4,
                    normalize = true,
                    offset = 0x18,
                    semantic = NbBufferSemantic.NORMAL,
                    sem_text = "nPosition",
                    stride = 0x24,
                    type = NbPrimitiveDataType.Byte
                };

                //Tangents
                vx_buffer_info[3] = new()
                {
                    count = 4,
                    normalize = true,
                    offset = 0x1C,
                    semantic = NbBufferSemantic.TANGENT,
                    sem_text = "tPosition",
                    stride = 0x24,
                    type = NbPrimitiveDataType.Byte
                };

                //Tangents
                vx_buffer_info[4] = new()
                {
                    count = 4,
                    normalize = true,
                    offset = 0x20,
                    semantic = NbBufferSemantic.BITANGENT,
                    sem_text = "bPosition",
                    stride = 0x24,
                    type = NbPrimitiveDataType.Byte
                };

                
                //Create Mesh for Indices 0 stream
                _meshdata = new()
                {
                    buffers = vx_buffer_info,
                    IndexBuffer = indices_stream0_data,
                    IndexFormat = NbPrimitiveDataType.UnsignedShort,
                    IndicesType = NbRenderPrimitive.TriangleStrip,
                    VertexBuffer = vx_buffer,
                    VertexBufferStride = (uint) stride,
                };

                _meshdata.Hash = NbHasher.CombineHash(NbHasher.Hash(_meshdata.VertexBuffer),
                                                        NbHasher.Hash(_meshdata.IndexBuffer));

                //Create Mesh metadata
                NbMeshMetaData _metadata = new()
                {
                    BatchCount = (int) indices_stream0_count,
                    FirstSkinMat = 0,
                    LastSkinMat = 0,
                    VertrEndGraphics = (int) vertices_count - 1,
                    VertrEndPhysics = (int) vertices_count,
                    AABBMAX = new NbVector3(-1000000.0f),
                    AABBMIN = new NbVector3(1000000.0f),
                };

                //Generate NbMesh
                NbMesh _mesh = new()
                {
                    Hash = NbHasher.CombineHash(_meshdata.Hash, _metadata.GetHash()),
                    Data = _meshdata,
                    MetaData = _metadata,
                    Material = Materials[mat_id_stream0]
                };

                //Create Mesh Node
                SceneGraphNode node = PluginRef.EngineRef.CreateMeshNode("mesh_" + i + "stream0", _mesh);
                sc.AddNode(node);
                node.SetParent(scene);



                //Create Mesh for Indices 1 stream
                _meshdata = new()
                {
                    buffers = vx_buffer_info,
                    IndexBuffer = indices_stream1_data,
                    IndexFormat = NbPrimitiveDataType.UnsignedShort,
                    IndicesType = NbRenderPrimitive.TriangleStrip,
                    VertexBuffer = vx_buffer,
                    VertexBufferStride = (uint)stride,
                };

                _meshdata.Hash = NbHasher.CombineHash(NbHasher.Hash(_meshdata.VertexBuffer),
                                                        NbHasher.Hash(_meshdata.IndexBuffer));

                //Create Mesh metadata
                _metadata = new()
                {
                    BatchCount = (int)indices_stream1_count,
                    FirstSkinMat = 0,
                    LastSkinMat = 0,
                    VertrEndGraphics = (int)vertices_count - 1,
                    VertrEndPhysics = (int)vertices_count,
                    AABBMAX = new NbVector3(-1000000.0f),
                    AABBMIN = new NbVector3(1000000.0f),
                };

                //Generate NbMesh
                _mesh = new()
                {
                    Hash = NbHasher.CombineHash(_meshdata.Hash, _metadata.GetHash()),
                    Data = _meshdata,
                    MetaData = _metadata,
                    Material = Materials[mat_id_stream1]
                };

                //Create Mesh Node
                node = PluginRef.EngineRef.CreateMeshNode("mesh_" + i + "stream1", _mesh);
                sc.AddNode(node);
                node.SetParent(scene);


            }

            br.Close();

            return scene;
        }

        private static NbTexture LoadTexture(string path)
        {
            //Get texture full path
            path = "b" + path.Substring(1, path.Length - 6) + ".ztx";
            string tex_full_path = Path.Combine(((ZwiftPluginSettings)PluginRef.Settings).BaseDir, path);
            PluginRef.Log($"Loading texture from {tex_full_path}", LogVerbosityLevel.INFO);
            
            if (!File.Exists(tex_full_path))
            {
                PluginRef.Log($"Texture {tex_full_path} does not exist", LogVerbosityLevel.INFO);
                return null;
            }

            //Decompress stream
            FileStream tex_file = new FileStream(tex_full_path, FileMode.Open);
            BinaryReader br = new BinaryReader(tex_file);
            //Get compressed file info
            br.ReadUInt32(); //ZHR header
            int decompressed_file_size = (int) br.ReadUInt32();
            br.BaseStream.Seek(0x10, SeekOrigin.Begin);
            byte[] comp_data = br.ReadBytes((int)br.BaseStream.Length - 0x10);
            br.Close();

            MemoryStream decomp_stream = new MemoryStream();
            ZOutputStream zs = new ZOutputStream(decomp_stream);
            zs.Write(comp_data, 0, comp_data.Length);
            zs.Flush();
            
            
            //FileStream decomp_file = new FileStream("tex_test.tgax", FileMode.Create, FileAccess.Write);
            decomp_stream.Position = 0;
            //decomp_stream.CopyTo(decomp_file);
            //decomp_file.Close();

            //Get texture info
            br = new BinaryReader(decomp_stream);
            decomp_stream.Seek(0xC, SeekOrigin.Begin);
            uint tex_width = br.ReadUInt16();
            uint tex_height = br.ReadUInt16();
            uint tex_type = br.ReadUInt16();
            byte[] tex_raw_data = br.ReadBytes((int) decomp_stream.Length - 0x12);
            br.Close();

            PluginRef.Log($"Texture Width {tex_width} Height {tex_height} Type {tex_type}", LogVerbosityLevel.INFO);


            NbTextureInternalFormat _pif = NbTextureInternalFormat.DXT1;
            int block_size = 8;
            if (tex_type != 0x18)
            {
                //TODO: Check if there are other types
                _pif = NbTextureInternalFormat.DXT5;
                block_size = 16;
            }

            //Identify mipmap count
            int tex_size = 0;
            int mipmap_count = 0;
            int pitch_size = (int) (tex_width * tex_height * block_size) / 16;

            while(tex_size < tex_raw_data.Length)
            {
                tex_size += pitch_size;
                pitch_size = Math.Max(block_size, pitch_size / 4);
                mipmap_count++;
            }

            //Create NbTexture from file
            DDSImage tex_data = new()
            {
                target = NbTextureTarget.Texture2D,
                Data = tex_raw_data,
                Depth = 1,
                Faces = 1,
                blockSize = block_size,
                WrapMode = NbTextureWrapMode.Repeat,
                MagFilter = NbTextureFilter.Linear,
                MinFilter = NbTextureFilter.Linear,
                Width = (int)tex_width,
                Height = (int)tex_height,
                MipMapCount = mipmap_count,
                pif = _pif
            };

            //Manually Generate Texture
            NbTexture tex = new()
            {
                Path = tex_full_path,
                Data = tex_data,
            };

            PluginRef.EngineRef.CreateTexture(tex);

            return tex;

        }



    }
}


