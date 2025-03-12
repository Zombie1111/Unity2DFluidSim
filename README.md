
<h1 align="center">Unity 2D Fluid Simulation By David Westberg</h1>

## Table of Contents
- [Overview](#overview)
- [Key Features](#key-features)
- [Instructions](#instructions)
- [Documentation](#documentation)
- [Technical Details](#technical-details)
- [License](#license)

## Overview
A fast 2D particle-based fluid simulation that does not rely on Unitys Rigidbodies or PhysX implementation, allowing the simulation to run entirely on a separate thread.

Used in my mobile game Flow Down:
[Google Play](https://play.google.com/store/apps/details?id=com.DefaultCompany.FlowDownGame)

<img src="https://media.giphy.com/media/bz9tQJq9TKJoqrRhv3/giphy.gif" width="100%" height="100%"/>

## Key Features
<ul>
<li>2D fluid simulation and rendering</li>
<li>Fluid interacts with 2D box shaped colliders and rigidbodies</li>
<li>Simulation runs entirely on a Burst-compiled job</li>
<li>Utilizes GPU instancing for fluid rendering</li>
</ul>

## Instructions
**Requirements** (Should work in other versions)
<ul>
<li>Unity 2023.2.20f1 (Built-in)</li>
<li>Compute Shader support</li>
<li>Burst 1.8.18</li>
<li>Collections 2.1.4</li>
</ul>

**General Setup**

<ol>
  <li>Download and copy the <code>_Demo</code>, <code>Materials</code> and <code>Scripts</code> folders into an empty folder inside your <code>Assets</code> directory</li>
  <li>Create a new empty scene and add the prefab found at <code>_Demo/2DWaterSetup</code> to it</li>
  <li>Create a Quad gameobject and add the <code>WaterColliderBox.cs</code> script to it, position the new gameobject so its visible in Game view</li>
  <li>Enter playmode and you should now have a 2D fluid simulation that can collide with the Quad</li>
</ol>

(The fluid simulation itself does not rely heavily on Unity, porting this to non Unity enviroments should therefore not require too much work)

## Documentation
Most functions are documented and all parameters visible in the Unity inspector have tooltips

The `_Demo/` folder contains pratical exampels

## Technical Details
The fluid simulation is based on Smoothed Particle Hydrodynamics (SPH) described by Brandon Pelfrey https://web.archive.org/web/20090722233436/http://blog.brandonpelfrey.com/?p=303

## License
Unity2DFluidSim © 2024 by David Westberg is licensed under CC BY 4.0 - See the `LICENSE` file for more details.

