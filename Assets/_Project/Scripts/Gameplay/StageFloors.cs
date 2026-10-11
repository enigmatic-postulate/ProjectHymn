using System;
using System.Collections.Generic;
using ProjectHymn.Movement;
using UnityEngine;

namespace ProjectHymn.Gameplay
{
    /// <summary>The scene's floors (ground and one-way platforms) for enemies that hop between them (the brawl stage).</summary>
    public class StageFloors : MonoBehaviour
    {
        [Serializable]
        public struct Entry
        {
            public string name;
            public float xMin, xMax, top;
            public bool oneWay;
        }

        [SerializeField] List<Entry> floors = new List<Entry>();

        public static StageFloors Current { get; private set; }
        FloorMap _map;

        public FloorMap Map
        {
            get
            {
                if (_map != null) return _map;
                var list = new List<FloorMap.Floor>();
                foreach (var e in floors) list.Add(new FloorMap.Floor(e.name, e.xMin, e.xMax, e.top, e.oneWay));
                return _map = new FloorMap(list);
            }
        }

        public void Set(IEnumerable<Entry> entries)
        {
            floors = new List<Entry>(entries);
            _map = null;
        }

        void OnEnable() => Current = this;
        void OnDisable() { if (Current == this) Current = null; }
    }
}
