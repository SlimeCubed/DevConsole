using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using static DevConsole.GameConsole;
using ObjectType = AbstractPhysicalObject.AbstractObjectType;
using CreatureType = CreatureTemplate.Type;
using UnityEngine;

namespace DevConsole
{
    // Helper methods for selecting objects from the console
    internal class Selection
    {
        public static IEnumerable<AbstractPhysicalObject> SelectAbstractObjects(RainWorldGame game, string arg)
        {
            var args = arg?.Split(',');

            if (args == null || args.Length == 0)
            {
                return FindBaseAbstractObjects(game, "me");
            }
            else
            {
                // Find a base group to select from
                // If only a filter is specified, filter from all
                int filterIndex = 1;
                var objs = FindBaseAbstractObjects(game, args[0]);
                if (objs == null)
                {
                    if (GetAbstractObjectFilter(game, args[0]) == null)
                    {
                        WriteLine($"Unknown selector: \"{args[0]}\"");
                        return new AbstractPhysicalObject[0];
                    }
                    else
                    {
                        filterIndex = 0;
                        objs = FindBaseAbstractObjects(game, "all");
                    }
                }

                // Filter based on the args that come after
                for (; filterIndex < args.Length; filterIndex++)
                {
                    var filter = GetAbstractObjectFilter(game, args[filterIndex]);
                    if(filter == null)
                    {
                        WriteLine($"Unknown selection filter: {args[filterIndex]}");
                    }
                    else
                    {
                        objs = objs.Where(obj => filter(game, obj));
                    }
                }

                return objs;
            }
        }

        private static IEnumerable<AbstractPhysicalObject> FindBaseAbstractObjects(RainWorldGame game, string arg)
        {
            if (string.IsNullOrEmpty(arg)) arg = "me";
            var firstPlayer = game.Players.Count == 0 ? null : game.Players[0];

            switch (arg.ToLower())
            {
                case "me":
                    if (firstPlayer == null)
                    {
                        WriteLine("Could not find any players to select!");
                        return new AbstractPhysicalObject[0];
                    }
                    else
                    {
                        if (firstPlayer?.realizedObject is not Player)
                        {
                            WriteLine("Player 1 must be realized to select!");
                            return new AbstractPhysicalObject[0];
                        }
                        return new[] { firstPlayer };
                    }

                case "players":
                    return game.Players.Select(crit => (AbstractPhysicalObject)crit);

                case "all":
                    return game.world.abstractRooms
                        .SelectMany(room => room.entities
                            .Select(ent => ent as AbstractPhysicalObject)
                            .Where(apo => apo != null)
                        );

                case "none":
                    return new AbstractPhysicalObject[0];

                case "room":
                    var list = new List<AbstractPhysicalObject>();
                    var room = SpawnRoom?.abstractRoom ?? firstPlayer?.Room;
                    if (room == null)
                    {
                        WriteLine("Could not find a room to target!");
                    }
                    else
                    {
                        foreach (var entity in room.entities)
                        {
                            if (entity is AbstractPhysicalObject o && o.Room.index == room.index)
                            {
                                list.Add(o);
                            }
                        }
                    }
                    return list;

                default:
                    if (ParseEntityID(arg) is EntityID id)
                    {
                        var ret = FindAbstractObjectByID(game, firstPlayer?.Room, id);
                        if (ret == null)
                        {
                            WriteLine("Found no entities in this region with that ID!");
                            return new AbstractPhysicalObject[0];
                        }
                        return new[] { ret };
                    }
                    else
                    {
                        return null;
                    }
            }
        }

