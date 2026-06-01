using System;
using System.Collections.Generic;
using UnityEngine;

namespace ithappy
{
    public enum CarElementName
    {
        None = 0,
        FrontBumper = 1,
        RearBumper = 2,
        Pipe = 3,
        Headlight = 4,
        Wheel = 5,
        Decals = 6,
        FogLight = 7,
        Engine = 8,
        Spoiler = 9,
        Racer = 10,
    }

    [Serializable]
    public class CarElementSettings
    {
        public CarElementName ElementName;
        public List<GameObject> Elements;
    }
}
