//Unity2DFluidSim by David Westberg https://github.com/Zombie1111/Unity2DFluidSim
using System.Collections.Generic;
using UnityEngine;
using Unity.Burst;
using Unity.Collections;
using UnityEngine.Jobs;
using Unity.Jobs;
using System.Linq;
using System;
using NUnit.Framework.Internal.Commands;

#if UNITY_EDITOR
using UnityEditor;
#endif

namespace Zomb2DPhysics
{
    public class WaterPhy : MonoBehaviour
    {
        #region Main Tick+Awake
        [Header("Water Rendering Config")]
        [Tooltip("The camera that will be rendering the water")]
        [SerializeField] private WaterRenderingPost waterCamera = null;
        [Tooltip("Only the water should have this layer and only the waterCamera should render it")][SerializeField] private int waterLayer = 4;
        [Tooltip("The mesh used to render the water")][SerializeField] private Mesh wPat_mesh;
        [Tooltip("The material the mesh rendering the water should use")][SerializeField] private Material wPat_mat;

        [Space()]
        [Header("Water Spawn Config")]
        [Tooltip("The water particels will spawn between waterPatStartTrans and waterPatEndTrans. Stacked on top of each other in waterPatStartTrans up axis")]
        [SerializeField] private Transform waterPatStartTrans = null;
        [Tooltip("The water particels will spawn between waterPatStartTrans and waterPatEndTrans")]
        [SerializeField] private Transform waterPatEndTrans = null;
#if !FLUID_NORBBUOYANCY
        private WaterColliderBoxRb[] waterColsRb = null;
#endif
        private WaterPatTick_work wpt_job;
        private JobHandle wpt_handle;

#if UNITY_EDITOR
        private void OnDrawGizmos()
        {
            Transform trans = Selection.activeTransform;
            if (trans == null || waterPatStartTrans == null || waterPatEndTrans == null) return;
            if (trans != transform && trans != waterPatStartTrans && trans != waterPatEndTrans) return;

            Gizmos.color = Color.blue;
            Gizmos.DrawSphere(waterPatStartTrans.position, WaterPhyGlobals.waterRadius);
            Gizmos.DrawSphere(waterPatEndTrans.position, WaterPhyGlobals.waterRadius);
            Gizmos.DrawLine(waterPatStartTrans.position, waterPatEndTrans.position);
        }
#endif

        private void Start()
        {
            if (waterCamera == null) throw new Exception(transform.name + " waterCamera has not been assigned");
            if (wPat_mesh == null) throw new Exception(transform.name + " wPat_mesh has not been assigned");
            if (wPat_mat == null) throw new Exception(transform.name + " wPat_mat has not been assigned");
            if (waterPatStartTrans == null) throw new Exception(transform.name + " waterPatStartTrans has not been assigned");
            if (waterPatEndTrans == null) throw new Exception(transform.name + " waterPatEndTrans has not been assigned");

            SetupWaterPats();
            SetupWaterColliders();
            SetupWaterPatsRendering();
        }

        private void OnDestroy()
        {
            Dealloate();
        }

        private void FixedUpdate()
        {
            TickWaterParticelsEnd(Time.deltaTime);
            TickWaterParticelsStart(Time.deltaTime);
        }

        private void Update()
        {
            TickRenderWaterPats();
        }
        #endregion Main Tick+Awake

        #region Set Particels
        private void SetupWaterPats()
        {
            //Setup job
            wpt_job = new()
            {
                deltaTime = 1.0f,
                waterCols = new NativeArray<WaterCol>(WaterPhyGlobals.maxWaterColliders, Allocator.Persistent),
                waterPatsWrite = new NativeArray<WaterPat>(WaterPhyGlobals.patCount, Allocator.Persistent),
                waterPatPoss = new NativeArray<Vector2>(WaterPhyGlobals.patCount, Allocator.Persistent),
                waterPatLowestMidHighestY = new NativeReference<Vector3>(Allocator.Persistent),
                cellIdToPatIndex = new(WaterPhyGlobals.patCount / 2, Allocator.Persistent)
            };

#if !FLUID_NORBBUOYANCY
            waterColsRb = new WaterColliderBoxRb[WaterPhyGlobals.maxWaterColliders];
#endif

            //Create pat trans
            CreateOrResetWaterParticels();
        }

        private bool patsHasBeenCreated = false;

