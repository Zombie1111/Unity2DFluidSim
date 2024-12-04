//Unity2DFluidSim by David Westberg https://github.com/Zombie1111/Unity2DFluidSim
using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace Zomb2DPhysics
{
    //#################Optional Scripting Define Symbols (ProjectSettings>Player>OtherSettings)################
    //#########################################################################################################
    //FLUID_NORBBUOYANCY //If defined waterColliders with rigidbodies wont get force from the fluid sim


    public static class WaterPhyGlobals
    {
        public const float waterRadius = 0.4f;//The radius of each waterParticel
        public const float waterRadiusSQR = waterRadius * waterRadius;//Square of waterRadius
        public const int maxWaterColliders = 100;//Maximum number of active water colliders
        public const int patCount = 1000;//Number of water particels
    }

    public static class WaterSimConfig
    {
        public static readonly Vector2 Gravity = new(0.0f, -0.005f);//Gravity force

        public const float TS = 50.0f;//Timestep, Time.FixedTimestep * TS == 1.0f
        public const float SPACING = 1.0f;//The target distance between each particle
        public const float K = SPACING / 1000.0f;//Pressure factor
        public const float K_NEAR = K * 20.0f;//Pressure factor when particels are compressed
        public const float REST_DENSITY = 3.0f;
        public const float R = SPACING * 1.25f;//If the distance between two particles is less than R, they are neighbours
        public const float R_SQR = R * R;//Square of R
        public const float SIGMA = 0.2f;//Viscosity factor
        public const float MAX_VEL = 0.35f;//The maximum velocity a particle can have

#if !FLUID_NORBBUOYANCY
        public const float WALL_BUOYANCY = 400.0f;//How much force each particle can add to waterColliders that has a rigidbody
        public static readonly Vector2 WALL_GRAVITYPUSHFORCE = new(0.0f, 100.0f);//How much upwards force each particle can add to waterColliders that has a rigidbody
#endif
        //Below is unused, see line ~498 in WaterPhy.cs.WaterPatTick_work
        public const float WALL_BOUNCENESS = 0.03f;//How much a particle "bounce" on walls
        public const float WALL_MINBOUNCEVEL = 0.1f;//The minimum velocity a particle can have when it bounce
    }

    public static class EnumerableExtensions
    {
        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source)
        {
            return source.Shuffle(new System.Random());
        }

        public static IEnumerable<T> Shuffle<T>(this IEnumerable<T> source, System.Random rng)
        {
            if (source == null) throw new ArgumentNullException(nameof(source));
            if (rng == null) throw new ArgumentNullException(nameof(rng));

            return source.ShuffleIterator(rng);
        }

        private static IEnumerable<T> ShuffleIterator<T>(
            this IEnumerable<T> source, System.Random rng)
        {
            var buffer = source.ToList();
            for (int i = 0; i < buffer.Count; i++)
            {
                int j = rng.Next(i, buffer.Count);
                yield return buffer[j];

                buffer[j] = buffer[i];
            }
        }
    }
}