        private static readonly Regex parseFilter = new(@"^(?<type>[\w\s]+)((?<op>!=|<=|>=|=|<|>)(?<arg>.+))?$", RegexOptions.ExplicitCapture);
        private static Func<RainWorldGame, AbstractPhysicalObject, bool> GetAbstractObjectFilter(RainWorldGame game, string filter)
        {
            var match = parseFilter.Match(filter);
            var type = match.Groups["type"].Value.Trim();
            var op = match.Groups["op"].Value;
            var arg = match.Groups["arg"].Value;

            // Helper method that compares two operands using op
            bool Compare<T>(T a, T b) where T : IComparable<T>
            {
                int comp = a.CompareTo(b);
                switch (op)
                {
                    case "=": return comp == 0;
                    case "!=": return comp != 0;
                    case "<": return comp < 0;
                    case "<=": return comp <= 0;
                    case ">": return comp > 0;
                    case ">=": return comp >= 0;
                    default: return false;
                }
            }

            // Helper method that compares two equatable operands using op
            bool Equate<T>(T a, T b) where T : IEquatable<T>
            {
                bool eq = a.Equals(b);
                switch(op)
                {
                    case "<=":
                    case ">=":
                    case "=": return eq;
                    
                    case "<":
                    case ">":
                    case "!=": return !eq;

                    default: return false;
                };
            }

            static bool NullFilter(RainWorldGame game, AbstractPhysicalObject obj) => false;

            // Here's where the real logic happens
            // Generate a delegate that will filter using the specified type, operator, and argument
            switch (type)
            {
                // Filter by object type and creature type
                case "type":

                    if (op == "") goto missingOp;

                    try
                    {
                        var testType = (ObjectType)Enum.Parse(typeof(ObjectType), arg, true);
                        return (game, obj) => Equate((int)obj.type, (int)testType);
                    }
                    catch { }

                    try
                    {
                        var testType = (CreatureType)Enum.Parse(typeof(CreatureType), arg, true);
                        return (game, obj) =>
                        {
                            if (obj is not AbstractCreature crit) return false;
                            return Equate((int)crit.creatureTemplate.type, (int)testType);
                        };
                    }
                    catch { }

                    WriteLine($"Unknown object or creature type: \"{arg}\"");
                    return NullFilter;

                // Filter by distance
                case "dist":
                case "distance":

                    if (op == "") goto missingOp;

                    if(!float.TryParse(arg, out float dist))
                    {
                        WriteLine($"Selection filter argument \"{arg}\" is not a valid number!");
                        return NullFilter;
                    }
                    else
                    {
                        if (SpawnRoom == null) return NullFilter;

                        return (game, obj) => {
                            if(obj.Room.index != SpawnRoom.abstractRoom.index || obj.realizedObject == null) return false;
                            return Compare(Vector2.Distance(obj.realizedObject.firstChunk.pos, SpawnPos), dist);
                        };
                    }

                // Filter all non-creatures and all dead creatures
                case "alive":
                    if (op != "") goto extraneousOp;
                    return (game, obj) => (obj is AbstractCreature crit) && crit.state.alive;

                // Filter all non-creatures and all alive creatures
                case "dead":
                    if (op != "") goto extraneousOp;
                    return (game, obj) => (obj is AbstractCreature crit) && !crit.state.alive;

                // Filter all by default
                default:
                    WriteLine($"Unknown selection filter: \"{type}\"");
                    return NullFilter;
            }

        missingOp:
            WriteLine($"Selection filter \"{type}\" requires an operator!");
            return NullFilter;

        extraneousOp:
            WriteLine($"Selection filter \"{type}\" may not use operators!");
            return NullFilter;
        }

        private static EntityID? ParseEntityID(string arg)
        {
            if (arg.StartsWith("ID."))
            {
                try
                {
                    return EntityID.FromString(arg);
                }
                catch
                {
                    return null;
                }
            }
            else if (int.TryParse(arg, out int idNum))
            {
                return new EntityID(-1, idNum);
            }
            else
            {
                return null;
            }
        }

        private static AbstractPhysicalObject FindAbstractObjectByID(RainWorldGame game, AbstractRoom startSearch, EntityID id)
        {
            if (startSearch != null)
                foreach (var entity in startSearch.entities)
                    if (entity is AbstractPhysicalObject abstractPhysicalObject && id == entity.ID)
                        return abstractPhysicalObject;

            foreach (var room in game.world.abstractRooms)
                if (room != null && room != startSearch)
                    foreach (var entity in room.entities)
                        if (entity is AbstractPhysicalObject abstractPhysicalObject && id == entity.ID)
                            return abstractPhysicalObject;

            return null;
        }
    }
}