        /// <summary>
        /// Call to create water particels or reset them if they are already created
        /// </summary>
        public void CreateOrResetWaterParticels()
        {
            List<WaterPat> newWPats = new();
            Vector2 spawnStartPos = (Vector2)waterPatStartTrans.position;
            Vector2 towardsEndStep = (Vector2)waterPatEndTrans.position - spawnStartPos;
            Vector2 spawnUpDir = (Vector2)waterPatStartTrans.up;
            float patSize = WaterPhyGlobals.waterRadius * 2.0f;
            float endDis = towardsEndStep.magnitude;
            towardsEndStep = towardsEndStep.normalized * patSize;
            int spawnI = 0;
            int rowNumber = 0;

            while (spawnI < WaterPhyGlobals.patCount)
            {
                SpawnRow(rowNumber);
                rowNumber++;
            }

            //Randomize particle order
            if (patsHasBeenCreated == true) return;

            int i = 0;
            foreach (var wPat in newWPats.Shuffle())
            {
                wpt_job.waterPatsWrite[i] = wPat;
                i++;
            }

            patsHasBeenCreated = true;

            void SpawnRow(int rowI)
            {
                Vector2 nowPos = spawnStartPos + (patSize * rowI * spawnUpDir);
                float nowDis = 0.0f;

                while (nowDis < endDis && spawnI < WaterPhyGlobals.patCount)
                {
                    CreateWaterPatAtPosition(nowPos, Vector2.zero);
                    nowPos += towardsEndStep;
                    spawnI++;
                    nowDis += patSize;
                }
            }

            void CreateWaterPatAtPosition(Vector2 pos, Vector2 vel = default)
            {
                var wPat = wpt_job.waterPatsWrite[spawnI];
                wPat.pos = pos;
                wPat.vel = vel;//Wont do anything I think since pat vel is set to gravity

                if (patsHasBeenCreated == false) newWPats.Add(wPat);//Random pat order
                else wpt_job.waterPatsWrite[spawnI] = wPat;
            }
        }

        private void Dealloate()
        {
            TickWaterParticelsEnd(Time.deltaTime);//Make sure job aint running

            if (wpt_job.waterPatsWrite.IsCreated == true) wpt_job.waterPatsWrite.Dispose();
            if (wpt_job.waterCols.IsCreated == true) wpt_job.waterCols.Dispose();
            if (wpt_job.waterPatPoss.IsCreated == true) wpt_job.waterPatPoss.Dispose();
            if (wpt_job.cellIdToPatIndex.IsCreated == true) wpt_job.cellIdToPatIndex.Dispose();
            if (wpt_job.waterPatLowestMidHighestY.IsCreated == true) wpt_job.waterPatLowestMidHighestY.Dispose();
            if (commandBuf.IsValid() == true)
            {
                commandBuf.Release();
                commandBuf.Dispose();
            }
            if (possBuf.IsValid() == true)
            {
                possBuf.Release();
                possBuf.Dispose();
            }
        }

        public struct WaterCol
        {
            public Vector2 pos;
            public Vector2 dirForwardLenght;
            public Vector2 dirSideLenght;
            public bool isActive;

            public Vector2 center;
            public float sqrRadius;

#if !FLUID_NORBBUOYANCY
            public Vector2 vel;
            public Vector2 totalForcePos;
            public int forceCount;
#endif
        }
        #endregion Set Particels

        #region Handle Water Colliders

        /// <summary>
        /// 0 = remove, 1 = add, 2 = update
        /// </summary>
        private Dictionary<WaterColliderBox, bool> WaterColliderToShouldAdd = new();
        private HashSet<int> emptyWaterColIndexs = new();

        private void SetupWaterColliders()
        {
            for (int i = 0; i < WaterPhyGlobals.maxWaterColliders; i++)
            {
                emptyWaterColIndexs.Add(i);
            }
        }

        public void AddOrUpdateWaterCollider(WaterColliderBox wCol)
        {
            if (WaterColliderToShouldAdd.ContainsKey(wCol) == true) return;
            WaterColliderToShouldAdd[wCol] = true;
        }

        public void RemoveWaterCollider(WaterColliderBox wCol)
        {
            WaterColliderToShouldAdd[wCol] = false;
        }

        private void DoAddOrRemoveWaterColliders()
        {
            foreach (var wColShouldAdd in WaterColliderToShouldAdd)
            {
                WaterColliderBox waterCollider = wColShouldAdd.Key;

                if (wColShouldAdd.Value == true)
                {
                    //Add water collider
                    if (waterCollider.wColIndex >= 0)
                    {
                        //Update collider
                        wpt_job.waterCols[waterCollider.wColIndex] = waterCollider.ToWaterCol();
                        continue;
                    }

                    if (emptyWaterColIndexs.Count == 0)
                    {
                        Debug.LogError("No empty waterCol index, cant add " + waterCollider.transform.name);
                        continue;
                    }

                    waterCollider.wColIndex = emptyWaterColIndexs.FirstOrDefault();
                    emptyWaterColIndexs.Remove(waterCollider.wColIndex);
#if !FLUID_NORBBUOYANCY
                    waterColsRb[waterCollider.wColIndex] = waterCollider.TryGetWaterColRb();
#endif
                    wpt_job.waterCols[waterCollider.wColIndex] = waterCollider.ToWaterCol();
                }
                else
                {
                    //Remove collider
                    if (waterCollider.wColIndex < 0) continue;

                    emptyWaterColIndexs.Add(waterCollider.wColIndex);
                    wpt_job.waterCols[waterCollider.wColIndex] = new() { isActive = false };
                    waterCollider.wColIndex = -1;
                }
            }

            WaterColliderToShouldAdd.Clear();
        }

        #endregion Handle Water Colliders



        #region Handle Water Particels

        private struct WaterPat
        {
            public Vector2 pos;
            public Vector2 previous_pos;
            public float rho;
            public float rho_near;
            public float press;
            public float press_near;
            public FixedList128Bytes<short> neighbours;
            public Vector2 vel;
            public Vector2 force;
            public float speed;
        }

        private bool waterPatsIsTicking = false;

        private void TickWaterParticelsStart(float deltaTime)
        {
            if (waterPatsIsTicking == true) return;
            waterPatsIsTicking = true;

            //Prepare job
            wpt_job.deltaTime = deltaTime;

            //Run job
            wpt_handle = wpt_job.Schedule();
        }

        /// <summary>
        /// x == lowest Y any water particle has, y == particels average Y, z == highest Y any water particle has
        /// </summary>
        public Vector3 waterPatLowestMidHighestY;

        private void TickWaterParticelsEnd(float deltaTime)
        {
            if (waterPatsIsTicking == false) return;
            waterPatsIsTicking = false;

            //Complete job
            wpt_handle.Complete();

            //Update water pats rendering
            possBuf.SetData(wpt_job.waterPatPoss);
            waterPatLowestMidHighestY = wpt_job.waterPatLowestMidHighestY.Value;

            //Update water colliders
#if !FLUID_NORBBUOYANCY
            UpdateWaterCollidersRigidbodies(deltaTime);
#endif
            DoAddOrRemoveWaterColliders();
        }

#if !FLUID_NORBBUOYANCY
        /// <summary>
        /// Updates rigidbodies buoyancy (Only exists if FLUID_NORBBUOYANCY is not defined)
        /// </summary>
        private void UpdateWaterCollidersRigidbodies(float deltaTime)
        {
            for (int i = 0; i < WaterPhyGlobals.maxWaterColliders; i++)
            {
                //Get if waterCol with rb
                if (waterColsRb[i] == null) continue;

                var wCol = wpt_job.waterCols[i];
                if (wCol.isActive == false)
                {
                    waterColsRb[i].waterPatCountTouching = 0;
                    continue;
                }

                Rigidbody2D rb = waterColsRb[i].rb;
                waterColsRb[i].waterPatCountTouching = wCol.forceCount;
                if (wCol.forceCount <= 0) goto SkipAddRbForce;
                
                //Add force to rb
                Vector2 forcePos = wCol.totalForcePos / wCol.forceCount;

                //waterColsRb[i].AddForceAtPosition((WaterSimConfig.WALL_GRAVITYPUSHFORCE * wCol.forceCount)
                //    + (WaterSimConfig.WALL_BUOYANCY * wCol.forceCount * (waterColsRb[i].position - forcePos).normalized), forcePos, ForceMode2D.Force);
                rb.AddForceAtPosition(WaterSimConfig.WALL_GRAVITYPUSHFORCE * wCol.forceCount, forcePos, ForceMode2D.Force);
                rb.AddForce(WaterSimConfig.WALL_BUOYANCY * wCol.forceCount//Seems to work better if BUOYANCY is added using AddForce
                    * (rb.position - forcePos).normalized, ForceMode2D.Force);
                //rb.angularVelocity -= rb.angularVelocity * WaterSimConfig.WALL_ANGULARDAMPING * deltaTime * wCol.forceCount;

                //Reset waterCol rb
                wCol.forceCount = 0;
                wCol.totalForcePos = Vector2.zero;//We could set forceCount and totalForcePos in burst job however since 99% of all colliders wont have an rb checking for rb twice is not worth it

            SkipAddRbForce:;//We wont need to reset forceCount or totalForcePos if forceCount was 0
                wCol.vel = rb.velocity;
                wpt_job.waterCols[i] = wCol;

            }
        }
#endif

        #region RenderWaterPats

        GraphicsBuffer commandBuf;
        GraphicsBuffer.IndirectDrawIndexedArgs[] commandData;
        ComputeBuffer possBuf;
        RenderParams rendParams;

        private void SetupWaterPatsRendering()
        {
            commandBuf = new GraphicsBuffer(GraphicsBuffer.Target.IndirectArguments, 1, GraphicsBuffer.IndirectDrawIndexedArgs.size);
            commandData = new GraphicsBuffer.IndirectDrawIndexedArgs[1];
            possBuf = new ComputeBuffer(WaterPhyGlobals.patCount, sizeof(float) * 2);
            possBuf.SetData(wpt_job.waterPatPoss);

            rendParams = new RenderParams(wPat_mat)
            {
                worldBounds = new Bounds(Vector3.zero, 100000 * Vector3.one),
                matProps = new MaterialPropertyBlock(),
                camera = waterCamera.waterCam,
                layer = waterLayer
            };

            if (waterCamera.waterCam.cullingMask != 1 << waterLayer)
            {
                Debug.Log(waterCamera.transform.name + " camera did not have layer " + waterLayer + " as the only rendered layer");
                waterCamera.waterCam.cullingMask = 1 << waterLayer;
            }

            if (waterCamera.waterCam.clearFlags != CameraClearFlags.SolidColor || waterCamera.waterCam.backgroundColor != Color.black)
            {
                Debug.Log(waterCamera.transform.name + " camera did not have clearFlags set to black solid color");
                waterCamera.waterCam.clearFlags = CameraClearFlags.SolidColor;
                waterCamera.waterCam.backgroundColor = Color.black;
            }

            Camera mainCam = Camera.main;
            if (mainCam != null && mainCam != waterCamera.waterCam)
            {
                if (waterCamera.waterCam.depth >= mainCam.depth)
                {
                    Debug.LogWarning(waterCamera.transform.name + " does not have a lower depth than " + mainCam.transform.name
                        + ", this will likely cause the water to not render porperly");
                }

                if (mainCam.clearFlags != CameraClearFlags.Nothing)
                {
                    Debug.LogWarning(mainCam.transform.name + " does not have clearFlags set to Nothing, this will likely cause the water to not render");
                }
            }

            rendParams.matProps.SetBuffer("_waterPatPoss", possBuf);
            commandData[0].indexCountPerInstance = wPat_mesh.GetIndexCount(0);
            commandData[0].instanceCount = WaterPhyGlobals.patCount;
            commandBuf.SetData(commandData);
        }

        private void TickRenderWaterPats()
        {
            Graphics.RenderMeshIndirect(rendParams, wPat_mesh, commandBuf, 1);
        }

        #endregion RenderWaterPats

        [BurstCompile]
        private struct WaterPatTick_work : IJob
        {
            public float deltaTime;
            public NativeArray<WaterCol> waterCols;
            public NativeArray<WaterPat> waterPatsWrite;
            public NativeArray<Vector2> waterPatPoss;
            public NativeHashMap<Vector2Int, FixedList128Bytes<short>> cellIdToPatIndex;//Change FixedList128Bytes to FixedList512Bytes if list too small
            public NativeReference<Vector3> waterPatLowestMidHighestY;

            public void Execute()
            {
                //The fluid simulation is based on Smoothed Particle Hydrodynamics (SPH) described by Brandon Pelfrey
                //https://web.archive.org/web/20090722233436/http://blog.brandonpelfrey.com/?p=303

                //Get particle data
                cellIdToPatIndex.Clear();
                float self_speed;
                Vector2Int cellI = new();
                float patLowestY = float.MaxValue;
                float patHighestY = float.MinValue;
                float patTotalY = 0.0f;

                for (short i = 0; i < WaterPhyGlobals.patCount; i++)
                {
                    var p = waterPatsWrite[i];

                    // Reset previous position
                    Vector2 self_vel = p.vel;
                    Vector2 self_pos = p.pos;
                    Vector2 self_prevPos = self_pos;

                    #region Water Colliders
                    {
                        //Do waterCols collision, we simply loop through all colliders and get closest point (Wont ever be that many waterCols so a structure like kd-tree is not worth it)
                        Vector2 lineStart;
                        Vector2 line;
                        Vector2 lineNormal;
                        Vector2 closePos;
                        Vector2 closeDirLenght;
                        float bestDisSQR;
                        float closeDisSQR;
                        bool bestIsInside;
                        Vector2 bestDir;
                        Vector2 newVelDir;

                        //foreach (WaterCol wCol in waterCols)
                        for (int ii = 0; ii < WaterPhyGlobals.maxWaterColliders; ii++)
                        {
                            var wCol = waterCols[ii];
                            if (wCol.isActive == false) continue;
                            if ((wCol.center - self_pos).sqrMagnitude > wCol.sqrRadius) continue;
                            //if (wCol.forceCount > -1 && self_pos.y > wCol.center.y) continue;

                            //Reset ComputeSquare
                            bestIsInside = false;
                            bestDisSQR = float.MaxValue;
                            bestDir = self_pos;

                            //Top
                            lineStart = wCol.pos + wCol.dirSideLenght;
                            line = wCol.dirForwardLenght;
                            lineNormal = wCol.dirSideLenght.normalized;
                            ComputeEdgeInSquare();

                            //Side Start
                            line = -wCol.dirSideLenght * 2.0f;
                            lineNormal = -wCol.dirForwardLenght.normalized;
                            ComputeEdgeInSquare();

                            //Bottom
                            lineStart = (wCol.pos + wCol.dirForwardLenght) - wCol.dirSideLenght;
                            line = -wCol.dirForwardLenght;
                            lineNormal = -wCol.dirSideLenght.normalized;
                            ComputeEdgeInSquare();

                            //Side End
                            line = wCol.dirSideLenght * 2.0f;
                            lineNormal = wCol.dirForwardLenght.normalized;
                            ComputeEdgeInSquare();

                            //Make sure waterPat is outside collider
                            if (bestIsInside == true)
                            {
                                self_pos += bestDir * ((float)Math.Sqrt(bestDisSQR) + WaterPhyGlobals.waterRadius);
                                //if (wCol.forceCount > -1) self_prevPos = self_pos;

                                newVelDir = self_vel.normalized;

                                float dotP = -Vector2.Dot(newVelDir, bestDir);
                                //if (dotP < 0.0f) dotP = 0.0f;
                                self_vel = (newVelDir - (dotP * bestDir)) * self_vel.magnitude

                                //self_vel = (((newVelDir - (dotP * bestDir))//The bounce thing fixes particels stacking up next to walls
                                //    - (bestDir / Math.Max(self_vel.magnitude, WaterSimConfig.WALL_MINBOUNCEVEL) * WaterSimConfig.WALL_BOUNCENESS)) * self_vel.magnitude)
#if !FLUID_NORBBUOYANCY
                                + wCol.vel
#endif
                                    ;

#if !FLUID_NORBBUOYANCY
                                if (wCol.forceCount > -1 && self_pos.y < wCol.center.y)//Its negative if wCol dont have rb, which is 99% of the time
                                {
                                    //wCol.totalForcePos += self_pos - newVelDir;
                                    wCol.totalForcePos += self_pos - p.vel - newVelDir;
                                    wCol.forceCount++;
                                    waterCols[ii] = wCol;
                                }
#endif
                            }
                            else if (bestDisSQR < WaterPhyGlobals.waterRadiusSQR)
                            {
                                self_pos -= bestDir * (WaterPhyGlobals.waterRadius - (float)Math.Sqrt(bestDisSQR));
                                //if (wCol.forceCount > -1) self_prevPos = self_pos;

                                newVelDir = self_vel.normalized;

                                float dotP = Vector2.Dot(newVelDir, bestDir);
                                //if (dotP < 0.0f) dotP = 0.0f;
                                self_vel = (newVelDir - (dotP * bestDir)) * self_vel.magnitude

                                //self_vel = (((newVelDir - (dotP * bestDir))//The bounce thing fixes particels stacking up next to walls
                                //    - (bestDir / Math.Max(self_vel.magnitude, WaterSimConfig.WALL_MINBOUNCEVEL) * WaterSimConfig.WALL_BOUNCENESS)) * self_vel.magnitude)
#if !FLUID_NORBBUOYANCY
                                    + wCol.vel
#endif
                                    ;

#if !FLUID_NORBBUOYANCY
                                if (wCol.forceCount > -1 && self_pos.y < wCol.center.y)//Its negative if wCol dont have rb, which is 99% of the time
                                {
                                    //wCol.totalForcePos += self_pos - newVelDir;//I have no idea why offseting the position by p.vel helps
                                    wCol.totalForcePos += self_pos - p.vel - newVelDir;
                                    wCol.forceCount++;
                                    waterCols[ii] = wCol;
                                }
#endif
                            }
                        }

                        void ComputeEdgeInSquare()
                        {
                            closePos = lineStart + (Mathf.Clamp01(Vector2.Dot(self_pos - lineStart, line) / line.sqrMagnitude) * line);
                            closeDirLenght = closePos - self_pos;
                            closeDisSQR = closeDirLenght.sqrMagnitude;

                            if (closeDisSQR < bestDisSQR)
                            {
                                bestDisSQR = closeDisSQR;
                                bestDir = closeDirLenght.normalized;
                                bestIsInside = Vector3.Dot(lineNormal, bestDir) > 0.2f;
                            }
                        }
                    }
                    #endregion Water Colliders

                    #region Update state
                    //Update particle velocity
                    self_vel += deltaTime * WaterSimConfig.TS * p.force;
                    self_pos += deltaTime * WaterSimConfig.TS * self_vel;
                    self_vel = (self_pos - self_prevPos) / deltaTime / WaterSimConfig.TS;
                    self_speed = self_vel.magnitude;

                    if (self_speed > WaterSimConfig.MAX_VEL)
                    {
                        self_vel = self_vel.normalized * WaterSimConfig.MAX_VEL;
                    }

                    //Grid structure
                    cellI.x = (int)(self_pos.x / WaterPhyGlobals.gridCellSize);
                    cellI.y = (int)(self_pos.y / WaterPhyGlobals.gridCellSize);

                    if (cellIdToPatIndex.TryGetValue(cellI, out var List) == false)
                    {
                        List.Add(i);
                        cellIdToPatIndex.Add(cellI, List);
                    }
                    else
                    {
                        var list = cellIdToPatIndex[cellI];
                        if (List.Length < 63)
                        {
                            list.Add(i);
                            cellIdToPatIndex[cellI] = list;
                        }
                    }

                    //Write and reset particle data
                    p.rho = 0.0f;
                    p.rho_near = 0.0f;
                    p.neighbours.Clear();

                    p.vel = self_vel;
                    p.previous_pos = self_prevPos;
                    p.force = WaterSimConfig.Gravity;
                    p.speed = self_speed;
                    p.pos = self_pos;

                    waterPatPoss[i] = self_pos;
                    waterPatsWrite[i] = p;

                    //Get lowestMidHighest
                    patTotalY += self_pos.y;
                    if (self_pos.y < patLowestY) patLowestY = self_pos.y;
                    if (self_pos.y > patHighestY) patHighestY = self_pos.y;

                    #endregion Update state
                }

                //Update lowestMidY
                waterPatLowestMidHighestY.Value = new Vector3(patLowestY, patTotalY / WaterPhyGlobals.patCount, patHighestY);

                #region Calculate Density
                //Calculate Density
                var _waterPatsWrite = waterPatsWrite;//For access in LoopWaterPatsIn()

                for (int i = 0; i < WaterPhyGlobals.patCount; i++)
                {
                    var p = waterPatsWrite[i];
                    float density = 0.0f;
                    float density_near = 0.0f;
                    int nCount = 0;

                    cellI.x = (int)(p.pos.x / WaterPhyGlobals.gridCellSize);
                    cellI.y = (int)(p.pos.y / WaterPhyGlobals.gridCellSize);

                    if (cellIdToPatIndex.TryGetValue(cellI, out var nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.x++; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.y--; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.x--; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.x--; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.y++; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.y++; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.x++; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();
                    cellI.x++; if (cellIdToPatIndex.TryGetValue(cellI, out nearPatsToLoop) == true) LoopWaterPatsIn();

                    p.rho += density;//density and density_near may not be needed, looks almost the same without it
                    p.rho_near += density_near;

                    waterPatsWrite[i] = p;

                    void LoopWaterPatsIn()
                    {
                        foreach (short ii in nearPatsToLoop)
                        {
                            var n = _waterPatsWrite[ii];

                            float dist = (p.pos - n.pos).sqrMagnitude;//Sqr distance can be used for distance checks, we only perform sqrt if this particle is close

                            if (dist < WaterSimConfig.R_SQR)
                            {
                                //Self will be included in neighbours, feels wrong. But it seems to also be the case in the paper
                                float norDis = 1 - (float)Math.Sqrt(dist) / WaterSimConfig.R;
                                float norDisSquare2 = norDis * norDis;
                                float norDisSquare3 = norDis * norDis * norDis;
                                p.rho += norDisSquare2;
                                p.rho_near += norDisSquare3;
                                n.rho += norDisSquare2;
                                n.rho_near += norDisSquare3;
                                density += norDisSquare2;
                                density_near += norDisSquare3;

                                //Store particle neighbours for later use
                                if (nCount > 62) break;

                                p.neighbours.Add(ii);
                                nCount++;
                                _waterPatsWrite[ii] = n;
                            }
                        }
                    }
                }
                #endregion Calculate Density

                #region Calculate Pressure
                //Calculate Pressure
                for (int i = 0; i < WaterPhyGlobals.patCount; i++)
                {
                    var p = waterPatsWrite[i];
                    p.press = WaterSimConfig.K * (p.rho - WaterSimConfig.REST_DENSITY);
                    p.press_near = WaterSimConfig.K_NEAR * p.rho_near;

                    waterPatsWrite[i] = p;
                }
                #endregion Calculate Pressure

                #region Create Pressure
                //Create Pressure
                for (int i = 0; i < WaterPhyGlobals.patCount; i++)
                {
                    var p = waterPatsWrite[i];
                    Vector2 pressForce = Vector2.zero;

                    for (int ii = 0; ii < p.neighbours.Length; ii++)
                    {
                        var n = waterPatsWrite[p.neighbours[ii]];
                        float norDis = 1 - (p.pos - n.pos).magnitude / WaterSimConfig.R;
                        Vector2 pressVec = ((p.press + n.press) * norDis * norDis + (p.press_near + n.press_near) * norDis * norDis * norDis) * (n.pos - p.pos).normalized;

                        n.force += pressVec;
                        pressForce += pressVec;
                        waterPatsWrite[p.neighbours[ii]] = n;
                    }

                    p.force -= pressForce;

                    waterPatsWrite[i] = p;
                }
                #endregion Create Pressure

                #region Calculate Viscosity
                //Calculate Viscosity
                for (int i = 0; i < WaterPhyGlobals.patCount; i++)
                {
                    var p = waterPatsWrite[i];

                    for (int ii = 0; ii < p.neighbours.Length; ii++)
                    {
                        var n = waterPatsWrite[p.neighbours[ii]];
                        Vector2 patToN = n.pos - p.pos;
                        Vector2 patToN_nor = patToN.normalized;
                        float velDiff = Vector2.Dot(p.vel - n.vel, patToN_nor);

                        if (velDiff > 0.0f)
                        {
                            Vector2 viscoForce = (1 - (patToN.magnitude / WaterSimConfig.R)) * velDiff * WaterSimConfig.SIGMA * patToN_nor;
                            p.vel -= viscoForce * 0.5f;
                            n.vel += viscoForce * 0.5f;

                            waterPatsWrite[p.neighbours[ii]] = n;
                        }
                    }

                    waterPatsWrite[i] = p;
                }
                #endregion Calculate Viscosity
            }
        }

        #endregion Handle Water Particels

#if UNITY_EDITOR
        //debug stopwatch
        private System.Diagnostics.Stopwatch stopwatch = new();

        public void Debug_toggleTimer(string note = "")
        {
            if (stopwatch.IsRunning == false)
            {
                stopwatch.Restart();
            }
            else
            {
                stopwatch.Stop();
                Debug.Log(note + " time: " + stopwatch.Elapsed.TotalMilliseconds + "ms");
            }
        }
#endif
    }
}